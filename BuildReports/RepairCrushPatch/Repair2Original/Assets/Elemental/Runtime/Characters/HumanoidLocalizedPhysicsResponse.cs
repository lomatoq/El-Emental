using System;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Runtime.Characters
{
    /// <summary>
    /// Animation supplies kinematic targets; PhysX supplies the local hit offset.
    /// Separate proxies leave the visible skeleton kinematic until full ragdoll.
    /// This is the final hit-pose writer, after foot contact and before accessories.
    /// </summary>
    [DefaultExecutionOrder(2500), DisallowMultipleComponent]
    public sealed class HumanoidLocalizedPhysicsResponse : MonoBehaviour
    {
        private static readonly ProfilerMarker StepMarker = new("Elemental.Character.LocalPhysicsStep");
        private static readonly ProfilerMarker PoseMarker = new("Elemental.Character.LocalPhysicsPose");
        private const int Count = EarthLocalizedPhysicsResponse.BoneCount;
        private static readonly float[] Masses = { 7.2f, 8.4f, 3.4f, 2f, 1.4f, 2f, 1.4f, 4.2f, 3f, 4.2f, 3f };
        private readonly Transform[] _bones = new Transform[Count];
        private readonly Rigidbody[] _bodies = new Rigidbody[Count], _anchors = new Rigidbody[Count];
        private readonly ConfigurableJoint[] _joints = new ConfigurableJoint[Count];
        private readonly Vector3[] _targets = new Vector3[Count], _baseLocalPosition = new Vector3[Count];
        private readonly Quaternion[] _rotations = new Quaternion[Count], _baseLocalRotation = new Quaternion[Count];
        private readonly Vector3[] _poseVelocity = new Vector3[Count], _poseAngularVelocity = new Vector3[Count];
        private readonly float[] _started = new float[Count];
        private readonly bool[] _active = new bool[Count], _wrote = new bool[Count];
        private Transform _motionRoot;
        private HumanoidRagdollRig _ragdoll;
        private CharacterImpactResponseProfile _profile;
        private GameObject _proxyRoot;
        private Vector3 _sampleRootPosition, _previousPhysicsRootPosition;
        private Quaternion _sampleRootRotation, _previousPhysicsRootRotation;
        private Vector3 _sampleRootVelocity;
        private bool _built, _hasPose, _suspended;
        private uint _lastResponseId;
        private float _contactSearchDepth;

        public bool IsReady => _built;
        public int AcceptedImpactCount { get; private set; }
        public uint LastResponseId => _lastResponseId;
        public int LastHitRegion { get; private set; } = -1;
        public float PeakDisplacementMeters { get; private set; }
        public float CurrentMaximumAngle { get; private set; }
        public bool LeftLegActive => _active[0] || _active[7] || _active[8];
        public bool RightLegActive => _active[0] || _active[9] || _active[10];
        public bool HasActiveResponse
        {
            get { for (int i = 0; i < Count; i++) if (_active[i]) return true; return false; }
        }
        public Rigidbody ProxyBody(int region) => region >= 0 && region < Count ? _bodies[region] : null;
        public Transform Bone(int region) => region >= 0 && region < Count ? _bones[region] : null;
        public bool RegionActive(int region) => region >= 0 && region < Count && _active[region];
        private EarthLocalizedPhysicsTuning Tuning => _profile != null ? _profile.PhysicalTuning : EarthLocalizedPhysicsTuning.Default;

        public void Configure(Transform[] bones, Transform motionRoot,
            CharacterImpactResponseProfile profile, HumanoidRagdollRig ragdoll = null)
        {
            if (bones == null || bones.Length != Count || motionRoot == null)
                throw new ArgumentException("Local physical response needs eleven ordered Humanoid bones and an explicit motion root.");
            for (int i = 0; i < Count; i++)
            {
                if (bones[i] == null) throw new ArgumentException($"Local physical response bone {i} is missing.");
                _bones[i] = bones[i];
            }
            _motionRoot = motionRoot;
            var capsule = motionRoot.GetComponent<CapsuleCollider>();
            _contactSearchDepth = capsule != null
                ? Mathf.Clamp(capsule.radius * 2f * Mathf.Max(Mathf.Abs(motionRoot.lossyScale.x), Mathf.Abs(motionRoot.lossyScale.z)), .1f, 1.5f)
                : 0f;
            _profile = profile;
            _ragdoll = ragdoll;
            if (!Application.isPlaying) return;
            if (!_built) BuildProxies();
            ResetToAnimation();
        }

        public void ConfigureProfile(CharacterImpactResponseProfile profile) => _profile = profile;

        private void BuildProxies()
        {
            _proxyRoot = new GameObject(name + " Local hit physics") { hideFlags = HideFlags.DontSave };
            SceneManager.MoveGameObjectToScene(_proxyRoot, gameObject.scene);
            EarthLocalizedPhysicsTuning tuning = Tuning;
            for (int i = 0; i < Count; i++)
            {
                var anchor = new GameObject(((HumanoidRagdollBoneRole)i) + " animation target");
                anchor.transform.SetParent(_proxyRoot.transform, false);
                anchor.transform.SetPositionAndRotation(_bones[i].position, _bones[i].rotation);
                _anchors[i] = anchor.AddComponent<Rigidbody>();
                _anchors[i].isKinematic = true;
                _anchors[i].useGravity = false;
                _anchors[i].detectCollisions = false;
                var proxy = new GameObject(((HumanoidRagdollBoneRole)i) + " physical response");
                proxy.transform.SetParent(_proxyRoot.transform, false);
                proxy.transform.SetPositionAndRotation(_bones[i].position, _bones[i].rotation);
                Rigidbody body = proxy.AddComponent<Rigidbody>();
                _bodies[i] = body;
                body.mass = Masses[i];
                body.useGravity = false;
                body.detectCollisions = false;
                body.interpolation = RigidbodyInterpolation.None;
                body.linearDamping = .25f;
                body.angularDamping = .3f;
                body.maxAngularVelocity = 15f;
                body.solverIterations = 10;
                body.solverVelocityIterations = 4;
                body.centerOfMass = Vector3.up * .1f;
                body.inertiaTensor = new Vector3(.018f, .012f, .018f) * body.mass;
                body.inertiaTensorRotation = Quaternion.identity;
                body.isKinematic = true;
                ConfigurableJoint joint = proxy.AddComponent<ConfigurableJoint>();
                _joints[i] = joint;
                joint.connectedBody = _anchors[i];
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = joint.connectedAnchor = Vector3.zero;
                joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Limited;
                float2 limits = tuning.LimitsFor(i);
                joint.linearLimit = new SoftJointLimit { limit = limits.x };
                joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Limited;
                joint.lowAngularXLimit = new SoftJointLimit { limit = -limits.y };
                joint.highAngularXLimit = joint.angularYLimit = joint.angularZLimit =
                    new SoftJointLimit { limit = limits.y };
                joint.rotationDriveMode = RotationDriveMode.Slerp;
                joint.targetRotation = Quaternion.identity;
                joint.enableCollision = false;
                joint.projectionMode = JointProjectionMode.PositionAndRotation;
                joint.projectionDistance = .15f;
                joint.projectionAngle = 45f;
                SetDrive(i, 1f, in tuning);
            }
            _built = true;
            CaptureTargets(0f);
        }

        public bool ApplyHit(in EarthWorldResponseEvent response, float reactionVelocity)
        {
            if (!_built || _suspended || !isActiveAndEnabled ||
                (_ragdoll != null && (_ragdoll.IsRagdollActive || _ragdoll.IsRecoveringToAnimation)) ||
                !EarthLocalizedPhysicsResponse.IsLocal(response.Response) ||
                (_profile != null && !_profile.LocalizedHitReaction) ||
                response.ResponseId != 0u && response.ResponseId == _lastResponseId) return false;
            Vector3 point = new(response.Point.x, response.Point.y, response.Point.z);
            Vector3 direction = new(response.Direction.x, response.Direction.y, response.Direction.z);
            if (direction.sqrMagnitude < .0001f) direction = _motionRoot.forward;
            direction.Normalize();
            int nearest = response.HitRegion != EarthHitRegion.Unspecified ? (int)response.HitRegion : NearestRegion(point, direction);
            float velocity = EarthLocalizedPhysicsResponse.LocalVelocity(reactionVelocity, response.Response);
            EarthLocalizedPhysicsTuning tuning = Tuning;
            Activate(nearest, point, direction * velocity, in tuning);
            int parent = EarthLocalizedPhysicsResponse.Parent(nearest);
            if (parent >= 0 && tuning.ParentTransfer > 0f)
                Activate(parent, point, direction * (velocity * tuning.ParentTransfer), in tuning);
            _lastResponseId = response.ResponseId;
            LastHitRegion = nearest;
            PeakDisplacementMeters = 0f;
            AcceptedImpactCount++;
            return true;
        }

        public EarthHitRegion ResolveHitRegion(Vector3 point, Vector3 direction) =>
            _built ? (EarthHitRegion)NearestRegion(point, direction) : EarthHitRegion.Unspecified;

        private int NearestRegion(Vector3 point, Vector3 direction)
        {
            int nearest = 0;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < Count; i++)
            {
                float candidate = EarthLocalizedPhysicsResponse.RegionContactScore(
                    (float3)_bones[i].position, (float3)point, (float3)direction, _contactSearchDepth);
                if (candidate < distance) { distance = candidate; nearest = i; }
            }
            return nearest;
        }

        private void Activate(int index, Vector3 point, Vector3 velocity, in EarthLocalizedPhysicsTuning tuning)
        {
            Rigidbody body = _bodies[index];
            if (!_active[index])
            {
                _anchors[index].position = body.position = _bones[index].position;
                _anchors[index].rotation = body.rotation = _bones[index].rotation;
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            _active[index] = true;
            _started[index] = Time.time;
            float2 limits = tuning.LimitsFor(index);
            var joint = _joints[index];
            joint.linearLimit = new SoftJointLimit { limit = limits.x };
            joint.lowAngularXLimit = new SoftJointLimit { limit = -limits.y };
            joint.highAngularXLimit = joint.angularYLimit = joint.angularZLimit = new SoftJointLimit { limit = limits.y };
            SetDrive(index, tuning.WeakDriveScale, in tuning);
            Vector3 contact = body.position + Vector3.ClampMagnitude(point - body.position, .3f);
            body.AddForceAtPosition(velocity, contact, ForceMode.VelocityChange);
            body.WakeUp();
        }

        // Remove our previous render offset before Animator/EAMM samples the next
        // base pose. This also prevents accumulation with paused/partial clips.
        private void Update() => RestoreBasePose();

        private void RestoreBasePose()
        {
            if (!_built || _suspended) return;
            for (int i = 0; i < Count; i++)
                if (_wrote[i] && _bones[i] != null)
                {
                    _bones[i].localPosition = _baseLocalPosition[i];
                    _bones[i].localRotation = _baseLocalRotation[i];
                    _wrote[i] = false;
                }
        }

        private void FixedUpdate()
        {
            if (!_built || _suspended) return;
            using (StepMarker.Auto())
            {
                EarthLocalizedPhysicsTuning tuning = Tuning;
                Quaternion rootDelta = _motionRoot.rotation * Quaternion.Inverse(_previousPhysicsRootRotation);
                Quaternion sampleDelta = _motionRoot.rotation * Quaternion.Inverse(_sampleRootRotation);
                bool teleported = Vector3.Distance(_motionRoot.position, _previousPhysicsRootPosition) > 2f ||
                                  Quaternion.Angle(_motionRoot.rotation, _previousPhysicsRootRotation) > 60f;
                if (teleported) { ResetToAnimation(); return; }
                for (int i = 0; i < Count; i++)
                {
                    if (!_active[i]) continue;
                    float age = Time.time - _started[i];
                    if (age >= tuning.WeakDriveSeconds + tuning.RecoverySeconds)
                    {
                        _active[i] = false;
                        _bodies[i].isKinematic = true;
                        continue;
                    }
                    // Transport the small physical reaction with the motor frame;
                    // running must not drag the proxy behind its animation target.
                    Rigidbody body = _bodies[i];
                    body.position = _motionRoot.position + rootDelta * (body.position - _previousPhysicsRootPosition);
                    body.rotation = rootDelta * body.rotation;
                    body.linearVelocity = rootDelta * body.linearVelocity;
                    body.angularVelocity = rootDelta * body.angularVelocity;
                    _anchors[i].MovePosition(_motionRoot.position + sampleDelta * (_targets[i] - _sampleRootPosition));
                    _anchors[i].MoveRotation(sampleDelta * _rotations[i]);
                    SetDrive(i, EarthLocalizedPhysicsResponse.DriveScale(age, in tuning), in tuning);
                }
                _previousPhysicsRootPosition = _motionRoot.position;
                _previousPhysicsRootRotation = _motionRoot.rotation;
            }
        }

        private void SetDrive(int index, float scale, in EarthLocalizedPhysicsTuning tuning)
        {
            Rigidbody body = _bodies[index];
            float spring = tuning.DriveSpring * body.mass * scale;
            var linear = new JointDrive
            {
                positionSpring = spring * 3f,
                positionDamper = 2f * Mathf.Sqrt(spring * 3f * body.mass) * tuning.DriveDamping,
                maximumForce = body.mass * 1500f
            };
            var angular = new JointDrive
            {
                positionSpring = spring,
                positionDamper = 2f * Mathf.Sqrt(spring * body.inertiaTensor.x) * tuning.DriveDamping,
                maximumForce = body.mass * 300f
            };
            ConfigurableJoint joint = _joints[index];
            joint.xDrive = joint.yDrive = joint.zDrive = linear;
            joint.slerpDrive = angular;
        }

        private void LateUpdate()
        {
            if (!_built || _suspended || Time.deltaTime <= 0f) return;
            if (_ragdoll != null && (_ragdoll.IsRagdollActive || _ragdoll.IsRecoveringToAnimation)) return;
            using (PoseMarker.Auto())
            {
                CaptureTargets(Time.deltaTime);
                EarthLocalizedPhysicsTuning tuning = Tuning;
                CurrentMaximumAngle = 0f;
                for (int i = 0; i < Count; i++)
                {
                    if (!_active[i])
                    {
                        _anchors[i].position = _bodies[i].position = _targets[i];
                        _anchors[i].rotation = _bodies[i].rotation = _rotations[i];
                        continue;
                    }
                    float weight = EarthLocalizedPhysicsResponse.VisibleWeight(Time.time - _started[i], in tuning);
                    Quaternion physicalRotation = _bodies[i].rotation * Quaternion.Inverse(_anchors[i].rotation);
                    float2 limits = tuning.LimitsFor(i);
                    physicalRotation = Quaternion.RotateTowards(Quaternion.identity, physicalRotation, limits.y);
                    physicalRotation = Quaternion.Slerp(Quaternion.identity, physicalRotation, weight);
                    Vector3 physicalOffset = Vector3.ClampMagnitude(_bodies[i].position - _anchors[i].position,
                        limits.x) * weight;
                    PeakDisplacementMeters = Mathf.Max(PeakDisplacementMeters, physicalOffset.magnitude);
                    CurrentMaximumAngle = Mathf.Max(CurrentMaximumAngle, Quaternion.Angle(Quaternion.identity, physicalRotation));
                    _bones[i].SetPositionAndRotation(_targets[i] + physicalOffset, physicalRotation * _rotations[i]);
                    _wrote[i] = true;
                }
            }
        }

        private void CaptureTargets(float dt)
        {
            if (_motionRoot == null) return;
            _sampleRootVelocity = _hasPose && dt > 0f ? (_motionRoot.position - _sampleRootPosition) / dt : Vector3.zero;
            for (int i = 0; i < Count; i++)
            {
                Transform bone = _bones[i];
                if (_hasPose && dt > 0f)
                {
                    _poseVelocity[i] = Vector3.ClampMagnitude((bone.position - _targets[i]) / dt, 12f);
                    Quaternion delta = bone.rotation * Quaternion.Inverse(_rotations[i]);
                    delta.ToAngleAxis(out float angle, out Vector3 axis);
                    if (angle > 180f) angle -= 360f;
                    _poseAngularVelocity[i] = axis.sqrMagnitude > .1f && float.IsFinite(axis.x)
                        ? Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad / dt), 20f) : Vector3.zero;
                }
                _targets[i] = bone.position;
                _rotations[i] = bone.rotation;
                _baseLocalPosition[i] = bone.localPosition;
                _baseLocalRotation[i] = bone.localRotation;
            }
            _sampleRootPosition = _motionRoot.position;
            _sampleRootRotation = _motionRoot.rotation;
            if (!_hasPose)
            {
                _previousPhysicsRootPosition = _sampleRootPosition;
                _previousPhysicsRootRotation = _sampleRootRotation;
            }
            _hasPose = true;
        }

        public Vector3 HandoffLinearVelocity(int region, Vector3 rootVelocity) =>
            !_built || region < 0 || region >= Count ? rootVelocity : rootVelocity + Vector3.ClampMagnitude(
                _poseVelocity[region] - _sampleRootVelocity +
                (_active[region] ? _bodies[region].linearVelocity - _anchors[region].linearVelocity : Vector3.zero), 8f);
        public Vector3 HandoffAngularVelocity(int region, Vector3 rootAngular) =>
            !_built || region < 0 || region >= Count ? rootAngular : Vector3.ClampMagnitude(
                _poseAngularVelocity[region] + (_active[region] ? _bodies[region].angularVelocity : rootAngular), 24f);

        public void SuspendForFullRagdoll()
        {
            // Keep the currently rendered bone pose for the atomic heavy handoff.
            _suspended = true;
            for (int i = 0; i < Count; i++)
            {
                _wrote[i] = _active[i] = false;
                if (_bodies[i] != null) _bodies[i].isKinematic = true;
            }
        }

        public void ResetToAnimation()
        {
            if (!_built) return;
            RestoreBasePose();
            _suspended = false;
            _hasPose = false;
            for (int i = 0; i < Count; i++)
            {
                _wrote[i] = _active[i] = false;
                _poseVelocity[i] = _poseAngularVelocity[i] = Vector3.zero;
                _bodies[i].isKinematic = true;
                _bodies[i].position = _anchors[i].position = _bones[i].position;
                _bodies[i].rotation = _anchors[i].rotation = _bones[i].rotation;
            }
            CurrentMaximumAngle = 0f;
            CaptureTargets(0f);
        }

        private void OnDisable() { if (_built && !_suspended) ResetToAnimation(); }
        private void OnDestroy()
        {
            if (_proxyRoot != null) Destroy(_proxyRoot);
        }
    }
}
