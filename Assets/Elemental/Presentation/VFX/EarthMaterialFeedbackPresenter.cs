using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.VFX
{
    [DefaultExecutionOrder(900)]
    public sealed class EarthMaterialFeedbackPresenter : MonoBehaviour
    {
        private static readonly ProfilerMarker Marker = new ProfilerMarker("Elemental.Earth.MaterialParticles");
        [SerializeField] private EarthMaterialFeedbackHub hub;
        [SerializeField] private EarthEffectsTuningProfile profile;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private ParticleSystem dust, chips, fractureDust;
        [SerializeField] private Mesh chipMesh;
        private ParticleSystem.Particle[] dustBuffer, chipBuffer, fractureBuffer;
        private uint seed = 7919;
        private readonly EarthCosmeticMaterialCache cosmeticMaterials = new();
        private void OnDestroy() => cosmeticMaterials.Dispose();
        public void Configure(EarthMaterialFeedbackHub events, EarthEffectsTuningProfile tuning, Transform planet,
            ParticleSystem dustSystem, ParticleSystem chipSystem, Mesh mesh, ParticleSystem fractureSystem = null)
        {
            if (hub != null) hub.Presented -= Handle;
            hub = events; profile = tuning; planetCenter = planet; dust = dustSystem; chips = chipSystem; chipMesh = mesh; fractureDust = fractureSystem;
            if (Application.isPlaying) { Initialize(); if (isActiveAndEnabled && hub != null) hub.Presented += Handle; }
        }
        private void Awake() => Initialize();
        private void OnEnable()
        {
            if (Application.isPlaying && (dustBuffer == null || chipBuffer == null)) Initialize();
            if (hub != null) { hub.Presented -= Handle; hub.Presented += Handle; }
            if (dustBuffer != null && dust != null) dust.Play(true);
            if (chipBuffer != null && chips != null) chips.Play(true);
            if (fractureBuffer != null && fractureDust != null) fractureDust.Play(true);
        }
        private void OnDisable()
        {
            if (hub != null) hub.Presented -= Handle;
            if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (chips != null) chips.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (fractureDust != null) fractureDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private void Initialize()
        {
            if (profile == null || dust == null || chips == null) return;
            ConfigureSystem(dust, profile.Impact.Dust, profile.Materials.ImpactDust, false);
            ConfigureSystem(chips, profile.Impact.Rubble, profile.Materials.ImpactRubble, true);
            if (fractureDust != null)
            {
                ConfigureSystem(fractureDust, profile.Fracture.Dust, profile.Materials.FractureDust, false);
                if (fractureBuffer == null || fractureBuffer.Length != fractureDust.main.maxParticles)
                    fractureBuffer = new ParticleSystem.Particle[fractureDust.main.maxParticles];
            }
            if (dustBuffer == null || dustBuffer.Length != dust.main.maxParticles)
                dustBuffer = new ParticleSystem.Particle[dust.main.maxParticles];
            if (chipBuffer == null || chipBuffer.Length != chips.main.maxParticles)
                chipBuffer = new ParticleSystem.Particle[chips.main.maxParticles];
        }
        private void ConfigureSystem(ParticleSystem ps, EarthParticleLayerTuning tuning, Material material, bool rubble)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (rubble) EarthParticleSystemTuningApplier.ApplyChips(ps, tuning, material, cosmeticMaterials);
            else EarthParticleSystemTuningApplier.ApplyDust(ps, tuning, material);
            var main = ps.main;
            // Continuous simulation, no automatic emission. Event particles must
            // still age when a new impact arrives minutes after the last one.
            main.playOnAwake = false; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = tuning.MaxParticles; main.gravityModifier = 0f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var collision = ps.collision; collision.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = rubble;
            if (rubble) { renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = chipMesh; }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f,0f), new GradientAlphaKey(1f,.08f), new GradientAlphaKey(.5f,.6f), new GradientAlphaKey(0f,1f) });
                var color = ps.colorOverLifetime; color.enabled = true; color.color = gradient;
            }
            if (isActiveAndEnabled && ps.gameObject.activeInHierarchy) ps.Play(true);
        }
        private float Next() { seed = seed * 1664525u + 1013904223u; return (seed & 0x00ffffffu) / 16777216f; }
        private void Handle(EarthMaterialFeedbackCue cue)
        {
            if (dust == null || chips == null || profile == null) return;
            using (Marker.Auto())
            {
                seed ^= cue.SourceId ^ cue.Generation;
                Vector3 up = cue.Normal, point = cue.Point;
                Vector3 right = Vector3.Cross(up, Mathf.Abs(up.y) < .9f ? Vector3.up : Vector3.forward).normalized;
                Vector3 forward = Vector3.Cross(right, up);
                Emit(UsesBroadDust(cue.Kind) && fractureDust != null ? fractureDust : dust,
                    cue.DustCount, false, cue, point, up, right, forward);
                Emit(chips, cue.ChipCount, true, cue, point, up, right, forward);
            }
        }
        private static bool UsesBroadDust(EarthMaterialFeedbackKind kind) =>
            kind == EarthMaterialFeedbackKind.Fracture || kind == EarthMaterialFeedbackKind.Extract ||
            kind == EarthMaterialFeedbackKind.WaveSurfaceContact || kind == EarthMaterialFeedbackKind.WaveSurfaceBurst ||
            kind == EarthMaterialFeedbackKind.ExtractionSurfaceContact;

        private void Emit(ParticleSystem ps, int count, bool rubble, EarthMaterialFeedbackCue cue,
            Vector3 point, Vector3 up, Vector3 right, Vector3 forward)
        {
            EarthParticleLayerTuning tuning = rubble ? profile.Impact.Rubble :
                UsesBroadDust(cue.Kind) ? profile.Fracture.Dust : profile.Impact.Dust;
            if (!tuning.Enabled) return;
            for (int i = 0; i < count; i++)
            {
                float angle = Next() * Mathf.PI * 2f;
                Vector3 radial = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                float energy = Mathf.Sqrt(Mathf.Max(.1f, cue.Strength));
                float size = Mathf.Lerp(tuning.Size.x,tuning.Size.y,Next()) * cue.ParticleSizeScale;
                if (!rubble && UsesBroadDust(cue.Kind) && cue.Kind != EarthMaterialFeedbackKind.Fracture) size *= 1.45f;
                var p = new ParticleSystem.EmitParams
                {
                    // A billboard centered at the contact has half its area buried.
                    // Lift its center by half its own size, keeping the lower edge at the contact.
                    position = point + up * (rubble ? .04f : Mathf.Max(.04f, size * .5f)) + radial * (Mathf.Sqrt(Next()) * cue.Radius * .35f),
                    velocity = (radial + up * Mathf.Lerp(.4f,1.2f,Next())).normalized * (Mathf.Lerp(tuning.Speed.x,tuning.Speed.y,Next()) * energy),
                    startLifetime = Mathf.Lerp(tuning.Lifetime.x,tuning.Lifetime.y,Next()),
                    startSize = size,
                    startColor = new Color(1f, 1f, 1f, Mathf.Lerp(tuning.ColorA.a,tuning.ColorB.a,Next())),
                    rotation3D = rubble ? new Vector3(Next(),Next(),Next()) * 360f : Vector3.forward * (Next() * 360f)
                };
                ps.Emit(p, 1);
            }
        }
        private void LateUpdate()
        {
            using (Marker.Auto()) { Integrate(dust, dustBuffer, 1.3f); Integrate(fractureDust, fractureBuffer, 1.3f); Integrate(chips, chipBuffer, 14f); }
        }
        private void Integrate(ParticleSystem ps, ParticleSystem.Particle[] buffer, float gravity)
        {
            if (ps == null || buffer == null || !ps.IsAlive()) return;
            int count = ps.GetParticles(buffer);
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            for (int i = 0; i < count; i++)
            {
                if (!IsFinite(buffer[i].position) || !IsFinite(buffer[i].velocity))
                { buffer[i].remainingLifetime = 0f; continue; }
                buffer[i].velocity += (center - buffer[i].position).normalized * (gravity * dt);
            }
            ps.SetParticles(buffer, count);
        }
        private static bool IsFinite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
