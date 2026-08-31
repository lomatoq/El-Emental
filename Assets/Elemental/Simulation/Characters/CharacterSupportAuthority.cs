using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum CharacterSupportKind : byte
    {
        Unknown = 0,
        PlanetGround = 1,
        ArenaWalkableProxy = 2,
        MovingAbilitySurface = 3,
        ReleasedFracture = 4,
        DynamicDebris = 5
    }

    public readonly struct CharacterSupportCandidate
    {
        public CharacterSupportCandidate(
            uint surfaceId,
            uint generation,
            CharacterSupportKind kind,
            float distance,
            float upDot,
            bool isValid,
            bool isWalkable)
        {
            SurfaceId = surfaceId;
            Generation = generation;
            Kind = kind;
            Distance = math.max(0f, math.isfinite(distance) ? distance : float.MaxValue);
            UpDot = math.clamp(math.isfinite(upDot) ? upDot : -1f, -1f, 1f);
            IsValid = isValid && surfaceId != 0u;
            IsWalkable = isWalkable;
        }

        public uint SurfaceId { get; }
        public uint Generation { get; }
        public CharacterSupportKind Kind { get; }
        public float Distance { get; }
        public float UpDot { get; }
        public bool IsValid { get; }
        public bool IsWalkable { get; }
    }

    public readonly struct CharacterSupportSelection
    {
        public CharacterSupportSelection(
            bool hasSupport,
            CharacterSupportCandidate candidate,
            bool retainedPrevious)
        {
            HasSupport = hasSupport;
            Candidate = candidate;
            RetainedPrevious = retainedPrevious;
        }

        public bool HasSupport { get; }
        public CharacterSupportCandidate Candidate { get; }
        public bool RetainedPrevious { get; }

        public static CharacterSupportSelection None =>
            new CharacterSupportSelection(false, default, false);
    }

    public static class CharacterSupportAuthority
    {
        public static CharacterSupportSelection Select(
            CharacterSupportCandidate[] candidates,
            int count,
            in CharacterSupportSelection previous,
            float minimumWalkableUpDot,
            float retentionDistanceMetres)
        {
            if (candidates == null || count <= 0) return CharacterSupportSelection.None;
            int safeCount = math.min(count, candidates.Length);
            float minimumUp = math.clamp(minimumWalkableUpDot, -1f, 1f);
            float retention = math.max(0f, retentionDistanceMetres);

            int bestIndex = -1;
            int previousIndex = -1;
            for (int index = 0; index < safeCount; index++)
            {
                CharacterSupportCandidate candidate = candidates[index];
                if (!CanOwnCharacterSupport(in candidate, minimumUp)) continue;

                if (previous.HasSupport &&
                    candidate.SurfaceId == previous.Candidate.SurfaceId &&
                    candidate.Generation == previous.Candidate.Generation)
                {
                    previousIndex = index;
                }

                if (bestIndex < 0 || IsPreferred(
                        in candidate,
                        in candidates[bestIndex]))
                    bestIndex = index;
            }

            if (bestIndex < 0) return CharacterSupportSelection.None;
            CharacterSupportCandidate best = candidates[bestIndex];
            if (previousIndex >= 0)
            {
                CharacterSupportCandidate retained = candidates[previousIndex];
                // Distance hysteresis is applied only after semantic classification.
                // It can stabilize two legitimate walkable surfaces, but can never
                // make a released fragment or debris collider become ground.
                if (retained.Distance <= best.Distance + retention)
                    return new CharacterSupportSelection(true, retained, true);
            }

            return new CharacterSupportSelection(true, best, false);
        }

        public static bool CanOwnCharacterSupport(
            in CharacterSupportCandidate candidate,
            float minimumWalkableUpDot)
        {
            if (!candidate.IsValid || !candidate.IsWalkable ||
                candidate.UpDot < minimumWalkableUpDot)
                return false;

            return candidate.Kind == CharacterSupportKind.PlanetGround ||
                   candidate.Kind == CharacterSupportKind.ArenaWalkableProxy ||
                   candidate.Kind == CharacterSupportKind.MovingAbilitySurface;
        }

        public static bool Matches(
            in CharacterSupportCandidate candidate,
            in CharacterSupportCandidate selected) =>
            candidate.SurfaceId == selected.SurfaceId &&
            candidate.Generation == selected.Generation &&
            candidate.Kind == selected.Kind &&
            math.abs(candidate.Distance - selected.Distance) <= 0.0001f &&
            math.abs(candidate.UpDot - selected.UpDot) <= 0.0001f;

        private static bool IsPreferred(
            in CharacterSupportCandidate candidate,
            in CharacterSupportCandidate incumbent)
        {
            int candidatePriority = Priority(candidate.Kind);
            int incumbentPriority = Priority(incumbent.Kind);
            if (candidatePriority != incumbentPriority)
                return candidatePriority > incumbentPriority;

            if (math.abs(candidate.Distance - incumbent.Distance) > 0.0001f)
                return candidate.Distance < incumbent.Distance;
            if (math.abs(candidate.UpDot - incumbent.UpDot) > 0.0001f)
                return candidate.UpDot > incumbent.UpDot;
            if (candidate.SurfaceId != incumbent.SurfaceId)
                return candidate.SurfaceId < incumbent.SurfaceId;
            return candidate.Generation < incumbent.Generation;
        }

        private static int Priority(CharacterSupportKind kind) => kind switch
        {
            CharacterSupportKind.ArenaWalkableProxy => 3,
            CharacterSupportKind.MovingAbilitySurface => 2,
            CharacterSupportKind.PlanetGround => 1,
            _ => 0
        };
    }
}
