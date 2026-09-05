using System;
using Elemental.Simulation.Matter;
using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum EarthTechniqueId : ushort
    {
        None = 0,
        RaiseWall = 1,
        PullStone = 2,
        ThrowStone = 3,
        RaisePlatform = 4,
        VectorPush = 5,
        PillarJump = 6,
        WebWave = 7,
        GravityGrip = 8,
        Repair = 9,
        Armor = 10,
        Surf = 11,
        Resonance = 12,
        WallSlide = 20,
        FractureFan = 21,
        CrestPluck = 22,
        SurfConversion = 23,
        SpearMorph = 24,
        MeteorFinish = 25,
        ArmorDome = 26,
        ArmorOrbit = 27,
        ArmorBarrage = 28,
        ArmorRepack = 29,
        PartialRepairFeint = 30,
        CompressedDrill = 31,
        TerrainStitch = 32,
        SubsurfaceReturn = 33,
        LaunchRamp = 34,
        RearWall = 35,
        FaultLine = 36,
        QuickStonePunch = 37
    }

    [Flags]
    public enum EarthEventTag : ushort
    {
        None = 0,
        Formed = 1 << 0,
        Propelled = 1 << 1,
        Impacted = 1 << 2,
        Fractured = 1 << 3,
        PartiallyRepaired = 1 << 4,
        Repaired = 1 << 5,
        Reintegrated = 1 << 6,
        Launched = 1 << 7,
        Airborne = 1 << 8,
        MovingSurface = 1 << 9
    }

    public readonly struct EarthMoveRecord
    {
        public EarthMoveRecord(
            EarthTechniqueId technique,
            EarthMatterId primaryMatter,
            EarthEventTag result,
            uint startTick,
            uint commitTick,
            float energy,
            float3 direction)
        {
            Technique = technique;
            PrimaryMatter = primaryMatter;
            Result = result;
            StartTick = startTick;
            CommitTick = commitTick;
            Energy = math.max(0f, energy);
            Direction = math.normalizesafe(direction);
        }
        public EarthTechniqueId Technique { get; }
        public EarthMatterId PrimaryMatter { get; }
        public EarthEventTag Result { get; }
        public uint StartTick { get; }
        public uint CommitTick { get; }
        public float Energy { get; }
        public float3 Direction { get; }
    }

    public sealed class EarthMoveHistory
    {
        private readonly EarthMoveRecord[] _records;
        private int _next;
        public EarthMoveHistory(int capacity = 16) => _records = new EarthMoveRecord[math.clamp(capacity, 4, 32)];
        public int Count { get; private set; }

        public void Add(in EarthMoveRecord record)
        {
            _records[_next] = record;
            _next = (_next + 1) % _records.Length;
            Count = math.min(Count + 1, _records.Length);
        }

        public bool TryGetFromNewest(int offset, out EarthMoveRecord record)
        {
            if (offset < 0 || offset >= Count)
            {
                record = default;
                return false;
            }
            int index = (_next - 1 - offset + _records.Length) % _records.Length;
            record = _records[index];
            return true;
        }

        public int CopyNewestNonAlloc(EarthMoveRecord[] destination)
        {
            int count = math.min(destination?.Length ?? 0, Count);
            for (int index = 0; index < count; index++) TryGetFromNewest(index, out destination[index]);
            return count;
        }
    }

    public readonly struct EarthComboOpportunity
    {
        public EarthComboOpportunity(
            EarthTechniqueId technique,
            EarthMatterId matter,
            float score,
            uint expiresAtTick,
            EarthEventTag requiredResult)
        {
            Technique = technique;
            Matter = matter;
            Score = math.saturate(score);
            ExpiresAtTick = expiresAtTick;
            RequiredResult = requiredResult;
        }
        public EarthTechniqueId Technique { get; }
        public EarthMatterId Matter { get; }
        public float Score { get; }
        public uint ExpiresAtTick { get; }
        public EarthEventTag RequiredResult { get; }
        public bool IsAvailable(uint tick) => Technique != EarthTechniqueId.None && tick <= ExpiresAtTick;
    }

    public static class EarthComboResolver
    {
        private const uint DefaultWindowTicks = 150u;

        public static int ResolveNonAlloc(
            EarthMoveHistory history,
            uint currentTick,
            EarthMatterId activeMatter,
            EarthComboOpportunity[] destination)
        {
            if (history == null || destination == null || destination.Length == 0 ||
                !history.TryGetFromNewest(0, out EarthMoveRecord latest)) return 0;
            if (currentTick > latest.CommitTick + DefaultWindowTicks) return 0;
            EarthMatterId matter = activeMatter.IsValid ? activeMatter : latest.PrimaryMatter;
            float continuity = activeMatter.IsValid && latest.PrimaryMatter == activeMatter ? 1f : 0.82f;
            int count = 0;
            switch (latest.Technique)
            {
                case EarthTechniqueId.RaiseWall:
                    Add(destination, ref count, EarthTechniqueId.WallSlide, matter, 0.96f * continuity, latest, EarthEventTag.Formed);
                    Add(destination, ref count, EarthTechniqueId.FractureFan, matter, 0.88f * continuity, latest, EarthEventTag.Formed);
                    break;
                case EarthTechniqueId.PillarJump:
                    Add(destination, ref count, EarthTechniqueId.SpearMorph, matter, 0.94f * continuity, latest, EarthEventTag.Airborne);
                    Add(destination, ref count, EarthTechniqueId.MeteorFinish, matter, 0.82f * continuity, latest, EarthEventTag.Airborne);
                    break;
                case EarthTechniqueId.WebWave:
                    Add(destination, ref count, EarthTechniqueId.CrestPluck, matter, 0.96f * continuity, latest, EarthEventTag.Formed);
                    Add(destination, ref count, EarthTechniqueId.SurfConversion, matter, 0.90f * continuity, latest, EarthEventTag.Formed);
                    break;
                case EarthTechniqueId.Armor:
                case EarthTechniqueId.ArmorDome:
                case EarthTechniqueId.ArmorOrbit:
                    Add(destination, ref count, EarthTechniqueId.ArmorBarrage, matter, 0.95f * continuity, latest, EarthEventTag.Formed);
                    Add(destination, ref count, EarthTechniqueId.ArmorRepack, matter, 0.92f * continuity, latest, EarthEventTag.Reintegrated);
                    break;
                case EarthTechniqueId.Repair:
                    Add(destination, ref count, EarthTechniqueId.PartialRepairFeint, matter, 0.98f * continuity, latest, EarthEventTag.PartiallyRepaired);
                    Add(destination, ref count, EarthTechniqueId.TerrainStitch, matter, 0.86f * continuity, latest, EarthEventTag.Reintegrated);
                    break;
                case EarthTechniqueId.FractureFan:
                    Add(destination, ref count, EarthTechniqueId.GravityGrip, matter, 0.91f * continuity, latest, EarthEventTag.Fractured);
                    Add(destination, ref count, EarthTechniqueId.SubsurfaceReturn, matter, 0.84f * continuity, latest, EarthEventTag.Reintegrated);
                    break;
                case EarthTechniqueId.Surf:
                    Add(destination, ref count, EarthTechniqueId.LaunchRamp, matter, 0.94f * continuity, latest, EarthEventTag.MovingSurface);
                    Add(destination, ref count, EarthTechniqueId.RearWall, matter, 0.87f * continuity, latest, EarthEventTag.MovingSurface);
                    break;
            }
            Sort(destination, count);
            return count;
        }

        private static void Add(
            EarthComboOpportunity[] output,
            ref int count,
            EarthTechniqueId technique,
            EarthMatterId matter,
            float score,
            in EarthMoveRecord latest,
            EarthEventTag required)
        {
            if (count >= output.Length) return;
            output[count++] = new EarthComboOpportunity(
                technique, matter, score, latest.CommitTick + DefaultWindowTicks, required);
        }

        private static void Sort(EarthComboOpportunity[] output, int count)
        {
            for (int index = 1; index < count; index++)
            {
                EarthComboOpportunity value = output[index];
                int cursor = index - 1;
                while (cursor >= 0 && output[cursor].Score < value.Score)
                {
                    output[cursor + 1] = output[cursor];
                    cursor--;
                }
                output[cursor + 1] = value;
            }
        }
    }
}
