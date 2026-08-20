using System;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Voxel;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Matter
{
    [DisallowMultipleComponent]
    public sealed class EarthMatterReturnController : MonoBehaviour
    {
        private const int HardMaximumConcurrentReturns = 16;

        private sealed class ReturnSlot
        {
            public readonly EarthReturnSession Session = new EarthReturnSession();
            public EarthMatterIdentity Identity;
            public Rigidbody Body;
            public float JamElapsed;
            public bool CommitSubmitted;
            public bool SubsurfaceEmitted;
            public bool Active => Identity != null && Body != null;
            public void Clear()
            {
                Identity = null;
                Body = null;
                JamElapsed = 0f;
                CommitSubmitted = false;
                SubsurfaceEmitted = false;
            }
        }

        [SerializeField] private VoxelPlanetBehaviour voxelPlanet;
        [SerializeField] private EarthMatterKernelBehaviour kernel;
        [SerializeField, Min(0.1f)] private float materialDensity = 120f;
        [SerializeField, Range(1, HardMaximumConcurrentReturns)] private int maximumConcurrentReturns = 16;
        [SerializeField, Min(0.1f)] private float jamSeconds = 0.72f;

        private readonly ReturnSlot[] _slots = CreateSlots();
        private MagicWorldEvents _events;

        public event Action<EarthReturnEvent> ReturnStageChanged;

        public EarthReturnPhase Phase
        {
            get
            {
                for (int index = 0; index < _slots.Length; index++)
                    if (_slots[index].Active) return _slots[index].Session.Phase;
                return EarthReturnPhase.Idle;
            }
        }
        public bool IsReturning => ActiveReturnCount > 0;
        public int ActiveReturnCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _slots.Length; index++) if (_slots[index].Active) count++;
                return count;
            }
        }
        public EarthMatterId ReturningMatter
        {
            get
            {
                for (int index = 0; index < _slots.Length; index++)
                    if (_slots[index].Active) return _slots[index].Session.MatterId;
                return default;
            }
        }

        public void Configure(VoxelPlanetBehaviour configuredPlanet, EarthMatterKernelBehaviour configuredKernel, float configuredDensity)
        {
            DetachPlanetEvent();
            voxelPlanet = configuredPlanet;
            kernel = configuredKernel;
            materialDensity = Mathf.Max(0.1f, configuredDensity);
            _events = GetComponent<MagicExecutor>()?.Events;
            AttachPlanetEvent();
        }

        private void Awake()
        {
            if (voxelPlanet == null) voxelPlanet = FindAnyObjectByType<VoxelPlanetBehaviour>();
            if (kernel == null) kernel = EarthMatterKernelBehaviour.FindOrCreate(this);
            _events = GetComponent<MagicExecutor>()?.Events;
            AttachPlanetEvent();
        }

        private void OnDestroy() => DetachPlanetEvent();

        public bool TryBeginReturn(EarthMatterIdentity identity, Vector3 fallbackSurfaceWorld)
        {
            if (identity == null || voxelPlanet == null || FindSlot(identity) >= 0 ||
                !identity.TryRead(out EarthMatterRecord record)) return false;
            int slotIndex = FindFreeSlot();
            if (slotIndex < 0) return false;
            Rigidbody body = identity.Body;
            if (body == null || !TryCapturePhase(identity, record.Phase)) return false;

            float3 startLocal = ToFloat3(voxelPlanet.transform.InverseTransformPoint(body.worldCenterOfMass));
            float3 fallbackLocal = ToFloat3(voxelPlanet.transform.InverseTransformPoint(fallbackSurfaceWorld));
            EarthReturnDestination destination = EarthReturnDestinationResolver.Resolve(
                in record, float3.zero, false, fallbackLocal, true);
            if (destination.Kind == EarthReturnDestinationKind.DormantStorage) return false;
            ReturnSlot slot = _slots[slotIndex];
            EarthReturnConfiguration configuration = EarthReturnConfiguration.Default;
            if (!slot.Session.Begin(identity.MatterId, in record, startLocal, in destination, in configuration))
                return false;

            slot.Identity = identity;
            slot.Body = body;
            slot.JamElapsed = 0f;
            slot.CommitSubmitted = false;
            slot.SubsurfaceEmitted = false;
            identity.GetComponent<EarthFragment>()?.StopBendControl();
            body.isKinematic = false;
            body.WakeUp();
            Emit(slot, in record, EarthReturnEventStage.Captured);
            return true;
        }

        public int TryBeginReturnsNonAlloc(EarthMatterIdentity[] identities, int count, Vector3 fallbackSurfaceWorld)
        {
            int safeCount = Mathf.Min(identities?.Length ?? 0, Mathf.Max(0, count));
            int started = 0;
            for (int index = 0; index < safeCount; index++)
                if (TryBeginReturn(identities[index], fallbackSurfaceWorld)) started++;
            return started;
        }

        public bool ReverseBeforeCommit()
        {
            bool reversedAny = false;
            for (int index = 0; index < _slots.Length; index++)
            {
                ReturnSlot slot = _slots[index];
                if (!slot.Active || !slot.Session.ReverseBeforeCommit()) continue;
                if (slot.Identity.TryRead(out EarthMatterRecord record))
                    Emit(slot, in record, EarthReturnEventStage.Reversed);
                slot.Identity.TryTransition(EarthMatterPhase.FreeDynamic);
                RestoreDynamicBody(slot);
                slot.Clear();
                reversedAny = true;
            }
            return reversedAny;
        }

        private void FixedUpdate()
        {
            if (voxelPlanet == null) return;
            for (int index = 0; index < _slots.Length; index++)
            {
                ReturnSlot slot = _slots[index];
                if (!slot.Active || !slot.Session.IsActive || slot.CommitSubmitted) continue;
                Vector3 localPosition = voxelPlanet.transform.InverseTransformPoint(slot.Body.worldCenterOfMass);
                Vector3 localVelocity = voxelPlanet.transform.InverseTransformVector(slot.Body.linearVelocity);
                EarthReturnFrame frame = slot.Session.Step(
                    Time.fixedDeltaTime, ToFloat3(localPosition), ToFloat3(localVelocity));
                if ((frame.Phase == EarthReturnPhase.SubsurfaceTransfer ||
                     frame.Phase == EarthReturnPhase.Reintegrating) &&
                    slot.Identity.TryRead(out EarthMatterRecord record) &&
                    record.Phase == EarthMatterPhase.CapturedForReturn)
                {
                    slot.Identity.TryTransition(EarthMatterPhase.Returning);
                    if (!slot.SubsurfaceEmitted)
                    {
                        slot.SubsurfaceEmitted = true;
                        Emit(slot, in record, EarthReturnEventStage.Subsurface);
                    }
                }

                Vector3 acceleration = voxelPlanet.transform.TransformVector(ToVector3(frame.Acceleration));
                slot.Body.AddForce(acceleration, ForceMode.Acceleration);
                slot.Body.AddTorque(-slot.Body.angularVelocity * 7.5f, ForceMode.Acceleration);
                float error = Vector3.Distance(
                    slot.Body.worldCenterOfMass,
                    voxelPlanet.transform.TransformPoint(ToVector3(frame.Target)));
                if (error > 0.45f && slot.Body.linearVelocity.sqrMagnitude < 0.01f)
                    slot.JamElapsed += Time.fixedDeltaTime;
                else
                    slot.JamElapsed = Mathf.Max(0f, slot.JamElapsed - Time.fixedDeltaTime * 0.5f);
                if (slot.JamElapsed >= jamSeconds)
                {
                    slot.Session.MarkJammed();
                    if (slot.Identity.TryRead(out EarthMatterRecord jammed))
                        Emit(slot, in jammed, EarthReturnEventStage.Jammed);
                    slot.Identity.TryTransition(EarthMatterPhase.FreeDynamic);
                    RestoreDynamicBody(slot);
                    slot.Clear();
                    continue;
                }
                if (frame.RequestCommit) SubmitCommit(slot);
            }
        }

        private void SubmitCommit(ReturnSlot slot)
        {
            if (slot.CommitSubmitted || !slot.Active || !slot.Identity.TryRead(out EarthMatterRecord record)) return;
            if (!slot.Identity.TryTransition(EarthMatterPhase.Reintegrating)) return;
            float radius = EarthReturnGeometry.SphereRadiusForVolume(record.Volume);
            VoxelEditReceipt receipt = voxelPlanet.ApplySphereEditTransactional(
                ToVector3(slot.Session.Destination.PlanetLocalPoint), radius, true);
            if (!receipt.IsValid) return;
            slot.CommitSubmitted = slot.Session.MarkSdfCommitPending(receipt.TransactionId);
            if (!slot.CommitSubmitted) return;
            slot.Body.linearVelocity = Vector3.zero;
            slot.Body.angularVelocity = Vector3.zero;
            slot.Body.isKinematic = true;
            Emit(slot, in record, EarthReturnEventStage.CommitSubmitted);
        }

        private void HandleEditCommitted(VoxelEditReceipt receipt)
        {
            for (int index = 0; index < _slots.Length; index++)
            {
                ReturnSlot slot = _slots[index];
                if (!slot.Active || !slot.CommitSubmitted ||
                    receipt.TransactionId != slot.Session.PendingTransactionId ||
                    !slot.Session.ConfirmCommit(receipt.TransactionId)) continue;
                slot.Identity.TryTransition(EarthMatterPhase.TerrainAttached);
                GameObject representation = slot.Identity.gameObject;
                EarthFragment fragment = representation.GetComponent<EarthFragment>();
                if (slot.Identity.TryRead(out EarthMatterRecord committed))
                    Emit(slot, in committed, EarthReturnEventStage.Completed);
                slot.Identity.ReleaseRepresentationAfterTerrainCommit();
                if (fragment != null) fragment.CompleteReintegration();
                else representation.SetActive(false);
                slot.Clear();
                return;
            }
        }

        private int FindFreeSlot()
        {
            int limit = Mathf.Clamp(maximumConcurrentReturns, 1, _slots.Length);
            for (int index = 0; index < limit; index++) if (!_slots[index].Active) return index;
            return -1;
        }

        private int FindSlot(EarthMatterIdentity identity)
        {
            for (int index = 0; index < _slots.Length; index++) if (_slots[index].Identity == identity) return index;
            return -1;
        }

        private static bool TryCapturePhase(EarthMatterIdentity identity, EarthMatterPhase current) =>
            current == EarthMatterPhase.CapturedForReturn || identity.TryTransition(EarthMatterPhase.CapturedForReturn);

        private static void RestoreDynamicBody(ReturnSlot slot)
        {
            if (slot.Body == null) return;
            slot.Body.isKinematic = false;
            slot.Body.WakeUp();
        }

        private void AttachPlanetEvent()
        {
            if (voxelPlanet == null) return;
            voxelPlanet.EditCommitted -= HandleEditCommitted;
            voxelPlanet.EditCommitted += HandleEditCommitted;
        }

        private void DetachPlanetEvent()
        {
            if (voxelPlanet != null) voxelPlanet.EditCommitted -= HandleEditCommitted;
        }

        private static ReturnSlot[] CreateSlots()
        {
            var slots = new ReturnSlot[HardMaximumConcurrentReturns];
            for (int index = 0; index < slots.Length; index++) slots[index] = new ReturnSlot();
            return slots;
        }

        private void Emit(ReturnSlot slot, in EarthMatterRecord record, EarthReturnEventStage stage)
        {
            if (slot == null || slot.Body == null) return;
            Vector3 point = slot.Body.worldCenterOfMass;
            var value = new EarthReturnEvent(
                unchecked((uint)Time.frameCount),
                record.Id.StableId,
                record.Id.Generation,
                stage,
                ToFloat3(point),
                record.Volume,
                record.Mass);
            ReturnStageChanged?.Invoke(value);
            _events?.Emit(in value);
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
