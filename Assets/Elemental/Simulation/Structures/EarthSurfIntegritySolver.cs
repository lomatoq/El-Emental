using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public enum EarthSurfDamageKind : byte
    {
        None = 0,
        SupportTransfer = 1,
        Bump = 2,
        SideScrape = 3,
        NoseCrash = 4
    }

    public enum EarthSurfCellRole : byte
    {
        LeftFootCore = 0,
        RightFootCore = 1,
        FootBridge = 2,
        Nose = 3,
        OuterRail = 4,
        Tail = 5
    }

    public readonly struct EarthSurfCellDefinition
    {
        public EarthSurfCellDefinition(
            int index,
            EarthSurfCellRole role,
            float2 center01,
            float2 size01,
            ushort neighbourMask)
        {
            Index = index;
            Role = role;
            Center01 = center01;
            Size01 = size01;
            NeighbourMask = neighbourMask;
        }

        public int Index { get; }
        public EarthSurfCellRole Role { get; }
        public float2 Center01 { get; }
        public float2 Size01 { get; }
        public ushort NeighbourMask { get; }
        public bool IsSupportCore => Index >= 0 && Index <= 2;
    }

    /// <summary>
    /// Fixed semantic graph used by both the pure integrity policy and the
    /// prebuilt runtime views. Three central cells carry the rider. The twelve
    /// remaining cells are sacrificial geometry rather than cosmetic counters.
    /// </summary>
    public static class EarthSurfCellGraph
    {
        public const int CellCount = 12;
        public const ushort LeftFootCoreMask = 1 << 0;
        public const ushort RightFootCoreMask = 1 << 1;
        public const ushort FootBridgeMask = 1 << 2;
        public const ushort SupportCoreMask = LeftFootCoreMask | RightFootCoreMask | FootBridgeMask;
        public const ushort AllCellsMask = (1 << CellCount) - 1;
        public const ushort DetachableMask = AllCellsMask & ~SupportCoreMask;

        public static EarthSurfCellDefinition GetDefinition(int index)
        {
            return index switch
            {
                0 => Cell(0, EarthSurfCellRole.LeftFootCore, -0.205f, -0.09f, 0.43f, 0.46f,
                    (1 << 1) | (1 << 2) | (1 << 8) | (1 << 10)),
                1 => Cell(1, EarthSurfCellRole.RightFootCore, 0.205f, -0.09f, 0.43f, 0.46f,
                    (1 << 0) | (1 << 2) | (1 << 9) | (1 << 11)),
                2 => Cell(2, EarthSurfCellRole.FootBridge, 0f, 0.19f, 0.38f, 0.22f,
                    (1 << 0) | (1 << 1) | (1 << 4) | (1 << 6) | (1 << 7)),
                3 => Cell(3, EarthSurfCellRole.Nose, -0.31f, 0.43f, 0.34f, 0.25f,
                    (1 << 4) | (1 << 6)),
                4 => Cell(4, EarthSurfCellRole.Nose, 0f, 0.45f, 0.27f, 0.23f,
                    (1 << 2) | (1 << 3) | (1 << 5)),
                5 => Cell(5, EarthSurfCellRole.Nose, 0.31f, 0.43f, 0.34f, 0.25f,
                    (1 << 4) | (1 << 7)),
                6 => Cell(6, EarthSurfCellRole.OuterRail, -0.43f, 0.16f, 0.18f, 0.28f,
                    (1 << 2) | (1 << 3) | (1 << 8)),
                7 => Cell(7, EarthSurfCellRole.OuterRail, 0.43f, 0.16f, 0.18f, 0.28f,
                    (1 << 2) | (1 << 5) | (1 << 9)),
                8 => Cell(8, EarthSurfCellRole.OuterRail, -0.45f, -0.14f, 0.17f, 0.25f,
                    (1 << 0) | (1 << 6) | (1 << 10)),
                9 => Cell(9, EarthSurfCellRole.OuterRail, 0.45f, -0.14f, 0.17f, 0.25f,
                    (1 << 1) | (1 << 7) | (1 << 11)),
                10 => Cell(10, EarthSurfCellRole.Tail, -0.31f, -0.42f, 0.34f, 0.23f,
                    (1 << 0) | (1 << 8) | (1 << 11)),
                11 => Cell(11, EarthSurfCellRole.Tail, 0.22f, -0.43f, 0.43f, 0.28f,
                    (1 << 1) | (1 << 9) | (1 << 10)),
                _ => default
            };
        }

        public static int CountBits(ushort mask)
        {
            int count = 0;
            uint value = mask;
            while (value != 0u)
            {
                value &= value - 1u;
                count++;
            }
            return count;
        }

        private static EarthSurfCellDefinition Cell(
            int index,
            EarthSurfCellRole role,
            float x,
            float z,
            float width,
            float length,
            int neighbours) =>
            new EarthSurfCellDefinition(index, role, new float2(x, z), new float2(width, length),
                (ushort)neighbours);
    }

    public readonly struct EarthSurfIntegrityState
    {
        public EarthSurfIntegrityState(
            float integrity,
            ushort attachedMask,
            ushort occupiedSupportMask,
            uint eventSequence)
        {
            Integrity = math.clamp(math.isfinite(integrity) ? integrity : 100f, 0f, 100f);
            AttachedMask = (ushort)(attachedMask & EarthSurfCellGraph.AllCellsMask);
            OccupiedSupportMask = (ushort)(occupiedSupportMask & AttachedMask & EarthSurfCellGraph.SupportCoreMask);
            EventSequence = eventSequence;
        }

        public float Integrity { get; }
        public ushort AttachedMask { get; }
        public ushort OccupiedSupportMask { get; }
        public uint EventSequence { get; }
        public int AttachedCellCount => EarthSurfCellGraph.CountBits(AttachedMask);
        public int AttachedOuterCellCount => EarthSurfCellGraph.CountBits((ushort)(AttachedMask & EarthSurfCellGraph.DetachableMask));
        public static EarthSurfIntegrityState Initial => new EarthSurfIntegrityState(
            100f,
            EarthSurfCellGraph.AllCellsMask,
            EarthSurfCellGraph.SupportCoreMask,
            0u);
    }

    public readonly struct EarthSurfDamageEvent
    {
        public EarthSurfDamageEvent(
            EarthSurfDamageKind kind,
            float relativeNormalSpeed,
            float normalDiscontinuityDegrees,
            float contactLocalX,
            uint seed)
        {
            Kind = kind;
            RelativeNormalSpeed = math.max(0f,
                math.isfinite(relativeNormalSpeed) ? relativeNormalSpeed : 0f);
            NormalDiscontinuityDegrees = math.clamp(
                math.isfinite(normalDiscontinuityDegrees) ? normalDiscontinuityDegrees : 0f,
                0f,
                90f);
            ContactLocalX = math.clamp(math.isfinite(contactLocalX) ? contactLocalX : 0f, -1f, 1f);
            Seed = seed;
        }

        public EarthSurfDamageKind Kind { get; }
        public float RelativeNormalSpeed { get; }
        public float NormalDiscontinuityDegrees { get; }
        public float ContactLocalX { get; }
        public uint Seed { get; }
    }

    public readonly struct EarthSurfIntegrityDecision
    {
        public EarthSurfIntegrityDecision(
            in EarthSurfIntegrityState state,
            float damage,
            ushort detachedCellMask,
            bool collapse)
        {
            State = state;
            Damage = math.max(0f, damage);
            DetachedCellMask = (ushort)(detachedCellMask & EarthSurfCellGraph.DetachableMask);
            Collapse = collapse;
        }

        public EarthSurfIntegrityState State { get; }
        public float Integrity => State.Integrity;
        public float Damage { get; }
        public ushort DetachedCellMask { get; }
        public int DetachedOuterCells => EarthSurfCellGraph.CountBits(DetachedCellMask);
        public bool Collapse { get; }
        public float Wear01 => 1f - Integrity / 100f;
    }

    /// <summary>
    /// Deterministic event-only durability policy. It cannot detach the occupied
    /// foot cores or their bridge and it cannot create wear from time/distance.
    /// </summary>
    public static class EarthSurfIntegritySolver
    {
        public static EarthSurfIntegrityDecision Resolve(
            in EarthSurfIntegrityState current,
            in EarthSurfDamageEvent damageEvent)
        {
            EarthSurfIntegrityState safe = new EarthSurfIntegrityState(
                current.Integrity,
                current.AttachedMask,
                current.OccupiedSupportMask,
                current.EventSequence);
            float damage = ResolveDamage(in damageEvent);
            if (damage <= 0f)
                return new EarthSurfIntegrityDecision(in safe, 0f, 0, false);

            int requested = math.clamp((int)math.ceil(damage / 7f), 1, 3);
            ushort available = (ushort)(safe.AttachedMask & EarthSurfCellGraph.DetachableMask &
                                         ~safe.OccupiedSupportMask);
            ushort detached = SelectCells(
                available,
                requested,
                damageEvent.Kind,
                damageEvent.ContactLocalX,
                damageEvent.Seed ^ safe.EventSequence * 0x9E3779B9u);
            ushort nextAttached = (ushort)(safe.AttachedMask & ~detached);
            float nextIntegrity = math.max(0f, safe.Integrity - damage);
            bool thresholdCollapse = safe.Integrity > 12f && nextIntegrity <= 12f;
            bool severeCrash = damageEvent.Kind == EarthSurfDamageKind.NoseCrash &&
                               damageEvent.RelativeNormalSpeed >= 10.5f;
            bool structuralCollapse = EarthSurfCellGraph.CountBits(
                (ushort)(nextAttached & EarthSurfCellGraph.DetachableMask)) <= 1;
            bool collapse = thresholdCollapse || severeCrash || structuralCollapse;
            var next = new EarthSurfIntegrityState(
                nextIntegrity,
                nextAttached,
                safe.OccupiedSupportMask,
                safe.EventSequence + 1u);
            return new EarthSurfIntegrityDecision(in next, damage, detached, collapse);
        }

        private static float ResolveDamage(in EarthSurfDamageEvent damageEvent)
        {
            float speed = damageEvent.RelativeNormalSpeed;
            float angle = damageEvent.NormalDiscontinuityDegrees;
            return damageEvent.Kind switch
            {
                EarthSurfDamageKind.SupportTransfer =>
                    math.clamp((speed - 1.0f) * 2.2f + (angle - 12f) * 0.12f, 0f, 10f),
                EarthSurfDamageKind.Bump =>
                    math.clamp((speed - 1.4f) * 2.8f + (angle - 14f) * 0.15f, 0f, 14f),
                EarthSurfDamageKind.SideScrape =>
                    math.clamp((speed - 2f) * 1.9f, 0f, 18f),
                EarthSurfDamageKind.NoseCrash =>
                    math.lerp(20f, 50f, math.saturate((speed - 5f) / 9f)),
                _ => 0f
            };
        }

        private static ushort SelectCells(
            ushort available,
            int requested,
            EarthSurfDamageKind kind,
            float contactLocalX,
            uint seed)
        {
            ushort selected = 0;
            for (int selection = 0; selection < requested; selection++)
            {
                int bestIndex = -1;
                float bestScore = float.NegativeInfinity;
                for (int index = 0; index < EarthSurfCellGraph.CellCount; index++)
                {
                    ushort bit = (ushort)(1 << index);
                    if ((available & bit) == 0 || (selected & bit) != 0) continue;
                    EarthSurfCellDefinition cell = EarthSurfCellGraph.GetDefinition(index);
                    float score = RolePreference(kind, cell.Role);
                    if (math.abs(contactLocalX) > 0.08f)
                        score += math.sign(contactLocalX) == math.sign(cell.Center01.x) ? 2.25f : -0.65f;
                    score += Hash01(seed ^ (uint)(index * 0x45D9F3B)) * 0.42f;
                    score -= selection * math.abs(cell.Center01.y) * 0.03f;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    bestIndex = index;
                }
                if (bestIndex < 0) break;
                selected |= (ushort)(1 << bestIndex);
            }
            return selected;
        }

        private static float RolePreference(EarthSurfDamageKind kind, EarthSurfCellRole role)
        {
            if (kind == EarthSurfDamageKind.NoseCrash)
                return role == EarthSurfCellRole.Nose ? 8f : role == EarthSurfCellRole.OuterRail ? 4f : 1f;
            if (kind == EarthSurfDamageKind.SideScrape)
                return role == EarthSurfCellRole.OuterRail ? 8f : role == EarthSurfCellRole.Tail ? 3f : 1f;
            if (kind == EarthSurfDamageKind.Bump)
                return role == EarthSurfCellRole.OuterRail ? 6f : role == EarthSurfCellRole.Nose ? 4f : 2f;
            return role == EarthSurfCellRole.Tail ? 5f : role == EarthSurfCellRole.OuterRail ? 4f : 2f;
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }

    public readonly struct EarthSurfWallBandDecision
    {
        public EarthSurfWallBandDecision(float impactHeight01, float damageRadius01, bool accepted)
        {
            ImpactHeight01 = math.clamp(impactHeight01, 0f, 1f);
            DamageRadius01 = math.clamp(damageRadius01, 0.05f, 0.28f);
            Accepted = accepted;
        }

        public float ImpactHeight01 { get; }
        public float DamageRadius01 { get; }
        public bool Accepted { get; }
    }

    public static class EarthSurfWallBandSolver
    {
        public const float MaximumLowerBand01 = 0.32f;

        public static EarthSurfWallBandDecision Resolve(float hitHeight01, float surfSpeed)
        {
            float speed = math.max(0f, math.isfinite(surfSpeed) ? surfSpeed : 0f);
            float clampedHeight = math.min(
                math.clamp(math.isfinite(hitHeight01) ? hitHeight01 : 0f, 0f, 1f),
                MaximumLowerBand01);
            float radius = math.lerp(0.10f, 0.22f, math.saturate((speed - 5f) / 8f));
            return new EarthSurfWallBandDecision(clampedHeight, radius, speed >= 5f);
        }
    }
}
