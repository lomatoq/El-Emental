using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>
    /// Restores only the caster/launch-body collision pairs suppressed for one
    /// release. It never changes a project layer or a global collision matrix.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthLaunchCollisionGrace : MonoBehaviour
    {
        private const int MaximumPairs = 96;
        private readonly Collider[] _launchColliders = new Collider[8];
        private readonly Collider[] _casterColliders = new Collider[24];
        private readonly Collider[] _pairLaunch = new Collider[MaximumPairs];
        private readonly Collider[] _pairCaster = new Collider[MaximumPairs];
        private readonly List<Collider> _scratch = new List<Collider>(24);
        private Rigidbody _body;
        private Transform _caster;
        private Vector3 _launchDirection;
        private float _restoreAt;
        private float _safeDistance;
        private int _pairCount;
        private bool _active;

        public void Begin(Transform caster, Vector3 launchDirection, float timeoutSeconds = 0.85f,
            float safeDistance = 1.65f)
        {
            Restore();
            if (caster == null) return;
            _body = GetComponentInParent<Rigidbody>();
            _caster = caster;
            _launchDirection = launchDirection.sqrMagnitude > 0.001f
                ? launchDirection.normalized
                : caster.forward;
            _restoreAt = Time.fixedUnscaledTime + Mathf.Max(0.1f, timeoutSeconds);
            _safeDistance = Mathf.Max(0.5f, safeDistance);

            int launchCount = CopyColliders(_body != null ? _body.transform : transform, _launchColliders);
            int casterCount = CopyColliders(caster, _casterColliders);
            for (int launchIndex = 0; launchIndex < launchCount && _pairCount < MaximumPairs; launchIndex++)
            for (int casterIndex = 0; casterIndex < casterCount && _pairCount < MaximumPairs; casterIndex++)
            {
                Collider launch = _launchColliders[launchIndex];
                Collider owner = _casterColliders[casterIndex];
                if (launch == null || owner == null || launch == owner) continue;
                UnityEngine.Physics.IgnoreCollision(launch, owner, true);
                _pairLaunch[_pairCount] = launch;
                _pairCaster[_pairCount] = owner;
                _pairCount++;
            }
            _active = _pairCount > 0;
        }

        private int CopyColliders(Transform root, Collider[] destination)
        {
            _scratch.Clear();
            root.GetComponentsInChildren(false, _scratch);
            int count = Mathf.Min(destination.Length, _scratch.Count);
            for (int index = 0; index < count; index++) destination[index] = _scratch[index];
            return count;
        }

        private void FixedUpdate()
        {
            if (!_active) return;
            Vector3 delta = _caster != null ? transform.position - _caster.position : Vector3.zero;
            bool separated = delta.sqrMagnitude >= _safeDistance * _safeDistance;
            bool movingAway = _body == null || Vector3.Dot(_body.linearVelocity, _launchDirection) > -0.25f;
            if (Time.fixedUnscaledTime >= _restoreAt || (separated && movingAway)) Restore();
        }

        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();

        private void Restore()
        {
            for (int index = 0; index < _pairCount; index++)
            {
                Collider launch = _pairLaunch[index];
                Collider owner = _pairCaster[index];
                if (launch != null && owner != null) UnityEngine.Physics.IgnoreCollision(launch, owner, false);
                _pairLaunch[index] = null;
                _pairCaster[index] = null;
            }
            _pairCount = 0;
            _active = false;
        }
    }
}
