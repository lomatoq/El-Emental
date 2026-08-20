using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    public readonly struct EarthAudioResponse
    {
        public EarthAudioResponse(float body, float crack, float grit, float pitch)
        {
            Body = math.saturate(body);
            Crack = math.saturate(crack);
            Grit = math.saturate(grit);
            Pitch = math.clamp(pitch, 0.55f, 1.65f);
        }

        public float Body { get; }
        public float Crack { get; }
        public float Grit { get; }
        public float Pitch { get; }
    }

    public static class EarthAudioResponseSolver
    {
        public static EarthAudioResponse Impact(in EarthImpactEvent impact)
        {
            float mass = 1f - math.exp(-impact.Mass / 180f);
            float impulse = 1f - math.exp(-impact.Impulse / 850f);
            float energy = 1f - math.exp(-impact.KineticEnergy / 4200f);
            float speed = 1f - math.exp(-impact.RelativeSpeed / 15f);
            float materialBody = impact.Material is EarthImpactMaterialKind.HeavyBlock or
                EarthImpactMaterialKind.Structure or EarthImpactMaterialKind.Meteor ? 1f : 0.72f;
            return new EarthAudioResponse(
                (mass * 0.56f + impulse * 0.44f) * materialBody,
                impulse * 0.56f + energy * 0.44f,
                speed * 0.64f + energy * 0.36f,
                math.lerp(1.32f, 0.72f, mass));
        }

        public static EarthAudioResponse Return(in EarthReturnEvent value)
        {
            float mass = 1f - math.exp(-value.Mass / 140f);
            float volume = 1f - math.exp(-value.Volume / 1.8f);
            float stage = value.Stage switch
            {
                EarthReturnEventStage.Captured => 0.22f,
                EarthReturnEventStage.Subsurface => 0.68f,
                EarthReturnEventStage.CommitSubmitted => 0.84f,
                EarthReturnEventStage.Completed => 1f,
                EarthReturnEventStage.Reversed => 0.48f,
                EarthReturnEventStage.Jammed => 0.72f,
                _ => 0f
            };
            return new EarthAudioResponse(
                stage * (0.42f + mass * 0.58f),
                stage * volume * 0.42f,
                stage * (1f - mass) * 0.35f,
                math.lerp(1.08f, 0.68f, mass));
        }
    }

    /// <summary>
    /// Lightweight parameter-driven earth mix. Procedural fallback one-shots keep
    /// the causal body/crack/grit layers audible before bespoke recordings arrive.
    /// Gameplay events remain the sole authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthAudioDirector : MonoBehaviour
    {
        [SerializeField] private MagicExecutor executor;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.72f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.68f;

        private AudioSource _bodySource;
        private AudioSource _crackSource;
        private AudioSource _gritSource;
        private AudioClip _bodyClip;
        private AudioClip _crackClip;
        private AudioClip _gritClip;
        private bool _subscribed;

        public EarthAudioResponse LastResponse { get; private set; }

        public void Configure(MagicExecutor configuredExecutor)
        {
            Unsubscribe();
            executor = configuredExecutor;
            EnsureSources();
            if (isActiveAndEnabled) Subscribe();
        }

        private void Awake()
        {
            if (executor == null) executor = GetComponent<MagicExecutor>();
            EnsureSources();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            if (_bodyClip != null) Destroy(_bodyClip);
            if (_crackClip != null) Destroy(_crackClip);
            if (_gritClip != null) Destroy(_gritClip);
        }

        private void Subscribe()
        {
            if (_subscribed || executor == null) return;
            executor.Events.EarthImpactOccurred += OnImpact;
            executor.Events.EarthReturnOccurred += OnReturn;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || executor == null) return;
            executor.Events.EarthImpactOccurred -= OnImpact;
            executor.Events.EarthReturnOccurred -= OnReturn;
            _subscribed = false;
        }

        private void OnImpact(EarthImpactEvent value)
        {
            LastResponse = EarthAudioResponseSolver.Impact(in value);
            Vector3 point = ToVector3(value.Point);
            EarthAudioResponse response = LastResponse;
            PlayLayers(in response, point);
        }

        private void OnReturn(EarthReturnEvent value)
        {
            LastResponse = EarthAudioResponseSolver.Return(in value);
            EarthAudioResponse response = LastResponse;
            PlayLayers(in response, ToVector3(value.Position));
        }

        private void PlayLayers(in EarthAudioResponse response, Vector3 point)
        {
            EnsureSources();
            // The director lives on the shared Earth-magic root. Moving that root to an
            // impact point would drag every gameplay controller and pool with it. Only
            // the three disposable spatial emitters follow the authored event position.
            _bodySource.transform.position = point;
            _crackSource.transform.position = point;
            _gritSource.transform.position = point;
            Play(_bodySource, _bodyClip, response.Body * masterVolume, response.Pitch * 0.82f);
            Play(_crackSource, _crackClip, response.Crack * masterVolume, response.Pitch);
            Play(_gritSource, _gritClip, response.Grit * masterVolume, response.Pitch * 1.12f);
        }

        private void EnsureSources()
        {
            if (_bodySource != null) return;
            _bodySource = CreateSource("Earth Audio — Body");
            _crackSource = CreateSource("Earth Audio — Crack");
            _gritSource = CreateSource("Earth Audio — Grit");
            _bodyClip = CreateClip("Earth Procedural Body", 0, 0.42f);
            _crackClip = CreateClip("Earth Procedural Crack", 1, 0.18f);
            _gritClip = CreateClip("Earth Procedural Grit", 2, 0.28f);
        }

        private AudioSource CreateSource(string sourceName)
        {
            GameObject child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1.2f;
            source.maxDistance = 42f;
            source.dopplerLevel = 0.15f;
            return source;
        }

        private static void Play(AudioSource source, AudioClip clip, float volume, float pitch)
        {
            if (source == null || clip == null || volume <= 0.002f) return;
            source.pitch = Mathf.Clamp(pitch, 0.5f, 1.8f);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private static AudioClip CreateClip(string clipName, int layer, float seconds)
        {
            const int sampleRate = 22050;
            int count = Mathf.CeilToInt(sampleRate * seconds);
            var samples = new float[count];
            uint noise = 0xE17F0411u + (uint)layer * 0x9E3779B9u;
            float previousNoise = 0f;
            for (int index = 0; index < count; index++)
            {
                float t = index / (float)sampleRate;
                float envelope = Mathf.Exp(-t * (layer == 0 ? 8.5f : layer == 1 ? 22f : 13f));
                noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                float white = ((noise & 0xFFFFu) / 32767.5f) - 1f;
                float sample;
                if (layer == 0)
                    sample = (Mathf.Sin(t * Mathf.PI * 2f * 54f) * 0.72f + white * 0.12f) * envelope;
                else if (layer == 1)
                {
                    sample = (white - previousNoise * 0.68f) * envelope * 0.58f;
                    previousNoise = white;
                }
                else
                {
                    float grain = index % 337 < 8 ? white * 0.72f : white * 0.08f;
                    sample = grain * envelope;
                }
                samples[index] = Mathf.Clamp(sample, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(clipName, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
