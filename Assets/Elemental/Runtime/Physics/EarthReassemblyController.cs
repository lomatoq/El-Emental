using System;
using Elemental.Simulation.Structures;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthReassemblyController : MonoBehaviour
    {
        private static readonly ProfilerMarker RepairAlignMarker =
            new ProfilerMarker("Elemental.Earth.Repair.Align");
        private static readonly ProfilerMarker RepairSelectMarker =
            new ProfilerMarker("Elemental.Earth.Repair.Select");
        private static readonly ProfilerMarker RepairOrderMarker =
            new ProfilerMarker("Elemental.Earth.Repair.Order");
        private static readonly ProfilerMarker RepairSolveMarker =
            new ProfilerMarker("Elemental.Earth.Repair.Solve");
        private static readonly ProfilerMarker RepairWeldMarker =
            new ProfilerMarker("Elemental.Earth.Repair.Weld");

        private EarthStructureRuntime _structure;
        private EarthWall _wall;
        private EarthRepairProfile _profile;
        private EarthPieceDefinition[] _pieceDefinitions = Array.Empty<EarthPieceDefinition>();
        private EarthPieceState[] _pieceStateSnapshot = Array.Empty<EarthPieceState>();
        private EarthBondDefinition[] _bondDefinitions = Array.Empty<EarthBondDefinition>();
        private EarthBondState[] _bondStateSnapshot = Array.Empty<EarthBondState>();
        private bool[] _available = Array.Empty<bool>();
        private bool[] _visited = Array.Empty<bool>();
        private bool[] _welded = Array.Empty<bool>();
        private int[] _order = Array.Empty<int>();
        private int[] _graphDepth = Array.Empty<int>();
        private float[] _phaseElapsed = Array.Empty<float>();
        private EarthRepairSettleState[] _settle = Array.Empty<EarthRepairSettleState>();
        private EarthRepairProgressState[] _progress = Array.Empty<EarthRepairProgressState>();
        private Collider[] _ignoredPieceColliders = Array.Empty<Collider>();
        private Collider[] _ignoredWorldColliders = Array.Empty<Collider>();
        private int[] _ignoredPieceIndices = Array.Empty<int>();
        private readonly Collider[] _seatingOverlap = new Collider[16];
        private int _ignoredCollisionCount;
        private EarthReassemblyTuning _tuning;
        private EarthRepairOrderResult _orderResult;
        private uint _generation;
        private uint _tick;
        private int _nextOrderSlot;
        private int _weldedPieceCount;
        private byte _congestionExpansion;

        public event Action<EarthRepairStartedEvent> RepairStarted;
        public event Action<EarthPieceCapturedEvent> PieceCaptured;
        public event Action<EarthPieceStagedEvent> PieceStaged;
        public event Action<EarthBondReformingEvent> BondReforming;
        public event Action<EarthBondRepairedEvent> BondRepaired;
        public event Action<EarthRepairInterruptedEvent> RepairInterrupted;
        public event Action<EarthStructureRebuiltEvent> StructureRebuilt;
        public event Action<EarthRepairRejectedEvent> RepairRejected;

        public bool IsRepairing { get; private set; }
        public bool LastRepairWasPartial { get; private set; }
        public int SelectedPieceCount => _orderResult.OrderedPieceCount;
        public int WeldedPieceCount => _weldedPieceCount;
        public int CurrentPieceIndex => _nextOrderSlot < _orderResult.OrderedPieceCount
            ? _order[_nextOrderSlot]
            : -1;
        public EarthPiecePhase CurrentPiecePhase => CurrentPieceIndex >= 0
            ? _structure.GetPieceState(CurrentPieceIndex).Phase
            : EarthPiecePhase.Welded;
        public float CurrentPiecePositionError { get; private set; }
        public float CurrentPieceSpeed { get; private set; }
        public float CurrentPieceAngleErrorDegrees { get; private set; }
        public float CurrentPieceAngularSpeed { get; private set; }
        public byte CurrentPieceRetryCount => CurrentPieceIndex >= 0
            ? _progress[CurrentPieceIndex].RetryCount
            : (byte)0;
        public float Progress01 => SelectedPieceCount > 0
            ? Mathf.Clamp01(_weldedPieceCount / (float)SelectedPieceCount)
            : 0f;

        public void Configure(
            EarthStructureRuntime structure,
            EarthWall wall,
            EarthRepairProfile profile)
        {
            _structure = structure;
            _wall = wall;
            _profile = profile;
            int pieceCount = structure != null ? structure.PieceCount : 0;
            int bondCount = structure != null ? structure.BondCount : 0;
            _pieceDefinitions = new EarthPieceDefinition[pieceCount];
            _pieceStateSnapshot = new EarthPieceState[pieceCount];
            _bondDefinitions = new EarthBondDefinition[bondCount];
            _bondStateSnapshot = new EarthBondState[bondCount];
            _available = new bool[pieceCount];
            _visited = new bool[pieceCount];
            _welded = new bool[pieceCount];
            _order = new int[pieceCount];
            _graphDepth = new int[pieceCount];
            _phaseElapsed = new float[pieceCount];
            _settle = new EarthRepairSettleState[pieceCount];
            _progress = new EarthRepairProgressState[pieceCount];
            int ignoreCapacity = pieceCount * 4;
            _ignoredPieceColliders = new Collider[ignoreCapacity];
            _ignoredWorldColliders = new Collider[ignoreCapacity];
            _ignoredPieceIndices = new int[ignoreCapacity];
            _ignoredCollisionCount = 0;
            IsRepairing = false;
        }

        public bool TryBeginRepair(uint tick)
        {
            if (_structure == null || _wall == null || !_structure.IsConfigured || !_wall.IsCollapsing)
                return Reject(tick, EarthRepairRejectReason.StructureNotFractured);
            if (IsRepairing) return true;
            if (!_structure.CopyRepairDataNonAlloc(
                    _pieceDefinitions, _pieceStateSnapshot,
                    _bondDefinitions, _bondStateSnapshot))
            {
                return Reject(tick, EarthRepairRejectReason.InvalidGraph);
            }

            _tuning = _profile != null ? _profile.ToTuning() : DefaultTuning();
            EarthRepairAnchorMode anchorMode = _profile != null
                ? _profile.AnchorMode
                : EarthRepairAnchorMode.OriginalStructureFrame;
            float massLimit = _profile != null ? _profile.MaximumSelectedMass : 30000f;
            float selectedMass = 0f;
            int candidateCount = 0;
            bool conflictingOwner = false;
            EarthStructureId structureId = _structure.State.Id;
            _generation = _structure.Generation;
            using (RepairSelectMarker.Auto())
            {
                for (int index = 0; index < _pieceDefinitions.Length; index++)
                {
                    EarthPieceRuntime piece = _structure.GetPieceRuntime(index);
                    bool conflict = piece != null && piece.HasMagicOwner &&
                                    piece.MagicOwner != EarthMagicGripKind.Repair;
                    conflictingOwner |= conflict;
                    bool selectable = piece != null && piece.gameObject.activeSelf &&
                                      piece.Generation == _generation &&
                                      EarthRepairOrdering.IsSelectable(
                                          structureId,
                                          piece.StructureId,
                                          in _pieceDefinitions[index],
                                          in _pieceStateSnapshot[index],
                                          conflict,
                                          selectedMass,
                                          massLimit);
                    _available[index] = selectable;
                    _welded[index] = selectable &&
                                     _pieceStateSnapshot[index].Phase == EarthPiecePhase.Welded;
                    _phaseElapsed[index] = 0f;
                    _settle[index] = default;
                    _progress[index] = new EarthRepairProgressState { BestError = float.MaxValue };
                    if (!selectable) continue;
                    selectedMass += Mathf.Max(0f, _pieceDefinitions[index].Mass);
                    candidateCount++;
                }
            }
            if (candidateCount == 0)
            {
                return Reject(tick, conflictingOwner
                    ? EarthRepairRejectReason.ConflictingOwner
                    : EarthRepairRejectReason.NoRepairablePieces);
            }

            using (RepairOrderMarker.Auto())
            {
                _orderResult = EarthRepairOrdering.Build(
                    _pieceDefinitions,
                    _pieceStateSnapshot,
                    _pieceDefinitions.Length,
                    _bondDefinitions,
                    _bondDefinitions.Length,
                    _available,
                    anchorMode,
                    _order,
                    _graphDepth,
                    _visited);
            }
            if (!_orderResult.IsSuccess)
                return Reject(tick, EarthRepairRejectReason.InvalidGraph);
            if (!_structure.BeginRepair(tick))
                return Reject(tick, EarthRepairRejectReason.StructureNotFractured);

            _tick = tick;
            _nextOrderSlot = 0;
            _weldedPieceCount = 0;
            _congestionExpansion = 0;
            LastRepairWasPartial = false;
            IsRepairing = true;
            for (int orderIndex = 0; orderIndex < _orderResult.OrderedPieceCount; orderIndex++)
            {
                int pieceIndex = _order[orderIndex];
                if (_welded[pieceIndex])
                {
                    _weldedPieceCount++;
                    continue;
                }
                EarthPieceRuntime piece = _structure.GetPieceRuntime(pieceIndex);
                if (piece == null || !piece.TryAcquireForRepair())
                {
                    Interrupt(EarthRepairInterruptReason.TargetInvalidated, tick);
                    return false;
                }
                _structure.SetPiecePhase(pieceIndex, EarthPiecePhase.Captured, tick);
                PieceCaptured?.Invoke(new EarthPieceCapturedEvent(
                    tick, structureId, _pieceDefinitions[pieceIndex].Id, orderIndex));
            }
            AdvanceOrderCursor();
            RepairStarted?.Invoke(new EarthRepairStartedEvent(
                tick, structureId, _orderResult.OrderedPieceCount, _orderResult.SelectedMass));
            return true;
        }

        public void Interrupt(
            EarthRepairInterruptReason reason = EarthRepairInterruptReason.ExplicitCancel,
            uint tick = 0u)
        {
            if (!IsRepairing) return;
            if (tick == 0u) tick = _tick;
            IsRepairing = false;
            ClearSeatingCollisionIgnores(false);
            for (int index = 0; index < _available.Length; index++)
            {
                if (!_available[index] || _welded[index]) continue;
                EarthPieceRuntime piece = _structure.GetPieceRuntime(index);
                piece?.ReleaseFromRepair();
                _structure.SetPiecePhase(index, EarthPiecePhase.Dynamic, tick);
            }
            _structure.FinishPartialRepair(tick);
            RepairInterrupted?.Invoke(new EarthRepairInterruptedEvent(
                tick, _structure.State.Id, reason, _weldedPieceCount));
        }

        public void TickRepair(float deltaTime)
        {
            if (!IsRepairing) return;
            using (RepairAlignMarker.Auto())
            {
                _tick++;
                if (_structure.Generation != _generation)
                {
                    Interrupt(EarthRepairInterruptReason.GenerationChanged, _tick);
                    return;
                }

                float dt = Mathf.Max(0.0001f, deltaTime);
                int activePiece = _nextOrderSlot < _orderResult.OrderedPieceCount
                    ? _order[_nextOrderSlot]
                    : -1;
                for (int index = 0; index < _available.Length; index++)
                {
                    if (!_available[index] || _welded[index]) continue;
                    EarthPieceRuntime piece = _structure.GetPieceRuntime(index);
                    if (piece == null || !piece.gameObject.activeSelf ||
                        piece.Generation != _generation || piece.Body == null)
                    {
                        Interrupt(EarthRepairInterruptReason.TargetInvalidated, _tick);
                        return;
                    }
                    UpdatePiece(index, piece, index == activePiece, dt);
                    if (!IsRepairing) return;
                }

                if (_weldedPieceCount >= _orderResult.OrderedPieceCount)
                    FinishRepair();
            }
        }

        private void FixedUpdate() => TickRepair(Time.fixedDeltaTime);

        private void UpdatePiece(int index, EarthPieceRuntime piece, bool isActivePiece, float dt)
        {
            Rigidbody body = piece.Body;
            EarthPieceState canonical = _structure.GetPieceState(index);
            EarthPiecePhase phase = canonical.Phase;
            if (phase != EarthPiecePhase.Captured && phase != EarthPiecePhase.Staging &&
                phase != EarthPiecePhase.Aligning && phase != EarthPiecePhase.WeldCandidate)
            {
                return;
            }

            Vector3 restPosition = _wall.transform.TransformPoint(ToVector3(
                _pieceDefinitions[index].RestLocalPosition));
            Quaternion restRotation = _wall.transform.rotation * ToQuaternion(
                _pieceDefinitions[index].RestLocalRotation);
            float3 offset = EarthRepairPoseSolver.StagingOffset(
                _pieceDefinitions[index].Id,
                _graphDepth[index],
                StagingDistance * (1f + (_congestionExpansion * 0.45f)),
                ToFloat3(_wall.transform.forward),
                ToFloat3(_wall.transform.right),
                _progress[index].RetryCount);
            float offsetScale = phase == EarthPiecePhase.Captured ? 1.8f :
                                phase == EarthPiecePhase.Staging ? 1f : 0f;
            Vector3 targetPosition = restPosition + ToVector3(offset * offsetScale);
            var input = new EarthRepairPoseInput(
                ToFloat3(body.worldCenterOfMass),
                ToQuaternion(body.rotation),
                ToFloat3(body.linearVelocity),
                ToFloat3(body.angularVelocity),
                ToFloat3(targetPosition),
                ToQuaternion(restRotation),
                float3.zero,
                float3.zero,
                Mathf.Max(0.01f, body.mass));
            bool capturePhase = phase == EarthPiecePhase.Captured || phase == EarthPiecePhase.Staging;
            EarthRepairPoseControlSample sample;
            using (RepairSolveMarker.Auto())
                sample = EarthRepairPoseSolver.Solve(in input, in _tuning, capturePhase);
            if (!sample.IsFinite)
            {
                Interrupt(EarthRepairInterruptReason.SolverRejected, _tick);
                return;
            }

            if (body.isKinematic) body.isKinematic = false;
            body.detectCollisions = true;
            body.AddForce(ToVector3(sample.Acceleration), ForceMode.Acceleration);
            // Convex slabs have extremely anisotropic inertia. A torque-only
            // controller can remain wedged at a contact despite a valid PD target.
            // Keep the body dynamic and collidable, but integrate a bounded
            // angular step on the Rigidbody itself so it cannot snap or spin wild.
            float angularSpeed = Mathf.Min(
                body.maxAngularVelocity,
                Mathf.Sqrt(Mathf.Max(0f,
                    2f * _tuning.MaximumAngularAcceleration * sample.AngleErrorRadians)));
            float maximumStep = angularSpeed * dt * Mathf.Rad2Deg;
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, restRotation, maximumStep));
            body.angularVelocity = Vector3.MoveTowards(
                body.angularVelocity,
                Vector3.zero,
                _tuning.MaximumAngularAcceleration * dt);
            body.WakeUp();
            if (isActivePiece)
            {
                CurrentPiecePositionError = sample.PositionError;
                CurrentPieceSpeed = body.linearVelocity.magnitude;
                CurrentPieceAngleErrorDegrees = sample.AngleErrorRadians * Mathf.Rad2Deg;
                CurrentPieceAngularSpeed = body.angularVelocity.magnitude;
            }
            _phaseElapsed[index] += dt;

            if (phase == EarthPiecePhase.Captured)
            {
                if (sample.PositionError <= Mathf.Max(StagingTolerance * 2f, 0.35f) ||
                    _phaseElapsed[index] >= MaximumCaptureSeconds)
                {
                    _phaseElapsed[index] = 0f;
                    _structure.SetPiecePhase(index, EarthPiecePhase.Staging, _tick);
                    PieceStaged?.Invoke(new EarthPieceStagedEvent(
                        _tick, _structure.State.Id, _pieceDefinitions[index].Id,
                        _progress[index].RetryCount));
                }
                return;
            }

            if (phase == EarthPiecePhase.Staging)
            {
                if (_progress[index].RetryDelayRemaining > 0f)
                {
                    EarthRepairProgressState progress = _progress[index];
                    progress.RetryDelayRemaining = Mathf.Max(0f, progress.RetryDelayRemaining - dt);
                    _progress[index] = progress;
                    return;
                }
                if (isActivePiece && HasRequiredNeighbor(index) &&
                    (sample.PositionError <= StagingTolerance || _phaseElapsed[index] >= MaximumCaptureSeconds))
                {
                    _phaseElapsed[index] = 0f;
                    _settle[index] = default;
                    EarthRepairProgressState progress = _progress[index];
                    progress.BestError = float.MaxValue;
                    progress.SecondsWithoutProgress = 0f;
                    _progress[index] = progress;
                    _structure.SetPiecePhase(index, EarthPiecePhase.Aligning, _tick);
                    BeginReformingBonds(index);
                    PrepareTerrainSeating(index, piece, restPosition, restRotation);
                }
                return;
            }

            EarthRepairProgressState progressState = _progress[index];
            EarthRepairProgressSample progressSample = EarthRepairPoseSolver.UpdateProgress(
                sample.PositionError, dt, in _tuning, ref progressState);
            _progress[index] = progressState;
            if (progressSample.RetryRequested)
            {
                _congestionExpansion = (byte)Mathf.Min(4, _congestionExpansion + 1);
                _phaseElapsed[index] = 0f;
                _settle[index] = default;
                _structure.SetPiecePhase(index, EarthPiecePhase.Staging, _tick);
                PieceStaged?.Invoke(new EarthPieceStagedEvent(
                    _tick, _structure.State.Id, _pieceDefinitions[index].Id,
                    progressState.RetryCount));
                return;
            }

            EarthRepairSettleState settleState = _settle[index];
            bool settled = EarthRepairPoseSolver.UpdateSettle(
                in sample,
                body.linearVelocity.magnitude,
                body.angularVelocity.magnitude,
                dt,
                in _tuning,
                ref settleState);
            _settle[index] = settleState;
            if (settled) WeldPiece(index, restPosition, restRotation);
        }

        private bool HasRequiredNeighbor(int pieceIndex)
        {
            if (pieceIndex == _orderResult.AnchorPieceIndex || _graphDepth[pieceIndex] == 0) return true;
            for (int bondIndex = 0; bondIndex < _bondDefinitions.Length; bondIndex++)
            {
                EarthBondDefinition bond = _bondDefinitions[bondIndex];
                if ((bond.Flags & EarthBondFlags.Repairable) == 0) continue;
                int neighbor = bond.PieceA == pieceIndex ? bond.PieceB :
                               bond.PieceB == pieceIndex ? bond.PieceA : int.MinValue;
                if (neighbor == EarthBondGraph.WorldPieceIndex &&
                    (bond.Flags & EarthBondFlags.Foundation) != 0) return true;
                if (neighbor >= 0 && neighbor < _welded.Length && _available[neighbor] && _welded[neighbor])
                    return true;
            }
            return false;
        }

        private void BeginReformingBonds(int pieceIndex)
        {
            for (int bondIndex = 0; bondIndex < _bondDefinitions.Length; bondIndex++)
            {
                EarthBondDefinition bond = _bondDefinitions[bondIndex];
                if (!BondCanRestore(in bond, pieceIndex)) continue;
                _wall.SetRepairBondCollisionIgnored(bondIndex, true);
                _structure.SetBondReforming(bondIndex, _tick);
                BondReforming?.Invoke(new EarthBondReformingEvent(
                    _tick, _structure.State.Id, bond.Id, 0f));
            }
        }

        private void WeldPiece(int index, Vector3 restPosition, Quaternion restRotation)
        {
            using var marker = RepairWeldMarker.Auto();
            Rigidbody body = _structure.GetPieceRuntime(index).Body;
            _structure.SetPiecePhase(index, EarthPiecePhase.WeldCandidate, _tick);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = restPosition;
            body.rotation = restRotation;
            body.isKinematic = true;
            _structure.SetPiecePhase(index, EarthPiecePhase.Welded, _tick);
            _welded[index] = true;
            _weldedPieceCount++;

            for (int bondIndex = 0; bondIndex < _bondDefinitions.Length; bondIndex++)
            {
                EarthBondDefinition bond = _bondDefinitions[bondIndex];
                if (!BondCanRestore(in bond, index)) continue;
                EarthBondState state = _structure.GetBondState(bondIndex);
                if (state.Phase == EarthBondPhase.Repaired) continue;
                _structure.SetBondRepaired(bondIndex, _tick);
                _wall.RestoreBondForRepair(bondIndex);
                BondRepaired?.Invoke(new EarthBondRepairedEvent(
                    _tick, _structure.State.Id, bond.Id));
            }
            _structure.GetPieceRuntime(index).ReleaseFromRepair();
            AdvanceOrderCursor();
        }

        private bool BondCanRestore(in EarthBondDefinition bond, int pieceIndex)
        {
            if ((bond.Flags & EarthBondFlags.Repairable) == 0 ||
                (bond.PieceA != pieceIndex && bond.PieceB != pieceIndex)) return false;
            int other = bond.PieceA == pieceIndex ? bond.PieceB : bond.PieceA;
            if (other == EarthBondGraph.WorldPieceIndex)
                return (bond.Flags & EarthBondFlags.Foundation) != 0;
            return other >= 0 && other < _welded.Length && _available[other] && _welded[other];
        }

        private void FinishRepair()
        {
            bool complete = _orderResult.OrderedPieceCount == _pieceDefinitions.Length;
            if (complete)
            {
                for (int bondIndex = 0; bondIndex < _bondDefinitions.Length; bondIndex++)
                {
                    if ((_bondDefinitions[bondIndex].Flags & EarthBondFlags.Repairable) != 0 &&
                        _structure.GetBondState(bondIndex).Phase != EarthBondPhase.Repaired)
                    {
                        complete = false;
                        break;
                    }
                }
            }
            IsRepairing = false;
            LastRepairWasPartial = !complete;
            if (complete)
            {
                ClearSeatingCollisionIgnores(true);
                _wall.CompletePhysicalRepair(_tick);
                StructureRebuilt?.Invoke(new EarthStructureRebuiltEvent(
                    _tick, _structure.State.Id, _structure.State.Revision));
            }
            else
            {
                _structure.FinishPartialRepair(_tick);
            }
        }

        public void ResetRepairCollisionPolicy()
        {
            if (IsRepairing) Interrupt(EarthRepairInterruptReason.TargetInvalidated, _tick);
            ClearSeatingCollisionIgnores(true);
        }

        private void PrepareTerrainSeating(
            int pieceIndex,
            EarthPieceRuntime piece,
            Vector3 targetPosition,
            Quaternion targetRotation)
        {
            Collider pieceCollider = piece.GetComponent<Collider>();
            if (pieceCollider == null || _ignoredCollisionCount >= _ignoredPieceColliders.Length) return;
            Vector3 halfExtents = Vector3.Max(
                pieceCollider.bounds.extents * 0.96f,
                Vector3.one * 0.02f);
            int count = UnityEngine.Physics.OverlapBoxNonAlloc(
                targetPosition,
                halfExtents,
                _seatingOverlap,
                targetRotation,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int hitIndex = 0; hitIndex < count &&
                 _ignoredCollisionCount < _ignoredPieceColliders.Length; hitIndex++)
            {
                Collider obstacle = _seatingOverlap[hitIndex];
                if (obstacle == null || obstacle == pieceCollider || obstacle.attachedRigidbody != null)
                    continue;
                bool duplicate = false;
                for (int existing = 0; existing < _ignoredCollisionCount; existing++)
                {
                    if (_ignoredPieceColliders[existing] == pieceCollider &&
                        _ignoredWorldColliders[existing] == obstacle)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate) continue;
                UnityEngine.Physics.IgnoreCollision(pieceCollider, obstacle, true);
                _ignoredPieceColliders[_ignoredCollisionCount] = pieceCollider;
                _ignoredWorldColliders[_ignoredCollisionCount] = obstacle;
                _ignoredPieceIndices[_ignoredCollisionCount] = pieceIndex;
                _ignoredCollisionCount++;
            }
            for (int hitIndex = 0; hitIndex < count; hitIndex++) _seatingOverlap[hitIndex] = null;
        }

        private void ClearSeatingCollisionIgnores(bool includeWelded)
        {
            for (int index = _ignoredCollisionCount - 1; index >= 0; index--)
            {
                int pieceIndex = _ignoredPieceIndices[index];
                if (!includeWelded && pieceIndex >= 0 && pieceIndex < _welded.Length && _welded[pieceIndex])
                    continue;
                Collider piece = _ignoredPieceColliders[index];
                Collider obstacle = _ignoredWorldColliders[index];
                if (piece != null && obstacle != null)
                    UnityEngine.Physics.IgnoreCollision(piece, obstacle, false);
                int last = --_ignoredCollisionCount;
                _ignoredPieceColliders[index] = _ignoredPieceColliders[last];
                _ignoredWorldColliders[index] = _ignoredWorldColliders[last];
                _ignoredPieceIndices[index] = _ignoredPieceIndices[last];
                _ignoredPieceColliders[last] = null;
                _ignoredWorldColliders[last] = null;
                _ignoredPieceIndices[last] = 0;
            }
        }

        private void AdvanceOrderCursor()
        {
            while (_nextOrderSlot < _orderResult.OrderedPieceCount)
            {
                int piece = _order[_nextOrderSlot];
                if (piece >= 0 && !_welded[piece]) break;
                _nextOrderSlot++;
            }
        }

        private bool Reject(uint tick, EarthRepairRejectReason reason)
        {
            EarthStructureId structureId = _structure != null ? _structure.State.Id : default;
            RepairRejected?.Invoke(new EarthRepairRejectedEvent(tick, structureId, reason));
            return false;
        }

        private float StagingDistance => _profile != null ? _profile.StagingDistance : 0.72f;
        private float StagingTolerance => _profile != null ? _profile.StagingTolerance : 0.22f;
        private float MaximumCaptureSeconds => _profile != null ? _profile.MaximumCaptureSeconds : 1.1f;

        private static EarthReassemblyTuning DefaultTuning()
        {
            return new EarthReassemblyTuning
            {
                CaptureSettleTime = 0.42f,
                AlignmentSettleTime = 0.28f,
                DampingRatio = 1f,
                MaximumAcceleration = 55f,
                MaximumForce = 2400f,
                MaximumAngularAcceleration = 90f,
                RotationStiffness = 42f,
                RotationDamping = 13f,
                PositionTolerance = 0.025f,
                AngleToleranceRadians = math.radians(2.5f),
                MaximumRelativeSpeed = 0.12f,
                MaximumRelativeAngularSpeed = 0.2f,
                SettleDuration = 0.12f,
                JamDuration = 0.65f,
                JamProgressEpsilon = 0.004f,
                RetryDelay = 0.18f
            };
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
        private static quaternion ToQuaternion(Quaternion value) =>
            new quaternion(value.x, value.y, value.z, value.w);
        private static Quaternion ToQuaternion(quaternion value) =>
            new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
    }
}
