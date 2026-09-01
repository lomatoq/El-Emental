using System;
using System.Collections.Generic;
using System.Reflection;
using Elemental.Runtime.Characters;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public static class Gate1RecoverySampleMath
    {
        public const float MaximumPelvisContinuityErrorMeters = 0.002f;

        public static Vector3 MotorRootPelvisOffset(
            Vector3 pelvisWorldPosition,
            Vector3 motorRootWorldPosition,
            Quaternion motorRootWorldRotation)
        {
            return Quaternion.Inverse(motorRootWorldRotation) *
                (pelvisWorldPosition - motorRootWorldPosition);
        }

        public static Vector3 ReconstructPreClearancePelvis(
            Vector3 motorRootWorldPosition,
            Quaternion motorRootWorldRotation,
            Vector3 pelvisOffsetLocal,
            Vector3 clearanceUp,
            float clearanceLiftMeters)
        {
            Vector3 up = clearanceUp.sqrMagnitude > 0.25f
                ? clearanceUp.normalized
                : Vector3.up;
            return motorRootWorldPosition +
                   motorRootWorldRotation * pelvisOffsetLocal -
                   up * Mathf.Max(0f, clearanceLiftMeters);
        }
    }

    internal readonly struct Gate1RecoveryPoseFixture
    {
        public Gate1RecoveryPoseFixture(
            Vector3 pelvisOffset,
            Vector3 chestOffset,
            Vector3 leftHandOffset,
            Vector3 rightHandOffset,
            Vector3 leftFootOffset,
            Vector3 rightFootOffset,
            Vector3 chestOutward)
        {
            PelvisOffset = pelvisOffset;
            ChestOffset = chestOffset;
            LeftHandOffset = leftHandOffset;
            RightHandOffset = rightHandOffset;
            LeftFootOffset = leftFootOffset;
            RightFootOffset = rightFootOffset;
            ChestOutward = chestOutward;
        }

        public Vector3 PelvisOffset { get; }
        public Vector3 ChestOffset { get; }
        public Vector3 LeftHandOffset { get; }
        public Vector3 RightHandOffset { get; }
        public Vector3 LeftFootOffset { get; }
        public Vector3 RightFootOffset { get; }
        public Vector3 ChestOutward { get; }
    }

    internal static class Gate1IsolatedRecoveryPoseSampler
    {
        public static bool TrySample(
            Animator sourceAnimator,
            Rigidbody sourceMotorRoot,
            int stateHash,
            float phase,
            out Gate1RecoveryPoseFixture fixture,
            out string failure)
        {
            fixture = default;
            failure = string.Empty;
            if (sourceAnimator == null || sourceAnimator.runtimeAnimatorController == null ||
                sourceMotorRoot == null)
            {
                failure = "The isolated recovery sampler requires an Animator controller and motor-root body.";
                return false;
            }

            var isolationRoot = new GameObject("Gate1 Isolated Recovery Pose Sampler")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            isolationRoot.SetActive(false);
            try
            {
                GameObject clone = UnityEngine.Object.Instantiate(
                    sourceAnimator.gameObject,
                    isolationRoot.transform,
                    false);
                clone.name = "Gate1 Isolated Authored Recovery Rig";
                clone.hideFlags = HideFlags.HideAndDontSave;
                Animator sampler = clone.GetComponent<Animator>();
                if (sampler == null)
                {
                    failure = "The isolated recovery clone did not retain its Animator.";
                    return false;
                }

                Behaviour[] behaviours = clone.GetComponentsInChildren<Behaviour>(true);
                for (int index = behaviours.Length - 1; index >= 0; index--)
                {
                    Behaviour behaviour = behaviours[index];
                    if (behaviour != null && behaviour != sampler)
                        UnityEngine.Object.DestroyImmediate(behaviour);
                }
                Collider[] colliders = clone.GetComponentsInChildren<Collider>(true);
                for (int index = 0; index < colliders.Length; index++)
                    colliders[index].enabled = false;
                Rigidbody[] bodies = clone.GetComponentsInChildren<Rigidbody>(true);
                for (int index = 0; index < bodies.Length; index++)
                {
                    bodies[index].detectCollisions = false;
                    bodies[index].isKinematic = true;
                }

                sampler.applyRootMotion = false;
                sampler.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                sampler.enabled = true;
                isolationRoot.SetActive(true);
                sampler.Play(stateHash, 0, phase);
                sampler.Update(0f);

                Transform hips = sampler.GetBoneTransform(HumanBodyBones.Hips);
                Transform chest = sampler.GetBoneTransform(HumanBodyBones.Chest);
                Transform leftHand = sampler.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                Transform rightHand = sampler.GetBoneTransform(HumanBodyBones.RightLowerArm);
                Transform leftFoot = sampler.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Transform rightFoot = sampler.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                if (hips == null || chest == null || leftHand == null || rightHand == null ||
                    leftFoot == null || rightFoot == null)
                {
                    failure = "The isolated recovery clone is missing required Humanoid bones.";
                    return false;
                }

                Vector3 sourceRootToMotor = sourceAnimator.transform.InverseTransformPoint(
                    sourceMotorRoot.position);
                Quaternion sourceRootToMotorRotation =
                    Quaternion.Inverse(sourceAnimator.transform.rotation) * sourceMotorRoot.rotation;
                Vector3 sampledMotorPosition = sampler.transform.TransformPoint(sourceRootToMotor);
                Quaternion sampledMotorRotation =
                    sampler.transform.rotation * sourceRootToMotorRotation;
                Quaternion inversePelvis = Quaternion.Inverse(hips.rotation);
                fixture = new Gate1RecoveryPoseFixture(
                    Gate1RecoverySampleMath.MotorRootPelvisOffset(
                        hips.position,
                        sampledMotorPosition,
                        sampledMotorRotation),
                    inversePelvis * (chest.position - hips.position),
                    inversePelvis * (leftHand.position - hips.position),
                    inversePelvis * (rightHand.position - hips.position),
                    inversePelvis * (leftFoot.position - hips.position),
                    inversePelvis * (rightFoot.position - hips.position),
                    inversePelvis * chest.up);
                return true;
            }
            finally
            {
                isolationRoot.SetActive(false);
                UnityEngine.Object.DestroyImmediate(isolationRoot);
            }
        }
    }

    internal static class Gate1Reflection
    {
        public const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static Type RequireType(string assemblyQualifiedName)
        {
            return Type.GetType(assemblyQualifiedName, false);
        }

        public static bool TrySetField(object target, string name, object value)
        {
            FieldInfo field = target?.GetType().GetField(name, InstanceFlags);
            if (field == null) return false;
            field.SetValue(target, value);
            return true;
        }

        public static object GetField(object target, string name)
        {
            return target?.GetType().GetField(name, InstanceFlags)?.GetValue(target);
        }

        public static object GetProperty(object target, string name)
        {
            return target?.GetType().GetProperty(name, InstanceFlags)?.GetValue(target);
        }

        public static bool GetBoolean(object target, string name)
        {
            object value = GetProperty(target, name);
            return value is bool flag && flag;
        }

        public static int GetInt32(object target, string name)
        {
            object value = GetProperty(target, name);
            if (value is int integer) return integer;
            if (value is uint unsigned) return unsigned > int.MaxValue
                ? int.MaxValue
                : (int)unsigned;
            return 0;
        }

        public static MethodInfo FindMethod(Type type, string name, int parameterCount)
        {
            if (type == null) return null;
            MethodInfo[] methods = type.GetMethods(InstanceFlags);
            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (method.Name == name && method.GetParameters().Length == parameterCount)
                    return method;
            }
            return null;
        }
    }

    internal sealed class Gate1AnimatorSnapshot
    {
        private readonly Animator _animator;
        private readonly bool _enabled;
        private readonly float _speed;
        private readonly int[] _stateHashes;
        private readonly float[] _stateTimes;
        private readonly float[] _layerWeights;

        public Gate1AnimatorSnapshot(Animator animator)
        {
            _animator = animator;
            _enabled = animator != null && animator.enabled;
            _speed = animator != null ? animator.speed : 1f;
            int layerCount = animator != null ? animator.layerCount : 0;
            _stateHashes = new int[layerCount];
            _stateTimes = new float[layerCount];
            _layerWeights = new float[layerCount];
            if (animator == null) return;
            for (int layer = 0; layer < layerCount; layer++)
            {
                AnimatorStateInfo state = animator.IsInTransition(layer)
                    ? animator.GetNextAnimatorStateInfo(layer)
                    : animator.GetCurrentAnimatorStateInfo(layer);
                _stateHashes[layer] = state.fullPathHash;
                _stateTimes[layer] = Mathf.Repeat(state.normalizedTime, 1f);
                _layerWeights[layer] = animator.GetLayerWeight(layer);
            }
        }

        public void Restore()
        {
            if (_animator == null) return;
            _animator.enabled = true;
            _animator.speed = _speed;
            for (int layer = 0; layer < _stateHashes.Length; layer++)
            {
                if (_stateHashes[layer] != 0)
                    _animator.Play(_stateHashes[layer], layer, _stateTimes[layer]);
                _animator.SetLayerWeight(layer, _layerWeights[layer]);
            }
            _animator.Update(0f);
            _animator.enabled = _enabled;
        }
    }

    internal sealed class Gate1LegacyAnimationStimulusScope : IDisposable
    {
        private readonly List<Gate1AnimatorSnapshot> _snapshots =
            new List<Gate1AnimatorSnapshot>(2);

        public static bool TryBegin(
            IReadOnlyList<Component> presentations,
            out Gate1LegacyAnimationStimulusScope scope,
            out string failure)
        {
            scope = new Gate1LegacyAnimationStimulusScope();
            if (presentations == null || presentations.Count == 0)
            {
                failure = "Legacy animation capture found no presentation owner.";
                return false;
            }
            for (int index = 0; index < presentations.Count; index++)
            {
                Animator animator = Gate1Reflection.GetProperty(
                    presentations[index],
                    "Animator") as Animator;
                if (animator == null || !animator.enabled) continue;
                AnimatorStateInfo state = animator.IsInTransition(0)
                    ? animator.GetNextAnimatorStateInfo(0)
                    : animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == 0) continue;
                scope._snapshots.Add(new Gate1AnimatorSnapshot(animator));
                animator.Play(
                    state.fullPathHash,
                    0,
                    Mathf.Repeat(state.normalizedTime + 0.45f, 1f));
                animator.Update(0f);
            }
            if (scope._snapshots.Count == 0)
            {
                failure = "Legacy animation capture could not apply its deterministic phase-change stimulus.";
                return false;
            }
            failure = string.Empty;
            return true;
        }

        public void Dispose()
        {
            for (int index = _snapshots.Count - 1; index >= 0; index--)
                _snapshots[index].Restore();
            _snapshots.Clear();
        }
    }

    /// <summary>
    /// Reflection is intentionally confined to this optional evidence bridge so
    /// the rendering branch compiles before A1 is merged. Missing A1 contracts
    /// fail the capture with exact type/method names instead of silently falling
    /// back to the legacy image.
    /// </summary>
    public sealed class Gate1AnimationCaptureScope : IDisposable
    {
        private const string GraphTypeName =
            "Elemental.Presentation.Animation.EarthAnimationGraph, Elemental.Presentation";
        private const string ProfileTypeName =
            "Elemental.Presentation.Animation.EarthAnimationGraphProfile, Elemental.Presentation";

        private sealed class Entry
        {
            public Component Presentation;
            public Animator Animator;
            public Component OriginalGraph;
            public UnityEngine.Object OriginalProfile;
            public object OriginalPresentationGraph;
            public Gate1AnimatorSnapshot AnimatorSnapshot;
            public Component CaptureGraph;
        }

        private readonly List<Entry> _entries = new List<Entry>(2);
        private ScriptableObject _captureProfile;
        private Type _graphType;
        private bool _disposed;

        public bool RestoreSucceeded { get; private set; } = true;

        private Gate1AnimationCaptureScope()
        {
        }

        public static bool TryBegin(
            IReadOnlyList<Component> presentations,
            out Gate1AnimationCaptureScope scope,
            out string failure)
        {
            scope = null;
            Type graphType = Gate1Reflection.RequireType(GraphTypeName);
            Type profileType = Gate1Reflection.RequireType(ProfileTypeName);
            if (graphType == null || profileType == null)
            {
                failure = "A1 capture requires EarthAnimationGraph and EarthAnimationGraphProfile from Elemental.Presentation.";
                return false;
            }
            if (presentations == null || presentations.Count == 0)
            {
                failure = "A1 capture found no HumanoidCharacterPresentation owner.";
                return false;
            }

            var candidate = new Gate1AnimationCaptureScope
            {
                _graphType = graphType,
                _captureProfile = ScriptableObject.CreateInstance(profileType)
            };
            candidate._captureProfile.name = "Gate1 Transient Animation Graph Profile";
            candidate._captureProfile.hideFlags = HideFlags.DontSave;
            if (!Gate1Reflection.TrySetField(
                    candidate._captureProfile,
                    "usePlayablesAnimationGraph",
                    true) ||
                !Gate1Reflection.TrySetField(
                    candidate._captureProfile,
                    "usePoseInertialization",
                    true))
            {
                candidate.Dispose();
                failure = "A1 profile no longer exposes the expected transient feature fields.";
                return false;
            }

            try
            {
                for (int index = 0; index < presentations.Count; index++)
                {
                    Component presentation = presentations[index];
                    if (presentation == null) continue;
                    Animator animator = Gate1Reflection.GetProperty(
                        presentation,
                        "Animator") as Animator;
                    MethodInfo setProfile = Gate1Reflection.FindMethod(
                        presentation.GetType(),
                        "SetAnimationGraphProfile",
                        1);
                    if (animator == null || setProfile == null)
                    {
                        failure = $"A1 capture owner '{presentation.name}' lacks Animator or SetAnimationGraphProfile.";
                        candidate.Dispose();
                        return false;
                    }

                    Component originalGraph = presentation.GetComponent(graphType);
                    var entry = new Entry
                    {
                        Presentation = presentation,
                        Animator = animator,
                        OriginalGraph = originalGraph,
                        OriginalProfile = Gate1Reflection.GetField(
                            presentation,
                            "animationGraphProfile") as UnityEngine.Object,
                        OriginalPresentationGraph = Gate1Reflection.GetField(
                            presentation,
                            "animationGraph"),
                        AnimatorSnapshot = new Gate1AnimatorSnapshot(animator)
                    };
                    candidate._entries.Add(entry);
                    setProfile.Invoke(presentation, new object[] { candidate._captureProfile });
                    entry.CaptureGraph = presentation.GetComponent(graphType);
                    if (entry.CaptureGraph == null)
                    {
                        failure = $"A1 did not create an EarthAnimationGraph for '{presentation.name}'.";
                        candidate.Dispose();
                        return false;
                    }
                }
            }
            catch (TargetInvocationException exception)
            {
                candidate.Dispose();
                Exception cause = exception.InnerException ?? exception;
                failure = $"A1 transient configuration threw {cause.GetType().Name}: {cause.Message}";
                return false;
            }
            catch (Exception exception)
            {
                candidate.Dispose();
                failure = $"A1 transient configuration failed: {exception.GetType().Name}: {exception.Message}";
                return false;
            }

            if (candidate._entries.Count == 0)
            {
                candidate.Dispose();
                failure = "A1 capture had no valid presentation entries.";
                return false;
            }
            scope = candidate;
            failure = string.Empty;
            return true;
        }

        public bool TryTriggerDeterministicInertialization(
            out int accepted,
            out string failure)
        {
            accepted = 0;
            try
            {
                for (int index = 0; index < _entries.Count; index++)
                {
                    Entry entry = _entries[index];
                    if (entry.CaptureGraph == null || entry.Animator == null) continue;
                    AnimatorStateInfo state = entry.Animator.GetCurrentAnimatorStateInfo(0);
                    if (state.fullPathHash == 0) continue;
                    MethodInfo play = Gate1Reflection.FindMethod(_graphType, "Play", 3);
                    MethodInfo begin = Gate1Reflection.FindMethod(
                        _graphType,
                        "BeginInertialization",
                        1);
                    if (play == null || begin == null) continue;
                    float destinationPhase = Mathf.Repeat(state.normalizedTime + 0.45f, 1f);
                    play.Invoke(entry.CaptureGraph, new object[]
                    {
                        state.fullPathHash,
                        0,
                        destinationPhase
                    });
                    object result = begin.Invoke(entry.CaptureGraph, new object[] { 0.4f });
                    if (result is bool wasAccepted && wasAccepted) accepted++;
                }
                failure = accepted > 0
                    ? string.Empty
                    : "A1 accepted no deterministic inertialization request.";
                return accepted > 0;
            }
            catch (TargetInvocationException exception)
            {
                Exception cause = exception.InnerException ?? exception;
                failure = $"A1 inertialization stimulus threw {cause.GetType().Name}: {cause.Message}";
                return false;
            }
            catch (Exception exception)
            {
                failure = $"A1 inertialization stimulus failed: {exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        public void AccumulateEvidence(Gate1CaptureFrameEvidence evidence)
        {
            if (evidence == null) return;
            for (int index = 0; index < _entries.Count; index++)
            {
                Component graph = _entries[index].CaptureGraph;
                if (graph == null) continue;
                if (Gate1Reflection.GetBoolean(graph, "IsActive"))
                    evidence.animationGraphActiveFrames++;
                object diagnostics = Gate1Reflection.GetProperty(graph, "Diagnostics");
                if (Gate1Reflection.GetBoolean(diagnostics, "TopologyValid"))
                    evidence.animationTopologyValidFrames++;
                if (Gate1Reflection.GetBoolean(diagnostics, "InertiaActive"))
                    evidence.animationInertiaActiveFrames++;
                evidence.animationTransitionRequests += Gate1Reflection.GetInt32(
                    diagnostics,
                    "TransitionRequestCount");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int index = _entries.Count - 1; index >= 0; index--)
            {
                Entry entry = _entries[index];
                try
                {
                    if (entry.OriginalProfile != null)
                    {
                        MethodInfo setProfile = Gate1Reflection.FindMethod(
                            entry.Presentation.GetType(),
                            "SetAnimationGraphProfile",
                            1);
                        setProfile?.Invoke(
                            entry.Presentation,
                            new object[] { entry.OriginalProfile });
                    }
                    if (entry.OriginalGraph == null && entry.CaptureGraph != null)
                    {
                        if (entry.CaptureGraph is Behaviour behaviour)
                            behaviour.enabled = false;
                        UnityEngine.Object.Destroy(entry.CaptureGraph);
                    }
                    Gate1Reflection.TrySetField(
                        entry.Presentation,
                        "animationGraphProfile",
                        entry.OriginalProfile);
                    Gate1Reflection.TrySetField(
                        entry.Presentation,
                        "animationGraph",
                        entry.OriginalPresentationGraph);
                    entry.AnimatorSnapshot.Restore();
                }
                catch (Exception exception)
                {
                    RestoreSucceeded = false;
                    Debug.LogError(
                        $"[Elemental] Gate1 could not restore A1 owner '{entry.Presentation?.name}': {exception.Message}");
                }
            }
            _entries.Clear();
            if (_captureProfile != null)
            {
                UnityEngine.Object.Destroy(_captureProfile);
                _captureProfile = null;
            }
        }
    }

    internal sealed class Gate1CaptureOwnerMarker : MonoBehaviour
    {
    }

    /// <summary>
    /// Transient P1 profile/owner-group adapter. Its sample database is built at
    /// runtime from the currently authored Knockdown Recovery Animator state and
    /// is never written back to the profile asset, controller, prefab or scene.
    /// </summary>
    public sealed class Gate1PhysicalAnimationCaptureScope : IDisposable
    {
        private const string ProfileTypeName =
            "Elemental.Runtime.Characters.EarthPhysicalAnimationProfile, Elemental.Runtime";
        private const string SampleTypeName =
            "Elemental.Runtime.Characters.EarthRecoveryPoseSampleAuthoring, Elemental.Runtime";
        private const string MarkerTypeName =
            "Elemental.Runtime.Characters.EarthRecoveryMarkerAuthoring, Elemental.Runtime";
        private const string OrientationTypeName =
            "Elemental.Simulation.Characters.EarthRecoveryOrientation, Elemental.Simulation";
        private const string RecoveryStatePath = "Base Layer.Knockdown Recovery";

        private readonly HumanoidRagdollRig _rig;
        private readonly MethodInfo _configure;
        private readonly object _originalProfile;
        private readonly object _originalFeetOwners;
        private readonly object _originalControlOwners;
        private readonly object _originalProceduralOwners;
        private readonly ScriptableObject _captureProfile;
        private readonly GameObject _ownerObject;
        private readonly Behaviour[] _feetOwners;
        private readonly Behaviour[] _controlOwners;
        private readonly Behaviour[] _proceduralOwners;
        private readonly Rigidbody _motorRootBody;
        private readonly Transform _hips;
        private readonly Vector3 _sampledPelvisOffsetLocal;
        private Vector3 _livePelvisBeforeHandoff;
        private Vector3 _continuityUp;
        private bool _continuityOriginCaptured;
        private bool _pelvisContinuityVerified;
        private float _pelvisContinuityErrorMeters;
        private bool _disposed;

        public bool RestoreSucceeded { get; private set; } = true;

        private Gate1PhysicalAnimationCaptureScope(
            HumanoidRagdollRig rig,
            MethodInfo configure,
            object originalProfile,
            object originalFeetOwners,
            object originalControlOwners,
            object originalProceduralOwners,
            ScriptableObject captureProfile,
            GameObject ownerObject,
            Behaviour[] feetOwners,
            Behaviour[] controlOwners,
            Behaviour[] proceduralOwners,
            Rigidbody motorRootBody,
            Transform hips,
            Vector3 sampledPelvisOffsetLocal)
        {
            _rig = rig;
            _configure = configure;
            _originalProfile = originalProfile;
            _originalFeetOwners = originalFeetOwners;
            _originalControlOwners = originalControlOwners;
            _originalProceduralOwners = originalProceduralOwners;
            _captureProfile = captureProfile;
            _ownerObject = ownerObject;
            _feetOwners = feetOwners;
            _controlOwners = controlOwners;
            _proceduralOwners = proceduralOwners;
            _motorRootBody = motorRootBody;
            _hips = hips;
            _sampledPelvisOffsetLocal = sampledPelvisOffsetLocal;
        }

        public static bool TryBegin(
            HumanoidRagdollRig rig,
            Animator animator,
            out Gate1PhysicalAnimationCaptureScope scope,
            out string failure)
        {
            scope = null;
            Type profileType = Gate1Reflection.RequireType(ProfileTypeName);
            Type sampleType = Gate1Reflection.RequireType(SampleTypeName);
            Type markerType = Gate1Reflection.RequireType(MarkerTypeName);
            Type orientationType = Gate1Reflection.RequireType(OrientationTypeName);
            if (profileType == null || sampleType == null || markerType == null ||
                orientationType == null)
            {
                failure = "P1 capture requires the physical-animation profile, recovery sample, marker and orientation contracts.";
                return false;
            }
            if (rig == null || animator == null || !animator.isHuman)
            {
                failure = "P1 capture requires one explicit built Humanoid ragdoll and Humanoid Animator.";
                return false;
            }
            MethodInfo configure = Gate1Reflection.FindMethod(
                rig.GetType(),
                "ConfigurePhysicalAnimation",
                4);
            if (configure == null)
            {
                failure = "P1 HumanoidRagdollRig.ConfigurePhysicalAnimation was not found.";
                return false;
            }

            ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
            profile.name = "Gate1 Transient Physical Animation Profile";
            profile.hideFlags = HideFlags.DontSave;
            try
            {
                if (!TryConfigureRecoveryProfile(
                        profile,
                        rig,
                        animator,
                        sampleType,
                        markerType,
                        orientationType,
                        out Vector3 sampledPelvisOffsetLocal,
                        out failure))
                {
                    UnityEngine.Object.Destroy(profile);
                    return false;
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Object.Destroy(profile);
                Exception cause = exception is TargetInvocationException invocation &&
                                  invocation.InnerException != null
                    ? invocation.InnerException
                    : exception;
                failure = $"P1 transient recovery sampling failed: {cause.GetType().Name}: {cause.Message}";
                return false;
            }

            var ownerObject = new GameObject("Gate1 Transient Recovery Owners")
            {
                hideFlags = HideFlags.DontSave
            };
            var feetOwners = new Behaviour[]
            {
                ownerObject.AddComponent<Gate1CaptureOwnerMarker>()
            };
            var controlOwners = new Behaviour[]
            {
                ownerObject.AddComponent<Gate1CaptureOwnerMarker>()
            };
            var proceduralOwners = new Behaviour[]
            {
                ownerObject.AddComponent<Gate1CaptureOwnerMarker>()
            };
            scope = new Gate1PhysicalAnimationCaptureScope(
                rig,
                configure,
                Gate1Reflection.GetField(rig, "physicalAnimationProfile"),
                Gate1Reflection.GetField(rig, "recoveryFeetOwners"),
                Gate1Reflection.GetField(rig, "recoveryControlOwners"),
                Gate1Reflection.GetField(rig, "recoveryProceduralOwners"),
                profile,
                ownerObject,
                feetOwners,
                controlOwners,
                proceduralOwners,
                Gate1Reflection.GetField(rig, "motorRootBody") as Rigidbody,
                animator.GetBoneTransform(HumanBodyBones.Hips),
                sampledPelvisOffsetLocal);
            failure = string.Empty;
            return true;
        }

        public bool TryConfigureLegacy(out string failure)
        {
            return TryConfigure(new object[]
            {
                null,
                Array.Empty<Behaviour>(),
                Array.Empty<Behaviour>(),
                Array.Empty<Behaviour>()
            }, out failure);
        }

        public bool TryConfigurePoseMatched(out string failure)
        {
            return TryConfigure(new object[]
            {
                _captureProfile,
                _feetOwners,
                _controlOwners,
                _proceduralOwners
            }, out failure);
        }

        public void AccumulateLegacyEvidence(Gate1CaptureFrameEvidence evidence)
        {
            if (evidence == null) return;
            if (_rig.IsRecoveringToAnimation &&
                !Gate1Reflection.GetBoolean(_rig, "UsedPoseMatchedRecovery"))
                evidence.recoveryLegacyFrames++;
        }

        public bool TryConfirmLegacyRecovery(out string failure)
        {
            bool poseMatched = Gate1Reflection.GetBoolean(
                _rig,
                "UsedPoseMatchedRecovery");
            if (_rig.IsRecoveringToAnimation && !poseMatched)
            {
                failure = string.Empty;
                return true;
            }
            failure =
                "The legacy recovery leg did not enter its authored legacy handoff state.";
            return false;
        }

        public void AccumulatePoseMatchedEvidence(Gate1CaptureFrameEvidence evidence)
        {
            if (evidence == null) return;
            if (Gate1Reflection.GetBoolean(_rig, "UsedPoseMatchedRecovery"))
                evidence.recoveryPoseMatchedFrames++;
            if (Gate1Reflection.GetBoolean(_rig, "RecoveryStateVerifiedAfterEvent"))
                evidence.recoveryStateVerifiedFrames++;
            if (Gate1Reflection.GetBoolean(_rig, "LastRecoveryClearanceSucceeded"))
                evidence.recoveryClearanceSucceededFrames++;
            evidence.recoveryIsolatedSamplerFrames++;
            if (_pelvisContinuityVerified)
                evidence.recoveryPelvisContinuityVerifiedFrames++;
            evidence.recoveryPelvisContinuityErrorMeters =
                _pelvisContinuityErrorMeters;
        }

        public bool TryCapturePoseMatchedContinuityOrigin(
            Vector3 localUp,
            out string failure)
        {
            _continuityOriginCaptured = false;
            _pelvisContinuityVerified = false;
            _pelvisContinuityErrorMeters = float.PositiveInfinity;
            if (!_rig.IsRagdollActive || _hips == null || _motorRootBody == null)
            {
                failure = "P1 continuity evidence requires an active ragdoll Hips and motor-root body.";
                return false;
            }
            _livePelvisBeforeHandoff = _hips.position;
            _continuityUp = localUp.sqrMagnitude > 0.25f
                ? localUp.normalized
                : _rig.transform.up;
            _continuityOriginCaptured = true;
            failure = string.Empty;
            return true;
        }

        public bool TryConfirmPoseMatchedRecovery(out string failure)
        {
            bool usedPoseMatched = Gate1Reflection.GetBoolean(
                _rig,
                "UsedPoseMatchedRecovery");
            bool stateVerified = Gate1Reflection.GetBoolean(
                _rig,
                "RecoveryStateVerifiedAfterEvent");
            bool clearanceSucceeded = Gate1Reflection.GetBoolean(
                _rig,
                "LastRecoveryClearanceSucceeded");
            if (_continuityOriginCaptured && usedPoseMatched && clearanceSucceeded)
            {
                float clearanceLift = GetSingle(
                    Gate1Reflection.GetProperty(_rig, "LastRecoveryClearanceLiftMeters"));
                Vector3 reconstructed = Gate1RecoverySampleMath.ReconstructPreClearancePelvis(
                    _motorRootBody.position,
                    _motorRootBody.rotation,
                    _sampledPelvisOffsetLocal,
                    _continuityUp,
                    clearanceLift);
                _pelvisContinuityErrorMeters = Vector3.Distance(
                    reconstructed,
                    _livePelvisBeforeHandoff);
                _pelvisContinuityVerified =
                    !float.IsNaN(_pelvisContinuityErrorMeters) &&
                    !float.IsInfinity(_pelvisContinuityErrorMeters) &&
                    _pelvisContinuityErrorMeters <=
                        Gate1RecoverySampleMath.MaximumPelvisContinuityErrorMeters;
            }
            if (usedPoseMatched && stateVerified && clearanceSucceeded &&
                _pelvisContinuityVerified)
            {
                failure = string.Empty;
                return true;
            }
            bool hasLiveSupport = Gate1Reflection.GetBoolean(
                _rig,
                "RecoveryHasLiveSupport");
            failure =
                "P1 pose-matched recovery fell back to legacy or rejected its authored state " +
                $"(usedPoseMatched={usedPoseMatched}, stateVerified={stateVerified}, " +
                $"clearanceSucceeded={clearanceSucceeded}, liveSupport={hasLiveSupport}, " +
                $"isolatedSampler=True, pelvisContinuityErrorMeters=" +
                $"{_pelvisContinuityErrorMeters:F6}, continuityLimitMeters=" +
                $"{Gate1RecoverySampleMath.MaximumPelvisContinuityErrorMeters:F6}).";
            return false;
        }

        private static float GetSingle(object value)
        {
            return value is float number ? number : float.NaN;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _rig.ResetToAnimated();
                _configure.Invoke(_rig, new[]
                {
                    _originalProfile,
                    _originalFeetOwners,
                    _originalControlOwners,
                    _originalProceduralOwners
                });
            }
            catch (Exception exception)
            {
                RestoreSucceeded = false;
                Debug.LogError(
                    $"[Elemental] Gate1 could not restore P1 owner '{_rig?.name}': {exception.Message}");
            }
            if (_ownerObject != null)
            {
                _ownerObject.SetActive(false);
                UnityEngine.Object.Destroy(_ownerObject);
            }
            if (_captureProfile != null) UnityEngine.Object.Destroy(_captureProfile);
        }

        private bool TryConfigure(object[] arguments, out string failure)
        {
            try
            {
                _configure.Invoke(_rig, arguments);
                failure = string.Empty;
                return true;
            }
            catch (TargetInvocationException exception)
            {
                Exception cause = exception.InnerException ?? exception;
                failure = $"P1 transient configuration threw {cause.GetType().Name}: {cause.Message}";
                return false;
            }
            catch (Exception exception)
            {
                failure = $"P1 transient configuration failed: {exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        private static bool TryConfigureRecoveryProfile(
            ScriptableObject profile,
            HumanoidRagdollRig rig,
            Animator animator,
            Type sampleType,
            Type markerType,
            Type orientationType,
            out Vector3 sampledPelvisOffsetLocal,
            out string failure)
        {
            sampledPelvisOffsetLocal = Vector3.zero;
            int stateHash = Animator.StringToHash(RecoveryStatePath);
            if (!animator.HasState(0, stateHash))
            {
                failure = $"The active Animator does not contain authored state '{RecoveryStatePath}'.";
                return false;
            }
            Rigidbody motorRootBody = Gate1Reflection.GetField(
                rig,
                "motorRootBody") as Rigidbody;
            if (motorRootBody == null)
            {
                failure =
                    "The authored recovery state cannot be sampled because the motor-root body is missing.";
                return false;
            }

            ConstructorInfo markerConstructor = markerType.GetConstructor(new[]
            {
                typeof(float), typeof(float), typeof(float)
            });
            ConstructorInfo sampleConstructor = FindSampleConstructor(sampleType);
            MethodInfo configureRecovery = Gate1Reflection.FindMethod(
                profile.GetType(),
                "ConfigureRecovery",
                3);
            if (markerConstructor == null || sampleConstructor == null ||
                configureRecovery == null)
            {
                failure = "P1 recovery authoring constructors or ConfigureRecovery changed incompatibly.";
                return false;
            }

            if (!Gate1IsolatedRecoveryPoseSampler.TrySample(
                    animator,
                    motorRootBody,
                    stateHash,
                    0.55f,
                    out Gate1RecoveryPoseFixture fixture,
                    out failure))
                return false;
            sampledPelvisOffsetLocal = fixture.PelvisOffset;
            Array samples = Array.CreateInstance(sampleType, 4);
            object markers = markerConstructor.Invoke(new object[] { 0.56f, 0.80f, 0.95f });
            for (int orientation = 1; orientation <= 4; orientation++)
            {
                object sample = sampleConstructor.Invoke(new object[]
                {
                    0xA1000000u + (uint)orientation,
                    RecoveryStatePath,
                    Enum.ToObject(orientationType, orientation),
                    0.55f,
                    fixture.PelvisOffset,
                    fixture.ChestOffset,
                    fixture.LeftHandOffset,
                    fixture.RightHandOffset,
                    fixture.LeftFootOffset,
                    fixture.RightFootOffset,
                    fixture.ChestOutward,
                    markers
                });
                samples.SetValue(sample, orientation - 1);
            }

            configureRecovery.Invoke(profile, new object[] { true, samples, 1.4f });
            failure = string.Empty;
            return true;
        }

        private static ConstructorInfo FindSampleConstructor(Type sampleType)
        {
            ConstructorInfo[] constructors = sampleType.GetConstructors();
            for (int index = 0; index < constructors.Length; index++)
                if (constructors[index].GetParameters().Length == 12)
                    return constructors[index];
            return null;
        }
    }
}
