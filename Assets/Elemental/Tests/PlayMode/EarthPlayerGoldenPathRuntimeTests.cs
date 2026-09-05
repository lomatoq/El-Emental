using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Elemental.Input.Actions;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Camera;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthPlayerGoldenPathRuntimeTests
    {
        [UnityTest]
        public IEnumerator ProductionCameraDoesNotCollapseIntoArmorOrReleasedPlates()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool loadedForTest = !scene.IsValid() || !scene.isLoaded;
            if (loadedForTest)
            {
                yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByPath(scenePath);
            }
            GameObject player = FindByName(scene, "Planet Character");
            PlanetMotor motor = player != null ? player.GetComponent<PlanetMotor>() : null;
            EarthArmorController armor = player != null ? player.GetComponent<EarthArmorController>() : null;
            EarthCinemachineCameraController cameraController =
                FindInScene<EarthCinemachineCameraController>(scene);
            UnityEngine.Camera gameplayCamera = FindInScene<UnityEngine.Camera>(scene);
            EarthMvpBotController bot = FindInScene<EarthMvpBotController>(scene);
            bool botWasEnabled = bot != null && bot.enabled;
            if (bot != null) bot.enabled = false;
            Assert.That(motor, Is.Not.Null);
            Assert.That(armor, Is.Not.Null);
            Assert.That(cameraController, Is.Not.Null);
            Assert.That(gameplayCamera, Is.Not.Null);
            Assert.That(cameraController.WorldUpFrame, Is.Not.Null,
                "The production camera must retain or recover its spherical world-up frame.");
            Assert.That(cameraController.AimPivot, Is.Not.Null,
                "The production camera must retain or recover its Cinemachine tracking target.");
            Assert.That(cameraController.TracksAimPivot, Is.True,
                "The gameplay virtual camera must track the local-up aim pivot.");
            Assert.That(cameraController.IgnoresControlledMagic, Is.True);
            Assert.That(cameraController.HasSphericalClearance, Is.True,
                "The production Cinemachine rig must own a spherical-world final clearance constraint.");

            for (int frame = 0; frame < 20; frame++) yield return null;
            float baseline = Vector3.Distance(gameplayCamera.transform.position, motor.transform.position);
            float minimumDistance = baseline;
            int maximumHiddenArmorPieces = 0;
            Assert.That(armor.Begin(), Is.True);
            for (int frame = 0; frame < 35; frame++)
            {
                cameraController.RefreshArmorVisibilityNow();
                maximumHiddenArmorPieces = Mathf.Max(
                    maximumHiddenArmorPieces,
                    cameraController.HiddenArmorPieceCount);
                minimumDistance = Mathf.Min(minimumDistance,
                    Vector3.Distance(gameplayCamera.transform.position, motor.transform.position));
                yield return null;
            }
            for (int step = 0; step < 6; step++) armor.ApplyWheel(120f, Time.unscaledTime);
            for (int frame = 0; frame < 35; frame++)
            {
                cameraController.RefreshArmorVisibilityNow();
                maximumHiddenArmorPieces = Mathf.Max(
                    maximumHiddenArmorPieces,
                    cameraController.HiddenArmorPieceCount);
                minimumDistance = Mathf.Min(minimumDistance,
                    Vector3.Distance(gameplayCamera.transform.position, motor.transform.position));
                yield return null;
            }
            armor.ReleaseAsDebris();
            for (int frame = 0; frame < 20; frame++)
            {
                minimumDistance = Mathf.Min(minimumDistance,
                    Vector3.Distance(gameplayCamera.transform.position, motor.transform.position));
                yield return null;
            }

            Assert.That(minimumDistance, Is.GreaterThan(Mathf.Max(4.5f, baseline * 0.72f)),
                $"Armor collapsed the camera from {baseline:F2} m to {minimumDistance:F2} m.");
            VoxelPlanetBehaviour planet = FindInScene<VoxelPlanetBehaviour>(scene);
            Assert.That(planet, Is.Not.Null);
            float surfaceClearance = Vector3.Distance(
                gameplayCamera.transform.position,
                planet.transform.position) - planet.Radius;
            Assert.That(surfaceClearance, Is.GreaterThanOrEqualTo(0.38f),
                "The long armor camera arm must stay outside the spherical surface instead of being collision-pulled into the hero.");
            Assert.That(maximumHiddenArmorPieces, Is.Zero,
                "The complete compact shell must remain rendered; camera readability may not punch holes through armor.");
            if (bot != null) bot.enabled = botWasEnabled;
            if (loadedForTest) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ProductionArmorStartsOffAndCoversEveryVisibleBodyRegion()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool loadedForTest = !scene.IsValid() || !scene.isLoaded;
            if (loadedForTest)
            {
                yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByPath(scenePath);
            }
            GameObject player = FindByName(scene, "Planet Character");
            PlanetMotor motor = player != null ? player.GetComponent<PlanetMotor>() : null;
            EarthArmorController armor = player != null
                ? player.GetComponent<EarthArmorController>()
                : null;
            HumanoidCharacterPresentation presentation = player != null
                ? player.GetComponentInChildren<HumanoidCharacterPresentation>(true)
                : null;
            Assert.That(armor, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(armor.IsActive, Is.False,
                "Armor is an input-owned hold spell and must never assemble on scene startup.");

            EarthMvpBotController bot = FindInScene<EarthMvpBotController>(scene);
            bool botWasEnabled = bot != null && bot.enabled;
            if (bot != null) bot.enabled = false;
            for (int frame = 0; frame < 24; frame++) yield return null;
            motor.SettleTangentialMotion();
            Animator animator = presentation.Animator;
            SkinnedMeshRenderer visibleBody = FindVisibleHumanoidRenderer(animator);
            Assert.That(visibleBody, Is.Not.Null,
                "The production Humanoid must expose one enabled skinned body renderer.");
            float humanScale = Mathf.Max(0.75f, animator.humanScale);
            string[] labels = { "head", "torso", "left arm", "right arm", "left leg", "right leg" };
            int[] minimumTiles = { 10, 10, 4, 4, 8, 8 };

            Assert.That(armor.Begin(), Is.True);
            for (int tick = 0; tick < 32; tick++) yield return new WaitForFixedUpdate();
            // The production actor may legitimately move while the armor gathers
            // (player input, moving support or spherical settling). Measure the
            // animated body at the same instant as the fitted plates; comparing a
            // stale pre-cast pose to the live shell falsely reports every region as
            // uncovered even when the shell follows the correct Humanoid.
            Bounds[] bodyRegions =
            {
                GetHumanoidRegionBounds(animator, humanScale * 0.16f,
                    HumanBodyBones.Head, HumanBodyBones.Neck),
                GetHumanoidRegionBounds(animator, humanScale * 0.19f,
                    HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
                    HumanBodyBones.UpperChest, HumanBodyBones.Neck),
                GetHumanoidRegionBounds(animator, humanScale * 0.11f,
                    HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
                GetHumanoidRegionBounds(animator, humanScale * 0.11f,
                    HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
                GetHumanoidRegionBounds(animator, humanScale * 0.13f,
                    HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
                GetHumanoidRegionBounds(animator, humanScale * 0.13f,
                    HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot)
            };
            HumanBodyBones[][] regionBones =
            {
                new[] { HumanBodyBones.Head, HumanBodyBones.Neck },
                new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
                    HumanBodyBones.UpperChest, HumanBodyBones.Neck },
                new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand },
                new[] { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand },
                new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot },
                new[] { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot }
            };
            for (int region = 0; region < bodyRegions.Length; region++)
                Assert.That(bodyRegions[region].size.sqrMagnitude, Is.GreaterThan(0f),
                    $"Missing valid Humanoid bones for the visible {labels[region]} region.");
            var pieces = new EarthArmorPiece[EarthArmorProfile.MaximumPieceCount];
            int count = armor.CopyActivePiecesNonAlloc(pieces);
            Assert.That(count, Is.EqualTo(EarthArmorProfile.MaximumPieceCount));
            for (int region = 0; region < bodyRegions.Length; region++)
            {
                Bounds regionBounds = bodyRegions[region];
                float allowance = Mathf.Max(0.18f, regionBounds.extents.magnitude * 0.34f);
                int nearby = 0;
                float farthestFaceGap = 0f;
                for (int piece = 0; piece < count; piece++)
                {
                    if (regionBounds.SqrDistance(pieces[piece].transform.position) <= allowance * allowance)
                        nearby++;
                }

                var coverageSamples = new List<Vector3>(6);
                if (region <= 1)
                {
                    coverageSamples.Add(regionBounds.center + Vector3.right * regionBounds.extents.x);
                    coverageSamples.Add(regionBounds.center - Vector3.right * regionBounds.extents.x);
                    coverageSamples.Add(regionBounds.center + Vector3.up * regionBounds.extents.y);
                    coverageSamples.Add(regionBounds.center - Vector3.up * regionBounds.extents.y);
                    coverageSamples.Add(regionBounds.center + Vector3.forward * regionBounds.extents.z);
                    coverageSamples.Add(regionBounds.center - Vector3.forward * regionBounds.extents.z);
                }
                else
                {
                    // A diagonal animated limb does not occupy all six face centres
                    // of its world-axis AABB. Those empty corners move every gait
                    // frame and previously produced random "uncovered limb" failures.
                    // Probe the actual Humanoid joint chain against physical plate
                    // surfaces instead: shoulder/forearm/hand or hip/shin/foot.
                    HumanBodyBones[] bones = regionBones[region];
                    for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                    {
                        Transform bone = animator.GetBoneTransform(bones[boneIndex]);
                        if (bone != null) coverageSamples.Add(bone.position);
                    }
                }
                for (int sample = 0; sample < coverageSamples.Count; sample++)
                {
                    float nearest = float.PositiveInfinity;
                    for (int piece = 0; piece < count; piece++)
                    {
                        Collider plateCollider = pieces[piece].PieceCollider;
                        Vector3 nearestPlatePoint = plateCollider != null && plateCollider.enabled
                            ? plateCollider.ClosestPoint(coverageSamples[sample])
                            : pieces[piece].transform.position;
                        nearest = Mathf.Min(nearest,
                            Vector3.Distance(coverageSamples[sample], nearestPlatePoint));
                    }
                    farthestFaceGap = Mathf.Max(farthestFaceGap, nearest);
                }

                Assert.That(nearby, Is.GreaterThanOrEqualTo(minimumTiles[region]),
                    $"The compact shell leaves the visible {labels[region]} exposed: " +
                    $"only {nearby} fitted tiles near the Humanoid bone region, bounds={regionBounds}.");
                Assert.That(farthestFaceGap, Is.LessThanOrEqualTo(allowance + 0.16f),
                    $"The compact shell has a large uncovered face on the visible {labels[region]} " +
                    $"(gap {farthestFaceGap:F3} m, bounds={regionBounds}).");
            }

            armor.ReleaseAsDebris();
            for (int tick = 0; tick < 10; tick++) yield return new WaitForFixedUpdate();
            if (bot != null) bot.enabled = botWasEnabled;
            if (loadedForTest) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ProductionMageStandsWalksAndAnimatesWithoutCameraChaos()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool loadedForTest = !scene.IsValid() || !scene.isLoaded;
            if (loadedForTest)
            {
                yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByPath(scenePath);
            }
            GameObject player = FindByName(scene, "Planet Character");
            PlanetMotor motor = player != null ? player.GetComponent<PlanetMotor>() : null;
            ActiveRagdollPuppet puppet = player != null
                ? player.GetComponent<ActiveRagdollPuppet>()
                : null;
            HumanoidCharacterPresentation presentation = player != null
                ? player.GetComponentInChildren<HumanoidCharacterPresentation>(true)
                : null;
            EarthCharacterPoseController poseController = player != null
                ? player.GetComponentInChildren<EarthCharacterPoseController>(true)
                : null;
            EarthFootContactController footContactController = presentation != null
                ? presentation.FootContactController
                : null;
            PlanetCameraRig cameraRig = FindInScene<PlanetCameraRig>(scene);
            EarthCinemachineCameraController cameraController =
                FindInScene<EarthCinemachineCameraController>(scene);
            UnityEngine.Camera gameplayCamera = FindInScene<UnityEngine.Camera>(scene);
            EarthMvpBotController bot = FindInScene<EarthMvpBotController>(scene);
            bool botWasEnabled = bot != null && bot.enabled;
            if (bot != null) bot.enabled = false;
            Assert.That(motor, Is.Not.Null);
            Assert.That(puppet, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(poseController, Is.Not.Null);
            Assert.That(footContactController, Is.Not.Null,
                "Every visible Humanoid must own the independent pair-wise foot contact controller.");
            Assert.That(presentation.ProceduralBodyResponse, Is.Not.Null,
                "The production Humanoid must own the final bounded upper-body response pass.");
            Assert.That(cameraRig, Is.Not.Null);
            Assert.That(cameraController, Is.Not.Null);
            Assert.That(cameraController.IsLive, Is.True,
                "The production camera must be owned by the local-up Cinemachine rig.");

            Rigidbody body = motor.GetComponent<Rigidbody>();
            Animator animator = presentation.Animator;
            Assert.That(body, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            SkinnedMeshRenderer visibleBody = FindVisibleHumanoidRenderer(animator);
            Assert.That(visibleBody, Is.Not.Null,
                "The visible character must expose an enabled skinned body mesh, not only animated bones.");
            for (int tick = 0; tick < 150; tick++) yield return new WaitForFixedUpdate();

            float idleUpright = Vector3.Dot(body.transform.up, motor.LocalUp);
            EarthMvpDuelController duel = FindInScene<EarthMvpDuelController>(scene);
            Assert.That(motor.enabled, Is.True,
                $"The production motor was disabled during an ordinary idle. " +
                $"mode={puppet.CurrentState.Mode}, balance={puppet.CurrentState.BalanceError:F3}, " +
                $"debt={puppet.CurrentState.StaggerDebt:F3}, body={body.position}, " +
                $"velocity={body.linearVelocity}, angular={body.angularVelocity}, " +
                $"grounded={motor.IsGrounded}, stable={motor.HasStableSupport}, " +
                $"playerPhase={duel?.PlayerPhase}, knockouts={duel?.PlayerKnockoutCount}.");
            Assert.That(puppet.CurrentState.Mode, Is.Not.EqualTo(CharacterPhysicalMode.FullRagdoll),
                "Spawn/support contacts must not put the player into FullRagdoll.");
            Assert.That(idleUpright, Is.GreaterThan(0.9f),
                $"The Mage must visibly stand before accepting input (upright dot {idleUpright:F3}).");
            Assert.That(body.angularVelocity.magnitude, Is.LessThan(1.5f),
                "An idle Mage may not keep tumbling on its support.");

            ScriptedMotorInput scripted = motor.gameObject.AddComponent<ScriptedMotorInput>();
            // A straight 7.2 m/s run from the arena centre reaches the 16 m crown
            // wall during this 120-tick sample. That correctly drops measured gait
            // speed to idle, but used to make the QA gate demand running in place.
            // Follow a bounded tank-steering arc so two complete gait cycles are
            // sampled on free combat floor instead of against architecture.
            scripted.Move = new float2(0.65f, 1f);
            motor.ConfigureInputSource(scripted);
            Vector3 start = body.position;
            Vector3 startUp = motor.LocalUp;
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Transform rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            Assert.That(leftFoot, Is.Not.Null);
            Assert.That(rightFoot, Is.Not.Null);
            Vector3 initialFootLocal = animator.transform.InverseTransformPoint(leftFoot.position);
            var leftLegProbe = new VisibleGeometryProbe(visibleBody, animator.transform,
                leftUpperLeg,
                leftLowerLeg,
                leftFoot);
            var rightLegProbe = new VisibleGeometryProbe(visibleBody, animator.transform,
                rightUpperLeg,
                rightLowerLeg,
                rightFoot);
            EarthAnimationMotionAuditState motionAudit = default;
            var motionTrace = new StringBuilder(32768);
            motionTrace.AppendLine(
                "frame,time,dt,speed,gaitRate,ikWeight,leftIkWeight,rightIkWeight,pelvis," +
                "leftLocked,rightLocked," +
                "supported,supportId,supportGeneration,leftX,leftY,leftZ,rightX,rightY,rightZ," +
                "leftAnchorError,rightAnchorError,leftKneeX,leftKneeY,leftKneeZ," +
                "rightKneeX,rightKneeY,rightKneeZ,leftReason,rightReason," +
                 "leftReleaseCooldown,rightReleaseCooldown,animatorState,normalizedTime," +
                 "inTransition,nextState,nextNormalizedTime,primaryClip,primaryClipWeight," +
                 "secondaryClip,secondaryClipWeight,discontinuity,authoredAction,footPolicy," +
                 "bodyPitch,bodyYaw,bodyRoll");
            float maximumFootTravel = 0f;
            float maximumVisibleLegTravel = 0f;
            float lateCycleVisibleTravel = 0f;
            float minimumUpright = 1f;
            float maximumCameraStep = 0f;
            float maximumCameraRadialStep = 0f;
            float maximumCameraAngleStep = 0f;
            int maximumCameraStepTick = -1;
            Vector3 maximumCameraStepFrom = Vector3.zero;
            Vector3 maximumCameraStepTo = Vector3.zero;
            float maximumAnimatorSpeed = 0f;
            float accumulatedTangentTravel = 0f;
            int previousDiscontinuityFrames = 0;
            Vector3 previousBodyPosition = body.position;
            Vector3 previousCameraPosition = cameraRig.transform.position;
            Quaternion previousCameraRotation = cameraRig.transform.rotation;
            float previousCameraRadius = Vector3.Distance(
                cameraRig.transform.position,
                body.position + motor.LocalUp * 0.92f);
            // Sample consecutive rendered poses. Combining WaitForFixedUpdate
            // with an additional null yield skipped one or two rendered Animator
            // evaluations between rows, while the audit still used only the last
            // frame's deltaTime. That mislabeled valid in-between gait motion as
            // a one-frame teleport.
            for (int tick = 0; tick < 120; tick++)
            {
                yield return null;
                Vector3 tangentStep = Vector3.ProjectOnPlane(
                    body.position - previousBodyPosition,
                    motor.LocalUp);
                accumulatedTangentTravel += tangentStep.magnitude;
                previousBodyPosition = body.position;
                minimumUpright = Mathf.Min(minimumUpright, Vector3.Dot(body.transform.up, motor.LocalUp));
                Vector3 footLocal = animator.transform.InverseTransformPoint(leftFoot.position);
                maximumFootTravel = Mathf.Max(maximumFootTravel, Vector3.Distance(initialFootLocal, footLocal));
                Vector3 rightFootLocal = animator.transform.InverseTransformPoint(rightFoot.position);
                Vector3 leftKneeDirection = animator.transform.InverseTransformDirection(
                    (leftLowerLeg.position - leftUpperLeg.position).normalized);
                Vector3 rightKneeDirection = animator.transform.InverseTransformDirection(
                    (rightLowerLeg.position - rightUpperLeg.position).normalized);
                uint supportId = footContactController.LeftSupportId != 0u
                    ? footContactController.LeftSupportId
                    : footContactController.RightSupportId;
                uint supportGeneration = footContactController.LeftSupportId != 0u
                    ? footContactController.LeftSupportGeneration
                    : footContactController.RightSupportGeneration;
                var auditSample = new EarthAnimationMotionSample(
                    Mathf.Max(0.0001f, Time.deltaTime),
                    ToFloat3(footLocal),
                    ToFloat3(rightFootLocal),
                    ToFloat3(leftKneeDirection),
                    ToFloat3(rightKneeDirection),
                    footContactController.FootIkWeight,
                    footContactController.LeftFootIkWeight,
                    footContactController.RightFootIkWeight,
                    footContactController.PelvisCorrectionMeters,
                    footContactController.LeftFootLocked,
                    footContactController.RightFootLocked,
                    motor.HasStableSupport,
                    supportId,
                    supportGeneration);
                EarthAnimationMotionAuditSummary frameAudit =
                    EarthAnimationMotionAudit.Step(ref motionAudit, in auditSample);
                bool discontinuity =
                    frameAudit.DiscontinuityFrames > previousDiscontinuityFrames;
                previousDiscontinuityFrames = frameAudit.DiscontinuityFrames;
                AnimatorStateInfo frameState = animator.GetCurrentAnimatorStateInfo(0);
                AnimatorStateInfo frameNextState = animator.GetNextAnimatorStateInfo(0);
                AnimatorClipInfo[] frameClips = animator.GetCurrentAnimatorClipInfo(0);
                string primaryClip = frameClips.Length > 0 && frameClips[0].clip != null
                    ? frameClips[0].clip.name
                    : "none";
                float primaryClipWeight = frameClips.Length > 0 ? frameClips[0].weight : 0f;
                string secondaryClip = frameClips.Length > 1 && frameClips[1].clip != null
                    ? frameClips[1].clip.name
                    : "none";
                float secondaryClipWeight = frameClips.Length > 1 ? frameClips[1].weight : 0f;
                float3 bodyAngles = presentation.ProceduralBodyResponse != null
                    ? presentation.ProceduralBodyResponse.CurrentAnglesDegrees
                    : float3.zero;
                motionTrace.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1:F6},{2:F6},{3:F5},{4:F5},{5:F5},{6:F5},{7:F5},{8:F5}," +
                    "{9},{10},{11},{12},{13},{14:F6},{15:F6},{16:F6},{17:F6},{18:F6}," +
                    "{19:F6},{20:F6},{21:F6},{22:F6},{23:F6},{24:F6},{25:F6},{26:F6}," +
                    "{27:F6},{28},{29},{30:F6},{31:F6},{32},{33:F6},{34},{35}," +
                    "{36:F6},{37},{38:F6},{39},{40:F6},{41},{42},{43},{44:F5},{45:F5},{46:F5}\n",
                    tick,
                    Time.time,
                    Time.deltaTime,
                    animator.GetFloat("Speed"),
                    animator.GetFloat("GaitRate"),
                    footContactController.FootIkWeight,
                    footContactController.LeftFootIkWeight,
                    footContactController.RightFootIkWeight,
                    footContactController.PelvisCorrectionMeters,
                    footContactController.LeftFootLocked ? 1 : 0,
                    footContactController.RightFootLocked ? 1 : 0,
                    motor.HasStableSupport ? 1 : 0,
                    supportId,
                    supportGeneration,
                    footLocal.x,
                    footLocal.y,
                    footLocal.z,
                    rightFootLocal.x,
                    rightFootLocal.y,
                    rightFootLocal.z,
                    footContactController.LeftAnchorErrorMeters,
                    footContactController.RightAnchorErrorMeters,
                    leftKneeDirection.x,
                    leftKneeDirection.y,
                    leftKneeDirection.z,
                    rightKneeDirection.x,
                    rightKneeDirection.y,
                    rightKneeDirection.z,
                    footContactController.LeftReason,
                    footContactController.RightReason,
                    footContactController.LeftReleaseCooldownSeconds,
                    footContactController.RightReleaseCooldownSeconds,
                    frameState.fullPathHash,
                    frameState.normalizedTime,
                    animator.IsInTransition(0) ? 1 : 0,
                    frameNextState.fullPathHash,
                    frameNextState.normalizedTime,
                    primaryClip,
                    primaryClipWeight,
                    secondaryClip,
                    secondaryClipWeight,
                    discontinuity ? 1 : 0,
                    presentation.CurrentAuthoredAction,
                    presentation.CurrentFootPolicy,
                    bodyAngles.x,
                    bodyAngles.y,
                    bodyAngles.z);
                maximumVisibleLegTravel = Mathf.Max(maximumVisibleLegTravel,
                    Mathf.Max(leftLegProbe.MeasureMaximumTravel(), rightLegProbe.MeasureMaximumTravel()));
                if (tick == 80)
                {
                    leftLegProbe.CaptureReferencePose();
                    rightLegProbe.CaptureReferencePose();
                }
                else if (tick > 80)
                    lateCycleVisibleTravel = Mathf.Max(
                        lateCycleVisibleTravel,
                        Mathf.Max(
                            leftLegProbe.MeasureMaximumTravelFromReference(),
                            rightLegProbe.MeasureMaximumTravelFromReference()));
                maximumAnimatorSpeed = Mathf.Max(maximumAnimatorSpeed, animator.GetFloat("Speed"));
                float cameraStep = Vector3.Distance(previousCameraPosition, cameraRig.transform.position);
                float cameraRadius = Vector3.Distance(
                    cameraRig.transform.position,
                    body.position + motor.LocalUp * 0.92f);
                maximumCameraRadialStep = Mathf.Max(
                    maximumCameraRadialStep,
                    Mathf.Abs(cameraRadius - previousCameraRadius));
                if (cameraStep > maximumCameraStep)
                {
                    maximumCameraStep = cameraStep;
                    maximumCameraStepTick = tick;
                    maximumCameraStepFrom = previousCameraPosition;
                    maximumCameraStepTo = cameraRig.transform.position;
                }
                maximumCameraAngleStep = Mathf.Max(maximumCameraAngleStep,
                    Quaternion.Angle(previousCameraRotation, cameraRig.transform.rotation));
                previousCameraPosition = cameraRig.transform.position;
                previousCameraRotation = cameraRig.transform.rotation;
                previousCameraRadius = cameraRadius;
                if (puppet.CurrentState.Mode == CharacterPhysicalMode.FullRagdoll)
                    Assert.Fail(
                        $"Ordinary walking entered FullRagdoll at tick {tick}; " +
                        $"balance={puppet.CurrentState.BalanceError:F3}, " +
                        $"debt={puppet.CurrentState.StaggerDebt:F3}, " +
                        $"upright={Vector3.Dot(body.transform.up, motor.LocalUp):F3}, " +
                        $"speed={body.linearVelocity.magnitude:F3}, angular={body.angularVelocity.magnitude:F3}, " +
                        $"contactPoint={puppet.LastCollisionImpact.Point}, " +
                        $"contactNormal={puppet.LastCollisionImpact.Normal}, " +
                        $"contactImpulse={puppet.LastCollisionImpact.Impulse:F3}, " +
                        $"otherDynamic={puppet.LastCollisionImpact.OtherBodyIsDynamic}, " +
                        $"classifiedSupport={puppet.LastCollisionWasSupport}.");
            }

            EarthAnimationMotionAuditSummary auditSummary =
                EarthAnimationMotionAudit.Snapshot(in motionAudit);
            WriteAnimationMotionAudit(motionTrace, in auditSummary);
            Assert.That(auditSummary.BothLockedFrames, Is.Zero,
                "Ordinary locomotion may never pin both feet to support anchors.");
            Assert.That(auditSummary.LeftLockTransitions, Is.GreaterThan(0),
                "The left foot never completed a measured stance hand-off.");
            Assert.That(auditSummary.RightLockTransitions, Is.GreaterThan(0),
                "The right foot never completed a measured stance hand-off.");
            Assert.That(auditSummary.MaximumIkWeightStep, Is.LessThanOrEqualTo(0.3001f),
                $"A per-foot IK weight popped by {auditSummary.MaximumIkWeightStep:F3} in one frame.");
            Assert.That(auditSummary.DiscontinuityFrames, Is.Zero,
                $"Visible locomotion contained {auditSummary.DiscontinuityFrames} untagged foot/knee/pelvis discontinuities.");
            Assert.That(auditSummary.MaximumKneeAngleStep, Is.LessThanOrEqualTo(8.001f),
                $"A knee direction jumped {auditSummary.MaximumKneeAngleStep:F2} degrees in one rendered frame.");
            Assert.That(auditSummary.MaximumPelvisStep, Is.LessThanOrEqualTo(0.0201f),
                $"Pelvis contact correction stepped {auditSummary.MaximumPelvisStep:F4} m in one rendered frame.");

            Vector3 tangentTravel = Vector3.ProjectOnPlane(body.position - start, startUp);
            Assert.That(accumulatedTangentTravel, Is.GreaterThan(3f),
                $"A real motor command must move the production Mage along its bounded arena arc; " +
                $"path={accumulatedTangentTravel:F3} m, chord={tangentTravel.magnitude:F3} m. " +
                "Setting Rigidbody velocity in QA is not proof.");
            Assert.That(minimumUpright, Is.GreaterThan(0.94f),
                $"The Mage fell over while walking (minimum upright dot {minimumUpright:F3}).");
            Assert.That(maximumAnimatorSpeed, Is.GreaterThan(1f),
                "Locomotion animation must react while the real motor is moving, even if an obstacle stops it later.");
            Assert.That(maximumFootTravel, Is.GreaterThan(0.05f),
                "The authored locomotion clip must visibly cycle the feet while the motor walks.");
            Assert.That(maximumVisibleLegTravel, Is.GreaterThan(0.035f),
                "The rendered leg geometry must move through the gait; moving only hidden Humanoid bones is not acceptable.");
            Assert.That(lateCycleVisibleTravel, Is.GreaterThan(0.015f),
                $"Visible gait froze after its first cycle instead of continuing while the motor walks. " +
                $"bodyVelocity={body.linearVelocity}, command={motor.LastCommand.Move}, " +
                $"speed={animator.GetFloat("Speed"):F2}, gaitRate={animator.GetFloat("GaitRate"):F2}, " +
                $"state={animator.GetCurrentAnimatorStateInfo(0).fullPathHash}, " +
                $"time={animator.GetCurrentAnimatorStateInfo(0).normalizedTime:F2}.");
            AnimatorClipInfo[] activeClips = animator.GetCurrentAnimatorClipInfo(0);
            Assert.That(activeClips.Length, Is.GreaterThan(0));
            for (int index = 0; index < activeClips.Length; index++)
                if (activeClips[index].weight > 0.01f)
                    Assert.That(activeClips[index].clip.isLooping, Is.True,
                        $"Active locomotion clip '{activeClips[index].clip.name}' must loop.");
            AnimatorStateInfo finalLocomotionState = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo nextLocomotionState = animator.GetNextAnimatorStateInfo(0);
            Assert.That(finalLocomotionState.IsName("Locomotion") ||
                        nextLocomotionState.IsName("Locomotion"), Is.True,
                $"Grounded walking must recover to Locomotion, not remain in state " +
                $"{finalLocomotionState.fullPathHash} at {finalLocomotionState.normalizedTime:F2}.");

            var dodgeTrace = new StringBuilder(4096);
            dodgeTrace.AppendLine(
                "frame,time,state,nextState,normalizedTime,authoredAction,footPolicy," +
                "leftIkWeight,rightIkWeight,primaryClip,bodyPitch,bodyYaw,bodyRoll");
            Assert.That(presentation.TryPlayDirectionalDodge(Vector2.right), Is.True,
                $"The live grounded player rejected the authored right dodge: " +
                $"{presentation.LastDodgeDecision.RejectReason}.");
            bool sawDirectionalDodgeAction = false;
            bool sawDodgeFlightWindow =
                presentation.CurrentFootPolicy == EarthAuthoredFootPolicy.FlightIkOff;
            bool sawDodgeContactWindow = false;
            bool sawKayKitDodgeClip = false;
            for (int dodgeFrame = 0; dodgeFrame < 36; dodgeFrame++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                AnimatorStateInfo dodgeState = animator.GetCurrentAnimatorStateInfo(0);
                AnimatorStateInfo dodgeNext = animator.GetNextAnimatorStateInfo(0);
                AnimatorClipInfo[] dodgeClips = animator.IsInTransition(0)
                    ? animator.GetNextAnimatorClipInfo(0)
                    : animator.GetCurrentAnimatorClipInfo(0);
                string primaryDodgeClip = dodgeClips.Length > 0 && dodgeClips[0].clip != null
                    ? dodgeClips[0].clip.name
                    : string.Empty;
                sawKayKitDodgeClip |= primaryDodgeClip.StartsWith("Dodge_", StringComparison.Ordinal);
                sawDirectionalDodgeAction |=
                    presentation.CurrentAuthoredAction == EarthAuthoredActionId.DirectionalDodge;
                sawDodgeFlightWindow |=
                    presentation.CurrentFootPolicy == EarthAuthoredFootPolicy.FlightIkOff;
                sawDodgeContactWindow |=
                    presentation.CurrentFootPolicy == EarthAuthoredFootPolicy.AuthoredContact;
                float3 dodgeBodyAngles = presentation.ProceduralBodyResponse != null
                    ? presentation.ProceduralBodyResponse.CurrentAnglesDegrees
                    : float3.zero;
                dodgeTrace.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1:F6},{2},{3},{4:F6},{5},{6},{7:F6},{8:F6},{9},{10:F6},{11:F6},{12:F6}\n",
                    dodgeFrame,
                    Time.time,
                    dodgeState.fullPathHash,
                    dodgeNext.fullPathHash,
                    dodgeState.normalizedTime,
                    presentation.CurrentAuthoredAction,
                    presentation.CurrentFootPolicy,
                    footContactController.LeftFootIkWeight,
                    footContactController.RightFootIkWeight,
                    primaryDodgeClip,
                    dodgeBodyAngles.x,
                    dodgeBodyAngles.y,
                    dodgeBodyAngles.z);
            }
            WriteDodgeMotionEvidence(
                dodgeTrace,
                sawDirectionalDodgeAction,
                sawDodgeFlightWindow,
                sawDodgeContactWindow,
                sawKayKitDodgeClip);
            Assert.That(sawDirectionalDodgeAction, Is.True);
            Assert.That(sawDodgeFlightWindow, Is.True);
            Assert.That(sawDodgeContactWindow, Is.True);
            Assert.That(sawKayKitDodgeClip, Is.True,
                "The runtime controller never evaluated one of its licensed KayKit Dodge_* clips.");

            scripted.JumpPressed = true;
            yield return new WaitForFixedUpdate();
            scripted.JumpPressed = false;
            bool sawAirborne = false;
            bool relanded = false;
            bool enteredFullRagdollDuringJump = false;
            bool sawExplicitFlightWindow = false;
            bool sawAuthoredLandingAction = false;
            bool sawAuthoredLandingContact = false;
            float airborneSeconds = 0f;
            float maximumSustainedFlightIkWeight = 0f;
            var jumpTrace = new StringBuilder(16384);
            jumpTrace.AppendLine(
                "frame,time,grounded,stable,verticalSpeed,motionPhase,action,footPolicy," +
                "state,nextState,normalizedTime,nextNormalizedTime,leftIk,rightIk");
            int jumpTraceFrame = 0;
            for (int tick = 0; tick < 180; tick++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                bool airborne = !motor.IsGrounded;
                sawAirborne |= airborne;
                if (airborne)
                {
                    airborneSeconds += Time.deltaTime;
                    sawExplicitFlightWindow |=
                        presentation.CurrentFootPolicy == EarthAuthoredFootPolicy.FlightIkOff;
                    if (airborneSeconds >= 0.12f)
                        maximumSustainedFlightIkWeight = Mathf.Max(
                            maximumSustainedFlightIkWeight,
                            Mathf.Max(
                                footContactController.LeftFootIkWeight,
                                footContactController.RightFootIkWeight));
                }
                EarthAuthoredActionId action = presentation.CurrentAuthoredAction;
                sawAuthoredLandingAction |= action is EarthAuthoredActionId.SoftLanding or
                    EarthAuthoredActionId.MovingLandingRoll or
                    EarthAuthoredActionId.HardLandingBrace;
                sawAuthoredLandingContact |= presentation.CurrentFootPolicy is
                    EarthAuthoredFootPolicy.AuthoredContact or EarthAuthoredFootPolicy.BraceBoth;
                AppendJumpLandingTrace(
                    jumpTrace,
                    jumpTraceFrame++,
                    motor,
                    body,
                    presentation,
                    footContactController,
                    animator);
                enteredFullRagdollDuringJump |= puppet.CurrentState.Mode == CharacterPhysicalMode.FullRagdoll;
                if (sawAirborne && motor.IsGrounded &&
                    Mathf.Abs(Vector3.Dot(body.linearVelocity, motor.LocalUp)) < 0.6f)
                {
                    relanded = true;
                    break;
                }
            }
            Assert.That(sawAirborne, Is.True, "The golden-path jump never left its support.");
            Assert.That(relanded, Is.True,
                $"The golden-path jump did not return to stable support. position={body.position}, " +
                $"velocity={body.linearVelocity}, grounded={motor.IsGrounded}, " +
                $"stable={motor.HasStableSupport}, mode={puppet.CurrentState.Mode}, " +
                $"motion={presentation.MotionPhase}.");
            Assert.That(enteredFullRagdollDuringJump, Is.False,
                "An ordinary self-powered jump must not disable locomotion through FullRagdoll.");
            Assert.That(sawExplicitFlightWindow, Is.True,
                "The live jump/fall graph never entered its explicit IK-off flight window.");
            Assert.That(maximumSustainedFlightIkWeight, Is.LessThanOrEqualTo(0.1501f),
                $"Flight foot IK remained at {maximumSustainedFlightIkWeight:F3} after its fade window.");

            for (int tick = 0; tick < 32; tick++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                EarthAuthoredActionId action = presentation.CurrentAuthoredAction;
                sawAuthoredLandingAction |= action is EarthAuthoredActionId.SoftLanding or
                    EarthAuthoredActionId.MovingLandingRoll or
                    EarthAuthoredActionId.HardLandingBrace;
                sawAuthoredLandingContact |= presentation.CurrentFootPolicy is
                    EarthAuthoredFootPolicy.AuthoredContact or EarthAuthoredFootPolicy.BraceBoth;
                AppendJumpLandingTrace(
                    jumpTrace,
                    jumpTraceFrame++,
                    motor,
                    body,
                    presentation,
                    footContactController,
                    animator);
            }
            WriteJumpLandingEvidence(
                jumpTrace,
                sawAirborne,
                relanded,
                sawExplicitFlightWindow,
                sawAuthoredLandingAction,
                sawAuthoredLandingContact);
            Assert.That(sawAuthoredLandingAction, Is.True,
                "The live jump never entered an authored soft/roll/brace landing action.");
            Assert.That(sawAuthoredLandingContact, Is.True,
                "The live landing never reached its authored contact window.");
            leftLegProbe.CaptureReferencePose();
            rightLegProbe.CaptureReferencePose();
            float postLandingGaitTravel = 0f;
            for (int tick = 0; tick < 80; tick++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                postLandingGaitTravel = Mathf.Max(postLandingGaitTravel,
                    Mathf.Max(
                        leftLegProbe.MeasureMaximumTravelFromReference(),
                        rightLegProbe.MeasureMaximumTravelFromReference()));
            }
            AnimatorStateInfo postLandingState = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo postLandingNext = animator.GetNextAnimatorStateInfo(0);
            AnimatorClipInfo[] postLandingClips = animator.GetCurrentAnimatorClipInfo(0);
            string postLandingClipSummary = postLandingClips.Length == 0
                ? "none"
                : string.Join(", ", System.Array.ConvertAll(postLandingClips,
                    value => $"{value.clip.name}:w={value.weight:F2}:loop={value.clip.isLooping}:len={value.clip.length:F2}"));
            Assert.That(postLandingGaitTravel, Is.GreaterThan(0.015f),
                $"Leg animation must keep cycling after landing. animatorEnabled={animator.enabled}, " +
                $"animatorSpeed={animator.speed:F2}, grounded={motor.IsGrounded}, " +
                $"velocity={body.linearVelocity}, command={motor.LastCommand.Move}, " +
                $"speedParam={animator.GetFloat("Speed"):F2}, gaitRate={animator.GetFloat("GaitRate"):F2}, " +
                $"physicalMode={puppet.CurrentState.Mode}, state={postLandingState.fullPathHash}, " +
                $"time={postLandingState.normalizedTime:F2}, next={postLandingNext.fullPathHash}, " +
                $"clips=[{postLandingClipSummary}].");
            Assert.That(postLandingState.IsName("Locomotion") ||
                        postLandingNext.IsName("Locomotion"), Is.True,
                "A completed landing must return the base layer to Locomotion.");
            leftLegProbe.Dispose();
            rightLegProbe.Dispose();
            Vector3 chestViewport = gameplayCamera.WorldToViewportPoint(body.position + motor.LocalUp * 0.92f);
            Vector3 chestCameraLocal = gameplayCamera.transform.InverseTransformPoint(
                body.position + motor.LocalUp * 0.92f);
            Assert.That(chestViewport.z, Is.GreaterThan(0f));
            Assert.That(chestViewport.y, Is.InRange(0.2f, 0.5f),
                $"The player must sit in the lower third while the ground ahead remains visible " +
                $"(viewport {chestViewport}, camera-local {chestCameraLocal}, " +
                $"camera {gameplayCamera.transform.position}, body {body.position}, " +
                $"pitchDot {-Vector3.Dot(gameplayCamera.transform.forward, motor.LocalUp):F3}, " +
                $"upDot {Vector3.Dot(gameplayCamera.transform.up, motor.LocalUp):F3}, " +
                $"aimPitch {cameraController.AimPitch:F2}).");
            Assert.That(-Vector3.Dot(gameplayCamera.transform.forward, motor.LocalUp), Is.InRange(0.1f, 0.58f),
                $"The gameplay camera must have a bounded downward pitch instead of flying level or diving vertically. " +
                $"cameraForward={gameplayCamera.transform.forward}, localUp={motor.LocalUp}, " +
                $"aimForward={cameraController.AimPivot.forward}, aimPitch={cameraController.AimPitch:F2}, " +
                $"camera={gameplayCamera.transform.position}, body={body.position}.");
            Assert.That(Vector3.Dot(gameplayCamera.transform.up, motor.LocalUp), Is.GreaterThan(0.8f),
                "Cinemachine world-up must follow the spherical surface without rolling the horizon.");
            // At a physical 47 mm lens the authored framing places the camera roughly
            // 17 m from the player. A deliberate tank turn therefore travels over a
            // metre tangentially in a rendered frame even though the rig is smooth.
            // Bound the geometric translation together with angle, and separately
            // reject the radial arm-length pop caused by obstacle collision.
            Assert.That(maximumCameraStep, Is.LessThan(2.5f),
                $"Camera translated {maximumCameraStep:F3} m in one rendered frame at walk tick " +
                $"{maximumCameraStepTick}: {maximumCameraStepFrom} -> {maximumCameraStepTo}.");
            Assert.That(maximumCameraRadialStep, Is.LessThan(0.75f),
                $"Camera arm length popped {maximumCameraRadialStep:F3} m in one rendered frame.");
            Assert.That(maximumCameraAngleStep, Is.LessThan(8f),
                $"Camera rotated {maximumCameraAngleStep:F2} degrees in one rendered frame.");
            PlanetInputReader shippingInput = motor.GetComponent<PlanetInputReader>();
            if (shippingInput != null) motor.ConfigureInputSource(shippingInput);
            UnityEngine.Object.Destroy(scripted);
            if (bot != null) bot.enabled = botWasEnabled;
            if (loadedForTest) yield return SceneManager.UnloadSceneAsync(scene);
        }

        private sealed class ScriptedMotorInput : MonoBehaviour, IPlanetMotorInputSource
        {
            public float2 Move;
            public bool JumpPressed;
            public PlanetMotorCommand SampleCommand(uint tick) => new PlanetMotorCommand(tick, Move, JumpPressed);
        }

        private sealed class VisibleGeometryProbe
        {
            private const int MaximumSamples = 64;
            private readonly Renderer _renderer;
            private readonly SkinnedMeshRenderer _skinned;
            private readonly Transform _reference;
            private readonly Mesh _baked;
            private readonly List<Vector3> _vertices = new List<Vector3>(256);
            private readonly int[] _sampleIndices;
            private readonly Vector3[] _initialPositions;
            private readonly Vector3[] _referencePositions;
            private readonly Vector3 _initialBoundsCenter;

            public VisibleGeometryProbe(Renderer renderer, Transform reference, params Transform[] anchors)
            {
                _renderer = renderer;
                _skinned = renderer as SkinnedMeshRenderer;
                _reference = reference;
                _initialBoundsCenter = reference.InverseTransformPoint(renderer.bounds.center);
                if (_skinned == null)
                {
                    _sampleIndices = System.Array.Empty<int>();
                    _initialPositions = System.Array.Empty<Vector3>();
                    _referencePositions = System.Array.Empty<Vector3>();
                    return;
                }

                _baked = new Mesh { name = $"{renderer.name} Golden Path Probe" };
                _skinned.BakeMesh(_baked);
                _baked.GetVertices(_vertices);
                var rankedVertices = new List<VertexCandidate>(_vertices.Count);
                for (int vertex = 0; vertex < _vertices.Count; vertex++)
                {
                    Vector3 referenceLocal = ToReferenceLocal(_vertices[vertex]);
                    float nearest = float.PositiveInfinity;
                    for (int anchor = 0; anchor < anchors.Length; anchor++)
                    {
                        if (anchors[anchor] == null) continue;
                        Vector3 anchorLocal = reference.InverseTransformPoint(anchors[anchor].position);
                        nearest = Mathf.Min(nearest, (referenceLocal - anchorLocal).sqrMagnitude);
                    }
                    rankedVertices.Add(new VertexCandidate(vertex, nearest));
                }
                rankedVertices.Sort((left, right) => left.Distance.CompareTo(right.Distance));
                int sampleCount = Mathf.Min(MaximumSamples, rankedVertices.Count);
                _sampleIndices = new int[sampleCount];
                _initialPositions = new Vector3[sampleCount];
                _referencePositions = new Vector3[sampleCount];
                for (int sample = 0; sample < sampleCount; sample++)
                {
                    int index = rankedVertices[sample].Index;
                    _sampleIndices[sample] = index;
                    _initialPositions[sample] = ToReferenceLocal(_vertices[index]);
                    _referencePositions[sample] = _initialPositions[sample];
                }
            }

            public float MeasureMaximumTravel()
            {
                if (_skinned == null)
                {
                    Vector3 current = _reference.InverseTransformPoint(_renderer.bounds.center);
                    return Vector3.Distance(_initialBoundsCenter, current);
                }

                RefreshSkinnedGeometry();
                float maximum = 0f;
                for (int sample = 0; sample < _sampleIndices.Length; sample++)
                {
                    int index = _sampleIndices[sample];
                    if (index < 0 || index >= _vertices.Count) continue;
                    maximum = Mathf.Max(maximum,
                        Vector3.Distance(_initialPositions[sample], ToReferenceLocal(_vertices[index])));
                }
                return maximum;
            }

            public void CaptureReferencePose()
            {
                if (_skinned == null) return;
                RefreshSkinnedGeometry();
                for (int sample = 0; sample < _sampleIndices.Length; sample++)
                {
                    int index = _sampleIndices[sample];
                    if (index < 0 || index >= _vertices.Count) continue;
                    _referencePositions[sample] = ToReferenceLocal(_vertices[index]);
                }
            }

            public float MeasureMaximumTravelFromReference()
            {
                if (_skinned == null) return 0f;
                RefreshSkinnedGeometry();
                float maximum = 0f;
                for (int sample = 0; sample < _sampleIndices.Length; sample++)
                {
                    int index = _sampleIndices[sample];
                    if (index < 0 || index >= _vertices.Count) continue;
                    maximum = Mathf.Max(maximum,
                        Vector3.Distance(_referencePositions[sample], ToReferenceLocal(_vertices[index])));
                }
                return maximum;
            }

            public void Dispose()
            {
                if (_baked != null) UnityEngine.Object.Destroy(_baked);
            }

            private Vector3 ToReferenceLocal(Vector3 rendererLocal) =>
                _reference.InverseTransformPoint(_renderer.transform.TransformPoint(rendererLocal));

            private void RefreshSkinnedGeometry()
            {
                _skinned.BakeMesh(_baked);
                _vertices.Clear();
                _baked.GetVertices(_vertices);
            }

            private readonly struct VertexCandidate
            {
                public readonly int Index;
                public readonly float Distance;

                public VertexCandidate(int index, float distance)
                {
                    Index = index;
                    Distance = distance;
                }
            }
        }

        private static void AppendJumpLandingTrace(
            StringBuilder trace,
            int frame,
            PlanetMotor motor,
            Rigidbody body,
            HumanoidCharacterPresentation presentation,
            EarthFootContactController footContact,
            Animator animator)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            trace.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0},{1:F6},{2},{3},{4:F6},{5},{6},{7},{8},{9},{10:F6},{11:F6},{12:F6},{13:F6}\n",
                frame,
                Time.time,
                motor.IsGrounded ? 1 : 0,
                motor.HasStableSupport ? 1 : 0,
                Vector3.Dot(body.linearVelocity, motor.LocalUp),
                presentation.MotionPhase,
                presentation.CurrentAuthoredAction,
                presentation.CurrentFootPolicy,
                state.fullPathHash,
                next.fullPathHash,
                state.normalizedTime,
                next.normalizedTime,
                footContact.LeftFootIkWeight,
                footContact.RightFootIkWeight);
        }

        private static void WriteJumpLandingEvidence(
            StringBuilder trace,
            bool airborne,
            bool relanded,
            bool flight,
            bool landingAction,
            bool landingContact)
        {
            string directory = Path.GetFullPath("BuildReports");
            Directory.CreateDirectory(directory);
            string csv = Path.Combine(directory, "AnimationJumpLandingTelemetryLatest.csv");
            string json = Path.Combine(directory, "AnimationJumpLandingTelemetryLatest.json");
            File.WriteAllText(csv, trace.ToString());
            File.WriteAllText(
                json,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\n" +
                    "  \"schema\": \"animation-action-v1\",\n" +
                    "  \"utc\": \"{0}\",\n" +
                    "  \"actorId\": \"player\",\n" +
                    "  \"scenarioId\": \"production-jump-authored-landing\",\n" +
                    "  \"airborneObserved\": {1},\n" +
                    "  \"relanded\": {2},\n" +
                    "  \"flightIkOffObserved\": {3},\n" +
                    "  \"authoredLandingActionObserved\": {4},\n" +
                    "  \"authoredLandingContactObserved\": {5},\n" +
                    "  \"passed\": {6}\n" +
                    "}}\n",
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    airborne ? "true" : "false",
                    relanded ? "true" : "false",
                    flight ? "true" : "false",
                    landingAction ? "true" : "false",
                    landingContact ? "true" : "false",
                    airborne && relanded && flight && landingAction && landingContact
                        ? "true"
                        : "false"));
        }

        private static void WriteDodgeMotionEvidence(
            StringBuilder trace,
            bool action,
            bool flight,
            bool contact,
            bool authoredClip)
        {
            string directory = Path.GetFullPath("BuildReports");
            Directory.CreateDirectory(directory);
            string csv = Path.Combine(directory, "AnimationDodgeTelemetryLatest.csv");
            string json = Path.Combine(directory, "AnimationDodgeTelemetryLatest.json");
            File.WriteAllText(csv, trace.ToString());
            File.WriteAllText(
                json,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\n" +
                    "  \"schema\": \"animation-action-v1\",\n" +
                    "  \"utc\": \"{0}\",\n" +
                    "  \"actorId\": \"player\",\n" +
                    "  \"scenarioId\": \"licensed-kaykit-directional-dodge-right\",\n" +
                    "  \"authoredActionObserved\": {1},\n" +
                    "  \"flightIkOffObserved\": {2},\n" +
                    "  \"authoredContactObserved\": {3},\n" +
                    "  \"licensedClipObserved\": {4},\n" +
                    "  \"passed\": {5}\n" +
                    "}}\n",
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    action ? "true" : "false",
                    flight ? "true" : "false",
                    contact ? "true" : "false",
                    authoredClip ? "true" : "false",
                    action && flight && contact && authoredClip ? "true" : "false"));
        }

        private static void WriteAnimationMotionAudit(
            StringBuilder trace,
            in EarthAnimationMotionAuditSummary summary)
        {
            string directory = Path.GetFullPath("BuildReports");
            Directory.CreateDirectory(directory);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string historicalCsv = Path.Combine(directory, $"AnimationArenaTelemetry-{stamp}.csv");
            string latestCsv = Path.Combine(directory, "AnimationArenaTelemetryLatest.csv");
            string latestJson = Path.Combine(directory, "AnimationArenaTelemetryLatest.json");
            File.WriteAllText(historicalCsv, trace.ToString());
            File.WriteAllText(latestCsv, trace.ToString());
            string json = string.Format(
                CultureInfo.InvariantCulture,
                "{{\n" +
                "  \"schema\": \"animation-contact-v1\",\n" +
                "  \"utc\": \"{0}\",\n" +
                "  \"historicalCsv\": \"{1}\",\n" +
                "  \"actorId\": \"player\",\n" +
                "  \"scenarioId\": \"production-locomotion-arc\",\n" +
                "  \"targetFrameRate\": 60,\n" +
                "  \"elapsedSeconds\": {2:F6},\n" +
                "  \"sampleCount\": {3},\n" +
                "  \"leftLockTransitions\": {4},\n" +
                "  \"rightLockTransitions\": {5},\n" +
                "  \"totalLockTransitions\": {6},\n" +
                "  \"bothLockedFrames\": {7},\n" +
                "  \"unsupportedFrames\": {8},\n" +
                "  \"supportTransitions\": {9},\n" +
                "  \"discontinuityFrames\": {10},\n" +
                "  \"maximumFootStepMeters\": {11:F6},\n" +
                "  \"maximumFootSpeedMetersPerSecond\": {12:F6},\n" +
                "  \"maximumFootAccelerationMetersPerSecondSquared\": {13:F6},\n" +
                "  \"maximumKneeAngleStepDegrees\": {14:F6},\n" +
                "  \"maximumPelvisStepMeters\": {15:F6},\n" +
                "  \"maximumIkWeightStep\": {16:F6},\n" +
                "  \"hardGatesPassed\": {17}\n" +
                "}}\n",
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                historicalCsv.Replace('\\', '/'),
                summary.ElapsedSeconds,
                summary.SampleCount,
                summary.LeftLockTransitions,
                summary.RightLockTransitions,
                summary.TotalLockTransitions,
                summary.BothLockedFrames,
                summary.UnsupportedFrames,
                summary.SupportTransitions,
                summary.DiscontinuityFrames,
                summary.MaximumFootStep,
                summary.MaximumFootSpeed,
                summary.MaximumFootAcceleration,
                summary.MaximumKneeAngleStep,
                summary.MaximumPelvisStep,
                summary.MaximumIkWeightStep,
                summary.BothLockedFrames == 0 &&
                summary.DiscontinuityFrames == 0 &&
                summary.MaximumKneeAngleStep <= 8.001f &&
                summary.MaximumPelvisStep <= 0.0201f
                    ? "true"
                    : "false");
            File.WriteAllText(latestJson, json);
            Debug.Log(
                $"[Animation Audit] {summary.SampleCount} frames, " +
                $"lock transitions={summary.TotalLockTransitions}, " +
                $"discontinuities={summary.DiscontinuityFrames}, " +
                $"max foot step={summary.MaximumFootStep:F4} m, " +
                $"max knee step={summary.MaximumKneeAngleStep:F2} deg. " +
                $"Trace: {historicalCsv}");
        }

        private static float3 ToFloat3(Vector3 value) =>
            new float3(value.x, value.y, value.z);

        private static SkinnedMeshRenderer FindVisibleHumanoidRenderer(Animator animator)
        {
            SkinnedMeshRenderer[] renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            SkinnedMeshRenderer best = null;
            int bestVertices = -1;
            for (int index = 0; index < renderers.Length; index++)
            {
                SkinnedMeshRenderer candidate = renderers[index];
                if (!candidate.enabled || candidate.sharedMesh == null) continue;
                int vertices = candidate.sharedMesh.vertexCount;
                if (vertices <= bestVertices) continue;
                best = candidate;
                bestVertices = vertices;
            }
            return best;
        }

        private static Bounds GetHumanoidRegionBounds(
            Animator animator,
            float padding,
            params HumanBodyBones[] bones)
        {
            Bounds bounds = default;
            bool initialized = false;
            for (int index = 0; index < bones.Length; index++)
            {
                Transform bone = animator.GetBoneTransform(bones[index]);
                if (bone == null) continue;
                if (!initialized)
                {
                    bounds = new Bounds(bone.position, Vector3.zero);
                    initialized = true;
                }
                else
                    bounds.Encapsulate(bone.position);
            }
            if (initialized) bounds.Expand(padding * 2f);
            return bounds;
        }

        private static Renderer FindRenderer(Renderer[] renderers, params string[] tokens)
        {
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                string normalized = renderer.name.Replace("_", string.Empty).ToLowerInvariant();
                for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
                {
                    string token = tokens[tokenIndex].Replace("_", string.Empty).ToLowerInvariant();
                    if (normalized.Contains(token)) return renderer;
                }
            }
            return null;
        }

        private static GameObject FindByName(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                    if (transforms[index].name == objectName)
                        return transforms[index].gameObject;
            }
            return null;
        }

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
