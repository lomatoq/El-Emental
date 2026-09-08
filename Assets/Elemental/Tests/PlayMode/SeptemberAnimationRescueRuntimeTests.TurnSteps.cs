using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [Serializable] private sealed class TurnStepFrame
        {
            public string scenario;
            public float time, phase, turnWeight, direction, leftHeight, rightHeight, leftContact, rightContact, eamm;
            public float presentationClock, animatorSpeed, stateLength, stateSpeed, stateSpeedMultiplier, turnClipLength;
            public float leftAboveSurface, rightAboveSurface, leftProbeClearance, rightProbeClearance;
            public float leftAnkleFloor, rightAnkleFloor, leftToeFloor, rightToeFloor, leftFloorCorrection, rightFloorCorrection;
            public float pelvisOffset, leftFloorPredicted, rightFloorPredicted, leftFloorGoal, rightFloorGoal, leftFloorBone, rightFloorBone;
            public bool leftFloorEvaluated, rightFloorEvaluated;
            public int leftReason, rightReason;
            public bool step;
        }
        [Serializable] private sealed class TurnStepReport { public TurnStepFrame[] frames; }

        [UnityTest]
        public IEnumerator ProductionTurnStepsCompleteShortTapsAndSustainedHalfTurns()
        {
            Actor actor = _actors.Find(candidate => candidate.Presentation.GetComponent<EarthCharacterPoseController>() != null);
            Assert.That(actor, Is.Not.Null);
            var animator = actor.Presentation.GetComponent<Animator>();
            var driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            var motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            var body = motor.GetComponent<Rigidbody>();
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot), right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Assert.That(left, Is.Not.Null); Assert.That(right, Is.Not.Null);
            Transform leftToe = animator.GetBoneTransform(HumanBodyBones.LeftToes), rightToe = animator.GetBoneTransform(HumanBodyBones.RightToes);
            Assert.That(leftToe, Is.Not.Null); Assert.That(rightToe, Is.Not.Null);
            var floorHits = new RaycastHit[64];
            string directory = "BuildReports/TurnInPlaceRepair/Proof"; Directory.CreateDirectory(directory);
            var frames = new List<TurnStepFrame>(); var clips = new List<AnimatorClipInfo>();
            var sequenceFloorFailures = new List<string>();
            var cameraObject = new GameObject("Turn step proof camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.fieldOfView = 36; camera.nearClipPlane = .05f;
            camera.allowHDR = true;
            var texture = new RenderTexture(640, 800, 24, RenderTextureFormat.ARGB32); texture.Create();
            var pixels = new Texture2D(640, 800, TextureFormat.RGB24, false);
            int capture = 0;
            try
            {
                foreach (float direction in new[] { -1f, 1f })
                foreach (bool tap in new[] { true, false })
                {
                    actor.Input.Move = float2.zero; yield return new WaitForSeconds(1.3f);
                    Assert.That(motor.HasStableSupport, Is.True, "Turn proof needs the actual grounded production actor.");
                    string scenario = (direction < 0 ? "left" : "right") + (tap ? "-tap" : "-180");
                    Vector3 origin = body.position, up = motor.LocalUp, previous = motor.FacingForward;
                    Vector3 viewSide = Vector3.Cross(up, previous).normalized;
                    camera.transform.position = origin + previous * 3.9f + viewSide * 2.6f + up * .45f;
                    camera.transform.rotation = Quaternion.LookRotation(origin - up * .10f - camera.transform.position, up);
                    float began = Time.time, deadline = began + (tap ? 2.3f : 4.2f), yaw = 0, releasedAt = -1, nextCapture = began;
                    float maxWeight = 0, minRelativeHeight = float.PositiveInfinity, maxRelativeHeight = float.NegativeInfinity;
                    float minContact = 1, latestPhase = 0; bool stepSeen = false, stepAfterRelease = false;
                    float minSwingAnkle = float.PositiveInfinity, minSwingToe = float.PositiveInfinity, swingTravel = 0;
                    float minSequenceAnkle = float.PositiveInfinity, minSequenceToe = float.PositiveInfinity;
                    bool sequenceFloorFound = true;
                    int swingFloorSamples = 0;
                    Vector3 previousLeft = left.position, previousRight = right.position;
                    actor.Input.Move = new float2(direction, 0);
                    while (Time.time < deadline)
                    {
                        yield return _frame;
                        yaw += Mathf.Abs(Vector3.SignedAngle(previous, motor.FacingForward, motor.LocalUp)); previous = motor.FacingForward;
                        if (releasedAt < 0 && (tap ? Time.time - began >= .08f : yaw >= 180f))
                        { actor.Input.Move = float2.zero; releasedAt = Time.time; }
                        var state = driver.GetCurrentAnimatorStateInfo(0);
                        float turnWeight = 0, turnClipLength = 0; clips.Clear(); driver.GetCurrentAnimatorClipInfo(0, clips);
                        foreach (var clip in clips) if (clip.clip != null && clip.clip.name == "Left Turn")
                        { turnWeight += clip.weight; turnClipLength = clip.clip.length; }
                        float leftHeight = Vector3.Dot(left.position - body.position, motor.LocalUp);
                        float rightHeight = Vector3.Dot(right.position - body.position, motor.LocalUp);
                        var contacts = actor.Presentation.FootContactController;
                        bool leftAnkleFound = TurnFloorClearance(left.position, motor, floorHits, out float leftAnkleFloor);
                        bool rightAnkleFound = TurnFloorClearance(right.position, motor, floorHits, out float rightAnkleFloor);
                        bool leftToeFound = TurnFloorClearance(leftToe.position, motor, floorHits, out float leftToeFloor);
                        bool rightToeFound = TurnFloorClearance(rightToe.position, motor, floorHits, out float rightToeFloor);
                        sequenceFloorFound &= leftAnkleFound && rightAnkleFound && leftToeFound && rightToeFound;
                        minSequenceAnkle = Mathf.Min(minSequenceAnkle, leftAnkleFloor, rightAnkleFloor);
                        minSequenceToe = Mathf.Min(minSequenceToe, leftToeFloor, rightToeFloor);
                        frames.Add(new TurnStepFrame { scenario = scenario, time = Time.time - began, phase = state.normalizedTime,
                            turnWeight = turnWeight, direction = actor.Presentation.AuthoredTurnStepDirection,
                            leftHeight = leftHeight, rightHeight = rightHeight, leftContact = actor.Probe.Latest.leftContactWeight,
                            rightContact = actor.Probe.Latest.rightContactWeight, eamm = actor.Bridge.AppliedEammMasterWeight,
                            presentationClock = driver.PresentationClockMultiplier, animatorSpeed = animator.speed,
                            stateLength = state.length, stateSpeed = state.speed, stateSpeedMultiplier = state.speedMultiplier,
                            turnClipLength = turnClipLength,
                            leftAboveSurface = Vector3.Dot(left.position - contacts.LeftRawContactPointWorld, motor.LocalUp),
                            rightAboveSurface = Vector3.Dot(right.position - contacts.RightRawContactPointWorld, motor.LocalUp),
                            leftProbeClearance = contacts.LeftSoleClearance, rightProbeClearance = contacts.RightSoleClearance,
                            leftAnkleFloor = leftAnkleFloor, rightAnkleFloor = rightAnkleFloor, leftToeFloor = leftToeFloor, rightToeFloor = rightToeFloor,
                            leftFloorCorrection = contacts.LeftSwingFloorCorrectionMeters, rightFloorCorrection = contacts.RightSwingFloorCorrectionMeters,
                            pelvisOffset = contacts.PelvisOffsetMeters,
                            leftFloorEvaluated = contacts.LeftFloorEvaluated, rightFloorEvaluated = contacts.RightFloorEvaluated,
                            leftFloorPredicted = contacts.LeftFloorPredictedClearance, rightFloorPredicted = contacts.RightFloorPredictedClearance,
                            leftFloorGoal = contacts.LeftFloorGoalClearance, rightFloorGoal = contacts.RightFloorGoalClearance,
                            leftFloorBone = contacts.LeftFloorBoneClearance, rightFloorBone = contacts.RightFloorBoneClearance,
                            leftReason = (int)contacts.LeftReason, rightReason = (int)contacts.RightReason,
                            step = actor.Presentation.AuthoredTurnStepActive });
                        if (actor.Presentation.AuthoredTurnStepActive)
                        {
                            stepSeen = true; stepAfterRelease |= releasedAt >= 0 && Time.time - releasedAt > .2f;
                            maxWeight = Mathf.Max(maxWeight, turnWeight);
                            if (turnWeight > .8f)
                            {
                                float relativeHeight = leftHeight - rightHeight;
                                minRelativeHeight = Mathf.Min(minRelativeHeight, relativeHeight); maxRelativeHeight = Mathf.Max(maxRelativeHeight, relativeHeight);
                                minContact = Mathf.Min(minContact, actor.Probe.Latest.leftContactWeight, actor.Probe.Latest.rightContactWeight);
                                latestPhase = Mathf.Max(latestPhase, state.normalizedTime);
                                if (actor.Probe.Latest.leftContactWeight < .2f && !contacts.LeftFootLocked && contacts.LeftReason == Elemental.Simulation.Characters.EarthFootContactReason.Swing)
                                {
                                    minSwingAnkle = Mathf.Min(minSwingAnkle, leftAnkleFloor); minSwingToe = Mathf.Min(minSwingToe, leftToeFloor); swingFloorSamples++;
                                    swingTravel += Vector3.ProjectOnPlane(left.position - previousLeft, motor.LocalUp).magnitude;
                                }
                                if (actor.Probe.Latest.rightContactWeight < .2f && !contacts.RightFootLocked && contacts.RightReason == Elemental.Simulation.Characters.EarthFootContactReason.Swing)
                                {
                                    minSwingAnkle = Mathf.Min(minSwingAnkle, rightAnkleFloor); minSwingToe = Mathf.Min(minSwingToe, rightToeFloor); swingFloorSamples++;
                                    swingTravel += Vector3.ProjectOnPlane(right.position - previousRight, motor.LocalUp).magnitude;
                                }
                            }
                        }
                        previousLeft = left.position; previousRight = right.position;
                        if (Time.time >= nextCapture)
                        {
                            nextCapture = Time.time + .1f;
                            RenderTexture prior = RenderTexture.active;
                            try
                            {
                                var request = new RenderPipeline.StandardRequest { destination = texture };
                                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                                RenderPipeline.SubmitRenderRequest(camera, request);
                                RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, 640, 800), 0, 0); pixels.Apply();
                                File.WriteAllBytes(Path.Combine(directory, $"frame-{capture++:D4}.png"), pixels.EncodeToPNG());
                            }
                            finally { RenderTexture.active = prior; }
                        }
                        if (releasedAt >= 0 && Time.time - releasedAt > 1.3f && !actor.Presentation.AuthoredTurnStepActive) break;
                    }
                    actor.Input.Move = float2.zero;
                    Debug.Log($"[Turn whole-sequence floor proof] {scenario}: min ankle={minSequenceAnkle:F4}m, toe={minSequenceToe:F4}m; includes Capture and turn-to-idle exit.");
                    Debug.Log($"[Turn clock proof] {scenario}: presentationClock={driver.PresentationClockMultiplier:R}, animator.speed={animator.speed:R}, state.length={driver.GetCurrentAnimatorStateInfo(0).length:R}, state.speed={driver.GetCurrentAnimatorStateInfo(0).speed:R}, normalized={driver.GetCurrentAnimatorStateInfo(0).normalizedTime:R}, controller={animator.runtimeAnimatorController.name}.");
                    Assert.That(stepSeen, Is.True, scenario + ": no authored step.");
                    Assert.That(maxWeight, Is.GreaterThan(.85f), scenario + ": the turn clip is still diluted by idle.");
                    Assert.That(latestPhase, Is.GreaterThan(.8f), scenario + ": turn was cut before its planted finish.");
                    Assert.That(maxRelativeHeight - minRelativeHeight, Is.GreaterThan(.018f), scenario + ": final rendered feet did not visibly alternate vertical separation.");
                    Assert.That(minContact, Is.LessThan(.65f), scenario + ": both feet stayed pinned through the supposed step.");
                    if (swingFloorSamples == 0)
                        sequenceFloorFailures.Add(scenario + ": no genuinely released swing sample was measured.");
                    if (minSwingAnkle < .015f)
                        sequenceFloorFailures.Add($"{scenario}: released Swing ankle {minSwingAnkle:R}m is below .015m.");
                    if (minSwingToe < -.02f)
                        sequenceFloorFailures.Add($"{scenario}: released Swing toe {minSwingToe:R}m is below -.02m.");
                    Assert.That(swingTravel, Is.GreaterThan(.025f), scenario + ": correction erased the horizontal footstep.");
                    if (!sequenceFloorFound)
                        sequenceFloorFailures.Add(scenario + ": every turn/exit sample needs actual ankle and toe floor evidence.");
                    if (minSequenceAnkle < .015f)
                        sequenceFloorFailures.Add($"{scenario}: minimum ankle {minSequenceAnkle:R}m is below .015m during Capture or turn-to-idle exit.");
                    if (minSequenceToe < -.02f)
                        sequenceFloorFailures.Add($"{scenario}: minimum toe {minSequenceToe:R}m is below -.02m during Capture or turn-to-idle exit.");
                    if (tap) Assert.That(stepAfterRelease, Is.True, "A short tap must finish its visible step after key-up.");
                    else Assert.That(yaw, Is.GreaterThanOrEqualTo(180f), scenario + ": did not perform the real half-turn.");
                    Assert.That(Vector3.ProjectOnPlane(body.position - origin, up).magnitude, Is.LessThan(.35f), "Turn test translated instead of turning in place.");
                    Assert.That(actor.Presentation.AuthoredTurnStepActive, Is.False, "Released turn never returned to idle.");
                    Debug.Log($"[Turn step proof] {scenario}: clip={maxWeight:F3}, final foot separation span={maxRelativeHeight-minRelativeHeight:F4}m, min contact={minContact:F3}, yaw={yaw:F1}deg.");
                    Debug.Log($"[Turn floor proof] {scenario}: min ankle={minSwingAnkle:F4}m, toe={minSwingToe:F4}m, released horizontal travel={swingTravel:F4}m, samples={swingFloorSamples}.");
                }
                Assert.That(sequenceFloorFailures, Is.Empty, string.Join("\n", sequenceFloorFailures));
            }
            finally
            {
                actor.Input.Move = float2.zero;
                File.WriteAllText(Path.Combine(directory, "TurnStepFrames.json"), JsonUtility.ToJson(new TurnStepReport { frames = frames.ToArray() }, true));
                texture.Release(); UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(cameraObject);
            }
        }
        private static bool TurnFloorClearance(Vector3 point, PlanetMotor motor, RaycastHit[] hits, out float clearance)
        {
            Vector3 up = motor.LocalUp.normalized;
            int count = Physics.RaycastNonAlloc(point + up * .55f, -up, hits, 1.1f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity; clearance = 1000f;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(motor.transform) ||
                    EarthBodyTargetFilter.IsCharacterBody(collider.attachedRigidbody) || Vector3.Dot(hits[i].normal, up) < .45f || hits[i].distance >= nearest) continue;
                nearest = hits[i].distance; clearance = Vector3.Dot(point - hits[i].point, up);
            }
            return nearest < float.PositiveInfinity;
        }
    }
}
