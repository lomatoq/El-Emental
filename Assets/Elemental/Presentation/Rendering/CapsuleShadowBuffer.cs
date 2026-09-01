using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public enum CapsuleShadowCasterClass
    {
        Character = 0,
        HeroRock = 1,
        ActiveFragment = 2,
        TinyDebris = 3,
        Vfx = 4,
        Other = 5
    }

    public static class CapsuleShadowCasterPolicy
    {
        public static bool IsAdmittedClassification(
            CapsuleShadowCasterClass classification)
        {
            return classification == CapsuleShadowCasterClass.Character ||
                classification == CapsuleShadowCasterClass.HeroRock ||
                classification == CapsuleShadowCasterClass.ActiveFragment;
        }

        public static bool IsIncluded(
            CapsuleShadowCasterClass classification,
            float worldDiameter,
            in CapsuleContactShadowRuntimeSettings settings)
        {
            if (!DuelShadowMath.IsFinite(worldDiameter) || worldDiameter <= 0f)
                return false;
            switch (classification)
            {
                case CapsuleShadowCasterClass.Character:
                    return true;
                case CapsuleShadowCasterClass.HeroRock:
                    return worldDiameter >= settings.MinimumHeroRockDiameter;
                case CapsuleShadowCasterClass.ActiveFragment:
                    return worldDiameter >= settings.MinimumActiveFragmentDiameter;
                default:
                    return false;
            }
        }

        public static int Priority(CapsuleShadowCasterClass classification)
        {
            switch (classification)
            {
                case CapsuleShadowCasterClass.Character:
                    return 0;
                case CapsuleShadowCasterClass.HeroRock:
                    return 2;
                case CapsuleShadowCasterClass.ActiveFragment:
                    return 1;
                default:
                    return int.MaxValue;
            }
        }
    }

    public readonly struct CapsuleShadowRegistrationHandle
    {
        public static readonly CapsuleShadowRegistrationHandle Invalid =
            new CapsuleShadowRegistrationHandle(-1, 0u);

        public CapsuleShadowRegistrationHandle(int slot, uint revision)
        {
            Slot = slot;
            Revision = revision;
        }

        public int Slot { get; }
        public uint Revision { get; }
        public bool IsValid => Slot >= 0 && Revision != 0u;
    }

    internal readonly struct CapsuleShadowCasterRecord
    {
        internal CapsuleShadowCasterRecord(
            CapsuleShadowCaster caster,
            uint stableGroupId,
            uint generation,
            CapsuleShadowCasterClass classification)
        {
            Caster = caster;
            StableGroupId = stableGroupId;
            Generation = generation;
            Classification = classification;
        }

        public CapsuleShadowCaster Caster { get; }
        public uint StableGroupId { get; }
        public uint Generation { get; }
        public CapsuleShadowCasterClass Classification { get; }
    }

    /// <summary>
    /// Fixed-capacity presentation buffer for analytic capsule and sphere proxies.
    /// Generation state survives empty pool intervals, so a stale acquisition can
    /// register for diagnostics but cannot become active without an explicit commit.
    /// </summary>
    public sealed class CapsuleShadowBuffer
    {
        public const int MaximumCasterCount = 24;
        public const int MaximumProxyCount = 32;
        public const int MaximumGenerationGroups = 16;

        private struct Entry
        {
            public bool Used;
            public uint Revision;
            public ulong RegistrationOrder;
            public CapsuleShadowCasterRecord Record;
        }

        private struct GenerationState
        {
            public bool Used;
            public bool HasActiveGeneration;
            public bool HasCommittedGeneration;
            public uint StableGroupId;
            public uint ActiveGeneration;
        }

        private struct Candidate
        {
            public Entry Entry;
        }

        private static CapsuleShadowBuffer s_Shared = new CapsuleShadowBuffer();

        private readonly Entry[] _entries;
        private readonly uint[] _slotRevisions;
        private readonly GenerationState[] _generationStates;
        private readonly Candidate[] _candidateScratch;
        private ulong _nextRegistrationOrder;
        private int _count;
        private int _capacityRejectCount;
        private int _generationRejectCount;

        public CapsuleShadowBuffer(
            int capacity = MaximumCasterCount,
            int generationCapacity = MaximumGenerationGroups)
        {
            _entries = new Entry[Mathf.Clamp(capacity, 1, MaximumCasterCount)];
            _slotRevisions = new uint[_entries.Length];
            _generationStates = new GenerationState[Mathf.Clamp(
                generationCapacity,
                1,
                MaximumGenerationGroups)];
            _candidateScratch = new Candidate[_entries.Length];
        }

        public static CapsuleShadowBuffer Shared => s_Shared;
        public int Count => _count;
        public int Capacity => _entries.Length;
        public int CapacityRejectCount => _capacityRejectCount;
        public int GenerationRejectCount => _generationRejectCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedBuffer()
        {
            s_Shared = new CapsuleShadowBuffer();
        }

        internal bool TryRegister(
            in CapsuleShadowCasterRecord record,
            out CapsuleShadowRegistrationHandle handle)
        {
            handle = CapsuleShadowRegistrationHandle.Invalid;
            if (record.Caster == null || record.StableGroupId == 0u ||
                !CapsuleShadowCasterPolicy.IsAdmittedClassification(
                    record.Classification))
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
                    HasActiveGeneration = false,
                    HasCommittedGeneration = false,
                    ActiveGeneration = 0u
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
            if (revision == 0u)
                revision = ++_slotRevisions[slot];
            _entries[slot] = new Entry
            {
                Used = true,
                Revision = revision,
                RegistrationOrder = ++_nextRegistrationOrder,
                Record = record
            };
            _count++;
            handle = new CapsuleShadowRegistrationHandle(slot, revision);
            return true;
        }

        public bool Unregister(in CapsuleShadowRegistrationHandle handle)
        {
            if (!IsRegistrationCurrent(handle))
                return false;
            _entries[handle.Slot] = default;
            _count--;
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
            if (state.HasCommittedGeneration &&
                !(state.HasActiveGeneration && state.ActiveGeneration == generation) &&
                !IsSerialNewer(generation, state.ActiveGeneration))
            {
                _generationRejectCount++;
                return false;
            }
            state.ActiveGeneration = generation;
            state.HasActiveGeneration = true;
            state.HasCommittedGeneration = true;
            _generationStates[generationIndex] = state;
            return true;
        }

        public bool TryReleaseGroup(uint stableGroupId, uint committedGeneration)
        {
            int generationIndex = FindGeneration(stableGroupId);
            if (generationIndex < 0 ||
                !_generationStates[generationIndex].HasActiveGeneration ||
                _generationStates[generationIndex].ActiveGeneration != committedGeneration ||
                HasAnyEntry(stableGroupId))
                return false;
            GenerationState state = _generationStates[generationIndex];
            state.HasActiveGeneration = false;
            _generationStates[generationIndex] = state;
            return true;
        }

        public bool IsRegistrationCurrent(in CapsuleShadowRegistrationHandle handle)
        {
            return handle.IsValid &&
                handle.Slot < _entries.Length &&
                _entries[handle.Slot].Used &&
                _entries[handle.Slot].Revision == handle.Revision;
        }

        public bool IsGenerationActive(in CapsuleShadowRegistrationHandle handle)
        {
            if (!IsRegistrationCurrent(handle))
                return false;
            CapsuleShadowCasterRecord record = _entries[handle.Slot].Record;
            return IsActiveGeneration(record.StableGroupId, record.Generation);
        }

        public int CopyActiveProxies(
            Vector4[] startRadius,
            Vector4[] endSoftness,
            in CapsuleContactShadowRuntimeSettings settings,
            out int activeCasterCount,
            out int rejectedCasterCount,
            out int rejectedProxyCount)
        {
            activeCasterCount = 0;
            rejectedCasterCount = 0;
            rejectedProxyCount = 0;
            if (startRadius == null || endSoftness == null)
                return 0;
            int proxyLimit = Mathf.Min(
                Mathf.Min(startRadius.Length, endSoftness.Length),
                settings.Quality.MaximumCapsuleCount);
            if (proxyLimit <= 0)
                return 0;

            int candidateCount = 0;
            for (int index = 0; index < _entries.Length; index++)
            {
                Entry entry = _entries[index];
                if (!entry.Used ||
                    !IsActiveGeneration(
                        entry.Record.StableGroupId,
                        entry.Record.Generation))
                    continue;
                CapsuleShadowCaster caster = entry.Record.Caster;
                if (caster == null ||
                    !caster.isActiveAndEnabled ||
                    !caster.gameObject.activeInHierarchy ||
                    !CapsuleShadowCasterPolicy.IsIncluded(
                        entry.Record.Classification,
                        caster.EstimateWorldDiameter(),
                        settings))
                {
                    rejectedCasterCount++;
                    continue;
                }
                InsertCandidate(candidateCount++, entry);
            }

            int casterLimit = Mathf.Min(candidateCount, settings.MaximumCasterCount);
            rejectedCasterCount += candidateCount - casterLimit;
            int proxyCount = 0;
            for (int candidateIndex = 0; candidateIndex < casterLimit; candidateIndex++)
            {
                CapsuleShadowCaster caster =
                    _candidateScratch[candidateIndex].Entry.Record.Caster;
                bool wroteCaster = false;
                for (int proxyIndex = 0; proxyIndex < caster.ProxyCount; proxyIndex++)
                {
                    if (!caster.TryGetProxy(proxyIndex, out CapsuleShadowProxy proxy))
                    {
                        rejectedProxyCount++;
                        continue;
                    }
                    if (proxyCount >= proxyLimit)
                    {
                        rejectedProxyCount += caster.ProxyCount - proxyIndex;
                        break;
                    }
                    startRadius[proxyCount] = new Vector4(
                        proxy.StartWorld.x,
                        proxy.StartWorld.y,
                        proxy.StartWorld.z,
                        proxy.Radius);
                    endSoftness[proxyCount] = new Vector4(
                        proxy.EndWorld.x,
                        proxy.EndWorld.y,
                        proxy.EndWorld.z,
                        proxy.Softness);
                    proxyCount++;
                    wroteCaster = true;
                }
                if (wroteCaster)
                    activeCasterCount++;
            }
            return proxyCount;
        }

        private void InsertCandidate(int countBeforeInsert, in Entry entry)
        {
            int insertionIndex = countBeforeInsert;
            while (insertionIndex > 0 &&
                   Compare(entry, _candidateScratch[insertionIndex - 1].Entry) < 0)
            {
                _candidateScratch[insertionIndex] =
                    _candidateScratch[insertionIndex - 1];
                insertionIndex--;
            }
            _candidateScratch[insertionIndex] = new Candidate { Entry = entry };
        }

        private static int Compare(in Entry left, in Entry right)
        {
            int priority = CapsuleShadowCasterPolicy.Priority(left.Record.Classification)
                .CompareTo(CapsuleShadowCasterPolicy.Priority(right.Record.Classification));
            if (priority != 0)
                return priority;
            int group = left.Record.StableGroupId.CompareTo(right.Record.StableGroupId);
            if (group != 0)
                return group;
            int generation = left.Record.Generation.CompareTo(right.Record.Generation);
            if (generation != 0)
                return generation;
            return left.RegistrationOrder.CompareTo(right.RegistrationOrder);
        }

        private bool IsActiveGeneration(uint stableGroupId, uint generation)
        {
            int index = FindGeneration(stableGroupId);
            return index >= 0 &&
                _generationStates[index].HasActiveGeneration &&
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

        private static bool IsSerialNewer(uint candidate, uint baseline)
        {
            uint distance = unchecked(candidate - baseline);
            return distance != 0u && distance < 0x80000000u;
        }
    }
}
