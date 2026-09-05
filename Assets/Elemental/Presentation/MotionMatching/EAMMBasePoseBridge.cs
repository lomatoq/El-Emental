using System;
using System.Collections.Generic;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using MotionMatching;
using UnityEngine;

namespace Elemental.Presentation.MotionMatching
{
    /// <summary>
    /// Places the JLPM pose ahead of the existing Animator IK pass. It never
    /// applies JLPM root motion; authored actions/ragdoll bypass the base pose.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(120)]
    public sealed class EAMMBasePoseBridge : MonoBehaviour
    {
        private const float IdleKneeHandoffSeconds = 0.14f;

        private static readonly HumanBodyBones[] Bones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.RightToes
        };

        [SerializeField] private Animator animator;
        [SerializeField] private MotionMatchingController source;
        [SerializeField] private HumanoidCharacterPresentation presentation;
        [SerializeField] private HumanoidRagdollRig ragdoll;
        [SerializeField] private EAMMRuntimeProfile profile;
        [SerializeField] private EarthRetargetBindPose bindPose;
        [SerializeField] private bool player = true;

        private Transform[] _sourceBones;
        private Quaternion[] _sourceTPose;
        private Quaternion[] _targetTPose;
        private Transform[] _targetBones;
        private Quaternion[] _previousTargets;
        private Quaternion[] _candidateTargets;
        private Transform[] _candidateHierarchy;
        private int[] _candidateParentIndices;
        private int[] _candidateBoneIndices;
        private Matrix4x4[] _candidateWorldMatrices;
        private int _candidateHipsIndex;
        private int _candidateHeadIndex;
        private int _candidateLeftFootIndex;
        private int _candidateRightFootIndex;
        private EarthAnimationGraph _animationGraph;
        private EarthAnimationDriver _animationDriver;
        private EarthTransitionDirector _transitionDirector;
        private EarthAuthoredActionId _previousAction;
        private bool _previousAuthoredTurn;
        private bool _poseSane;
        private bool _visiblePoseRejected;
        private bool _ready;
        private bool _initialized;
        private bool _authoredIdleKneeOwnership;
        private bool _hasLocomotionQueryState;
        private bool _previousLocomotionQuery;
        private UnityEngine.Object _externalBasePoseOwner;
        private float _idleKneeEammWeight = 1f;
        private int _stablePoseFrames;
        private int _visibleInvalidFrames;

        public bool IsReady => _ready;
        public float AppliedEammMasterWeight => _animationGraph?.EammMasterWeight ?? 0f;
        public bool IsInitialized => _initialized;
        public int StablePoseFrames => _stablePoseFrames;
        public bool HasAnimationGraph => _animationGraph != null && _animationGraph.IsCreated;
        public string InitializationStatus { get; private set; } = "not-started";
        public string PoseRejectionReason { get; private set; } = string.Empty;
        public EAMMRuntimeStatus RuntimeStatus { get; private set; } = EAMMRuntimeStatus.Disabled;
        public float SourceHeadHeight { get; private set; }
        public float SourceLeftFootHeight { get; private set; }
        public float SourceRightFootHeight { get; private set; }
        public float LastVisibleHeadHeight { get; private set; }
        public float LastVisibleLeftFootHeight { get; private set; }
        public float LastVisibleRightFootHeight { get; private set; }
        public float CandidateHeadHeight { get; private set; }
        public float CandidateLeftFootHeight { get; private set; }
        public float CandidateRightFootHeight { get; private set; }
        public bool OwnsGameplayRoot => false;
        public bool OwnsFootIk => false;
        public bool UsesAuthoredIdleKnees => _authoredIdleKneeOwnership;
        public float AppliedIdleKneeEammWeight => _idleKneeEammWeight;
        public int SourcePoseFrame => source != null ? source.CurrentFrame : -1;
        public bool HasLocomotionQuery => QueryAdapter != null && QueryAdapter.HasLocomotionQuery;
        public bool IsLocomotionQuery => HasLocomotionQuery && QueryAdapter.LocomotionQuery;
        public bool HasExternalBasePoseOverride => _externalBasePoseOwner != null;

        private PlanetEAMMCharacterController QueryAdapter => source != null
            ? source.CharacterController as PlanetEAMMCharacterController
            : null;

