using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Elemental.Input.Actions;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    /// <summary>
    /// Player-visible animation proof. Solver telemetry alone cannot prove that the
    /// authored pose, camera framing and procedural layers compose into readable
    /// motion. This scenario records a deterministic normal-speed frame sequence
    /// through the actual production camera and writes a state manifest beside it.
    /// </summary>
    public sealed class EarthAnimationVisualAuditRuntimeTests
    {
        private const string ScenePath =
            "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;

        [UnityTest]
        public IEnumerator ProductionAnimationSequenceKeepsArenaAndBothActorsReadable()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool loadedForTest = !scene.IsValid() || !scene.isLoaded;
            if (loadedForTest)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
                Assert.That(load, Is.Not.Null);
                yield return load;
                scene = SceneManager.GetSceneByPath(ScenePath);
            }

            GameObject player = FindByName(scene, "Planet Character");
            GameObject botRoot = FindByName(scene, "Rumble Linebreaker Bot");
            PlanetMotor playerMotor = player != null ? player.GetComponent<PlanetMotor>() : null;
            Rigidbody playerBody = player != null ? player.GetComponent<Rigidbody>() : null;
            HumanoidCharacterPresentation playerPresentation = player != null
                ? player.GetComponentInChildren<HumanoidCharacterPresentation>(true)
                : null;
            HumanoidCharacterPresentation botPresentation = botRoot != null
                ? botRoot.GetComponentInChildren<HumanoidCharacterPresentation>(true)
                : null;
            EarthCharacterImpactTarget botImpact = botRoot != null
                ? botRoot.GetComponent<EarthCharacterImpactTarget>()
                : null;
            EarthCharacterImpactTarget playerImpact = player != null
                ? player.GetComponent<EarthCharacterImpactTarget>()
                : null;
            EarthMvpBotController botController = botRoot != null
                ? botRoot.GetComponent<EarthMvpBotController>()
                : null;
            Camera camera = FindInScene<Camera>(scene);

            Assert.That(player, Is.Not.Null);
            Assert.That(botRoot, Is.Not.Null);
            Assert.That(playerMotor, Is.Not.Null);
            Assert.That(playerBody, Is.Not.Null);
            Assert.That(playerPresentation, Is.Not.Null);
            Assert.That(botPresentation, Is.Not.Null);
            Assert.That(botImpact, Is.Not.Null);
            Assert.That(playerImpact, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            EarthCinematicDepthOfFieldController depthOfField =
                camera.GetComponent<EarthCinematicDepthOfFieldController>();
            bool depthOfFieldWasEnabled = depthOfField != null && depthOfField.enabled;
            // This is the pose-proof lane, not a rendering-quality A/B. The
            // production camera is moved immediately before a manual render, so
            // its late-updated focus envelope would describe the previous view.
            // Disable only for this isolated sharp sequence and restore below.
            if (depthOfField != null) depthOfField.enabled = false;
            Vector3 originalCameraPosition = camera.transform.position;
            Quaternion originalCameraRotation = camera.transform.rotation;

            bool botControllerWasEnabled = botController != null && botController.enabled;
            if (botController != null) botController.enabled = false;
            // Keep locomotion/pivot diagnosis free from incidental arena-body
            // collision reactions. Direct projectile/arena callers can invoke
            // ApplyImpact even when this MonoBehaviour is disabled, so use the
            // production suppression gate for this isolated evidence lane. The
            // explicit bot knockdown below remains fully live.
            playerImpact.SuppressImpacts(30f);
            var scripted = player.AddComponent<VisualAuditMotorInput>();
            playerMotor.ConfigureInputSource(scripted);
            var manifest = new AnimationVisualAuditManifest
            {
                schema = "animation-visual-audit-v1",
                capturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                scene = ScenePath,
                width = CaptureWidth,
                height = CaptureHeight,
                frames = new List<AnimationVisualAuditFrame>(64)
            };
            string directory = Path.GetFullPath(Path.Combine(
                "BuildReports",
                "AnimationVisualAudit"));
            Directory.CreateDirectory(directory);

            for (int tick = 0; tick < 150; tick++)
                yield return new WaitForFixedUpdate();
            Assert.That(playerMotor.HasStableSupport, Is.True,
                "Visual animation audit requires the production player to settle on the arena first.");

            // The shipping spawn faces the opponent and a large destructible
            // block. Turn around before sampling so the run lane stays on the
            // same real arena floor instead of ending in collision/recoil.
            scripted.Move = new float2(1f, 0f);
            yield return new WaitForSeconds(1.08f);
            scripted.Move = float2.zero;
            yield return new WaitForSeconds(0.24f);
            var continuityCollector = player.AddComponent<VisualContinuityCollector>();
            continuityCollector.Configure(playerMotor, playerPresentation);

            scripted.Move = float2.zero;
            CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                "idle-00", directory, manifest);
            yield return WaitNormalizedFrames(10);
            CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                "idle-01", directory, manifest);

            scripted.Move = new float2(0f, 1f);
            yield return WaitNormalizedFrames(1);
            CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                "start-01", directory, manifest);
            yield return WaitNormalizedFrames(4);
            CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                "start-05", directory, manifest);
            yield return WaitNormalizedFrames(8);
            CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                "start-13", directory, manifest);
            for (int stride = 0; stride < 6; stride++)
            {
                yield return WaitNormalizedFrames(4);
                CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                    $"stride-{stride:00}", directory, manifest);
            }

            scripted.Move = float2.zero;
            CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                "stop-00", directory, manifest);
            yield return WaitNormalizedFrames(5);
            CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                "stop-05", directory, manifest);
            yield return WaitNormalizedFrames(9);
            CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                "stop-14", directory, manifest);
            yield return WaitNormalizedFrames(12);
            CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                "stop-26", directory, manifest);

            scripted.Move = new float2(1f, 0f);
            for (int turn = 0; turn < 6; turn++)
            {
                yield return WaitNormalizedFrames(4);
                CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                    $"sharp-turn-{turn:00}", directory, manifest);
            }
            scripted.Move = float2.zero;
            yield return WaitNormalizedFrames(10);

            scripted.JumpPressed = true;
            yield return new WaitForFixedUpdate();
            scripted.JumpPressed = false;
            bool capturedRising = false;
            bool capturedApex = false;
            bool capturedFalling = false;
            bool capturedLanding = false;
            // The editor can render hundreds of frames per second while physics
            // still advances at the configured fixed step. A render-frame budget
            // therefore observed Rising/Falling but sometimes expired before the
            // real body could make contact. Sample the jump on physics ticks so
            // this remains a real motor/arena landing test on every dev machine.
            for (int fixedTick = 0; fixedTick < 240 && !capturedLanding; fixedTick++)
            {
                yield return new WaitForFixedUpdate();
                EarthAnimationPhase phase = playerPresentation.MotionPhase;
                if (!capturedRising && phase == EarthAnimationPhase.Rising)
                {
                    CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                        "jump-rising", directory, manifest);
                    capturedRising = true;
                }
                else if (!capturedApex && phase == EarthAnimationPhase.Apex)
                {
                    CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                        "jump-apex", directory, manifest);
                    capturedApex = true;
                }
                else if (!capturedFalling && phase == EarthAnimationPhase.Falling)
                {
                    CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                        "jump-falling", directory, manifest);
                    capturedFalling = true;
                }
                else if (phase is EarthAnimationPhase.LandingContact or
                         EarthAnimationPhase.LandingRecovery)
                {
                    CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                        "jump-landing", directory, manifest);
                    capturedLanding = true;
                }
            }

            Assert.That(capturedRising, Is.True);
            Assert.That(capturedFalling, Is.True);
            Assert.That(capturedLanding, Is.True);
            yield return WaitNormalizedFrames(28);

            Vector2[] dodgeDirections =
            {
                Vector2.up,
                Vector2.down,
                Vector2.left,
                Vector2.right
            };
            string[] dodgeLabels = { "forward", "backward", "left", "right" };
            for (int dodge = 0; dodge < dodgeDirections.Length; dodge++)
            {
                Assert.That(playerPresentation.TryPlayDirectionalDodge(dodgeDirections[dodge]), Is.True,
                    $"Visual audit could not start the {dodgeLabels[dodge]} dodge: " +
                    playerPresentation.LastDodgeDecision.RejectReason);
                CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                    $"dodge-{dodgeLabels[dodge]}-00", directory, manifest);
                yield return WaitNormalizedFrames(6);
                CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                    $"dodge-{dodgeLabels[dodge]}-06", directory, manifest);
                yield return WaitNormalizedFrames(8);
                CaptureFrame(scene, camera, player, botRoot, playerPresentation,
                    $"dodge-{dodgeLabels[dodge]}-14", directory, manifest);
                // The authored action gate is time based, while editor render FPS
                // is intentionally uncapped. Frame-count waiting made the next
                // direction non-interruptible on fast machines.
                yield return new WaitForSeconds(0.55f);
            }

            EarthCharacterImpactResponse response = botImpact.ApplyImpact(
                botImpact.transform.position + botImpact.transform.up * 0.85f,
                botImpact.transform.right + botImpact.transform.up * 0.12f,
                botImpact.Body.mass * 8.2f,
                EarthCharacterImpactSourceKind.LooseStone,
                0xA11D1701u,
                8.2f,
                1f,
                0xA11D1701u);
            Assert.That(response, Is.EqualTo(EarthCharacterImpactResponse.RecoverableKnockdown));
            yield return new WaitForFixedUpdate();
            // Let presentation publish the canonical full-ragdoll semantic state
            // after the physics handoff before capturing its output-owner metadata.
            yield return null;
            CaptureFrame(scene, camera, player, botRoot, botPresentation,
                "impact-physical-01", directory, manifest);
            yield return WaitNormalizedFrames(12);
            CaptureFrame(scene, camera, player, botRoot, botPresentation,
                "impact-physical-13", directory, manifest);
            yield return new WaitForSeconds(0.62f);
            yield return null;
            CaptureFrame(scene, camera, player, botRoot, botPresentation,
                "getup-contact", directory, manifest);
            yield return WaitNormalizedFrames(16);
            CaptureFrame(scene, camera, player, botRoot, botPresentation,
                "getup-recovery", directory, manifest);
            yield return new WaitForSeconds(0.55f);
            yield return null;
            CaptureFrame(scene, camera, player, botRoot, botPresentation,
                "getup-complete", directory, manifest);

            EarthAnimationVisualContinuitySummary continuity = continuityCollector.Summary;
            manifest.swingResidualViolationFrames = continuity.SwingResidualViolationFrames;
            manifest.pivotWithoutPlantedFootFrames = continuity.PivotWithoutPlantedFootFrames;
            manifest.maximumSwingIkAfterTwoFrames = continuity.MaximumSwingIkAfterTwoFrames;
            manifest.maximumAnkleStepDegrees = continuity.MaximumAnkleStepDegrees;
            manifest.maximumStartStopFootStepMeters = continuity.MaximumStartStopFootStepMeters;
            manifest.maximumPivotPlantedFootStepMeters = continuity.MaximumPivotPlantedFootStepMeters;
            manifest.maximumAnkleDiagnostic = continuityCollector.MaximumAnkleDiagnostic;
            manifest.maximumStartStopDiagnostic = continuityCollector.MaximumStartStopDiagnostic;
            manifest.maximumPivotDiagnostic = continuityCollector.MaximumPivotDiagnostic;
            manifest.continuityHardGatesPassed = continuity.HardGatesPassed;
            WriteManifest(directory, manifest);
            PlanetInputReader shippingInput = player.GetComponent<PlanetInputReader>();
            if (shippingInput != null) playerMotor.ConfigureInputSource(shippingInput);
            UnityEngine.Object.Destroy(scripted);
            UnityEngine.Object.Destroy(continuityCollector);
            if (botController != null) botController.enabled = botControllerWasEnabled;
            camera.transform.SetPositionAndRotation(
                originalCameraPosition,
                originalCameraRotation);
            if (depthOfField != null) depthOfField.enabled = depthOfFieldWasEnabled;
            if (loadedForTest)
                yield return SceneManager.UnloadSceneAsync(scene);

            Assert.That(manifest.frames.Count, Is.GreaterThanOrEqualTo(40));
            for (int index = 0; index < manifest.frames.Count; index++)
            {
                AnimationVisualAuditFrame frame = manifest.frames[index];
                Assert.That(frame.playerVisible, Is.True,
                    $"Player left the audit frame in {frame.label}: {frame.playerViewport}.");
                Assert.That(frame.botVisible, Is.True,
                    $"Opponent left the audit frame in {frame.label}: {frame.botViewport}.");
                Assert.That(frame.visibleArenaRenderers, Is.GreaterThan(0),
                    $"Arena is absent from audit frame {frame.label}.");
                Assert.That(frame.primaryClip, Does.Not.Contain("T-Pose"),
                    $"Audit frame {frame.label} evaluated a T-pose clip.");
                AssertExpectedMotion(frame);
            }
            Assert.That(continuity.SwingResidualViolationFrames, Is.Zero,
                $"Released swing-foot IK exceeded 0.15 after two normalized frames " +
                $"({continuity.SwingResidualViolationFrames} frames, max " +
                $"{continuity.MaximumSwingIkAfterTwoFrames:F3}).");
            Assert.That(continuity.MaximumAnkleStepDegrees,
                Is.LessThanOrEqualTo(EarthAnimationVisualContinuityAudit.MaximumAnkleStepDegrees + 0.001f),
                $"A visible ankle rotated {continuity.MaximumAnkleStepDegrees:F2} degrees in one normalized frame.");
            Assert.That(continuity.MaximumStartStopFootStepMeters,
                Is.LessThanOrEqualTo(EarthAnimationVisualContinuityAudit.MaximumStartStopFootStepMeters + 0.0001f),
                $"Start/stop changed a foot pose by {continuity.MaximumStartStopFootStepMeters:F3} m.");
            Assert.That(continuity.PivotWithoutPlantedFootFrames, Is.Zero,
                $"Turn-in-place had {continuity.PivotWithoutPlantedFootFrames} frames without a planted foot.");
            Assert.That(continuity.MaximumPivotPlantedFootStepMeters,
                Is.LessThanOrEqualTo(EarthAnimationVisualContinuityAudit.MaximumPivotFootStepMeters + 0.0001f),
                $"A planted pivot foot slid {continuity.MaximumPivotPlantedFootStepMeters:F3} m in one normalized frame.");
        }

        private static IEnumerator WaitNormalizedFrames(int count)
        {
            // Evidence labels are expressed as 60-Hz-normalized frames. Waiting
            // for raw Editor renders made clip progression machine-dependent and
            // could sample only the outgoing state on fast GPUs.
            yield return new WaitForSeconds(Mathf.Max(0, count) / 60f);
        }

        private static void AssertExpectedMotion(AnimationVisualAuditFrame frame)
        {
            Assert.That(frame.graphActive, Is.True,
                $"{frame.label} did not sample the production Playables output owner.");
            string expectedClip = frame.label switch
            {
                "idle-00" or "idle-01" => "Idle_A",
                "start-13" => "Walking_A",
                "stride-00" or "stride-01" or "stride-02" or "stride-03" or
                    "stride-04" or "stride-05" => "Walking_A",
                "stop-26" => "Idle_A",
                "sharp-turn-01" or "sharp-turn-02" or "sharp-turn-03" or
                    "sharp-turn-04" or "sharp-turn-05" => "Left Turn",
                "jump-rising" => "Jump_Start",
                "jump-apex" or "jump-falling" => "Falling",
                "jump-landing" => "Jump_Land",
                "dodge-forward-06" or "dodge-forward-14" => "Dodge_Forward",
                "dodge-backward-06" or "dodge-backward-14" => "Dodge_Backward",
                "dodge-left-06" or "dodge-left-14" => "Dodge_Left",
                "dodge-right-06" or "dodge-right-14" => "Dodge_Right",
                "impact-physical-01" or "impact-physical-13" => "none",
                "getup-contact" or "getup-recovery" => "Falling To Roll",
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(expectedClip))
                Assert.That(frame.primaryClip, Is.EqualTo(expectedClip),
                    $"{frame.label} published action {frame.authoredAction} but sampled " +
                    $"'{frame.primaryClip}' from the output owner.");

            if (frame.label is "impact-physical-01" or "impact-physical-13")
            {
                Assert.That(frame.authoredAction, Is.EqualTo(EarthAuthoredActionId.None.ToString()));
                Assert.That(frame.footPolicy, Is.EqualTo(EarthAuthoredFootPolicy.FlightIkOff.ToString()));
            }
        }

        private static void CaptureFrame(
            Scene scene,
            Camera camera,
            GameObject player,
            GameObject bot,
            HumanoidCharacterPresentation sampledPresentation,
            string label,
            string directory,
            AnimationVisualAuditManifest manifest)
        {
            Animator playerAnimator = player.GetComponentInChildren<Animator>(true);
            Animator botAnimator = bot.GetComponentInChildren<Animator>(true);
            Transform playerChest = ResolveChest(playerAnimator, player.transform);
            Transform botChest = ResolveChest(botAnimator, bot.transform);
            Vector3 playerPoint = playerChest.position;
            Vector3 botPoint = botChest.position;
            Vector3 up = (player.transform.up + bot.transform.up).normalized;
            if (up.sqrMagnitude < 0.5f) up = player.transform.up;
            Vector3 separation = Vector3.ProjectOnPlane(botPoint - playerPoint, up);
            if (separation.sqrMagnitude < 0.25f)
                separation = Vector3.ProjectOnPlane(player.transform.right, up);
            Vector3 horizontal = separation.normalized;
            Vector3 viewDirection = Vector3.Cross(up, horizontal).normalized;
            if (viewDirection.sqrMagnitude < 0.5f)
                viewDirection = Vector3.ProjectOnPlane(player.transform.forward, up).normalized;
            Vector3 focus = (playerPoint + botPoint) * 0.5f + up * 0.30f;
            float span = Mathf.Max(5f, separation.magnitude + 3.5f);
            float horizontalFov = Camera.VerticalToHorizontalFieldOfView(
                camera.fieldOfView,
                (float)CaptureWidth / CaptureHeight);
            float distance = span * 0.5f /
                             Mathf.Tan(Mathf.Max(20f, horizontalFov) * 0.5f * Mathf.Deg2Rad);
            distance = Mathf.Clamp(distance * 1.35f, 11f, 24f);
            Vector3 cameraPosition = focus - viewDirection * distance + up * Mathf.Lerp(3.2f, 5.2f,
                Mathf.InverseLerp(11f, 24f, distance));
            camera.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation(focus - cameraPosition, up));

            string path = Path.Combine(directory, label + ".png");
            CaptureCamera(camera, path);
            Vector3 playerViewport = camera.WorldToViewportPoint(playerPoint);
            Vector3 botViewport = camera.WorldToViewportPoint(botPoint);
            Animator sampledAnimator = sampledPresentation != null
                ? sampledPresentation.Animator
                : null;
            var clips = new List<AnimatorClipInfo>(4);
            EarthAnimationGraph sampledGraph = sampledPresentation != null
                ? sampledPresentation.AnimationGraph
                : null;
            if (sampledAnimator != null && sampledAnimator.enabled)
            {
                if (sampledGraph != null && sampledGraph.IsActive)
                    sampledGraph.GetCurrentAnimatorClipInfo(0, clips);
                else
                    sampledAnimator.GetCurrentAnimatorClipInfo(0, clips);
            }
            string primaryClip = ResolveDominantClipName(clips);
            bool inTransition = sampledAnimator != null && sampledAnimator.enabled &&
                                (sampledGraph != null && sampledGraph.IsActive
                                    ? sampledGraph.IsInTransition(0)
                                    : sampledAnimator.IsInTransition(0));
            AnimatorStateInfo sampledState = sampledAnimator != null && sampledAnimator.enabled
                ? sampledGraph != null && sampledGraph.IsActive
                    ? sampledGraph.GetCurrentAnimatorStateInfo(0)
                    : sampledAnimator.GetCurrentAnimatorStateInfo(0)
                : default;
            AnimatorStateInfo nextState = inTransition
                ? sampledGraph != null && sampledGraph.IsActive
                    ? sampledGraph.GetNextAnimatorStateInfo(0)
                    : sampledAnimator.GetNextAnimatorStateInfo(0)
                : default;
            var nextClips = new List<AnimatorClipInfo>(4);
            if (inTransition)
            {
                if (sampledGraph != null && sampledGraph.IsActive)
                    sampledGraph.GetNextAnimatorClipInfo(0, nextClips);
                else
                    sampledAnimator.GetNextAnimatorClipInfo(0, nextClips);
            }
            AnimatorTransitionInfo transitionInfo = inTransition
                ? sampledGraph != null && sampledGraph.IsActive
                    ? sampledGraph.GetAnimatorTransitionInfo(0)
                    : sampledAnimator.GetAnimatorTransitionInfo(0)
                : default;
            EarthFootContactController feet = sampledPresentation != null
                ? sampledPresentation.FootContactController
                : null;
            Rigidbody sampledBody = sampledPresentation != null
                ? sampledPresentation.GetComponentInParent<Rigidbody>()
                : null;
            manifest.frames.Add(new AnimationVisualAuditFrame
            {
                label = label,
                file = path.Replace('\\', '/'),
                time = Time.time,
                playerViewport = playerViewport,
                botViewport = botViewport,
                playerVisible = IsReadableViewport(playerViewport),
                botVisible = IsReadableViewport(botViewport),
                visibleArenaRenderers = CountVisibleArenaRenderers(scene, camera),
                sampledActor = sampledPresentation != null
                    ? sampledPresentation.transform.root.name
                    : string.Empty,
                authoredAction = sampledPresentation != null
                    ? sampledPresentation.CurrentAuthoredAction.ToString()
                    : string.Empty,
                footPolicy = sampledPresentation != null
                    ? sampledPresentation.CurrentFootPolicy.ToString()
                    : string.Empty,
                primaryClip = primaryClip,
                baseStateHash = sampledState.fullPathHash,
                normalizedTime = sampledState.normalizedTime,
                graphActive = sampledGraph != null && sampledGraph.IsActive,
                inTransition = inTransition,
                nextStateHash = nextState.fullPathHash,
                nextNormalizedTime = nextState.normalizedTime,
                nextPrimaryClip = ResolveDominantClipName(nextClips),
                transitionNormalizedTime = transitionInfo.normalizedTime,
                filteredTurn = sampledPresentation != null
                    ? sampledPresentation.FilteredTurn
                    : 0f,
                speed = sampledAnimator != null && sampledAnimator.enabled
                    ? sampledAnimator.GetFloat("Speed")
                    : 0f,
                grounded = sampledPresentation != null &&
                           sampledPresentation.GetComponentInParent<PlanetMotor>() is { } motor &&
                           motor.HasStableSupport,
                rootSpeed = sampledBody != null ? sampledBody.linearVelocity.magnitude : 0f,
                leftIkWeight = feet != null ? feet.LeftFootIkWeight : 0f,
                rightIkWeight = feet != null ? feet.RightFootIkWeight : 0f,
                leftLocked = feet != null && feet.LeftFootLocked,
                rightLocked = feet != null && feet.RightFootLocked,
                leftSoleClearance = feet != null ? feet.LeftSoleClearance : 0f,
                rightSoleClearance = feet != null ? feet.RightSoleClearance : 0f
            });
        }

        private static string ResolveDominantClipName(IReadOnlyList<AnimatorClipInfo> clips)
        {
            AnimationClip dominant = null;
            float greatestWeight = float.NegativeInfinity;
            for (int index = 0; index < clips.Count; index++)
            {
                AnimationClip candidate = clips[index].clip;
                if (candidate == null || clips[index].weight <= greatestWeight) continue;
                dominant = candidate;
                greatestWeight = clips[index].weight;
            }
            return dominant != null ? dominant.name : "none";
        }

        private static void CaptureCamera(Camera camera, string path)
        {
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(
                CaptureWidth,
                CaptureHeight,
                24,
                RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(
                CaptureWidth,
                CaptureHeight,
                TextureFormat.RGB24,
                false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0, false);
                pixels.Apply(false, false);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static int CountVisibleArenaRenderers(Scene scene, Camera camera)
        {
            int visible = 0;
            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer.gameObject.scene != scene || !renderer.enabled) continue;
                if (!BelongsToArena(renderer)) continue;
                Vector3 viewport = camera.WorldToViewportPoint(renderer.bounds.center);
                if (IsReadableViewport(viewport)) visible++;
            }
            return visible;
        }

        private static bool BelongsToArena(Renderer renderer)
        {
            Transform current = renderer.transform;
            while (current != null)
            {
                string objectName = current.name;
                if (objectName.IndexOf("Arena", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    objectName.IndexOf("Broken Crown", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    objectName.IndexOf("Amphitheatre", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                current = current.parent;
            }
            Material material = renderer.sharedMaterial;
            return material != null &&
                   material.name.IndexOf("ArenaSandstone", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsReadableViewport(Vector3 point) =>
            point.z > 0f && point.x >= 0.04f && point.x <= 0.96f &&
            point.y >= 0.04f && point.y <= 0.96f;

        private static Transform ResolveChest(Animator animator, Transform fallback)
        {
            if (animator == null || !animator.isHuman) return fallback;
            return animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                   animator.GetBoneTransform(HumanBodyBones.Chest) ?? fallback;
        }

        private static void WriteManifest(
            string directory,
            AnimationVisualAuditManifest manifest)
        {
            manifest.capturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            string path = Path.Combine(directory, "AnimationVisualAuditLatest.json");
            File.WriteAllText(path, JsonUtility.ToJson(manifest, true));
        }

        private static GameObject FindByName(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Transform[] transforms = roots[index].GetComponentsInChildren<Transform>(true);
                for (int child = 0; child < transforms.Length; child++)
                    if (transforms[child].name == objectName)
                        return transforms[child].gameObject;
            }
            return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T candidate = roots[index].GetComponentInChildren<T>(true);
                if (candidate != null) return candidate;
            }
            return null;
        }

        private sealed class VisualAuditMotorInput : MonoBehaviour, IPlanetMotorInputSource
        {
            public float2 Move;
            public bool JumpPressed;

            public PlanetMotorCommand SampleCommand(uint tick) =>
                new PlanetMotorCommand(tick, Move, JumpPressed);
        }

        [DefaultExecutionOrder(2000)]
        private sealed class VisualContinuityCollector : MonoBehaviour
        {
            private PlanetMotor _motor;
            private HumanoidCharacterPresentation _presentation;
            private Animator _animator;
            private EarthAnimationGraph _animationGraph;
            private readonly List<AnimatorClipInfo> _clipInfoScratch = new(4);
            private Rigidbody _rootBody;
            private Transform _leftFoot;
            private Transform _rightFoot;
            private EarthAnimationVisualContinuityState _state;
            private float _maximumAnkleStep;
            private float _maximumStartStopStep;
            private float _maximumPivotStep;
            private Quaternion _previousLeftAnkle;
            private Quaternion _previousRightAnkle;
            private bool _hasPreviousAnkles;

            public string MaximumAnkleDiagnostic { get; private set; } = string.Empty;
            public string MaximumStartStopDiagnostic { get; private set; } = string.Empty;
            public string MaximumPivotDiagnostic { get; private set; } = string.Empty;

            public EarthAnimationVisualContinuitySummary Summary =>
                EarthAnimationVisualContinuityAudit.Snapshot(in _state);

            public void Configure(
                PlanetMotor configuredMotor,
                HumanoidCharacterPresentation configuredPresentation)
            {
                _motor = configuredMotor;
                _presentation = configuredPresentation;
                _animator = configuredPresentation != null
                    ? configuredPresentation.Animator
                    : null;
                _animationGraph = configuredPresentation != null
                    ? configuredPresentation.AnimationGraph
                    : null;
                _rootBody = configuredMotor != null
                    ? configuredMotor.GetComponent<Rigidbody>()
                    : null;
                if (_animator == null || !_animator.isHuman) return;
                _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }

            private void LateUpdate()
            {
                if (_motor == null || _presentation == null || _animator == null ||
                    _leftFoot == null || _rightFoot == null) return;
                EarthFootContactController feet = _presentation.FootContactController;
                if (feet == null) return;
                Transform reference = _animator.transform;
                bool grounded = _motor.HasStableSupport;
                float speed = _animator.GetFloat("Speed");
                bool contactLocomotion =
                    _presentation.CurrentAuthoredAction == EarthAuthoredActionId.Locomotion &&
                    _presentation.CurrentFootPolicy == EarthAuthoredFootPolicy.DefaultContact;
                Vector3 up = _motor.LocalUp.sqrMagnitude > 0.5f
                    ? _motor.LocalUp.normalized
                    : _motor.transform.up;
                float tangentSpeed = _rootBody != null
                    ? Vector3.ProjectOnPlane(_rootBody.linearVelocity, up).magnitude
                    : 0f;
                bool locomoting = contactLocomotion && grounded && Mathf.Abs(speed) > 0.12f;
                bool turning = contactLocomotion && grounded && Mathf.Abs(speed) < 0.35f &&
                               Mathf.Abs(_presentation.FilteredTurn) > 0.20f &&
                               tangentSpeed < 0.45f;
                Quaternion inverseReference = Quaternion.Inverse(reference.rotation);
                Quaternion leftLocal = inverseReference * _leftFoot.rotation;
                Quaternion rightLocal = inverseReference * _rightFoot.rotation;
                float normalizedScale = Mathf.Clamp(
                    (1f / 60f) / Mathf.Max(0.0001f, Time.deltaTime),
                    0.25f,
                    4f);
                float leftAnkleStep = _hasPreviousAnkles
                    ? Quaternion.Angle(_previousLeftAnkle, leftLocal) * normalizedScale
                    : 0f;
                float rightAnkleStep = _hasPreviousAnkles
                    ? Quaternion.Angle(_previousRightAnkle, rightLocal) * normalizedScale
                    : 0f;
                _previousLeftAnkle = leftLocal;
                _previousRightAnkle = rightLocal;
                _hasPreviousAnkles = true;
                var sample = new EarthAnimationVisualContinuitySample(
                    Mathf.Max(0.0001f, Time.deltaTime),
                    grounded && contactLocomotion,
                    locomoting,
                    turning,
                    ToFloat3(reference.InverseTransformPoint(_leftFoot.position)),
                    ToFloat3(reference.InverseTransformPoint(_rightFoot.position)),
                    ToQuaternion(leftLocal),
                    ToQuaternion(rightLocal),
                    ToFloat3(_leftFoot.position),
                    ToFloat3(_rightFoot.position),
                    feet.LeftFootIkWeight,
                    feet.RightFootIkWeight,
                    feet.LeftFootLocked,
                    feet.RightFootLocked);
                EarthAnimationVisualContinuitySummary summary =
                    EarthAnimationVisualContinuityAudit.Step(ref _state, in sample);
                if (summary.MaximumAnkleStepDegrees > _maximumAnkleStep + 0.0001f)
                {
                    _maximumAnkleStep = summary.MaximumAnkleStepDegrees;
                    AnimatorStateInfo animatorState = GetCurrentStateInfo();
                    GetCurrentClipInfo();
                    string clip = ResolveDominantClipName(_clipInfoScratch);
                    MaximumAnkleDiagnostic =
                        $"time={Time.time:F3}; clip={clip}; state={animatorState.fullPathHash}; " +
                        $"foot={(leftAnkleStep >= rightAnkleStep ? "left" : "right")}; " +
                        $"leftStep={leftAnkleStep:F2}; rightStep={rightAnkleStep:F2}; " +
                        $"action={_presentation.CurrentAuthoredAction}; " +
                        $"policy={_presentation.CurrentFootPolicy}; " +
                        $"leftWeight={feet.LeftFootIkWeight:F3}; rightWeight={feet.RightFootIkWeight:F3}; " +
                        $"leftLocked={feet.LeftFootLocked}; rightLocked={feet.RightFootLocked}; " +
                        $"leftReason={feet.LeftReason}; rightReason={feet.RightReason}";
                }
                if (summary.MaximumStartStopFootStepMeters > _maximumStartStopStep + 0.0001f)
                {
                    _maximumStartStopStep = summary.MaximumStartStopFootStepMeters;
                    MaximumStartStopDiagnostic = DescribeCurrentState(feet);
                }
                if (summary.MaximumPivotPlantedFootStepMeters > _maximumPivotStep + 0.0001f)
                {
                    _maximumPivotStep = summary.MaximumPivotPlantedFootStepMeters;
                    MaximumPivotDiagnostic = DescribeCurrentState(feet);
                }
            }

            private string DescribeCurrentState(EarthFootContactController feet)
            {
                AnimatorStateInfo animatorState = GetCurrentStateInfo();
                GetCurrentClipInfo();
                string clip = ResolveDominantClipName(_clipInfoScratch);
                return $"time={Time.time:F3}; clip={clip}; state={animatorState.fullPathHash}; " +
                       $"action={_presentation.CurrentAuthoredAction}; " +
                       $"policy={_presentation.CurrentFootPolicy}; " +
                       $"leftWeight={feet.LeftFootIkWeight:F3}; rightWeight={feet.RightFootIkWeight:F3}; " +
                       $"leftLocked={feet.LeftFootLocked}; rightLocked={feet.RightFootLocked}; " +
                       $"leftReason={feet.LeftReason}; rightReason={feet.RightReason}";
            }

            private AnimatorStateInfo GetCurrentStateInfo() =>
                _animationGraph != null && _animationGraph.IsActive
                    ? _animationGraph.GetCurrentAnimatorStateInfo(0)
                    : _animator.GetCurrentAnimatorStateInfo(0);

            private void GetCurrentClipInfo()
            {
                _clipInfoScratch.Clear();
                if (_animationGraph != null && _animationGraph.IsActive)
                    _animationGraph.GetCurrentAnimatorClipInfo(0, _clipInfoScratch);
                else
                    _animator.GetCurrentAnimatorClipInfo(0, _clipInfoScratch);
            }

            private static float3 ToFloat3(Vector3 value) =>
                new float3(value.x, value.y, value.z);

            private static quaternion ToQuaternion(Quaternion value) =>
                new quaternion(value.x, value.y, value.z, value.w);
        }

        [Serializable]
        private sealed class AnimationVisualAuditManifest
        {
            public string schema;
            public string capturedUtc;
            public string scene;
            public int width;
            public int height;
            public int swingResidualViolationFrames;
            public int pivotWithoutPlantedFootFrames;
            public float maximumSwingIkAfterTwoFrames;
            public float maximumAnkleStepDegrees;
            public float maximumStartStopFootStepMeters;
            public float maximumPivotPlantedFootStepMeters;
            public string maximumAnkleDiagnostic;
            public string maximumStartStopDiagnostic;
            public string maximumPivotDiagnostic;
            public bool continuityHardGatesPassed;
            public List<AnimationVisualAuditFrame> frames;
        }

        [Serializable]
        private sealed class AnimationVisualAuditFrame
        {
            public string label;
            public string file;
            public float time;
            public Vector3 playerViewport;
            public Vector3 botViewport;
            public bool playerVisible;
            public bool botVisible;
            public int visibleArenaRenderers;
            public string sampledActor;
            public string authoredAction;
            public string footPolicy;
            public string primaryClip;
            public int baseStateHash;
            public float normalizedTime;
            public bool graphActive;
            public bool inTransition;
            public int nextStateHash;
            public float nextNormalizedTime;
            public string nextPrimaryClip;
            public float transitionNormalizedTime;
            public float filteredTurn;
            public float speed;
            public bool grounded;
            public float rootSpeed;
            public float leftIkWeight;
            public float rightIkWeight;
            public bool leftLocked;
            public bool rightLocked;
            public float leftSoleClearance;
            public float rightSoleClearance;
        }
    }
}
