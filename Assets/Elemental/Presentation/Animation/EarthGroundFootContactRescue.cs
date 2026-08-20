using System;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    internal static class EarthGroundFootContactRescueBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EarthGroundFootContactInstaller>(
                    FindObjectsInactive.Include) != null)
                return;
            var host = new GameObject("Earth Ground Foot Contact Installer")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<EarthGroundFootContactInstaller>();
        }
    }

    internal sealed class EarthGroundFootContactInstaller : MonoBehaviour
    {
        private float _nextScanAt;

        private void Update()
        {
            if (Time.unscaledTime < _nextScanAt) return;
            _nextScanAt = Time.unscaledTime + 1f;
            EarthCharacterPoseController[] controllers =
                UnityEngine.Object.FindObjectsByType<EarthCharacterPoseController>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < controllers.Length; index++)
            {
                EarthCharacterPoseController controller = controllers[index];
                if (controller != null &&
                    controller.GetComponent<EarthGroundFootContactRescue>() == null)
                    controller.gameObject.AddComponent<EarthGroundFootContactRescue>();
            }
        }
    }

    /// <summary>
    /// Late, phase-aware contact correction for normal planet and platform movement.
    /// It deliberately does not world-lock the swing foot: the planted foot follows
    /// the real collider while the other remains owned by the authored gait.
    /// </summary>
    [DefaultExecutionOrder(2080)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator), typeof(EarthCharacterPoseController))]
    public sealed class EarthGroundFootContactRescue : MonoBehaviour
    {
        private const int HitCapacity = 12;
        private readonly RaycastHit[] _leftHits = new RaycastHit[HitCapacity];
        private readonly RaycastHit[] _rightHits = new RaycastHit[HitCapacity];

        private Animator _animator;
        private EarthCharacterPoseController _pose;
        private PlanetMotor _motor;
        private Rigidbody _rootBody;
        private ActiveRagdollPuppet _puppet;
        private EarthSurfController _surf;
        private Transform _leftFoot;
        private Transform _rightFoot;
        private Transform _leftUpperLeg;
        private Transform _rightUpperLeg;
        private Vector3 _previousLeft;
        private Vector3 _previousRight;
        private Vector3 _leftKneeDirection;
        private Vector3 _rightKneeDirection;
        private float _leftWeight;
        private float _rightWeight;
        private float _pelvisOffset;
        private float _pelvisVelocity;
        private bool _sampled;

        public float LeftContactWeight => _leftWeight;
        public float RightContactWeight => _rightWeight;
        public float PelvisOffset => _pelvisOffset;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _pose = GetComponent<EarthCharacterPoseController>();
            _motor = GetComponentInParent<PlanetMotor>();
            _rootBody = GetComponentInParent<Rigidbody>();
            _puppet = GetComponentInParent<ActiveRagdollPuppet>();
            _surf = GetComponentInParent<EarthSurfController>();
            ResolveBones();
        }

        private void OnEnable()
        {
            _sampled = false;
            _leftWeight = _rightWeight = 0f;
            _pelvisOffset = _pelvisVelocity = 0f;
        }

        private void ResolveBones()
        {
            if (_animator == null || !_animator.isHuman) return;
            _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _leftUpperLeg = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _rightUpperLeg = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null || !_animator.isHuman ||
                _motor == null || _leftFoot == null || _rightFoot == null)
                return;

            bool surfing = _surf != null && _surf.IsActive;
            bool supported = _motor.HasStableSupport && !surfing;
            if (!supported)
            {
                FadeOutContacts();
                SampleAnimatedFeet();
                return;
            }

            Vector3 up = _motor.LocalUp.sqrMagnitude > 0.5f
                ? _motor.LocalUp.normalized
                : transform.up;
            Vector3 forward = Vector3.ProjectOnPlane(_motor.FacingForward, up).normalized;
            if (forward.sqrMagnitude < 0.1f)
                forward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            if (right.sqrMagnitude < 0.1f) right = transform.right;

            float delta = Mathf.Max(0.0001f, Time.deltaTime);
            Vector3 leftAnimated = _leftFoot.position;
            Vector3 rightAnimated = _rightFoot.position;
            if (!_sampled)
            {
                _previousLeft = leftAnimated;
                _previousRight = rightAnimated;
                _sampled = true;
            }

            Vector3 supportVelocity = SupportVelocity();
            Vector3 leftVelocity = (leftAnimated - _previousLeft) / delta - supportVelocity;
            Vector3 rightVelocity = (rightAnimated - _previousRight) / delta - supportVelocity;
            _previousLeft = leftAnimated;
            _previousRight = rightAnimated;

            Vector3 rootVelocity = _rootBody != null
                ? _rootBody.linearVelocity - supportVelocity
                : Vector3.zero;
            float bodySpeed = Vector3.ProjectOnPlane(rootVelocity, up).magnitude;
            float moveIntent = math.length(_motor.LastCommand.Move);
            float brace = _pose != null ? _pose.CurrentIntent.Brace01 : 0f;
            float authoredWidth = _pose != null ? _pose.CurrentIntent.StanceWidth01 : 0f;
            float minimumSeparation = Mathf.Lerp(0.30f, 0.25f,
                Mathf.InverseLerp(0.5f, 7.5f, bodySpeed));
            minimumSeparation += authoredWidth * 0.16f + brace * 0.055f;

            Vector3 midpoint = (leftAnimated + rightAnimated) * 0.5f;
            float currentSeparation = Mathf.Abs(Vector3.Dot(rightAnimated - leftAnimated, right));
            float halfSeparation = Mathf.Max(currentSeparation * 0.5f, minimumSeparation * 0.5f);
            Vector3 leftGuess = midpoint - right * halfSeparation;
            Vector3 rightGuess = midpoint + right * halfSeparation;

            bool leftHit = TryProbe(
                leftGuess, up, _leftHits, out Vector3 leftPoint, out Vector3 leftNormal);
            bool rightHit = TryProbe(
                rightGuess, up, _rightHits, out Vector3 rightPoint, out Vector3 rightNormal);

            float leftTargetWeight = ContactTarget(
                leftHit,
                leftAnimated,
                leftPoint,
                leftVelocity,
                up,
                bodySpeed,
                moveIntent,
                rightAnimated,
                rightPoint,
                rightHit);
            float rightTargetWeight = ContactTarget(
                rightHit,
                rightAnimated,
                rightPoint,
                rightVelocity,
                up,
                bodySpeed,
                moveIntent,
                leftAnimated,
                leftPoint,
                leftHit);

            // At least one foot must remain authoritative while standing or moving
            // slowly, otherwise both heuristic swing weights can dip on the same
            // retargeting frame and the whole body visibly pops upward.
            if (bodySpeed < 1.15f && Mathf.Max(leftTargetWeight, rightTargetWeight) < 0.72f)
            {
                if (leftHit && rightHit)
                {
                    float leftHeight = Mathf.Abs(Vector3.Dot(leftAnimated - leftPoint, up));
                    float rightHeight = Mathf.Abs(Vector3.Dot(rightAnimated - rightPoint, up));
                    if (leftHeight <= rightHeight) leftTargetWeight = 0.92f;
                    else rightTargetWeight = 0.92f;
                }
                else if (leftHit) leftTargetWeight = 0.92f;
                else if (rightHit) rightTargetWeight = 0.92f;
            }

            _leftWeight = ApproachWeight(_leftWeight, leftTargetWeight, delta);
            _rightWeight = ApproachWeight(_rightWeight, rightTargetWeight, delta);
            if (leftHit) ApplyFoot(AvatarIKGoal.LeftFoot, _leftFoot, leftPoint, leftNormal, _leftWeight);
            else ClearFoot(AvatarIKGoal.LeftFoot);
            if (rightHit) ApplyFoot(AvatarIKGoal.RightFoot, _rightFoot, rightPoint, rightNormal, _rightWeight);
            else ClearFoot(AvatarIKGoal.RightFoot);
            ApplyKnees(forward, right, up);
            ApplyPelvis(
                up,
                leftAnimated,
                leftPoint,
                leftHit,
                rightAnimated,
                rightPoint,
                rightHit,
                delta);
        }

        private float ContactTarget(
            bool hasHit,
            Vector3 animated,
            Vector3 point,
            Vector3 footVelocity,
            Vector3 up,
            float bodySpeed,
            float moveIntent,
            Vector3 otherAnimated,
            Vector3 otherPoint,
            bool otherHasHit)
        {
            if (!hasHit) return 0f;
            float height = Vector3.Dot(animated - point, up);
            float verticalSpeed = Vector3.Dot(footVelocity, up);
            float tangentSpeed = Vector3.ProjectOnPlane(footVelocity, up).magnitude;
            float proximity = 1f - Mathf.InverseLerp(0.055f, 0.25f, Mathf.Abs(height));
            float downward = 1f - Mathf.InverseLerp(0.18f, 1.8f, Mathf.Max(0f, verticalSpeed));
            float slowFoot = 1f - Mathf.InverseLerp(0.65f, 3.4f, tangentSpeed);
            float plant = Mathf.Clamp01(proximity * 0.52f + downward * 0.26f + slowFoot * 0.22f);

            if (bodySpeed < 0.28f && moveIntent < 0.08f)
                plant = Mathf.Max(plant, 0.97f);
            else if (otherHasHit)
            {
                float otherHeight = Mathf.Abs(Vector3.Dot(otherAnimated - otherPoint, up));
                if (Mathf.Abs(height) <= otherHeight - 0.025f)
                    plant = Mathf.Max(plant, 0.88f);
            }

            // Swing feet retain a light orientation correction but never get pinned
            // hard enough to erase the authored stride arc.
            return Mathf.Lerp(0.08f, 1f, plant);
        }

        private bool TryProbe(
            Vector3 animated,
            Vector3 up,
            RaycastHit[] hits,
            out Vector3 point,
            out Vector3 normal)
        {
            Vector3 origin = animated + up * 0.46f;
            int count = UnityEngine.Physics.RaycastNonAlloc(
                origin,
                -up,
                hits,
                1.35f,
                _motor.GroundMask,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            RaycastHit selected = default;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.collider == null || hit.distance >= nearest) continue;
                if (_rootBody != null &&
                    (hit.rigidbody == _rootBody || hit.collider.transform.IsChildOf(_rootBody.transform)))
                    continue;
                if (_puppet != null && _puppet.OwnsCollider(hit.collider)) continue;
                if (Vector3.Dot(hit.normal, up) < -0.05f) continue;
                nearest = hit.distance;
                selected = hit;
            }

            if (selected.collider == null)
            {
                point = default;
                normal = up;
                return false;
            }
            normal = selected.normal.normalized;
            point = selected.point + normal * 0.032f;
            return true;
        }

        private void ApplyFoot(
            AvatarIKGoal goal,
            Transform animatedFoot,
            Vector3 point,
            Vector3 normal,
            float weight)
        {
            float safeWeight = Mathf.Clamp01(weight);
            _animator.SetIKPositionWeight(goal, safeWeight);
            _animator.SetIKRotationWeight(goal, safeWeight);
            _animator.SetIKPosition(goal, point);
            Vector3 footForward = Vector3.ProjectOnPlane(animatedFoot.forward, normal).normalized;
            if (footForward.sqrMagnitude < 0.1f)
                footForward = Vector3.ProjectOnPlane(transform.forward, normal).normalized;
            _animator.SetIKRotation(goal, Quaternion.LookRotation(footForward, normal));
        }

        private void ClearFoot(AvatarIKGoal goal)
        {
            _animator.SetIKPositionWeight(goal, 0f);
            _animator.SetIKRotationWeight(goal, 0f);
        }

        private void ApplyKnees(Vector3 forward, Vector3 right, Vector3 up)
        {
            float leftApplied = _leftWeight * 0.88f;
            float rightApplied = _rightWeight * 0.88f;
            _animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, leftApplied);
            _animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, rightApplied);
            if (_leftUpperLeg != null && leftApplied > 0.001f)
            {
                Vector3 desiredDirection = (forward * 0.82f - right * 0.16f + up * 0.06f).normalized;
                _leftKneeDirection = Vector3.Slerp(
                    _leftKneeDirection.sqrMagnitude > 0.1f ? _leftKneeDirection : desiredDirection,
                    desiredDirection,
                    1f - Mathf.Exp(-14f * Time.deltaTime));
                _animator.SetIKHintPosition(
                    AvatarIKHint.LeftKnee,
                    _leftUpperLeg.position + _leftKneeDirection * 0.43f);
            }
            if (_rightUpperLeg != null && rightApplied > 0.001f)
            {
                Vector3 desiredDirection = (forward * 0.82f + right * 0.16f + up * 0.06f).normalized;
                _rightKneeDirection = Vector3.Slerp(
                    _rightKneeDirection.sqrMagnitude > 0.1f ? _rightKneeDirection : desiredDirection,
                    desiredDirection,
                    1f - Mathf.Exp(-14f * Time.deltaTime));
                _animator.SetIKHintPosition(
                    AvatarIKHint.RightKnee,
                    _rightUpperLeg.position + _rightKneeDirection * 0.43f);
            }
        }

        private void ApplyPelvis(
            Vector3 up,
            Vector3 leftAnimated,
            Vector3 leftPoint,
            bool leftHit,
            Vector3 rightAnimated,
            Vector3 rightPoint,
            bool rightHit,
            float delta)
        {
            float target = 0f;
            bool hasWeightedContact = false;
            if (leftHit && _leftWeight > 0.42f)
            {
                target = Vector3.Dot(leftPoint - leftAnimated, up) * _leftWeight;
                hasWeightedContact = true;
            }
            if (rightHit && _rightWeight > 0.42f)
            {
                float rightError = Vector3.Dot(rightPoint - rightAnimated, up) * _rightWeight;
                target = hasWeightedContact ? Mathf.Min(target, rightError) : rightError;
                hasWeightedContact = true;
            }
            target = hasWeightedContact ? Mathf.Clamp(target, -0.12f, 0.026f) : 0f;
            _pelvisOffset = Mathf.SmoothDamp(
                _pelvisOffset,
                target,
                ref _pelvisVelocity,
                0.075f,
                2.8f,
                delta);
            _animator.bodyPosition += up * _pelvisOffset;
        }

        private void FadeOutContacts()
        {
            float delta = Mathf.Max(0.0001f, Time.deltaTime);
            _leftWeight = Mathf.MoveTowards(_leftWeight, 0f, delta / 0.065f);
            _rightWeight = Mathf.MoveTowards(_rightWeight, 0f, delta / 0.065f);
            _pelvisOffset = Mathf.SmoothDamp(
                _pelvisOffset, 0f, ref _pelvisVelocity, 0.06f, 3f, delta);
            ClearFoot(AvatarIKGoal.LeftFoot);
            ClearFoot(AvatarIKGoal.RightFoot);
            _animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, 0f);
            _animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, 0f);
        }

        private void SampleAnimatedFeet()
        {
            if (_leftFoot == null || _rightFoot == null) return;
            _previousLeft = _leftFoot.position;
            _previousRight = _rightFoot.position;
            _sampled = true;
        }

        private Vector3 SupportVelocity()
        {
            if (!_motor.CurrentSupportFrame.IsValid) return Vector3.zero;
            float3 velocity = _motor.CurrentSupportFrame.ContactPointVelocity;
            return new Vector3(velocity.x, velocity.y, velocity.z);
        }

        private static float ApproachWeight(float current, float target, float delta)
        {
            float seconds = target > current ? 0.075f : 0.11f;
            return Mathf.MoveTowards(current, target, delta / seconds);
        }
    }
}
