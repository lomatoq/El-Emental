using Unity.Mathematics;

namespace Elemental.Simulation.Matter
{
    public readonly struct EarthRepresentationBudget
    {
        public EarthRepresentationBudget(
            int heroPhysical,
            int secondaryPhysical,
            int visualGpu,
            float heroDistance,
            float secondaryDistance)
        {
            HeroPhysical = math.max(1, heroPhysical);
            SecondaryPhysical = math.max(HeroPhysical, secondaryPhysical);
            VisualGpu = math.max(SecondaryPhysical, visualGpu);
            HeroDistance = math.max(1f, heroDistance);
            SecondaryDistance = math.max(HeroDistance, secondaryDistance);
        }

        public int HeroPhysical { get; }
        public int SecondaryPhysical { get; }
        public int VisualGpu { get; }
        public float HeroDistance { get; }
        public float SecondaryDistance { get; }

        public static EarthRepresentationBudget NativeHigh =>
            new EarthRepresentationBudget(72, 192, 4096, 18f, 42f);
        public static EarthRepresentationBudget NativeLow =>
            new EarthRepresentationBudget(40, 96, 1536, 14f, 30f);
        public static EarthRepresentationBudget WebLab =>
            new EarthRepresentationBudget(22, 52, 640, 10f, 22f);
    }

    public readonly struct EarthRepresentationCandidate
    {
        public EarthRepresentationCandidate(
            float distance,
            float screenSize01,
            float speed,
            float energy,
            bool gameplayRelevant,
            bool controlled,
            bool cameraFocus,
            float recentInteraction01)
        {
            Distance = math.max(0f, distance);
            ScreenSize01 = math.saturate(screenSize01);
            Speed = math.max(0f, speed);
            Energy = math.max(0f, energy);
            GameplayRelevant = gameplayRelevant;
            Controlled = controlled;
            CameraFocus = cameraFocus;
            RecentInteraction01 = math.saturate(recentInteraction01);
        }

        public float Distance { get; }
        public float ScreenSize01 { get; }
        public float Speed { get; }
        public float Energy { get; }
        public bool GameplayRelevant { get; }
        public bool Controlled { get; }
        public bool CameraFocus { get; }
        public float RecentInteraction01 { get; }
    }

    public readonly struct EarthRepresentationPressure
    {
        public EarthRepresentationPressure(int heroCount, int secondaryCount, int visualCount)
        {
            HeroCount = math.max(0, heroCount);
            SecondaryCount = math.max(0, secondaryCount);
            VisualCount = math.max(0, visualCount);
        }
        public int HeroCount { get; }
        public int SecondaryCount { get; }
        public int VisualCount { get; }
    }

    public readonly struct EarthRepresentationDecision
    {
        public EarthRepresentationDecision(EarthRepresentationTier tier, float score, bool admitted)
        {
            Tier = tier;
            Score = math.saturate(score);
            Admitted = admitted;
        }
        public EarthRepresentationTier Tier { get; }
        public float Score { get; }
        public bool Admitted { get; }
    }

    /// <summary>
    /// Pure, allocation-free representation arbitration. It may degrade presentation
    /// and distant debris, but never demotes controlled or authoritative matter.
    /// </summary>
    public static class EarthRepresentationBudgetSolver
    {
        public static EarthRepresentationDecision Evaluate(
            in EarthRepresentationCandidate candidate,
            in EarthRepresentationPressure pressure,
            in EarthRepresentationBudget budget)
        {
            float distance01 = math.saturate(1f - candidate.Distance / budget.SecondaryDistance);
            float motion01 = 1f - math.exp(-candidate.Speed / 8f);
            float energy01 = 1f - math.exp(-candidate.Energy / 1200f);
            float score = math.saturate(
                candidate.ScreenSize01 * 0.20f + distance01 * 0.14f + motion01 * 0.12f +
                energy01 * 0.12f + candidate.RecentInteraction01 * 0.12f +
                (candidate.GameplayRelevant ? 0.16f : 0f) +
                (candidate.CameraFocus ? 0.12f : 0f) +
                (candidate.Controlled ? 0.42f : 0f));

            if (candidate.Controlled)
                return new EarthRepresentationDecision(EarthRepresentationTier.HeroPhysical, 1f, true);

            bool heroRoom = pressure.HeroCount < budget.HeroPhysical;
            if (candidate.GameplayRelevant && heroRoom &&
                (candidate.Distance <= budget.HeroDistance || score >= 0.62f))
                return new EarthRepresentationDecision(EarthRepresentationTier.HeroPhysical, score, true);

            bool secondaryRoom = pressure.SecondaryCount < budget.SecondaryPhysical;
            if (secondaryRoom && (candidate.Distance <= budget.SecondaryDistance || score >= 0.36f))
                return new EarthRepresentationDecision(EarthRepresentationTier.SecondaryPhysical, score, true);

            bool visualRoom = pressure.VisualCount < budget.VisualGpu;
            if (visualRoom && (candidate.ScreenSize01 >= 0.006f || candidate.RecentInteraction01 > 0.1f))
                return new EarthRepresentationDecision(EarthRepresentationTier.VisualOnlyGpu, score, true);

            return new EarthRepresentationDecision(EarthRepresentationTier.DormantRecord, score, false);
        }
    }
}
