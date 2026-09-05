using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Input.Gestures;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class ArmorJumpAimVisualProofRuntimeTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const int CaptureSize = 1024;

        [Serializable]
        private sealed class Manifest
        {
            public string capturedUtc;
            public string scene;
            public string folder;
            public PoseMetrics baseline;
            public PoseMetrics armored;
            public PoseMetrics armoredWalk;
            public PoseMetrics armoredTurn;
            public PoseMetrics ordinaryJump;
            public int jumpFramesObserved;
            public float maximumJumpMagicLayerWeight;
            public float maximumJumpHandHeightAsymmetry;
            public float minimumJumpHandBelowHead = float.MaxValue;
            public bool sawPillarJump;
            public string[] captures;
            public bool passed;
        }

        [Serializable]
        private struct PoseMetrics
        {
            public string phase;
            public string semanticTechnique;
            public float magicLayerWeight;
            public float armorEncumbrance;
            public float castBrace;
            public float headHeightAboveFeet;
            public float chestTiltDegrees;
            public float headTiltDegrees;
            public float chestYawFromFacingDegrees;
            public float leftHandBelowHead;
            public float rightHandBelowHead;
            public float handHeightAsymmetry;
        }

        [UnityTest]
        public IEnumerator PhysicalArmorAndShortJumpProduceFullBodyVisualEvidence()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            Mouse mouse = null;
            Keyboard keyboard = null;
            GameObject[] suppressedBots = Array.Empty<GameObject>();
            AsyncOperation unload = null;
            var manifest = new Manifest
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                scene = ScenePath
            };
            string root = Path.GetFullPath(Path.Combine("BuildReports", "ArmorJumpAimVisualProof"));
            string folder = Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            manifest.folder = folder.Replace('\\', '/');
            Directory.CreateDirectory(folder);

            try
            {
                EarthSceneReadinessGate gate = All<EarthSceneReadinessGate>(scene).First();
                double readinessDeadline = Time.realtimeSinceStartupAsDouble + 130d;
                while (!gate.IsReady && !gate.Failed &&
                       Time.realtimeSinceStartupAsDouble < readinessDeadline)
                    yield return null;
                Assert.That(gate.IsReady, Is.True, gate.Status);
                suppressedBots = All<EarthMvpBotController>(scene)
                    .Select(bot => bot.gameObject)
                    .Distinct()
                    .ToArray();
                foreach (GameObject bot in suppressedBots)
                    bot.SetActive(false);

                MagicInputController input = All<MagicInputController>(scene)
                    .First(value => value.name == "Planet Character");
                PlanetMotor motor = input.GetComponent<PlanetMotor>();
                HumanoidCharacterPresentation presentation =
                    input.GetComponentInChildren<HumanoidCharacterPresentation>(true);
                EarthCharacterPoseController pose = presentation.PoseController;
                EarthAnimationDriver driver = presentation.GetComponent<EarthAnimationDriver>();
                Animator animator = presentation.Animator;
                PlayerInput player = input.GetComponent<PlayerInput>();
                Camera camera = All<Camera>(scene).First(value => value.CompareTag("MainCamera"));
                Assert.That(animator != null && animator.isHuman, Is.True);
                Assert.That(camera, Is.Not.Null);
                int magicLayer = animator.GetLayerIndex("Earth Magic Upper Body");
                Assert.That(magicLayer, Is.GreaterThanOrEqualTo(0));

                mouse = InputSystem.AddDevice<Mouse>("Armor visual-proof mouse");
                keyboard = InputSystem.AddDevice<Keyboard>("Armor visual-proof keyboard");
                player.neverAutoSwitchControlSchemes = true;
                player.ActivateInput();
                player.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
                player.currentActionMap.Enable();
                Vector2 center = new(Screen.width * .5f, Screen.height * .5f);
                QueueMouse(mouse, center, false);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                for (int frame = 0; frame < 12; frame++) yield return null;
                yield return new WaitForEndOfFrame();

                manifest.baseline = Sample(animator, motor, presentation, pose, driver, magicLayer);
                string baselinePath = Capture(camera, animator, motor, folder,
                    "01-baseline-centered-front", View.Front);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                QueueMouse(mouse, center, true);
                yield return null;
                yield return null;
                Assert.That(input.IsArmorActive, Is.True,
                    "The production Shift+MMB route did not equip armor.");
                double armorDeadline = Time.realtimeSinceStartupAsDouble + 2d;
                while (Time.realtimeSinceStartupAsDouble < armorDeadline)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                    QueueMouse(mouse, center, true);
                    yield return null;
                }
                yield return new WaitForEndOfFrame();

                manifest.armored = Sample(animator, motor, presentation, pose, driver, magicLayer);
                string armorFrontPath = Capture(camera, animator, motor, folder,
                    "02-armor-centered-front", View.Front);
                string armorSidePath = Capture(camera, animator, motor, folder,
                    "03-armor-profile", View.Side);

                Assert.That(manifest.armored.magicLayerWeight, Is.LessThan(.12f));
                Assert.That(manifest.armored.castBrace, Is.LessThan(.01f));
                Assert.That(manifest.armored.leftHandBelowHead, Is.GreaterThan(.18f));
                Assert.That(manifest.armored.rightHandBelowHead, Is.GreaterThan(.18f));
                Assert.That(manifest.armored.headHeightAboveFeet,
                    Is.GreaterThanOrEqualTo(manifest.baseline.headHeightAboveFeet - .12f));
                Assert.That(Mathf.Abs(manifest.armored.chestYawFromFacingDegrees),
                    Is.LessThanOrEqualTo(Mathf.Abs(manifest.baseline.chestYawFromFacingDegrees) + 10f),
                    "Centered pointer introduced a persistent lateral torso drift.");

                // Keep using the shipping input/Animator while junctions flex. These
                // views are the visual acceptance for collar, shoulder and torso
                // seams; the focused coverage test supplies physical gap bounds.
                double walkUntil = Time.realtimeSinceStartupAsDouble + .55d;
                while (Time.realtimeSinceStartupAsDouble < walkUntil)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.W));
                    QueueMouse(mouse, center, true);
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                manifest.armoredWalk = Sample(animator, motor, presentation, pose, driver, magicLayer);
                string armorWalkPath = Capture(camera, animator, motor, folder,
                    "04-armor-walk-front", View.Front);

                double turnUntil = Time.realtimeSinceStartupAsDouble + .48d;
                while (Time.realtimeSinceStartupAsDouble < turnUntil)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.A));
                    QueueMouse(mouse, center, true);
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                manifest.armoredTurn = Sample(animator, motor, presentation, pose, driver, magicLayer);
                string armorTurnPath = Capture(camera, animator, motor, folder,
                    "05-armor-turn-profile", View.Side);
                string armorBackPath = Capture(camera, animator, motor, folder,
                    "06-armor-turn-back", View.Back);

                QueueMouse(mouse, center, false);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return null;
                Assert.That(input.IsArmorActive, Is.False);
                // The shell release is physical and outlives the input flag. Let
                // those pieces clear the body before collecting jump evidence.
                yield return new WaitForSeconds(.80f);
                yield return new WaitForEndOfFrame();

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                double jumpDeadline = Time.realtimeSinceStartupAsDouble + 1.2d;
                while (motor.HasStableSupport && Time.realtimeSinceStartupAsDouble < jumpDeadline)
                    yield return null;
                Assert.That(motor.HasStableSupport, Is.False,
                    "Short Space did not reach the ordinary jump lane.");
                double takeoffAt = Time.realtimeSinceStartupAsDouble;
                string jumpPath = null;
                while (Time.realtimeSinceStartupAsDouble - takeoffAt < .22d)
                {
                    yield return new WaitForEndOfFrame();
                    PoseMetrics frame = Sample(animator, motor, presentation, pose, driver, magicLayer);
                    manifest.jumpFramesObserved++;
                    manifest.maximumJumpMagicLayerWeight = Mathf.Max(
                        manifest.maximumJumpMagicLayerWeight,
                        frame.magicLayerWeight);
                    manifest.maximumJumpHandHeightAsymmetry = Mathf.Max(
                        manifest.maximumJumpHandHeightAsymmetry,
                        frame.handHeightAsymmetry);
                    manifest.minimumJumpHandBelowHead = Mathf.Min(
                        manifest.minimumJumpHandBelowHead,
                        frame.leftHandBelowHead,
                        frame.rightHandBelowHead);
                    manifest.sawPillarJump |= frame.semanticTechnique == "PillarJump";
                    if (jumpPath == null && Time.realtimeSinceStartupAsDouble - takeoffAt >= .15d)
                    {
                        manifest.ordinaryJump = frame;
                        jumpPath = Capture(camera, animator, motor, folder,
                            "07-short-jump-profile", View.Side);
                    }
                }
                Assert.That(jumpPath, Is.Not.Null,
                    "The visual proof did not reach its 150 ms airborne capture point.");
                Assert.That(manifest.ordinaryJump.semanticTechnique,
                    Is.Not.EqualTo("PillarJump"));
                Assert.That(manifest.sawPillarJump, Is.False);
                Assert.That(manifest.maximumJumpMagicLayerWeight, Is.LessThan(.12f));
                Assert.That(manifest.minimumJumpHandBelowHead, Is.GreaterThan(.10f));
                Assert.That(manifest.maximumJumpHandHeightAsymmetry, Is.LessThan(.32f),
                    "Ordinary jump retained a one-hand-up magic gesture in its first 220 ms.");

                manifest.captures = new[]
                {
                    baselinePath, armorFrontPath, armorSidePath,
                    armorWalkPath, armorTurnPath, armorBackPath, jumpPath
                };
                manifest.passed = true;
            }
            finally
            {
                Directory.CreateDirectory(root);
                string json = JsonUtility.ToJson(manifest, true);
                File.WriteAllText(Path.Combine(folder, "ArmorJumpAimVisualManifest.json"), json);
                File.WriteAllText(Path.Combine(root, "Latest.json"), json);
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                foreach (GameObject bot in suppressedBots)
                    if (bot != null) bot.SetActive(true);
                if (scene.IsValid() && scene.isLoaded)
                    unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) while (!unload.isDone) yield return null;
        }

        private enum View { Front, Side, Back }

        private static PoseMetrics Sample(
            Animator animator,
            PlanetMotor motor,
            HumanoidCharacterPresentation presentation,
            EarthCharacterPoseController pose,
            EarthAnimationDriver driver,
            int magicLayer)
        {
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest) ??
                              animator.GetBoneTransform(HumanBodyBones.UpperChest);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 up = motor.LocalUp.normalized;
            Vector3 facing = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            Vector3 chestForward = Vector3.ProjectOnPlane(chest.forward, up).normalized;
            float leftHeight = Vector3.Dot(leftHand.position, up);
            float rightHeight = Vector3.Dot(rightHand.position, up);
            return new PoseMetrics
            {
                phase = presentation.MotionPhase.ToString(),
                semanticTechnique = pose.CurrentRequest.Technique.ToString(),
                magicLayerWeight = driver.GetLayerWeight(magicLayer),
                armorEncumbrance = motor.ArmorEncumbrance01,
                castBrace = motor.Telemetry.Brace01,
                headHeightAboveFeet = Vector3.Dot(
                    head.position - (leftFoot.position + rightFoot.position) * .5f,
                    up),
                chestTiltDegrees = Vector3.Angle(chest.up, up),
                headTiltDegrees = Vector3.Angle(head.up, up),
                chestYawFromFacingDegrees = Vector3.SignedAngle(facing, chestForward, up),
                leftHandBelowHead = Vector3.Dot(head.position - leftHand.position, up),
                rightHandBelowHead = Vector3.Dot(head.position - rightHand.position, up),
                handHeightAsymmetry = Mathf.Abs(leftHeight - rightHeight)
            };
        }

        private static string Capture(
            Camera camera,
            Animator animator,
            PlanetMotor motor,
            string folder,
            string label,
            View view)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 up = motor.LocalUp.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 focus = hips.position + up * .52f;
            Vector3 fromActor = view switch
            {
                View.Front => forward,
                View.Back => -forward,
                _ => right
            };
            Vector3 position = focus + fromActor * 5.2f + up * .10f;

            Vector3 savedPosition = camera.transform.position;
            Quaternion savedRotation = camera.transform.rotation;
            RenderTexture savedTarget = camera.targetTexture;
            float savedFov = camera.fieldOfView;
            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(CaptureSize, CaptureSize, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(CaptureSize, CaptureSize, TextureFormat.RGB24, false);
            string path = Path.Combine(folder, label + ".png");
            try
            {
                camera.transform.SetPositionAndRotation(
                    position,
                    Quaternion.LookRotation(focus - position, up));
                camera.fieldOfView = 43f;
                var renderRequest = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target };
                Assert.That(UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, renderRequest), Is.True);
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, renderRequest);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, CaptureSize, CaptureSize), 0, 0);
                image.Apply(false, false);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = savedTarget;
                camera.fieldOfView = savedFov;
                camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
                UnityEngine.Object.Destroy(image);
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            return path.Replace('\\', '/');
        }

        private static T[] All<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();

        private static void QueueMouse(Mouse mouse, Vector2 position, bool middleHeld)
        {
            var state = new MouseState { position = position };
            state.WithButton(MouseButton.Middle, middleHeld);
            InputSystem.QueueStateEvent(mouse, state);
        }
    }
}
