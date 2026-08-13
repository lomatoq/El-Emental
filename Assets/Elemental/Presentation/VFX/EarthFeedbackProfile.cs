using Elemental.Simulation.Magic;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    public readonly struct EarthFeedbackSample
    {
        public EarthFeedbackSample(int dustCount, int chipCount, float scarRadius, float lifetime)
        {
            DustCount = dustCount;
            ChipCount = chipCount;
            ScarRadius = scarRadius;
            Lifetime = lifetime;
        }

        public int DustCount { get; }
        public int ChipCount { get; }
        public float ScarRadius { get; }
        public float Lifetime { get; }
    }

    [CreateAssetMenu(menuName = "Elemental/VFX/Earth Feedback Profile", fileName = "EarthFeedbackProfile")]
    public sealed class EarthFeedbackProfile : ScriptableObject
    {
        [SerializeField, Range(8, 96)] private int decalCapacity = 40;
        [SerializeField, Range(4, 96)] private int maximumDustCount = 52;
        [SerializeField, Range(0, 32)] private int maximumChipCount = 14;
        [SerializeField, Min(1f)] private float minimumScarImpulse = 45f;
        [SerializeField, Range(0.12f, 2.5f)] private float minimumScarRadius = 0.24f;
        [SerializeField, Range(0.25f, 4f)] private float maximumScarRadius = 1.8f;
        [SerializeField, Range(2f, 60f)] private float scarLifetime = 24f;
        [SerializeField] private bool persistentSurfaceScars = true;
        [SerializeField, Range(1f, 12f)] private float scarFadeSeconds = 5f;
        [SerializeField, Range(4f, 80f)] private float decalDrawDistance = 42f;

        public int DecalCapacity => decalCapacity;
        public float MinimumScarImpulse => minimumScarImpulse;
        public float ScarFadeSeconds => scarFadeSeconds;
        public float DecalDrawDistance => decalDrawDistance;
        public bool PersistentSurfaceScars => persistentSurfaceScars;

        public EarthFeedbackSample Evaluate(in EarthImpactEvent impact)
        {
            float energy01 = Mathf.Clamp01(Mathf.Log10(1f + impact.KineticEnergy) / 5f);
            float impulse01 = Mathf.Clamp01(impact.Impulse / 2200f);
            float materialWeight = impact.Material == EarthImpactMaterialKind.Structure ? 1f :
                                   impact.Material == EarthImpactMaterialKind.HeavyBlock ? 0.88f :
                                   impact.Material == EarthImpactMaterialKind.Meteor ? 1.15f : 0.65f;
            float strength = Mathf.Clamp01(Mathf.Max(energy01, impulse01) * materialWeight);
            return new EarthFeedbackSample(
                Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(5f, maximumDustCount, strength)), 0, maximumDustCount),
                Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, maximumChipCount, strength)), 0, maximumChipCount),
                Mathf.Lerp(minimumScarRadius, maximumScarRadius, strength),
                scarLifetime * Mathf.Lerp(0.7f, 1.15f, strength));
        }
    }
}