        public void Configure(
            MotionMatchingController configuredSource,
            EAMMRuntimeProfile configuredProfile,
            EarthRetargetBindPose configuredBindPose,
            bool isPlayer)
        {
            if (isActiveAndEnabled && source != null)
                source.OnSkeletonTransformUpdated -= HandleSourcePose;
            source = configuredSource;
            profile = configuredProfile;
            bindPose = configuredBindPose;
            player = isPlayer;
            _initialized = false;
            _ready = false;
            _visiblePoseRejected = false;
            _visibleInvalidFrames = 0;
            _authoredIdleKneeOwnership = false;
            _hasLocomotionQueryState = false;
            _idleKneeEammWeight = 1f;
            if (isActiveAndEnabled && source != null)
                source.OnSkeletonTransformUpdated += HandleSourcePose;
        }

        /// <summary>
        /// Temporarily keeps the authored Animator playable graph alive while an
        /// external diagnostic source supplies the Humanoid base pose. The graph
        /// must remain alive so Animator IK callbacks and the final foot-contact
        /// pass continue to run. Ownership is explicit so one preview cannot
        /// release another preview's override.
        /// </summary>
        public bool TryAcquireExternalBasePoseOverride(UnityEngine.Object owner)
        {
            if (owner == null) return false;
            if (_externalBasePoseOwner != null && _externalBasePoseOwner != owner)
                return false;
            _externalBasePoseOwner = owner;
            _animationGraph?.SetEammMasterWeight(0f);
            presentation?.FootContactController?.ClearBasePoseContactMetadata();
            return true;
        }

        public void ReleaseExternalBasePoseOverride(UnityEngine.Object owner)
        {
            if (owner == null || _externalBasePoseOwner != owner) return;
            _externalBasePoseOwner = null;
            presentation?.FootContactController?.ClearBasePoseContactMetadata();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (presentation == null) presentation = GetComponent<HumanoidCharacterPresentation>();
            if (ragdoll == null) ragdoll = GetComponent<HumanoidRagdollRig>();
            if (_transitionDirector == null) _transitionDirector = GetComponent<EarthTransitionDirector>();
            if (_animationDriver == null) _animationDriver = GetComponent<EarthAnimationDriver>();
            if (_animationDriver == null) _animationDriver = gameObject.AddComponent<EarthAnimationDriver>();
            _animationDriver.Configure(animator);
        }

        private void OnEnable()
        {
            if (source != null) source.OnSkeletonTransformUpdated += HandleSourcePose;
            SubscribeTransitionDirector();
        }

        private void Start()
        {
            TryInitialize();
        }

        private void OnDisable()
        {
            if (source != null) source.OnSkeletonTransformUpdated -= HandleSourcePose;
            presentation?.FootContactController?.ClearBasePoseContactMetadata();
            UnsubscribeTransitionDirector();
            DisposeGraph();
        }

        private void OnDestroy() => DisposeGraph();

        private void Update()
        {
            if (!_initialized) TryInitialize();
            if (!_initialized || _animationGraph == null) return;
            UpdateLocomotionQueryOwnership();
            _idleKneeEammWeight = StepIdleKneeEammWeight(
                _idleKneeEammWeight,
                _authoredIdleKneeOwnership,
                Time.deltaTime);
            // Idle databases do not guarantee a skeleton-updated event every
            // rendered frame. Sample explicitly so the stable-pose gate can
            // complete without requiring movement input.
            HandleSourcePose();

            bool featureEnabled = profile != null &&
                                  (player ? profile.EnabledForPlayer : profile.EnabledForBots);
            bool authoredLocomotion = presentation == null ||
                                      presentation.CurrentAuthoredAction is EarthAuthoredActionId.None or
                                          EarthAuthoredActionId.Locomotion;
            bool authoredTurn = HasAuthoredTurnOwnership();
            authoredLocomotion &= !authoredTurn;
            if (authoredTurn != _previousAuthoredTurn)
            {
                _animationGraph.RequestInertialization(0.12f);
                _previousAuthoredTurn = authoredTurn;
            }
            bool ragdollActive = ragdoll != null &&
                                 (ragdoll.IsRagdollActive || ragdoll.IsRecoveringToAnimation);
            _animationGraph.SetEammMasterWeight(
                !HasExternalBasePoseOverride && _ready && featureEnabled &&
                authoredLocomotion && !ragdollActive && _poseSane
                ? profile.BasePoseWeight
                : 0f);
            bool eammActive = _animationGraph.EammMasterWeight > 0f;
            ResolveSourcePoseFootContacts(out bool sourceLeftContact, out bool sourceRightContact);
            bool leftFootPlanted = eammActive
                ? sourceLeftContact
                : presentation != null && presentation.FootContactController != null &&
                  presentation.FootContactController.LeftFootLocked;
            bool rightFootPlanted = eammActive
                ? sourceRightContact
                : presentation != null && presentation.FootContactController != null &&
                  presentation.FootContactController.RightFootLocked;
            _animationGraph.SetFootContacts(leftFootPlanted, rightFootPlanted);
            EarthFootContactController footContacts = presentation != null
                ? presentation.FootContactController
                : null;
            if (eammActive)
            {
                float phase = ResolveSourcePhase01();
                footContacts?.SetBasePoseContactMetadata(
                    phase,
                    Mathf.Repeat(phase + 0.5f, 1f),
                    sourceLeftContact,
                    sourceRightContact);
            }
            else
            {
                footContacts?.ClearBasePoseContactMetadata();
            }

            EarthAuthoredActionId currentAction = presentation != null
                ? presentation.CurrentAuthoredAction
                : EarthAuthoredActionId.None;
            if (currentAction != _previousAction)
            {
                _animationGraph.RequestInertialization(ResolveActionTransitionSeconds(currentAction));
                _previousAction = currentAction;
            }
        }

