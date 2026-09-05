using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class EarthArenaFractureDustPresenter : MonoBehaviour
    {
        [SerializeField] private ParticleSystem dust;
        [SerializeField] private EarthEffectsTuningProfile effectsProfile;
        private EarthArenaStructure[] _structures;

        public void Configure(EarthEffectsTuningProfile configuredProfile)
        {
            dust = GetComponent<ParticleSystem>();
            effectsProfile = configuredProfile;
            ConfigureParticles();
        }

        private void Awake()
        {
            if (dust == null) dust = GetComponent<ParticleSystem>();
            ConfigureParticles();
        }

        private void OnEnable()
        {
            Subscribe(false);
            Subscribe(true);
        }

        private void OnDisable() => Subscribe(false);

        private void Subscribe(bool subscribe)
        {
            EarthArenaStructure[] structures = GetComponentsInParent<EarthArenaStructure>(true);
            if (structures.Length == 0 && transform.parent != null)
                structures = transform.parent.GetComponentsInChildren<EarthArenaStructure>(true);
            _structures = structures;
            for (int index = 0; index < _structures.Length; index++)
            {
                EarthArenaStructure structure = _structures[index];
                if (structure == null) continue;
                structure.FracturePresented -= HandleFracture;
                // The shared material presenter supplies mesh chips and contact dust.
                // This presenter is the authored, shaped 120-260 particle cloud that
                // hides the intact-to-fracture proxy swap. Removing it made large
                // arena and outer-column fractures read like a small contact puff.
                if (subscribe) structure.FracturePresented += HandleFracture;
            }
        }

        private void HandleFracture(EarthArenaFracturePulse pulse)
        {
            if (dust == null) return;
            Vector3 impact = pulse.Direction.sqrMagnitude > 0.01f
                ? pulse.Direction.normalized
                : Vector3.up;
            EarthFractureEffectsTuning tuning = effectsProfile != null ? effectsProfile.Fracture : null;
            float directionBlend = tuning != null ? tuning.DirectionBlend : 0.24f;
            float inset = tuning != null ? tuning.EmitterInset : 0.05f;
            Vector3 emissionUp = Vector3.Slerp(Vector3.up, impact, directionBlend).normalized;
            transform.SetPositionAndRotation(
                pulse.Point + Vector3.up * inset,
                Quaternion.FromToRotation(Vector3.up, emissionUp));

            // A fracture swap exposes several pieces in one frame. A dense,
            // lingering sandstone cloud hides that unavoidable proxy transition.
            int count = effectsProfile != null
                ? effectsProfile.EvaluateFractureCount(pulse.ReleasedPieces, pulse.Impulse)
                : Mathf.Clamp(105 + pulse.ReleasedPieces * 34 + Mathf.RoundToInt(pulse.Impulse * 0.045f), 120, 260);
            dust.Emit(count);
        }

        private void ConfigureParticles()
        {
            if (dust == null) return;
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            EarthFractureEffectsTuning tuning = effectsProfile != null ? effectsProfile.Fracture : null;
            if (tuning != null)
                EarthParticleSystemTuningApplier.Apply(dust, tuning.Dust, effectsProfile.Materials.FractureDust);

            ParticleSystem.MainModule main = dust.main;
            main.loop = false;
            main.playOnAwake = false;
            if (tuning == null) main.maxParticles = 1600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            if (tuning == null)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.75f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.32f, 2.35f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.82f);
            }
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            if (tuning == null)
            {
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.88f, 0.70f, 0.49f, 0.68f),
                    new Color(0.56f, 0.39f, 0.25f, 0.38f));
                main.gravityModifier = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            }

            ParticleSystem.EmissionModule emission = dust.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = dust.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = tuning != null ? tuning.EmitterRadius : 0.42f;
            shape.radiusThickness = tuning != null ? tuning.RadiusThickness : 0.72f;

            ParticleSystem.NoiseModule noise = dust.noise;
            if (tuning == null)
            {
                noise.enabled = true;
                noise.quality = ParticleSystemNoiseQuality.High;
                noise.strength = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
                noise.frequency = 0.52f;
                noise.scrollSpeed = 0.18f;
                noise.damping = true;
            }

            ParticleSystem.ColorOverLifetimeModule color = dust.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.72f, 0.52f, 0.34f), 0f),
                    new GradientColorKey(new Color(0.52f, 0.39f, 0.29f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.78f, 0.08f),
                    new GradientAlphaKey(0.48f, 0.52f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;
            EarthParticleSystemTuningApplier.UseMaterialDustColor(dust);

            ParticleSystem.SizeOverLifetimeModule size = dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.38f),
                new Keyframe(0.18f, 1.10f),
                new Keyframe(1f, 1.72f)));

            ParticleSystemRenderer renderer = dust.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (effectsProfile != null && effectsProfile.Materials.FractureDust != null)
                renderer.sharedMaterial = effectsProfile.Materials.FractureDust;
        }
    }
}
