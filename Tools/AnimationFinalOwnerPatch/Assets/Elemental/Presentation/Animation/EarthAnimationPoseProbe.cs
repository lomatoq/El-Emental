using System;
using Elemental.Presentation.MotionMatching;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [Serializable]
    public struct EarthAnimationPoseSample
    {
        public string actor;
        public string scenario;
        public int frame;
        public EAMMRuntimeStatus eammStatus;
        public float eammWeight;
        public bool authoredTurn;
        public bool grounded;
        public float speed;
        public float headHeight;
        public float headPitchDegrees;
        public float neckLength;
        public float leftFootError;
        public float rightFootError;
        public float leftContactWeight;
        public float rightContactWeight;
        public float leftSurfaceSlopeDegrees;
        public float rightSurfaceSlopeDegrees;
        public int contactFrame;
        public float magicSampleTime;
        public float handConstraintWeight;
        public int finalGraphEvaluations;
        public int weightedContactPasses;
        public float leftSoleClearance;
        public float rightSoleClearance;
    }

    /// <summary>Optional final-pose observer for A/B captures; writes no bones or gameplay state.</summary>
    [DefaultExecutionOrder(3100)]
    public sealed class EarthAnimationPoseProbe : MonoBehaviour
    {
        private HumanoidCharacterPresentation _presentation;
        private Animator _animator;
        private EarthAnimationDriver _driver;
        private EAMMBasePoseBridge _bridge;
        private EarthFootContactController _feet;
        private Transform _head, _hips, _neck;
        private Vector3 _headForwardLocal;
        public EarthAnimationPoseSample Latest { get; private set; }
        public string Scenario { get; set; } = "production";

        private void Awake()
        {
            _presentation = GetComponent<HumanoidCharacterPresentation>();
            _animator = GetComponent<Animator>();
            _driver = GetComponent<EarthAnimationDriver>();
            _bridge = GetComponent<EAMMBasePoseBridge>();
            _feet = GetComponent<EarthFootContactController>();
            if (_animator == null || !_animator.isHuman) return;
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _neck = _animator.GetBoneTransform(HumanBodyBones.Neck);
            Quaternion rest = Quaternion.identity;
            SkeletonBone[] skeleton = _animator.avatar.humanDescription.skeleton;
            for (Transform node = _head; node != null && node != _animator.transform; node = node.parent)
            {
                Quaternion local = node.localRotation;
                for (int index = 0; index < skeleton.Length; index++)
                    if (skeleton[index].name == node.name) { local = skeleton[index].rotation; break; }
                rest = local * rest;
            }
            _headForwardLocal = Quaternion.Inverse(rest) * Vector3.forward;
        }

        private void LateUpdate()
        {
            if (_head == null || _hips == null || _driver == null || _feet == null) return;
            Vector3 up = _animator.transform.up;
            Latest = new EarthAnimationPoseSample
            {
                actor = name, scenario = Scenario, frame = Time.frameCount,
                eammStatus = _bridge != null ? _bridge.RuntimeStatus : EAMMRuntimeStatus.Disabled,
                eammWeight = _bridge != null ? _bridge.AppliedEammMasterWeight : 0f,
                authoredTurn = _bridge != null && _bridge.HasAuthoredTurnOwnership(),
                grounded = _driver.GetBool(Animator.StringToHash("Grounded")),
                speed = _presentation != null ? _presentation.FilteredSpeed : 0f,
                headHeight = Vector3.Dot(_head.position - _hips.position, up),
                headPitchDegrees = Mathf.Asin(Mathf.Clamp(Vector3.Dot(
                    _head.TransformDirection(_headForwardLocal).normalized, up), -1f, 1f)) * Mathf.Rad2Deg,
                neckLength = _neck != null ? Vector3.Distance(_neck.position, _head.position) : 0f,
                leftFootError = _feet.LeftAnchorErrorMeters,
                rightFootError = _feet.RightAnchorErrorMeters,
                leftContactWeight = _feet.LeftFootIkWeight,
                rightContactWeight = _feet.RightFootIkWeight,
                leftSurfaceSlopeDegrees = Vector3.Angle(up, _feet.LeftRawContactNormalWorld),
                rightSurfaceSlopeDegrees = Vector3.Angle(up, _feet.RightRawContactNormalWorld),
                contactFrame = _feet.LastContactEvaluationFrame,
                magicSampleTime = _driver.GetFloat(Animator.StringToHash("EarthMotionTime")),
                handConstraintWeight = _presentation != null ? _presentation.HandConstraintWeight : 0f,
                finalGraphEvaluations = _driver.FinalIkEvaluationCount,
                weightedContactPasses = _driver.FinalContactPassCount,
                leftSoleClearance = _feet.LeftSoleClearance,
                rightSoleClearance = _feet.RightSoleClearance
            };
        }
    }
}
