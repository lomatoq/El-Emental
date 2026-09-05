using System;
using Elemental.Simulation.Magic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Runtime.World
{
    [Serializable]
    public sealed class EarthParticleLayerTuning
    {
        [SerializeField] private bool enabled = true;
        [SerializeField, Range(1, 4096)] private int maxParticles = 256;
        [SerializeField] private Vector2 lifetime = new Vector2(0.45f, 0.9f);
        [SerializeField] private Vector2 speed = new Vector2(0.3f, 1.8f);
        [SerializeField] private Vector2 size = new Vector2(0.1f, 0.35f);
        [SerializeField] private Color colorA = new Color(0.60f, 0.43f, 0.27f, 0.68f);
        [SerializeField] private Color colorB = new Color(0.34f, 0.23f, 0.15f, 0.38f);
        [SerializeField] private Vector2 gravity = Vector2.zero;
        [SerializeField] private Vector2 noiseStrength = Vector2.zero;
        [SerializeField, Min(0.01f)] private float noiseFrequency = 0.5f;
        [SerializeField] private float noiseScrollSpeed = 0.15f;
        [Tooltip("Random spin on each local axis for mesh shards, in degrees per second.")]
        [SerializeField] private Vector2 angularSpeed = new Vector2(-360f, 360f);

        public bool Enabled => enabled;
        public int MaxParticles => Mathf.Max(1, maxParticles);
        public Vector2 Lifetime => Sorted(lifetime, 0.01f);
        public Vector2 Speed => Sorted(speed, 0f);
        public Vector2 Size => Sorted(size, 0.001f);
        public Color ColorA => colorA;
        public Color ColorB => colorB;
        public Vector2 Gravity => Sorted(gravity, -20f);
        public Vector2 NoiseStrength => Sorted(noiseStrength, 0f);
        public float NoiseFrequency => Mathf.Max(0.01f, noiseFrequency);
        public float NoiseScrollSpeed => noiseScrollSpeed;
        public Vector2 AngularSpeed => Sorted(angularSpeed, -2160f);

        internal static EarthParticleLayerTuning Create(
            int capacity,
            Vector2 configuredLifetime,
            Vector2 configuredSpeed,
            Vector2 configuredSize,
            Color configuredColorA,
            Color configuredColorB,
            Vector2 configuredGravity,
            Vector2 configuredNoise)
        {
            return new EarthParticleLayerTuning
            {
                maxParticles = capacity,
                lifetime = configuredLifetime,
                speed = configuredSpeed,
                size = configuredSize,
                colorA = configuredColorA,
                colorB = configuredColorB,
                gravity = configuredGravity,
                noiseStrength = configuredNoise
            };
        }

        private static Vector2 Sorted(Vector2 value, float minimum)
        {
            float a = float.IsFinite(value.x) ? value.x : minimum;
            float b = float.IsFinite(value.y) ? value.y : minimum;
            return new Vector2(Mathf.Max(minimum, Mathf.Min(a, b)), Mathf.Max(minimum, Mathf.Max(a, b)));
        }
    }

    [Serializable]
    public sealed class EarthEffectsMaterials
    {
        [SerializeField] private Material fractureDust;
        [SerializeField] private Material impactDust;
        [SerializeField] private Material impactSparks;
        [SerializeField] private Material impactRubble;
        [SerializeField] private Material surfDust;
        [SerializeField] private Material surfTrail;
        [SerializeField] private Material stoneFadeDust;
        [SerializeField] private Material ambientMotes;
        [SerializeField] private Material meteorStreaks;
        [SerializeField] private Material pillarChips;

        public Material FractureDust => fractureDust;
        public Material ImpactDust => impactDust;
        public Material ImpactSparks => impactSparks;
        public Material ImpactRubble => impactRubble;
        public Material SurfDust => surfDust;
        public Material SurfTrail => surfTrail;
        public Material StoneFadeDust => stoneFadeDust;
        public Material AmbientMotes => ambientMotes;
        public Material MeteorStreaks => meteorStreaks;
        public Material PillarChips => pillarChips;

        internal void Initialize(
            Material dust,
            Material sparks,
            Material rubble,
            Material trail,
            Material ambient,
            Material meteor,
            Material pillar)
        {
            fractureDust = dust;
            impactDust = dust;
            impactSparks = sparks;
            impactRubble = rubble;
            surfDust = dust;
            surfTrail = trail;
            stoneFadeDust = dust;
            ambientMotes = ambient;
            meteorStreaks = meteor;
            pillarChips = pillar;
        }
    }

    [Serializable]
    public sealed class EarthFractureEffectsTuning
    {
        [SerializeField] private EarthParticleLayerTuning dust = EarthParticleLayerTuning.Create(
            1600, new Vector2(0.85f, 1.75f), new Vector2(0.32f, 2.35f),
            new Vector2(0.18f, 0.82f), new Color(0.88f, 0.70f, 0.49f, 0.68f),
            new Color(0.56f, 0.39f, 0.25f, 0.38f), new Vector2(0.05f, 0.18f),
            new Vector2(0.16f, 0.42f));
        [SerializeField, Range(0, 512)] private int baseCount = 105;
        [SerializeField, Range(0, 128)] private int perReleasedPiece = 34;
        [SerializeField, Min(0f)] private float perImpulse = 0.045f;
        [SerializeField, Range(0, 1024)] private int minimumCount = 120;
        [SerializeField, Range(0, 2048)] private int maximumCount = 260;
        [SerializeField, Range(0f, 1f)] private float directionBlend = 0.24f;
        [SerializeField, Range(0f, 0.5f)] private float emitterInset = 0.05f;
        [SerializeField, Min(0f)] private float emitterRadius = 0.42f;
        [SerializeField, Range(0f, 1f)] private float radiusThickness = 0.72f;

        public EarthParticleLayerTuning Dust => dust;
        public int BaseCount => Mathf.Max(0, baseCount);
        public int PerReleasedPiece => Mathf.Max(0, perReleasedPiece);
        public float PerImpulse => Mathf.Max(0f, perImpulse);
        public int MinimumCount => Mathf.Max(0, Mathf.Min(minimumCount, maximumCount));
        public int MaximumCount => Mathf.Max(MinimumCount, maximumCount);
        public float DirectionBlend => Mathf.Clamp01(directionBlend);
        public float EmitterInset => Mathf.Max(0f, emitterInset);
        public float EmitterRadius => Mathf.Max(0f, emitterRadius);
        public float RadiusThickness => Mathf.Clamp01(radiusThickness);
    }

    [Serializable]
    public sealed class EarthImpactEffectsTuning
    {
        [SerializeField] private EarthParticleLayerTuning dust = EarthParticleLayerTuning.Create(
            768, new Vector2(0.35f, 0.95f), new Vector2(0.35f, 2.2f), new Vector2(0.12f, 0.46f),
            new Color(0.67f, 0.48f, 0.29f, 0.68f), new Color(0.34f, 0.23f, 0.15f, 0.36f),
            Vector2.zero, new Vector2(0.06f, 0.18f));
        [SerializeField] private EarthParticleLayerTuning sparks = EarthParticleLayerTuning.Create(
            192, new Vector2(0.18f, 0.48f), new Vector2(1.8f, 5.5f), new Vector2(0.035f, 0.11f),
            new Color(1f, 0.48f, 0.08f, 1f), new Color(1f, 0.18f, 0.015f, 0.7f),
            Vector2.zero, Vector2.zero);
        [SerializeField] private EarthParticleLayerTuning rubble = EarthParticleLayerTuning.Create(
            256, new Vector2(0.45f, 0.92f), new Vector2(1.4f, 4.1f), new Vector2(0.10f, 0.24f),
            new Color(0.68f, 0.50f, 0.31f, 1f), new Color(0.35f, 0.24f, 0.16f, 1f),
            Vector2.zero, Vector2.zero);
        [SerializeField, Range(0, 128)] private int maximumDustCount = 52;
        [SerializeField, Range(0, 64)] private int maximumRubbleCount = 14;
        [SerializeField, Range(0, 256)] private int maximumBatchedDustPerFrame = 72;
        [SerializeField, Range(0, 96)] private int maximumBatchedRubblePerFrame = 20;
        [SerializeField, Min(1f)] private float impulseNormalization = 2200f;
        [SerializeField, Min(1f)] private float sparkEnergyThreshold = 18000f;
        [SerializeField, Min(1f)] private float heroSparkEnergyThreshold = 65000f;
        [SerializeField, Range(0, 16)] private int sparkCount = 1;
        [SerializeField, Range(0, 32)] private int heroSparkCount = 3;

        public EarthParticleLayerTuning Dust => dust;
        public EarthParticleLayerTuning Sparks => sparks;
        public EarthParticleLayerTuning Rubble => rubble;
        public int MaximumDustCount => Mathf.Max(0, maximumDustCount);
        public int MaximumRubbleCount => Mathf.Max(0, maximumRubbleCount);
        public int MaximumBatchedDustPerFrame => Mathf.Max(0, maximumBatchedDustPerFrame);
        public int MaximumBatchedRubblePerFrame => Mathf.Max(0, maximumBatchedRubblePerFrame);
        public float ImpulseNormalization => Mathf.Max(1f, impulseNormalization);
        public float SparkEnergyThreshold => Mathf.Max(1f, Mathf.Min(sparkEnergyThreshold, heroSparkEnergyThreshold));
        public float HeroSparkEnergyThreshold => Mathf.Max(SparkEnergyThreshold, heroSparkEnergyThreshold);
        public int SparkCount => Mathf.Max(0, sparkCount);
        public int HeroSparkCount => Mathf.Max(SparkCount, heroSparkCount);
    }

    [Serializable]
    public sealed class EarthSurfEffectsTuning
    {
        [SerializeField] private EarthParticleLayerTuning dust = EarthParticleLayerTuning.Create(
            512, new Vector2(0.32f, 0.68f), new Vector2(0.7f, 2.2f), new Vector2(0.12f, 0.34f),
            new Color(0.30f, 0.18f, 0.10f, 0.62f), new Color(0.52f, 0.35f, 0.20f, 0.34f),
            Vector2.zero, new Vector2(0.04f, 0.15f));
        [SerializeField, Min(0f)] private float rateOverDistance = 29f;
        [SerializeField, Min(0f)] private float trailLifetime = 0.85f;
        [SerializeField, Min(0f)] private float trailEndWidth = 0.34f;
        [SerializeField] private Color trailStartColor = new Color(0.24f, 0.13f, 0.065f, 0.72f);
        [SerializeField] private Color trailEndColor = new Color(0.16f, 0.075f, 0.03f, 0f);
        [SerializeField, Min(0.01f)] private float cutChipInterval = 0.055f;
        [SerializeField] private Vector2 coarseCount = new Vector2(4f, 14f);
        [SerializeField] private Vector2 bodyCount = new Vector2(14f, 44f);
        [SerializeField] private Vector2 veilCount = new Vector2(18f, 54f);

        public EarthParticleLayerTuning Dust => dust;
        public float RateOverDistance => Mathf.Max(0f, rateOverDistance);
        public float TrailLifetime => Mathf.Max(0f, trailLifetime);
        public float TrailEndWidth => Mathf.Max(0f, trailEndWidth);
        public Color TrailStartColor => trailStartColor;
        public Color TrailEndColor => trailEndColor;
        public float CutChipInterval => Mathf.Max(0.01f, cutChipInterval);
        public Vector2 CoarseCount => SortedCounts(coarseCount);
        public Vector2 BodyCount => SortedCounts(bodyCount);
        public Vector2 VeilCount => SortedCounts(veilCount);

        private static Vector2 SortedCounts(Vector2 value) =>
            new Vector2(Mathf.Max(0f, Mathf.Min(value.x, value.y)), Mathf.Max(0f, Mathf.Max(value.x, value.y)));
    }

    [Serializable]
    public sealed class EarthStoneFadeEffectsTuning
    {
        [SerializeField] private EarthParticleLayerTuning dust = EarthParticleLayerTuning.Create(
            32, new Vector2(0.45f, 0.82f), new Vector2(0.45f, 1.65f), new Vector2(0.10f, 0.28f),
            new Color(0.46f, 0.33f, 0.22f, 0.72f), new Color(0.24f, 0.17f, 0.12f, 0.48f),
            Vector2.zero, new Vector2(0.02f, 0.10f));
        [SerializeField, Range(0f, 1f)] private float trigger01 = 0.42f;
        [SerializeField, Range(0, 256)] private int emitCount = 20;
        [SerializeField, Min(0f)] private float emitterRadius = 0.46f;
        public EarthParticleLayerTuning Dust => dust;
        public float Trigger01 => Mathf.Clamp01(trigger01);
        public int EmitCount => Mathf.Max(0, emitCount);
        public float EmitterRadius => Mathf.Max(0f, emitterRadius);
    }

    [Serializable]
    public sealed class EarthAmbientEffectsTuning
    {
        [SerializeField] private EarthParticleLayerTuning motes = EarthParticleLayerTuning.Create(
            64, new Vector2(4.5f, 7f), new Vector2(0.018f, 0.07f), new Vector2(0.030f, 0.084f),
            new Color(1f, 0.76f, 0.40f, 0.24f), new Color(1f, 0.95f, 0.78f, 0.58f),
            Vector2.zero, Vector2.zero);
        [SerializeField, Min(0f)] private float emissionRate = 18f;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.15f, 5.1f);
        [SerializeField] private Vector3 boxSize = new Vector3(10f, 5.5f, 9f);
        [SerializeField] private Vector2 horizontalVelocity = new Vector2(-0.022f, 0.022f);
        [SerializeField] private Vector2 verticalVelocity = new Vector2(0.018f, 0.06f);
        public EarthParticleLayerTuning Motes => motes;
        public float EmissionRate => Mathf.Max(0f, emissionRate);
        public Vector3 LocalOffset => localOffset;
        public Vector3 BoxSize => new Vector3(Mathf.Abs(boxSize.x), Mathf.Abs(boxSize.y), Mathf.Abs(boxSize.z));
        public Vector2 HorizontalVelocity => horizontalVelocity;
        public Vector2 VerticalVelocity => verticalVelocity;
    }

    [Serializable]
    public sealed class EarthMeteorEffectsTuning
    {
        [SerializeField] private EarthParticleLayerTuning streaks = EarthParticleLayerTuning.Create(
            96, new Vector2(0.8f, 2.2f), new Vector2(18f, 46f), new Vector2(0.05f, 0.22f),
            new Color(1f, 0.62f, 0.18f, 1f), new Color(1f, 0.22f, 0.035f, 0.68f),
            Vector2.zero, Vector2.zero);
        [SerializeField, Min(0f)] private float distantRate = 0.24f;
        [SerializeField, Min(0f)] private float radius = 240f;
        [SerializeField, Range(0f, 1f)] private float radiusThickness = 0.05f;
        [SerializeField, Min(0f)] private float velocityScale = 0.12f;
        [SerializeField, Min(0f)] private float lengthScale = 3.5f;
        [SerializeField, Min(0f)] private float physicalTrailLifetime = 0.95f;
        [SerializeField, Min(0f)] private float approachTrailLifetime = 1.55f;
        public EarthParticleLayerTuning Streaks => streaks;
        public float DistantRate => Mathf.Max(0f, distantRate);
        public float Radius => Mathf.Max(0f, radius);
        public float RadiusThickness => Mathf.Clamp01(radiusThickness);
        public float VelocityScale => Mathf.Max(0f, velocityScale);
        public float LengthScale => Mathf.Max(0f, lengthScale);
        public float PhysicalTrailLifetime => Mathf.Max(0f, physicalTrailLifetime);
        public float ApproachTrailLifetime => Mathf.Max(0f, approachTrailLifetime);
    }

    [Serializable]
    public sealed class EarthPillarEffectsTuning
    {
        [SerializeField, Range(1, 64)] private int chipPoolCount = 20;
        [SerializeField] private Vector2 chipSize = new Vector2(0.09f, 0.24f);
        [SerializeField] private Vector2 chipRadialSpeed = new Vector2(1.4f, 4.2f);
        [SerializeField] private Vector2 chipUpSpeed = new Vector2(0.8f, 2.5f);
        [SerializeField] private Vector2 waveDustCount = new Vector2(2f, 8f);
        [SerializeField] private Vector2 waveRubbleCount = new Vector2(1f, 5f);
        [SerializeField, Range(0f, 1f)] private float waveSparkThreshold = 0.48f;
        [SerializeField, Range(0, 16)] private int waveSparkCount = 2;
        [SerializeField] private Vector2 pulseStrength = new Vector2(0.15f, 0.62f);
        public int ChipPoolCount => Mathf.Clamp(chipPoolCount, 1, 64);
        public Vector2 ChipSize => Sorted(chipSize);
        public Vector2 ChipRadialSpeed => Sorted(chipRadialSpeed);
        public Vector2 ChipUpSpeed => Sorted(chipUpSpeed);
        public Vector2 WaveDustCount => Sorted(waveDustCount);
        public Vector2 WaveRubbleCount => Sorted(waveRubbleCount);
        public float WaveSparkThreshold => Mathf.Clamp01(waveSparkThreshold);
        public int WaveSparkCount => Mathf.Max(0, waveSparkCount);
        public Vector2 PulseStrength => Sorted(pulseStrength);
        private static Vector2 Sorted(Vector2 value) => new Vector2(Mathf.Min(value.x, value.y), Mathf.Max(value.x, value.y));
    }

    public readonly struct EarthImpactEffectsSample
    {
        public EarthImpactEffectsSample(int dustCount, int rubbleCount, int sparkCount)
        {
            DustCount = dustCount;
            RubbleCount = rubbleCount;
            SparkCount = sparkCount;
        }
        public int DustCount { get; }
        public int RubbleCount { get; }
        public int SparkCount { get; }
    }

    [CreateAssetMenu(menuName = "Elemental/VFX/Earth Effects Tuning Profile", fileName = "EarthEffectsTuningProfile")]
    public sealed class EarthEffectsTuningProfile : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;
        [SerializeField, HideInInspector] private int schemaVersion;
        [SerializeField] private EarthEffectsMaterials materials = new EarthEffectsMaterials();
        [SerializeField] private EarthFractureEffectsTuning fracture = new EarthFractureEffectsTuning();
        [SerializeField] private EarthImpactEffectsTuning impact = new EarthImpactEffectsTuning();
        [SerializeField] private EarthSurfEffectsTuning surf = new EarthSurfEffectsTuning();
        [SerializeField] private EarthStoneFadeEffectsTuning stoneFade = new EarthStoneFadeEffectsTuning();
        [SerializeField] private EarthAmbientEffectsTuning ambient = new EarthAmbientEffectsTuning();
        [SerializeField] private EarthMeteorEffectsTuning meteor = new EarthMeteorEffectsTuning();
        [SerializeField] private EarthPillarEffectsTuning pillar = new EarthPillarEffectsTuning();
        [SerializeField] private EarthMaterialEventsTuning materialEvents = new EarthMaterialEventsTuning();

        public int SchemaVersion => schemaVersion;
        public EarthEffectsMaterials Materials => materials;
        public EarthFractureEffectsTuning Fracture => fracture;
        public EarthImpactEffectsTuning Impact => impact;
        public EarthSurfEffectsTuning Surf => surf;
        public EarthStoneFadeEffectsTuning StoneFade => stoneFade;
        public EarthAmbientEffectsTuning Ambient => ambient;
        public EarthMeteorEffectsTuning Meteor => meteor;
        public EarthPillarEffectsTuning Pillar => pillar;
        public EarthMaterialEventsTuning MaterialEvents => materialEvents;

        public void InitializeAuthoringDefaults(
            Material dust,
            Material sparks,
            Material rubble,
            Material surfTrail,
            Material ambientMotes,
            Material meteorStreaks,
            Material pillarChips)
        {
            if (schemaVersion >= CurrentSchemaVersion) return;
            materials ??= new EarthEffectsMaterials();
            materials.Initialize(dust, sparks, rubble, surfTrail, ambientMotes, meteorStreaks, pillarChips);
            schemaVersion = CurrentSchemaVersion;
        }

        public int EvaluateFractureCount(int releasedPieces, float impulse)
        {
            float safeImpulse = float.IsFinite(impulse) ? Mathf.Max(0f, impulse) : 0f;
            long impulseContribution = (long)Mathf.Round(
                Mathf.Min(1000000000f, safeImpulse * fracture.PerImpulse));
            long raw = fracture.BaseCount + (long)Mathf.Max(0, releasedPieces) * fracture.PerReleasedPiece +
                       impulseContribution;
            return Mathf.Clamp((int)Math.Min(int.MaxValue, raw), fracture.MinimumCount, fracture.MaximumCount);
        }

        public EarthImpactEffectsSample EvaluateImpact(in EarthImpactEvent value)
        {
            float energy = float.IsFinite(value.KineticEnergy) ? Mathf.Max(0f, value.KineticEnergy) : 0f;
            float impulseValue = float.IsFinite(value.Impulse) ? Mathf.Max(0f, value.Impulse) : 0f;
            float energy01 = Mathf.Clamp01(Mathf.Log10(1f + energy) / 5f);
            float impulse01 = Mathf.Clamp01(impulseValue / impact.ImpulseNormalization);
            float materialWeight = value.Material == EarthImpactMaterialKind.Structure ? 1f :
                                   value.Material == EarthImpactMaterialKind.HeavyBlock ? 0.88f :
                                   value.Material == EarthImpactMaterialKind.Meteor ? 1.15f : 0.65f;
            float strength = Mathf.Clamp01(Mathf.Max(energy01, impulse01) * materialWeight);
            int sparks = energy > impact.HeroSparkEnergyThreshold ? impact.HeroSparkCount :
                         energy > impact.SparkEnergyThreshold ? impact.SparkCount : 0;
            return new EarthImpactEffectsSample(
                Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(5f, impact.MaximumDustCount, strength)), 0, impact.MaximumDustCount),
                Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, impact.MaximumRubbleCount, strength)), 0, impact.MaximumRubbleCount),
                sparks);
        }
    }

    public static class EarthParticleSystemTuningApplier
    {
        public static void ApplyDust(ParticleSystem system, EarthParticleLayerTuning tuning, Material material)
        {
            Apply(system, tuning, material);
            UseMaterialDustColor(system);
        }

        public static void ApplyChips(ParticleSystem system, EarthParticleLayerTuning tuning,
            Material material, EarthCosmeticMaterialCache ownedMaterials)
        {
            Apply(system, tuning, material);
            if (system == null) return;
            ApplyChipRotation(system, tuning != null ? tuning.AngularSpeed : new Vector2(-360f, 360f));
            EarthEffectRenderOrder.ApplyCosmeticRenderer(system.GetComponent<ParticleSystemRenderer>(),
                ownedMaterials.Get(material));
        }

        public static void ApplyChipRotation(ParticleSystem system, Vector2 degreesPerSecond)
        {
            if (system == null) return;
            var main = system.main;
            main.startRotation3D = true;
            var angle = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationX = angle;
            main.startRotationY = angle;
            main.startRotationZ = angle;
            var spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            var rate = new ParticleSystem.MinMaxCurve(degreesPerSecond.x * Mathf.Deg2Rad,
                degreesPerSecond.y * Mathf.Deg2Rad);
            spin.x = rate;
            spin.y = rate;
            spin.z = rate;
        }

        // The shader already multiplies by the shared material tint. White particle
        // RGB leaves that tint authoritative (copying it here would square it).
        // Keep opacity ranges and fade timing; never modify the material asset.
        public static void UseMaterialDustColor(ParticleSystem system)
        {
            if (system == null) return;
            EarthEffectRenderOrder.ApplyDustRenderer(system.GetComponent<ParticleSystemRenderer>());
            var main = system.main;
            main.startColor = AlphaOnly(main.startColor);
            var lifetime = system.colorOverLifetime;
            if (lifetime.enabled) lifetime.color = AlphaOnly(lifetime.color);
            var speed = system.colorBySpeed;
            if (speed.enabled) speed.color = AlphaOnly(speed.color);
        }

        private static ParticleSystem.MinMaxGradient AlphaOnly(ParticleSystem.MinMaxGradient value)
        {
            var mode = value.mode;
            value.colorMin = new Color(1f, 1f, 1f, value.colorMin.a);
            value.colorMax = new Color(1f, 1f, 1f, value.colorMax.a);
            if (value.gradientMin != null) value.gradientMin = AlphaOnly(value.gradientMin);
            if (value.gradientMax != null) value.gradientMax = AlphaOnly(value.gradientMax);
            value.mode = mode;
            return value;
        }

        private static Gradient AlphaOnly(Gradient source)
        {
            var result = new Gradient { mode = source.mode };
            result.SetKeys(new[] { new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f) }, source.alphaKeys);
            return result;
        }

        public static void Apply(ParticleSystem system, EarthParticleLayerTuning tuning, Material material)
        {
            if (system == null || tuning == null) return;
            ParticleSystem.MainModule main = system.main;
            Vector2 lifetime = tuning.Lifetime;
            Vector2 speed = tuning.Speed;
            Vector2 size = tuning.Size;
            Vector2 gravity = tuning.Gravity;
            main.maxParticles = tuning.MaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(tuning.ColorA, tuning.ColorB);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(gravity.x, gravity.y);
            ParticleSystem.NoiseModule noise = system.noise;
            Vector2 strength = tuning.NoiseStrength;
            noise.enabled = strength.y > 0.0001f;
            if (noise.enabled)
            {
                noise.quality = ParticleSystemNoiseQuality.High;
                noise.strength = new ParticleSystem.MinMaxCurve(strength.x, strength.y);
                noise.frequency = tuning.NoiseFrequency;
                noise.scrollSpeed = tuning.NoiseScrollSpeed;
                noise.damping = true;
            }
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null) renderer.sharedMaterial = material;
        }
    }
}
