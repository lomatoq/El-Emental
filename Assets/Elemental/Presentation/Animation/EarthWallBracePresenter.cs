using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    /// <summary>Allocation-free wall contacts, evaluated by the existing final hand owner.</summary>
    internal sealed class EarthWallBracePresenter
    {
        private static readonly ProfilerMarker Marker = new("Elemental.Character.WallBrace");
        private readonly RaycastHit[] _hits = new RaycastHit[12];
        private EarthWallBraceState _state;
        private Animator _animator;
        private Transform _leftShoulder, _rightShoulder, _spine;
        private float _leftReach, _rightReach;
        private Contact _left, _right;
        private struct Contact
        {
            public Collider Collider;
            public Vector3 LocalPoint, LocalNormal;
            public bool Alive => Collider != null && Collider.enabled && Collider.gameObject.activeInHierarchy;
            public Vector3 Point => Collider.transform.TransformPoint(LocalPoint);
            public Vector3 Normal => Collider.transform.TransformDirection(LocalNormal).normalized;
        }
        public float Weight => _state.Weight;
        public float LeftSubmittedWeight { get; private set; }
        public float RightSubmittedWeight { get; private set; }
        public Vector2 ArmReach => new Vector2(_leftReach, _rightReach);
        public Vector2 ContactDistance => new Vector2(
            _left.Alive ? Vector3.Distance(_leftShoulder.position, _left.Point) : -1f,
            _right.Alive ? Vector3.Distance(_rightShoulder.position, _right.Point) : -1f);

        public void SubmitRigTargets(PlanetMotor motor, Transform leftTarget, Transform rightTarget,
            EarthAnimationRigBridge bridge)
        {
            LeftSubmittedWeight = WriteTarget(leftTarget, _leftShoulder, in _left, motor, _leftReach);
            RightSubmittedWeight = WriteTarget(rightTarget, _rightShoulder, in _right, motor, _rightReach);
            bridge.SetWallContactWeight(_state.Weight * .95f, LeftSubmittedWeight, RightSubmittedWeight,
                Quaternion.LookRotation(motor.FacingForward, motor.LocalUp));
            bridge.PrepareForEvaluation();
        }

        private static float WriteTarget(Transform target, Transform shoulder, in Contact contact, PlanetMotor motor, float reach)
        {
            if (target == null || shoulder == null || !contact.Alive ||
                Vector3.Distance(shoulder.position, contact.Point) > reach) return 0f;
            target.SetPositionAndRotation(contact.Point + contact.Normal * .025f,
                Quaternion.LookRotation(motor.FacingForward, motor.LocalUp));
            return 1f;
        }

        public void Step(Animator animator, PlanetMotor motor, bool permitted, float dt)
        {
            using (Marker.Auto())
            {
                if (animator == null || !animator.isHuman || motor == null) { _state = default; return; }
                if (_animator != animator)
                {
                    _animator = animator;
                    _leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    _rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                    _leftReach = MeasureReach(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
                    _rightReach = MeasureReach(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
                }
                Vector3 up = motor.LocalUp;
                Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
                bool allowed = permitted && motor.HasStableSupport && !motor.IsMantling &&
                               !motor.LastCommand.JumpPressed && !motor.HasDirectedExternalMotion;
                bool leftFound = false, rightFound = false;
                if (allowed && motor.LastCommand.Move.y > .25f)
                {
                    leftFound = Probe(motor, _leftShoulder, forward, up, _leftReach, ref _left);
                    rightFound = Probe(motor, _rightShoulder, forward, up, _rightReach, ref _right);
                }
                Vector3 velocity = motor.Body != null ? motor.Body.linearVelocity : Vector3.zero;
                if (motor.CurrentSupportFrame.IsValid)
                {
                    var v = motor.CurrentSupportFrame.ContactPointVelocity;
                    velocity -= new Vector3(v.x, v.y, v.z);
                }
                _state.Step(allowed, leftFound || rightFound, motor.LastCommand.Move.y,
                    Vector3.Dot(velocity, forward), dt);
                if (_state.Weight <= .001f) { _left = default; _right = default; }
            }
        }

        private static float MeasureReach(Animator animator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand)
        {
            Transform a = animator.GetBoneTransform(upper), b = animator.GetBoneTransform(lower), c = animator.GetBoneTransform(hand);
            return a != null && b != null && c != null
                ? .92f * (Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position)) : 0f;
        }

        private bool Probe(PlanetMotor motor, Transform shoulder, Vector3 forward, Vector3 up, float reach, ref Contact contact)
        {
            if (shoulder == null) return false;
            // Probe from the chest plane rather than the last solved hand to avoid IK feedback.
            Vector3 origin = shoulder.position - up * .08f;
            // Candidate envelope includes bounded torso lean. Individual hands still
            // require actual anatomical reach before their rig weights are admitted.
            float candidateReach = reach + .18f;
            float rayLength = Mathf.Sqrt(Mathf.Max(0f, candidateReach * candidateReach - .08f * .08f));
            int count = Physics.RaycastNonAlloc(origin, forward, _hits, rayLength,
                motor.GroundMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            RaycastHit chosen = default;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(motor.transform) ||
                    Mathf.Abs(Vector3.Dot(hit.normal, up)) > .45f ||
                    Vector3.Dot(hit.normal, -forward) < .65f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                chosen = hit;
            }
            if (chosen.collider == null) { contact = default; return false; }
            // Retain the captured point in collider space while reachable, including moving walls.
            if (!contact.Alive || contact.Collider != chosen.collider ||
                Vector3.Distance(contact.Point, chosen.point) > .12f)
            {
                contact.Collider = chosen.collider;
                contact.LocalPoint = chosen.collider.transform.InverseTransformPoint(chosen.point);
                contact.LocalNormal = chosen.collider.transform.InverseTransformDirection(chosen.normal);
            }
            return true;
        }

        public void Apply(Animator animator, PlanetMotor motor)
        {
            if (_state.Weight <= .001f || motor == null || motor.IsMantling) return;
            ApplyHand(animator, AvatarIKGoal.LeftHand, _leftShoulder, in _left, _leftReach);
            ApplyHand(animator, AvatarIKGoal.RightHand, _rightShoulder, in _right, _rightReach);
            // Small local spine lean only; no root/body-position offset or head aim override.
            if (_spine != null)
                animator.SetBoneLocalRotation(HumanBodyBones.Spine,
                    _spine.localRotation * Quaternion.AngleAxis(3f * _state.Weight,
                        _spine.InverseTransformDirection(Vector3.Cross(motor.LocalUp, motor.FacingForward).normalized)));
        }

        private void ApplyHand(Animator animator, AvatarIKGoal goal, Transform shoulder, in Contact contact, float reach)
        {
            bool reachable = contact.Alive && shoulder != null &&
                             Vector3.Distance(shoulder.position, contact.Point) <= reach;
            float weight = reachable ? _state.Weight * .85f : 0f;
            animator.SetIKPositionWeight(goal, weight);
            // Preserve authored wrist orientation; positional contact must not twist the forearm.
            animator.SetIKRotationWeight(goal, 0f);
            if (reachable) animator.SetIKPosition(goal, contact.Point + contact.Normal * .025f);
        }
    }
}
