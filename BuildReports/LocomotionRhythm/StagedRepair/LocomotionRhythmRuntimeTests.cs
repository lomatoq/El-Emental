using System.Collections;
using System.IO;
using Elemental.Presentation.Animation;
using Elemental.Presentation.UI;
using Elemental.Presentation.MotionMatching;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator ProductionLocomotionUsesActualSpeedPhaseAndKeepsFinalFootAnchors()
        {
            yield return EnterRhythmTestCombat();
            int laneIndex = 0;
            foreach (Actor actor in _actors)
            {
                var rhythm = actor.Presentation.GetComponent<LocomotionRhythmController>();
                var motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
                var feet = actor.Presentation.FootContactController;
                Assert.That(rhythm, Is.Not.Null, "Install the targeted metadata/adapters menu before acceptance.");
                Assert.That(motor.Capsule.sharedMaterial, Is.Not.Null,
                    "Restore the existing CharacterFrictionless material on production motor capsules.");
                Assert.That(motor.Capsule.sharedMaterial.dynamicFriction, Is.Zero,
                    "PhysX capsule drag must not compete with motor-owned locomotion traction.");
                // Elevated physical tangent lane avoids random rocks/the opposing
                // capsule while retaining the production motor, gravity and rig.
                Vector3 up = motor.LocalUp.normalized, forward = motor.FacingForward.normalized;
                Vector3 originalFeet = motor.SupportFeetPoint(up);
                Vector3 laneStart = originalFeet + up * (16f + laneIndex++ * 12f);
                Vector3 rootToFeet = motor.Body.position - originalFeet;
                var lane = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lane.name = "Locomotion physical acceptance lane";
                SceneManager.MoveGameObjectToScene(lane, _scene);
                lane.transform.SetPositionAndRotation(laneStart + forward * 50f - up * .5f, Quaternion.LookRotation(forward, up));
                lane.transform.localScale = new Vector3(20f, 1f, 120f);
                for (int layer = 0; layer < 32; layer++) if ((motor.GroundMask.value & (1 << layer)) != 0) { lane.layer = layer; break; }
                motor.Body.position = laneStart + rootToFeet + up * .03f;
                motor.Body.linearVelocity = Vector3.zero; motor.Body.angularVelocity = Vector3.zero;
                motor.ResetAfterTeleport(); UnityEngine.Physics.SyncTransforms();
                yield return new WaitForSeconds(.6f);
                float startDistance = rhythm.Motion.Distance, travel = 0f, peakSpeed = 0f, peakNominal = 0f;
                float peakError = 0f, maxPulse = 1f, minPulse = 1f;
                int contacts = 0, movingFrames = 0, independentPhases = 0;
                string worstContact = "none";
                float activeMovingSeconds = 0f;
                Vector3 previous = motor.transform.position;
                actor.Input.Move = new float2(0f, .35f);
                float runAt = Time.time + 1.5f;
                float until = Time.time + 3f;
                while (Time.time < until)
                {
                    if (Time.time >= runAt) actor.Input.Move = new float2(0f, 1f);
                    yield return _frame;
                    travel += Vector3.Distance(previous, motor.transform.position); previous = motor.transform.position;
                    var motion = rhythm.Motion;
                    Assert.That(actor.Bridge.RuntimeStatus, Is.Not.EqualTo(EAMMRuntimeStatus.PoseRejected), actor.Bridge.PoseRejectionReason);
                    peakSpeed = Mathf.Max(peakSpeed, motion.Speed); peakNominal = Mathf.Max(peakNominal, rhythm.AuthoredSpeed);
                    maxPulse = Mathf.Max(maxPulse, motion.Pulse); minPulse = Mathf.Min(minPulse, motion.Pulse);
                    Assert.That(rhythm.PlaybackRate, Is.InRange(.7999f, 1.2501f));
                    Assert.That(rhythm.StrideScale, Is.InRange(.8999f, 1.1001f));
                    if (motion.Speed > .5f && actor.Bridge.AppliedEammMasterWeight > .5f)
                    {
                        movingFrames++;
                        activeMovingSeconds += Time.deltaTime;
                        Assert.That(rhythm.AuthoredSpeed, Is.GreaterThan(.12f), "Active EAMM clip has no measured cadence speed.");
                        Assert.That(actor.Bridge.SourceController.LocomotionPlaybackRate,
                            Is.EqualTo(rhythm.PlaybackRate).Within(.001f), "Visible pose source ignored cadence.");
                        if (Mathf.Abs(Mathf.DeltaAngle(rhythm.RightPhase * 360f, (rhythm.LeftPhase + .5f) * 360f)) > 2f)
                            independentPhases++;
                    }
                    var pose = actor.Probe.Latest; _samples.Add(pose);
                    // Contact acquisition deliberately ramps by only .04/frame;
                    // short gait plants need not reach the full idle IK weight.
                    // Inspect the actual support lock and final anchor error.
                    if (feet.LeftFootLocked && pose.leftContactWeight > .05f)
                    {
                        contacts++;
                        if (feet.LeftAnchorErrorMeters > peakError)
                        { peakError = feet.LeftAnchorErrorMeters; worstContact = $"left weight={pose.leftContactWeight:F3} reason={feet.LeftReason} phase={rhythm.LeftPhase:F3} speed={motion.Speed:F3} source={actor.Bridge.SourceController.ContinuousFrame:F3}"; }
                    }
                    if (feet.RightFootLocked && pose.rightContactWeight > .05f)
                    {
                        contacts++;
                        if (feet.RightAnchorErrorMeters > peakError)
                        { peakError = feet.RightAnchorErrorMeters; worstContact = $"right weight={pose.rightContactWeight:F3} reason={feet.RightReason} phase={rhythm.RightPhase:F3} speed={motion.Speed:F3} source={actor.Bridge.SourceController.ContinuousFrame:F3}"; }
                    }
                }
                actor.Input.Move = float2.zero;
                Directory.CreateDirectory("BuildReports/LocomotionRhythm");
                File.WriteAllText("BuildReports/LocomotionRhythm/" + motor.name + ".txt",
                    $"distance={rhythm.Motion.Distance-startDistance:F4}; travel={travel:F4}; peakActual={peakSpeed:F3}; measuredNominal={peakNominal:F3}; rate={rhythm.PlaybackRate:F3}; stride={rhythm.StrideScale:F3}; pulse=[{minPulse:F3},{maxPulse:F3}]; movingFrames={movingFrames}; independentPhaseFrames={independentPhases}; contacts={contacts}; finalContactError={peakError:F4}");
                Debug.Log($"[LocomotionRhythm] {motor.name}: source={actor.Bridge.InitializationStatus}, reject={actor.Bridge.PoseRejectionReason}, action={actor.Presentation.CurrentAuthoredAction}, cycle={rhythm.MeasuredCycleDistance:F3}, leftPhase={rhythm.LeftPhase:F3}, motorPhase={rhythm.Motion.Phase01:F3}; left={feet.LeftReason}/{feet.LeftPlantState}/{feet.LeftSoleClearance:F3}; right={feet.RightReason}/{feet.RightPlantState}/{feet.RightSoleClearance:F3}; activeSeconds={activeMovingSeconds:F3}; worst={worstContact}");
                Assert.That(activeMovingSeconds, Is.GreaterThan(1.8f), "Expected sustained physical locomotion on the clear test lane. " + actor.Bridge.InitializationStatus);
                Assert.That(actor.Bridge.RuntimeStatus, Is.EqualTo(EAMMRuntimeStatus.Active));
                Assert.That(peakSpeed, Is.GreaterThan(.5f));
                Assert.That(rhythm.Motion.Distance-startDistance, Is.EqualTo(travel).Within(.6f), "Motor distance diverged from physical displacement.");
                Assert.That(minPulse, Is.GreaterThanOrEqualTo(.9499f)); Assert.That(maxPulse, Is.LessThanOrEqualTo(1.0501f));
                Assert.That(contacts, Is.GreaterThan(2)); Assert.That(peakError, Is.LessThan(.18f));
                // Measure ten complete distance cycles through the production
                // rigidbody motor, after acceleration settles. This includes the
                // real pulse/traction/phase adapter, not just solver arithmetic.
                actor.Input.Move = new float2(0f, 1f);
                yield return new WaitForSeconds(1f);
                float cycleTravel = 0f, desiredTravel = 0f, elapsed = 0f, cycles = 0f;
                float lastDistance = rhythm.Motion.Distance;
                float tenCycleDeadline = Time.time + 18f;
                var fixedFrame = new WaitForFixedUpdate();
                while (cycles < 10f && Time.time < tenCycleDeadline)
                {
                    yield return fixedFrame;
                    var sample = rhythm.Motion;
                    Assert.That(sample.Grounded, Is.True, "Ten-cycle acceptance left its physical test support.");
                    Assert.That(actor.Bridge.SourceController.HasValidSearchResult, Is.True, "Empty pose search must never be hidden by pose retention.");
                    float step = sample.Distance - lastDistance; lastDistance = sample.Distance;
                    cycleTravel += step;
                    cycles += step / Mathf.Max(.2f, rhythm.MeasuredCycleDistance * rhythm.StrideScale);
                    desiredTravel += math.length(sample.DesiredVelocity) * Time.fixedDeltaTime;
                    elapsed += Time.fixedDeltaTime;
                }
                actor.Input.Move = float2.zero;
                File.AppendAllText("BuildReports/LocomotionRhythm/" + motor.name + ".txt",
                    $"\nphysicalCycles={cycles:F3}; actualDistance={cycleTravel:F5}; desiredDistance={desiredTravel:F5}; seconds={elapsed:F3}; meanRatio={cycleTravel/Mathf.Max(.01f,desiredTravel):F6}; visibleLegScale={rhythm.VisualStrideScale:F4}");
                Assert.That(cycles, Is.GreaterThanOrEqualTo(10f));
                File.AppendAllText("BuildReports/LocomotionRhythm/" + motor.name + ".txt",
                    $"\nfootCorrectionCount={actor.Bridge.FootTrajectoryCorrectionCount}; peakRawFootStep={actor.Bridge.PeakRawFootStepDegrees:F3}; peakFootCorrection={actor.Bridge.PeakFootTrajectoryCorrectionDegrees:F3}");
                Assert.That(cycleTravel / desiredTravel, Is.EqualTo(1f).Within(.01f), "Actual ten-cycle mean speed changed by more than one percent.");
                ScreenCapture.CaptureScreenshot("BuildReports/LocomotionRhythm/"+motor.name+".png");
                yield return new WaitForSeconds(.5f);
            }
        }

        [UnityTest]
        public IEnumerator ProductionPresentationClockSlowsActualEammWithoutChangingSimulationTime()
        {
            yield return EnterRhythmTestCombat();
            Actor actor = _actors[0];
            var driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            var source = actor.Bridge.SourceController;
            Assert.That(source, Is.Not.Null);
            float timeScale = Time.timeScale;
            driver.SetPresentationClockMultiplier(.45f);
            int accepted = 0; float sum = 0f;
            try
            {
                yield return _frame;
                float last = source.ContinuousFrame;
                float until = Time.time + .7f;
                while (Time.time < until)
                {
                    yield return _frame;
                    float delta = source.ContinuousFrame - last; last = source.ContinuousFrame;
                    float expected = Time.deltaTime / source.DatabaseFrameTime * source.LocomotionPlaybackRate;
                    // Search jumps/wraps are discontinuities, not playback samples.
                    if (expected > .0001f && delta > 0f && delta < expected * .8f)
                    { sum += delta / expected; accepted++; }
                    Assert.That(Time.timeScale, Is.EqualTo(timeScale));
                    Assert.That(source.PresentationClockMultiplier, Is.EqualTo(.45f));
                }
                Assert.That(accepted, Is.GreaterThan(3));
                Assert.That(sum / accepted, Is.EqualTo(.45f).Within(.04f), "External clock never reached visible EAMM frame advancement.");
            }
            finally { driver.SetPresentationClockMultiplier(1f); }
            yield return _frame;
            Assert.That(source.PresentationClockMultiplier, Is.EqualTo(1f));
        }

        private IEnumerator EnterRhythmTestCombat()
        {
            foreach (var root in _scene.GetRootGameObjects())
            {
                var flow = root.GetComponentInChildren<FrontendFlowController>(true);
                if (flow == null) continue;
                if (flow.State == FrontendState.Main) Assert.That(flow.BeginBot(), Is.True);
                float deadline = Time.time + 5f;
                while (flow.State != FrontendState.Combat && Time.time < deadline) yield return _frame;
                Assert.That(flow.State, Is.EqualTo(FrontendState.Combat));
            }
            foreach (var root in _scene.GetRootGameObjects())
            {
                foreach (var duel in root.GetComponentsInChildren<EarthMvpDuelController>()) duel.enabled = false;
                foreach (var bot in root.GetComponentsInChildren<EarthMvpBotController>()) bot.enabled = false;
                foreach (var impact in root.GetComponentsInChildren<EarthCharacterImpactTarget>()) impact.SuppressImpacts(120f);
            }
            foreach (Actor actor in _actors)
                actor.Presentation.GetComponentInParent<PlanetMotor>().ConfigureInputSource(actor.Input);
            yield return new WaitForSeconds(.3f);
        }
    }
}
