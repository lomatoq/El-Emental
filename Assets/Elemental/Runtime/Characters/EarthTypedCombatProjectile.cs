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
        private EarthFragment _subscribedFragment;
        private EarthCharacterImpactSourceKind _source;
        private bool _armed;

        public void Arm(EarthFragment fragment, EarthCharacterImpactSourceKind source)
        {
            _fragment = fragment;
            SubscribeToFragment();
            _source = source;
            _armed = true;
        }

        private void HandleSurfaceImpact(EarthProjectileSurfaceImpact impact)
        {
            if (!_armed || _fragment == null) return;
            _armed = false;
            Collider hit = impact.Surface;
            EarthCharacterImpactTarget target = hit != null
                ? hit.GetComponentInParent<EarthCharacterImpactTarget>()
                : null;
            if (target == null) return;
            Rigidbody body = _fragment.Body;
            float speed = impact.RelativeVelocity.magnitude;
            Vector3 direction = body != null && body.linearVelocity.sqrMagnitude > 0.001f
                ? body.linearVelocity.normalized
                : -impact.Normal;
            target.ApplyImpact(
                impact.Point,
                direction,
                Mathf.Max(0.01f, _fragment.Mass) * speed,
                _source,
                _fragment.FragmentId,
                speed,
                1f);
        }

        private void Awake()
        {
            if (_fragment == null) _fragment = GetComponent<EarthFragment>();
        }

        private void OnEnable() => SubscribeToFragment();

        private void SubscribeToFragment()
        {
            EarthFragment resolved = _fragment != null ? _fragment : GetComponent<EarthFragment>();
            if (_subscribedFragment == resolved) return;
            if (_subscribedFragment != null)
                _subscribedFragment.SurfaceImpactAccepted -= HandleSurfaceImpact;
            _fragment = resolved;
            _subscribedFragment = resolved;
            if (_subscribedFragment != null)
                _subscribedFragment.SurfaceImpactAccepted += HandleSurfaceImpact;
        }

        private void OnDisable()
        {
            if (_subscribedFragment != null)
                _subscribedFragment.SurfaceImpactAccepted -= HandleSurfaceImpact;
            _subscribedFragment = null;
            _armed = false;
            _fragment = null;
        }
    }
}