        private void LateUpdate()
        {
            // Authored casts/rolls legitimately leave the upright locomotion
            // envelope. They must not permanently reject the EAMM database.
            if (!_ready || !_poseSane || _visiblePoseRejected || animator == null ||
                AppliedEammMasterWeight <= 0f) return;
            if (HasUprightLocomotionPosture(out string reason))
            {
                _visibleInvalidFrames = 0;
                return;
            }

            // The first evaluated PlayableGraph frame can briefly expose the
            // Animator's pre-graph pose. A one-frame 1 mm threshold miss used
            // to permanently disable EAMM for the player even though the next
            // frame was fully upright. Reject only a sustained visible failure;
            // real folded/inverted retargets still fail closed within 3 frames.
            _visibleInvalidFrames++;
            if (_visibleInvalidFrames < 3) return;

            // The source database can be numerically valid while its hierarchy
            // basis is still incompatible with the visible Humanoid. Never let
            // that failure masquerade as an Active pose: fail closed to the
            // authored controller and clear any contact state derived from the
            // rejected frame.
            _visiblePoseRejected = true;
            presentation?.FootContactController?.InvalidateBasePose();
            RejectPose(reason);
        }

        public bool HasAuthoredTurnOwnership()
        {
            if (_animationDriver == null || !_animationDriver.IsUsable) return false;
            int turn = Animator.StringToHash("Base Layer.Turn In Place");
            return (_transitionDirector != null &&
                    _transitionDirector.ActiveState == EarthMotionStateId.TurnInPlace) ||
                   _animationDriver.GetCurrentAnimatorStateInfo(0).fullPathHash == turn ||
                   (_animationDriver.IsInTransition(0) &&
                    _animationDriver.GetNextAnimatorStateInfo(0).fullPathHash == turn);
        }

        public bool TryGetBaseFootPosition(bool left, out Vector3 position)
        {
            int index = left ? _candidateLeftFootIndex : _candidateRightFootIndex;
            if (!_ready || !_poseSane || AppliedEammMasterWeight <= 0f ||
                _idleKneeEammWeight < 0.999f ||
                _candidateWorldMatrices == null || index < 0 || index >= _candidateWorldMatrices.Length)
            { position = default; return false; }
            position = _candidateWorldMatrices[index].MultiplyPoint3x4(Vector3.zero);
            return true;
        }

