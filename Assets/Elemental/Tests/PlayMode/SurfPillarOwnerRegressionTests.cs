using System;
using System.Collections;
using Elemental.Input.Actions;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class SurfPillarOwnerRegressionTests
    {
        [UnityTest]
        public IEnumerator OrdinaryPhysicalSpaceHoldRemainsVerticalPillarChargeUntilRelease()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = default;
            Keyboard keyboard = null;
            Mouse mouse = null;
            PlayerInput playerInput = null;
            bool previousNeverAutoSwitch = false;
            EarthPillarMobility mobility = null;
            Action<EarthPillarLaunchEvent> onRaised = null;

            try
            {
                yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByPath(scenePath);
                EarthSceneReadinessGate gate = FindInScene<EarthSceneReadinessGate>(scene);
                for (int frame = 0; frame < 2400 && gate != null && !gate.IsReady && !gate.Failed; frame++)
                    yield return null;
                Assert.That(gate != null && gate.IsReady && !gate.Failed, Is.True);
                foreach (EarthMvpBotController bot in FindAllInScene<EarthMvpBotController>(scene))
                    bot.enabled = false;

                PlanetMotor motor = FindInScene<PlanetMotor>(scene);
                mobility = motor != null ? motor.GetComponent<EarthPillarMobility>() : null;
                EarthActionRouterBehaviour router = motor != null
                    ? motor.GetComponent<EarthActionRouterBehaviour>()
                    : null;
                playerInput = motor != null ? motor.GetComponent<PlayerInput>() : null;
                for (int frame = 0; frame < 120 && motor != null && !motor.IsGrounded; frame++)
                    yield return new WaitForFixedUpdate();
                Assert.That(motor, Is.Not.Null);
                Assert.That(mobility, Is.Not.Null);
                Assert.That(router, Is.Not.Null);
                Assert.That(playerInput, Is.Not.Null);

                mouse = InputSystem.AddDevice<Mouse>("Ordinary pillar regression mouse");
                keyboard = InputSystem.AddDevice<Keyboard>("Ordinary pillar regression keyboard");
                previousNeverAutoSwitch = playerInput.neverAutoSwitchControlSchemes;
                playerInput.neverAutoSwitchControlSchemes = true;
                playerInput.ActivateInput();
                Assert.That(playerInput.user.valid, Is.True);
                playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
                playerInput.currentActionMap?.Enable();
                var presentation = motor.GetComponentInChildren<Elemental.Presentation.Animation.HumanoidCharacterPresentation>(true);
                Animator actorAnimator = presentation != null ? presentation.Animator : null;
                Assert.That(actorAnimator, Is.Not.Null);
                var animationDriver = actorAnimator.GetComponent<Elemental.Presentation.Animation.EarthAnimationDriver>();
                Assert.That(animationDriver, Is.Not.Null);
                Vector3 neutralHips = actorAnimator.GetBoneTransform(HumanBodyBones.Hips).position;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));

                double chargeDeadline = Time.realtimeSinceStartupAsDouble + 1d;
                while (!mobility.IsCharging && Time.realtimeSinceStartupAsDouble < chargeDeadline)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    yield return null;
                }
                Assert.That(mobility.IsCharging, Is.True,
                    "The normal Space path must survive its tap window and enter pillar charge.");
                Assert.That(router.Owner, Is.EqualTo(EarthActionOwner.Pillar));
                float firstCharge = mobility.Charge01;
                double retainUntil = Time.realtimeSinceStartupAsDouble + 0.24d;
                while (Time.realtimeSinceStartupAsDouble < retainUntil)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    yield return null;
                }
                Assert.That(mobility.IsCharging, Is.True,
                    "Surf-only cancellation must not clear an ordinary vertical pillar hold.");
                Assert.That(mobility.Charge01, Is.GreaterThan(firstCharge));
                int chargeState = Animator.StringToHash("Base Layer.Pillar Charge");
                Assert.That(animationDriver.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(chargeState),
                    $"Held Space must enter the live full-body half-crouch state. actor={actorAnimator.name}, phase={presentation.MotionPhase}, support={motor.HasStableSupport}");
                float chargeTime = animationDriver.GetCurrentAnimatorStateInfo(0).normalizedTime;
                retainUntil = Time.realtimeSinceStartupAsDouble + .30d;
                while (Time.realtimeSinceStartupAsDouble < retainUntil)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    yield return null;
                }
                Assert.That(animationDriver.GetCurrentAnimatorStateInfo(0).normalizedTime,
                    Is.GreaterThan(chargeTime + .02f), "Charge idle must continue playing rather than freeze a cast frame.");
                Assert.That(animationDriver.GetBool(Animator.StringToHash("Cast")), Is.False);
                Assert.That(Vector3.Dot(neutralHips - actorAnimator.GetBoneTransform(HumanBodyBones.Hips).position, motor.LocalUp),
                    Is.GreaterThan(.025f), "The actual hips must lower into a half crouch.");
                CaptureCharge(scene, motor, actorAnimator);

                int raisedCount = 0;
                EarthPillarLaunchEvent raised = default;
                onRaised = value =>
                {
                    raisedCount++;
                    raised = value;
                };
                mobility.PillarRaised += onRaised;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(raisedCount, Is.EqualTo(1));
                Assert.That(mobility.IsCharging, Is.False);
                Vector3 surface = ToVector3(raised.SurfaceNormal).normalized;
                Vector3 direction = ToVector3(raised.Direction).normalized;
                Assert.That(Vector3.Dot(surface, direction), Is.GreaterThan(0.999f),
                    "Ordinary pillar release must remain vertical; only the surf trick supplies a tilted direction.");
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) yield return unload;
            }
            finally
            {
                if (mobility != null && onRaised != null) mobility.PillarRaised -= onRaised;
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (playerInput != null)
                    playerInput.neverAutoSwitchControlSchemes = previousNeverAutoSwitch;
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
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

        private static void CaptureCharge(Scene scene, PlanetMotor motor, Animator animator)
        {
            string directory = System.IO.Path.Combine(Application.dataPath, "../BuildReports/MobilityWallFollowup");
            System.IO.Directory.CreateDirectory(directory);
            var obj = new GameObject("Charge QA Camera");
            var camera = obj.AddComponent<Camera>();
            Camera source = FindInScene<Camera>(scene);
            if (source != null) camera.CopyFrom(source);
            camera.enabled = false;
            camera.fieldOfView = 42f;
            Vector3 target = animator.GetBoneTransform(HumanBodyBones.Hips).position + motor.LocalUp * .25f;
            camera.transform.position = target + motor.FacingForward * 3.3f + motor.transform.right * 2.5f + motor.LocalUp;
            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, motor.LocalUp);
            var texture = RenderTexture.GetTemporary(960, 720, 24);
            var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = texture };
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = texture;
                pixels = new Texture2D(960, 720, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
                pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, "ChargeHalfCrouch.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(texture);
                if (pixels != null) UnityEngine.Object.Destroy(pixels);
                UnityEngine.Object.Destroy(obj);
            }
        }

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            var values = new System.Collections.Generic.List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                values.AddRange(root.GetComponentsInChildren<T>(true));
            return values.ToArray();
        }

        private static Vector3 ToVector3(Unity.Mathematics.float3 value) =>
            new Vector3(value.x, value.y, value.z);
    }
}
