using Elemental.Runtime.World;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Bending;
using Elemental.Presentation.Camera;
using Elemental.Input.Gestures;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class EarthMagicFeedback : MonoBehaviour
    {
        private readonly EarthCosmeticMaterialCache cosmeticMaterials = new();
        private void OnDestroy() => cosmeticMaterials.Dispose();
        private static readonly ProfilerMarker RouteMarker =
            new ProfilerMarker("Elemental.Earth.Feedback.Route");
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private ParticleSystem dust;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystem rubble;
        [SerializeField] private Light pulseLight;
        [SerializeField] private LineRenderer[] strainCracks;
        [SerializeField] private PlanetCameraRig cameraRig;
        [SerializeField] private MagicInputController input;
        [SerializeField] private EarthPillarWavePool wavePool;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private EarthFeedbackProfile impactFeedbackProfile;
        [SerializeField] private EarthEffectsTuningProfile effectsProfile;
        [SerializeField] private EarthMaterialFeedbackHub materialFeedback;
        public void ConfigureMaterialFeedback(EarthMaterialFeedbackHub hub) => materialFeedback = hub;

        private void Awake()
        {
            // Repair only known children of this authored feedback object.
            if (dust == null) dust = transform.Find("Chunky Earth Dust")?.GetComponent<ParticleSystem>();
            if (rubble == null) rubble = transform.Find("Loose Earth Chips")?.GetComponent<ParticleSystem>();
            if (sparks == null) sparks = transform.Find("Amber Shards")?.GetComponent<ParticleSystem>();
            ApplyEffectsProfile();
        }

        private float _pulse;
        private float _crackLife;
        private float _nextChargePulseTime;
        private float _nextWaveCameraPulse;
        private EarthFeedbackBatchAccumulator _impactBatch;

        public void ConfigureImpactProfile(EarthFeedbackProfile configuredProfile) =>
            impactFeedbackProfile = configuredProfile;

        public void ConfigureEffectsProfile(EarthEffectsTuningProfile configuredProfile)
        {
            effectsProfile = configuredProfile;
            ApplyEffectsProfile();
        }

        public void Configure(
            MagicExecutor configuredExecutor,
            ParticleSystem configuredDust,
            ParticleSystem configuredSparks,
            Light configuredPulseLight,
            LineRenderer[] configuredStrainCracks = null,
            ParticleSystem configuredRubble = null,
            PlanetCameraRig configuredCameraRig = null,
            MagicInputController configuredInput = null,
            EarthPillarWavePool configuredWavePool = null,
            Transform configuredPlanetCenter = null)
        {
            if (isActiveAndEnabled) Unsubscribe();
            executor = configuredExecutor;
            dust = configuredDust;
            sparks = configuredSparks;
            rubble = configuredRubble;
            pulseLight = configuredPulseLight;
            strainCracks = configuredStrainCracks;
            cameraRig = configuredCameraRig;
            input = configuredInput;
            wavePool = configuredWavePool;
            planetCenter = configuredPlanetCenter;
            ApplyEffectsProfile();
            HideCracks();
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
            ApplyEffectsProfile();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            using var marker = RouteMarker.Auto();
            FlushImpactBatch();
            if (pulseLight == null) return;
            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.deltaTime * 5f);
            pulseLight.intensity = _pulse * 4.5f;
            if (_crackLife > 0f)
            {
                _crackLife -= Time.deltaTime;
                if (_crackLife <= 0f) HideCracks();
            }
        }

        private void Subscribe()
        {
            if (executor != null)
            {
                executor.Events.TerrainEdited += OnTerrainEdited;
                executor.Events.WallRaised += OnWallRaised;
                executor.Events.WallCollapsed += OnWallCollapsed;
                executor.Events.FragmentSpawned += OnFragmentSpawned;
                executor.Events.FragmentLaunched += OnFragmentLaunched;
                executor.Events.ImpactOccurred += OnImpact;
                executor.Events.EarthImpactOccurred += OnEarthImpact;
                executor.Events.MagicPushed += OnMagicPushed;
                executor.Events.EarthReturnOccurred += OnEarthReturn;
            }
            if (input != null) input.PushChargeChanged += OnPushChargeChanged;
            if (wavePool != null) wavePool.ColumnBurst += OnWaveColumnBurst;
        }

        private void Unsubscribe()
        {
            if (executor != null)
            {
                executor.Events.TerrainEdited -= OnTerrainEdited;
                executor.Events.WallRaised -= OnWallRaised;
                executor.Events.WallCollapsed -= OnWallCollapsed;
                executor.Events.FragmentSpawned -= OnFragmentSpawned;
                executor.Events.FragmentLaunched -= OnFragmentLaunched;
                executor.Events.ImpactOccurred -= OnImpact;
                executor.Events.EarthImpactOccurred -= OnEarthImpact;
                executor.Events.MagicPushed -= OnMagicPushed;
                executor.Events.EarthReturnOccurred -= OnEarthReturn;
            }
            if (input != null) input.PushChargeChanged -= OnPushChargeChanged;
            if (wavePool != null) wavePool.ColumnBurst -= OnWaveColumnBurst;
        }

        private void OnTerrainEdited(TerrainEditedEvent value) =>
            Emit(new Vector3(value.Center.x, value.Center.y, value.Center.z), 14, 5);

        private void OnWallRaised(WallRaisedEvent value)
        {
            Vector3 start = new Vector3(value.Start.x, value.Start.y, value.Start.z);
            Vector3 end = new Vector3(value.End.x, value.End.y, value.End.z);
            Vector3 midpoint = (start + end) * 0.5f;
            Vector3 up = LocalUp(midpoint);
            Vector3 tangent = Vector3.ProjectOnPlane(end - start, up).normalized;
            Vector3 side = Vector3.Cross(tangent, up).normalized;
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera != null)
            {
                Vector3 cameraSide = Vector3.ProjectOnPlane(camera.transform.position - midpoint, up).normalized;
                if (cameraSide.sqrMagnitude > 0.5f) side = cameraSide;
            }
            for (int index = 0; index < 9; index++)
            {
                float t = index / 8f;
                Vector3 alongWall = Vector3.Lerp(start, end, t);
                // The wall itself is pooled. Short-lived irregular chips sell loose
                // soil being pushed aside without modifying voxel geometry.
                if (materialFeedback != null) materialFeedback.Emit(EarthMaterialFeedbackKind.Emerge,
                    alongWall + up * .10f, up, 1f, .35f, value.WallId, dustCount: 12, chipCount: 6);
                else Emit(alongWall + (up * 0.10f), 5, index % 2 == 0 ? 2 : 1);
                if (materialFeedback == null && rubble != null)
                {
                    rubble.transform.position = alongWall + (up * 0.32f) + (side * (0.48f + ((index % 3) * 0.10f)));
                    rubble.transform.rotation = Quaternion.FromToRotation(Vector3.up, up);
                    rubble.Emit(8);
                }
            }
            float height01 = Mathf.InverseLerp(1.25f, 10.5f, value.Height);
            cameraRig?.AddPresentationImpulse(
                Mathf.Lerp(0.085f, 0.19f, height01),
                Mathf.Lerp(0.46f, 0.76f, height01),
                value.WallId);
        }

        private void OnWallCollapsed(WallCollapsedEvent value)
        {
            Vector3 start = new Vector3(value.Start.x, value.Start.y, value.Start.z);
            Vector3 end = new Vector3(value.End.x, value.End.y, value.End.z);
            Vector3 midpoint = (start + end) * 0.5f;
            Vector3 up = LocalUp(midpoint);
            for (int index = 0; index < 7; index++)
            {
                Vector3 point = Vector3.Lerp(start, end, index / 6f) + (up * value.Height * 0.22f);
                if (materialFeedback != null) materialFeedback.Emit(EarthMaterialFeedbackKind.Fracture,
                    point, up, 1f, .35f, value.WallId, dustCount: 16, chipCount: 6);
                else Emit(point, 4, index % 2 == 0 ? 1 : 0);
                if (materialFeedback == null && rubble != null)
                {
                    rubble.transform.position = point;
                    rubble.transform.rotation = Quaternion.FromToRotation(Vector3.up, up);
                    rubble.Emit(5);
                }
            }
            cameraRig?.AddPresentationImpulse(0.13f, 0.48f, value.WallId ^ 0xC011A95Eu);
        }

        private void OnFragmentSpawned(FragmentSpawnedEvent value)
        {
            Vector3 anchor = new Vector3(value.SurfaceAnchor.x, value.SurfaceAnchor.y, value.SurfaceAnchor.z);
            if (materialFeedback != null) materialFeedback.Emit(EarthMaterialFeedbackKind.Extract,
                anchor, LocalUp(anchor), 1f, .5f, value.FragmentId, dustCount: 40, chipCount: 12);
            else Emit(anchor, 20, 9);
            ShowCracks(value);
            cameraRig?.AddPresentationImpulse(0.075f, 0.34f, value.FragmentId);
        }

        private void OnFragmentLaunched(FragmentLaunchedEvent value)
        {
            Emit(new Vector3(value.Position.x, value.Position.y, value.Position.z), 8, 16);
            cameraRig?.AddPresentationImpulse(0.045f, 0.18f, value.FragmentId ^ 0xF11C7u);
        }

        private void OnImpact(ImpactEvent value)
        {
            if (effectsProfile != null || impactFeedbackProfile != null) return;
            Emit(new Vector3(value.Point.x, value.Point.y, value.Point.z),
                Mathf.Clamp(Mathf.RoundToInt(value.Impulse * 0.025f), 18, 72), 24);
            cameraRig?.AddPresentationImpulse(
                Mathf.Clamp(value.Impulse * 0.00075f, 0.04f, 0.24f),
                0.38f,
                value.FragmentId ^ 0x1A4AC7u);
        }

        private void OnEarthImpact(EarthImpactEvent value)
        {
            if (effectsProfile == null) return;
            EarthImpactEffectsSample evaluated = effectsProfile.EvaluateImpact(in value);
            if (materialFeedback != null) materialFeedback.Emit(EarthMaterialFeedbackKind.Impact,
                value.Point, value.Normal, 1f, .35f, dustCount: evaluated.DustCount, chipCount: evaluated.RubbleCount);
            var sample = new EarthFeedbackSample(evaluated.DustCount, evaluated.RubbleCount, 0f, 0f);
            _impactBatch.Add(
                in value,
                in sample,
                effectsProfile.Impact.MaximumBatchedDustPerFrame,
                effectsProfile.Impact.MaximumBatchedRubblePerFrame);
        }

        private void FlushImpactBatch()
        {
            if (!_impactBatch.TryFlush(out EarthFeedbackBatchResult batch)) return;
            Vector3 point = new Vector3(batch.Point.x, batch.Point.y, batch.Point.z);
            Vector3 up = new Vector3(batch.Normal.x, batch.Normal.y, batch.Normal.z);
            SetEmitterFrame(dust, point, up);
            SetEmitterFrame(rubble, point + up * 0.025f, up);
            if (materialFeedback == null) { dust?.Emit(batch.DustCount); rubble?.Emit(batch.ChipCount); }
            // Bright motes are an accent for exceptional energy, never the dominant layer.
            if (sparks != null && effectsProfile != null &&
                batch.MaximumKineticEnergy > effectsProfile.Impact.SparkEnergyThreshold)
            {
                SetEmitterFrame(sparks, point + up * 0.04f, up);
                sparks.Emit(batch.MaximumKineticEnergy > effectsProfile.Impact.HeroSparkEnergyThreshold
                    ? effectsProfile.Impact.HeroSparkCount
                    : effectsProfile.Impact.SparkCount);
            }
            cameraRig?.AddPresentationImpulse(
                Mathf.Clamp(Mathf.Log10(1f + batch.MaximumKineticEnergy) * 0.022f, 0.025f, 0.19f),
                0.34f,
                batch.Seed ^ 0xEA47F11u);
        }

        private void OnPushChargeChanged(float charge)
        {
            if (charge <= 0f || Time.unscaledTime < _nextChargePulseTime) return;
            _nextChargePulseTime = Time.unscaledTime + Mathf.Lerp(0.15f, 0.075f, charge);
            cameraRig?.AddPresentationImpulse(Mathf.Lerp(0.012f, 0.035f, charge), 0.09f,
                unchecked((uint)(Time.frameCount * 2654435761)));
        }

        private void OnMagicPushed(MagicPushEvent value)
        {
            Vector3 point = new Vector3(value.Point.x, value.Point.y, value.Point.z);
            Emit(point, Mathf.RoundToInt(Mathf.Lerp(4f, 14f, value.Charge)), 2);
            cameraRig?.AddPresentationImpulse(
                Mathf.Lerp(0.035f, 0.105f, value.Charge),
                Mathf.Lerp(0.16f, 0.32f, value.Charge),
                value.Tick ^ 0x50555348u);
        }

        private void OnEarthReturn(EarthReturnEvent value)
        {
            Vector3 point = new Vector3(value.Position.x, value.Position.y, value.Position.z);
            int massBand = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log10(1f + value.Mass) * 4f), 2, 14);
            switch (value.Stage)
            {
                case EarthReturnEventStage.Captured:
                    Emit(point, massBand, 0);
                    break;
                case EarthReturnEventStage.Subsurface:
                    Emit(point, massBand + 4, 1);
                    if (rubble != null)
                    {
                        SetEmitterFrame(rubble, point, LocalUp(point));
                        rubble.Emit(Mathf.Clamp(massBand / 2, 2, 7));
                    }
                    break;
                case EarthReturnEventStage.Completed:
                    Emit(point, massBand + 7, 1);
                    cameraRig?.AddPresentationImpulse(
                        Mathf.Clamp(0.025f + Mathf.Log10(1f + value.Mass) * 0.016f, 0.025f, 0.11f),
                        0.26f,
                        value.MatterId ^ 0x5E771Eu);
                    break;
                case EarthReturnEventStage.Reversed:
                    Emit(point, massBand, 1);
                    break;
                case EarthReturnEventStage.Jammed:
                    Emit(point, massBand + 2, 0);
                    break;
            }
        }

        private void OnWaveColumnBurst(EarthPillarWavePulse pulse)
        {
            EarthPillarEffectsTuning tuning = effectsProfile != null ? effectsProfile.Pillar : null;
            Vector2 dustRange = tuning != null ? tuning.WaveDustCount : new Vector2(2f, 8f);
            Vector2 rubbleRange = tuning != null ? tuning.WaveRubbleCount : new Vector2(1f, 5f);
            int dustCount = Mathf.RoundToInt(Mathf.Lerp(dustRange.x, dustRange.y, pulse.Crest01));
            int chipCount = Mathf.RoundToInt(Mathf.Lerp(rubbleRange.x, rubbleRange.y, pulse.Crest01));
            SetEmitterFrame(dust, pulse.Position, pulse.Up);
            SetEmitterFrame(sparks, pulse.Position + (pulse.Up * 0.08f), pulse.Up);
            SetEmitterFrame(rubble, pulse.Position + (pulse.Up * 0.06f), pulse.Up);
            if (materialFeedback != null) materialFeedback.Emit(EarthMaterialFeedbackKind.Emerge,
                pulse.Position, pulse.Up, .8f + pulse.Crest01, .4f, pulse.StableId, dustCount: Mathf.Max(12, dustCount), chipCount: Mathf.Max(4, chipCount));
            else dust?.Emit(dustCount);
            float sparkThreshold = tuning != null ? tuning.WaveSparkThreshold : 0.48f;
            int sparkCount = tuning != null ? tuning.WaveSparkCount : 2;
            sparks?.Emit(pulse.Crest01 > sparkThreshold ? sparkCount : 0);
            if (materialFeedback == null) rubble?.Emit(chipCount);
            if (pulse.Crest01 > 0.45f && Time.unscaledTime >= _nextWaveCameraPulse)
            {
                _nextWaveCameraPulse = Time.unscaledTime + 0.075f;
                cameraRig?.AddPresentationImpulse(
                    Mathf.Lerp(0.012f, 0.055f, pulse.Crest01),
                    0.12f,
                    pulse.StableId ^ 0x57415645u);
            }
            if (pulseLight != null) pulseLight.transform.position = pulse.Position + (pulse.Up * 0.45f);
            Vector2 pulseRange = tuning != null ? tuning.PulseStrength : new Vector2(0.15f, 0.62f);
            _pulse = Mathf.Max(_pulse, Mathf.Lerp(pulseRange.x, pulseRange.y, pulse.Crest01));
        }

        private void ApplyEffectsProfile()
        {
            if (effectsProfile == null) return;
            EarthParticleSystemTuningApplier.ApplyDust(
                dust, effectsProfile.Impact.Dust, effectsProfile.Materials.ImpactDust);
            EarthParticleSystemTuningApplier.ApplyChips(
                sparks, effectsProfile.Impact.Sparks, effectsProfile.Materials.ImpactSparks, cosmeticMaterials);
            EarthParticleSystemTuningApplier.ApplyChips(
                rubble, effectsProfile.Impact.Rubble, effectsProfile.Materials.ImpactRubble, cosmeticMaterials);
        }

        private void Emit(Vector3 position, int dustCount, int sparkCount)
        {
            if (materialFeedback != null) materialFeedback.Emit(EarthMaterialFeedbackKind.Impact,
                position, LocalUp(position), 1f, .3f, dustCount: dustCount, chipCount: Mathf.Max(2, dustCount / 3));
            if (dust != null && materialFeedback == null)
            {
                SetEmitterFrame(dust, position, LocalUp(position));
                dust.Emit(dustCount);
            }
            if (sparks != null)
            {
                SetEmitterFrame(sparks, position, LocalUp(position));
                sparks.Emit(sparkCount);
            }
            if (pulseLight != null) pulseLight.transform.position = position + Vector3.up * 0.4f;
            _pulse = 1f;
        }

        private Vector3 LocalUp(Vector3 position)
        {
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            Vector3 radial = position - center;
            return radial.sqrMagnitude > 0.01f ? radial.normalized : Vector3.up;
        }

        private static void SetEmitterFrame(ParticleSystem system, Vector3 position, Vector3 up)
        {
            if (system == null) return;
            system.transform.SetPositionAndRotation(
                position,
                Quaternion.FromToRotation(Vector3.up,
                    up.sqrMagnitude > 0.01f ? up.normalized : Vector3.up));
        }

        private void ShowCracks(FragmentSpawnedEvent value)
        {
            if (strainCracks == null || strainCracks.Length == 0) return;
            Vector3 anchor = new Vector3(value.SurfaceAnchor.x, value.SurfaceAnchor.y, value.SurfaceAnchor.z);
            Vector3 up = anchor.sqrMagnitude > 0.01f ? anchor.normalized : Vector3.up;
            Vector3 tangent = Vector3.Cross(up, Mathf.Abs(Vector3.Dot(up, Vector3.forward)) < 0.9f
                ? Vector3.forward : Vector3.right).normalized;
            Vector3 bitangent = Vector3.Cross(up, tangent).normalized;
            for (int index = 0; index < strainCracks.Length; index++)
            {
                LineRenderer crack = strainCracks[index];
                if (crack == null) continue;
                float angle = index * (Mathf.PI * 2f / strainCracks.Length) + 0.23f;
                Vector3 direction = (tangent * Mathf.Cos(angle)) + (bitangent * Mathf.Sin(angle));
                float length = value.Radius * (1.05f + ((index % 3) * 0.16f));
                Vector3 side = Vector3.Cross(up, direction) * value.Radius * (index % 2 == 0 ? 0.10f : -0.08f);
                crack.gameObject.SetActive(true);
                crack.positionCount = 3;
                crack.SetPosition(0, anchor + (up * 0.025f) + (direction * value.Radius * 0.12f));
                crack.SetPosition(1, anchor + (up * 0.035f) + (direction * length * 0.57f) + side);
                crack.SetPosition(2, anchor + (up * 0.025f) + (direction * length));
            }
            _crackLife = 0.72f;
        }

        private void HideCracks()
        {
            if (strainCracks == null) return;
            for (int index = 0; index < strainCracks.Length; index++)
                if (strainCracks[index] != null) strainCracks[index].gameObject.SetActive(false);
        }
    }
}
