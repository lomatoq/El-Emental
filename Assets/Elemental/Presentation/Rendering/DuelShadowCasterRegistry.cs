using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public enum DuelShadowCasterClass
    {
        Player = 0,
        Opponent = 1,
        Arena = 2,
        HeroRock = 3,
        ActiveFragment = 4,
        TinyDebris = 5,
        Vfx = 6,
        Other = 7
    }

    public static class DuelShadowCasterPolicy
    {
        public static bool IsSupportedOpaqueRenderer(Renderer renderer)
        {
            return renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
        }

        public static bool IsIncluded(
            DuelShadowCasterClass classification,
            float worldDiameter,
            in DuelShadowClassificationSettings settings)
        {
            if (!DuelShadowMath.IsFinite(worldDiameter) || worldDiameter <= 0f)
                return false;

            switch (classification)
            {
                case DuelShadowCasterClass.Player:
                case DuelShadowCasterClass.Opponent:
                case DuelShadowCasterClass.Arena:
                    return true;
                case DuelShadowCasterClass.HeroRock:
                    return worldDiameter >= settings.MinimumHeroRockDiameter;
                case DuelShadowCasterClass.ActiveFragment:
                    return worldDiameter >= settings.MinimumActiveFragmentDiameter;
                default:
                    return false;
            }
        }

        public static int Priority(DuelShadowCasterClass classification)
        {
            switch (classification)
            {
                case DuelShadowCasterClass.Player:
                    return 0;
                case DuelShadowCasterClass.Opponent:
                    return 1;
                case DuelShadowCasterClass.Arena:
                    return 2;
                case DuelShadowCasterClass.HeroRock:
                    return 3;
                case DuelShadowCasterClass.ActiveFragment:
                    return 4;
                default:
                    return int.MaxValue;
            }
        }
    }

    public readonly struct DuelShadowRegistrationHandle
    {
        public static readonly DuelShadowRegistrationHandle Invalid =
            new DuelShadowRegistrationHandle(-1, 0);

        public readonly int Slot;
        public readonly uint Revision;

        public bool IsValid => Slot >= 0 && Revision != 0;

        public DuelShadowRegistrationHandle(int slot, uint revision)
        {
            Slot = slot;
            Revision = revision;
        }
    }

    public readonly struct DuelShadowCasterRecord
    {
        public readonly Renderer Renderer;
        public readonly Bounds WorldBounds;
        public readonly uint StableGroupId;
        public readonly uint Generation;
        public readonly DuelShadowCasterClass Classification;
        public readonly int SubmeshCount;

        public DuelShadowCasterRecord(
            Renderer renderer,
            Bounds worldBounds,
            uint stableGroupId,
            uint generation,
            DuelShadowCasterClass classification,
            int submeshCount)
        {
            Renderer = renderer;
            WorldBounds = worldBounds;
            StableGroupId = stableGroupId;
            Generation = generation;
            Classification = classification;
            SubmeshCount = Mathf.Max(1, submeshCount);
        }
    }

    public struct DuelShadowDrawCommand
    {
        public Renderer Renderer;
        public Bounds WorldBounds;
        public uint StableGroupId;
        public uint Generation;
        public DuelShadowCasterClass Classification;
        public int SubmeshCount;
        internal ulong RegistrationOrder;
    }

    /// <summary>
    /// Fixed-capacity presentation registry. A stable group exposes exactly one
    /// committed generation, allowing fracture pieces to register before the
    /// intact/fractured representation is swapped atomically.
    /// </summary>
    public sealed class DuelShadowCasterRegistry
    {
        public const int MaximumCapacity = 256;
        public const int MaximumGenerationGroups = 128;

        private struct Entry
        {
            public bool Used;
            public uint Revision;
            public ulong RegistrationOrder;
            public DuelShadowCasterRecord Record;
        }

        private struct GenerationState
        {
            public bool Used;
            public uint StableGroupId;
            public uint ActiveGeneration;
        }

        private static DuelShadowCasterRegistry s_Shared =
            new DuelShadowCasterRegistry(MaximumCapacity, MaximumGenerationGroups);

        private readonly Entry[] _entries;
        private readonly uint[] _slotRevisions;
        private readonly GenerationState[] _generationStates;
        private ulong _nextRegistrationOrder;
        private int _count;
        private int _capacityRejectCount;
        private int _generationRejectCount;

        public static DuelShadowCasterRegistry Shared => s_Shared;
        public int Capacity => _entries.Length;
        public int Count => _count;
        public int CapacityRejectCount => _capacityRejectCount;
        public int GenerationRejectCount => _generationRejectCount;

        public DuelShadowCasterRegistry(
            int capacity = MaximumCapacity,
            int generationCapacity = MaximumGenerationGroups)
        {
            _entries = new Entry[Mathf.Clamp(capacity, 1, MaximumCapacity)];
            _slotRevisions = new uint[_entries.Length];
            _generationStates = new GenerationState[Mathf.Clamp(
                generationCapacity,
                1,
                MaximumGenerationGroups)];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedRegistry()
        {
            s_Shared = new DuelShadowCasterRegistry(
                MaximumCapacity,
                MaximumGenerationGroups);
        }

        public bool TryRegister(
            in DuelShadowCasterRecord record,
            out DuelShadowRegistrationHandle handle)
        {
            handle = DuelShadowRegistrationHandle.Invalid;
            if (record.StableGroupId == 0u ||
                !DuelShadowMath.IsFinite(record.WorldBounds.center) ||
                !DuelShadowMath.IsFinite(record.WorldBounds.extents))
                return false;

            int generationIndex = FindGeneration(record.StableGroupId);
            bool createdGenerationState = false;
            if (generationIndex < 0)
            {
                generationIndex = FindFreeGeneration();
                if (generationIndex < 0)
                {
                    _generationRejectCount++;
                    return false;
                }

                _generationStates[generationIndex] = new GenerationState
                {
                    Used = true,
                    StableGroupId = record.StableGroupId,
                    ActiveGeneration = record.Generation
                };
                createdGenerationState = true;
            }

            int slot = FindFreeEntry();
            if (slot < 0)
            {
                _capacityRejectCount++;
                if (createdGenerationState)
                    _generationStates[generationIndex] = default;
                return false;
            }

            uint revision = ++_slotRevisions[slot];
            if (revision == 0)
                revision = ++_slotRevisions[slot];
            _entries[slot] = new Entry
            {
                Used = true,
                Revision = revision,
                RegistrationOrder = ++_nextRegistrationOrder,
                Record = record
            };
            _count++;
            handle = new DuelShadowRegistrationHandle(slot, revision);
            return true;
        }

        public bool Unregister(in DuelShadowRegistrationHandle handle)
        {
            if (!IsRegistrationCurrent(handle))
                return false;

            _entries[handle.Slot] = default;
            _count--;
            return true;
        }

        /// <summary>
        /// Releases generation authority only when a group is permanently retired.
        /// Ordinary pool disable/unregister must not call this: retaining the
        /// committed generation prevents an older pooled representation from
        /// reactivating itself during an empty interval.
        /// </summary>
        public bool TryReleaseGroup(uint stableGroupId, uint committedGeneration)
        {
            int generationIndex = FindGeneration(stableGroupId);
            if (generationIndex < 0 ||
                _generationStates[generationIndex].ActiveGeneration != committedGeneration ||
                HasAnyEntry(stableGroupId))
                return false;
            _generationStates[generationIndex] = default;
            return true;
        }

        public bool TryCommitGeneration(uint stableGroupId, uint generation)
        {
            if (stableGroupId == 0u)
            {
                _generationRejectCount++;
                return false;
            }

            int generationIndex = FindGeneration(stableGroupId);
            if (generationIndex < 0 || !HasGeneration(stableGroupId, generation))
            {
                _generationRejectCount++;
                return false;
            }

            GenerationState state = _generationStates[generationIndex];
            state.ActiveGeneration = generation;
            _generationStates[generationIndex] = state;
            return true;
        }

        public bool IsRegistrationCurrent(in DuelShadowRegistrationHandle handle)
        {
            return handle.IsValid &&
                handle.Slot < _entries.Length &&
                _entries[handle.Slot].Used &&
                _entries[handle.Slot].Revision == handle.Revision;
        }

        public bool IsGenerationActive(in DuelShadowRegistrationHandle handle)
        {
            if (!IsRegistrationCurrent(handle))
                return false;
            Entry entry = _entries[handle.Slot];
            return IsActiveGeneration(
                entry.Record.StableGroupId,
                entry.Record.Generation);
        }

        public int CountActiveRegistrations(
            in DuelShadowClassificationSettings settings)
        {
            int activeCount = 0;
            for (int index = 0; index < _entries.Length; index++)
            {
                Entry entry = _entries[index];
                if (!entry.Used ||
                    !IsActiveGeneration(
                        entry.Record.StableGroupId,
                        entry.Record.Generation) ||
                    !DuelShadowCasterPolicy.IsIncluded(
                        entry.Record.Classification,
                        Diameter(entry.Record.WorldBounds),
                        settings))
                    continue;
                activeCount++;
            }

            return activeCount;
        }

        public int CopyActiveDrawCommands(
            DuelShadowDrawCommand[] destination,
            in DuelShadowClassificationSettings settings,
            int maximumCount,
            out Bounds activeBounds,
            out int rejectedCount)
        {
            activeBounds = default;
            rejectedCount = 0;
            if (destination == null || destination.Length == 0 || maximumCount <= 0)
                return 0;

            int limit = Mathf.Min(destination.Length, maximumCount);
            int count = 0;
            int eligibleCount = 0;
            bool hasBounds = false;
            for (int index = 0; index < _entries.Length; index++)
            {
                Entry entry = _entries[index];
                if (!entry.Used ||
                    !IsActiveGeneration(
                        entry.Record.StableGroupId,
                        entry.Record.Generation))
                    continue;

                Renderer renderer = entry.Record.Renderer;
                Bounds bounds = renderer != null ? renderer.bounds : entry.Record.WorldBounds;
                if (!DuelShadowCasterPolicy.IsIncluded(
                        entry.Record.Classification,
                        Diameter(bounds),
                        settings) ||
                    renderer == null ||
                    !DuelShadowCasterPolicy.IsSupportedOpaqueRenderer(renderer) ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    rejectedCount++;
                    continue;
                }

                eligibleCount++;
                DuelShadowDrawCommand command = new DuelShadowDrawCommand
                {
                    Renderer = renderer,
                    WorldBounds = bounds,
                    StableGroupId = entry.Record.StableGroupId,
                    Generation = entry.Record.Generation,
                    Classification = entry.Record.Classification,
                    SubmeshCount = entry.Record.SubmeshCount,
                    RegistrationOrder = entry.RegistrationOrder
                };
                InsertDeterministically(destination, limit, ref count, command);
            }

            rejectedCount += eligibleCount - count;

            for (int index = 0; index < count; index++)
            {
                if (!hasBounds)
                {
                    activeBounds = destination[index].WorldBounds;
                    hasBounds = true;
                }
                else
                {
                    activeBounds.Encapsulate(destination[index].WorldBounds);
                }
            }

            return count;
        }

        private static void InsertDeterministically(
            DuelShadowDrawCommand[] destination,
            int limit,
            ref int count,
            in DuelShadowDrawCommand command)
        {
            int insertionIndex = 0;
            while (insertionIndex < count &&
                   Compare(destination[insertionIndex], command) <= 0)
                insertionIndex++;

            if (count >= limit && insertionIndex >= limit)
                return;

            int lastIndex = Mathf.Min(count, limit - 1);
            for (int index = lastIndex; index > insertionIndex; index--)
                destination[index] = destination[index - 1];
            destination[insertionIndex] = command;
            if (count < limit)
                count++;
        }

        private static int Compare(
            in DuelShadowDrawCommand left,
            in DuelShadowDrawCommand right)
        {
            int priorityCompare = DuelShadowCasterPolicy.Priority(left.Classification)
                .CompareTo(DuelShadowCasterPolicy.Priority(right.Classification));
            if (priorityCompare != 0)
                return priorityCompare;
            int groupCompare = left.StableGroupId.CompareTo(right.StableGroupId);
            if (groupCompare != 0)
                return groupCompare;
            int generationCompare = left.Generation.CompareTo(right.Generation);
            if (generationCompare != 0)
                return generationCompare;
            return left.RegistrationOrder.CompareTo(right.RegistrationOrder);
        }

        private bool IsActiveGeneration(uint stableGroupId, uint generation)
        {
            int index = FindGeneration(stableGroupId);
            return index >= 0 &&
                _generationStates[index].ActiveGeneration == generation;
        }

        private int FindFreeEntry()
        {
            for (int index = 0; index < _entries.Length; index++)
            {
                if (!_entries[index].Used)
                    return index;
            }

            return -1;
        }

        private int FindGeneration(uint stableGroupId)
        {
            for (int index = 0; index < _generationStates.Length; index++)
            {
                if (_generationStates[index].Used &&
                    _generationStates[index].StableGroupId == stableGroupId)
                    return index;
            }

            return -1;
        }

        private int FindFreeGeneration()
        {
            for (int index = 0; index < _generationStates.Length; index++)
            {
                if (!_generationStates[index].Used)
                    return index;
            }

            return -1;
        }

        private bool HasAnyEntry(uint stableGroupId)
        {
            for (int index = 0; index < _entries.Length; index++)
            {
                if (_entries[index].Used &&
                    _entries[index].Record.StableGroupId == stableGroupId)
                    return true;
            }

            return false;
        }

        private bool HasGeneration(uint stableGroupId, uint generation)
        {
            for (int index = 0; index < _entries.Length; index++)
            {
                if (_entries[index].Used &&
                    _entries[index].Record.StableGroupId == stableGroupId &&
                    _entries[index].Record.Generation == generation)
                    return true;
            }

            return false;
        }

        private static float Diameter(Bounds bounds)
        {
            Vector3 size = bounds.size;
            return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }
    }
}
