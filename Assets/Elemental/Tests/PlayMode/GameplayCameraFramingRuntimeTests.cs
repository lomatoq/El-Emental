using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Input.Gestures;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Camera;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class GameplayCameraFramingRuntimeTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";

        [Serializable]
        private sealed class Manifest
        {
            public string capturedUtc;
            public string folder;
            public Frame neutral;
            public Frame magic;
            public Frame returned;
            public string[] captures;
            public bool passed;
        }

        [Serializable]
        private struct Frame
        {
            public string label;
            public string cameraState;
            public Vector3 headViewport;
            public Vector3 leftFootViewport;
            public Vector3 rightFootViewport;
            public Vector3 cameraPosition;
            public float cameraDistance;
            public float fieldOfView;
            public float aspect;
            public bool physicalLens;
            public bool fullBodyFramed;
        }

        [UnityTest]
        public IEnumerator ProductionCameraFramesHeadAndFeetThroughMagicAndReturn()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            string root = Path.GetFullPath(Path.Combine("BuildReports", "GameplayCameraFraming"));
            string folder = Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            Directory.CreateDirectory(folder);
            var manifest = new Manifest
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                folder = folder.Replace('\\', '/')
            };
            MagicInputController input = null;
            AsyncOperation unload = null;

            try
            {
                EarthSceneReadinessGate gate = All<EarthSceneReadinessGate>(scene).First();
                double deadline = Time.realtimeSinceStartupAsDouble + 130d;
                while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.That(gate.IsReady, Is.True, gate.Status);
                foreach (EarthMvpBotController bot in All<EarthMvpBotController>(scene)) bot.enabled = false;

                EarthCinemachineCameraController controller = All<EarthCinemachineCameraController>(scene).Single();
                EarthCameraDirector director = All<EarthCameraDirector>(scene).Single();
                Camera camera = All<Camera>(scene).Single(value => value.CompareTag("MainCamera"));
                HumanoidCharacterPresentation presentation = All<HumanoidCharacterPresentation>(scene)
                    .Single(value => value.PoseController != null);
                Animator animator = presentation.Animator;
                input = presentation.GetComponentInParent<MagicInputController>();
                Assert.That(animator != null && animator.isHuman, Is.True);
                Assert.That(input, Is.Not.Null);

                deadline = Time.realtimeSinceStartupAsDouble + 2d;
                while (director.State != EarthCameraState.Explore && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                for (int frame = 0; frame < 24; frame++) yield return null;
                yield return new WaitForEndOfFrame();
                Assert.That(controller.UsesAuthoredPerspectiveLens, Is.True,
                    "Production Cinemachine did not take ownership of the perspective lens.");
                manifest.neutral = Sample("neutral", director, controller, camera, animator);
                AssertFullBody(manifest.neutral);
                string neutralPath = CaptureCurrentProductionCamera(camera, folder, "01-neutral-full-body");

                float2 center = new(Screen.width * .5f, Screen.height * .5f);
                input.TryBeginEarthBendAtScreenPoint(center, BendOriginMode.Aim, .25f);
                deadline = Time.realtimeSinceStartupAsDouble + 1.5d;
                while (director.State == EarthCameraState.Explore && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.That(director.State, Is.Not.EqualTo(EarthCameraState.Explore),
                    "The production bend ingress did not enter a camera action state.");
                for (int frame = 0; frame < 24; frame++) yield return null;
                yield return new WaitForEndOfFrame();
                manifest.magic = Sample("magic", director, controller, camera, animator);
                AssertFullBody(manifest.magic);
                string magicPath = CaptureCurrentProductionCamera(camera, folder, "02-magic-offset-full-body");
                Assert.That(Mathf.Abs(manifest.magic.cameraDistance - manifest.neutral.cameraDistance),
                    Is.GreaterThan(.05f), "Magic camera state did not preserve its authored distance offset.");

                input.enabled = false;
                deadline = Time.realtimeSinceStartupAsDouble + 2d;
                while (director.State != EarthCameraState.Explore && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.That(director.State, Is.EqualTo(EarthCameraState.Explore));
                for (int frame = 0; frame < 36; frame++) yield return null;
                yield return new WaitForEndOfFrame();
                manifest.returned = Sample("returned", director, controller, camera, animator);
                AssertFullBody(manifest.returned);
                string returnedPath = CaptureCurrentProductionCamera(camera, folder, "03-returned-full-body");
                Assert.That(manifest.returned.cameraDistance,
                    Is.EqualTo(manifest.neutral.cameraDistance).Within(.10f),
                    "Camera did not return to neutral authored framing after magic ended.");
                manifest.captures = new[] { neutralPath, magicPath, returnedPath };
                manifest.passed = true;
            }
            finally
            {
                Directory.CreateDirectory(root);
                string json = JsonUtility.ToJson(manifest, true);
                File.WriteAllText(Path.Combine(folder, "GameplayCameraFramingManifest.json"), json);
                File.WriteAllText(Path.Combine(root, "Latest.json"), json);
                if (input != null) input.enabled = false;
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) while (!unload.isDone) yield return null;
        }

        private static Frame Sample(
            string label,
            EarthCameraDirector director,
            EarthCinemachineCameraController controller,
            Camera camera,
            Animator animator)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 headViewport = camera.WorldToViewportPoint(head.position);
            Vector3 leftViewport = camera.WorldToViewportPoint(leftFoot.position);
            Vector3 rightViewport = camera.WorldToViewportPoint(rightFoot.position);
            return new Frame
            {
                label = label,
                cameraState = director.State.ToString(),
                headViewport = headViewport,
                leftFootViewport = leftViewport,
                rightFootViewport = rightViewport,
                cameraPosition = camera.transform.position,
                cameraDistance = controller.CameraDistance,
                fieldOfView = controller.FieldOfView,
                aspect = camera.aspect,
                physicalLens = camera.usePhysicalProperties,
                fullBodyFramed = Inside(headViewport) && Inside(leftViewport) && Inside(rightViewport)
            };
        }

        private static void AssertFullBody(Frame frame)
        {
            Assert.That(frame.physicalLens, Is.False, frame.label + " retained the cropped physical lens.");
            Assert.That(frame.fullBodyFramed, Is.True,
                $"{frame.label} did not frame the whole actor: head={frame.headViewport}, " +
                $"leftFoot={frame.leftFootViewport}, rightFoot={frame.rightFootViewport}.");
            Assert.That(Mathf.Min(frame.leftFootViewport.y, frame.rightFootViewport.y),
                Is.GreaterThanOrEqualTo(.07f), frame.label + " feet need readable lower-frame margin.");
            Assert.That(frame.headViewport.y, Is.LessThanOrEqualTo(.94f),
                frame.label + " head needs readable upper-frame margin.");
        }

        private static bool Inside(Vector3 point) => point.z > 0f &&
            point.x >= .05f && point.x <= .95f && point.y >= .05f && point.y <= .95f;

        private static string CaptureCurrentProductionCamera(Camera camera, string folder, string label)
        {
            int width = 1280;
            int height = Mathf.Clamp(Mathf.RoundToInt(width / Mathf.Max(.5f, camera.aspect)), 640, 1280);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            string path = Path.Combine(folder, label + ".png");
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply(false, false);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(pixels);
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            return path.Replace('\\', '/');
        }

        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
