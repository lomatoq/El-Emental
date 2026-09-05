using System.Collections;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Presentation.VFX;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthIdleFootOrientationTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private Scene _scene;
        private bool _loaded;
        private int _frameRate;
        private int _vSync;

        [UnityTest]
        public IEnumerator ProductionMobilityViewsAndWaveStayBoundToTheirSurface()
        {
            _frameRate = Application.targetFrameRate;
            _vSync = QualitySettings.vSyncCount;
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False);
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(ScenePath);
            _loaded = true;
            GameObject player = null;
            EarthPillarFeedback feedback = null;
            EarthSurfaceQueryService service = null;
            Collider floor = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (root.name == "Planet Character") player = root;
                foreach (var f in root.GetComponentsInChildren<EarthPillarFeedback>(true)) feedback = f;
                foreach (var s in root.GetComponentsInChildren<EarthSurfaceQueryService>(true)) service = s;
                foreach (var c in root.GetComponentsInChildren<Collider>(true))
                    if (c.name == "Arena_FloorBase_INTACT") floor = c;
                foreach (var bot in root.GetComponentsInChildren<EarthMvpBotController>()) bot.enabled = false;
                foreach (var m in root.GetComponentsInChildren<PlanetMotor>()) m.ConfigureInputSource(null);
                foreach (var impact in root.GetComponentsInChildren<EarthCharacterImpactTarget>()) impact.SuppressImpacts(60f);
            }
            yield return new WaitForSeconds(2f);
            Assert.That(player, Is.Not.Null);
            Assert.That(service, Is.Not.Null);
            Assert.That(floor, Is.Not.Null);
            // At the gate the old +4m query selected the arch roof, ~3.8m above
            // the arena floor. Use the real imported floor/provider pair.
            var ray = new Ray(new Vector3(-0.26f, floor.bounds.max.y + 1f, 5f), Vector3.down);
            Assert.That(floor.Raycast(ray, out RaycastHit floorHit, 10f), Is.True);
            EarthSurfaceQuery query = EarthWaveSurfaceFollow.CreateQuery(
                new float3(floorHit.point.x, floorHit.point.y, floorHit.point.z), new float3(0f, 1f, 0f));
            Assert.That(service.TrySample(in query, out EarthSurfaceSample surface), Is.True);
            Assert.That(surface.Point.y, Is.EqualTo(floorHit.point.y).Within(0.05f));
            Debug.Log($"[Mobility visuals] gate floor={floorHit.point.y:F3} wave support={surface.Point.y:F3}");

            var motor = player.GetComponent<PlanetMotor>();
            var mobility = player.GetComponent<EarthPillarMobility>();
            Assert.That(mobility.BeginCharge(), Is.True);
            yield return new WaitForSeconds(0.1f);
            Assert.That(mobility.ReleaseCharge(), Is.True);
            yield return new WaitForSeconds(0.12f);
            Transform pillar = feedback.transform.Find("Rising Earth Pillar");
            Assert.That(pillar, Is.Not.Null);
            Assert.That(pillar.gameObject.activeInHierarchy, Is.True, "Launch must display its existing pillar.");
            Assert.That(pillar.GetComponent<Renderer>().bounds.size.y, Is.GreaterThan(0.25f));
            int activeChips = 0;
            foreach (Transform child in feedback.transform)
                if (child.name.StartsWith("Lift Ground Chip ") && child.gameObject.activeInHierarchy) activeChips++;
            Assert.That(activeChips, Is.GreaterThan(0), "Saved/reloaded feedback must restore chip buffers.");
            System.IO.Directory.CreateDirectory("BuildReports/MobilityVisuals");
            ScreenCapture.CaptureScreenshot("BuildReports/MobilityVisuals/LaunchPillar.png");
            yield return new WaitForSeconds(3f);
            float deadline = Time.time + 4f;
            while (!motor.HasStableSupport && Time.time < deadline) yield return null;
            Assert.That(motor.HasStableSupport, Is.True);
            var surf = player.GetComponent<EarthSurfController>();
            Assert.That(surf.Begin(Time.time, motor.FacingForward), Is.True);
            yield return new WaitForSeconds(0.15f);
            int renderedPieces = 0;
            foreach (var renderer in surf.BoardTransform.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, renderer.name);
                    Assert.That(material.shader, Is.Not.Null);
                    Assert.That(material.shader.isSupported, Is.True);
                    Assert.That(material.shader.name, Does.Not.Contain("InternalErrorShader"));
                }
                renderedPieces++;
            }
            Assert.That(renderedPieces, Is.GreaterThan(0));
            ScreenCapture.CaptureScreenshot("BuildReports/MobilityVisuals/SurfMaterial.png");
            Debug.Log($"[Mobility visuals] pillar chips={activeChips} surf renderers={renderedPieces}");
            yield return null;
            surf.Cancel();
        }

        [UnityTest]
        public IEnumerator LandingRollMovesBothFightersForwardAndSettles()
        {
            _frameRate = Application.targetFrameRate;
            _vSync = QualitySettings.vSyncCount;
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False);
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(ScenePath);
            _loaded = true;
            var motors = new System.Collections.Generic.List<PlanetMotor>();
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                motors.AddRange(root.GetComponentsInChildren<PlanetMotor>());
                foreach (var bot in root.GetComponentsInChildren<EarthMvpBotController>()) bot.enabled = false;
                foreach (var impact in root.GetComponentsInChildren<EarthCharacterImpactTarget>()) impact.SuppressImpacts(60f);
            }
            Assert.That(motors.Count, Is.EqualTo(2));
            // Measure unobstructed travel for each fighter; the other idle fighter
            // otherwise blocks the longer roll. Arena collision remains enabled.
            foreach (Collider a in motors[0].GetComponentsInChildren<Collider>())
                foreach (Collider b in motors[1].GetComponentsInChildren<Collider>())
                    UnityEngine.Physics.IgnoreCollision(a, b, true);
            foreach (var motor in motors) motor.ConfigureInputSource(null);
            yield return new WaitForSeconds(2f);
            foreach (var motor in motors)
            {
                Assert.That(motor.HasStableSupport, Is.True);
                uint sequence = motor.LandingRollSequence;
                motor.Body.position += motor.LocalUp * 3.5f;
                motor.Body.linearVelocity = Vector3.zero;
                motor.BeginExternalLaunch(4);
                float deadline = Time.time + 3f;
                while (motor.LandingRollSequence == sequence && Time.time < deadline) yield return new WaitForFixedUpdate();
                Assert.That(motor.LandingRollSequence, Is.GreaterThan(sequence), motor.name + " landing must start a roll");
                Vector3 start = motor.Body.position;
                Vector3 direction = motor.FacingForward;
                bool sawRollClip = false;
                bool captured = false;
                float maximumRollPhase = 0f;
                float minimumTorsoUp = 1f;
                float exitBlendFirst = -1f, exitBlendLast = -1f;
                int capturePhase = 0;
                var rollPresentation = motor.GetComponentInChildren<HumanoidCharacterPresentation>();
                var driver = rollPresentation.GetComponent<EarthAnimationDriver>();
                var rollAnimator = rollPresentation.GetComponent<Animator>();
                float finishAt = Time.time + 2f;
                float previousTarget = motor.LandingRollSpeed;
                while (Time.time < finishAt)
                {
                    yield return new WaitForFixedUpdate();
                    Assert.That(motor.LandingRollSpeed, Is.LessThanOrEqualTo(previousTarget + 0.001f));
                    previousTarget = motor.LandingRollSpeed;
                    var state = driver.GetCurrentAnimatorStateInfo(0);
                    if (state.fullPathHash == Animator.StringToHash("Base Layer.Moving Land"))
                    {
                        if (!driver.IsInTransition(0)) maximumRollPhase = Mathf.Max(maximumRollPhase, state.normalizedTime);
                        Vector3 hips = rollAnimator.GetBoneTransform(HumanBodyBones.Hips).position;
                        Vector3 head = rollAnimator.GetBoneTransform(HumanBodyBones.Head).position;
                        minimumTorsoUp = Mathf.Min(minimumTorsoUp, Vector3.Dot((head - hips).normalized, motor.LocalUp));
                        if (state.normalizedTime >= 0.35f + capturePhase * 0.18f && capturePhase < 4)
                        {
                            System.IO.Directory.CreateDirectory("BuildReports/LandingRollMotion");
                            ScreenCapture.CaptureScreenshot($"BuildReports/LandingRollMotion/{motor.name}-phase-{capturePhase}.png");
                            capturePhase++;
                        }
                        if (driver.IsInTransition(0) && driver.GetNextAnimatorStateInfo(0).fullPathHash == Animator.StringToHash("Base Layer.Locomotion"))
                        {
                            if (exitBlendFirst < 0f) exitBlendFirst = Time.time;
                            exitBlendLast = Time.time;
                        }
                    }
                    var presentation = motor.GetComponentInChildren<HumanoidCharacterPresentation>();
                    sawRollClip |= presentation != null && presentation.CurrentAuthoredAction == EarthAuthoredActionId.MovingLandingRoll;
                    if (!captured && sawRollClip && motor.LandingRollSpeed < 2.5f)
                    {
                        System.IO.Directory.CreateDirectory("BuildReports/LandingRollMotion");
                        ScreenCapture.CaptureScreenshot("BuildReports/LandingRollMotion/" + motor.name + ".png");
                        captured = true;
                    }
                }
                yield return new WaitForSeconds(0.2f);
                float travel = Vector3.Dot(motor.Body.position - start, direction);
                float speed = Vector3.ProjectOnPlane(motor.Body.linearVelocity, motor.LocalUp).magnitude;
                Debug.Log($"[Landing roll] actor={motor.name} travel={travel:F3}m finalSpeed={speed:F3} clip={sawRollClip} completedPhase={maximumRollPhase:F3} torsoUp={minimumTorsoUp:F3}");
                Assert.That(maximumRollPhase, Is.GreaterThanOrEqualTo(0.85f), "Do not blend away the roll before its authored get-up.");
                Assert.That(minimumTorsoUp, Is.LessThan(-0.15f), "Must actually tumble through the roll, not just enter its state.");
                Assert.That(exitBlendLast - exitBlendFirst, Is.GreaterThan(0.1f), "Outgoing blend must remain visible, not be overwritten by an immediate standing pose.");
                Assert.That(sawRollClip, Is.True, "Movement must accompany the authored roll.");
                Assert.That(travel, Is.InRange(2f, 4.5f), motor.name);
                Assert.That(speed, Is.LessThan(0.25f));
            }
        }

        [UnityTest]
        public IEnumerator ForwardAndBackwardStopsDoNotDragOrStretchAnkles()
        {
            _frameRate = Application.targetFrameRate;
            _vSync = QualitySettings.vSyncCount;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(_scene.isLoaded, Is.False, "Run from the focused QA launcher.");
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(ScenePath);
            _loaded = true;
            var feet = new System.Collections.Generic.List<EarthFootContactController>();
            var inputs = new System.Collections.Generic.List<StopRegressionInput>();
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                feet.AddRange(root.GetComponentsInChildren<EarthFootContactController>());
                foreach (var bot in root.GetComponentsInChildren<EarthMvpBotController>())
                    bot.enabled = false;
                foreach (var motor in root.GetComponentsInChildren<PlanetMotor>())
                {
                    var input = motor.gameObject.AddComponent<StopRegressionInput>();
                    inputs.Add(input);
                    motor.ConfigureInputSource(input);
                }
                foreach (var impact in root.GetComponentsInChildren<EarthCharacterImpactTarget>())
                    impact.SuppressImpacts(60f);
            }
            Assert.That(feet.Count, Is.EqualTo(2));
            yield return new WaitForSeconds(2f);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            var ankleLengths = new float[feet.Count * 2];
            for (int i = 0; i < feet.Count; i++)
            {
                var animator = feet[i].GetComponent<Animator>();
                ankleLengths[i * 2] = animator.GetBoneTransform(HumanBodyBones.LeftFoot).localPosition.magnitude;
                ankleLengths[i * 2 + 1] = animator.GetBoneTransform(HumanBodyBones.RightFoot).localPosition.magnitude;
            }
            foreach (float direction in new[] { -1f, 1f })
            {
                var peakSpeed = new float[feet.Count];
                foreach (var input in inputs) input.Move = new float2(0f, direction);
                float moveUntil = Time.time + 0.6f;
                while (Time.time < moveUntil)
                {
                    yield return null;
                    for (int i = 0; i < feet.Count; i++)
                    {
                        var motor = feet[i].GetComponentInParent<PlanetMotor>();
                        peakSpeed[i] = Mathf.Max(peakSpeed[i],
                            Vector3.ProjectOnPlane(motor.Body.linearVelocity, motor.LocalUp).magnitude);
                    }
                }
                for (int i = 0; i < feet.Count; i++)
                    Assert.That(peakSpeed[i], Is.GreaterThan(0.2f),
                        "Stop regression must include actual movement for both fighters.");
                foreach (var input in inputs) input.Move = float2.zero;
                yield return new WaitForSeconds(0.35f);
                float peakTargetLag = 0f;
                for (int frame = 0; frame < 60; frame++)
                {
                    yield return null;
                    for (int i = 0; i < feet.Count; i++)
                    {
                        var c = feet[i];
                        var animator = c.GetComponent<Animator>();
                        var motor = c.GetComponentInParent<PlanetMotor>();
                        for (int side = 0; side < 2; side++)
                        {
                            var ankle = animator.GetBoneTransform(side == 0
                                ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                            // Humanoid IK legitimately adjusts limb length within
                            // Avatar.legStretch. Compare with that budget, not an
                            // exact bind-length invariant (the idle baseline can
                            // itself already include Humanoid stretch).
                            float stretchBudget = animator.avatar.humanDescription.legStretch;
                            float maximumLength = ankleLengths[i * 2 + side] *
                                (1f + stretchBudget) / Mathf.Max(0.5f, 1f - stretchBudget) + 0.002f;
                            Assert.That(ankle.localPosition.magnitude, Is.LessThanOrEqualTo(maximumLength),
                                "Late foot correction must not stretch the solved shin.");
                            bool locked = side == 0 ? c.LeftFootLocked : c.RightFootLocked;
                            bool contact = side == 0 ? c.LeftHasContact : c.RightHasContact;
                            if (locked || !contact || !motor.HasStableSupport) continue;
                            Vector3 raw = side == 0 ? c.LeftRawContactPointWorld : c.RightRawContactPointWorld;
                            Vector3 target = side == 0 ? c.LeftTargetWorld : c.RightTargetWorld;
                            float lag = Vector3.ProjectOnPlane(target - raw, motor.LocalUp).magnitude;
                            peakTargetLag = Mathf.Max(peakTargetLag, lag);
                            Assert.That(lag, Is.LessThan(0.15f),
                                $"{c.name} free foot target trails after stop direction={direction}.");
                        }
                    }
                }
                Debug.Log($"[Foot stop] direction={direction}, samples=60, peakTargetLag={peakTargetLag:F4}");
            }
        }

        private sealed class StopRegressionInput : MonoBehaviour, IPlanetMotorInputSource
        {
            public float2 Move;
            public PlanetMotorCommand SampleCommand(uint tick) => new PlanetMotorCommand(tick, Move, false);
        }

        [UnityTest]
        public IEnumerator BothFightersKeepToesForwardWhenIdleContactReachesFullWeight()
        {
            _frameRate = Application.targetFrameRate;
            _vSync = QualitySettings.vSyncCount;
            // Use an isolated production-scene instance, never edit user assets.
            _scene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(_scene.isLoaded, Is.False, "Run from the focused QA launcher.");
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(ScenePath);
            _loaded = true;
            var feet = new System.Collections.Generic.List<EarthFootContactController>();
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                feet.AddRange(root.GetComponentsInChildren<EarthFootContactController>());
                foreach (var bot in root.GetComponentsInChildren<EarthMvpBotController>())
                    bot.enabled = false;
                foreach (var motor in root.GetComponentsInChildren<PlanetMotor>())
                    motor.ConfigureInputSource(null);
                foreach (var impact in root.GetComponentsInChildren<EarthCharacterImpactTarget>())
                    impact.SuppressImpacts(60f);
            }
            Assert.That(feet.Count, Is.EqualTo(2));
            yield return new WaitForSeconds(2f);
            QualitySettings.vSyncCount = 0;
            foreach (int cap in new[] { 30, 60, 120 })
            {
                Application.targetFrameRate = cap;
                // Re-enable the controller to replay the contact-weight ramp.
                foreach (var foot in feet) foot.enabled = false;
                yield return null;
                yield return null;
                foreach (var foot in feet) foot.enabled = true;
                yield return new WaitForSeconds(1f);
                float peakToeUp = -1f;
                for (int sample = 0; sample < 60; sample++)
                {
                    yield return null;
                    foreach (var foot in feet)
                    {
                        var animator = foot.GetComponent<Animator>();
                        var motor = foot.GetComponentInParent<PlanetMotor>();
                        Assert.That(motor.HasStableSupport, Is.True, foot.name);
                        Assert.That(foot.LeftFootIkWeight, Is.GreaterThan(0.8f), foot.name);
                        Assert.That(foot.RightFootIkWeight, Is.GreaterThan(0.8f), foot.name);
                        foreach (bool left in new[] { true, false })
                        {
                            Transform ankle = animator.GetBoneTransform(left
                                ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                            Transform toe = animator.GetBoneTransform(left
                                ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
                            Assert.That(toe, Is.Not.Null);
                            Vector3 direction = (toe.position - ankle.position).normalized;
                            float toeUp = Vector3.Dot(direction, motor.LocalUp);
                            peakToeUp = Mathf.Max(peakToeUp, toeUp);
                            Assert.That(float.IsFinite(toeUp), Is.True);
                            // Before the fix all four toes pointed almost straight
                            // up (0.97..0.99). Allow authored roll and arena slopes,
                            // but reject the imported-bone/IK-goal basis inversion.
                            Assert.That(toeUp, Is.LessThan(0.8f),
                                $"{foot.name} {(left ? "left" : "right")} toe flipped at cap {cap}.");
                        }
                    }
                }
                Debug.Log($"[Foot orientation] cap={cap}, samples=60, peakToeUp={peakToeUp:F4}");
            }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Application.targetFrameRate = _frameRate;
            QualitySettings.vSyncCount = _vSync;
            if (_loaded && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
            _loaded = false;
        }
    }
}
