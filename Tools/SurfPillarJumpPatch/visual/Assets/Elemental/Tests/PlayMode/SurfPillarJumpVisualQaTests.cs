using System;
using System.Collections;
using System.IO;
using Elemental.Input.Actions;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

namespace Elemental.Tests.PlayMode
{
    public sealed class SurfPillarJumpVisualQaTests
    {
        [Serializable]
        private sealed class CaptureReport
        {
            public bool passed;
            public string scene;
            public string surfFrame;
            public string breakFrame;
            public string airborneFrame;
            public int releasedStones;
            public int pillarEvents;
            public float riderRiseMeters;
            public float riderUpSpeed;
        }

        [UnityTest]
        public IEnumerator ProductionSideViewShowsSurfPillarBreakAndAirborneRider()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            EarthSceneReadinessGate gate = FindInScene<EarthSceneReadinessGate>(scene);
            for (int frame = 0; frame < 2400 && gate != null && !gate.IsReady && !gate.Failed; frame++)
                yield return null;
            Assert.That(gate, Is.Not.Null);
            Assert.That(gate.Failed, Is.False);
            Assert.That(gate.IsReady, Is.True);
            EarthMvpBotController rival = FindInScene<EarthMvpBotController>(scene);
            if (rival != null) rival.enabled = false;
            EarthSurfController surf = FindInScene<EarthSurfController>(scene);
            PlanetMotor motor = surf != null ? surf.GetComponent<PlanetMotor>() : null;
            Rigidbody body = motor != null ? motor.Body : null;
            EarthPillarMobility pillar = motor != null ? motor.GetComponent<EarthPillarMobility>() : null;
            EarthActionRouterBehaviour router = motor != null ? motor.GetComponent<EarthActionRouterBehaviour>() : null;
            PlayerInput playerInput = motor != null ? motor.GetComponent<PlayerInput>() : null;
            Camera camera = FindProductionCamera(scene);
            for (int frame = 0; frame < 120 && motor != null && !motor.IsGrounded; frame++)
                yield return new WaitForFixedUpdate();

            Assert.That(surf, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(pillar, Is.Not.Null);
            Assert.That(router, Is.Not.Null);
            Assert.That(playerInput, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);

            Vector3 up = motor.LocalUp.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            Vector3 side = Vector3.Cross(up, forward).normalized;
            Vector3 start = body.worldCenterOfMass;
            camera.transform.position = start + side * 10f + up * 2.8f - forward * 0.8f;
            camera.transform.rotation = Quaternion.LookRotation(start + up * 1.25f - camera.transform.position, up);
            camera.fieldOfView = 58f;

            string directory = Path.Combine(
                Directory.GetCurrentDirectory(),
                "BuildReports/EnvironmentAnimationRescue/SurfPillarJumpVisualQa",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            string surfFrame = Path.Combine(directory, "01-Surf.png");
            string breakFrame = Path.Combine(directory, "02-PillarBreak.png");
            string airborneFrame = Path.Combine(directory, "03-Airborne.png");

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("Surf Pillar Jump QA Keyboard");
            playerInput.ActivateInput();
            if (playerInput.actions != null) playerInput.actions.devices = null;
            playerInput.currentActionMap?.Enable();
            QueueKeys(keyboard, Key.LeftShift, Key.W);
            for (int frame = 0; frame < 120 && !surf.IsActive; frame++) yield return null;
            Assert.That(surf.IsActive, Is.True, "Physical Shift+W must enter surf through the shipping input map.");
            for (int frame = 0; frame < 14 && motor.MovingSurfaceId != surf.SurfaceId; frame++)
                yield return new WaitForFixedUpdate();
            yield return null;
            Capture(camera, surfFrame);

            int pillarEvents = 0;
            pillar.PillarRaised += _ => pillarEvents++;
            QueueKeys(keyboard, Key.LeftShift, Key.W, Key.Space);
            yield return null;
            QueueKeys(keyboard, Key.LeftShift, Key.W);
            yield return null;
            Assert.That(router.SurfPillarJumpSequence, Is.EqualTo(1u),
                "Physical Space must reach the shipping router exactly once while Shift+W surf is active.");
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
            Capture(camera, breakFrame);

            var released = new Vector3[EarthSurfCellGraph.CellCount];
            int releasedCount = surf.CopyReleasedStonePositionsNonAlloc(released);
            int remainingRiseTicks = Mathf.CeilToInt(pillar.LastLaunch.RiseSeconds / Time.fixedDeltaTime);
            for (int frame = 0; frame < remainingRiseTicks; frame++) yield return new WaitForFixedUpdate();
            yield return null;
            Capture(camera, airborneFrame);

            float rise = Vector3.Dot(body.worldCenterOfMass - start, motor.LocalUp);
            float upSpeed = Vector3.Dot(body.linearVelocity, motor.LocalUp);
            GameObject pillarVisual = FindByName(scene, "Rising Earth Pillar");
            Vector3 viewport = camera.WorldToViewportPoint(body.worldCenterOfMass);
            bool framed = viewport.z > 0f && viewport.x > 0.08f && viewport.x < 0.92f &&
                          viewport.y > 0.08f && viewport.y < 0.92f;
            bool passed = pillarEvents == 1 && releasedCount == EarthSurfCellGraph.CellCount &&
                          rise > 0.35f && upSpeed > 2.5f && framed &&
                          FileSize(surfFrame) > 4096 && FileSize(breakFrame) > 4096 &&
                          FileSize(airborneFrame) > 4096;
            var report = new CaptureReport
            {
                passed = passed,
                scene = scenePath,
                surfFrame = surfFrame,
                breakFrame = breakFrame,
                airborneFrame = airborneFrame,
                releasedStones = releasedCount,
                pillarEvents = pillarEvents,
                riderRiseMeters = rise,
                riderUpSpeed = upSpeed
            };
            File.WriteAllText(Path.Combine(directory, "CaptureReport.json"),
                JsonUtility.ToJson(report, true));

            Assert.That(pillarVisual, Is.Not.Null);
            Assert.That(passed, Is.True,
                $"Visual proof failed: events={pillarEvents}, stones={releasedCount}, rise={rise:F2}, " +
                $"upSpeed={upSpeed:F2}, framed={framed}, report={directory}");
            QueueKeys(keyboard);
            InputSystem.RemoveDevice(keyboard);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static void Capture(Camera camera, string path)
        {
            RenderTexture texture = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = texture };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException(
                        $"Active render pipeline cannot submit a standard request for '{camera.name}'.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = texture;
                pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0f, 0f, 960f, 540f), 0, 0);
                pixels.Apply(false, false);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(texture);
                if (pixels != null) UnityEngine.Object.Destroy(pixels);
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

        private static GameObject FindByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            return null;
        }

        private static Camera FindProductionCamera(Scene scene)
        {
            Camera fallback = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Camera candidate in root.GetComponentsInChildren<Camera>(true))
            {
                fallback ??= candidate;
                if (candidate.CompareTag("MainCamera")) return candidate;
            }
            return fallback;
        }

        private static long FileSize(string path) => File.Exists(path) ? new FileInfo(path).Length : 0L;

        private static void QueueKeys(Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }
    }
}
