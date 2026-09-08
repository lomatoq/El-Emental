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
        public EarthCharacterImpactSourceKind SourceKind => _source;
        public bool IsArmed => _armed;
        public float DamageOverride { get; private set; } = -1f;

        public void Arm(EarthFragment fragment, EarthCharacterImpactSourceKind source, float damageOverride = -1f)
        {
            _fragment = fragment;
            SubscribeToFragment();
            _source = source;
            DamageOverride = damageOverride;
            _armed = true;
        }

        private void HandleSurfaceImpact(EarthProjectileSurfaceImpact impact)
        {
            if (!_armed || _fragment == null) return;
            _armed = false;
            Collider hit = impact.Surface;
            EarthCharacterImpactTarget target = hit != null
                ? EarthStoneCharacterContact.ResolveTarget(hit)
                : null;
            if (target == null) return;
            float speed = Mathf.Abs(Vector3.Dot(impact.RelativeVelocity, impact.Normal));
            Vector3 direction = impact.RelativeVelocity.sqrMagnitude > 0.001f
                ? impact.RelativeVelocity.normalized
                : -impact.Normal;
            target.ApplyStoneImpact(
                impact.Point,
                direction,
                _fragment.Mass,
                speed,
                _source,
                _fragment.FragmentId,
                damageOverride: DamageOverride);
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
            DamageOverride = -1f;
            _fragment = null;
        }
    }
}
