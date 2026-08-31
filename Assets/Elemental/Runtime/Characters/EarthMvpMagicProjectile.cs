using Elemental.Runtime.Physics;
using Elemental.Simulation.Combat;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public enum EarthMvpProjectileState : byte
    {
        Inactive = 0,
        Armed = 1,
        SpentDynamic = 2,
        Sleeping = 3,
        Reintegrating = 4
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(EarthFragment))]
    public sealed class EarthMvpMagicProjectile : MonoBehaviour
    {
        private EarthFragment _fragment;
        private EarthFragment _subscribedFragment;
        private ActiveRagdollPuppet _target;
        private EarthMvpDuelController _duel;
        private EarthMvpBotController _owner;
        private Vector3 _launchDirection;
        private float _knockoutVelocityChange;
        private float _hardFailsafeAt;
        private float _groundedAt;
        private float _sleepStartedAt;
        private float _reintegratingAt;
        private Vector3 _reintegratingPosition;
        private Vector3 _reintegratingScale;

        public EarthMvpProjectileState State { get; private set; }

        public void Configure(
            EarthFragment fragment,
            ActiveRagdollPuppet target,
            EarthMvpDuelController duel,
            EarthMvpBotController owner,
            Vector3 launchDirection,
            float knockoutVelocityChange,
            float lifetimeSeconds)
        {
            _fragment = fragment;
            SubscribeToFragment();
            _target = target;
            _duel = duel;
            _owner = owner;
            _launchDirection = launchDirection.sqrMagnitude > 0.001f
                ? launchDirection.normalized
                : transform.forward;
            _knockoutVelocityChange = Mathf.Max(0.1f, knockoutVelocityChange);
            _hardFailsafeAt = Time.time + 20f;
            _groundedAt = float.PositiveInfinity;
            _sleepStartedAt = float.PositiveInfinity;
            State = EarthMvpProjectileState.Armed;
        }

        private void FixedUpdate()
        {
            if (State == EarthMvpProjectileState.Inactive || _fragment == null) return;
            if (_fragment.IsHeld)
            {
                if (State is EarthMvpProjectileState.Sleeping or EarthMvpProjectileState.Reintegrating)
                    State = EarthMvpProjectileState.SpentDynamic;
                _sleepStartedAt = float.PositiveInfinity;
                transform.localScale = _reintegratingScale.sqrMagnitude > 0.001f
                    ? _reintegratingScale
                    : transform.localScale;
                return;
            }

            Rigidbody body = _fragment.Body;
            if (State is EarthMvpProjectileState.SpentDynamic or EarthMvpProjectileState.Sleeping)
            {
                bool stable = body != null &&
                              (body.IsSleeping() ||
                               (body.linearVelocity.sqrMagnitude < 0.0064f &&
                                body.angularVelocity.sqrMagnitude < 0.0225f));
                if (!stable)
                {
                    State = EarthMvpProjectileState.SpentDynamic;
                    _sleepStartedAt = float.PositiveInfinity;
                }
                else
                {
                    if (!float.IsFinite(_sleepStartedAt)) _sleepStartedAt = Time.time;
                    State = EarthMvpProjectileState.Sleeping;
                    if (Time.time - _groundedAt >= 6f && Time.time - _sleepStartedAt >= 4f)
                        BeginReintegration();
                }
            }

            if (State == EarthMvpProjectileState.Reintegrating)
            {
                float t = Mathf.Clamp01((Time.time - _reintegratingAt) / 0.8f);
                Vector3 up = transform.position.sqrMagnitude > 0.1f
                    ? transform.position.normalized
                    : transform.up;
                transform.position = _reintegratingPosition - up * (0.34f * t);
                transform.localScale = Vector3.Lerp(_reintegratingScale, Vector3.zero, t * t);
                if (t >= 1f) CompleteReintegration();
            }

            if (Time.time >= _hardFailsafeAt && IsOutsidePlayableWorld(transform.position))
                CompleteReintegration();
        }

        private void HandleSurfaceImpact(EarthProjectileSurfaceImpact impact)
        {
            if (State != EarthMvpProjectileState.Armed) return;
            State = EarthMvpProjectileState.SpentDynamic;
            _groundedAt = Time.time;
            Collider hit = impact.Surface;
            bool hitTarget = _target != null &&
                             (_target.OwnsCollider(hit) ||
                              (hit != null &&
                               (hit.transform == _target.transform ||
                                hit.transform.IsChildOf(_target.transform))));
            Vector3 point = impact.Point;
            if (hitTarget)
            {
                Vector3 up = _target.transform.up;
                Vector3 launch = (_launchDirection + up * 0.22f).normalized *
                                 _knockoutVelocityChange;
                EarthCharacterImpactTarget impactTarget = hit != null
                    ? hit.GetComponentInParent<EarthCharacterImpactTarget>()
                    : null;
                if (impactTarget == null && _target != null)
                {
                    impactTarget = _target.GetComponent<EarthCharacterImpactTarget>();
                    if (impactTarget == null)
                        impactTarget = _target.GetComponentInParent<EarthCharacterImpactTarget>();
                }
                if (impactTarget != null)
                {
                    float targetMass = impactTarget.Body != null ? impactTarget.Body.mass : 42f;
                    impactTarget.ApplyImpact(
                        point,
                        launch,
                        Mathf.Max(0.01f, targetMass) * _knockoutVelocityChange,
                        EarthCharacterImpactSourceKind.BotProjectile,
                        _fragment != null ? _fragment.FragmentId : 0xB0700001u,
                        _knockoutVelocityChange,
                        1f);
                }
                else
                {
                    _duel?.KnockoutPlayer(launch);
                }
                _owner?.NotifyProjectileLanded(point);
            }
        }

        private void BeginReintegration()
        {
            if (State == EarthMvpProjectileState.Reintegrating) return;
            State = EarthMvpProjectileState.Reintegrating;
            _reintegratingAt = Time.time;
            _reintegratingPosition = transform.position;
            _reintegratingScale = transform.localScale;
            Rigidbody body = _fragment != null ? _fragment.Body : null;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }

        private void CompleteReintegration()
        {
            if (State == EarthMvpProjectileState.Inactive) return;
            State = EarthMvpProjectileState.Inactive;
            _fragment?.CompleteReintegration();
        }

        private static bool IsOutsidePlayableWorld(Vector3 position) =>
            !float.IsFinite(position.x) || !float.IsFinite(position.y) ||
            !float.IsFinite(position.z) || position.sqrMagnitude > 62500f;

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
            State = EarthMvpProjectileState.Inactive;
            _target = null;
            _duel = null;
            _owner = null;
        }
    }
}
