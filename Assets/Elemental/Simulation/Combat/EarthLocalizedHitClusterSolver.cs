using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    public readonly struct EarthLocalizedHitClusterState
    {
        public EarthLocalizedHitClusterState(
            float3 center,
            float startedAt,
            int hitCount,
            float cumulativeVelocityChange = 0f,
            uint sourceA = 0u,
            uint sourceB = 0u,
            uint sourceC = 0u)
        {
            Center = center;
            StartedAt = startedAt;
            HitCount = math.max(0, hitCount);
            CumulativeVelocityChange = math.max(0f, cumulativeVelocityChange);
            SourceA = sourceA != 0u ? sourceA : HitCount > 0 ? 1u : 0u;
            SourceB = sourceB != 0u ? sourceB : HitCount > 1 ? 2u : 0u;
            SourceC = sourceC;
        }

        public float3 Center { get; }
        public float StartedAt { get; }
        public int HitCount { get; }
        public float CumulativeVelocityChange { get; }
        public uint SourceA { get; }
        public uint SourceB { get; }
        public uint SourceC { get; }
    }

    public readonly struct EarthLocalizedHitClusterResult
    {
        public EarthLocalizedHitClusterResult(EarthLocalizedHitClusterState state, bool fullRagdoll)
        {
            State = state;
            FullRagdoll = fullRagdoll;
        }

        public EarthLocalizedHitClusterState State { get; }
        public bool FullRagdoll { get; }
    }

    public static class EarthLocalizedHitClusterSolver
    {
        public const float WindowSeconds = 0.72f;
        public const float RadiusMeters = 0.72f;
        public const int FullRagdollHitCount = 3;
        public const float FullRagdollVelocityChange = 5.5f;

        public static EarthLocalizedHitClusterResult Step(
            in EarthLocalizedHitClusterState state,
            float3 point,
            float time) => Step(
                in state,
                point,
                time,
                unchecked((uint)(state.HitCount + 1)),
                3f);

        public static EarthLocalizedHitClusterResult Step(
            in EarthLocalizedHitClusterState state,
            float3 point,
            float time,
            uint sourceStableId,
            float effectiveVelocityChange)
        {
            bool same = state.HitCount > 0 &&
                        time - state.StartedAt <= WindowSeconds &&
                        math.distancesq(point, state.Center) <= RadiusMeters * RadiusMeters;
            if (!same)
            {
                uint source = sourceStableId != 0u ? sourceStableId : 1u;
                var restarted = new EarthLocalizedHitClusterState(
                    point,
                    time,
                    1,
                    effectiveVelocityChange,
                    source);
                return new EarthLocalizedHitClusterResult(restarted, false);
            }

            uint safeSource = sourceStableId != 0u
                ? sourceStableId
                : unchecked((uint)(state.HitCount + 1));
            bool duplicate = safeSource == state.SourceA || safeSource == state.SourceB || safeSource == state.SourceC;
            if (duplicate) return new EarthLocalizedHitClusterResult(state, false);
            int hitCount = state.HitCount + 1;
            float3 center = math.lerp(state.Center, point, 0.35f);
            uint sourceA = state.SourceA;
            uint sourceB = state.SourceB;
            uint sourceC = state.SourceC;
            if (sourceA == 0u) sourceA = safeSource;
            else if (sourceB == 0u) sourceB = safeSource;
            else if (sourceC == 0u) sourceC = safeSource;
            float cumulative = state.CumulativeVelocityChange + math.max(0f, effectiveVelocityChange);
            var next = new EarthLocalizedHitClusterState(
                center,
                state.StartedAt,
                hitCount,
                cumulative,
                sourceA,
                sourceB,
                sourceC);
            return new EarthLocalizedHitClusterResult(
                next,
                hitCount >= FullRagdollHitCount && cumulative >= FullRagdollVelocityChange);
        }
    }
}
