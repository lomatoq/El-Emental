using System;
using System.Collections;
using System.Collections.Generic;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Elemental.Tests.PlayMode
{
    public sealed class ActiveRagdollRuntimeTests
    {
        private static readonly string[] OwnedAdditiveScenePaths =
        {
            "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity",
            "Assets/Elemental/Content/Scenes/CharacterFeelLab.unity"
        };

        [UnityTearDown]
        public IEnumerator EnsureOwnedAdditiveScenesFinishUnloading()
        {
            yield return null;
            for (int index = 0; index < OwnedAdditiveScenePaths.Length; index++)
            {
                Scene scene = SceneManager.GetSceneByPath(OwnedAdditiveScenePaths[index]);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                DestroySceneRoots(scene);
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) yield return unload;
            }
        }

        [UnityTest]
        public IEnumerator PoweredAssist_DefaultOffAndAcceptedMediumOwnsNoKickOrLegDrive()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = default;
            EarthPhysicalAnimationProfile profile = null;
            try
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(
                    scenePath, LoadSceneMode.Additive);
                Assert.That(load, Is.Not.Null);
                yield return load;
                yield return null;

                scene = SceneManager.GetSceneByPath(scenePath);
                ActiveRagdollPuppet puppet = FindInScene<ActiveRagdollPuppet>(scene);
                Assert.That(puppet, Is.Not.Null);
                PlanetMotor motor = puppet.GetComponent<PlanetMotor>();
                Rigidbody body = puppet.GetComponent<Rigidbody>();
                Animator animator = puppet.GetComponentInChildren<Animator>(true);
                Assert.That(motor, Is.Not.Null);
                Assert.That(body, Is.Not.Null);
                Assert.That(animator, Is.Not.Null);

                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Assert.That(leftFoot, Is.Not.Null);
                Assert.That(rightFoot, Is.Not.Null);

                puppet.ConfigurePoweredPhysicalAssist(
                    null, leftFoot, rightFoot, head, leftHand, rightHand);
                CharacterPhysicalMode legacyMode = puppet.CanonicalMode;
                EarthWorldResponseEvent disabledResponse = Response(
                    0xA001u, EarthCharacterImpactResponse.Stagger, body.worldCenterOfMass);
                EarthPoweredImpactDecision disabled = puppet.ReceiveAcceptedWorldResponse(
                    in disabledResponse);
                Assert.That(disabled.Accepted, Is.False);
                Assert.That(puppet.CanonicalMode, Is.EqualTo(legacyMode));

                ActiveRagdollJoint[] joints = FindOwnedActiveRagdollJoints(scene, puppet);
                Assert.That(joints.Length, Is.GreaterThan(0));
                int legJointCount = 0;
                for (int index = 0; index < joints.Length; index++)
                {
                    EarthBodyRegion region = RegionForAuthoredTarget(joints[index].TargetPose);
                    Assert.That(joints[index].ConfigureBodyRegion(region), Is.True,
                        joints[index].name);
                    if (region == EarthBodyRegion.Leg) legJointCount++;
                }
                Assert.That(legJointCount, Is.GreaterThan(0));

                profile = ScriptableObject.CreateInstance<EarthPhysicalAnimationProfile>();
                profile.ConfigurePoweredPhysicalAssist(true);
                puppet.ConfigurePoweredPhysicalAssist(
                    profile, leftFoot, rightFoot, head, leftHand, rightHand);
                puppet.ResetPhysicalState(body.position, body.rotation);
                for (int tick = 0; tick < 30 && !motor.HasStableSupport; tick++)
                    yield return new WaitForFixedUpdate();
                Assert.That(motor.HasStableSupport, Is.True,
                    "The accepted medium path requires PlanetMotor stable support.");

                Assert.That(puppet.PoweredAssistConfigurationValid, Is.True);

                EarthWorldResponseEvent noPlantedContact = Response(
                    0xA002u, EarthCharacterImpactResponse.Stagger, body.worldCenterOfMass);
                EarthPoweredImpactDecision fallback = puppet.ReceiveAcceptedWorldResponse(
                    in noPlantedContact);
                Assert.That(fallback.FallsBackToAgentA, Is.True);
                Assert.That(fallback.Rejection,
                    Is.EqualTo(EarthPoweredAssistRejection.NoPlantedFoot));

                puppet.SetPoweredFootContactState(true, true);
                for (int index = 0; index < joints.Length; index++)
                {
                    if (joints[index].BodyRegion != EarthBodyRegion.Leg) continue;
                    ConfigurableJoint legJoint = joints[index].GetComponent<ConfigurableJoint>();
                    SeedEveryDriveChannel(legJoint);
                    AssertEveryDriveChannelIsNonZero(legJoint, joints[index].name);
                }
                Vector3 velocityBefore = body.linearVelocity;
                EarthPoweredImpactDecision first = puppet.ReceiveAcceptedWorldResponse(
                    in noPlantedContact);
                EarthPoweredImpactDecision duplicate = puppet.ReceiveAcceptedWorldResponse(
                    in noPlantedContact);
                puppet.SetPoweredFootContactState(true, true);

                Assert.That(first.Owner, Is.EqualTo(EarthPoweredImpactOwner.PoweredPhysicalAssist));
                Assert.That(first.EmitsImpulse || first.RequestsRagdoll, Is.False);
                Assert.That(duplicate.Duplicate, Is.True);
                Assert.That(puppet.CanonicalMode, Is.EqualTo(CharacterPhysicalMode.Stagger));
                Assert.That(body.linearVelocity, Is.EqualTo(velocityBefore),
                    "Accepted medium ownership must not add a second impulse.");

                yield return new WaitForFixedUpdate();
                Assert.That(puppet.LastPoweredAssistOutput.PreservesFeet, Is.True);
                Assert.That(motor.enabled, Is.True,
                    "A medium response must preserve PlanetMotor and support authority.");
                Assert.That(puppet.LastBalanceTorque, Is.EqualTo(Vector3.zero),
                    "Powered balance may shape joints and request an authored step, not torque the pelvis.");
                for (int index = 0; index < joints.Length; index++)
                {
                    if (joints[index].BodyRegion != EarthBodyRegion.Leg) continue;
                    AssertEveryDriveChannelIsZero(
                        joints[index].GetComponent<ConfigurableJoint>(),
                        joints[index].name);
                }
            }
            finally
            {
                if (profile != null)
                    Object.DestroyImmediate(profile);
                DestroyAndUnloadScene(scene);
            }
        }

        [UnityTest]
        public IEnumerator MotorPositionConstraintOwnerPersistsPinAndReleasesForRagdoll()
        {
            var root = new GameObject("Motor Constraint Ownership Fixture");
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.None;
            ActiveRagdollPuppet puppet = root.AddComponent<ActiveRagdollPuppet>();
            puppet.Configure(
                77u,
                null,
                body,
                null,
                null,
                root.transform,
                Array.Empty<ActiveRagdollJoint>(),
                Array.Empty<Collider>());

            try
            {
                Assert.That(body.constraints,
                    Is.EqualTo(RigidbodyConstraints.FreezeRotation));
                puppet.ConfigureMotorPositionConstraints(
                    RigidbodyConstraints.FreezePosition);
                Assert.That(puppet.MotorPositionConstraints,
                    Is.EqualTo(RigidbodyConstraints.FreezePosition));
                Assert.That(body.constraints,
                    Is.EqualTo(RigidbodyConstraints.FreezeAll));

                yield return new WaitForFixedUpdate();
                Assert.That(body.constraints,
                    Is.EqualTo(RigidbodyConstraints.FreezeAll),
                    "FixedUpdate must preserve the explicit motor-position owner.");

                Assert.That(puppet.TryBeginExternalFullRagdoll(), Is.True);
                Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints.None),
                    "Motor-only translation pinning must not leak into ragdoll ownership.");

                puppet.ResetPhysicalState(body.position, body.rotation);
                Assert.That(body.constraints,
                    Is.EqualTo(RigidbodyConstraints.FreezeAll),
                    "Returning to motor ownership must restore the configured pin.");

                puppet.ConfigureMotorPositionConstraints(RigidbodyConstraints.None);
                Assert.That(body.constraints,
                    Is.EqualTo(RigidbodyConstraints.FreezeRotation));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    puppet.ConfigureMotorPositionConstraints(
                        RigidbodyConstraints.FreezeRotationX));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ActiveRagdollJoint[] FindOwnedActiveRagdollJoints(
            Scene scene,
            ActiveRagdollPuppet puppet)
        {
            ActiveRagdollJoint[] candidates = FindInSceneAll<ActiveRagdollJoint>(scene);
            int ownedCount = 0;
            for (int index = 0; index < candidates.Length; index++)
            {
                ActiveRagdollJoint candidate = candidates[index];
                if (candidate.TargetPose != null &&
                    candidate.TargetPose.IsChildOf(puppet.transform))
                    ownedCount++;
            }

            var owned = new ActiveRagdollJoint[ownedCount];
            int writeIndex = 0;
            for (int index = 0; index < candidates.Length; index++)
            {
                ActiveRagdollJoint candidate = candidates[index];
                if (candidate.TargetPose == null ||
                    !candidate.TargetPose.IsChildOf(puppet.transform))
                    continue;
                owned[writeIndex++] = candidate;
            }
            return owned;
        }

        private static void DestroyAndUnloadScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            DestroySceneRoots(scene);
            SceneManager.UnloadSceneAsync(scene);
        }

        private static void DestroySceneRoots(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
                Object.DestroyImmediate(roots[index]);
        }

        private static EarthBodyRegion RegionForAuthoredTarget(Transform target)
        {
            Assert.That(target, Is.Not.Null);
            string targetName = target.name;
            if (targetName.IndexOf("Leg", StringComparison.OrdinalIgnoreCase) >= 0)
                return EarthBodyRegion.Leg;
            if (targetName.IndexOf("Arm", StringComparison.OrdinalIgnoreCase) >= 0)
                return EarthBodyRegion.Arm;
            if (targetName.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                return EarthBodyRegion.Head;
            if (targetName.IndexOf("Chest", StringComparison.OrdinalIgnoreCase) >= 0)
                return EarthBodyRegion.Chest;
            Assert.Fail($"No explicit Wave P2 body-region binding for '{targetName}'.");
            return EarthBodyRegion.Unassigned;
        }

        private static void SeedEveryDriveChannel(ConfigurableJoint joint)
        {
            var drive = new JointDrive
            {
                positionSpring = 123f,
                positionDamper = 45f,
                maximumForce = 678f
            };
            joint.xDrive = drive;
            joint.yDrive = drive;
            joint.zDrive = drive;
            joint.angularXDrive = drive;
            joint.angularYZDrive = drive;
            joint.slerpDrive = drive;
        }

        private static void AssertEveryDriveChannelIsZero(
            ConfigurableJoint joint,
            string jointName)
        {
            AssertDriveIsZero(joint.xDrive, jointName, "xDrive");
            AssertDriveIsZero(joint.yDrive, jointName, "yDrive");
            AssertDriveIsZero(joint.zDrive, jointName, "zDrive");
            AssertDriveIsZero(joint.angularXDrive, jointName, "angularXDrive");
            AssertDriveIsZero(joint.angularYZDrive, jointName, "angularYZDrive");
            AssertDriveIsZero(joint.slerpDrive, jointName, "slerpDrive");
        }

        private static void AssertEveryDriveChannelIsNonZero(
            ConfigurableJoint joint,
            string jointName)
        {
            AssertDriveIsNonZero(joint.xDrive, jointName, "xDrive");
            AssertDriveIsNonZero(joint.yDrive, jointName, "yDrive");
            AssertDriveIsNonZero(joint.zDrive, jointName, "zDrive");
            AssertDriveIsNonZero(joint.angularXDrive, jointName, "angularXDrive");
            AssertDriveIsNonZero(joint.angularYZDrive, jointName, "angularYZDrive");
            AssertDriveIsNonZero(joint.slerpDrive, jointName, "slerpDrive");
        }

        private static void AssertDriveIsZero(
            JointDrive drive,
            string jointName,
            string channel)
        {
            Assert.That(drive.positionSpring, Is.Zero,
                $"{jointName}/{channel} spring must not fight leg/contact ownership.");
            Assert.That(drive.positionDamper, Is.Zero,
                $"{jointName}/{channel} damper must not fight leg/contact ownership.");
            Assert.That(drive.maximumForce, Is.Zero,
                $"{jointName}/{channel} force must not fight leg/contact ownership.");
        }

        private static void AssertDriveIsNonZero(
            JointDrive drive,
            string jointName,
            string channel)
        {
            Assert.That(drive.positionSpring, Is.GreaterThan(0f),
                $"{jointName}/{channel} spring seed");
            Assert.That(drive.positionDamper, Is.GreaterThan(0f),
                $"{jointName}/{channel} damper seed");
            Assert.That(drive.maximumForce, Is.GreaterThan(0f),
                $"{jointName}/{channel} force seed");
        }

        [UnityTest]
        public IEnumerator EarthCorePuppetRemainsGroundedWhenPlayerProvidesNoInput()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = default;
            try
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(
                    scenePath, LoadSceneMode.Additive);
                Assert.That(load, Is.Not.Null);
                yield return load;
                yield return null;

                scene = SceneManager.GetSceneByPath(scenePath);
                ActiveRagdollPuppet puppet = FindInScene<ActiveRagdollPuppet>(scene);
                VoxelPlanetBehaviour planet = FindInScene<VoxelPlanetBehaviour>(scene);
                Assert.That(puppet, Is.Not.Null);
                Assert.That(planet, Is.Not.Null);
                Assert.That(planet.State, Is.Not.Null);
                Rigidbody body = puppet.GetComponent<Rigidbody>();
                CapsuleCollider capsule = puppet.GetComponent<CapsuleCollider>();
                PlanetMotor motor = puppet.GetComponent<PlanetMotor>();
                Assert.That(body, Is.Not.Null);
                Assert.That(capsule, Is.Not.Null);
                Assert.That(motor, Is.Not.Null);

                Vector3 planetScale = planet.transform.lossyScale;
                float minimumPlanetScale = Mathf.Min(
                    Mathf.Abs(planetScale.x),
                    Mathf.Abs(planetScale.y),
                    Mathf.Abs(planetScale.z));
                float maximumPlanetScale = Mathf.Max(
                    Mathf.Abs(planetScale.x),
                    Mathf.Abs(planetScale.y),
                    Mathf.Abs(planetScale.z));
                Assert.That(maximumPlanetScale - minimumPlanetScale,
                    Is.LessThan(0.0001f),
                    "The voxel planet radius is spherical only under uniform world scale.");

                Vector3 planetCenter = planet.transform.position;
                float planetWorldRadius = planet.Radius * maximumPlanetScale;
                float terrainShell = planet.State.NoiseAmplitude * maximumPlanetScale;
                float voxelSurfaceError = planet.State.CellSize * maximumPlanetScale;
                float maximumSurfaceClearance =
                    capsule.bounds.extents.magnitude + terrainShell + voxelSurfaceError;
                float startRadius = Vector3.Distance(body.position, planetCenter);
                float startSurfaceClearance = startRadius - planetWorldRadius;
                float peakSpeed = 0f;
                float peakSurfaceClearance = startSurfaceClearance;
                for (int tick = 0; tick < 180; tick++)
                {
                    yield return new WaitForFixedUpdate();
                    peakSpeed = Mathf.Max(peakSpeed, body.linearVelocity.magnitude);
                    float surfaceClearance =
                        Vector3.Distance(body.position, planetCenter) - planetWorldRadius;
                    peakSurfaceClearance = Mathf.Max(
                        peakSurfaceClearance, surfaceClearance);
                }

                float finalSurfaceClearance =
                    Vector3.Distance(body.position, planetCenter) - planetWorldRadius;
                Assert.That(peakSurfaceClearance,
                    Is.InRange(0f, maximumSurfaceClearance),
                    $"Idle active ragdoll left the planet surface shell; " +
                    $"start={startSurfaceClearance:0.000} m, " +
                    $"peak={peakSurfaceClearance:0.000} m, " +
                    $"bound={maximumSurfaceClearance:0.000} m, " +
                    $"speed={peakSpeed:0.00} m/s.");
                Assert.That(finalSurfaceClearance,
                    Is.InRange(0f, maximumSurfaceClearance));
                Assert.That(motor.HasStableSupport, Is.True);
                Assert.That(IsFinite(body.position), Is.True);
                Assert.That(IsFinite(body.linearVelocity), Is.True);
                Assert.That(IsFinite(body.angularVelocity), Is.True);
                Assert.That(float.IsFinite(peakSpeed), Is.True);
                Assert.That(body.linearVelocity.magnitude, Is.LessThan(2f));
            }
            finally
            {
                DestroyAndUnloadScene(scene);
            }
        }

        [UnityTest]
        public IEnumerator CharacterFeelLab_RepeatedImpactsRemainFinite()
        {
            const string scenePath =
                "Assets/Elemental/Content/Scenes/CharacterFeelLab.unity";
            Scene scene = default;
            try
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(
                    scenePath,
                    LoadSceneMode.Additive);
                Assert.That(load, Is.Not.Null);
                yield return load;

                scene = SceneManager.GetSceneByPath(scenePath);
                ActiveRagdollPuppet puppet = FindInScene<ActiveRagdollPuppet>(scene);
                CharacterFeelLabDriver driver = FindInScene<CharacterFeelLabDriver>(scene);
                Assert.That(puppet, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                for (int tick = 0; tick < 300; tick++)
                    yield return new WaitForFixedUpdate();

                Rigidbody rootBody = puppet.GetComponent<Rigidbody>();
                Assert.That(driver.PulseCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(IsFinite(rootBody.position), Is.True);
                Assert.That(IsFinite(rootBody.linearVelocity), Is.True);
                Assert.That(IsFinite(rootBody.angularVelocity), Is.True);
                Assert.That(float.IsFinite(puppet.CurrentState.StaggerDebt), Is.True);
                Assert.That(float.IsFinite(puppet.MaximumJointError), Is.True);

                ActiveRagdollJoint[] joints = FindInSceneAll<ActiveRagdollJoint>(scene);
                Assert.That(joints.Length, Is.GreaterThanOrEqualTo(6));
                for (int index = 0; index < joints.Length; index++)
                {
                    Assert.That(IsFinite(joints[index].Body.linearVelocity), Is.True);
                    Assert.That(IsFinite(joints[index].Body.angularVelocity), Is.True);
                    Assert.That(joints[index].Body.angularVelocity.magnitude, Is.LessThan(100f));
                }
            }
            finally
            {
                DestroyAndUnloadScene(scene);
            }
        }

        [UnityTest]
        public IEnumerator PuppetDisablesMotorDrivesOnRagdollAndRecoversWithoutExplosion()
        {
            GameObject floor = null;
            GameObject root = null;
            try
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Ragdoll Test Floor";
                floor.transform.position = new Vector3(0f, -0.5f, 0f);
                floor.transform.localScale = new Vector3(10f, 1f, 10f);

                root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                root.name = "Ragdoll Runtime Test Root";
                root.SetActive(false);
                root.transform.position = new Vector3(0f, 1f, 0f);
                Rigidbody rootBody = root.AddComponent<Rigidbody>();
                rootBody.isKinematic = true;
                PhysicalImpactTarget impact = root.AddComponent<PhysicalImpactTarget>();
                impact.Configure(rootBody);

                GameObject targetObject = new GameObject("Chest Pose Target");
                targetObject.transform.SetParent(root.transform, false);
                targetObject.transform.localPosition = new Vector3(0f, 0.8f, 0f);

                GameObject chestObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chestObject.name = "Physical Chest";
                chestObject.transform.SetParent(root.transform, false);
                chestObject.transform.localPosition = targetObject.transform.localPosition;
                Rigidbody chestBody = chestObject.AddComponent<Rigidbody>();
                chestBody.mass = 5f;
                chestBody.useGravity = false;
                ConfigurableJoint configurableJoint =
                    chestObject.AddComponent<ConfigurableJoint>();
                configurableJoint.connectedBody = rootBody;
                ActiveRagdollJoint joint = chestObject.AddComponent<ActiveRagdollJoint>();
                joint.Configure(
                    chestBody,
                    configurableJoint,
                    targetObject.transform,
                    500f,
                    50f,
                    800f,
                    45f);

                ActiveRagdollPuppet puppet = root.AddComponent<ActiveRagdollPuppet>();
                puppet.Configure(
                    1u,
                    null,
                    rootBody,
                    null,
                    impact,
                    chestObject.transform,
                    new[] { joint },
                    new[] { root.GetComponent<Collider>(), chestObject.GetComponent<Collider>() });
                root.SetActive(true);

                for (int index = 0; index < 200; index++)
                    puppet.InjectImpact(20f);

                yield return new WaitForFixedUpdate();
                Assert.That(puppet.CurrentState.Mode,
                    Is.EqualTo(CharacterPhysicalMode.FullRagdoll));
                Assert.That(configurableJoint.slerpDrive.maximumForce,
                    Is.EqualTo(0f).Within(0.001f));

                for (int index = 0; index < 70; index++)
                    yield return new WaitForFixedUpdate();

                Assert.That(puppet.CurrentState.Mode,
                    Is.EqualTo(CharacterPhysicalMode.AnimatedMotor));
                Assert.That(puppet.CurrentState.MuscleStrength,
                    Is.EqualTo(1f).Within(0.001f));
                Assert.That(configurableJoint.slerpDrive.maximumForce, Is.GreaterThan(0f));
                Assert.That(float.IsFinite(puppet.MaximumJointError), Is.True);
                Assert.That(float.IsFinite(chestBody.angularVelocity.x), Is.True);
                Assert.That(chestBody.angularVelocity.magnitude, Is.LessThan(50f));
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (floor != null) Object.DestroyImmediate(floor);
            }
        }

        [UnityTest]
        public IEnumerator HumanoidRecoveryPreservesLegacyFallbackAndMarkersOwnPoseMatchedHandoff()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = default;
            HumanoidRagdollRig rig = null;
            EarthPhysicalAnimationProfile profile = null;
            EarthPhysicalAnimationProfile missingStateProfile = null;
            Action<AuthoredRecoveryHandoff> observeSelectedState = null;
            try
            {
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            scene = SceneManager.GetSceneByPath(scenePath);
            rig = FindInScene<HumanoidRagdollRig>(scene);
            Assert.That(rig, Is.Not.Null);
            PlanetMotor motor = rig.GetComponentInParent<PlanetMotor>();
            Vector3 localUp = motor != null ? motor.LocalUp : rig.transform.up;
            Vector3 preferredForward = motor != null ? motor.transform.forward : rig.transform.forward;

            rig.ConfigurePhysicalAnimation(null, null, null, null);
            int legacyRecoveryHandoffs = rig.RecoveryOwnershipHandoffCount;
            rig.BeginRagdoll(Vector3.zero);
            yield return new WaitForFixedUpdate();
            rig.RecoverToAnimated(localUp, preferredForward, false);
            rig.RecoverToAnimated(localUp, preferredForward, false);
            Assert.That(rig.UsedPoseMatchedRecovery, Is.False);
            Assert.That(rig.RecoveryOwnershipHandoffCount, Is.EqualTo(legacyRecoveryHandoffs),
                "A disabled feature must keep the legacy recovery path exact.");
            int legacyInterruptHandoffs = rig.RagdollOwnershipHandoffCount;
            rig.BeginRagdoll(new RagdollHandoff(
                rig.transform.position,
                Vector3.right * 0.25f,
                true));
            Assert.That(rig.RagdollOwnershipHandoffCount,
                Is.EqualTo(legacyInterruptHandoffs + 1));
            Assert.That(rig.IsRagdollActive, Is.True);
            Assert.That(rig.IsRecoveringToAnimation, Is.False);
            Assert.That(rig.PhysicalAnimationMode,
                Is.EqualTo(CharacterPhysicalMode.FullRagdoll));
            Assert.That(rig.PhysicalOwnershipConsistent, Is.True);
            yield return new WaitForFixedUpdate();
            rig.RecoverToAnimated(localUp, preferredForward, false);
            rig.CompleteRecovery();
            rig.ResetToAnimated();
            yield return null;
            Assert.That(rig.PhysicalAnimationMode,
                Is.EqualTo(CharacterPhysicalMode.AnimatedMotor));
            Assert.That(rig.PhysicalOwnershipConsistent, Is.True);
            HumanoidCharacterPresentation recoveryPresentation =
                rig.GetComponent<HumanoidCharacterPresentation>();
            Assert.That(recoveryPresentation, Is.Not.Null);
            EarthTransitionDirector recoveryTransitionOwner =
                recoveryPresentation.TransitionDirector;
            Assert.That(recoveryTransitionOwner, Is.Not.Null);
            Assert.That(recoveryTransitionOwner.BaseStateOwnerMode,
                Is.EqualTo(CharacterPhysicalMode.AnimatedMotor));
            Assert.That(recoveryTransitionOwner.OwnedBaseStateHash, Is.EqualTo(0));

            Light feetOwner = rig.gameObject.AddComponent<Light>();
            AudioSource controlOwner = rig.gameObject.AddComponent<AudioSource>();
            Animation proceduralOwner = rig.gameObject.AddComponent<Animation>();
            profile = ScriptableObject.CreateInstance<EarthPhysicalAnimationProfile>();
            EarthRecoveryMarkerAuthoring markers =
                new EarthRecoveryMarkerAuthoring(0.56f, 0.80f, 0.95f);
            Animator recoveryAnimator = rig.GetComponentInChildren<Animator>(true);
            Assert.That(recoveryAnimator, Is.Not.Null);
            const float authoredRecoveryEntryPhase = 0.55f;
            int authoredRecoveryStateHash =
                Animator.StringToHash("Base Layer.Knockdown Recovery");
            Vector3 authoredPelvisOffsetLocal = SampleAuthoredRecoveryPelvisOffset(
                recoveryAnimator,
                recoveryTransitionOwner,
                motor.Body,
                authoredRecoveryStateHash,
                authoredRecoveryEntryPhase);
            profile.ConfigureRecovery(
                true,
                new[]
                {
                    RecoverySample(101u, EarthRecoveryOrientation.Front,
                        authoredPelvisOffsetLocal, in markers),
                    RecoverySample(102u, EarthRecoveryOrientation.Back,
                        authoredPelvisOffsetLocal, in markers),
                    RecoverySample(103u, EarthRecoveryOrientation.Left,
                        authoredPelvisOffsetLocal, in markers),
                    RecoverySample(104u, EarthRecoveryOrientation.Right,
                        authoredPelvisOffsetLocal, in markers)
                });
            rig.ConfigurePhysicalAnimation(
                profile,
                new Behaviour[] { feetOwner },
                new Behaviour[] { controlOwner },
                new Behaviour[] { proceduralOwner });

            CreateIsolatedRecoverySupport(
                scene,
                rig,
                motor,
                localUp);
            yield return new WaitForFixedUpdate();
            Assert.That(motor.HasStableSupport, Is.True,
                "The isolated fixture must establish stable support before recovery starts.");

            rig.BeginRagdoll(Vector3.zero);
            yield return new WaitForFixedUpdate();
            Assert.That(feetOwner.enabled, Is.False);
            Assert.That(controlOwner.enabled, Is.False);
            Assert.That(proceduralOwner.enabled, Is.False);
            int recoveryHandoffsBefore = rig.RecoveryOwnershipHandoffCount;
            uint transitionEvaluationBefore =
                recoveryTransitionOwner.ImmediateEvaluationSequence;
            bool observedSelectedStateInEvent = false;
            int eventStateHash = 0;
            int eventTransitionOwnerStateHash = 0;
            uint eventTransitionEvaluationSequence = 0u;
            float eventStatePhase = 0f;
            observeSelectedState = handoff =>
            {
                if (!handoff.HasSelectedState) return;
                AnimatorStateInfo state = recoveryAnimator.GetCurrentAnimatorStateInfo(0);
                observedSelectedStateInEvent = true;
                eventStateHash = state.fullPathHash;
                eventTransitionOwnerStateHash = recoveryTransitionOwner.ActiveStateHash;
                eventTransitionEvaluationSequence =
                    recoveryTransitionOwner.ImmediateEvaluationSequence;
                eventStatePhase = Mathf.Repeat(state.normalizedTime, 1f);
            };
            rig.AuthoredRecoveryBegan += observeSelectedState;
            rig.RecoverToAnimated(localUp, preferredForward, false);
            rig.RecoverToAnimated(localUp, preferredForward, false);

            Assert.That(rig.UsedPoseMatchedRecovery, Is.True);
            Assert.That(rig.LastPoseMatchedRecovery.IsValid, Is.True);
            float3 authoredPelvisOffset = new float3(
                authoredPelvisOffsetLocal.x,
                authoredPelvisOffsetLocal.y,
                authoredPelvisOffsetLocal.z);
            float3 reconstructedLivePelvis = rig.LastPoseMatchedRecovery.RootPosition +
                                             math.rotate(
                                                 rig.LastPoseMatchedRecovery.RootRotation,
                                                 authoredPelvisOffset);
            Assert.That(rig.LastPoseMatchedRecovery.Clearance.LiftMeters,
                Is.EqualTo(0f).Within(0.0001f),
                "The isolated authored pose must be clear without moving its live pelvis.");
            Assert.That(math.distance(
                    reconstructedLivePelvis,
                    rig.LastPoseMatchedRecovery.LivePelvisPosition),
                Is.LessThan(0.001f),
                "The selected authored pelvis offset must preserve live-pelvis continuity.");
            Assert.That(rig.RecoveryOwnershipHandoffCount,
                Is.EqualTo(recoveryHandoffsBefore + 1),
                "Repeated recovery requests must not hand Animator ownership over twice.");
            Assert.That(feetOwner.enabled, Is.False,
                "Feet must remain disabled before their authored marker.");
            Assert.That(observedSelectedStateInEvent, Is.True);
            Assert.That(eventTransitionOwnerStateHash,
                Is.EqualTo(rig.LastPoseMatchedRecovery.AnimationStateId),
                "The presentation transition owner must commit before later recovery observers run.");
            Assert.That(recoveryTransitionOwner.BaseStateOwnerMode,
                Is.EqualTo(CharacterPhysicalMode.Recovery));
            Assert.That(recoveryTransitionOwner.OwnedBaseStateHash,
                Is.EqualTo(rig.LastPoseMatchedRecovery.AnimationStateId));
            Assert.That(eventTransitionEvaluationSequence,
                Is.EqualTo(transitionEvaluationBefore + 1u),
                "The selected state must be evaluated by the sole transition owner before later observers run.");
            uint rejectedTransitionsBefore =
                recoveryTransitionOwner.RecoveryOwnedTransitionRejectCount;
            var conflictingLocomotion = new EarthAnimationTransitionContext(
                EarthMotionStateId.KnockdownRecovery,
                EarthMotionStateId.Locomotion,
                EarthMotionCategory.RagdollRecovery,
                EarthMotionCategory.Locomotion,
                EarthAnimationTransitionPriority.Locomotion,
                EarthAnimationTransitionPriority.HeavyImpact,
                0.55f,
                0f,
                1f,
                0f,
                0f,
                false,
                true,
                false,
                true);
            Assert.That(recoveryTransitionOwner.RequestTransition(
                    Animator.StringToHash("Base Layer.Locomotion"),
                    in conflictingLocomotion),
                Is.False,
                "Ordinary locomotion must not overwrite a Recovery-owned base state.");
            Assert.That(recoveryTransitionOwner.RecoveryOwnedTransitionRejectCount,
                Is.EqualTo(rejectedTransitionsBefore + 1u));
            Assert.That(recoveryTransitionOwner.LastRecoveryOwnedRejectedStateHash,
                Is.EqualTo(Animator.StringToHash("Base Layer.Locomotion")));
            Assert.That(eventStateHash,
                Is.EqualTo(rig.LastPoseMatchedRecovery.AnimationStateId));
            Assert.That(eventStatePhase,
                Is.EqualTo(0.55f).Within(0.005f));
            Assert.That(rig.RecoveryStateHashAfterEvent,
                Is.EqualTo(rig.LastPoseMatchedRecovery.AnimationStateId));
            Assert.That(rig.RecoveryStatePhaseAfterEvent,
                Is.EqualTo(0.55f).Within(0.005f));
            Assert.That(rig.RecoveryStateVerifiedAfterEvent, Is.True);
            yield return null;
            yield return null;
            Assert.That(rig.RecoveryStateHashNextFrame,
                Is.EqualTo(rig.LastPoseMatchedRecovery.AnimationStateId));
            Assert.That(rig.RecoveryStateVerifiedNextFrame, Is.True);
            Assert.That(rig.RecoveryStateLengthSeconds, Is.GreaterThan(0f));
            Assert.That(rig.RecoveryStateSpeed, Is.GreaterThanOrEqualTo(0f));
            Assert.That(rig.RecoveryStateSpeedMultiplier, Is.GreaterThanOrEqualTo(0f));
            Assert.That(rig.RecoveryStateLoops, Is.False,
                "The authored get-up must remain a non-looping recovery state.");
            Assert.That(rig.RecoveryStateElapsedSecondsNextFrame, Is.GreaterThanOrEqualTo(0f));
            Assert.That(rig.RecoveryStateEvaluationLeadSeconds, Is.GreaterThan(0f));
            Assert.That(rig.RecoveryStateAppliedEvaluationLeadSeconds,
                Is.GreaterThanOrEqualTo(0f));
            Assert.That(rig.RecoveryStateAppliedEvaluationLeadSeconds,
                Is.LessThanOrEqualTo(
                    EarthRecoveryAnimatorContinuityGate.MaximumEvaluationLeadSeconds));
            Assert.That(rig.RecoveryStateAppliedEvaluationLeadSeconds,
                Is.LessThanOrEqualTo(rig.RecoveryStateEvaluationLeadSeconds));
            Assert.That(rig.RecoveryStateEffectiveElapsedSeconds,
                Is.EqualTo(
                    rig.RecoveryStateElapsedSecondsNextFrame +
                    rig.RecoveryStateAppliedEvaluationLeadSeconds)
                    .Within(0.0001f));
            Assert.That(rig.RecoveryStateMeasuredPhaseAdvance,
                Is.GreaterThanOrEqualTo(-EarthRecoveryAnimatorContinuityGate.DefaultPhaseSlack));
            Assert.That(rig.RecoveryStateMeasuredPhaseAdvance,
                Is.LessThanOrEqualTo(rig.RecoveryStateAllowedPhaseAdvance));
            Assert.That(rig.RecoveryStateAllowedPhaseAdvance,
                Is.EqualTo(
                    rig.RecoveryStateEffectiveElapsedSeconds *
                    rig.RecoveryStateNormalizedRate +
                    EarthRecoveryAnimatorContinuityGate.DefaultPhaseSlack)
                    .Within(0.0001f));
            Assert.That(rig.RecoveryAnimatorCurrentStateHash,
                Is.EqualTo(rig.LastPoseMatchedRecovery.AnimationStateId),
                "An outgoing controller transition must not replace the current recovery owner.");
            Assert.That(rig.RecoveryAnimatorSampledNextState, Is.False,
                "Recovery validation must prefer the selected current state over an outgoing next state.");
            Assert.That(recoveryTransitionOwner.BaseStateOwnerMode,
                Is.EqualTo(CharacterPhysicalMode.Recovery));
            Assert.That(recoveryTransitionOwner.ActiveStateHash,
                Is.EqualTo(rig.LastPoseMatchedRecovery.AnimationStateId));
            rig.AuthoredRecoveryBegan -= observeSelectedState;

            int supportWaitFrames = 0;
            while ((!rig.RecoveryHasLiveSupport || !rig.RecoveryFeetEnabled) &&
                   supportWaitFrames++ < 4)
                yield return null;
            Assert.That(rig.RecoveryHasLiveSupport, Is.True);
            Assert.That(rig.RecoveryFeetEnabled, Is.True);
            Assert.That(motor, Is.Not.Null);
            Assert.That(motor.Body, Is.Not.Null);
            Assert.That(motor.Capsule, Is.Not.Null);
            int supportSamplesBeforeLoss = rig.RecoverySupportSampleCount;
            bool motorWasEnabled = motor.enabled;
            motor.enabled = false;
            Rigidbody motorBody = motor.Body;
            bool bodyWasKinematic = motorBody.isKinematic;
            bool bodyDetectedCollisions = motorBody.detectCollisions;
            RigidbodyConstraints bodyConstraints = motorBody.constraints;
            RigidbodyInterpolation bodyInterpolation = motorBody.interpolation;
            Vector3 bodyLinearVelocity = motorBody.linearVelocity;
            Vector3 bodyAngularVelocity = motorBody.angularVelocity;
            if (!bodyWasKinematic)
            {
                motorBody.linearVelocity = Vector3.zero;
                motorBody.angularVelocity = Vector3.zero;
            }
            motorBody.interpolation = RigidbodyInterpolation.None;
            motorBody.isKinematic = true;
            TeleportKinematicBody(
                motorBody,
                motorBody.position,
                motorBody.rotation);

            Vector3 supportedRootPosition = motorBody.position;
            Quaternion supportedRootRotation = motorBody.rotation;
            Vector3 supportedProbeOrigin = motor.Capsule.transform.TransformPoint(
                motor.Capsule.center);
            Vector3 unsupportedRootPosition = supportedRootPosition + localUp * 3f;
            TeleportKinematicBody(
                motorBody,
                unsupportedRootPosition,
                supportedRootRotation);
            Vector3 unsupportedProbeOrigin = motor.Capsule.transform.TransformPoint(
                motor.Capsule.center);
            Assert.That(Vector3.Dot(
                    unsupportedProbeOrigin - supportedProbeOrigin,
                    localUp),
                Is.GreaterThan(2.9f),
                $"Recovery probe did not follow the frozen Rigidbody teleport. " +
                $"body={motorBody.position}, capsule={unsupportedProbeOrigin}, " +
                $"supportedCapsule={supportedProbeOrigin}.");
            yield return null;
            yield return null;
            Assert.That(rig.RecoverySupportSampleCount,
                Is.GreaterThan(supportSamplesBeforeLoss));
            Assert.That(rig.RecoveryHasLiveSupport, Is.False);
            Assert.That(rig.RecoveryFeetEnabled, Is.False,
                "Live support loss must revoke feet while movement control is disabled.");

            TeleportKinematicBody(
                motorBody,
                supportedRootPosition,
                supportedRootRotation);
            Vector3 reacquiredProbeOrigin = motor.Capsule.transform.TransformPoint(
                motor.Capsule.center);
            Assert.That(Vector3.Distance(reacquiredProbeOrigin, supportedProbeOrigin),
                Is.LessThan(0.001f),
                $"Recovery probe did not return to its supported pose. " +
                $"body={motorBody.position}, capsule={reacquiredProbeOrigin}, " +
                $"expectedCapsule={supportedProbeOrigin}.");
            yield return null;
            yield return null;
            Assert.That(rig.RecoveryHasLiveSupport, Is.True);
            Assert.That(rig.RecoveryFeetEnabled, Is.True,
                "Live support reacquisition must re-enable marker ownership.");
            motorBody.detectCollisions = bodyDetectedCollisions;
            motorBody.constraints = bodyConstraints;
            motorBody.isKinematic = bodyWasKinematic;
            motorBody.interpolation = bodyInterpolation;
            if (!bodyWasKinematic)
            {
                motorBody.linearVelocity = bodyLinearVelocity;
                motorBody.angularVelocity = bodyAngularVelocity;
                motorBody.WakeUp();
            }
            motor.enabled = motorWasEnabled;

            int poseInterruptHandoffs = rig.RagdollOwnershipHandoffCount;
            rig.BeginRagdoll(new RagdollHandoff(
                rig.transform.position,
                Vector3.forward * 0.25f,
                true));
            Assert.That(rig.RagdollOwnershipHandoffCount,
                Is.EqualTo(poseInterruptHandoffs + 1));
            Assert.That(rig.IsRecoveringToAnimation, Is.False);
            Assert.That(rig.IsRagdollActive, Is.True);
            Assert.That(feetOwner.enabled, Is.False);
            Assert.That(controlOwner.enabled, Is.False);
            Assert.That(proceduralOwner.enabled, Is.False);
            Assert.That(rig.PhysicalOwnershipConsistent, Is.True);
            rig.RecoverToAnimated(localUp, preferredForward, false);
            rig.RecoverToAnimated(localUp, preferredForward, false);
            Assert.That(rig.UsedPoseMatchedRecovery, Is.True,
                $"The accepted-hit interrupt must re-enter pose-matched recovery " +
                $"from the same live pelvis frame. blocker=" +
                $"{rig.RecoveryClearanceBlockingCollider?.name ?? "<none>"}, " +
                $"blockerUpOffset={rig.RecoveryClearanceBlockingUpOffset:F4}, " +
                $"probeRoot={rig.RecoveryClearanceProbeRootPosition}, " +
                $"probeRadius={rig.RecoveryClearanceProbeRadius:F4}, " +
                $"body={motorBody.position}, velocity={motorBody.linearVelocity}.");
            Assert.That(rig.RecoveryOwnershipHandoffCount,
                Is.EqualTo(recoveryHandoffsBefore + 2));

            int renderedFrames = 0;
            while (rig.IsRecoveringToAnimation && renderedFrames++ < 180)
            {
                AnimatorStateInfo ownedRecoveryState = recoveryAnimator.IsInTransition(0)
                    ? recoveryAnimator.GetNextAnimatorStateInfo(0)
                    : recoveryAnimator.GetCurrentAnimatorStateInfo(0);
                Assert.That(ownedRecoveryState.fullPathHash,
                    Is.EqualTo(rig.LastPoseMatchedRecovery.AnimationStateId),
                    "The selected recovery must own the base state until its exit marker.");
                yield return null;
            }

            Assert.That(rig.IsRecoveringToAnimation, Is.False,
                "A valid supported recovery must reach its exit marker.");
            Assert.That(feetOwner.enabled, Is.True);
            Assert.That(controlOwner.enabled, Is.True);
            Assert.That(proceduralOwner.enabled, Is.True);
            Assert.That(rig.PhysicalAnimationMode,
                Is.EqualTo(CharacterPhysicalMode.AnimatedMotor));
            Assert.That(rig.PhysicalOwnershipConsistent, Is.True);
            yield return null;
            Assert.That(recoveryTransitionOwner.OwnedBaseStateHash, Is.EqualTo(0));
            rig.CompleteRecovery();
            rig.ResetToAnimated();

            missingStateProfile =
                ScriptableObject.CreateInstance<EarthPhysicalAnimationProfile>();
            EarthRecoveryMarkerAuthoring fallbackMarkers =
                new EarthRecoveryMarkerAuthoring(0.20f, 0.60f, 0.90f);
            missingStateProfile.ConfigureRecovery(
                true,
                new[]
                {
                    RecoverySample(
                        501u,
                        EarthRecoveryOrientation.Front,
                        authoredPelvisOffsetLocal,
                        in fallbackMarkers,
                        "Base Layer.State That Does Not Exist"),
                    RecoverySample(
                        502u,
                        EarthRecoveryOrientation.Back,
                        authoredPelvisOffsetLocal,
                        in fallbackMarkers,
                        "Base Layer.State That Does Not Exist"),
                    RecoverySample(
                        503u,
                        EarthRecoveryOrientation.Left,
                        authoredPelvisOffsetLocal,
                        in fallbackMarkers,
                        "Base Layer.State That Does Not Exist"),
                    RecoverySample(
                        504u,
                        EarthRecoveryOrientation.Right,
                        authoredPelvisOffsetLocal,
                        in fallbackMarkers,
                        "Base Layer.State That Does Not Exist")
                });
            rig.ConfigurePhysicalAnimation(
                missingStateProfile,
                new Behaviour[] { feetOwner },
                new Behaviour[] { controlOwner },
                new Behaviour[] { proceduralOwner });
            int fallbackRecoveryHandoffs = rig.RecoveryOwnershipHandoffCount;
            rig.BeginRagdoll(Vector3.zero);
            yield return new WaitForFixedUpdate();
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex(
                    "Pose-matched recovery is enabled but incomplete.*legacy live-pelvis recovery"));
            rig.RecoverToAnimated(localUp, preferredForward, false);
            Assert.That(rig.UsedPoseMatchedRecovery, Is.False);
            Assert.That(rig.RecoveryOwnershipHandoffCount,
                Is.EqualTo(fallbackRecoveryHandoffs));
            Assert.That(rig.IsRecoveringToAnimation, Is.True);
            Assert.That(rig.PhysicalAnimationMode,
                Is.EqualTo(CharacterPhysicalMode.Recovery));
            rig.CompleteRecovery();
            rig.ResetToAnimated();

            }
            finally
            {
                if (rig != null && observeSelectedState != null)
                    rig.AuthoredRecoveryBegan -= observeSelectedState;
                if (profile != null) Object.DestroyImmediate(profile);
                if (missingStateProfile != null)
                    Object.DestroyImmediate(missingStateProfile);
                DestroyAndUnloadScene(scene);
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static GameObject CreateIsolatedRecoverySupport(
            Scene scene,
            HumanoidRagdollRig rig,
            PlanetMotor motor,
            Vector3 localUp)
        {
            Assert.That(rig, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(motor.Body, Is.Not.Null);
            Assert.That(motor.GroundMask.value, Is.Not.EqualTo(0));

            int disabledForeignColliders = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                foreach (Collider collider in colliders)
                {
                    if (collider == null || !collider.enabled ||
                        collider.transform.IsChildOf(motor.transform))
                        continue;

                    collider.enabled = false;
                    disabledForeignColliders++;
                }
            }
            Assert.That(disabledForeignColliders, Is.GreaterThan(0),
                "The recovery fixture must isolate the selected rig from foreign scene colliders.");

            const float supportThickness = 0.25f;
            Vector3 up = localUp.sqrMagnitude > 0.25f
                ? localUp.normalized
                : motor.transform.up;
            var support = GameObject.CreatePrimitive(PrimitiveType.Cube);
            support.name = "Pose-Matched Recovery Isolated Stable Support";
            SceneManager.MoveGameObjectToScene(support, scene);
            support.layer = FirstLayerInMask(motor.GroundMask.value);
            support.transform.rotation = Quaternion.FromToRotation(Vector3.up, up);
            support.transform.localScale = new Vector3(12f, supportThickness, 12f);
            support.transform.position = motor.SupportFeetPoint(up) -
                                         up * (supportThickness * 0.5f + 0.01f);
            UnityEngine.Physics.SyncTransforms();
            return support;
        }

        private static int FirstLayerInMask(int mask)
        {
            for (int layer = 0; layer < 32; layer++)
            {
                if ((mask & (1 << layer)) != 0) return layer;
            }
            return 0;
        }

        private static void TeleportKinematicBody(
            Rigidbody body,
            Vector3 position,
            Quaternion rotation)
        {
            Assert.That(body, Is.Not.Null);
            Assert.That(body.isKinematic, Is.True,
                "Recovery support probe teleports require a frozen Rigidbody.");
            body.position = position;
            body.rotation = rotation;
            body.transform.SetPositionAndRotation(position, rotation);
            UnityEngine.Physics.SyncTransforms();
            Assert.That(Vector3.Distance(body.position, position),
                Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(body.rotation, rotation),
                Is.LessThan(0.01f));
        }

        private static Vector3 SampleAuthoredRecoveryPelvisOffset(
            Animator animator,
            EarthTransitionDirector transitionOwner,
            Rigidbody motorBody,
            int recoveryStateHash,
            float entryPhase)
        {
            Assert.That(animator, Is.Not.Null);
            Assert.That(transitionOwner, Is.Not.Null);
            Assert.That(motorBody, Is.Not.Null);
            Assert.That(animator.IsInTransition(0), Is.False,
                "Authored recovery sampling requires a stable prior base state.");

            AnimatorStateInfo priorAnimatorState = animator.GetCurrentAnimatorStateInfo(0);
            EarthMotionStateId priorSemanticState = transitionOwner.ActiveState;
            int priorActiveStateHash = transitionOwner.ActiveStateHash;
            CharacterPhysicalMode priorOwnerMode = transitionOwner.BaseStateOwnerMode;
            int priorOwnedStateHash = transitionOwner.OwnedBaseStateHash;
            float priorPhase = Mathf.Repeat(priorAnimatorState.normalizedTime, 1f);
            uint evaluationBefore = transitionOwner.ImmediateEvaluationSequence;
            Transform authoredPelvis = animator.GetBoneTransform(HumanBodyBones.Hips);
            Assert.That(authoredPelvis, Is.Not.Null);
            Assert.That(priorAnimatorState.fullPathHash, Is.Not.EqualTo(0));
            Assert.That(priorOwnerMode, Is.EqualTo(CharacterPhysicalMode.AnimatedMotor));
            Assert.That(priorOwnedStateHash, Is.EqualTo(0));

            AnimatorStateInfo sampledState = default;
            Vector3 pelvisOffsetLocal = default;
            try
            {
                transitionOwner.ForcePlayImmediate(
                    EarthMotionStateId.KnockdownRecovery,
                    recoveryStateHash,
                    entryPhase);
                sampledState = animator.GetCurrentAnimatorStateInfo(0);
                pelvisOffsetLocal = Quaternion.Inverse(motorBody.rotation) *
                                    (authoredPelvis.position - motorBody.position);
            }
            finally
            {
                transitionOwner.ForcePlayImmediate(
                    priorSemanticState,
                    priorAnimatorState.fullPathHash,
                    priorPhase);
                transitionOwner.SynchronizeState(
                    priorSemanticState,
                    priorActiveStateHash,
                    EarthAnimationTransitionPriority.Idle);
                transitionOwner.SynchronizeBaseStateOwnership(
                    priorOwnerMode,
                    priorOwnedStateHash);
            }

            Assert.That(sampledState.fullPathHash, Is.EqualTo(recoveryStateHash));
            Assert.That(Mathf.Repeat(sampledState.normalizedTime, 1f),
                Is.EqualTo(entryPhase).Within(0.001f));
            Assert.That(transitionOwner.ImmediateEvaluationSequence,
                Is.EqualTo(evaluationBefore + 2u),
                "Sampling and restoration must each evaluate through the sole owner.");
            Assert.That(IsFinite(pelvisOffsetLocal), Is.True,
                "Recovery samples require a finite pelvis offset in motor-root space.");

            AnimatorStateInfo restoredState = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(restoredState.fullPathHash,
                Is.EqualTo(priorAnimatorState.fullPathHash));
            Assert.That(Mathf.Repeat(restoredState.normalizedTime, 1f),
                Is.EqualTo(priorPhase).Within(0.001f));
            Assert.That(transitionOwner.ActiveState, Is.EqualTo(priorSemanticState));
            Assert.That(transitionOwner.ActiveStateHash, Is.EqualTo(priorActiveStateHash));
            Assert.That(transitionOwner.BaseStateOwnerMode, Is.EqualTo(priorOwnerMode));
            Assert.That(transitionOwner.OwnedBaseStateHash, Is.EqualTo(priorOwnedStateHash));
            return pelvisOffsetLocal;
        }

        private static EarthRecoveryPoseSampleAuthoring RecoverySample(
            uint clipId,
            EarthRecoveryOrientation orientation,
            Vector3 pelvisOffsetLocal,
            in EarthRecoveryMarkerAuthoring markers,
            string animationStatePath = "Base Layer.Knockdown Recovery") =>
            new EarthRecoveryPoseSampleAuthoring(
                clipId,
                animationStatePath,
                orientation,
                0.55f,
                pelvisOffsetLocal,
                new Vector3(0f, 0.4f, 0.1f),
                new Vector3(-0.45f, 0.1f, 0.15f),
                new Vector3(0.45f, 0.1f, 0.15f),
                new Vector3(-0.2f, -0.7f, 0f),
                new Vector3(0.2f, -0.7f, 0f),
                Vector3.up,
                in markers);

        private static EarthWorldResponseEvent Response(
            uint responseId,
            EarthCharacterImpactResponse response,
            Vector3 point) => new EarthWorldResponseEvent(
            responseId,
            100u,
            200u,
            1u,
            response is EarthCharacterImpactResponse.Knockout or
                EarthCharacterImpactResponse.RecoverableKnockdown
                ? EarthWorldResponseKind.Knockdown
                : EarthWorldResponseKind.CharacterImpact,
            EarthCharacterImpactSourceKind.Physics,
            response,
            new float3(point.x, point.y, point.z),
            new float3(0f, 1f, 0f),
            new float3(1f, 0f, 0f),
            10f,
            50f,
            0.65f);

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static T[] FindInSceneAll<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<T>(true));
            return found.ToArray();
        }
    }
}
