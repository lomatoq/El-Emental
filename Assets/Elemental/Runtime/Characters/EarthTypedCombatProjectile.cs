using Elemental.Runtime.Physics;
using Elemental.Simulation.Combat;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EarthFragment))]
    public sealed class EarthTypedCombatProjectile : MonoBehaviour
    {
        private EarthFragment _fragment;
        private EarthCharacterImpactSourceKind _source;
        private bool _armed;

        public void Arm(EarthFragment fragment, EarthCharacterImpactSourceKind source)
        {
            _fragment = fragment;
            _source = source;
            _armed = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_armed || collision == null || collision.contactCount == 0 || _fragment == null) return;
            _armed = false;
            Collider hit = collision.collider;
            EarthCharacterImpactTarget target = hit != null
                ? hit.GetComponentInParent<EarthCharacterImpactTarget>()
                : null;
            if (target == null) return;
            ContactPoint contact = collision.GetContact(0);
            Rigidbody body = _fragment.Body;
            float speed = collision.relativeVelocity.magnitude;
            Vector3 direction = body != null && body.linearVelocity.sqrMagnitude > 0.001f
                ? body.linearVelocity.normalized
                : -contact.normal;
            target.ApplyImpact(
                contact.point,
                direction,
                Mathf.Max(0.01f, _fragment.Mass) * speed,
                _source,
                _fragment.FragmentId,
                speed,
                1f);
        }

        private void OnDisable()
        {
            _armed = false;
            _fragment = null;
        }
    }
}