        private void TryInitialize()
        {
            if (_initialized) return;
            if (animator == null)
            {
                InitializationStatus = "missing-animator";
                return;
            }
            if (animator.runtimeAnimatorController == null)
            {
                InitializationStatus = "missing-runtime-controller";
                return;
            }
            // The authored graph also owns landing pose strength. Its fallback
            // must remain available when EAMM data/calibration is unavailable.
            EnsureAuthoredGraph();
            if (source == null)
            {
                InitializationStatus = "missing-source";
                return;
            }
            if (source.MMData == null)
            {
                InitializationStatus = "missing-database";
                return;
            }
            if (bindPose == null)
            {
                InitializationStatus = "missing-baked-bind-pose";
                RuntimeStatus = EAMMRuntimeStatus.MissingCalibration;
                return;
            }
            if (!bindPose.ValidateAgainst(source.MMData, out string bindReason))
            {
                InitializationStatus = bindReason;
                RuntimeStatus = EAMMRuntimeStatus.InvalidMapping;
                return;
            }
            if (source.SkeletonTransforms == null)
            {
                InitializationStatus = $"source-{source.InitializationStatus}";
                return;
            }

            InitializationStatus = "retargeting";

            _sourceBones = new Transform[Bones.Length];
            _sourceTPose = new Quaternion[Bones.Length];
            _targetTPose = new Quaternion[Bones.Length];
            _targetBones = new Transform[Bones.Length];
            _previousTargets = new Quaternion[Bones.Length];
            _candidateTargets = new Quaternion[Bones.Length];

            if (!TryCacheRetargetingBindPose())
            {
                InitializationStatus = ResolveRetargetingFailure();
                RuntimeStatus = bindPose == null
                    ? EAMMRuntimeStatus.MissingCalibration
                    : EAMMRuntimeStatus.InvalidMapping;
                _animationGraph?.SetEammMasterWeight(0f);
                return;
            }

            source.FootLock = false;
            source.LockFPS = false;
            CacheCandidateHierarchy();
            EnsureAuthoredGraph();
            SubscribeTransitionDirector();
            _previousAction = presentation != null
                ? presentation.CurrentAuthoredAction
                : EarthAuthoredActionId.None;
            _initialized = true;
            _ready = false;
            _poseSane = false;
            _visiblePoseRejected = false;
            _visibleInvalidFrames = 0;
            _stablePoseFrames = 0;
            _authoredIdleKneeOwnership = false;
            _hasLocomotionQueryState = false;
            _idleKneeEammWeight = 1f;
            RuntimeStatus = EAMMRuntimeStatus.Disabled;
            InitializationStatus = "awaiting-stable-pose";
            UpdateLocomotionQueryOwnership();
            // Initialization occurs while the graph is still held at zero master
            // weight. Seed the current query directly so the first visible idle
            // frame does not spend another handoff interval in an unseen state.
            _idleKneeEammWeight = _authoredIdleKneeOwnership ? 0f : 1f;
            HandleSourcePose();
        }

        private void EnsureAuthoredGraph()
        {
            if (HasAnimationGraph || animator == null || !animator.isHuman ||
                animator.runtimeAnimatorController == null) return;
            var authoredBones = new Transform[Bones.Length];
            for (int index = 0; index < Bones.Length; index++)
                authoredBones[index] = animator.GetBoneTransform(Bones[index]);
            _animationGraph = new EarthAnimationGraph();
            _animationGraph.Create(
                animator,
                authoredBones,
                CreateContactGroups(),
                $"Earth Animation - {name}");
            // Seed once; the driver is the only parameter/state writer afterwards.
            _animationGraph.SyncParametersFrom(animator);
            if (_animationDriver == null) _animationDriver = GetComponent<EarthAnimationDriver>();
            if (_animationDriver == null) _animationDriver = gameObject.AddComponent<EarthAnimationDriver>();
            _animationDriver.Configure(animator);
            _animationDriver.Attach(_animationGraph);
        }

        private string ResolveRetargetingFailure()
        {
            if (animator.avatar == null) return "missing-avatar";
            if (!animator.avatar.isValid) return "invalid-avatar";
            if (!animator.avatar.isHuman) return "non-humanoid-avatar";
            if (bindPose == null) return "missing-baked-bind-pose";
            return "retarget-map-incomplete";
        }

