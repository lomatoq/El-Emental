using System.Collections;
using Elemental.Presentation.Rendering;
using Elemental.Simulation.Time;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class DayNightSkyRuntimeTests
    {
        private Scene _scene, _previous;
        private bool _opened;
        private CelestialSystemBehaviour _system;
        private float _oldPhase;

        [UnitySetUp]
        public IEnumerator Load()
        {
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            _previous = SceneManager.GetActiveScene();
            _scene = SceneManager.GetSceneByPath(path);
            _opened = !_scene.IsValid() || !_scene.isLoaded;
            if (_opened) yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(_scene);
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<CelestialSystemBehaviour>(true);
                if (found != null) _system = found;
            }
            Assert.That(_system, Is.Not.Null);
            Elemental.Runtime.World.EarthSceneReadinessGate gate = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                var foundGate = root.GetComponentInChildren<Elemental.Runtime.World.EarthSceneReadinessGate>(true);
                if (foundGate != null) gate = foundGate;
            }
            Assert.That(gate, Is.Not.Null, "The production scene must expose its readiness boundary.");
            double deadline = Time.realtimeSinceStartupAsDouble + 130d;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.That(gate.Failed, Is.False, gate.Status);
            Assert.That(gate.IsReady, Is.True, "Waited 130 unscaled seconds for production physics readiness: " + gate.Status);
            // The gate restores timeScale; allow the celestial clock one normal frame.
            yield return null;
            _oldPhase = _system.Snapshot.TimeOfDay01;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (_system != null) _system.SetTimeOfDayForQa(_oldPhase);
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if (_opened && _scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [UnityTest]
        public IEnumerator SavedSkyRunsItsClockAndKeepsSunLightAndSkyAligned()
        {
            Assert.That(_system.HasRequiredBindings, Is.True, "Run DayNightSkyRestore.RestoreCurrentScene first.");
            Assert.That(_system.LightingAuthority, Is.EqualTo(CelestialLightingAuthorityMode.AnimatedEphemeris));
            Assert.That(_system.Profile.DaylightSeconds, Is.EqualTo(300));
            Assert.That(_system.Profile.NightSeconds, Is.EqualTo(300));
            Assert.That(_system.StarSkybox.GetTexture("_StarCube"), Is.TypeOf<Cubemap>());
            Assert.That(_system.StarSkybox.GetTexture("_StarCube").isReadable, Is.False, "Runtime retains no CPU pixel copy.");
            Assert.That(_system.SunLight.shadows, Is.Not.EqualTo(LightShadows.None));
            Transform meshSun = _system.transform.Find("Visible Sun");
            Assert.That(meshSun == null || !meshSun.gameObject.activeSelf, Is.True, "Only one solar disc.");
            float phase = _system.Snapshot.TimeOfDay01;
            yield return null;
            yield return null;
            Assert.That(_system.Snapshot.TimeOfDay01, Is.GreaterThan(phase));
            foreach (float samplePhase in new[] { .025f, .25f, .49f, .75f })
            {
                _system.SetTimeOfDayForQa(samplePhase);
                yield return null;
                yield return null;
                var s = _system.Snapshot.SunDirection;
                Vector3 solar = new Vector3(s.x, s.y, s.z);
                Vector3 shaderDirection = _system.StarSkybox.GetVector("_SunDirection");
                Vector3 atmosphereDirection = Shader.GetGlobalVector("_ElementalSunDirection");
                float lightingSolarAltitude = Vector3.Dot(solar, _system.LightingUp);
                Assert.That(Vector3.Dot(solar, -_system.SunLight.transform.forward), Is.GreaterThan(.99999f));
                Assert.That(Vector3.Distance(solar, shaderDirection), Is.LessThan(.00001f));
                Assert.That(Vector3.Distance(solar, atmosphereDirection), Is.LessThan(.00001f));
                Assert.That(Shader.GetGlobalFloat("_ElementalSolarAltitude"),
                    Is.EqualTo(lightingSolarAltitude).Within(.00001f));
                Assert.That(Shader.GetGlobalFloat("_ElementalTwilight01"),
                    Is.EqualTo(_system.StarSkybox.GetFloat("_Twilight01")).Within(.00001f));
                Assert.That(Vector3.Distance(_system.ObserverUp, (Vector3)_system.StarSkybox.GetVector("_LocalUp")), Is.LessThan(.00001f));
                Assert.That(Vector4.Distance(_system.SolarColor, _system.StarSkybox.GetColor("_SunColor")), Is.LessThan(.00001f));
                Assert.That(Vector4.Distance(_system.SolarColor, Shader.GetGlobalColor("_ElementalMieColor")), Is.LessThan(.00001f));
                if (samplePhase == .25f)
                {
                    Assert.That(_system.SunLight.intensity, Is.GreaterThan(1f));
                    Assert.That(_system.StarSkybox.GetFloat("_StarVisibility"), Is.LessThan(.01f));
                }
                if (samplePhase == .49f) Assert.That(_system.SunLight.color.r, Is.GreaterThan(_system.SunLight.color.g * 1.3f));
                if (samplePhase == .49f)
                    Assert.That(Shader.GetGlobalFloat("_ElementalTwilight01"), Is.GreaterThan(.45f),
                        "Arena dusk must reach both the skybox and fullscreen atmosphere even when the camera radial-up differs.");
                if (samplePhase == .75f)
                {
                    Assert.That(_system.SunLight.intensity, Is.LessThan(.001f));
                    Assert.That(_system.MoonLight, Is.Not.Null);
                    Assert.That(_system.MoonLight.enabled, Is.True);
                    Assert.That(_system.MoonLight.intensity, Is.EqualTo(_system.Profile.MoonlightIntensity).Within(.001f));
                    Assert.That(_system.MoonLight.shadows, Is.EqualTo(LightShadows.None),
                        "Night readability must not replace or modify the authored sun shadows.");
                    Assert.That(Vector3.Dot(-_system.MoonLight.transform.forward, _system.LightingUp),
                        Is.GreaterThanOrEqualTo(.275f), "The readability fill stays above the arena tangent.");
                    Assert.That(RenderSettings.ambientSkyColor.maxColorComponent, Is.InRange(.15f, .22f),
                        "Night ambient must remain subtle but survive ACES strongly enough to show silhouettes.");
                    Assert.That(RenderSettings.ambientIntensity, Is.InRange(.9f, 1.2f));
                    Assert.That(_system.StarSkybox.GetFloat("_StarVisibility"), Is.GreaterThan(.99f));
                }
            }
        }

        [UnityTest]
        public IEnumerator CameraOrbitCannotRetuneGlobalLightAtSunset()
        {
            Assert.That(_system.HasRequiredBindings, Is.True);
            Transform camera = _system.TargetCamera.transform;
            Vector3 savedPosition = camera.position;
            _system.SetTimeOfDayForQa(.49f);
            _system.EvaluatePresentationForQa();
            Quaternion sunRotation = _system.SunLight.transform.rotation;
            Color sunColor = _system.SunLight.color;
            float intensity = _system.SunLight.intensity;
            Vector3 originalHorizon = _system.ObserverUp;
            try
            {
                camera.position += camera.right * 12f;
                _system.EvaluatePresentationForQa();
                Assert.That(Quaternion.Angle(sunRotation, _system.SunLight.transform.rotation), Is.LessThan(.001f));
                Assert.That(_system.SunLight.intensity, Is.EqualTo(intensity).Within(.000001f));
                Assert.That(Vector4.Distance(sunColor, _system.SunLight.color), Is.LessThan(.000001f));
                Assert.That(Vector3.Distance(originalHorizon, _system.ObserverUp), Is.GreaterThan(.05f), "Camera horizon is independently updated.");
            }
            finally
            {
                camera.position = savedPosition;
                _system.EvaluatePresentationForQa();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CelestialUpdateProvidesMeasuredWarmCpuEvidence()
        {
            for (int i = 0; i < 8; i++) yield return null;
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Celestial.Update", 64);
            long maxNanoseconds = 0, sumNanoseconds = 0;
            int samples = 0;
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                if (recorder.LastValue <= 0) continue;
                maxNanoseconds = System.Math.Max(maxNanoseconds, recorder.LastValue);
                sumNanoseconds += recorder.LastValue;
                samples++;
            }
            Assert.That(recorder.Valid, Is.True);
            Assert.That(samples, Is.GreaterThan(0), "Profiler marker must yield actual measurements.");
            TestContext.Progress.WriteLine($"Elemental.Celestial.Update: {samples} warm Editor samples; mean {sumNanoseconds / (double)samples / 1e6:F4} ms, maximum {maxNanoseconds / 1e6:F4} ms. CPU adapter only; no GPU acceptance implied.");
        }
    }
}
