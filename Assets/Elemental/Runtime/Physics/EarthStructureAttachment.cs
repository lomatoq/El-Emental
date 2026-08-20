using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthStructureAttachment : MonoBehaviour
    {
        private readonly IEarthPhysicalTarget[] _targets = new IEarthPhysicalTarget[48];
        private EarthWall _child;
        private EarthPlatform _platformChild;
        private IEarthFractureSource _parent;
        private Vector3 _anchor;
        private FixedJoint _joint;
        private int _pendingBindTicks;
        private bool _protectJointDuringEmergence;

        public uint ParentStructureId => _parent != null ? _parent.StructureId : 0u;
        public EarthPhysicalTargetHandle SupportHandle { get; private set; }

        public void Configure(EarthWall child, IEarthFractureSource parent, Vector3 anchor)
        {
            Unsubscribe();
            _child = child;
            _platformChild = null;
            _parent = parent;
            _anchor = anchor;
            if (_parent != null) _parent.TargetsActivated += OnTargetsActivated;
            if (_parent != null && _parent.IsFractured) QueueSupportRebind();
        }

        public void Configure(EarthPlatform child, IEarthFractureSource parent, Vector3 anchor)
        {
            Unsubscribe();
            _child = null;
            _platformChild = child;
            _parent = parent;
            _anchor = anchor;
            if (_parent != null) _parent.TargetsActivated += OnTargetsActivated;
            if (_parent != null && _parent.IsFractured) QueueSupportRebind();
        }

        private void OnTargetsActivated(IEarthFractureSource source) => QueueSupportRebind();

        private void QueueSupportRebind()
        {
            // A fracture source announces its pieces in the activation tick. Most
            // sources are immediately queryable, but Unity can defer an active-state
            // or Rigidbody transition until the following physics step. Try now for
            // the strict same-tick path, then keep a short deterministic retry window.
            _pendingBindTicks = 3;
            if (TryBindToNearestSupport()) _pendingBindTicks = 0;
        }

        private void FixedUpdate()
        {
            if (_protectJointDuringEmergence && _joint != null && ChildEmergenceComplete)
            {
                _joint.breakForce = 4200f;
                _joint.breakTorque = 2800f;
                _protectJointDuringEmergence = false;
            }

            // The event is the zero-latency path. The state check is deliberately
            // retained as a safety net for pooled parents whose fracture callback
            // happened while this newly-added component was between enable phases.
            if (_pendingBindTicks <= 0)
            {
                if (_parent == null || !_parent.IsFractured || SupportHandle.IsValid) return;
                _pendingBindTicks = 3;
            }
            if (TryBindToNearestSupport())
            {
                _pendingBindTicks = 0;
                return;
            }

            _pendingBindTicks--;
            if (_pendingBindTicks == 0) BreakUnsupportedChild();
        }

        private bool TryBindToNearestSupport()
        {
            if (_parent == null || ChildObject == null) return false;
            int count = _parent.CopyActiveTargetsNonAlloc(_targets);
            IEarthPhysicalTarget nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                IEarthPhysicalTarget candidate = _targets[index];
                if (candidate == null || !candidate.IsEarthTargetValid || candidate.Body == null) continue;
                float distance = Vector3.SqrMagnitude(candidate.Body.worldCenterOfMass - _anchor);
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = candidate;
            }
            if (nearest == null) return false;
            SupportHandle = nearest.TargetHandle;
            if (_joint == null) _joint = ChildObject.AddComponent<FixedJoint>();
            _joint.autoConfigureConnectedAnchor = false;
            _joint.anchor = ChildObject.transform.InverseTransformPoint(_anchor);
            _joint.connectedBody = nearest.Body;
            _joint.connectedAnchor = nearest.Body.transform.InverseTransformPoint(_anchor);
            _protectJointDuringEmergence = !ChildEmergenceComplete;
            _joint.breakForce = _protectJointDuringEmergence ? float.PositiveInfinity : 4200f;
            _joint.breakTorque = _protectJointDuringEmergence ? float.PositiveInfinity : 2800f;
            return true;
        }

        private void BreakUnsupportedChild()
        {
            SupportHandle = default;
            var impact = new EarthStructureImpact(
                _anchor,
                -ChildSurfaceUp,
                1800f,
                EarthStructureImpactKind.Construction,
                ParentStructureId);
            ApplyChildImpact(in impact);
        }

        private void OnJointBreak(float force)
        {
            SupportHandle = default;
            var impact = new EarthStructureImpact(
                _anchor,
                -ChildSurfaceUp,
                Mathf.Max(1800f, force),
                EarthStructureImpactKind.Construction,
                ParentStructureId);
            ApplyChildImpact(in impact);
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();
        private void Unsubscribe()
        {
            if (_parent != null) _parent.TargetsActivated -= OnTargetsActivated;
            _pendingBindTicks = 0;
            _protectJointDuringEmergence = false;
            SupportHandle = default;
            if (_joint != null) _joint.connectedBody = null;
            _parent = null;
        }

        private GameObject ChildObject => _child != null
            ? _child.gameObject
            : _platformChild != null ? _platformChild.gameObject : null;

        private bool ChildEmergenceComplete => _child != null
            ? _child.IsEmergenceComplete
            : _platformChild != null && _platformChild.IsEmergenceComplete;

        private Vector3 ChildSurfaceUp => _child != null
            ? _child.SurfaceUp
            : _platformChild != null ? _platformChild.SurfaceUp : Vector3.up;

        private void ApplyChildImpact(in EarthStructureImpact impact)
        {
            if (_child != null) _child.ApplyEarthImpact(in impact);
            else if (_platformChild != null) _platformChild.ApplyEarthImpact(in impact);
        }
    }
}
