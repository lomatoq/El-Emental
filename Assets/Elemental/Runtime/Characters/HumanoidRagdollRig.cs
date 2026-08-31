using System;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public readonly struct RagdollHandoff
    {
        public RagdollHandoff(Vector3 worldPoint, Vector3 velocityChange, bool hasWorldPoint)
        {
            WorldPoint = worldPoint;
            VelocityChange = velocityChange;
            HasWorldPoint = hasWorldPoint;
        }

        public Vector3 WorldPoint { get; }
        public Vector3 VelocityChange { get; }
        public bool HasWorldPoint { get; }

        public static RagdollHandoff Uniform(Vector3 velocityChange) =>
            new RagdollHandoff(Vector3.zero, velocityChange, false);
    }

    /// <summary>
    /// Runtime adapter that hands the currently rendered Humanoid pose to PhysX.
    /// Gameplay still owns KO timing; this component owns only visible bone physics,
    /// the stone-fade presentation and the atomic return to Animator ownership.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HumanoidRagdollRig : MonoBehaviour
    {
        private static readonly ProfilerMarker HandoffMarker =
            new ProfilerMarker("Elemental.Character.RagdollHandoff");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
        private static readonly HumanBodyBones[] HumanBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Chest,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg
        };
        private static readonly int[] ParentIndices =
        {
            -1, 0, 1, 1, 3, 1, 5, 0, 7, 0, 9
        };
        private static readonly float[] BodyMasses =
        {
            7.2f, 8.4f, 3.4f, 2.0f, 1.4f, 2.0f, 1.4f, 4.2f, 3.0f, 4.2f, 3.0f
        };

        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody motorRootBody;
        [SerializeField] private Collider motorCollider;
        [SerializeField] private GravityWorldBehaviour gravityWorld;
        [SerializeField] private ActiveRagdollPuppet physicalStateOwner;
        [SerializeField] private Behaviour[] disabledDuringRagdoll = Array.Empty<Behaviour>();
        [SerializeField] private HumanoidRagdollBone[] bones = Array.Empty<HumanoidRagdollBone>();
        [SerializeField] private Renderer[] visibleRenderers = Array.Empty<Renderer>();
        [SerializeField] private ParticleSystem stoneFadeDust;
        [SerializeField] private CharacterImpactResponseProfile impactResponseProfile;
        [SerializeField] private EarthEffectsTuningProfile effectsProfile;

        private Transform _originalParent;
        private int _originalSiblingIndex;
        private Vector3 _defaultLocalPosition;
        private Quaternion _defaultLocalRotation;
        private Vector3 _defaultLocalScale;
        private bool[] _behaviourEnabled = Array.Empty<bool>();
        private bool _rootWasKinematic;
        private bool _rootDetectedCollisions;
        private RigidbodyConstraints _rootConstraints;
        private bool _motorColliderWasEnabled;
        private bool _animatorWasEnabled;
        private bool _dustEmitted;
        private Vector3 _recoveryPelvisOffsetLocal;
        private MaterialPropertyBlock _properties;
        private MaterialPropertyBlock[] _rendererPresentationBlocks =
            Array.Empty<MaterialPropertyBlock>();
        private Color[] _fadeSourceBaseColors = Array.Empty<Color>();
        private Color[] _fadeSourceLegacyColors = Array.Empty<Color>();
        private readonly Vector3[] _localizedHitAxes = new Vector3[HumanBones.Length];
        private readonly float[] _localizedHitStartedAt = new float[HumanBones.Length];
        private readonly float[] _localizedHitDurations = new float[HumanBones.Length];
        private readonly float[] _localizedHitAngles = new float[HumanBones.Length];
        private readonly bool[] _localizedHitActive = new bool[HumanBones.Length];
        private readonly Collider[] _recoveryOverlaps = new Collider[16];
        private EarthRagdollRecoveryGateState _recoveryGate;

        public bool IsRagdollActive { get; private set; }
        public bool IsRecoveringToAnimation { get; private set; }
        public float StoneFade01 { get; private set; }
        public int DynamicBodyCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < bones.Length; index++)
                    if (bones[index] != null && bones[index].Body != null &&
                        !bones[index].Body.isKinematic) count++;
                return count;
            }
        }
        public int LocalizedRagdollHitCount { get; private set; }
        public EarthRagdollRecoverySide LastRecoverySide { get; private set; }
        public float LastRecoveryClearanceLiftMeters { get; private set; }
        public bool LastRecoveryClearanceSucceeded { get; private set; }
        public bool LastRecoveryUsedFacingFallback { get; private set; }
        public event Action AuthoredRecoveryBegan;

        public void ConfigureLocalizedReactionProfile(CharacterImpactResponseProfile profile) =>
            impactResponseProfile = profile;

        public void ConfigureEffectsProfile(EarthEffectsTuningProfile profile)
        {
            effectsProfile = profile;
            if (stoneFadeDust != null && effectsProfile != null)
                EarthParticleSystemTuningApplier.Apply(
                    stoneFadeDust,
                    effectsProfile.StoneFade.Dust,
                    effectsProfile.Materials.StoneFadeDust);
        }

        public void ApplyLocalizedRagdollImpulse(
            Vector3 worldPoint,
            Vector3 direction,
            float effectiveVelocityChange)
        {
            if (IsRagdollActive || bones == null || bones.Length == 0 ||
                (impactResponseProfile != null && !impactResponseProfile.LocalizedHitReaction)) return;
            int nearestIndex = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < bones.Length; index++)
            {
                Transform candidate = bones[index] != null ? bones[index].transform : null;
                if (candidate == null) continue;
                float distance = (candidate.position - worldPoint).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearestIndex = index;
            }
            if (nearestIndex < 0) return;
            Vector3 safeDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : transform.forward;
            Vector3 axisWorld = Vector3.Cross(safeDirection, transform.up);
            if (axisWorld.sqrMagnitude < 0.001f) axisWorld = transform.right;
            float parentWeight = impactResponseProfile != null
                ? impactResponseProfile.LocalizedParentWeight
                : 0.55f;
            float torsoWeight = impactResponseProfile != null
                ? impactResponseProfile.LocalizedTorsoWeight
                : 0.25f;
            ApplyLocalizedBoneReaction(nearestIndex, axisWorld, effectiveVelocityChange, 1f);
            int parentIndex = ParentIndices[nearestIndex];
            if (parentIndex >= 0)
                ApplyLocalizedBoneReaction(parentIndex, axisWorld, effectiveVelocityChange, parentWeight);
            int torsoIndex = nearestIndex <= 2 ? 0 : 1;
            if (torsoIndex != nearestIndex && torsoIndex != parentIndex)
                ApplyLocalizedBoneReaction(torsoIndex, axisWorld, effectiveVelocityChange, torsoWeight);
            if (nearestIndex > 2 && parentIndex != 2)
            {
                float headWeight = impactResponseProfile != null
                    ? impactResponseProfile.LocalizedHeadTransferWeight
                    : 0.18f;
                ApplyLocalizedBoneReaction(2, axisWorld, effectiveVelocityChange, headWeight);
            }
            LocalizedRagdollHitCount++;
        }

        public void ConfigureAndBuild(
            Animator configuredAnimator,
            Rigidbody configuredMotorRoot,
            Collider configuredMotorCollider,
            GravityWorldBehaviour configuredGravityWorld,
            ActiveRagdollPuppet configuredPhysicalStateOwner,
            ParticleSystem configuredStoneFadeDust,
            params Behaviour[] configuredDisabledBehaviours)
        {
            animator = configuredAnimator;
            motorRootBody = configuredMotorRoot;
            motorCollider = configuredMotorCollider;
            gravityWorld = configuredGravityWorld;
            physicalStateOwner = configuredPhysicalStateOwner;
            stoneFadeDust = configuredStoneFadeDust;
            if (stoneFadeDust != null && effectsProfile != null)
                EarthParticleSystemTuningApplier.Apply(
                    stoneFadeDust,
                    effectsProfile.StoneFade.Dust,
                    effectsProfile.Materials.StoneFadeDust);
            disabledDuringRagdoll = configuredDisabledBehaviours ?? Array.Empty<Behaviour>();
            CaptureDefaultRoot();
            BuildRig();
            CacheRenderers();
            SetAnimatedPhysicsState();
        }

        public void BuildRig()
        {
            if (animator == null || !animator.isHuman)
                throw new InvalidOperationException("Humanoid ragdoll requires a valid Humanoid Animator.");

            var configured = new HumanoidRagdollBone[HumanBones.Length];
            HumanoidRagdollBone[] existing = animator.GetComponentsInChildren<HumanoidRagdollBone>(true);
            for (int index = 0; index < existing.Length; index++)
            {
                HumanoidRagdollBone marker = existing[index];
                int slot = (int)marker.Role;
                if (slot >= 0 && slot < configured.Length) configured[slot] = marker;
            }

            for (int index = 0; index < HumanBones.Length; index++)
            {
                Transform bone = animator.GetBoneTransform(HumanBones[index]);
                if (bone == null)
                    throw new InvalidOperationException(
                        $"Humanoid ragdoll is missing required bone {HumanBones[index]}.");
                HumanoidRagdollBone marker = configured[index];
                if (marker == null) marker = bone.gameObject.AddComponent<HumanoidRagdollBone>();
                Rigidbody body = bone.GetComponent<Rigidbody>();
                if (body == null) body = bone.gameObject.AddComponent<Rigidbody>();
                body.mass = BodyMasses[index];
                body.useGravity = false;
                body.linearDamping = 0.08f;
                body.angularDamping = 0.16f;
                body.maxAngularVelocity = 24f;
                // Animator owns visible bone transforms until a real ragdoll
                // handoff. Interpolation on kinematic bone bodies replays their
                // fixed-clock poses over render-clock animation, producing the
                // characteristic several-frames-still/one-frame-jump legs.
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                Collider shape = marker.Shape;
                if (shape == null) shape = CreateBoneCollider(index, bone);
                ConfigurableJoint joint = marker.GetComponent<ConfigurableJoint>();
                if (ParentIndices[index] >= 0)
                {
                    if (joint == null) joint = bone.gameObject.AddComponent<ConfigurableJoint>();
                    ConfigureJoint(joint, configured[ParentIndices[index]].Body, index);
                }
                GravityBody gravity = marker.GetComponent<GravityBody>();
                if (gravity == null) gravity = bone.gameObject.AddComponent<GravityBody>();
                gravity.Configure(gravityWorld, body);
                marker.Configure((HumanoidRagdollBoneRole)index, body, shape, joint, gravity);
                configured[index] = marker;
            }
            bones = configured;
        }

        public void BeginRagdoll(Vector3 launchVelocityChange)
        {
            BeginRagdoll(RagdollHandoff.Uniform(launchVelocityChange));
        }

        public void BeginRagdoll(in RagdollHandoff handoff)
        {
            using (HandoffMarker.Auto())
            {
                if (bones == null || bones.Length != HumanBones.Length)
                {
                    Debug.LogError("[Elemental] Visible Humanoid ragdoll was not authored for this fighter.", this);
                    return;
                }
                if (IsRagdollActive)
                {
                    ApplyHandoff(in handoff);
                    return;
                }

                ResetLocalizedReactions();
                IsRecoveringToAnimation = false;
                _recoveryGate = default;

                CaptureRuntimeState();
                CaptureRendererPresentationState();
                if (animator != null && animator.enabled) animator.Update(0f);
                Vector3 inheritedVelocity = motorRootBody != null
                    ? motorRootBody.linearVelocity
                    : Vector3.zero;
                Vector3 inheritedAngular = motorRootBody != null
                    ? motorRootBody.angularVelocity
                    : Vector3.zero;
                Rigidbody pelvisBeforeHandoff = bones[0] != null ? bones[0].Body : null;
                if (motorRootBody != null && pelvisBeforeHandoff != null)
                    _recoveryPelvisOffsetLocal = Quaternion.Inverse(motorRootBody.rotation) *
                                                 (pelvisBeforeHandoff.position - motorRootBody.position);
                float3 limitedInherited = EarthRagdollLaunchLimiter.LimitInheritedVelocity(
                    new float3(inheritedVelocity.x, inheritedVelocity.y, inheritedVelocity.z),
                    new float3(transform.up.x, transform.up.y, transform.up.z));
                inheritedVelocity = new Vector3(
                    limitedInherited.x, limitedInherited.y, limitedInherited.z);
                physicalStateOwner?.SetExternalRagdollAuthority(true);
                SuspendMotorRoot();
                SetControlBehaviours(false);
                if (animator != null) animator.enabled = false;
                transform.SetParent(null, true);

                for (int index = 0; index < bones.Length; index++)
                {
                    HumanoidRagdollBone bone = bones[index];
                    if (bone == null || bone.Body == null) continue;
                    if (bone.Shape != null) bone.Shape.enabled = true;
                    bone.Body.interpolation = RigidbodyInterpolation.Interpolate;
                    bone.Body.isKinematic = false;
                    bone.Body.detectCollisions = true;
                    bone.Body.linearVelocity = inheritedVelocity;
                    bone.Body.angularVelocity = inheritedAngular;
                    if (bone.GravityBody != null) bone.GravityBody.enabled = true;
                    bone.Body.WakeUp();
                }
                IgnoreSelfCollisions();
                IsRagdollActive = true;
                SetStoneFade(0f);
                ApplyHandoff(in handoff);
                Rigidbody pelvis = bones[0] != null ? bones[0].Body : null;
                if (pelvis != null)
                {
                    Vector3 torqueAxis = handoff.VelocityChange.sqrMagnitude > 0.0001f
                        ? Vector3.Cross(handoff.VelocityChange.normalized, transform.up)
                        : transform.right;
                    if (torqueAxis.sqrMagnitude < 0.001f) torqueAxis = transform.right;
                    pelvis.AddTorque((torqueAxis.normalized * 3.8f) + (transform.forward * 1.2f),
                        ForceMode.VelocityChange);
                }
            }
        }

        /// <summary>
        /// Returns a recoverable knockdown at the current pelvis instead of the
        /// spawn pose. Physics hands off once to the authored Falling-To-Roll
        /// recovery; controls may remain disabled until CompleteRecovery.
        /// </summary>
        public void RecoverToAnimated(
            Vector3 localUp,
            Vector3 forward,
            bool restoreControls)
        {
            using (HandoffMarker.Auto())
            {
                if (!EarthRagdollRecoveryPoseSolver.TryConsumeRecoveryRequest(
                        ref _recoveryGate,
                        IsRagdollActive))
                    return;
                Rigidbody pelvis = bones != null && bones.Length > 0 && bones[0] != null
                    ? bones[0].Body
                    : null;
                Vector3 pelvisPosition = pelvis != null ? pelvis.position : transform.position;
                Quaternion pelvisRotation = pelvis != null ? pelvis.rotation : transform.rotation;
                Transform chest = bones != null && bones.Length > 1 && bones[1] != null
                    ? bones[1].transform
                    : null;
                Vector3 chestPosition = chest != null ? chest.position : pelvisPosition;
                Vector3 chestForward = chest != null ? chest.forward : pelvisRotation * Vector3.forward;
                Vector3 chestOutward = chest != null ? chest.up : transform.up;
                Vector3 up = localUp.sqrMagnitude > 0.25f ? localUp.normalized : transform.up;

                EarthRagdollRecoveryPose basePose = ResolveRecoveryPose(
                    pelvisPosition,
                    chestPosition,
                    pelvisRotation * Vector3.forward,
                    chestForward,
                    chestOutward,
                    up,
                    forward,
                    0f,
                    false);
                bool baseClear = IsRecoveryCapsuleClear(
                    ToVector3(basePose.RootPosition),
                    ToQuaternion(basePose.RootRotation),
                    up);
                EarthRagdollRecoveryPose firstLiftPose = ResolveRecoveryPose(
                    pelvisPosition,
                    chestPosition,
                    pelvisRotation * Vector3.forward,
                    chestForward,
                    chestOutward,
                    up,
                    forward,
                    EarthRagdollRecoveryPoseSolver.FirstClearanceLiftMeters,
                    false);
                bool firstLiftClear = baseClear || IsRecoveryCapsuleClear(
                    ToVector3(firstLiftPose.RootPosition),
                    ToQuaternion(firstLiftPose.RootRotation),
                    up);
                EarthRagdollRecoveryPose maximumLiftPose = ResolveRecoveryPose(
                    pelvisPosition,
                    chestPosition,
                    pelvisRotation * Vector3.forward,
                    chestForward,
                    chestOutward,
                    up,
                    forward,
                    EarthRagdollRecoveryPoseSolver.MaximumClearanceLiftMeters,
                    false);
                bool maximumLiftClear = firstLiftClear || IsRecoveryCapsuleClear(
                    ToVector3(maximumLiftPose.RootPosition),
                    ToQuaternion(maximumLiftPose.RootRotation),
                    up);
                float clearanceLift = EarthRagdollRecoveryPoseSolver.SelectClearanceLift(
                    baseClear,
                    firstLiftClear,
                    maximumLiftClear,
                    out bool clearanceSucceeded);
                EarthRagdollRecoveryPose recoveryPose = ResolveRecoveryPose(
                    pelvisPosition,
                    chestPosition,
                    pelvisRotation * Vector3.forward,
                    chestForward,
                    chestOutward,
                    up,
                    forward,
                    clearanceLift,
                    clearanceSucceeded);
                Vector3 rootPosition = ToVector3(recoveryPose.RootPosition);
                Quaternion rootRotation = ToQuaternion(recoveryPose.RootRotation);
                LastRecoverySide = recoveryPose.Side;
                LastRecoveryClearanceLiftMeters = recoveryPose.ClearanceLiftMeters;
                LastRecoveryClearanceSucceeded = recoveryPose.ClearanceSucceeded;
                LastRecoveryUsedFacingFallback = recoveryPose.UsedFacingFallback;

                if (bones != null)
                    for (int index = 0; index < bones.Length; index++)
                    {
                        HumanoidRagdollBone bone = bones[index];
                        if (bone == null || bone.Body == null) continue;
                        if (!bone.Body.isKinematic)
                        {
                            bone.Body.linearVelocity = Vector3.zero;
                            bone.Body.angularVelocity = Vector3.zero;
                        }
                        bone.Body.detectCollisions = false;
                        bone.Body.isKinematic = true;
                        bone.Body.interpolation = RigidbodyInterpolation.None;
                        if (bone.Shape != null) bone.Shape.enabled = false;
                        if (bone.GravityBody != null) bone.GravityBody.enabled = false;
                    }

                if (motorRootBody != null)
                {
                    motorRootBody.position = rootPosition;
                    motorRootBody.rotation = rootRotation;
                }
                transform.SetParent(_originalParent, false);
                transform.SetSiblingIndex(Mathf.Clamp(
                    _originalSiblingIndex,
                    0,
                    _originalParent != null ? _originalParent.childCount - 1 : 0));
                transform.localPosition = _defaultLocalPosition;
                transform.localRotation = _defaultLocalRotation;
                transform.localScale = _defaultLocalScale;
                RestoreMotorRoot();
                physicalStateOwner?.SetExternalRagdollAuthority(false);
                SetControlBehaviours(restoreControls);
                IsRagdollActive = false;
                IsRecoveringToAnimation = !restoreControls;
                ResetLocalizedReactions();
                StoneFade01 = 0f;
                RestoreVisiblePresentation();
                if (animator != null)
                {
                    animator.enabled = _animatorWasEnabled;
                    if (animator.enabled)
                    {
                        animator.Rebind();
                        animator.Update(0f);
                    }
                }
                if (IsRecoveringToAnimation) AuthoredRecoveryBegan?.Invoke();
                UnityEngine.Physics.SyncTransforms();
            }
        }

        public void CompleteRecovery()
        {
            if (!IsRecoveringToAnimation) return;
            SetControlBehaviours(true);
            IsRecoveringToAnimation = false;
        }

        public void SetStoneFade(float fade01)
        {
            StoneFade01 = Mathf.Clamp01(fade01);
            float eased = StoneFade01 * StoneFade01 * (3f - (2f * StoneFade01));
            _properties ??= new MaterialPropertyBlock();
            for (int index = 0; index < visibleRenderers.Length; index++)
            {
                Renderer renderer = visibleRenderers[index];
                if (renderer == null) continue;
                renderer.enabled = StoneFade01 < 0.985f;
                if (!renderer.enabled) continue;
                Material material = renderer.sharedMaterial;
                renderer.GetPropertyBlock(_properties);
                if (material != null && material.HasProperty(BaseColorId))
                {
                    Color source = index < _fadeSourceBaseColors.Length
                        ? _fadeSourceBaseColors[index]
                        : material.GetColor(BaseColorId);
                    _properties.SetColor(
                        BaseColorId,
                        Color.Lerp(source, new Color(0.34f, 0.27f, 0.21f, source.a), eased));
                }
                if (material != null && material.HasProperty(LegacyColorId))
                {
                    Color source = index < _fadeSourceLegacyColors.Length
                        ? _fadeSourceLegacyColors[index]
                        : material.GetColor(LegacyColorId);
                    _properties.SetColor(
                        LegacyColorId,
                        Color.Lerp(source, new Color(0.34f, 0.27f, 0.21f, source.a), eased));
                }
                if (material != null && material.HasProperty("_Dissolve"))
                    _properties.SetFloat("_Dissolve", eased);
                renderer.SetPropertyBlock(_properties);
            }
            float dustTrigger = effectsProfile != null ? effectsProfile.StoneFade.Trigger01 : 0.42f;
            int dustCount = effectsProfile != null ? effectsProfile.StoneFade.EmitCount : 20;
            if (!_dustEmitted && StoneFade01 >= dustTrigger && stoneFadeDust != null)
            {
                _dustEmitted = true;
                stoneFadeDust.transform.SetParent(null, true);
                stoneFadeDust.Emit(dustCount);
            }
        }

        public void ResetToAnimated()
        {
            using (HandoffMarker.Auto())
            {
                if (bones != null)
                    for (int index = 0; index < bones.Length; index++)
                    {
                        HumanoidRagdollBone bone = bones[index];
                        if (bone == null || bone.Body == null) continue;
                        if (!bone.Body.isKinematic)
                        {
                            bone.Body.linearVelocity = Vector3.zero;
                            bone.Body.angularVelocity = Vector3.zero;
                        }
                        bone.Body.detectCollisions = false;
                        bone.Body.isKinematic = true;
                        if (bone.Shape != null) bone.Shape.enabled = false;
                        if (bone.GravityBody != null) bone.GravityBody.enabled = false;
                    }

                transform.SetParent(_originalParent, false);
                transform.SetSiblingIndex(Mathf.Clamp(
                    _originalSiblingIndex,
                    0,
                    _originalParent != null ? _originalParent.childCount - 1 : 0));
                transform.localPosition = _defaultLocalPosition;
                transform.localRotation = _defaultLocalRotation;
                transform.localScale = _defaultLocalScale;
                RestoreMotorRoot();
                physicalStateOwner?.SetExternalRagdollAuthority(false);
                SetControlBehaviours(true);
                IsRagdollActive = false;
                IsRecoveringToAnimation = false;
                ResetLocalizedReactions();
                StoneFade01 = 0f;
                _dustEmitted = false;
                if (stoneFadeDust != null)
                {
                    stoneFadeDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    stoneFadeDust.transform.SetParent(transform, false);
                }
                RestoreVisiblePresentation();
                if (animator != null)
                {
                    animator.enabled = _animatorWasEnabled;
                    if (animator.enabled)
                    {
                        animator.Rebind();
                        animator.Play("Locomotion", 0, 0f);
                        animator.Update(0f);
                    }
                }
                UnityEngine.Physics.SyncTransforms();
            }
        }

        private void RestoreVisiblePresentation()
        {
            _dustEmitted = false;
            if (stoneFadeDust != null)
            {
                stoneFadeDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                stoneFadeDust.transform.SetParent(transform, false);
            }
            for (int index = 0; index < visibleRenderers.Length; index++)
            {
                Renderer renderer = visibleRenderers[index];
                if (renderer == null) continue;
                renderer.enabled = true;
                MaterialPropertyBlock restored =
                    index < _rendererPresentationBlocks.Length
                        ? _rendererPresentationBlocks[index]
                        : null;
                renderer.SetPropertyBlock(restored);
            }
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            CaptureDefaultRoot();
            CacheRenderers();
            if (bones == null || bones.Length != HumanBones.Length)
                bones = animator != null
                    ? animator.GetComponentsInChildren<HumanoidRagdollBone>(true)
                    : Array.Empty<HumanoidRagdollBone>();
            SetAnimatedPhysicsState();
        }

        private void LateUpdate()
        {
            if (IsRagdollActive) return;
            for (int index = 0; index < _localizedHitActive.Length; index++)
            {
                if (!_localizedHitActive[index]) continue;
                HumanoidRagdollBone marker = index < bones.Length ? bones[index] : null;
                Transform bone = marker != null ? marker.transform : null;
                float age = Time.time - _localizedHitStartedAt[index];
                float duration = _localizedHitDurations[index];
                if (bone == null || age >= duration)
                {
                    _localizedHitActive[index] = false;
                    continue;
                }
                const float attackSeconds = 0.028f;
                float attack = Mathf.Clamp01(age / attackSeconds);
                float recovery = Mathf.Clamp01((age - attackSeconds) /
                                                Mathf.Max(0.01f, duration - attackSeconds));
                float envelope = attack * (1f - recovery) * (1f - recovery);
                bone.localRotation *= Quaternion.AngleAxis(
                    _localizedHitAngles[index] * envelope,
                    _localizedHitAxes[index]);
            }
        }

        private void ApplyLocalizedBoneReaction(
            int index,
            Vector3 axisWorld,
            float effectiveVelocityChange,
            float weight)
        {
            if (index < 0 || index >= bones.Length || bones[index] == null) return;
            // Foot, knee and pelvis ownership belongs to the contact/body pass.
            // A hit pose may transfer into chest/head/arms but must never twist
            // the planted chain after IK has solved it.
            if (index == 0 || index >= 7) return;
            Transform bone = bones[index].transform;
            bool head = index == 2;
            float maximum = head
                ? (impactResponseProfile != null ? impactResponseProfile.LocalizedHeadMaxAngle : 6f)
                : (impactResponseProfile != null ? impactResponseProfile.LocalizedArmChestMaxAngle : 12f);
            float minimum = head ? 2.5f : 4f;
            float angle = Mathf.Clamp(effectiveVelocityChange * 4.2f, minimum, maximum) * weight;
            _localizedHitAxes[index] = bone.InverseTransformDirection(axisWorld.normalized);
            _localizedHitStartedAt[index] = Time.time;
            _localizedHitDurations[index] = impactResponseProfile != null
                ? impactResponseProfile.LocalizedHitDuration
                : 0.18f;
            _localizedHitAngles[index] = Mathf.Max(_localizedHitAngles[index] * 0.45f, angle);
            _localizedHitActive[index] = true;
        }

        private void ResetLocalizedReactions()
        {
            Array.Clear(_localizedHitActive, 0, _localizedHitActive.Length);
            Array.Clear(_localizedHitAngles, 0, _localizedHitAngles.Length);
        }

        private void CaptureDefaultRoot()
        {
            _originalParent = transform.parent;
            _originalSiblingIndex = transform.GetSiblingIndex();
            _defaultLocalPosition = transform.localPosition;
            _defaultLocalRotation = transform.localRotation;
            _defaultLocalScale = transform.localScale;
        }

        private void CacheRenderers()
        {
            Renderer[] all = GetComponentsInChildren<Renderer>(true);
            int count = 0;
            for (int index = 0; index < all.Length; index++)
                if (all[index] is not ParticleSystemRenderer) count++;
            visibleRenderers = new Renderer[count];
            int output = 0;
            for (int index = 0; index < all.Length; index++)
                if (all[index] is not ParticleSystemRenderer) visibleRenderers[output++] = all[index];
        }

        private void CaptureRendererPresentationState()
        {
            int count = visibleRenderers?.Length ?? 0;
            if (_rendererPresentationBlocks.Length != count)
                _rendererPresentationBlocks = new MaterialPropertyBlock[count];
            if (_fadeSourceBaseColors.Length != count)
                _fadeSourceBaseColors = new Color[count];
            if (_fadeSourceLegacyColors.Length != count)
                _fadeSourceLegacyColors = new Color[count];

            for (int index = 0; index < count; index++)
            {
                Renderer renderer = visibleRenderers[index];
                MaterialPropertyBlock block = _rendererPresentationBlocks[index] ??=
                    new MaterialPropertyBlock();
                block.Clear();
                if (renderer == null)
                {
                    _fadeSourceBaseColors[index] = Color.white;
                    _fadeSourceLegacyColors[index] = Color.white;
                    continue;
                }

                renderer.GetPropertyBlock(block);
                Material material = renderer.sharedMaterial;
                _fadeSourceBaseColors[index] = block.HasColor(BaseColorId)
                    ? block.GetColor(BaseColorId)
                    : material != null && material.HasProperty(BaseColorId)
                        ? material.GetColor(BaseColorId)
                        : Color.white;
                _fadeSourceLegacyColors[index] = block.HasColor(LegacyColorId)
                    ? block.GetColor(LegacyColorId)
                    : material != null && material.HasProperty(LegacyColorId)
                        ? material.GetColor(LegacyColorId)
                        : Color.white;
            }
        }

        private void CaptureRuntimeState()
        {
            _animatorWasEnabled = animator != null && animator.enabled;
            if (motorRootBody != null)
            {
                _rootWasKinematic = motorRootBody.isKinematic;
                _rootDetectedCollisions = motorRootBody.detectCollisions;
                _rootConstraints = motorRootBody.constraints;
            }
            _motorColliderWasEnabled = motorCollider != null && motorCollider.enabled;
            if (_behaviourEnabled == null || _behaviourEnabled.Length != disabledDuringRagdoll.Length)
                _behaviourEnabled = new bool[disabledDuringRagdoll.Length];
            for (int index = 0; index < disabledDuringRagdoll.Length; index++)
                _behaviourEnabled[index] = disabledDuringRagdoll[index] != null &&
                                           disabledDuringRagdoll[index].enabled;
        }

        private void SuspendMotorRoot()
        {
            if (motorCollider != null) motorCollider.enabled = false;
            if (motorRootBody == null) return;
            motorRootBody.linearVelocity = Vector3.zero;
            motorRootBody.angularVelocity = Vector3.zero;
            motorRootBody.detectCollisions = false;
            motorRootBody.isKinematic = true;
        }

        private void RestoreMotorRoot()
        {
            if (motorRootBody != null)
            {
                motorRootBody.constraints = _rootConstraints;
                motorRootBody.isKinematic = _rootWasKinematic;
                motorRootBody.detectCollisions = _rootDetectedCollisions;
                if (!motorRootBody.isKinematic)
                {
                    motorRootBody.linearVelocity = Vector3.zero;
                    motorRootBody.angularVelocity = Vector3.zero;
                }
            }
            if (motorCollider != null) motorCollider.enabled = _motorColliderWasEnabled;
        }

        private EarthRagdollRecoveryPose ResolveRecoveryPose(
            Vector3 pelvisPosition,
            Vector3 chestPosition,
            Vector3 pelvisForward,
            Vector3 chestForward,
            Vector3 chestOutward,
            Vector3 localUp,
            Vector3 preferredForward,
            float clearanceLift,
            bool clearanceSucceeded) =>
            EarthRagdollRecoveryPoseSolver.Resolve(
                ToFloat3(pelvisPosition),
                ToFloat3(chestPosition),
                ToFloat3(pelvisForward),
                ToFloat3(chestForward),
                ToFloat3(chestOutward),
                ToFloat3(localUp),
                ToFloat3(preferredForward),
                ToFloat3(_recoveryPelvisOffsetLocal),
                clearanceLift,
                clearanceSucceeded);

        private bool IsRecoveryCapsuleClear(
            Vector3 rootPosition,
            Quaternion rootRotation,
            Vector3 localUp)
        {
            if (motorCollider is not CapsuleCollider capsule || motorRootBody == null)
                return true;
            Transform rootTransform = motorRootBody.transform;
            Vector3 centerLocal = rootTransform.InverseTransformPoint(
                capsule.transform.TransformPoint(capsule.center));
            Vector3 axis = capsule.direction switch
            {
                0 => Vector3.right,
                2 => Vector3.forward,
                _ => Vector3.up
            };
            Vector3 axisLocal = rootTransform.InverseTransformDirection(
                capsule.transform.TransformDirection(axis)).normalized;
            Vector3 worldAxis = (rootRotation * axisLocal).normalized;
            Vector3 worldCenter = rootPosition + rootRotation * centerLocal;
            Vector3 scale = capsule.transform.lossyScale;
            float axialScale = capsule.direction switch
            {
                0 => Mathf.Abs(scale.x),
                2 => Mathf.Abs(scale.z),
                _ => Mathf.Abs(scale.y)
            };
            float radialScale = capsule.direction switch
            {
                0 => Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)),
                2 => Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)),
                _ => Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z))
            };
            float radius = Mathf.Max(0.01f, capsule.radius * radialScale * 0.92f);
            float halfSegment = Mathf.Max(
                0f,
                capsule.height * axialScale * 0.5f - radius);
            Vector3 pointA = worldCenter + worldAxis * halfSegment;
            Vector3 pointB = worldCenter - worldAxis * halfSegment;
            PlanetMotor motor = motorRootBody.GetComponent<PlanetMotor>();
            int mask = motor != null ? motor.GroundMask.value : 1;
            int count = UnityEngine.Physics.OverlapCapsuleNonAlloc(
                pointA,
                pointB,
                radius,
                _recoveryOverlaps,
                mask,
                QueryTriggerInteraction.Ignore);
            if (count >= _recoveryOverlaps.Length) return false;
            Vector3 up = localUp.sqrMagnitude > 0.25f ? localUp.normalized : rootRotation * Vector3.up;
            float supportBand = -Mathf.Max(0.05f, radius * 0.35f);
            for (int index = 0; index < count; index++)
            {
                Collider candidate = _recoveryOverlaps[index];
                if (candidate == null || candidate == motorCollider) continue;
                if (candidate.transform.IsChildOf(transform) ||
                    candidate.transform.IsChildOf(rootTransform))
                    continue;
                Vector3 closest = candidate is MeshCollider mesh && !mesh.convex
                    ? candidate.bounds.ClosestPoint(worldCenter)
                    : candidate.ClosestPoint(worldCenter);
                if (Vector3.Dot(closest - worldCenter, up) <= supportBand)
                    continue;
                return false;
            }
            return true;
        }

        private void SetControlBehaviours(bool restore)
        {
            for (int index = 0; index < disabledDuringRagdoll.Length; index++)
            {
                Behaviour behaviour = disabledDuringRagdoll[index];
                if (behaviour == null) continue;
                behaviour.enabled = restore && index < _behaviourEnabled.Length
                    ? _behaviourEnabled[index]
                    : false;
            }
        }

        private void SetAnimatedPhysicsState()
        {
            if (bones == null) return;
            for (int index = 0; index < bones.Length; index++)
            {
                HumanoidRagdollBone bone = bones[index];
                if (bone == null || bone.Body == null) continue;
                bone.Body.interpolation = RigidbodyInterpolation.None;
                bone.Body.isKinematic = true;
                bone.Body.detectCollisions = false;
                if (bone.Shape != null) bone.Shape.enabled = false;
                if (bone.GravityBody != null) bone.GravityBody.enabled = false;
            }
        }

        private void ApplyVelocityChange(Vector3 velocityChange)
        {
            if (!IsFinite(velocityChange) || velocityChange.sqrMagnitude <= 0.0001f) return;
            for (int index = 0; index < bones.Length; index++)
            {
                Rigidbody body = bones[index] != null ? bones[index].Body : null;
                if (body != null && !body.isKinematic)
                    body.AddForce(velocityChange, ForceMode.VelocityChange);
            }
        }

        private void ApplyHandoff(in RagdollHandoff handoff)
        {
            Vector3 velocityChange = Vector3.ClampMagnitude(handoff.VelocityChange, 12f);
            if (!IsFinite(velocityChange) || velocityChange.sqrMagnitude <= 0.0001f) return;
            if (!handoff.HasWorldPoint)
            {
                ApplyVelocityChange(velocityChange);
                return;
            }

            Rigidbody nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < bones.Length; index++)
            {
                Rigidbody body = bones[index] != null ? bones[index].Body : null;
                if (body == null || body.isKinematic) continue;
                float distance = (body.worldCenterOfMass - handoff.WorldPoint).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = body;
            }

            ApplyBoneVelocityChange(0, velocityChange * 0.15f);
            ApplyBoneVelocityChange(1, velocityChange * 0.15f);
            if (nearest != null)
                nearest.AddForceAtPosition(velocityChange * 0.70f, handoff.WorldPoint, ForceMode.VelocityChange);
            else
                ApplyVelocityChange(velocityChange);
        }

        private void ApplyBoneVelocityChange(int index, Vector3 velocityChange)
        {
            if (index < 0 || index >= bones.Length || bones[index] == null) return;
            Rigidbody body = bones[index].Body;
            if (body != null && !body.isKinematic)
                body.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        private void IgnoreSelfCollisions()
        {
            for (int left = 0; left < bones.Length; left++)
            {
                Collider a = bones[left] != null ? bones[left].Shape : null;
                if (a == null) continue;
                for (int right = left + 1; right < bones.Length; right++)
                {
                    Collider b = bones[right] != null ? bones[right].Shape : null;
                    if (b != null) UnityEngine.Physics.IgnoreCollision(a, b, true);
                }
            }
        }

        private Collider CreateBoneCollider(int index, Transform bone)
        {
            if (index == (int)HumanoidRagdollBoneRole.Head)
            {
                SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
                sphere.radius = 0.115f;
                sphere.center = new Vector3(0f, 0.025f, 0.015f);
                return sphere;
            }

            Transform endpoint = ResolveEndpoint(index);
            Vector3 localVector = endpoint != null
                ? bone.InverseTransformVector(endpoint.position - bone.position)
                : Vector3.up * 0.24f;
            float length = Mathf.Max(0.12f, localVector.magnitude);
            int direction = DominantAxis(localVector);
            CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = direction;
            capsule.radius = Mathf.Clamp(length * (index is 0 or 1 ? 0.30f : 0.21f), 0.065f, 0.18f);
            capsule.height = Mathf.Max(capsule.radius * 2.05f, length * 0.88f);
            capsule.center = localVector.normalized * (capsule.height * 0.42f);
            return capsule;
        }

        private Transform ResolveEndpoint(int index)
        {
            HumanBodyBones endpoint = index switch
            {
                0 => HumanBodyBones.Chest,
                1 => HumanBodyBones.Neck,
                3 => HumanBodyBones.LeftLowerArm,
                4 => HumanBodyBones.LeftHand,
                5 => HumanBodyBones.RightLowerArm,
                6 => HumanBodyBones.RightHand,
                7 => HumanBodyBones.LeftLowerLeg,
                8 => HumanBodyBones.LeftFoot,
                9 => HumanBodyBones.RightLowerLeg,
                10 => HumanBodyBones.RightFoot,
                _ => HumanBodyBones.LastBone
            };
            return endpoint != HumanBodyBones.LastBone ? animator.GetBoneTransform(endpoint) : null;
        }

        private static int DominantAxis(Vector3 value)
        {
            Vector3 absolute = new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
            if (absolute.x >= absolute.y && absolute.x >= absolute.z) return 0;
            return absolute.y >= absolute.z ? 1 : 2;
        }

        private static void ConfigureJoint(ConfigurableJoint joint, Rigidbody parent, int index)
        {
            joint.connectedBody = parent;
            joint.autoConfigureConnectedAnchor = true;
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;
            float swing = index is 3 or 5 or 7 or 9 ? 62f : index is 4 or 6 or 8 or 10 ? 18f : 34f;
            float twist = index is 4 or 6 or 8 or 10 ? 72f : 42f;
            joint.lowAngularXLimit = Limit(-twist);
            joint.highAngularXLimit = Limit(twist);
            joint.angularYLimit = Limit(swing);
            joint.angularZLimit = Limit(swing);
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.045f;
            joint.projectionAngle = 9f;
            joint.enablePreprocessing = false;
            joint.enableCollision = false;
            joint.connectedMassScale = 1f;
        }

        private static SoftJointLimit Limit(float degrees) => new SoftJointLimit
        {
            limit = degrees,
            bounciness = 0f,
            contactDistance = 3f
        };

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static float3 ToFloat3(Vector3 value) =>
            new float3(value.x, value.y, value.z);

        private static Vector3 ToVector3(float3 value) =>
            new Vector3(value.x, value.y, value.z);

        private static Quaternion ToQuaternion(quaternion value) =>
            new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
    }
}
