using System;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public readonly struct PhysicalCollisionImpact
    {
        public PhysicalCollisionImpact(
            Vector3 point,
            Vector3 normal,
            float impulse,
            bool otherBodyIsDynamic)
        {
            Point = point;
            Normal = normal;
            Impulse = impulse;
            OtherBodyIsDynamic = otherBodyIsDynamic;
        }

        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public float Impulse { get; }
        public bool OtherBodyIsDynamic { get; }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicalImpactTarget : MonoBehaviour, IEarthPhysicalTarget
    {
        [SerializeField] private Rigidbody targetBody;
        [SerializeField, Min(0f)] private float impulseScale = 1f;
        private float _suppressUntil;

        public Rigidbody Body => targetBody;
        public int ImpactCount { get; private set; }
        public float AccumulatedImpulse { get; private set; }
        public event Action<Vector3, float> ImpactApplied;
        public event Action<PhysicalCollisionImpact> CollisionImpactApplied;
        public uint StableEarthId => unchecked((uint)GetHashCode());
        public EarthPhysicalTargetHandle TargetHandle => new EarthPhysicalTargetHandle(StableEarthId, 1u);
        public float EarthMass => targetBody != null ? targetBody.mass : 0f;
        public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.Rock;
        public bool IsEarthTargetValid => targetBody != null && !targetBody.isKinematic && gameObject.activeInHierarchy;

        public void Configure(Rigidbody body, float configuredImpulseScale = 1f)
        {
            targetBody = body;
            impulseScale = Mathf.Max(0f, configuredImpulseScale);
        }

        public void ApplyImpact(Vector3 point, Vector3 direction, float impulse)
        {
            if (Time.time < _suppressUntil) return;
            if (targetBody == null || targetBody.isKinematic || impulse <= 0f)
            {
                return;
            }

            Vector3 safeDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : transform.up;
            float scaledImpulse = impulse * impulseScale;
            targetBody.AddForceAtPosition(safeDirection * scaledImpulse, point, ForceMode.Impulse);
            RecordImpact(point, scaledImpulse);
        }

        private void Awake()
        {
            if (targetBody == null)
            {
                targetBody = GetComponent<Rigidbody>();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < _suppressUntil) return;
            float impulse = collision.impulse.magnitude;
            if (impulse <= 0.01f || collision.contactCount == 0)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            float scaledImpulse = impulse * impulseScale;
            if (scaledImpulse <= 0.01f) return;
            ImpactCount++;
            AccumulatedImpulse += scaledImpulse;
            Rigidbody otherBody = collision.rigidbody;
            CollisionImpactApplied?.Invoke(new PhysicalCollisionImpact(
                contact.point,
                contact.normal,
                scaledImpulse,
                otherBody != null && !otherBody.isKinematic));
        }

        public void SuppressImpacts(float seconds)
        {
            _suppressUntil = Mathf.Max(_suppressUntil, Time.time + Mathf.Max(0f, seconds));
        }

        private void RecordImpact(Vector3 point, float impulse)
        {
            ImpactCount++;
            AccumulatedImpulse += impulse;
            ImpactApplied?.Invoke(point, impulse);
        }

        public void OnEarthMagicGrabbed(EarthMagicGripKind grip)
        {
            targetBody?.WakeUp();
        }

        public void OnEarthMagicReleased(EarthMagicGripKind grip)
        {
        }
    }
}
