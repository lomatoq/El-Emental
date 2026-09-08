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
        private Mesh[] cosmeticChipMeshes;
        [SerializeField, Range(0f, .6f)] private float chipRestitution = .28f;
        [SerializeField, Range(0f, 1f)] private float chipFriction = .58f;
        [SerializeField, Min(0f)] private float secondaryDustThreshold = .85f;
        [SerializeField] private LayerMask chipSurfaceMask = UnityEngine.Physics.DefaultRaycastLayers;
        private int secondaryPuffsThisFrame;
        private void OnDestroy()
        {
            cosmeticMaterials.Dispose();
            if (cosmeticChipMeshes == null) return;
            foreach (Mesh owned in cosmeticChipMeshes)
                if (owned != null) { if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned); }
        }
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
            main.maxParticles = rubble ? Mathf.Min(128, tuning.MaxParticles) : tuning.MaxParticles; main.gravityModifier = 0f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var collision = ps.collision; collision.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = rubble;
            if (!rubble && ps == dust)
            {
                // The shared material's 1.5 m soft range is for broad fracture smoke.
                // Contact puffs are centimetres above the ground, so that range erases them.
                // Override this renderer only; leave the authored material and broad dust intact.
                var contactProperties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(contactProperties);
                contactProperties.SetVector("_SoftParticleFadeParams", new Vector4(.0125f, 1f / .2875f, 0f, 0f));
                contactProperties.SetFloat("_SoftParticleNearDistance",.0125f);
                contactProperties.SetFloat("_SoftParticleInvDistance",1f/.2875f);
                contactProperties.SetColor("_BaseColor",new Color(.93f,.78f,.59f,.8f));
                contactProperties.SetFloat("_Brightness",1.1f);
                renderer.SetPropertyBlock(contactProperties);
            }
            if (rubble)
            {
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                // Seeded per-particle angular velocity can settle after a contact.
                var spin = ps.rotationOverLifetime; spin.enabled = false;
                cosmeticChipMeshes ??= EarthCosmeticChipLibrary.Build(chipMesh);
                renderer.SetMeshes(cosmeticChipMeshes, cosmeticChipMeshes.Length);
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f,0f), new GradientAlphaKey(1f,.045f), new GradientAlphaKey(.68f,.5f), new GradientAlphaKey(0f,1f) });
                var color = ps.colorOverLifetime; color.enabled = true; color.color = gradient;
                var sizeOverLife = ps.sizeOverLifetime; sizeOverLife.enabled = true;
                sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f,.7f), new Keyframe(.2f,1f), new Keyframe(1f,1.65f)));
            }
            if (isActiveAndEnabled && ps.gameObject.activeInHierarchy) ps.Play(true);
        }
        private float Next() { seed = seed * 1664525u + 1013904223u; return (seed & 0x00ffffffu) / 16777216f; }
        private void Handle(EarthMaterialFeedbackCue cue)
        {
            if (dust == null || chips == null || profile == null) return;
            using (Marker.Auto())
            {
                seed = EarthStoneImpactDust.CueSeed(cue.SourceId, cue.Generation, cue.Kind, cue.Point);
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
            kind == EarthMaterialFeedbackKind.Emerge ||
            kind == EarthMaterialFeedbackKind.WaveSurfaceContact || kind == EarthMaterialFeedbackKind.WaveSurfaceBurst ||
            kind == EarthMaterialFeedbackKind.ExtractionSurfaceContact;

        private void Emit(ParticleSystem ps, int count, bool rubble, EarthMaterialFeedbackCue cue,
            Vector3 point, Vector3 up, Vector3 right, Vector3 forward)
        {
            EarthParticleLayerTuning tuning = rubble ? profile.Impact.Rubble :
                UsesBroadDust(cue.Kind) ? profile.Fracture.Dust : profile.Impact.Dust;
            if (!tuning.Enabled) return;
            float lobeAxis = Next() * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                float angle = Next() * Mathf.PI * 2f;
                if (cue.Kind == EarthMaterialFeedbackKind.Impact && Next() < .55f)
                    angle = lobeAxis + (Next() < .68f ? 0f : Mathf.PI) + (Next() - .5f) * 1.5f;
                Vector3 radial = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                float energy = Mathf.Sqrt(Mathf.Max(.1f, cue.Strength));
                float sizeSample = EarthStoneImpactDust.FineBiasedSize(Next());
                float size = Mathf.Lerp(tuning.Size.x,tuning.Size.y,sizeSample) * cue.ParticleSizeScale;
                // Keep event budgets, but reserve distinct contact, body and residual roles.
                // Low seed bits preserve the role through the allocation-free integration loop.
                bool layered = !rubble && (cue.Kind == EarthMaterialFeedbackKind.Impact ||
                    cue.Kind == EarthMaterialFeedbackKind.Land || UsesBroadDust(cue.Kind));
                int role = layered ? (i % 10 < 4 ? 0 : i % 10 < 8 ? 1 : 2) : 0;
                float speed = Mathf.Lerp(tuning.Speed.x,tuning.Speed.y,Next()) * energy;
                float lift = rubble ? Mathf.Lerp(.65f,1.3f,Next()) : Mathf.Lerp(.12f,.35f,Next());
                float lifetime = Mathf.Lerp(tuning.Lifetime.x,tuning.Lifetime.y,Next());
                float opacity = Mathf.Lerp(tuning.ColorA.a,tuning.ColorB.a,Next());
                if (layered)
                {
                    if (role == 0) { size *= .8f; speed *= 1.25f; lifetime *= .65f; }
                    else
                    {
                        size = Mathf.Lerp(profile.Fracture.Dust.Size.x, profile.Fracture.Dust.Size.y,
                            Mathf.Lerp(.3f,1f,sizeSample)) * cue.ParticleSizeScale * (role == 1 ? 1.1f : .95f);
                        speed *= role == 1 ? .42f : .19f;
                        lift = role == 1 ? .75f : 1.4f;
                        lifetime = Mathf.Lerp(profile.Fracture.Dust.Lifetime.x,profile.Fracture.Dust.Lifetime.y,Next())
                            * (role == 1 ? 1f : 1.6f);
                        opacity *= role == 1 ? .9f : .38f;
                    }
                }
                if (rubble) lifetime *= 1.8f;
                var p = new ParticleSystem.EmitParams
                {
                    // A billboard centered at the contact has half its area buried.
                    // Lift its center by half its own size, keeping the lower edge at the contact.
                    position = point + up * (rubble ? .04f : Mathf.Max(.04f, size * .5f)) + radial * (Mathf.Sqrt(Next()) * cue.Radius * .35f),
                    velocity = (radial + up * lift).normalized * speed,
                    startLifetime = lifetime,
                    randomSeed = ((seed & ~3u) | (uint)role) + 4u,
                    startSize = size,
                    startColor = new Color(1f, 1f, 1f, opacity),
                    rotation3D = rubble ? new Vector3(Next(),Next(),Next()) * 360f : Vector3.forward * (Next() * 360f)
                };
                ps.Emit(p, 1);
            }
        }
        private void LateUpdate()
        {
            using (Marker.Auto())
            {
                secondaryPuffsThisFrame = 0;
                Integrate(dust, dustBuffer, .6f); Integrate(fractureDust, fractureBuffer, .6f); Integrate(chips, chipBuffer, 14f);
            }
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
                ref ParticleSystem.Particle particle = ref buffer[i];
                Vector3 down = (center - particle.position).normalized;
                if (ps == chips)
                {
                    float age = 1f-particle.remainingLifetime/Mathf.Max(.01f,particle.startLifetime);
                    Color32 tint = particle.startColor; tint.a = (byte)(255f*(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.65f,1f,age))));
                    particle.startColor = tint;
                    int bounces = (int)(particle.randomSeed & 3u);
                    if (bounces == 3) { particle.velocity = Vector3.zero; continue; }
                    Vector3 velocity = particle.velocity;
                    float speed = velocity.magnitude;
                    float radius = Mathf.Max(.012f, particle.startSize * .3f);
                    // Sweep the step already advanced by ParticleSystem to avoid tunnelling.
                    Vector3 start = particle.position - velocity * dt;
                    if (speed > .01f && UnityEngine.Physics.Raycast(start, velocity / speed, out RaycastHit hit,
                        speed * dt + radius, chipSurfaceMask, QueryTriggerInteraction.Ignore) &&
                        Vector3.Dot(velocity, hit.normal) < 0f)
                    {
                        float impact = -Vector3.Dot(velocity, hit.normal);
                        particle.position = hit.point + hit.normal * (radius + .005f);
                        bounces++;
                        Vector3 tangent = Vector3.ProjectOnPlane(velocity,hit.normal) * (1f-chipFriction);
                        particle.velocity = tangent + hit.normal * (impact * chipRestitution);
                        if (impact > secondaryDustThreshold && secondaryPuffsThisFrame < 8)
                            EmitSecondaryPuff(hit.point, hit.normal, particle.startSize, particle.randomSeed);
                        if (bounces >= 2 || particle.velocity.sqrMagnitude < .12f)
                        { bounces = 3; particle.velocity = Vector3.zero; }
                        particle.randomSeed = (particle.randomSeed & ~3u) | (uint)bounces;
                    }
                    else particle.velocity += down * (gravity * dt);
                    float spin = Mathf.Lerp(profile.Impact.Rubble.AngularSpeed.x,profile.Impact.Rubble.AngularSpeed.y,
                        ((particle.randomSeed >> 2) & 255u) / 255f) / (1f+bounces*2f);
                    if (bounces < 3) particle.rotation3D += new Vector3(spin,spin*.73f,-spin*.41f)*dt;
                }
                else
                {
                    int role = (int)(particle.randomSeed & 3u);
                    float drag = role == 0 ? 3.8f : role == 1 ? 1.7f : 1.25f;
                    float settling = role == 0 ? gravity : role == 1 ? .16f : .035f;
                    particle.velocity = particle.velocity * Mathf.Exp(-drag * dt) + down * (settling * dt);
                }
            }
            ps.SetParticles(buffer, count);
        }
        private void EmitSecondaryPuff(Vector3 point, Vector3 normal, float chipSize, uint particleSeed)
        {
            if (dust == null || profile == null || !profile.Impact.Dust.Enabled) return;
            secondaryPuffsThisFrame++;
            float size = Mathf.Clamp(chipSize*2.8f,profile.Impact.Dust.Size.x,profile.Impact.Dust.Size.y);
            var puff = new ParticleSystem.EmitParams
            {
                position = point + normal * (size*.5f), velocity = normal*.25f,
                startSize = size, startLifetime = profile.Impact.Dust.Lifetime.y*.7f,
                startColor = new Color(1f,1f,1f,.42f), randomSeed = particleSeed & ~3u,
                rotation3D = Vector3.forward*(particleSeed%360u)
            };
            dust.Emit(puff,1);
        }
        private static bool IsFinite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
