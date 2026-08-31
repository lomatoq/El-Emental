using System;
using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Elemental.Tests.PlayMode
{
    public sealed class ActiveRagdollRuntimeTests
    {
        [UnityTest]
        public IEnumerator EarthCorePuppetRemainsGroundedWhenPlayerProvidesNoInput()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            ActiveRagdollPuppet puppet = FindInScene<ActiveRagdollPuppet>(scene);
            Assert.That(puppet, Is.Not.Null);
            Rigidbody body = puppet.GetComponent<Rigidbody>();
            float startRadius = body.position.magnitude;
            float peakSpeed = 0f;
            float peakRadius = startRadius;
            for (int tick = 0; tick < 180; tick++)
            {
                yield return new WaitForFixedUpdate();
                peakSpeed = Mathf.Max(peakSpeed, body.linearVelocity.magnitude);
                peakRadius = Mathf.Max(peakRadius, body.position.magnitude);
            }

            Assert.That(peakRadius, Is.LessThan(startRadius + 1.5f),
                $"Idle active ragdoll escaped the planet; peak speed was {peakSpeed:0.00} m/s.");
            Assert.That(body.position.magnitude, Is.LessThan(26.5f));
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(2f));

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator CharacterFeelLab_RepeatedImpactsRemainFinite()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "Assets/Elemental/Content/Scenes/CharacterFeelLab.unity",
                LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;

            ActiveRagdollPuppet puppet = Object.FindAnyObjectByType<ActiveRagdollPuppet>();
            CharacterFeelLabDriver driver = Object.FindAnyObjectByType<CharacterFeelLabDriver>();
            Assert.That(puppet, Is.Not.Null);
            Assert.That(driver, Is.Not.Null);

            for (int tick = 0; tick < 300; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Rigidbody rootBody = puppet.GetComponent<Rigidbody>();
            Assert.That(driver.PulseCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(IsFinite(rootBody.position), Is.True);
            Assert.That(IsFinite(rootBody.linearVelocity), Is.True);
            Assert.That(IsFinite(rootBody.angularVelocity), Is.True);
            Assert.That(float.IsFinite(puppet.CurrentState.StaggerDebt), Is.True);
            Assert.That(float.IsFinite(puppet.MaximumJointError), Is.True);

            ActiveRagdollJoint[] joints = Object.FindObjectsByType<ActiveRagdollJoint>();
            Assert.That(joints.Length, Is.GreaterThanOrEqualTo(6));
            for (int index = 0; index < joints.Length; index++)
            {
                Assert.That(IsFinite(joints[index].Body.linearVelocity), Is.True);
                Assert.That(IsFinite(joints[index].Body.angularVelocity), Is.True);
                Assert.That(joints[index].Body.angularVelocity.magnitude, Is.LessThan(100f));
            }

            AsyncOperation unload = SceneManager.UnloadSceneAsync(
                SceneManager.GetSceneByPath("Assets/Elemental/Content/Scenes/CharacterFeelLab.unity"));
            if (unload != null)
            {
                yield return unload;
            }
        }

        [UnityTest]
        public IEnumerator PuppetDisablesMotorDrivesOnRagdollAndRecoversWithoutExplosion()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Ragdoll Test Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(10f, 1f, 10f);

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
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
            ConfigurableJoint configurableJoint = chestObject.AddComponent<ConfigurableJoint>();
            configurableJoint.connectedBody = rootBody;
            ActiveRagdollJoint joint = chestObject.AddComponent<ActiveRagdollJoint>();
            joint.Configure(chestBody, configurableJoint, targetObject.transform, 500f, 50f, 800f, 45f);

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
            {
                puppet.InjectImpact(20f);
            }

            yield return new WaitForFixedUpdate();
            Assert.That(puppet.CurrentState.Mode, Is.EqualTo(CharacterPhysicalMode.FullRagdoll));
            Assert.That(configurableJoint.slerpDrive.maximumForce, Is.EqualTo(0f).Within(0.001f));

            for (int index = 0; index < 70; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(puppet.CurrentState.Mode, Is.EqualTo(CharacterPhysicalMode.AnimatedMotor));
            Assert.That(puppet.CurrentState.MuscleStrength, Is.EqualTo(1f).Within(0.001f));
            Assert.That(configurableJoint.slerpDrive.maximumForce, Is.GreaterThan(0f));
            Assert.That(float.IsFinite(puppet.MaximumJointError), Is.True);
            Assert.That(float.IsFinite(chestBody.angularVelocity.x), Is.True);
            Assert.That(chestBody.angularVelocity.magnitude, Is.LessThan(50f));

            Object.Destroy(root);
            Object.Destroy(floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HumanoidRecoveryPreservesLegacyFallbackAndMarkersOwnPoseMatchedHandoff()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            HumanoidRagdollRig rig = FindInScene<HumanoidRagdollRig>(scene);
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

            Light feetOwner = rig.gameObject.AddComponent<Light>();
            AudioSource controlOwner = rig.gameObject.AddComponent<AudioSource>();
            Animation proceduralOwner = rig.gameObject.AddComponent<Animation>();
            var profile = ScriptableObject.CreateInstance<EarthPhysicalAnimationProfile>();
            EarthRecoveryMarkerAuthoring markers =
                new EarthRecoveryMarkerAuthoring(0.56f, 0.80f, 0.95f);
            profile.ConfigureRecovery(
                true,
                new[]
                {
                    RecoverySample(101u, EarthRecoveryOrientation.Front, in markers),
                    RecoverySample(102u, EarthRecoveryOrientation.Back, in markers),
                    RecoverySample(103u, EarthRecoveryOrientation.Left, in markers),
                    RecoverySample(104u, EarthRecoveryOrientation.Right, in markers)
                });
            rig.ConfigurePhysicalAnimation(
                profile,
                new Behaviour[] { feetOwner },
                new Behaviour[] { controlOwner },
                new Behaviour[] { proceduralOwner });

            rig.BeginRagdoll(Vector3.zero);
            yield return new WaitForFixedUpdate();
            Assert.That(feetOwner.enabled, Is.False);
            Assert.That(controlOwner.enabled, Is.False);
            Assert.That(proceduralOwner.enabled, Is.False);
            int recoveryHandoffsBefore = rig.RecoveryOwnershipHandoffCount;
            Animator recoveryAnimator = rig.GetComponentInChildren<Animator>(true);
            Assert.That(recoveryAnimator, Is.Not.Null);
            bool observedSelectedStateInEvent = false;
            int eventStateHash = 0;
            float eventStatePhase = 0f;
            Action<AuthoredRecoveryHandoff> observeSelectedState = handoff =>
            {
                if (!handoff.HasSelectedState) return;
                AnimatorStateInfo state = recoveryAnimator.GetCurrentAnimatorStateInfo(0);
                observedSelectedStateInEvent = true;
                eventStateHash = state.fullPathHash;
                eventStatePhase = Mathf.Repeat(state.normalizedTime, 1f);
            };
            rig.AuthoredRecoveryBegan += observeSelectedState;
            rig.RecoverToAnimated(localUp, preferredForward, false);
            rig.RecoverToAnimated(localUp, preferredForward, false);

            Assert.That(rig.UsedPoseMatchedRecovery, Is.True);
            Assert.That(rig.LastPoseMatchedRecovery.IsValid, Is.True);
            Assert.That(rig.RecoveryOwnershipHandoffCount,
                Is.EqualTo(recoveryHandoffsBefore + 1),
                "Repeated recovery requests must not hand Animator ownership over twice.");
            Assert.That(feetOwner.enabled, Is.False,
                "Feet must remain disabled before their authored marker.");
            Assert.That(observedSelectedStateInEvent, Is.True);
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
            yield return new WaitForEndOfFrame();
            Assert.That(rig.RecoveryStateHashNextFrame,
                Is.EqualTo(rig.LastPoseMatchedRecovery.AnimationStateId));
            Assert.That(rig.RecoveryStatePhaseNextFrame,
                Is.EqualTo(0.55f).Within(0.08f));
            Assert.That(rig.RecoveryStateVerifiedNextFrame, Is.True);
            rig.AuthoredRecoveryBegan -= observeSelectedState;

            int supportWaitFrames = 0;
            while ((!rig.RecoveryHasLiveSupport || !rig.RecoveryFeetEnabled) &&
                   supportWaitFrames++ < 4)
                yield return null;
            Assert.That(rig.RecoveryHasLiveSupport, Is.True);
            Assert.That(rig.RecoveryFeetEnabled, Is.True);
            Assert.That(motor, Is.Not.Null);
            Assert.That(motor.Body, Is.Not.Null);
            int supportSamplesBeforeLoss = rig.RecoverySupportSampleCount;
            bool motorWasEnabled = motor.enabled;
            motor.enabled = false;
            Vector3 supportedRootPosition = motor.Body.position;
            Quaternion supportedRootRotation = motor.Body.rotation;
            motor.Body.position = supportedRootPosition + localUp * 3f;
            UnityEngine.Physics.SyncTransforms();
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.That(rig.RecoverySupportSampleCount,
                Is.GreaterThan(supportSamplesBeforeLoss));
            Assert.That(rig.RecoveryHasLiveSupport, Is.False);
            Assert.That(rig.RecoveryFeetEnabled, Is.False,
                "Live support loss must revoke feet while movement control is disabled.");

            motor.Body.position = supportedRootPosition;
            motor.Body.rotation = supportedRootRotation;
            UnityEngine.Physics.SyncTransforms();
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.That(rig.RecoveryHasLiveSupport, Is.True);
            Assert.That(rig.RecoveryFeetEnabled, Is.True,
                "Live support reacquisition must re-enable marker ownership.");
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
            yield return new WaitForFixedUpdate();
            rig.RecoverToAnimated(localUp, preferredForward, false);
            rig.RecoverToAnimated(localUp, preferredForward, false);
            Assert.That(rig.RecoveryOwnershipHandoffCount,
                Is.EqualTo(recoveryHandoffsBefore + 2));

            int renderedFrames = 0;
            while (rig.IsRecoveringToAnimation && renderedFrames++ < 180)
                yield return null;

            Assert.That(rig.IsRecoveringToAnimation, Is.False,
                "A valid supported recovery must reach its exit marker.");
            Assert.That(feetOwner.enabled, Is.True);
            Assert.That(controlOwner.enabled, Is.True);
            Assert.That(proceduralOwner.enabled, Is.True);
            Assert.That(rig.PhysicalAnimationMode,
                Is.EqualTo(CharacterPhysicalMode.AnimatedMotor));
            Assert.That(rig.PhysicalOwnershipConsistent, Is.True);
            rig.CompleteRecovery();
            rig.ResetToAnimated();

            var missingStateProfile =
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
                        in fallbackMarkers,
                        "Base Layer.State That Does Not Exist"),
                    RecoverySample(
                        502u,
                        EarthRecoveryOrientation.Back,
                        in fallbackMarkers,
                        "Base Layer.State That Does Not Exist"),
                    RecoverySample(
                        503u,
                        EarthRecoveryOrientation.Left,
                        in fallbackMarkers,
                        "Base Layer.State That Does Not Exist"),
                    RecoverySample(
                        504u,
                        EarthRecoveryOrientation.Right,
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

            Object.Destroy(profile);
            Object.Destroy(missingStateProfile);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static EarthRecoveryPoseSampleAuthoring RecoverySample(
            uint clipId,
            EarthRecoveryOrientation orientation,
            in EarthRecoveryMarkerAuthoring markers,
            string animationStatePath = "Base Layer.Knockdown Recovery") =>
            new EarthRecoveryPoseSampleAuthoring(
                clipId,
                animationStatePath,
                orientation,
                0.55f,
                new Vector3(0f, 0.9f, 0f),
                new Vector3(0f, 0.4f, 0.1f),
                new Vector3(-0.45f, 0.1f, 0.15f),
                new Vector3(0.45f, 0.1f, 0.15f),
                new Vector3(-0.2f, -0.7f, 0f),
                new Vector3(0.2f, -0.7f, 0f),
                Vector3.up,
                in markers);

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