        private void HandleSourcePose()
        {
            if (!_initialized || _animationGraph == null || !_animationGraph.IsCreated) return;
            if (_visiblePoseRejected) return;

            Transform sourceHips = _sourceBones[0];
            if (sourceHips == null)
            {
                _poseSane = false;
                RejectPose("missing-source-hips");
                return;
            }

            Vector3 sourceUp = sourceHips.TransformDirection((Vector3)source.MMData.HipsUpLocalVector);
            Vector3 characterUp = animator.transform.up;
            UpdateSourcePostureMetrics(characterUp);
            _poseSane = IsFinite(sourceUp) && Vector3.Dot(sourceUp.normalized, characterUp.normalized) >= 0.35f;
            if (!_poseSane)
            {
                RejectPose("source-up-diverged");
                return;
            }

            float normalizedFrame = Mathf.Max(0.25f, Time.unscaledDeltaTime * 60f);
            for (int i = 0; i < Bones.Length; i++)
            {
                Transform sourceBone = _sourceBones[i];
                if (sourceBone == null) continue;
                Quaternion delta = Quaternion.Inverse(_sourceTPose[i]) * sourceBone.localRotation;
                Quaternion targetLocal = Normalize(_targetTPose[i] * delta);
                // The database is baked on the Linebreaker Humanoid itself, so
                // each sampled leg rotation is already expressed in the target
                // skeleton basis. Do not fold the sampled pelvis delta into the
                // thighs: that double-applies hip yaw/lean and points both legs
                // diagonally or collapses a foot toward the body.
                if (!IsFinite(targetLocal))
                {
                    RejectPose($"non-finite:{Bones[i]}");
                    return;
                }
                if (_stablePoseFrames > 0 &&
                    Quaternion.Angle(_previousTargets[i], targetLocal) > 60f * normalizedFrame)
                {
                    RejectPose($"bone-step:{Bones[i]}");
                    return;
                }
                _candidateTargets[i] = targetLocal;
            }

            // Validate the retargeted pose in the actual target hierarchy BEFORE
            // any graph target can reach the visible rig. Valid source-up and
            // finite quaternions do not prove that source/target rest bases agree.
            // The previous post-output three-frame guard let a folded pose render
            // at startup and again after the bot left its authored casting lane.
            if (!HasUprightCandidatePosture(out string candidateReason))
            {
                RejectPose(candidateReason);
                presentation?.FootContactController?.ClearBasePoseContactMetadata();
                return;
            }

            for (int i = 0; i < Bones.Length; i++)
            {
                if (_sourceBones[i] == null) continue;
                _previousTargets[i] = _candidateTargets[i];
                // PlanetMotor retains gameplay-root position/rotation, but the
                // locomotion pose itself must remain one coherent full-body pose.
                // Applying only the legs made the gait twist below a static torso.
                float retargetWeight = ResolveLocomotionBoneWeight(
                    Bones[i],
                    _idleKneeEammWeight);
                _animationGraph.SetEammTarget(i, _candidateTargets[i], retargetWeight);
            }

            _poseSane = true;
            _stablePoseFrames++;
            if (_stablePoseFrames >= 3)
            {
                _ready = true;
                RuntimeStatus = EAMMRuntimeStatus.Active;
                InitializationStatus = "ready-baked-local-space";
                PoseRejectionReason = string.Empty;
            }

        }

        private float ResolveSourcePhase01()
        {
            if (source == null || source.PoseSet == null) return 0f;
            for (int clipIndex = 0; clipIndex < source.PoseSet.NumberClips; clipIndex++)
            {
                PoseSet.AnimationClip clip = source.PoseSet.GetAnimationClip(clipIndex);
                if (source.CurrentFrame < clip.Start || source.CurrentFrame >= clip.End) continue;
                return Mathf.InverseLerp(clip.Start, Mathf.Max(clip.Start + 1, clip.End - 1), source.CurrentFrame);
            }
            return 0f;
        }

        private void ResolveSourcePoseFootContacts(out bool left, out bool right)
        {
            left = false;
            right = false;
            if (source == null || source.PoseSet == null || source.PoseSet.NumberPoses <= 0)
                return;

            int frame = Mathf.Clamp(source.CurrentFrame, 0, source.PoseSet.NumberPoses - 1);
            source.PoseSet.GetPose(frame, out PoseVector pose);
            // MotionMatchingController.Left/RightFootContact report the state of
            // EAMM's own hidden-rig foot lock. That state has another velocity
            // gate and can remain false even when the selected pose says a foot
            // is planted. Final Humanoid IK needs the authored pose metadata;
            // terrain raycasts remain the authority for the actual target.
            left = pose.LeftFootContact;
            right = pose.RightFootContact;
        }

