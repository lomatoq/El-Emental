using Elemental.Runtime.World;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Magic;
using Elemental.Presentation.Camera;
using Elemental.Input.Gestures;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class EarthMagicFeedback : MonoBehaviour
    {
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

        private float _pulse;
        private float _crackLife;
        private float _nextChargePulseTime;
        private float _nextWaveCameraPulse;
        private EarthFeedbackBatchAccumulator _impactBatch;

        public void ConfigureImpactProfile(EarthFeedbackProfile configuredProfile) =>
            impactFeedbackProfile = configuredProfile;

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
            HideCracks();
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
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
            Vector3 up = midpoint.sqrMagnitude > 0.01f ? midpoint.normalized : Vector3.up;
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
                Emit(alongWall + (up * 0.10f), 5, index % 2 == 0 ? 2 : 1);
                if (rubble != null)
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
            Vector3 up = midpoint.sqrMagnitude > 0.01f ? midpoint.normalized : Vector3.up;
            for (int index = 0; index < 7; index++)
            {
                Vector3 point = Vector3.Lerp(start, end, index / 6f) + (up * value.Height * 0.22f);
                Emit(point, 4, index % 2 == 0 ? 1 : 0);
                if (rubble != null)
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
            Emit(new Vector3(value.Position.x, value.Position.y, value.Position.z), 20, 9);
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
            if (impactFeedbackProfile != null) return;
            Emit(new Vector3(value.Point.x, value.Point.y, value.Point.z),
                Mathf.Clamp(Mathf.RoundToInt(value.Impulse * 0.025f), 18, 72), 24);
            cameraRig?.AddPresentationImpulse(
                Mathf.Clamp(value.Impulse * 0.00075f, 0.04f, 0.24f),
                0.38f,
                value.FragmentId ^ 0x1A4AC7u);
        }

        private void OnEarthImpact(EarthImpactEvent value)
        {
            if (impactFeedbackProfile == null) return;
            EarthFeedbackSample sample = impactFeedbackProfile.Evaluate(in value);
            _impactBatch.Add(
                in value,
                in sample,
                impactFeedbackProfile.MaximumBatchedDustPerFrame,
                impactFeedbackProfile.MaximumBatchedChipsPerFrame);
        }

        private void FlushImpactBatch()
        {
            if (!_impactBatch.TryFlush(out EarthFeedbackBatchResult batch)) return;
            Vector3 point = new Vector3(batch.Point.x, batch.Point.y, batch.Point.z);
            Vector3 up = new Vector3(batch.Normal.x, batch.Normal.y, batch.Normal.z);
            SetEmitterFrame(dust, point, up);
            SetEmitterFrame(rubble, point + up * 0.025f, up);
            dust?.Emit(batch.DustCount);
            rubble?.Emit(batch.ChipCount);
            // Bright motes are an accent for exceptional energy, never the dominant layer.
            if (sparks != null && batch.MaximumKineticEnergy > 18000f)
            {
                SetEmitterFrame(sparks, point + up * 0.04f, up);
                sparks.Emit(batch.MaximumKineticEnergy > 65000f ? 3 : 1);
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

        private void OnWaveColumnBurst(EarthPillarWavePulse pulse)
        {
            int dustCount = Mathf.RoundToInt(Mathf.Lerp(2f, 8f, pulse.Crest01));
            int chipCount = Mathf.RoundToInt(Mathf.Lerp(1f, 5f, pulse.Crest01));
            SetEmitterFrame(dust, pulse.Position, pulse.Up);
            SetEmitterFrame(sparks, pulse.Position + (pulse.Up * 0.08f), pulse.Up);
            SetEmitterFrame(rubble, pulse.Position + (pulse.Up * 0.06f), pulse.Up);
            dust?.Emit(dustCount);
            sparks?.Emit(pulse.Crest01 > 0.48f ? 2 : 0);
            rubble?.Emit(chipCount);
            if (pulse.Crest01 > 0.45f && Time.unscaledTime >= _nextWaveCameraPulse)
            {
                _nextWaveCameraPulse = Time.unscaledTime + 0.075f;
                cameraRig?.AddPresentationImpulse(
                    Mathf.Lerp(0.012f, 0.055f, pulse.Crest01),
                    0.12f,
                    pulse.StableId ^ 0x57415645u);
            }
            if (pulseLight != null) pulseLight.transform.position = pulse.Position + (pulse.Up * 0.45f);
            _pulse = Mathf.Max(_pulse, Mathf.Lerp(0.15f, 0.62f, pulse.Crest01));
        }

        private void Emit(Vector3 position, int dustCount, int sparkCount)
        {
            if (dust != null)
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
