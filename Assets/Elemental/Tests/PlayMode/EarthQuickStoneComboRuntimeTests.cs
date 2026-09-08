using System.Collections;
using System.IO;
using System.Collections.Generic;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using Elemental.Input.Actions;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest] public IEnumerator ProductionPairedStoneSeries30Fps() => ObserveProductionStoneCombo(30);
        [UnityTest] public IEnumerator ProductionPairedStoneSeries60Fps() => ObserveProductionStoneCombo(60);
        [UnityTest] public IEnumerator ProductionPairedStoneSeries120Fps() => ObserveProductionStoneCombo(120);

        private IEnumerator ObserveProductionStoneCombo(int fps)
        {
            int previousRate = Application.targetFrameRate;
            int previousVsync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fps;
            string captureDirectory = Path.GetFullPath($"BuildReports/QuickStoneCombo/{fps}fps");
            Directory.CreateDirectory(captureDirectory);
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            var dual = actor.Presentation.GetComponentInParent<EarthDualMouseAbilityController>();
            Assert.That(dual, Is.Not.Null);
            var animator = actor.Presentation.Animator;
            Assert.That(animator.GetLayerIndex("Earth Combo Full Body"), Is.GreaterThanOrEqualTo(0),
                "Install the saved combo controller before running production acceptance.");
            dual.CancelStompStone();
            var releases = new List<EarthQuickStoneBeat>();
            int captureContactFrames = 0;
            void Released(float cost)
            {
                releases.Add(dual.CurrentShotBeat);
                Assert.That(cost, Is.EqualTo(EarthQuickStoneCombo.ManaCost(dual.CurrentShotBeat)));
                if (EarthQuickStoneCombo.IsKick(dual.CurrentShotBeat))
                    Assert.That(Vector3.Distance(dual.LastShotOrigin, dual.LastShotSocket), Is.LessThan(.12f),
                        "Kick stone did not leave from the active foot contact.");
                captureContactFrames = 2;
            }
            dual.StoneShotCommitted += Released;
            try
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                float baselineHeadDistance = Vector3.Distance(head.position, hips.position);
                for (int beat = 0; beat < 5; beat++)
                {
                    Assert.That(dual.CastStompStone(new Vector2(Screen.width * .5f, Screen.height * .5f)), Is.True, "A fresh paired-shot command was rejected.");
                    Assert.That(dual.CurrentShotBeat, Is.EqualTo((EarthQuickStoneBeat)beat));
                    double deadline = Time.realtimeSinceStartupAsDouble + 3d;
                    bool sawSemantic = false;
                    float peakLeftFootHeight = 0f, peakRightFootHeight = 0f;
                    Transform actorRoot = dual.transform;
                    Vector3 initialRootForward = actorRoot.forward;
                    Vector3 up = actorRoot.up;
                    Vector3 previousHipForward = Vector3.ProjectOnPlane(hips.forward, up).normalized;
                    float accumulatedHipYaw = 0f;
                    int spinCapture = 0;
                    var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    while (dual.IsComboActionActive && Time.realtimeSinceStartupAsDouble < deadline)
                    {
                        dual.UpdateStompAim(new Vector2(Screen.width * (.5f + .04f * Mathf.Sin(Time.time * 4f)), Screen.height * .5f));
                        yield return null;
                        if (captureContactFrames > 0 && --captureContactFrames == 0)
                        {
                            var cameraField = typeof(EarthDualMouseAbilityController).GetField("castCamera",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            var camera = cameraField.GetValue(dual) as Camera;
                            Assert.That(camera, Is.Not.Null);
                            CaptureComboCamera(camera, Path.Combine(captureDirectory, $"{beat}-{dual.CurrentShotBeat}.png"));
                            var driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
                            int fullBodyLayer = animator.GetLayerIndex("Earth Combo Full Body");
                            var feet = actor.Presentation.FootContactController;
                            string diagnostics = $"beat={dual.CurrentShotBeat} time={dual.ComboNormalizedTime:F3} " +
                                $"fullBody={driver.GetLayerWeight(fullBodyLayer):F3} " +
                                $"upper={driver.GetLayerWeight(animator.GetLayerIndex("Earth Magic Upper Body")):F3} " +
                                $"leftIK={feet.LeftFootIkWeight:F3} rightIK={feet.RightFootIkWeight:F3} " +
                                $"action={actor.Presentation.CurrentAuthoredAction} " +
                                $"EAMM={(actor.Bridge != null ? actor.Bridge.AppliedEammMasterWeight : 0f):F3}";
                            File.WriteAllText(Path.Combine(captureDirectory, $"{beat}-{dual.CurrentShotBeat}.txt"), diagnostics);
                        }
                        Vector3 hipForward = Vector3.ProjectOnPlane(hips.forward, up).normalized;
                        if (previousHipForward.sqrMagnitude > .5f && hipForward.sqrMagnitude > .5f)
                            accumulatedHipYaw += Vector3.SignedAngle(previousHipForward, hipForward, up);
                        previousHipForward = hipForward;
                        if (beat == 4 && spinCapture < 2 && dual.ComboNormalizedTime >= (spinCapture == 0 ? .25f : .40f))
                        {
                            var cameraField = typeof(EarthDualMouseAbilityController).GetField("castCamera",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            CaptureComboCamera((Camera)cameraField.GetValue(dual),
                                Path.Combine(captureDirectory, $"4-SpinKick-mid-{spinCapture}.png"));
                            spinCapture++;
                        }
                        sawSemantic |= actor.Presentation.PoseController.CurrentRequest.Technique ==
                            EarthQuickStoneCombo.Technique((EarthQuickStoneBeat)beat);
                        peakLeftFootHeight = Mathf.Max(peakLeftFootHeight, Vector3.Dot(left.position - right.position, up));
                        peakRightFootHeight = Mathf.Max(peakRightFootHeight, Vector3.Dot(right.position - left.position, up));
                        Assert.That(Vector3.Angle(initialRootForward, actorRoot.forward), Is.LessThan(60f),
                            "Visual combo spun the gameplay actor root away from its aim.");
                        Assert.That(Vector3.Distance(head.position, hips.position),
                            Is.GreaterThan(baselineHeadDistance * .60f), "Head collapsed into the torso during combo.");
                    }
                    Assert.That(dual.IsComboActionActive, Is.False, "Beat never recovered.");
                    Assert.That(sawSemantic, Is.True, "Accepted attack never acquired its animation.");
                    Assert.That(releases.Count, Is.EqualTo(beat + 1), "A command lost or duplicated its projectile release.");
                    if (beat == 2) Assert.That(peakLeftFootHeight, Is.GreaterThan(.12f), "Left kick was masked out or foot IK pinned it.");
                    if (beat == 3) Assert.That(peakRightFootHeight, Is.GreaterThan(.12f), "Right kick was masked out or foot IK pinned it.");
                    if (beat == 4)
                    {
                        File.WriteAllText(Path.Combine(captureDirectory, "4-SpinKick-yaw.txt"), $"accumulatedRootRelativeHipsYaw={accumulatedHipYaw:F2}");
                        Assert.That(Mathf.Abs(accumulatedHipYaw), Is.GreaterThan(250f),
                            "Baked spin never became a visible full-body revolution at runtime.");
                    }
                }
                Assert.That(releases, Is.EqualTo(new[] { EarthQuickStoneBeat.FirstPunch,
                    EarthQuickStoneBeat.SecondPunch, EarthQuickStoneBeat.LeftKick,
                    EarthQuickStoneBeat.RightKick, EarthQuickStoneBeat.SpinKick }));
            }
            finally
            {
                dual.StoneShotCommitted -= Released;
                dual.CancelStompStone();
                Application.targetFrameRate = previousRate;
                QualitySettings.vSyncCount = previousVsync;
            }
        }
        [UnityTest]
        public IEnumerator PhysicalRapidPairedClicksReachSpin()
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            var dual = actor.Presentation.GetComponentInParent<EarthDualMouseAbilityController>();
            var router = actor.Presentation.GetComponentInParent<EarthActionRouterBehaviour>();
            var playerInput = actor.Presentation.GetComponentInParent<PlayerInput>();
            Assert.That(dual, Is.Not.Null);
            Assert.That(router, Is.Not.Null);
            Assert.That(playerInput, Is.Not.Null);
            dual.CancelStompStone();
            var comboField = typeof(EarthDualMouseAbilityController).GetField("_shotCombo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var combo = (EarthQuickStoneCombo)comboField.GetValue(dual);
            Mouse mouse = CreateMagicBurstMouse(playerInput);
            var releases = new List<EarthQuickStoneBeat>();
            void Released(float cost) => releases.Add(dual.CurrentShotBeat);
            dual.StoneShotCommitted += Released;
            int maxPending = 0;
            var routeTrace = new System.Text.StringBuilder();
            var adapter = router.GetComponent<EarthInputAdapter>();
            try
            {
                Camera camera = FindAnimationSceneComponent<Camera>(_scene);
                GameObject proxyObject = FindAnimationSceneObject(_scene, "Planet Collision Proxy");
                Collider proxy = proxyObject != null ? proxyObject.GetComponent<Collider>() : null;
                Physics.SyncTransforms();
                Assert.That(TryFindAnimationSurfacePoint(camera, proxy, out Vector2 pointer), Is.True);
                QueueDualMouseState(mouse, pointer, false, false);
                yield return null;
                double deadline = Time.realtimeSinceStartupAsDouble + 16d;
                while (releases.Count < 5 && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    float sentAt = Time.time;
                    QueueDualMouseState(mouse, pointer, true, true);
                    yield return null;
                    if (routeTrace.Length < 5000)
                        routeTrace.AppendLine($"press {Time.time:F2} focus={Application.isFocused} mouse={mouse.leftButton.isPressed}/{mouse.rightButton.isPressed} adapter={adapter.BendPrimaryHeld}/{adapter.BendForceHeld} router={router.Owner}/{router.Current.Phase} active={dual.IsComboActionActive} enabled={dual.enabled}/{router.enabled}");
                    QueueDualMouseState(mouse, pointer, false, false);
                    yield return null;
                    if (routeTrace.Length < 5000)
                        routeTrace.AppendLine($"release {Time.time:F2} router={router.Owner}/{router.Current.Phase} active={dual.IsComboActionActive} buffered={combo.HasBufferedCommand}");
                    do
                    {
                        maxPending = Mathf.Max(maxPending, combo.HasBufferedCommand ? 1 : 0);
                        Assert.That(actor.Presentation.PoseController.QueuedPresentationCount, Is.LessThanOrEqualTo(1));
                        yield return null;
                    } while (Time.time - sentAt < .17f && releases.Count < 5);
                }
                QueueDualMouseState(mouse, pointer, false, false);
                Assert.That(releases.Count, Is.GreaterThanOrEqualTo(5),
                    "Physical rapid LMB+RMB input never reached the finisher through the shipping router.\n" + routeTrace);
                for (int beat = 0; beat < 5; beat++)
                    Assert.That(releases[beat], Is.EqualTo((EarthQuickStoneBeat)beat));
                Assert.That(maxPending, Is.EqualTo(1), "The test never exercised the one-command buffer.");
                int releasesAtStop = releases.Count;
                double tailDeadline = Time.realtimeSinceStartupAsDouble + 5d;
                while (dual.IsComboActionActive && Time.realtimeSinceStartupAsDouble < tailDeadline)
                    yield return null;
                Assert.That(dual.IsComboActionActive, Is.False, "Released buttons left a never-ending cast queue.");
                Assert.That(combo.HasBufferedCommand, Is.False);
                Assert.That(releases.Count, Is.LessThanOrEqualTo(releasesAtStop + 1),
                    "More than the single accepted pending shot replayed after button release.");
                int settledCount = releases.Count;
                float settledAt = Time.time;
                while (Time.time - settledAt < .7f) yield return null;
                Assert.That(releases.Count, Is.EqualTo(settledCount), "A stale input replayed after full recovery.");
            }
            finally
            {
                dual.StoneShotCommitted -= Released;
                dual.CancelStompStone();
                if (mouse != null) InputSystem.RemoveDevice(mouse);
            }
        }

        private static void CaptureComboCamera(Camera camera, string path)
        {
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            Texture2D image = null;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (image != null) Object.Destroy(image);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
