using System;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public readonly struct EarthPhysicalTargetHandle
    {
        public EarthPhysicalTargetHandle(uint stableId, uint generation)
        {
            StableId = stableId;
            Generation = generation;
        }

        public uint StableId { get; }
        public uint Generation { get; }
        public bool IsValid => StableId != 0u;
    }

    public enum EarthPhysicalTargetKind : byte
    {
        Rock = 0,
        Wall = 1,
        WallPiece = 2,
        PlatformPiece = 3,
        Pillar = 4,
        Platform = 5,
        WaveCell = 6,
        ArmorPiece = 7,
        ResonanceProjectile = 8
    }

    [Flags]
    public enum EarthTargetCapabilities : ushort
    {
        None = 0,
        Grab = 1 << 0,
        Push = 1 << 1,
        Gravity = 1 << 2,
        Damage = 1 << 3,
        Pluck = 1 << 4,
        Repair = 1 << 5,
        Surface = 1 << 6,
        Draw = 1 << 7
    }

    public enum EarthMagicGripKind : byte
    {
        Telekinesis = 0,
        VectorField = 1,
        GravityWell = 2,
        Repair = 3
    }

    public interface IEarthPhysicalTarget
    {
        Rigidbody Body { get; }
        uint StableEarthId { get; }
        EarthPhysicalTargetHandle TargetHandle { get; }
        float EarthMass { get; }
        EarthPhysicalTargetKind TargetKind { get; }
        bool IsEarthTargetValid { get; }
        void OnEarthMagicGrabbed(EarthMagicGripKind grip);
        void OnEarthMagicReleased(EarthMagicGripKind grip);
    }

    /// <summary>
    /// Fixed-capacity, allocation-free ownership of one MMB gravity-grip operation.
    /// Handles make a pooled object invalid as soon as it is activated for another generation.
    /// </summary>
    public sealed class EarthGravityGripSession
    {
        private readonly IEarthPhysicalTarget[] _targets;
        private readonly EarthPhysicalTargetHandle[] _handles;

        public EarthGravityGripSession(int capacity)
        {
            _targets = new IEarthPhysicalTarget[Mathf.Clamp(capacity, 1, 48)];
            _handles = new EarthPhysicalTargetHandle[_targets.Length];
        }

        public int Count { get; private set; }
        public int Capacity => _targets.Length;

        public IEarthPhysicalTarget GetTarget(int index)
        {
            if (index < 0 || index >= Count) return null;
            IEarthPhysicalTarget target = _targets[index];
            return target != null && SameHandle(_handles[index], target.TargetHandle) ? target : null;
        }

        public bool TryAdd(IEarthPhysicalTarget target, int requestedLimit)
        {
            if (target == null || !target.IsEarthTargetValid || !target.TargetHandle.IsValid) return false;
            EarthPhysicalTargetHandle handle = target.TargetHandle;
            for (int index = 0; index < Count; index++)
                if (SameHandle(_handles[index], handle)) return false;
            int limit = Mathf.Clamp(requestedLimit, 1, Capacity);
            if (Count >= limit) return false;
            _targets[Count] = target;
            _handles[Count] = handle;
            Count++;
            return true;
        }

        public void RemoveAtSwapBack(int index)
        {
            if (index < 0 || index >= Count) return;
            int last = --Count;
            _targets[index] = _targets[last];
            _handles[index] = _handles[last];
            _targets[last] = null;
            _handles[last] = default;
        }

        public void ReleaseAll(EarthMagicGripKind grip)
        {
            for (int index = 0; index < Count; index++)
            {
                IEarthPhysicalTarget target = GetTarget(index);
                target?.OnEarthMagicReleased(grip);
                _targets[index] = null;
                _handles[index] = default;
            }
            Count = 0;
        }

        private static bool SameHandle(EarthPhysicalTargetHandle left, EarthPhysicalTargetHandle right) =>
            left.StableId == right.StableId && left.Generation == right.Generation;
    }

    public interface IEarthFractureSource
    {
        event Action<IEarthFractureSource> TargetsActivated;
        uint StructureId { get; }
        bool IsFractured { get; }
        int CopyActiveTargetsNonAlloc(IEarthPhysicalTarget[] destination);
    }
}
