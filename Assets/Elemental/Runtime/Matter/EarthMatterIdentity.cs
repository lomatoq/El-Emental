using Elemental.Simulation.Matter;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Matter
{
    [DisallowMultipleComponent]
    public sealed class EarthMatterIdentity : MonoBehaviour
    {
        [SerializeField] private Rigidbody targetBody;
        private EarthMatterKernelBehaviour _kernel;

        public EarthMatterId MatterId { get; private set; }
        public EarthMatterKernelBehaviour Kernel => _kernel;
        public Rigidbody Body => targetBody;
        public bool IsRegistered => _kernel != null && MatterId.IsValid && _kernel.TryGet(MatterId, out _);

        public bool Configure(EarthMatterKernelBehaviour kernel, in EarthMatterRecord authored, Rigidbody body = null)
        {
            // Additive QA scenes and domain-reload-disabled play sessions can leave a
            // pooled visual holding a handle issued by a different kernel instance.
            // Handles are registry-local; carrying one into the new registry can
            // alias an unrelated live record and makes re-registration fail with a
            // misleading `None` failure. Drop only the stale binding, never matter in
            // the still-authoritative registry.
            if (_kernel != null && _kernel != kernel) MatterId = default;
            _kernel = kernel;
            if (body != null) targetBody = body;
            if (targetBody == null) targetBody = GetComponent<Rigidbody>();
            return _kernel != null && _kernel.TryRegister(this, authored);
        }

        internal void AcceptRegistration(EarthMatterKernelBehaviour kernel, EarthMatterId id)
        {
            _kernel = kernel;
            MatterId = id;
        }

        internal void BindBody(Rigidbody body) => targetBody = body;

        public bool TryRead(out EarthMatterRecord record)
        {
            if (_kernel == null || !MatterId.IsValid)
            {
                record = default;
                return false;
            }
            return _kernel.TryGet(MatterId, out record);
        }

        public bool TryTransition(EarthMatterPhase phase) =>
            _kernel != null && MatterId.IsValid && _kernel.Registry.TryTransition(MatterId, phase);

        public bool TryTransferOwner(EarthOwnerId owner) =>
            _kernel != null && MatterId.IsValid && _kernel.Registry.TryTransferOwner(MatterId, owner);

        /// <summary>
        /// Detaches this pooled GameObject from a canonical matter record without
        /// deleting the record. Used only after the voxel render and collider commit
        /// confirms that terrain owns the volume again.
        /// </summary>
        public bool ReleaseRepresentationAfterTerrainCommit()
        {
            if (_kernel == null || !MatterId.IsValid ||
                !_kernel.Registry.TryGet(MatterId, out EarthMatterRecord record) ||
                record.Phase != EarthMatterPhase.TerrainAttached)
                return false;
            _kernel.Registry.TrySetRepresentation(MatterId, EarthRepresentationTier.CanonicalTerrain);
            MatterId = default;
            targetBody = null;
            return true;
        }

        /// <summary>
        /// Retires a transient proxy whose source terrain was never subtracted from
        /// the canonical SDF (wave crests and armor gathering stones). The matter
        /// still follows the return phases, but ends as a recyclable dormant record
        /// instead of submitting a duplicate additive terrain brush.
        /// </summary>
        public bool RetireTransientRepresentation()
        {
            if (!TryRead(out EarthMatterRecord record)) return false;
            if (record.Phase == EarthMatterPhase.Consumed) return true;
            if (record.Phase == EarthMatterPhase.TerrainAttached &&
                !TryTransition(EarthMatterPhase.Forming)) return false;
            if (!TryRead(out record)) return false;
            if ((record.Phase == EarthMatterPhase.FreeDynamic || record.Phase == EarthMatterPhase.Sleeping) &&
                !TryTransition(EarthMatterPhase.CapturedForReturn)) return false;
            if (!TryRead(out record)) return false;
            if (record.Phase == EarthMatterPhase.Forming ||
                record.Phase == EarthMatterPhase.Controlled ||
                record.Phase == EarthMatterPhase.CapturedForReturn)
            {
                if (!TryTransition(EarthMatterPhase.Returning)) return false;
            }
            if (!TryRead(out record)) return false;
            if (record.Phase == EarthMatterPhase.Returning &&
                !TryTransition(EarthMatterPhase.Reintegrating)) return false;
            if (!TryRead(out record)) return false;
            return record.Phase == EarthMatterPhase.Consumed ||
                   (record.Phase == EarthMatterPhase.Reintegrating &&
                    TryTransition(EarthMatterPhase.Consumed));
        }

        private void FixedUpdate()
        {
            if (_kernel == null || !MatterId.IsValid) return;
            Vector3 position = targetBody != null ? targetBody.position : transform.position;
            Quaternion rotation = targetBody != null ? targetBody.rotation : transform.rotation;
            Vector3 velocity = targetBody != null ? targetBody.linearVelocity : Vector3.zero;
            Vector3 angular = targetBody != null ? targetBody.angularVelocity : Vector3.zero;
            _kernel.Registry.TrySetKinematics(
                MatterId,
                new EarthMatterPose(ToFloat3(position), ToQuaternion(rotation)),
                ToFloat3(velocity),
                ToFloat3(angular));
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static quaternion ToQuaternion(Quaternion value) =>
            new quaternion(value.x, value.y, value.z, value.w);
    }
}
