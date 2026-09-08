using System.Collections;
using System.IO;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator SeismicEnemyWaitsForWaveAndRendersThroughOpaqueWall()
        {
            EarthSeismicCameraTargets binding = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                var candidate = root.GetComponentInChildren<EarthSeismicCameraTargets>();
                if (candidate != null) binding = candidate;
            }
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.Targets.Length, Is.GreaterThan(0), "Production opponent was not bound.");
            Actor player = _actors.Find(a => a.Presentation.PoseController != null);
            var motor = player.Presentation.GetComponentInParent<PlanetMotor>();
            var vision = motor.GetComponent<EarthSeismicVision>();
            Renderer enemy = binding.Targets[0].Renderer;
            Assert.That(enemy.transform.IsChildOf(motor.transform), Is.False, "Player leaked into hostile targets.");
            vision.SetActive(true);
            yield return new WaitForSeconds(1.1f);
            yield return _frame;
            Assert.That(binding.Blend, Is.EqualTo(1f));

            var cameraObject = new GameObject("Seismic occlusion QA camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(binding.GetComponent<Camera>());
            camera.enabled = false;
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            var qaBinding = cameraObject.AddComponent<EarthSeismicCameraTargets>();
            qaBinding.Configure(vision, new[] { enemy });
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Opaque sonar QA wall";
            var target = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(384, 384, TextureFormat.RGB24, false);
            var oldTarget = RenderTexture.active;
            try
            {
                Vector3 center = enemy.bounds.center;
                Vector3 up = motor.LocalUp;
                Vector3 forward = Vector3.ProjectOnPlane(center - motor.transform.position, up).normalized;
                if (forward.sqrMagnitude < .5f) forward = motor.FacingForward;
                camera.transform.SetPositionAndRotation(center - forward * 7f, Quaternion.LookRotation(forward, up));
                camera.fieldOfView = 40f;
                wall.transform.SetPositionAndRotation(center - forward * 2f, camera.transform.rotation);
                wall.transform.localScale = new Vector3(5f, 5f, .5f);
                camera.targetTexture = target;
                target.Create();
                string folder = Path.GetFullPath("BuildReports/EnvironmentAnimationRescue/SeismicVision");
                Directory.CreateDirectory(folder);
                var waves = new Vector4[EarthSeismicVision.WaveCount];
                Vector3 origin = center - forward * 6f;
                waves[0] = new Vector4(origin.x, origin.y, origin.z, 0f);
                Shader.SetGlobalVectorArray("_EarthSeismicWaves16", waves);
                var strengths = new float[EarthSeismicVision.WaveCount];
                strengths[0] = 1f;
                Shader.SetGlobalFloatArray("_EarthSeismicStrengths16", strengths);
                Shader.SetGlobalFloatArray("_EarthSeismicRadiusTravels16", new float[EarthSeismicVision.WaveCount]);
                Shader.SetGlobalFloat("_EarthSeismicVision", 1f);
                Color32[] Capture(string name)
                {
                    camera.Render();
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 384, 384), 0, 0);
                    readback.Apply();
                    File.WriteAllBytes(Path.Combine(folder, name + ".png"), readback.EncodeToPNG());
                    return readback.GetPixels32();
                }
                qaBinding.enabled = false;
                Color32[] beforeDisabled = Capture("EnemyBeforeWaveReference");
                qaBinding.enabled = true;
                Color32[] before = Capture("EnemyBeforeWave");
                Assert.That(before, Is.EqualTo(beforeDisabled), "Enemy appeared before the wave reached it.");

                waves[0].w = 8f;
                Shader.SetGlobalVectorArray("_EarthSeismicWaves16", waves);
                qaBinding.enabled = false;
                Color32[] occluded = Capture("EnemyOccludedReference");
                qaBinding.enabled = true;
                Color32[] revealed = Capture("EnemyThroughWall");
                int changed = 0;
                for (int i = 0; i < revealed.Length; i++)
                    if (Mathf.Abs(revealed[i].r - occluded[i].r) > 12) changed++;
                Assert.That(changed, Is.GreaterThan(100), "Wave did not reveal the actual enemy through the opaque wall.");
                Shader.SetGlobalFloat("_EarthSeismicVision", 0f);
                Color32[] off = Capture("EnemyModeOff");
                qaBinding.enabled = false;
                Assert.That(Capture("EnemyModeOffReference"), Is.EqualTo(off), "X-ray remained after the mode faded out.");
                Debug.Log($"[Seismic occlusion QA] {changed} enemy pixels revealed through wall; before-wave and mode-off output byte-exact.");
            }
            finally
            {
                RenderTexture.active = oldTarget;
                camera.targetTexture = null;
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(wall);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
                vision.SetActive(false);
            }
        }
    }
}
