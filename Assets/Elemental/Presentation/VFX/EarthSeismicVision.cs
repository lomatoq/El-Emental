using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Magic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Elemental.Presentation.VFX
{
    /// <summary>Local earth perception, composed over visible opaque depth by the world pass.</summary>
    [DefaultExecutionOrder(1700), DisallowMultipleComponent, RequireComponent(typeof(PlanetMotor))]
    public sealed class EarthSeismicVision : MonoBehaviour
    {
        public const int WaveCount = 16;
        private static readonly ProfilerMarker PublishMarker = new("Elemental.SeismicVision.Publish");
        private static readonly int ActiveId = Shader.PropertyToID("_EarthSeismicVision");
        private static readonly int MotionId = Shader.PropertyToID("_EarthSeismicMotion01");
        // Unity fixes a global array's capacity on its first upload, across domain
        // reloads. New property IDs avoid retaining the legacy five-pulse capacity.
        private static readonly int WavesId = Shader.PropertyToID("_EarthSeismicWaves16");
        private static readonly int StrengthsId = Shader.PropertyToID("_EarthSeismicStrengths16");
        private static readonly int RadiusTravelsId = Shader.PropertyToID("_EarthSeismicRadiusTravels16");
        private readonly Vector4[] _waves = new Vector4[WaveCount];
        private readonly float[] _strengths = new float[WaveCount];
        private readonly float[] _radiusTravels = new float[WaveCount];
        private readonly Pulse[] _pulses = new Pulse[WaveCount];
        private PlanetMotor _motor;
        private MagicInputController _input;
        private int _nextPulse;
        private float _nextAutomaticPulse;
        private Vector3 _lastStepPosition;
        private bool _published;
        private float _fadeProgress;
        private float _motion01;

        private struct Pulse
        {
            public Vector3 Origin;
            public float StartedAt, Radius, Duration, LastPublishedRadius;
        }

        public bool Requested { get; private set; }
        public float VisualBlend => Mathf.SmoothStep(0f, 1f, _fadeProgress);
        public bool IsActive { get; private set; }
        public int VisiblePulseCount { get; private set; }
        public int EmittedPulseCount => _nextPulse;

        private void Awake()
        {
            _motor = GetComponent<PlanetMotor>();
            _input = GetComponent<MagicInputController>();
            _lastStepPosition = transform.position;
        }

        private void Update()
        {
            if (_input != null && _input.isActiveAndEnabled && Keyboard.current != null &&
                Keyboard.current.vKey.wasPressedThisFrame)
                SetActive(!Requested);
            RefreshPerception(Time.deltaTime);
        }

        public void SetActive(bool active)
        {
            Requested = active;
            RefreshPerception(0f);
        }

        private void RefreshPerception(float deltaTime)
        {
            using (PublishMarker.Auto())
            {
                _fadeProgress = EarthSeismicPerception.AdvanceFade(_fadeProgress, Requested, deltaTime);
                bool eligible = EarthSeismicPerception.CanPerceive(true,
                    _input != null && _input.isActiveAndEnabled && _input.SelectedElement == ElementId.Earth,
                    _motor != null && _motor.HasStableSupport,
                    _motor != null && _motor.AcceptsMovingSupport,
                    _motor != null && _motor.IsMantling);
                if (!eligible)
                {
                    // A one-tick support miss on an arena seam must hide vision,
                    // not repeatedly erase its one-second toggle fade and waves.
                    if (_published) Shader.SetGlobalFloat(ActiveId, 0f);
                    if (!Requested && _fadeProgress <= 0f && _published) ClearPerception();
                    VisiblePulseCount = 0;
                    IsActive = false;
                    return;
                }

                bool entering = !_published;
                IsActive = Requested;
                if (!Requested && _fadeProgress <= 0f)
                {
                    if (_published) ClearPerception();
                    return;
                }
                float speed = Vector3.ProjectOnPlane(_motor.Body.linearVelocity, _motor.LocalUp).magnitude;
                _motion01 = Mathf.MoveTowards(_motion01, Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0.15f, 2f, speed)), deltaTime / 0.18f);
                float stepSpacing = Mathf.Lerp(0.72f, 2f, _motion01);
                if (IsActive && (entering || Time.unscaledTime >= _nextAutomaticPulse ||
                    Vector3.ProjectOnPlane(transform.position - _lastStepPosition, _motor.LocalUp).sqrMagnitude >= stepSpacing * stepSpacing))
                {
                    EmitPulse(_motor.SupportFeetPoint(_motor.LocalUp), _motor.LocalUp, 40f, 4f);
                    _lastStepPosition = transform.position;
                    _nextAutomaticPulse = Time.unscaledTime + 0.68f;
                }
                VisiblePulseCount = 0;
                for (int i = 0; i < WaveCount; i++)
                {
                    Pulse pulse = _pulses[i];
                    float age = Time.unscaledTime - pulse.StartedAt;
                    float strength = pulse.Duration > 0f ? EarthSeismicPerception.Strength(age, pulse.Duration) : 0f;
                    float currentRadius = EarthSeismicPerception.Radius(age, pulse.Radius, pulse.Duration);
                    float previousRadius = Mathf.Clamp(pulse.LastPublishedRadius, 0f, currentRadius);
                    _radiusTravels[i] = currentRadius - previousRadius;
                    pulse.LastPublishedRadius = currentRadius;
                    _pulses[i] = pulse;
                    _waves[i] = new Vector4(pulse.Origin.x, pulse.Origin.y, pulse.Origin.z, currentRadius);
                    _strengths[i] = strength;
                    if (strength > 0f) VisiblePulseCount++;
                }
                Shader.SetGlobalVectorArray(WavesId, _waves);
                Shader.SetGlobalFloatArray(StrengthsId, _strengths);
                Shader.SetGlobalFloatArray(RadiusTravelsId, _radiusTravels);
                Shader.SetGlobalFloat(ActiveId, VisualBlend);
                Shader.SetGlobalFloat(MotionId, _motion01);
                _published = true;
            }
        }

        public void EmitPulse(Vector3 origin, Vector3 up, float radius, float duration)
        {
            if (!IsActive) return;
            int slot = _nextPulse++ % WaveCount;
            _pulses[slot] = new Pulse
            {
                Origin = origin,
                StartedAt = Time.unscaledTime,
                Radius = Mathf.Max(0.1f, radius),
                Duration = Mathf.Max(0.15f, duration),
                LastPublishedRadius = 0f
            };
            _waves[slot] = new Vector4(origin.x, origin.y, origin.z, 0f);
            _radiusTravels[slot] = 0f;
        }

        private void ClearPerception()
        {
            _fadeProgress = 0f;
            _motion01 = 0f;
            Shader.SetGlobalFloat(ActiveId, 0f);
            Shader.SetGlobalFloat(MotionId, 0f);
            for (int i = 0; i < WaveCount; i++)
            {
                _pulses[i] = default;
                _strengths[i] = 0f;
                _radiusTravels[i] = 0f;
            }
            Shader.SetGlobalFloatArray(StrengthsId, _strengths);
            Shader.SetGlobalFloatArray(RadiusTravelsId, _radiusTravels);
            VisiblePulseCount = 0;
            _published = false;
        }

        private void OnDisable()
        {
            if (_published) ClearPerception();
            IsActive = false;
        }
    }
}
