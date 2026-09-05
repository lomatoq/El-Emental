using System;
using System.Collections;
using System.IO;
using Elemental.Input.Gestures;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Camera;
using Elemental.Presentation.MotionMatching;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Elemental.Tests.PlayMode
{
    /// <summary>One saved-scene smoke pass; observations are not a full animation/performance acceptance.</summary>
    public sealed class EarthMaterialPassRuntimeTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string ReportFolder = "BuildReports/EarthMaterialPass";
        private Scene _scene;
        private Scene _previousActive;
        private bool _loaded;
        private int _frameRate;
        private int _vSync;

        [Serializable]
        private sealed class Report
        {
            public string utc;
            public string stage;
            public bool passed;
            public bool scatterComplete;
            public int requestedDust = 140;
            public int requestedChips = 28;
            public int observedDust;
            public int observedChips;
            public bool dustAged;
            public bool chipsAged;
            public int singleArmorShots;
            public float firstArmorSpeed;
            public float lastArmorSpeed;
            public int releasedBoardStones;
            public float releasedBoardDisplacement;
            public int gravityLaunched;
            public float[] gravitySpeedsAfterTwoTicks;
            public float[] gravityDisplacementsAfterTwoTicks;
            public string gravityStatus;
            public int spatialStressGroups;
            public int spatialStressDust;
            public int spatialStressChips;
            public int waveColumns;
            public bool launchPillarRaised;
            public int emergenceCues;
            public string eammStatus;
            public float eammAppliedWeight;
            public string eammInitialization;
            public string eammRejection;
            public float backwardMoveY;
            public float backwardWorldDisplacement;
            public double hubMaxObservedMilliseconds;
            public double presenterMaxObservedMilliseconds;
            public bool profilerRecordersValid;
            public string limitations = "Focused production smoke, not a full motion corpus. Profiler observations are not zero-GC or p95 certification. Mouse/button routing and every technique still require a hands-on feel review.";
        }

        [UnityTest]
        public IEnumerator ProductionMaterialPassSmoke()
        {
            _frameRate = Application.targetFrameRate;
            _vSync = QualitySettings.vSyncCount;
            _previousActive = SceneManager.GetActiveScene();
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False,
                "Run from the test runner, not an already-playing production scene.");
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), stage = "load" };
            Directory.CreateDirectory(ReportFolder);
            ProfilerRecorder hubRecorder = default;
            ProfilerRecorder presenterRecorder = default;
            try
            {
                yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
                _scene = SceneManager.GetSceneByPath(ScenePath);
                _loaded = true;
                SceneManager.SetActiveScene(_scene);
                Application.targetFrameRate = 60;
                QualitySettings.vSyncCount = 0;
                GameObject player = null;
                foreach (GameObject root in _scene.GetRootGameObjects())
                {
                    if (root.name == "Planet Character") player = root;
                    foreach (var bot in root.GetComponentsInChildren<EarthMvpBotController>(true)) bot.enabled = false;
                    foreach (var input in root.GetComponentsInChildren<MagicInputController>(true)) input.enabled = false;
                    foreach (var motor in root.GetComponentsInChildren<PlanetMotor>(true)) motor.ConfigureInputSource(null);
                    foreach (var impact in root.GetComponentsInChildren<EarthCharacterImpactTarget>(true)) impact.SuppressImpacts(60f);
                }
                Assert.That(player, Is.Not.Null);
                var playerMotor = player.GetComponent<PlanetMotor>();
                var scatter = FindInScene<EarthPlanetRockScatter>();
                var hub = FindInScene<EarthMaterialFeedbackHub>();
                var presenter = FindInScene<EarthMaterialFeedbackPresenter>();
                Assert.That(scatter, Is.Not.Null);
                Assert.That(hub, Is.Not.Null);
                Assert.That(presenter, Is.Not.Null);
                report.stage = "warm-scatter";
                float deadline = Time.realtimeSinceStartup + 20f;
                while (!scatter.IsComplete && Time.realtimeSinceStartup < deadline) yield return null;
                report.scatterComplete = scatter.IsComplete;
                Assert.That(report.scatterComplete, Is.True, "Incremental scatter did not finish within 20 seconds.");
                yield return new WaitForSeconds(0.3f);

                hubRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Earth.MaterialFeedback", 128);
                presenterRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Earth.MaterialParticles", 128);
                report.profilerRecordersValid = hubRecorder.Valid && presenterRecorder.Valid;
                report.stage = "fracture-particles";
                ParticleSystem dust = null, chips = null;
                foreach (var particles in presenter.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (particles.name == "Material Fracture Dust") dust = particles;
                    if (particles.name == "Material Contact Chips") chips = particles;
                }
                AssertOperational(dust);
                AssertOperational(chips);
                dust.Clear();
                chips.Clear();
                Vector3 point = player.transform.position + playerMotor.FacingForward * 1.5f;
                hub.Emit(EarthMaterialFeedbackKind.Fracture, point, playerMotor.LocalUp,
                    1f, 0.6f, dustCount: 140, chipCount: 28);
                yield return null;
                yield return null;
                var dustParticles = new ParticleSystem.Particle[dust.main.maxParticles];
                var chipParticles = new ParticleSystem.Particle[chips.main.maxParticles];
                report.observedDust = dust.GetParticles(dustParticles);
                report.observedChips = chips.GetParticles(chipParticles);
                Assert.That(report.observedDust, Is.GreaterThan(0));
                Assert.That(report.observedChips, Is.GreaterThan(0));
                ParticleSystem.Particle initialDust = dustParticles[0], initialChip = chipParticles[0];
                yield return new WaitForSeconds(0.3f); // Capture the body of the puff, not its zero-alpha birth frame.
                ScreenCapture.CaptureScreenshot(ReportFolder + "/Fracture.png");
                for (int frame = 0; frame < 8; frame++)
                {
                    ObserveProfiler(report, hubRecorder, presenterRecorder);
                    yield return null;
                }
                report.dustAged = ParticleAged(dust, dustParticles, initialDust);
                report.chipsAged = ParticleAged(chips, chipParticles, initialChip);
                Assert.That(report.dustAged && report.chipsAged, Is.True, "Both emitted systems must age between frames.");

                yield return CaptureBoard(player, playerMotor, report);

                report.stage = "armor-single-shots";
                var armor = player.GetComponent<EarthArmorController>();
                Assert.That(armor, Is.Not.Null);
                Assert.That(armor.Begin(), Is.True);
                for (int wheel = 0; wheel < 6; wheel++) armor.ApplyWheel(120f, Time.unscaledTime);
                yield return new WaitForSeconds(1f);
                var pieces = new EarthArmorPiece[128];
                int count = armor.CopyActivePiecesNonAlloc(pieces);
                Assert.That(count, Is.GreaterThan(1));
                var released = new bool[count];
                Vector3 aim = player.transform.position + (playerMotor.FacingForward + playerMotor.LocalUp * 2f).normalized * 100f;
                for (int shot = 0; shot < count; shot++)
                {
                    for (int i = 0; i < count; i++) released[i] = pieces[i].IsReleased;
                    if (!armor.FireNearestAtPoint(aim)) break;
                    bool found = false;
                    for (int i = 0; i < count; i++)
                    {
                        if (released[i] || !pieces[i].IsReleased) continue;
                        float speed = pieces[i].Body.linearVelocity.magnitude;
                        if (report.singleArmorShots == 0) report.firstArmorSpeed = speed;
                        report.lastArmorSpeed = speed;
                        report.singleArmorShots++;
                        Assert.That(pieces[i].Body.isKinematic, Is.False);
                        Assert.That(speed, Is.EqualTo(44f).Within(0.5f));
                        found = true;
                    }
                    Assert.That(found, Is.True, "A successful shot must release an actual piece.");
                }
                Assert.That(report.singleArmorShots, Is.EqualTo(count), "Single-shot sequencing must reach the last prepared piece.");

                report.stage = "backward-observation";
                var controlledInput = player.AddComponent<SmokeMotorInput>();
                playerMotor.ConfigureInputSource(controlledInput);
                controlledInput.Move = new float2(0f, -1f);
                Vector3 start = player.transform.position;
                yield return new WaitForSeconds(0.65f);
                var bridge = player.GetComponentInChildren<EAMMBasePoseBridge>(true);
                var driver = player.GetComponentInChildren<EarthAnimationDriver>(true);
                report.eammStatus = bridge != null ? bridge.RuntimeStatus.ToString() : "missing";
                report.eammAppliedWeight = bridge != null ? bridge.AppliedEammMasterWeight : 0f;
                report.eammInitialization = bridge != null ? bridge.InitializationStatus : "missing";
                report.eammRejection = bridge != null ? bridge.PoseRejectionReason : "missing";
                report.backwardMoveY = driver != null ? driver.GetFloat(Animator.StringToHash("MoveY")) : 0f;
                report.backwardWorldDisplacement = Vector3.Distance(start, player.transform.position);
                Assert.That(report.eammStatus, Is.EqualTo("Active"), report.eammRejection);
                Assert.That(report.backwardMoveY, Is.LessThan(-2f), "Backward run must retain its signed blend coordinate.");
                Assert.That(report.backwardWorldDisplacement, Is.GreaterThan(0.5f));
                for (int frame = 0; frame < 12; frame++)
                {
                    yield return null;
                    Assert.That(bridge.IsReady, Is.True);
                    Assert.That(bridge.RuntimeStatus.ToString(), Is.EqualTo("Active"), bridge.PoseRejectionReason);
                    Assert.That(bridge.AppliedEammMasterWeight, Is.EqualTo(1f).Within(0.001f));
                    Assert.That(bridge.CandidateHeadHeight, Is.GreaterThanOrEqualTo(0.20f));
                    Assert.That(bridge.CandidateLeftFootHeight > -0.08f && bridge.CandidateRightFootHeight > -0.08f, Is.False);
                }
                controlledInput.Move = float2.zero;
                playerMotor.ConfigureInputSource(null);
                ScreenCapture.CaptureScreenshot(ReportFolder + "/BackwardObservation.png");
                yield return null;
                yield return ObserveProductionGravity(player, playerMotor, report);
                report.stage = "wall-and-separated-impact-stress";
                hub.FlushPending();
                var points = new Vector3[8];
                Vector3 right = Vector3.Cross(playerMotor.LocalUp, playerMotor.FacingForward).normalized;
                Action<EarthMaterialFeedbackCue> onStress = cue =>
                {
                    if (cue.SourceId < 9000 || cue.SourceId >= 9008) return;
                    Assert.That(Vector3.Distance(cue.Point, points[cue.SourceId - 9000]), Is.LessThan(0.001f));
                    Assert.That(cue.DustCount, Is.GreaterThan(0));
                    Assert.That(cue.ChipCount, Is.GreaterThan(0));
                    report.spatialStressGroups++;
                    report.spatialStressDust += cue.DustCount;
                    report.spatialStressChips += cue.ChipCount;
                };
                hub.Presented += onStress;
                try
                {
                    for (uint i = 0; i < 8; i++)
                    {
                        points[i] = player.transform.position + right * ((i % 4 - 1.5f) * 2f) + playerMotor.FacingForward * (3f + i / 4 * 2f);
                        hub.Emit(EarthMaterialFeedbackKind.Fracture, points[i], playerMotor.LocalUp, 1f, .5f, 9000 + i, 1, 64, 16);
                    }
                    hub.FlushPending();
                }
                finally { hub.Presented -= onStress; }
                Assert.That(report.spatialStressGroups, Is.EqualTo(8));
                Assert.That(report.spatialStressDust, Is.LessThanOrEqualTo(256));
                Assert.That(report.spatialStressChips, Is.LessThanOrEqualTo(64));
                yield return new WaitForSeconds(.3f);
                ScreenCapture.CaptureScreenshot(ReportFolder + "/WallAndSpatialStress.png");
                yield return null;
                report.stage = "wave-and-launch-pillar";
                float groundedDeadline = Time.unscaledTime + 3f;
                while (!playerMotor.HasStableSupport && Time.unscaledTime < groundedDeadline) yield return null;
                Action<EarthMaterialFeedbackCue> onEmergence = cue =>
                {
                    if (cue.Kind == EarthMaterialFeedbackKind.Emerge && cue.DustCount > 0 && cue.ChipCount > 0)
                        report.emergenceCues++;
                };
                hub.Presented += onEmergence;
                try
                {
                    var wave = player.GetComponent<EarthPillarWaveAbility>();
                    Assert.That(wave, Is.Not.Null);
                    Assert.That(wave.TryCast(playerMotor.FacingForward, .2f, .25f, out var rejection), Is.True, rejection.ToString());
                    report.waveColumns = wave.LastColumnCount;
                    Assert.That(report.waveColumns, Is.GreaterThan(0));
                    yield return new WaitForSeconds(.4f);
                    Assert.That(report.emergenceCues, Is.GreaterThan(0), "Wave columns must present their own surface-emergence dust and chips before the launch pillar fires.");
                    ScreenCapture.CaptureScreenshot(ReportFolder + "/WaveEmergence.png");
                    yield return new WaitForSeconds(.25f);
                    var pillar = player.GetComponent<EarthPillarMobility>();
                    Assert.That(pillar, Is.Not.Null);
                    Assert.That(pillar.BeginCharge(), Is.True);
                    yield return new WaitForSeconds(.15f);
                    report.launchPillarRaised = pillar.ReleaseCharge();
                    Assert.That(report.launchPillarRaised, Is.True);
                    yield return new WaitForSeconds(.2f);
                    ScreenCapture.CaptureScreenshot(ReportFolder + "/LaunchPillar.png");
                    yield return null;
                    Assert.That(report.emergenceCues, Is.GreaterThan(0));
                }
                finally { hub.Presented -= onEmergence; }
                ObserveProfiler(report, hubRecorder, presenterRecorder);
                report.passed = true;
                report.stage = "complete";
            }
            finally
            {
                if (hubRecorder.Valid) hubRecorder.Dispose();
                if (presenterRecorder.Valid) presenterRecorder.Dispose();
                File.WriteAllText(ReportFolder + "/Latest.json", JsonUtility.ToJson(report, true));
                Debug.Log("[Earth Material Pass] " + JsonUtility.ToJson(report));
            }
        }

        private IEnumerator CaptureBoard(GameObject player, PlanetMotor motor, Report report)
        {
            report.stage = "board";
            var surf = player.GetComponent<EarthSurfController>();
            Assert.That(surf, Is.Not.Null);
            var cameraController = FindInScene<EarthCinemachineCameraController>();
            var hud = FindInScene<Elemental.Presentation.UI.EarthCoreHud>();
            UIDocument document = hud != null ? hud.GetComponent<UIDocument>() : null;
            // Keep the production Game Camera and Cinemachine brain. Only freeze the
            // framing data writer briefly and pitch its existing aim pivot for inspection.
            bool cameraEnabled = cameraController != null && cameraController.enabled;
            Quaternion oldPitch = cameraController != null ? cameraController.AimPivot.localRotation : Quaternion.identity;
            DisplayStyle oldDisplay = document != null ? document.rootVisualElement.resolvedStyle.display : DisplayStyle.Flex;
            try
            {
                if (document != null) document.rootVisualElement.style.display = DisplayStyle.None;
                if (cameraController != null)
                {
                    cameraController.enabled = false;
                    cameraController.AimPivot.localRotation = Quaternion.Euler(32f, 0f, 0f);
                }
                Assert.That(surf.Begin(Time.fixedUnscaledTime, motor.FacingForward), Is.True);
                yield return new WaitForSeconds(0.6f);
                Assert.That(surf.IsActive, Is.True);
                Assert.That(surf.BoardTransform.GetComponent<TrailRenderer>().emitting, Is.False);
                ScreenCapture.CaptureScreenshot(ReportFolder + "/BoardAssembled.png");
                yield return null;
                surf.Cancel();
                var before = new Vector3[48];
                var after = new Vector3[48];
                report.releasedBoardStones = surf.CopyReleasedStonePositionsNonAlloc(before);
                Assert.That(report.releasedBoardStones, Is.GreaterThan(0));
                yield return new WaitForSeconds(0.15f);
                Assert.That(surf.CopyReleasedStonePositionsNonAlloc(after), Is.EqualTo(report.releasedBoardStones));
                report.releasedBoardDisplacement = Vector3.Distance(before[0], after[0]);
                Assert.That(report.releasedBoardDisplacement, Is.GreaterThan(0.01f));
                ScreenCapture.CaptureScreenshot(ReportFolder + "/BoardReleased.png");
                yield return null;
            }
            finally
            {
                if (document != null) document.rootVisualElement.style.display = oldDisplay;
                if (cameraController != null)
                {
                    cameraController.AimPivot.localRotation = oldPitch;
                    cameraController.enabled = cameraEnabled;
                    if (cameraEnabled) cameraController.SnapToTarget();
                }
                if (surf.IsActive) surf.Cancel();
            }
            yield return new WaitForSeconds(1.1f);
        }

        private IEnumerator ObserveProductionGravity(GameObject player, PlanetMotor motor, Report report)
        {
            report.stage = "production-gravity";
            var executor = FindInScene<MagicExecutor>();
            EarthArenaStructure structure = null;
            foreach (var root in _scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<EarthArenaStructure>())
                    if (candidate.name.Contains("Wall") && candidate.PieceCount > 2 && candidate.OrdinaryDamageEnabled)
                    { structure = candidate; break; }
            Assert.That(structure, Is.Not.Null, "Production gravity probe needs an actual destructible arena wall.");
            Vector3 focus = player.transform.position + motor.FacingForward * 5f + motor.LocalUp * 4f;
            Collider targetCollider = structure.GetComponent<Collider>();
            Assert.That(targetCollider, Is.Not.Null);
            Assert.That(executor.TryBeginGravityWell(targetCollider, focus, motor.LocalUp, true), Is.True);
            // This invokes the same source's typed TargetsActivated path as the
            // disassembly gesture; no synthetic registration/reflection shortcut.
            structure.SetMagicDisassemblyProgress(0.25f, focus, motor.LocalUp);
            yield return new WaitForFixedUpdate();
            Assert.That(executor.GravityWellCapturedCount, Is.GreaterThan(1));
            Assert.That(executor.BeginGravityClusterThrow(motor.FacingForward), Is.True);
            float until = Time.unscaledTime + 1.05f;
            while (Time.unscaledTime < until)
            {
                executor.UpdateGravityWell(focus, motor.LocalUp, motor.FacingForward);
                executor.UpdateGravityClusterThrow(motor.FacingForward);
                yield return null;
            }
            report.gravityLaunched = executor.ReleaseGravityClusterThrow(motor.FacingForward);
            report.gravityStatus = executor.GravityThrowStatus.ToString();
            Assert.That(report.gravityLaunched, Is.GreaterThan(1));
            var bodies = new Rigidbody[report.gravityLaunched];
            var starts = new Vector3[bodies.Length];
            report.gravitySpeedsAfterTwoTicks = new float[bodies.Length];
            report.gravityDisplacementsAfterTwoTicks = new float[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i] = executor.GetLastGravityLaunchedBody(i);
                starts[i] = bodies[i].position;
                Assert.That(bodies[i].isKinematic, Is.False);
                Assert.That(bodies[i].linearVelocity.magnitude, Is.GreaterThan(.5f),
                    "The actual body must receive launch velocity before contact resolution.");
                Assert.That(bodies[i].detectCollisions, Is.True);
                // Isolate free-flight integration from the crowded arena for two
                // ticks. Collision and secondary breakup have their own regression.
                bodies[i].detectCollisions = false;
            }
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            for (int i = 0; i < bodies.Length; i++)
            {
                report.gravitySpeedsAfterTwoTicks[i] = bodies[i].linearVelocity.magnitude;
                report.gravityDisplacementsAfterTwoTicks[i] = Vector3.Distance(starts[i], bodies[i].position);
                Assert.That(bodies[i].GetComponent<GravityBody>().IsOperational, Is.True);
                bodies[i].detectCollisions = true;
                Assert.That(report.gravitySpeedsAfterTwoTicks[i], Is.GreaterThan(.5f));
                Assert.That(report.gravityDisplacementsAfterTwoTicks[i], Is.GreaterThan(0.02f), "Released body remained at its held position.");
            }
            ScreenCapture.CaptureScreenshot(ReportFolder + "/ProductionGravityRelease.png");
            yield return null;
        }

        private T FindInScene<T>() where T : Component
        {
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        private static void AssertOperational(ParticleSystem particles)
        {
            Assert.That(particles, Is.Not.Null);
            Assert.That(particles.isPlaying, Is.True, particles.name);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            Assert.That(renderer.sharedMaterial, Is.Not.Null, particles.name);
            Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True, particles.name);
        }

        private static bool ParticleAged(ParticleSystem system, ParticleSystem.Particle[] buffer, ParticleSystem.Particle original)
        {
            int count = system.GetParticles(buffer);
            for (int i = 0; i < count; i++)
                if (buffer[i].randomSeed == original.randomSeed)
                    return buffer[i].remainingLifetime < original.remainingLifetime;
            return true; // Expired particles have also completed their lifetime correctly.
        }

        private static void ObserveProfiler(Report report, ProfilerRecorder hub, ProfilerRecorder presenter)
        {
            if (hub.Valid) report.hubMaxObservedMilliseconds = Math.Max(report.hubMaxObservedMilliseconds, hub.LastValue * 0.000001d);
            if (presenter.Valid) report.presenterMaxObservedMilliseconds = Math.Max(report.presenterMaxObservedMilliseconds, presenter.LastValue * 0.000001d);
        }

        private sealed class SmokeMotorInput : MonoBehaviour, IPlanetMotorInputSource
        {
            public float2 Move;
            public PlanetMotorCommand SampleCommand(uint tick) => new PlanetMotorCommand(tick, Move, false);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Application.targetFrameRate = _frameRate;
            QualitySettings.vSyncCount = _vSync;
            if (_previousActive.IsValid() && _previousActive.isLoaded) SceneManager.SetActiveScene(_previousActive);
            if (_loaded && _scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            _loaded = false;
        }
    }
}
