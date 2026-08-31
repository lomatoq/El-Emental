using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public readonly struct EarthRecoveryPoseMatchWeights
    {
        public EarthRecoveryPoseMatchWeights(
            float chest,
            float hands,
            float feet,
            float chestOutward)
        {
            Chest = math.max(0f, chest);
            Hands = math.max(0f, hands);
            Feet = math.max(0f, feet);
            ChestOutward = math.max(0f, chestOutward);
        }

        public static EarthRecoveryPoseMatchWeights Default =>
            new EarthRecoveryPoseMatchWeights(1.4f, 0.75f, 1f, 1.25f);

        public float Chest { get; }
        public float Hands { get; }
        public float Feet { get; }
        public float ChestOutward { get; }
    }

    public readonly struct EarthRecoveryPoseMatch
    {
        public EarthRecoveryPoseMatch(
            int databaseIndex,
            in EarthRecoveryPoseCandidate candidate,
            float cost)
        {
            DatabaseIndex = databaseIndex;
            Candidate = candidate;
            Cost = cost;
        }

        public int DatabaseIndex { get; }
        public EarthRecoveryPoseCandidate Candidate { get; }
        public float Cost { get; }
        public bool IsValid => DatabaseIndex >= 0 && Candidate.IsUsable && math.isfinite(Cost);
    }

    public static class EarthRecoveryPoseMatcher
    {
        private const float TieEpsilon = 0.000001f;

        public static bool TryMatch(
            EarthRecoveryPoseDatabase database,
            EarthRecoveryOrientation orientation,
            in EarthRecoveryPoseFeature current,
            in EarthRecoveryPoseMatchWeights weights,
            out EarthRecoveryPoseMatch match)
        {
            match = default;
            if (database == null || database.Count == 0 ||
                orientation == EarthRecoveryOrientation.Unknown || !current.IsFinite)
                return false;

            int bestIndex = -1;
            float bestCost = float.PositiveInfinity;
            EarthRecoveryPoseCandidate best = default;
            for (int index = 0; index < database.Count; index++)
            {
                if (!database.TryGetCandidate(index, out EarthRecoveryPoseCandidate candidate) ||
                    !candidate.IsUsable || candidate.Orientation != orientation)
                    continue;

                EarthRecoveryPoseFeature candidateFeature = candidate.Feature;
                float cost = Score(in current, in candidateFeature, in weights);
                if (!math.isfinite(cost)) continue;
                bool better = cost < bestCost - TieEpsilon;
                if (!better && math.abs(cost - bestCost) <= TieEpsilon && bestIndex >= 0)
                {
                    better = candidate.ClipId < best.ClipId ||
                             (candidate.ClipId == best.ClipId &&
                              candidate.EntryPhase < best.EntryPhase);
                }
                if (!better && bestIndex >= 0) continue;
                bestIndex = index;
                bestCost = cost;
                best = candidate;
            }

            if (bestIndex < 0) return false;
            match = new EarthRecoveryPoseMatch(bestIndex, in best, bestCost);
            return true;
        }

        public static float Score(
            in EarthRecoveryPoseFeature current,
            in EarthRecoveryPoseFeature candidate,
            in EarthRecoveryPoseMatchWeights weights)
        {
            float chest = math.lengthsq(current.ChestOffset - candidate.ChestOffset);
            float hands = math.lengthsq(current.LeftHandOffset - candidate.LeftHandOffset) +
                          math.lengthsq(current.RightHandOffset - candidate.RightHandOffset);
            float feet = math.lengthsq(current.LeftFootOffset - candidate.LeftFootOffset) +
                         math.lengthsq(current.RightFootOffset - candidate.RightFootOffset);
            float outward = math.lengthsq(current.ChestOutward - candidate.ChestOutward);
            return chest * weights.Chest +
                   hands * weights.Hands +
                   feet * weights.Feet +
                   outward * weights.ChestOutward;
        }
    }
}
