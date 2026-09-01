using Elemental.Runtime.Physics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public enum EarthArenaFragmentShadowRejection : byte
    {
        None = 0,
        InactiveStructure = 1,
        NotReleased = 2,
        InvalidIdentity = 3,
        InvalidDiameter = 4,
        TinyDebris = 5
    }

    /// <summary>Pure admission contract for released Broken Crown pieces.</summary>
    public static class EarthArenaLargeFragmentCapsuleShadowPolicy
    {
        public const int MaximumTrackedStructures = 8;
        public const int MaximumActiveFragments = 4;
        public const float MinimumWorldDiameter = 0.75f;
        public const uint StableCohortGroupId = 0xEACF0001u;

        public static bool TryAdmit(
            bool structureActive,
            bool released,
            uint stablePieceId,
            float worldDiameter,
            out EarthArenaFragmentShadowRejection rejection)
        {
            if (!structureActive)
                rejection = EarthArenaFragmentShadowRejection.InactiveStructure;
            else if (!released)
                rejection = EarthArenaFragmentShadowRejection.NotReleased;
            else if (stablePieceId == 0u)
                rejection = EarthArenaFragmentShadowRejection.InvalidIdentity;
            else if (!float.IsFinite(worldDiameter) || worldDiameter <= 0f)
                rejection = EarthArenaFragmentShadowRejection.InvalidDiameter;
            else if (worldDiameter < MinimumWorldDiameter)
                rejection = EarthArenaFragmentShadowRejection.TinyDebris;
            else
            {
                rejection = EarthArenaFragmentShadowRejection.None;
                return true;
            }
            return false;
        }

        internal static bool ComesBefore(
            float leftDiameter,
            uint leftStableId,
            float rightDiameter,
            uint rightStableId)
        {
            int diameter = rightDiameter.CompareTo(leftDiameter);
            return diameter < 0 || (diameter == 0 && leftStableId < rightStableId);
        }
    }

    public readonly struct EarthArenaLargeFragmentCapsuleShadowDiagnostics
    {
        public EarthArenaLargeFragmentCapsuleShadowDiagnostics(
            int trackedStructures,
            int eligibleFragments,
            int activeFragments,
            int tinyRejected,
            int budgetRejected,
            uint generation,
            bool committed)
        {
            TrackedStructures = trackedStructures;
            EligibleFragments = eligibleFragments;
            ActiveFragments = activeFragments;
            TinyRejected = tinyRejected;
            BudgetRejected = budgetRejected;
            Generation = generation;
            Committed = committed;
        }

        public int TrackedStructures { get; }
        public int EligibleFragments { get; }
        public int ActiveFragments { get; }
        public int TinyRejected { get; }
        public int BudgetRejected { get; }
        public uint Generation { get; }
        public bool Committed { get; }
        public bool RequiresRealtimeArenaShadows => false;
    }

    [DisallowMultipleComponent]
    internal sealed class EarthArenaLargeFragmentCapsuleShadowProducer : MonoBehaviour
    {
        private CapsuleShadowCaster _caster;
        private CapsuleShadowCasterBinder _binder;

        public bool IsActiveGeneration => _caster != null && _caster.IsActiveGeneration;

        public bool TryStage(
            EarthArenaPiece piece,
            Renderer renderer,
            uint groupId,
            uint generation)
        {
            ReleaseHandle();
            if (piece == null || renderer == null || !piece.IsEarthTargetValid)
                return false;
            _caster = GetComponent<CapsuleShadowCaster>();
            if (_caster == null)
                _caster = gameObject.AddComponent<CapsuleShadowCaster>();
            _binder = GetComponent<CapsuleShadowCasterBinder>();
            if (_binder == null)
                _binder = gameObject.AddComponent<CapsuleShadowCasterBinder>();

            Bounds bounds = renderer.localBounds;
            float radius = Mathf.Max(
                bounds.extents.x,
                Mathf.Max(bounds.extents.y, bounds.extents.z));
            if (!float.IsFinite(radius) || radius <= 0f ||
                !_caster.ConfigureProxies(new[]
                {
                    new CapsuleShadowProxyBinding(
                        renderer.transform,
                        renderer.transform,
                        bounds.center,
                        bounds.center,
                        radius,
                        Mathf.Max(0.04f, radius * 0.12f))
                }))
                return false;
            if (!HeroRockCapsuleShadowIdentity.TryCreate(
                    CapsuleShadowProducerKind.LargeActiveFracture,
                    groupId,
                    generation,
                    out HeroRockCapsuleShadowIdentity identity))
                return false;
            CapsuleContactShadowRuntimeSettings settings = CreateAdmissionSettings();
            if (!HeroRockCapsuleShadowProducerPolicy.TryAdmit(
                    identity,
                    _caster.EstimateWorldDiameter(),
                    settings,
                    out _,
                    out _))
                return false;
            return _binder.TryAcquire(
                _caster,
                CapsuleShadowProducerKind.LargeActiveFracture,
                groupId,
                generation);
        }

        public void ReleaseHandle()
        {
            if (_binder != null)
                _binder.ReleaseAcquisition(_caster);
            else if (_caster != null)
                _caster.Unbind();
        }

        private void OnDisable() => ReleaseHandle();

        private static CapsuleContactShadowRuntimeSettings CreateAdmissionSettings()
        {
            return new CapsuleContactShadowRuntimeSettings(
                new CapsuleContactShadowQuality(32),
                CapsuleShadowBuffer.MaximumCasterCount,
                0.58f,
                1.25f,
                0.025f,
                0.02f,
                0.4f,
                EarthArenaLargeFragmentCapsuleShadowPolicy.MinimumWorldDiameter,
                CapsuleContactShadowDebugView.None);
        }
    }

    /// <summary>
    /// Transient owner for the four largest released Broken Crown pieces. One
    /// cohort generation prevents a repair from leaving stale fragment handles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthArenaLargeFragmentCapsuleShadowPresenter : MonoBehaviour
    {
        private struct Candidate
        {
            public EarthArenaPiece Piece;
            public Renderer Renderer;
            public uint StableId;
            public float Diameter;
        }

        private static readonly ProfilerMarker RebuildMarker =
            new ProfilerMarker("Elemental.Rendering.ArenaFragmentCapsuleRebuild");
        private static EarthArenaLargeFragmentCapsuleShadowPresenter s_Instance;
        private static EarthArenaLargeFragmentCapsuleShadowDiagnostics s_Current;
        private readonly EarthArenaStructure[] _structures =
            new EarthArenaStructure[EarthArenaLargeFragmentCapsuleShadowPolicy.MaximumTrackedStructures];
        private readonly Candidate[] _candidates =
            new Candidate[EarthArenaLargeFragmentCapsuleShadowPolicy.MaximumActiveFragments];
        private readonly EarthArenaLargeFragmentCapsuleShadowProducer[] _active =
            new EarthArenaLargeFragmentCapsuleShadowProducer[EarthArenaLargeFragmentCapsuleShadowPolicy.MaximumActiveFragments];
        private readonly uint[] _activeIds =
            new uint[EarthArenaLargeFragmentCapsuleShadowPolicy.MaximumActiveFragments];
        private int _structureCount;
        private int _activeCount;
        private uint _generation;
        private bool _hasCommittedGeneration;

        public static EarthArenaLargeFragmentCapsuleShadowDiagnostics Current => s_Current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Instance = null;
            s_Current = default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (s_Instance != null)
                return;
            var owner = new GameObject("Earth Arena Fragment Capsule Shadow Presenter")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(owner);
            s_Instance = owner.AddComponent<EarthArenaLargeFragmentCapsuleShadowPresenter>();
        }

        private void OnEnable() =>
            EarthArenaStructure.PresentationStateChanged += HandleStructureChanged;

        private void OnDisable()
        {
            EarthArenaStructure.PresentationStateChanged -= HandleStructureChanged;
            ReleaseActive();
        }

        private void HandleStructureChanged(EarthArenaStructure structure)
        {
            if (structure == null)
                return;
            int trackedIndex = FindStructure(structure);
            bool shouldTrack = structure.isActiveAndEnabled &&
                structure.gameObject.activeInHierarchy;
            if (!shouldTrack)
            {
                if (trackedIndex >= 0)
                    RemoveStructure(trackedIndex);
                Rebuild();
                return;
            }
            if (trackedIndex < 0)
            {
                if (_structureCount >= _structures.Length)
                {
                    Debug.LogError(
                        "Broken Crown capsule-shadow structure budget was exceeded.",
                        structure);
                    return;
                }
                _structures[_structureCount++] = structure;
            }
            Rebuild();
        }

        private void Rebuild()
        {
            using (RebuildMarker.Auto())
            {
                CompactStructures();
                int candidateCount = 0;
                int eligibleCount = 0;
                int tinyRejected = 0;
                for (int structureIndex = 0; structureIndex < _structureCount; structureIndex++)
                {
                    EarthArenaStructure structure = _structures[structureIndex];
                    for (int pieceIndex = 0; pieceIndex < structure.PieceCount; pieceIndex++)
                    {
                        if (!structure.TryGetPiece(pieceIndex, out EarthArenaPiece piece) ||
                            piece == null)
                            continue;
                        Renderer renderer = piece.GetComponent<Renderer>();
                        float diameter = renderer != null
                            ? MaxComponent(renderer.bounds.size)
                            : 0f;
                        if (!EarthArenaLargeFragmentCapsuleShadowPolicy.TryAdmit(
                                true,
                                structure.IsPieceReleased(pieceIndex),
                                piece.StableEarthId,
                                diameter,
                                out EarthArenaFragmentShadowRejection rejection))
                        {
                            if (rejection == EarthArenaFragmentShadowRejection.TinyDebris)
                                tinyRejected++;
                            continue;
                        }
                        eligibleCount++;
                        InsertCandidate(new Candidate
                        {
                            Piece = piece,
                            Renderer = renderer,
                            StableId = piece.StableEarthId,
                            Diameter = diameter
                        }, ref candidateCount);
                    }
                }
                if (MatchesActiveSet(candidateCount))
                {
                    Publish(eligibleCount, tinyRejected, candidateCount, true);
                    return;
                }
                ReleaseActive();
                if (candidateCount == 0)
                {
                    Publish(eligibleCount, tinyRejected, 0, true);
                    return;
                }

                _generation = NextGeneration(_generation);
                int stagedCount = 0;
                for (int index = 0; index < candidateCount; index++)
                {
                    Candidate candidate = _candidates[index];
                    EarthArenaLargeFragmentCapsuleShadowProducer producer =
                        candidate.Piece.GetComponent<EarthArenaLargeFragmentCapsuleShadowProducer>();
                    if (producer == null)
                        producer = candidate.Piece.gameObject.AddComponent<EarthArenaLargeFragmentCapsuleShadowProducer>();
                    if (!producer.TryStage(
                            candidate.Piece,
                            candidate.Renderer,
                            EarthArenaLargeFragmentCapsuleShadowPolicy.StableCohortGroupId,
                            _generation))
                    {
                        Rollback(stagedCount);
                        Debug.LogError(
                            "Broken Crown large-fragment capsule-shadow staging failed.",
                            candidate.Piece);
                        Publish(eligibleCount, tinyRejected, 0, false);
                        return;
                    }
                    _active[stagedCount] = producer;
                    _activeIds[stagedCount] = candidate.StableId;
                    stagedCount++;
                }
                if (!CapsuleShadowCaster.CommitGeneration(
                        EarthArenaLargeFragmentCapsuleShadowPolicy.StableCohortGroupId,
                        _generation))
                {
                    Rollback(stagedCount);
                    Debug.LogError("Broken Crown capsule-shadow generation commit failed.", this);
                    Publish(eligibleCount, tinyRejected, 0, false);
                    return;
                }
                _activeCount = stagedCount;
                _hasCommittedGeneration = true;
                Publish(eligibleCount, tinyRejected, _activeCount, true);
            }
        }

        private void InsertCandidate(in Candidate candidate, ref int count)
        {
            int insertion = Mathf.Min(count, _candidates.Length - 1);
            if (count == _candidates.Length &&
                !EarthArenaLargeFragmentCapsuleShadowPolicy.ComesBefore(
                    candidate.Diameter,
                    candidate.StableId,
                    _candidates[insertion].Diameter,
                    _candidates[insertion].StableId))
                return;
            if (count < _candidates.Length)
                count++;
            while (insertion > 0 &&
                   EarthArenaLargeFragmentCapsuleShadowPolicy.ComesBefore(
                       candidate.Diameter,
                       candidate.StableId,
                       _candidates[insertion - 1].Diameter,
                       _candidates[insertion - 1].StableId))
            {
                _candidates[insertion] = _candidates[insertion - 1];
                insertion--;
            }
            _candidates[insertion] = candidate;
        }

        private bool MatchesActiveSet(int candidateCount)
        {
            if (!_hasCommittedGeneration || candidateCount != _activeCount)
                return false;
            for (int index = 0; index < candidateCount; index++)
                if (_active[index] == null ||
                    _activeIds[index] != _candidates[index].StableId ||
                    !_active[index].IsActiveGeneration)
                    return false;
            return true;
        }

        private void ReleaseActive()
        {
            for (int index = 0; index < _activeCount; index++)
            {
                if (_active[index] != null)
                    _active[index].ReleaseHandle();
                _active[index] = null;
                _activeIds[index] = 0u;
            }
            _activeCount = 0;
            if (_hasCommittedGeneration)
            {
                CapsuleShadowCaster.ReleaseGroup(
                    EarthArenaLargeFragmentCapsuleShadowPolicy.StableCohortGroupId,
                    _generation);
                _hasCommittedGeneration = false;
            }
        }

        private void Rollback(int stagedCount)
        {
            _activeCount = stagedCount;
            ReleaseActive();
        }

        private int FindStructure(EarthArenaStructure structure)
        {
            for (int index = 0; index < _structureCount; index++)
                if (_structures[index] == structure)
                    return index;
            return -1;
        }

        private void RemoveStructure(int index)
        {
            _structureCount--;
            _structures[index] = _structures[_structureCount];
            _structures[_structureCount] = null;
        }

        private void CompactStructures()
        {
            for (int index = _structureCount - 1; index >= 0; index--)
                if (_structures[index] == null)
                    RemoveStructure(index);
        }

        private void Publish(int eligible, int tinyRejected, int active, bool committed)
        {
            s_Current = new EarthArenaLargeFragmentCapsuleShadowDiagnostics(
                _structureCount,
                eligible,
                active,
                tinyRejected,
                Mathf.Max(0, eligible - EarthArenaLargeFragmentCapsuleShadowPolicy.MaximumActiveFragments),
                _generation,
                committed);
        }

        private static float MaxComponent(Vector3 value) =>
            Mathf.Max(value.x, Mathf.Max(value.y, value.z));

        private static uint NextGeneration(uint generation) =>
            generation == uint.MaxValue ? 1u : generation + 1u;
    }
}