        private bool TryCacheRetargetingBindPose()
        {
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                return false;
            SkeletonBone[] targetSkeleton = animator.avatar.humanDescription.skeleton;
            bool sameAvatar = !string.IsNullOrEmpty(bindPose.SourceAvatarHash) &&
                              string.Equals(
                                  bindPose.SourceAvatarHash,
                                  EarthRetargetBindPose.ComputeAvatarHash(animator.avatar),
                                  StringComparison.Ordinal);

            for (int i = 0; i < Bones.Length; i++)
            {
                Transform target = animator.GetBoneTransform(Bones[i]);
                if (target == null) continue;
                if (!bindPose.TryGet(Bones[i], out EarthRetargetBindBone bindBone) ||
                    !source.MMData.GetJointName(Bones[i], out string sourceName) ||
                    sourceName != bindBone.SourceJointName) return false;

                Transform sourceBone = FindSourceBone(sourceName);
                if (sourceBone == null) return false;
                _sourceBones[i] = sourceBone;
                _sourceTPose[i] = bindBone.SourceRestLocalRotation;
                _targetBones[i] = target;

                int targetIndex = Array.FindIndex(targetSkeleton, bone => bone.name == target.name);
                if (targetIndex < 0) return false;
                // The production database and both visible Linebreakers use the
                // exact same Avatar. Unity's HumanDescription skeleton rotation
                // is an import-space value and is not guaranteed to equal the
                // collapsed local rest basis baked into JLPM. Using it as the
                // target rest reintroduced FBX pre/post rotations, twisting the
                // thighs diagonally and pulling the feet toward the hips. For an
                // identical Avatar the baked source rest is also the exact target
                // rest, so the retarget equation correctly reduces to the sampled
                // local rotation. Cross-avatar targets keep the explicit basis
                // conversion and its candidate-pose safety gate.
                _targetTPose[i] = sameAvatar
                    ? bindBone.SourceRestLocalRotation
                    : targetSkeleton[targetIndex].rotation;
                _previousTargets[i] = _targetTPose[i];
                _candidateTargets[i] = _targetTPose[i];
            }
            return _sourceBones[0] != null && _targetBones[0] != null;
        }

        private void CacheCandidateHierarchy()
        {
            var nodes = new List<Transform>();
            for (int index = 0; index < _targetBones.Length; index++)
                CacheCandidateNode(_targetBones[index], nodes);
            _candidateHierarchy = nodes.ToArray();
            _candidateParentIndices = new int[nodes.Count];
            _candidateBoneIndices = new int[nodes.Count];
            _candidateWorldMatrices = new Matrix4x4[nodes.Count];
            for (int index = 0; index < nodes.Count; index++)
            {
                _candidateParentIndices[index] = nodes.IndexOf(nodes[index].parent);
                _candidateBoneIndices[index] = Array.IndexOf(_targetBones, nodes[index]);
            }
            _candidateHipsIndex = nodes.IndexOf(FindTargetBone(HumanBodyBones.Hips));
            _candidateHeadIndex = nodes.IndexOf(FindTargetBone(HumanBodyBones.Head));
            _candidateLeftFootIndex = nodes.IndexOf(FindTargetBone(HumanBodyBones.LeftFoot));
            _candidateRightFootIndex = nodes.IndexOf(FindTargetBone(HumanBodyBones.RightFoot));
        }

        private void CacheCandidateNode(Transform node, List<Transform> nodes)
        {
            if (node == null || nodes.Contains(node)) return;
            if (node != animator.transform) CacheCandidateNode(node.parent, nodes);
            nodes.Add(node);
        }

