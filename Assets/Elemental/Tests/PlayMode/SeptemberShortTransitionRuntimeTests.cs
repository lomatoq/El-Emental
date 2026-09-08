using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator ProductionStartWalkPlaysOnceReturnsToLoopAndCancelsOnBackpedal()
        {
            Actor actor = ShortPlayer();
            var motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            var driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts,
                "Elemental.Character.Presentation", 128);
            var timing = new long[128];
            int timingCount = 0;
            actor.Input.Move = float2.zero;
            yield return new WaitForSeconds(.65f);
            Assert.That(motor.HasStableSupport, Is.True);
            Vector3 initial = motor.Body.position;
            actor.Input.Move = new float2(0f, 1f);
            double deadline = Time.realtimeSinceStartupAsDouble + .58d;
            bool entered = false, captured = false;
            int entries = 0;
            EarthShortTransition previous = EarthShortTransition.None;
            float firstEntry = 0f;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return _frame;
                if (recorder.Valid && recorder.LastValue > 0 && timingCount < timing.Length)
                    timing[timingCount++] = recorder.LastValue;
                var kind = actor.Presentation.ShortTransition;
                if (kind == EarthShortTransition.StartWalk && previous != kind)
                { entries++; entered = true; firstEntry = Time.time; }
                if (kind == EarthShortTransition.StartWalk)
                {
                    AssertShortClipVisible(actor, driver, kind);
                    if (!captured && Time.time - firstEntry > .12f)
                    { CaptureShortPose("01-start-walk"); captured = true; }
                }
                _samples.Add(actor.Probe.Latest);
                previous = kind;
            }
            Assert.That(entered && captured, Is.True, "Physical forward command never showed the short entry clip.");
            Assert.That(entries, Is.EqualTo(1), "Continuous travel replayed the one-shot bridge.");
            Assert.That(actor.Presentation.ShortTransition, Is.EqualTo(EarthShortTransition.None));
            Assert.That(driver.GetCurrentAnimatorStateInfo(0).fullPathHash,
                Is.EqualTo(Animator.StringToHash("Base Layer.Locomotion")));
            Assert.That(Vector3.Distance(initial, motor.Body.position), Is.GreaterThan(.2f),
                "Animation must not consume or delay physical movement.");
            Assert.That(timingCount, Is.GreaterThan(0), "The existing presentation CPU marker produced no measurements.");
            Array.Sort(timing, 0, timingCount);
            double totalNanoseconds = 0d;
            for (int index = 0; index < timingCount; index++) totalNanoseconds += timing[index];
            Directory.CreateDirectory("BuildReports/ShortTransitions");
            File.WriteAllText("BuildReports/ShortTransitions/presentation-profile.json", JsonUtility.ToJson(
                new ShortPresentationProfile
                {
                    utc = DateTime.UtcNow.ToString("O"), samples = timingCount,
                    meanMicroseconds = totalNanoseconds / timingCount / 1000d,
                    p95Microseconds = timing[Mathf.Min(timingCount - 1, Mathf.CeilToInt(timingCount * .95f) - 1)] / 1000d,
                    peakMicroseconds = timing[timingCount - 1] / 1000d,
                    scope = "Elemental.Character.Presentation; shared marker over production actors during physical start and loop; excludes full render/GPU cost"
                }, true));
            CaptureShortPose("02-walk-loop");

            actor.Input.Move = float2.zero;
            yield return new WaitForSeconds(.8f);
            actor.Input.Move = new float2(0f, 1f);
            deadline = Time.realtimeSinceStartupAsDouble + .8d;
            while (actor.Presentation.ShortTransition != EarthShortTransition.StartWalk &&
                Time.realtimeSinceStartupAsDouble < deadline) yield return _frame;
            Assert.That(actor.Presentation.ShortTransition, Is.EqualTo(EarthShortTransition.StartWalk));
            float reversedAt = Time.time;
            actor.Input.Move = new float2(0f, -1f);
            deadline = Time.realtimeSinceStartupAsDouble + .28d;
            while (actor.Presentation.ShortTransition == EarthShortTransition.StartWalk &&
                Time.realtimeSinceStartupAsDouble < deadline) yield return _frame;
            Assert.That(actor.Presentation.ShortTransition, Is.EqualTo(EarthShortTransition.None),
                "Backpedaling must cancel the forward-start clip before its normal timeout.");
            Assert.That(Time.time - reversedAt, Is.LessThan(.30f));
            yield return new WaitForSeconds(.25f);
            Assert.That(driver.GetCurrentAnimatorStateInfo(0).fullPathHash,
                Is.EqualTo(Animator.StringToHash("Base Layer.Locomotion")));
            CaptureShortPose("03-backward-cancel");
            actor.Input.Move = float2.zero;
        }

        [UnityTest]
        public IEnumerator ProductionGroundedPillarCancellationUsesCrouchExitWithoutLaunching()
        {
            Actor actor = ShortPlayer();
            var motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            var pillar = motor.GetComponent<EarthPillarMobility>();
            var driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            actor.Input.Move = float2.zero;
            yield return new WaitForSeconds(.4f);
            Assert.That(pillar.BeginCharge(), Is.True, "Real supported pillar charge ingress failed.");
            yield return new WaitForSeconds(.35f);
            Assert.That(pillar.IsCharging, Is.True);
            CaptureShortPose("04-grounded-charge");
            pillar.CancelCharge();
            double deadline = Time.realtimeSinceStartupAsDouble + .6d;
            bool observed = false, captured = false;
            float enteredAt = 0f;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return _frame;
                if (actor.Presentation.ShortTransition == EarthShortTransition.CrouchExit)
                {
                    if (!observed) enteredAt = Time.time;
                    observed = true;
                    AssertShortClipVisible(actor, driver, EarthShortTransition.CrouchExit);
                    if (!captured && Time.time - enteredAt > .12f)
                    { CaptureShortPose("05-crouch-exit"); captured = true; }
                }
                Assert.That(pillar.IsLaunchPending, Is.False, "Cancellation scheduled a gameplay launch.");
                Assert.That(motor.HasStableSupport, Is.True);
                _samples.Add(actor.Probe.Latest);
            }
            Assert.That(observed && captured, Is.True, "Grounded charge cancellation skipped the authored stand-up bridge.");
            Assert.That(actor.Presentation.ShortTransition, Is.EqualTo(EarthShortTransition.None));
            Assert.That(driver.GetCurrentAnimatorStateInfo(0).fullPathHash,
                Is.EqualTo(Animator.StringToHash("Base Layer.Locomotion")));
            CaptureShortPose("06-crouch-returned");
        }

        [UnityTest]
        public IEnumerator ProductionBackwardShortDropUsesStepDownAndYieldsAtRealContact()
        {
            Actor actor = ShortPlayer();
            var motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            var driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            Vector3 up = motor.LocalUp.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            Vector3 floor = motor.SupportFeetPoint(up);
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Short transition physical 55cm step fixture";
            SceneManager.MoveGameObjectToScene(block, _scene);
            block.transform.SetPositionAndRotation(floor + up * .265f, Quaternion.LookRotation(forward, up));
            block.transform.localScale = new Vector3(3f, .55f, 2f);
            for (int layer = 0; layer < 32; layer++)
                if ((motor.GroundMask.value & (1 << layer)) != 0) { block.layer = layer; break; }
            actor.Input.Move = float2.zero;
            var puppet = motor.GetComponent<ActiveRagdollPuppet>();
            Assert.That(puppet, Is.Not.Null, "Production relocation must include every active puppet joint body.");
            puppet.ResetPhysicalState(motor.Body.position + up * .58f, motor.Body.rotation);
            motor.ResetAfterTeleport();
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.7f);
            Assert.That(motor.HasStableSupport, Is.True, "Actor did not settle on the physical test step.");
            float originalHeight = Vector3.Dot(motor.Body.position, up);
            actor.Input.Move = new float2(0f, -.20f);
            double deadline = Time.realtimeSinceStartupAsDouble + 4d;
            bool departed = false, played = false, landed = false, captured = false;
            var trace = new System.Text.StringBuilder("time,grounded,kind,air,candidate,distance,vertical,speed,action,roll\n");
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return _frame;
                if (!motor.HasStableSupport) departed = true;
                var kind = actor.Presentation.ShortTransition;
                var prediction = actor.Presentation.LandingCandidate;
                Vector3 bottom = motor.Capsule.transform.TransformPoint(motor.Capsule.center) - motor.LocalUp * motor.Capsule.height * motor.Capsule.transform.lossyScale.y * .5f;
                var point = new Vector3(prediction.Point.x,prediction.Point.y,prediction.Point.z);
                var policyState = (EarthShortTransitionState)typeof(HumanoidCharacterPresentation).GetField("_shortTransitionState",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(actor.Presentation);
                trace.AppendLine($"{Time.time:F3},{motor.HasStableSupport},{kind},{policyState.AirSeconds:F3},{prediction.IsValid},{Vector3.Dot(bottom-point,motor.LocalUp):F3},{Vector3.Dot(motor.Body.linearVelocity,motor.LocalUp):F3},{Vector3.ProjectOnPlane(motor.Body.linearVelocity,motor.LocalUp).magnitude:F3},{actor.Presentation.CurrentAuthoredAction},{actor.Presentation.LandingRollAllowed}");
                if (kind == EarthShortTransition.StepDown)
                {
                    played = true;
                    Assert.That(motor.HasStableSupport, Is.False,
                        "Step-down cannot retain airborne ownership after true floor contact.");
                    AssertShortClipVisible(actor, driver, kind);
                    if (!captured) { CaptureShortPose("07-backward-step-down"); captured = true; }
                }
                if (departed && motor.HasStableSupport &&
                    originalHeight - Vector3.Dot(motor.Body.position, up) > .2f)
                { landed = true; break; }
                _samples.Add(actor.Probe.Latest);
            }
            actor.Input.Move = float2.zero;
            Directory.CreateDirectory("BuildReports/ShortTransitions");
            File.WriteAllText("BuildReports/ShortTransitions/short-drop-trace.csv",trace.ToString());
            Assert.That(departed && played && landed, Is.True,
                $"Short physical drop incomplete: left support={departed}, authored step={played}, lower contact={landed}.");
            yield return _frame;
            Assert.That(actor.Presentation.ShortTransition, Is.EqualTo(EarthShortTransition.None));
            CaptureShortPose("08-step-contact");
            UnityEngine.Object.Destroy(block);
        }

        private Actor ShortPlayer() => _actors.Single(value =>
            value.Presentation.GetComponent<EarthCharacterPoseController>() != null);

        [Serializable]
        private sealed class ShortPresentationProfile
        {
            public string utc, scope;
            public int samples;
            public double meanMicroseconds, p95Microseconds, peakMicroseconds;
        }

        private static void AssertShortClipVisible(Actor actor, EarthAnimationDriver driver, EarthShortTransition kind)
        {
            int hash = Animator.StringToHash("Base Layer." + EarthShortTransitionPolicy.StateName(kind));
            var current = driver.GetCurrentAnimatorStateInfo(0);
            var next = driver.IsInTransition(0) ? driver.GetNextAnimatorStateInfo(0) : default;
            Assert.That(current.fullPathHash == hash || next.fullPathHash == hash, Is.True,
                $"Telemetry claims {kind}, but actual Animator lane is {current.fullPathHash}/{next.fullPathHash}.");
            Assert.That(actor.Bridge.AppliedEammMasterWeight, Is.LessThan(.001f),
                "EAMM must not replace the imported bridge while its Animator state plays.");
            Assert.That(actor.Probe.Latest.headHeight, Is.GreaterThan(.25f));
        }

        private void CaptureShortPose(string name)
        {
            string directory = "BuildReports/ShortTransitions";
            Directory.CreateDirectory(directory);
            var camera = _scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<Camera>())
                .Single(value => value.CompareTag("MainCamera"));
            int width = 1280, height = Mathf.RoundToInt(width / camera.aspect);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            float previousFov = camera.fieldOfView;
            try
            {
                // Readable pose proof only: restore the gameplay lens before this
                // coroutine yields, without touching the virtual lens or state selection.
                camera.fieldOfView = 37f;
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.fieldOfView = previousFov;
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(pixels);
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
        }
    }
}
