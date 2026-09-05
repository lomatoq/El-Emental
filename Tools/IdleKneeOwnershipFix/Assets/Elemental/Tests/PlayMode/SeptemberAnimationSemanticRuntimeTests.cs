using System;
using System.Collections;
using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    /// <summary>
    /// Staged under Tools. Copy into Assets/Elemental/Tests/PlayMode before running.
    /// Partial fixture intentionally reuses the production-scene setup and cleanup.
    /// </summary>
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator SemanticMagicSlotsBecomeOneHotAndKeepAValidUpperBodyPose()
        {
            Actor actor = _actors.Find(value =>
                value.Presentation.GetComponent<EarthCharacterPoseController>() != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose =
                actor.Presentation.GetComponent<EarthCharacterPoseController>();
            EarthAnimationDriver driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            Animator animator = actor.Presentation.GetComponent<Animator>();
            MethodInfo begin = typeof(EarthCharacterPoseController).GetMethod(
                "BeginAuthoritative", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(begin, Is.Not.Null);
            int magicLayer = animator.GetLayerIndex("Earth Magic Upper Body");
            Assert.That(magicLayer, Is.GreaterThanOrEqualTo(0));
            int castHash = Animator.StringToHash("Earth Magic Upper Body.Earth Cast");
            EarthTechniqueId[] techniques =
            {
                EarthTechniqueId.RaiseWall, EarthTechniqueId.RaisePlatform,
                EarthTechniqueId.PullStone, EarthTechniqueId.ThrowStone,
                EarthTechniqueId.VectorPush, EarthTechniqueId.Repair,
                EarthTechniqueId.Resonance, EarthTechniqueId.PillarJump,
                EarthTechniqueId.Armor, EarthTechniqueId.ArmorBarrage,
                EarthTechniqueId.MeteorFinish
            };

            foreach (EarthTechniqueId technique in techniques)
            {
                int slot = (int)EarthHumanoidMotionResolver.Resolve(technique);
                begin.Invoke(pose, new object[]
                {
                    EarthTechniqueKind.Wall, technique, pose.PresentationTick,
                    actor.Presentation.transform.position + actor.Presentation.transform.forward * 3f,
                    80f, 4f
                });
                float desiredPeak = 0f;
                float smallestOtherSumAtPeak = float.PositiveInfinity;
                float minimumHeadHeight = float.PositiveInfinity;
                float maximumHeadPitch = 0f;
                bool sawCastState = false;
                double deadline = Time.realtimeSinceStartupAsDouble + 1.25d;
                while (Time.realtimeSinceStartupAsDouble < deadline)
                {
                    yield return _frame;
                    if (actor.Presentation.CurrentAuthoredAction != EarthAuthoredActionId.MagicCast)
                        continue;
                    EarthAnimationPoseSample sample = actor.Probe.Latest;
                    _samples.Add(sample);
                    minimumHeadHeight = Mathf.Min(minimumHeadHeight, sample.headHeight);
                    maximumHeadPitch = Mathf.Max(maximumHeadPitch, Mathf.Abs(sample.headPitchDegrees));
                    AnimatorStateInfo state = driver.GetCurrentAnimatorStateInfo(magicLayer);
                    AnimatorStateInfo next = driver.GetNextAnimatorStateInfo(magicLayer);
                    sawCastState |= state.fullPathHash == castHash || next.fullPathHash == castHash;
                    float desired = driver.GetFloat(Animator.StringToHash($"EarthPose{slot:00}"));
                    float other = 0f;
                    for (int candidate = 1; candidate <= 11; candidate++)
                        if (candidate != slot)
                            other += driver.GetFloat(Animator.StringToHash($"EarthPose{candidate:00}"));
                    if (desired > desiredPeak)
                    {
                        desiredPeak = desired;
                        smallestOtherSumAtPeak = other;
                    }
                }

                Assert.That(sawCastState, Is.True, $"{technique} never entered the saved cast state.");
                Assert.That(desiredPeak, Is.GreaterThan(0.82f),
                    $"{technique} never selected semantic slot {slot} strongly enough.");
                Assert.That(smallestOtherSumAtPeak, Is.LessThan(0.12f),
                    $"{technique} kept another semantic clip mixed into slot {slot}.");
                Assert.That(minimumHeadHeight, Is.GreaterThan(0.22f),
                    $"{technique} compressed the head into the torso.");
                Assert.That(maximumHeadPitch, Is.LessThan(65f),
                    $"{technique} turned the head toward the vertical axis.");
                yield return new WaitForSeconds(0.45f);
            }
        }

        [UnityTest]
        public IEnumerator WalkStopKeepsKneesFiniteAndAvoidsAOneFrameLegSnap()
        {
            foreach (Actor actor in _actors) actor.Input.Move = new float2(0f, 1f);
            yield return new WaitForSeconds(0.75f);

            float[] previousLeft = new float[_actors.Count];
            float[] previousRight = new float[_actors.Count];
            for (int index = 0; index < _actors.Count; index++)
            {
                EarthFootContactController feet = _actors[index].Presentation.FootContactController;
                previousLeft[index] = feet.LeftKneeAngleDegrees;
                previousRight[index] = feet.RightKneeAngleDegrees;
                _actors[index].Input.Move = float2.zero;
            }

            float maximumKneeStep = 0f;
            double deadline = Time.realtimeSinceStartupAsDouble + 1.1d;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return _frame;
                for (int index = 0; index < _actors.Count; index++)
                {
                    Actor actor = _actors[index];
                    EarthFootContactController feet = actor.Presentation.FootContactController;
                    float left = feet.LeftKneeAngleDegrees;
                    float right = feet.RightKneeAngleDegrees;
                    Assert.That(float.IsFinite(left) && float.IsFinite(right), Is.True);
                    // Vector3.Angle is unsigned and capped at 180 degrees, so a
                    // 178-degree per-frame ceiling cannot distinguish a natural
                    // heel-strike extension from backwards bending. Continuity is
                    // enforced here; the settled pose is checked below.
                    Assert.That(left, Is.InRange(.5f, 180f), $"{actor.Presentation.name}: invalid left-knee chain.");
                    Assert.That(right, Is.InRange(.5f, 180f), $"{actor.Presentation.name}: invalid right-knee chain.");
                    maximumKneeStep = Mathf.Max(maximumKneeStep,
                        Mathf.Abs(left - previousLeft[index]), Mathf.Abs(right - previousRight[index]));
                    previousLeft[index] = left;
                    previousRight[index] = right;
                    EarthAnimationPoseSample sample = actor.Probe.Latest;
                    _samples.Add(sample);
                    if (sample.leftContactWeight > 0.8f)
                        Assert.That(sample.leftFootError, Is.LessThan(0.18f));
                    if (sample.rightContactWeight > 0.8f)
                        Assert.That(sample.rightFootError, Is.LessThan(0.18f));
                }
            }
            Assert.That(maximumKneeStep, Is.LessThan(35f),
                "A walk-stop transition snapped a knee in one rendered frame.");
            foreach (Actor actor in _actors)
            {
                Assert.That(actor.Presentation.FilteredSpeed, Is.LessThan(0.35f),
                    $"{actor.Presentation.name}: locomotion did not settle after input release.");
                Assert.That(actor.Bridge.AppliedEammMasterWeight, Is.GreaterThan(0.95f),
                    $"{actor.Presentation.name}: the knee fix disabled the production EAMM base pose.");
                Assert.That(actor.Bridge.UsesAuthoredIdleKnees, Is.True,
                    $"{actor.Presentation.name}: the settled idle query did not return knee ownership to the Humanoid controller.");
                EarthFootContactController feet = actor.Presentation.FootContactController;
                Assert.That(feet.LeftKneeAngleDegrees, Is.InRange(3f, 178f),
                    $"{actor.Presentation.name}: left knee remained straight/folded after stopping.");
                Assert.That(feet.RightKneeAngleDegrees, Is.InRange(3f, 178f),
                    $"{actor.Presentation.name}: right knee remained straight/folded after stopping.");
            }
        }
    }
}