        private bool HasUprightCandidatePosture(out string reason)
        {
            if (_candidateHierarchy == null || _candidateHipsIndex < 0 ||
                _candidateHeadIndex < 0 || _candidateLeftFootIndex < 0 || _candidateRightFootIndex < 0)
            {
                reason = "candidate-map-incomplete";
                return false;
            }
            // Cached parent-before-child topology also includes non-Humanoid
            // intermediary transforms. No clone rig, Transform writes or per-frame
            // allocations are needed to predict the complete candidate skeleton.
            for (int index = 0; index < _candidateHierarchy.Length; index++)
            {
                Transform node = _candidateHierarchy[index];
                int parent = _candidateParentIndices[index];
                int bone = _candidateBoneIndices[index];
                _candidateWorldMatrices[index] = parent < 0
                    ? node.localToWorldMatrix
                    : _candidateWorldMatrices[parent] * Matrix4x4.TRS(
                        node.localPosition,
                        bone >= 0 ? _candidateTargets[bone] : node.localRotation,
                        node.localScale);
            }
            Vector3 up = animator.transform.up;
            Vector3 hips = _candidateWorldMatrices[_candidateHipsIndex].MultiplyPoint3x4(Vector3.zero);
            CandidateHeadHeight = Vector3.Dot(
                _candidateWorldMatrices[_candidateHeadIndex].MultiplyPoint3x4(Vector3.zero) - hips, up);
            CandidateLeftFootHeight = Vector3.Dot(
                _candidateWorldMatrices[_candidateLeftFootIndex].MultiplyPoint3x4(Vector3.zero) - hips, up);
            CandidateRightFootHeight = Vector3.Dot(
                _candidateWorldMatrices[_candidateRightFootIndex].MultiplyPoint3x4(Vector3.zero) - hips, up);
            if (!float.IsFinite(CandidateHeadHeight) || !float.IsFinite(CandidateLeftFootHeight) ||
                !float.IsFinite(CandidateRightFootHeight))
            {
                reason = "candidate-pose-non-finite";
                return false;
            }
            if (CandidateHeadHeight < 0.20f)
            {
                reason = "candidate-head-below-upright-envelope";
                return false;
            }
            if (CandidateLeftFootHeight > -0.08f && CandidateRightFootHeight > -0.08f)
            {
                reason = "candidate-feet-above-upright-envelope";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private void RejectPose(string reason)
        {
            _poseSane = false;
            _ready = false;
            _stablePoseFrames = 0;
            if (PoseRejectionReason != reason || RuntimeStatus != EAMMRuntimeStatus.PoseRejected)
            {
                PoseRejectionReason = reason;
                InitializationStatus = $"pose-rejected:{reason}";
            }
            RuntimeStatus = EAMMRuntimeStatus.PoseRejected;
            _animationGraph?.SetEammMasterWeight(0f);
        }

        private bool HasUprightLocomotionPosture(out string reason)
        {
            Transform hips = _targetBones != null && _targetBones.Length > 0
                ? _targetBones[0]
                : null;
            Transform head = FindTargetBone(HumanBodyBones.Head);
            Transform leftFoot = FindTargetBone(HumanBodyBones.LeftFoot);
            Transform rightFoot = FindTargetBone(HumanBodyBones.RightFoot);
            if (hips == null || head == null || leftFoot == null || rightFoot == null)
            {
                reason = "visible-map-incomplete";
                return false;
            }

            Vector3 up = animator.transform.up;
            float headHeight = Vector3.Dot(head.position - hips.position, up);
            float leftFootHeight = Vector3.Dot(leftFoot.position - hips.position, up);
            float rightFootHeight = Vector3.Dot(rightFoot.position - hips.position, up);
            LastVisibleHeadHeight = headHeight;
            LastVisibleLeftFootHeight = leftFootHeight;
            LastVisibleRightFootHeight = rightFootHeight;
            if (!float.IsFinite(headHeight) || !float.IsFinite(leftFootHeight) ||
                !float.IsFinite(rightFootHeight))
            {
                reason = "visible-pose-non-finite";
                return false;
            }
            // The KayKit run and guarded locomotion clips legitimately compress
            // the head-to-hips projection to about 0.34 m. Keep the gate below
            // that crouch envelope; the source-up and dual-foot-collapse checks
            // still reject inverted or folded retargets.
            if (headHeight < 0.20f)
            {
                reason = "visible-head-below-upright-envelope";
                return false;
            }
            // A locomotion pose legitimately lifts one foot close to the hips.
            // Reject only when both legs have collapsed upward together.
            if (leftFootHeight > -0.08f && rightFootHeight > -0.08f)
            {
                reason = "visible-feet-above-upright-envelope";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void UpdateSourcePostureMetrics(Vector3 up)
        {
            Transform hips = _sourceBones[0];
            Transform head = FindSourceBoneFor(HumanBodyBones.Head);
            Transform leftFoot = FindSourceBoneFor(HumanBodyBones.LeftFoot);
            Transform rightFoot = FindSourceBoneFor(HumanBodyBones.RightFoot);
            if (hips == null || head == null || leftFoot == null || rightFoot == null) return;
            Vector3 normalizedUp = up.sqrMagnitude > 0.5f ? up.normalized : Vector3.up;
            SourceHeadHeight = Vector3.Dot(head.position - hips.position, normalizedUp);
            SourceLeftFootHeight = Vector3.Dot(leftFoot.position - hips.position, normalizedUp);
            SourceRightFootHeight = Vector3.Dot(rightFoot.position - hips.position, normalizedUp);
        }

        private Transform FindSourceBoneFor(HumanBodyBones bone)
        {
            for (int index = 0; index < Bones.Length; index++)
                if (Bones[index] == bone) return _sourceBones[index];
            return null;
        }

        private Transform FindTargetBone(HumanBodyBones bone)
        {
            for (int index = 0; index < Bones.Length; index++)
                if (Bones[index] == bone) return _targetBones[index];
            return null;
        }

        public static float ResolveLocomotionBoneWeight(HumanBodyBones bone) =>
            ResolveLocomotionBoneWeight(bone, false);

        public static float ResolveLocomotionBoneWeight(
            HumanBodyBones bone,
            bool authoredIdleKneeOwnership)
            => ResolveLocomotionBoneWeight(
                bone,
                authoredIdleKneeOwnership ? 0f : 1f);

        public static float ResolveLocomotionBoneWeight(
            HumanBodyBones bone,
            float idleKneeEammWeight)
        {
            for (int index = 0; index < Bones.Length; index++)
            {
                if (Bones[index] != bone) continue;
                if (IsIdleLegBone(bone))
                    return Mathf.Clamp01(idleKneeEammWeight);
                return 1f;
            }
            return 0f;
        }

        public static float StepIdleKneeEammWeight(
            float current,
            bool authoredIdleKneeOwnership,
            float deltaTime,
            float handoffSeconds = IdleKneeHandoffSeconds)
        {
            float target = authoredIdleKneeOwnership ? 0f : 1f;
            float safeCurrent = float.IsFinite(current) ? Mathf.Clamp01(current) : target;
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f) return safeCurrent;
            float duration = float.IsFinite(handoffSeconds)
                ? Mathf.Max(0.001f, handoffSeconds)
                : IdleKneeHandoffSeconds;
            return Mathf.MoveTowards(safeCurrent, target, deltaTime / duration);
        }

        private static bool IsIdleLegBone(HumanBodyBones bone) => bone is
            HumanBodyBones.LeftUpperLeg or
            HumanBodyBones.LeftLowerLeg or
            HumanBodyBones.LeftFoot or
            HumanBodyBones.LeftToes or
            HumanBodyBones.RightUpperLeg or
            HumanBodyBones.RightLowerLeg or
            HumanBodyBones.RightFoot or
            HumanBodyBones.RightToes;

        private void UpdateLocomotionQueryOwnership()
        {
            PlanetEAMMCharacterController adapter = QueryAdapter;
            bool hasQuery = adapter != null && adapter.HasLocomotionQuery;
            bool locomotionQuery = hasQuery && adapter.LocomotionQuery;
            if (hasQuery && _hasLocomotionQueryState &&
                locomotionQuery != _previousLocomotionQuery)
                _animationGraph?.RequestInertialization(0.12f);

            _authoredIdleKneeOwnership = hasQuery && !locomotionQuery;
            if (hasQuery)
            {
                _previousLocomotionQuery = locomotionQuery;
                _hasLocomotionQueryState = true;
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z) && float.IsFinite(value.w);

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w);
            if (!float.IsFinite(magnitude) || magnitude < 0.0001f) return Quaternion.identity;
            float inverse = 1f / magnitude;
            return new Quaternion(value.x * inverse, value.y * inverse, value.z * inverse, value.w * inverse);
        }

        private static byte[] CreateContactGroups()
        {
            var groups = new byte[Bones.Length];
            for (int i = 0; i < Bones.Length; i++)
            {
                if (Bones[i] is HumanBodyBones.LeftFoot or HumanBodyBones.LeftToes) groups[i] = 1;
                else if (Bones[i] is HumanBodyBones.RightFoot or HumanBodyBones.RightToes) groups[i] = 2;
            }
            return groups;
        }

        private void SubscribeTransitionDirector()
        {
            if (_transitionDirector == null) _transitionDirector = GetComponent<EarthTransitionDirector>();
            if (_transitionDirector == null) return;
            _transitionDirector.InertializationRequested -= HandleInertializationRequested;
            _transitionDirector.InertializationRequested += HandleInertializationRequested;
        }

        private void UnsubscribeTransitionDirector()
        {
            if (_transitionDirector != null)
                _transitionDirector.InertializationRequested -= HandleInertializationRequested;
        }

        private void HandleInertializationRequested(float transitionSeconds) =>
            _animationGraph?.RequestInertialization(transitionSeconds);

        private float ResolveActionTransitionSeconds(EarthAuthoredActionId action) => action switch
        {
            EarthAuthoredActionId.RecoverableKnockdownRecovery => 0.18f,
            EarthAuthoredActionId.HitRecoil => 0.09f,
            EarthAuthoredActionId.MagicCast => 0.12f,
            EarthAuthoredActionId.Locomotion => 0.11f,
            _ => 0.08f
        };

        private Transform FindSourceBone(string boneName)
        {
            Transform[] transforms = source.SkeletonTransforms;
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == boneName) return transforms[i];
            }
            return null;
        }

        private void DisposeGraph()
        {
            _ready = false;
            _initialized = false;
            _visiblePoseRejected = false;
            _authoredIdleKneeOwnership = false;
            _hasLocomotionQueryState = false;
            _idleKneeEammWeight = 1f;
            RuntimeStatus = EAMMRuntimeStatus.Disabled;
            _animationDriver?.Detach(_animationGraph);
            _animationGraph?.Dispose();
            _animationGraph = null;
        }
    }
}
