using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Matter
{
    public enum EarthMatterRegistryFailure : byte
    {
        None = 0,
        CapacityExhausted = 1,
        InvalidRecord = 2,
        StaleHandle = 3,
        IllegalPhaseTransition = 4,
        RecordNotConsumed = 5
    }

    /// <summary>
    /// Fixed-capacity canonical registry. Lookup is deliberately array-based: the
    /// project has bounded hero/secondary physical budgets and steady-state paths
    /// must not allocate or invalidate pooled handles.
    /// </summary>
    public sealed class EarthMatterRegistry
    {
        private readonly EarthMatterRecord[] _records;
        private readonly bool[] _occupied;
        private uint _nextStableId = 1u;

        public EarthMatterRegistry(int capacity)
        {
            Capacity = math.clamp(capacity, 32, 8192);
            _records = new EarthMatterRecord[Capacity];
            _occupied = new bool[Capacity];
        }

        public int Capacity { get; }
        public int ActiveCount { get; private set; }
        public EarthMatterRegistryFailure LastFailure { get; private set; }

        public int CountByRepresentation(EarthRepresentationTier tier)
        {
            int count = 0;
            for (int index = 0; index < _records.Length; index++)
                if (_occupied[index] && _records[index].Representation == tier) count++;
            return count;
        }

        public bool TryRegister(in EarthMatterRecord authored, out EarthMatterId id)
        {
            id = default;
            int slot = FindReusableSlot(-1);
            if (slot < 0)
            {
                LastFailure = EarthMatterRegistryFailure.CapacityExhausted;
                return false;
            }

            bool recycledSlot = _occupied[slot];
            if (recycledSlot)
            {
                EarthMatterId consumed = _records[slot].Id;
                ushort generation = consumed.Generation == ushort.MaxValue
                    ? (ushort)1
                    : (ushort)(consumed.Generation + 1);
                id = new EarthMatterId(consumed.StableId, generation);
            }
            else
            {
                id = new EarthMatterId(AllocateStableId(), 1);
            }
            EarthMatterRecord record = authored;
            record.Id = id;
            if (!ValidateRecord(in record))
            {
                LastFailure = EarthMatterRegistryFailure.InvalidRecord;
                id = default;
                return false;
            }

            _records[slot] = record;
            _occupied[slot] = true;
            if (!recycledSlot) ActiveCount++;
            LastFailure = EarthMatterRegistryFailure.None;
            return true;
        }

        public bool TryGet(EarthMatterId id, out EarthMatterRecord record)
        {
            int slot = FindSlot(id);
            if (slot < 0)
            {
                record = default;
                LastFailure = EarthMatterRegistryFailure.StaleHandle;
                return false;
            }
            record = _records[slot];
            LastFailure = EarthMatterRegistryFailure.None;
            return true;
        }

        public bool TryTransition(EarthMatterId id, EarthMatterPhase next)
        {
            int slot = FindSlot(id);
            if (slot < 0)
            {
                LastFailure = EarthMatterRegistryFailure.StaleHandle;
                return false;
            }
            EarthMatterRecord record = _records[slot];
            if (!EarthMatterPhaseRules.CanTransition(record.Phase, next))
            {
                LastFailure = EarthMatterRegistryFailure.IllegalPhaseTransition;
                return false;
            }
            record.Phase = next;
            if (next == EarthMatterPhase.Consumed)
                record.Representation = EarthRepresentationTier.DormantRecord;
            _records[slot] = record;
            LastFailure = EarthMatterRegistryFailure.None;
            return true;
        }

        public bool TrySetKinematics(
            EarthMatterId id,
            in EarthMatterPose pose,
            float3 linearVelocity,
            float3 angularVelocity)
        {
            int slot = FindSlot(id);
            if (slot < 0 || !pose.IsFinite || !math.all(math.isfinite(linearVelocity)) ||
                !math.all(math.isfinite(angularVelocity)))
            {
                LastFailure = slot < 0
                    ? EarthMatterRegistryFailure.StaleHandle
                    : EarthMatterRegistryFailure.InvalidRecord;
                return false;
            }
            EarthMatterRecord record = _records[slot];
            record.CurrentPose = pose;
            record.LinearVelocity = linearVelocity;
            record.AngularVelocity = angularVelocity;
            _records[slot] = record;
            LastFailure = EarthMatterRegistryFailure.None;
            return true;
        }

        public bool TrySetRepresentation(EarthMatterId id, EarthRepresentationTier representation)
        {
            int slot = FindSlot(id);
            if (slot < 0)
            {
                LastFailure = EarthMatterRegistryFailure.StaleHandle;
                return false;
            }
            EarthMatterRecord record = _records[slot];
            record.Representation = representation;
            _records[slot] = record;
            LastFailure = EarthMatterRegistryFailure.None;
            return true;
        }

        public bool TryTransferOwner(EarthMatterId id, EarthOwnerId owner)
        {
            int slot = FindSlot(id);
            if (slot < 0)
            {
                LastFailure = EarthMatterRegistryFailure.StaleHandle;
                return false;
            }
            EarthMatterRecord record = _records[slot];
            record.Owner = owner;
            _records[slot] = record;
            LastFailure = EarthMatterRegistryFailure.None;
            return true;
        }

        public bool TryUpdateProvenance(EarthMatterId id, in EarthSourceProvenance source)
        {
            int slot = FindSlot(id);
            if (slot < 0)
            {
                LastFailure = EarthMatterRegistryFailure.StaleHandle;
                return false;
            }
            EarthMatterRecord record = _records[slot];
            record.Source = source;
            _records[slot] = record;
            LastFailure = EarthMatterRegistryFailure.None;
            return true;
        }

        public bool TryRecycleConsumed(EarthMatterId consumedId, in EarthMatterRecord replacement, out EarthMatterId nextId)
        {
            nextId = default;
            int slot = FindSlot(consumedId);
            if (slot < 0)
            {
                LastFailure = EarthMatterRegistryFailure.StaleHandle;
                return false;
            }
            if (_records[slot].Phase != EarthMatterPhase.Consumed)
            {
                LastFailure = EarthMatterRegistryFailure.RecordNotConsumed;
                return false;
            }
            ushort nextGeneration = consumedId.Generation == ushort.MaxValue
                ? (ushort)1
                : (ushort)(consumedId.Generation + 1);
            nextId = new EarthMatterId(consumedId.StableId, nextGeneration);
            EarthMatterRecord record = replacement;
            record.Id = nextId;
            if (!ValidateRecord(in record))
            {
                nextId = default;
                LastFailure = EarthMatterRegistryFailure.InvalidRecord;
                return false;
            }
            _records[slot] = record;
            LastFailure = EarthMatterRegistryFailure.None;
            return true;
        }

        public bool TrySplit(
            EarthMatterId parentId,
            EarthMatterRecord[] children,
            int childCount,
            EarthMatterId[] childIds,
            float maximumRelativeVolumeError = 0.03f)
        {
            int parentSlot = FindSlot(parentId);
            int count = math.min(children?.Length ?? 0, math.max(0, childCount));
            if (parentSlot < 0)
            {
                LastFailure = EarthMatterRegistryFailure.StaleHandle;
                return false;
            }
            if (count <= 0 || childIds == null || childIds.Length < count)
            {
                LastFailure = EarthMatterRegistryFailure.InvalidRecord;
                return false;
            }
            int freeCount = 0;
            for (int slot = 0; slot < _occupied.Length; slot++)
                if (!_occupied[slot] ||
                    (slot != parentSlot && _records[slot].Phase == EarthMatterPhase.Consumed))
                    freeCount++;
            if (freeCount < count)
            {
                LastFailure = EarthMatterRegistryFailure.CapacityExhausted;
                return false;
            }

            EarthMatterRecord parent = _records[parentSlot];
            float volume = 0f;
            float mass = 0f;
            for (int index = 0; index < count; index++)
            {
                EarthMatterRecord child = children[index];
                child.Id = new EarthMatterId(1u, 1);
                if (!ValidateRecord(in child))
                {
                    LastFailure = EarthMatterRegistryFailure.InvalidRecord;
                    return false;
                }
                volume += child.Volume;
                mass += child.Mass;
            }
            if (EarthMatterVolumeLedger.RelativeVolumeError(parent.Volume, volume) > maximumRelativeVolumeError ||
                EarthMatterVolumeLedger.RelativeVolumeError(parent.Mass, mass) > maximumRelativeVolumeError)
            {
                LastFailure = EarthMatterRegistryFailure.InvalidRecord;
                return false;
            }

            parent.Phase = EarthMatterPhase.Consumed;
            parent.Representation = EarthRepresentationTier.DormantRecord;
            _records[parentSlot] = parent;
            for (int index = 0; index < count; index++)
            {
                int slot = FindReusableSlot(parentSlot);
                bool recycledSlot = _occupied[slot];
                EarthMatterId childId;
                if (recycledSlot)
                {
                    EarthMatterId consumed = _records[slot].Id;
                    ushort generation = consumed.Generation == ushort.MaxValue
                        ? (ushort)1
                        : (ushort)(consumed.Generation + 1);
                    childId = new EarthMatterId(consumed.StableId, generation);
                }
                else
                {
                    childId = new EarthMatterId(AllocateStableId(), 1);
                }
                EarthMatterRecord child = children[index];
                child.Id = childId;
                _records[slot] = child;
                _occupied[slot] = true;
                childIds[index] = childId;
                if (!recycledSlot) ActiveCount++;
            }
            LastFailure = EarthMatterRegistryFailure.None;
            return true;
        }

        public bool TryMerge(
            EarthMatterId consumedParentId,
            EarthMatterId[] children,
            int childCount,
            in EarthMatterRecord replacement,
            out EarthMatterId restoredParentId,
            float maximumRelativeVolumeError = 0.03f)
        {
            restoredParentId = default;
            int parentSlot = FindSlot(consumedParentId);
            int count = math.min(children?.Length ?? 0, math.max(0, childCount));
            if (parentSlot < 0)
            {
                LastFailure = EarthMatterRegistryFailure.StaleHandle;
                return false;
            }
            if (_records[parentSlot].Phase != EarthMatterPhase.Consumed || count <= 0)
            {
                LastFailure = EarthMatterRegistryFailure.InvalidRecord;
                return false;
            }

            float mergedVolume = 0f;
            float mergedMass = 0f;
            for (int index = 0; index < count; index++)
            {
                int childSlot = FindSlot(children[index]);
                if (childSlot < 0 || _records[childSlot].Phase == EarthMatterPhase.Consumed)
                {
                    LastFailure = childSlot < 0
                        ? EarthMatterRegistryFailure.StaleHandle
                        : EarthMatterRegistryFailure.InvalidRecord;
                    return false;
                }
                mergedVolume += _records[childSlot].Volume;
                mergedMass += _records[childSlot].Mass;
            }

            EarthMatterRecord candidate = replacement;
            candidate.Id = new EarthMatterId(consumedParentId.StableId,
                consumedParentId.Generation == ushort.MaxValue ? (ushort)1 : (ushort)(consumedParentId.Generation + 1));
            if (!ValidateRecord(in candidate) ||
                EarthMatterVolumeLedger.RelativeVolumeError(candidate.Volume, mergedVolume) > maximumRelativeVolumeError ||
                EarthMatterVolumeLedger.RelativeVolumeError(candidate.Mass, mergedMass) > maximumRelativeVolumeError)
            {
                LastFailure = EarthMatterRegistryFailure.InvalidRecord;
                return false;
            }

            for (int index = 0; index < count; index++)
            {
                int childSlot = FindSlot(children[index]);
                EarthMatterRecord child = _records[childSlot];
                child.Phase = EarthMatterPhase.Consumed;
                child.Representation = EarthRepresentationTier.DormantRecord;
                _records[childSlot] = child;
            }
            return TryRecycleConsumed(consumedParentId, candidate, out restoredParentId);
        }

        public int CopyActiveNonAlloc(EarthMatterRecord[] destination, bool includeConsumed = false)
        {
            if (destination == null || destination.Length == 0) return 0;
            int output = 0;
            for (int slot = 0; slot < _records.Length && output < destination.Length; slot++)
            {
                if (!_occupied[slot]) continue;
                EarthMatterRecord record = _records[slot];
                if (!includeConsumed && record.Phase == EarthMatterPhase.Consumed) continue;
                destination[output++] = record;
            }
            return output;
        }

        private int FindFreeSlot()
        {
            for (int slot = 0; slot < _occupied.Length; slot++)
                if (!_occupied[slot]) return slot;
            return -1;
        }

        private int FindReusableSlot(int excludedSlot)
        {
            for (int slot = 0; slot < _occupied.Length; slot++)
            {
                if (slot == excludedSlot) continue;
                if (_occupied[slot] && _records[slot].Phase == EarthMatterPhase.Consumed)
                    return slot;
            }
            for (int slot = 0; slot < _occupied.Length; slot++)
                if (slot != excludedSlot && !_occupied[slot]) return slot;
            return -1;
        }

        private int FindSlot(EarthMatterId id)
        {
            if (!id.IsValid) return -1;
            for (int slot = 0; slot < _records.Length; slot++)
                if (_occupied[slot] && _records[slot].Id == id) return slot;
            return -1;
        }

        private uint AllocateStableId()
        {
            uint id = _nextStableId++;
            if (_nextStableId == 0u) _nextStableId = 1u;
            return id == 0u ? _nextStableId++ : id;
        }

        private static bool ValidateRecord(in EarthMatterRecord record) =>
            record.IsFiniteAndPhysical && record.Phase != EarthMatterPhase.Consumed &&
            record.Representation != EarthRepresentationTier.DormantRecord;
    }

    public static class EarthMatterPhaseRules
    {
        public static bool CanTransition(EarthMatterPhase current, EarthMatterPhase next)
        {
            if (current == next) return true;
            return current switch
            {
                EarthMatterPhase.TerrainAttached => next == EarthMatterPhase.Forming ||
                                                    next == EarthMatterPhase.CapturedForReturn,
                EarthMatterPhase.Forming => next == EarthMatterPhase.Controlled ||
                                            next == EarthMatterPhase.FreeDynamic ||
                                            next == EarthMatterPhase.CapturedForReturn ||
                                            next == EarthMatterPhase.Returning,
                EarthMatterPhase.Controlled => next == EarthMatterPhase.FreeDynamic ||
                                               next == EarthMatterPhase.CapturedForReturn ||
                                               next == EarthMatterPhase.Returning,
                EarthMatterPhase.FreeDynamic => next == EarthMatterPhase.Controlled ||
                                                next == EarthMatterPhase.Sleeping ||
                                                next == EarthMatterPhase.CapturedForReturn,
                EarthMatterPhase.Sleeping => next == EarthMatterPhase.Controlled ||
                                            next == EarthMatterPhase.FreeDynamic ||
                                            next == EarthMatterPhase.CapturedForReturn,
                EarthMatterPhase.CapturedForReturn => next == EarthMatterPhase.Returning ||
                                                      next == EarthMatterPhase.FreeDynamic,
                EarthMatterPhase.Returning => next == EarthMatterPhase.Reintegrating ||
                                              next == EarthMatterPhase.FreeDynamic,
                EarthMatterPhase.Reintegrating => next == EarthMatterPhase.TerrainAttached ||
                                                  next == EarthMatterPhase.Consumed ||
                                                  next == EarthMatterPhase.FreeDynamic,
                EarthMatterPhase.Consumed => false,
                _ => false
            };
        }
    }

    public readonly struct EarthMatterLedgerSnapshot
    {
        public EarthMatterLedgerSnapshot(float liveVolume, float liveMass, float reintegratingVolume, int liveCount)
        {
            LiveVolume = liveVolume;
            LiveMass = liveMass;
            ReintegratingVolume = reintegratingVolume;
            LiveCount = liveCount;
        }
        public float LiveVolume { get; }
        public float LiveMass { get; }
        public float ReintegratingVolume { get; }
        public int LiveCount { get; }
    }

    public static class EarthMatterVolumeLedger
    {
        public static EarthMatterLedgerSnapshot Calculate(EarthMatterRecord[] records, int count)
        {
            float volume = 0f;
            float mass = 0f;
            float reintegrating = 0f;
            int live = 0;
            int safeCount = math.min(records?.Length ?? 0, math.max(0, count));
            for (int index = 0; index < safeCount; index++)
            {
                EarthMatterRecord record = records[index];
                if (!record.Id.IsValid || record.Phase == EarthMatterPhase.Consumed) continue;
                volume += math.max(0f, record.Volume);
                mass += math.max(0f, record.Mass);
                if (record.Phase == EarthMatterPhase.Reintegrating)
                    reintegrating += math.max(0f, record.Volume);
                live++;
            }
            return new EarthMatterLedgerSnapshot(volume, mass, reintegrating, live);
        }

        public static float RelativeVolumeError(float expected, float actual) =>
            math.abs(actual - expected) / math.max(0.000001f, math.abs(expected));
    }
}
