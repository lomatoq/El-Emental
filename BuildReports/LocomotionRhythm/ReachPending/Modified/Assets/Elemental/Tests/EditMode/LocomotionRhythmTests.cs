using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using Elemental.Authoring.Editor.MotionMatching;

namespace Elemental.Tests.EditMode
{
    public sealed class LocomotionRhythmTests
    {
        [Test]
        public void ReloadedInitializationFlagCannotAuthorizeMissingNativeBuffers()
        {
            var go = new UnityEngine.GameObject("reload-state probe");
            try
            {
                var source = go.AddComponent<global::MotionMatching.MotionMatchingController>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(global::MotionMatching.MotionMatchingController).GetField("IsInitialized", flags).SetValue(source, true);
                Assert.That(source.RuntimeInitialized, Is.False);
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                    "[Motion Matching] Runtime buffers are unavailable after reload. Search suspended; re-enter the scene to initialize a fresh source.");
                var update = typeof(global::MotionMatching.MotionMatchingController).GetMethod("OnCharacterControllerUpdated", flags);
                update.Invoke(source, new object[] { 1f / 60f });
                update.Invoke(source, new object[] { 1f / 60f });
                Assert.That(source.UpdateAndGetFeatureWeights().IsCreated, Is.False);
                Assert.That(source.HasValidSearchResult, Is.False);
                Assert.That(source.InitializationStatus, Is.EqualTo("runtime-state-lost-after-reload"));
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void LowWalkingReturnStrokeReleasesEvenWhenClipLiftExceedsFlatShuffleThreshold()
        {
            float3[] samples = { new float3(-.1f, 0f, 0f), new float3(0f, .02f, 0f),
                new float3(.1f, .08f, 0f), new float3(0f, .02f, 0f), new float3(-.1f, 0f, 0f) };
            float3 travel = new float3(-1f, 0f, 0f);
            Assert.That(MotionLibraryBuilder.DetectFootContact(samples, 1, .1f, true, travel), Is.True);
            Assert.That(MotionLibraryBuilder.DetectFootContact(samples, 3, .1f, true, travel), Is.False,
                "A low forward recovery is not ground support, even when another sample lifts8cm.");
        }

        [Test]
        public void MovingPlantCapturesBeforeItsAnchorDriftsOutsideFootReach()
        {
            float weight = 0f, peakError = 0f;
            for (int frame = 1; frame <= 12; frame++)
            {
                float previous = weight;
                weight = EarthFootIkWeightBlend.StepContact(weight, 1f, 1f / 60f,
                    EarthFootIkWeightBlend.LocomotionCaptureResponseSeconds, .02f,
                    EarthFootIkWeightBlend.MaximumLocomotionCaptureFrameStep);
                Assert.That(weight - previous, Is.LessThanOrEqualTo(.17001f));
                float animatedDrift = 3.2f * frame / 60f;
                peakError = math.max(peakError, animatedDrift * (1f - EarthFootIkWeightBlend.ResolveSubmittedGoalWeight(weight)));
            }
            Assert.That(weight, Is.EqualTo(1f));
            Assert.That(peakError, Is.LessThan(.09f));
            Assert.That(EarthFootIkWeightBlend.StepContact(0f, 1f, 1f / 60f), Is.LessThan(.05f),
                "The established gentle idle capture remains unchanged.");
        }

        [TestCase(0f)] [TestCase(30f)] [TestCase(50f)]
        public void GroundTractionUsesOneSupportPlaneAndPreservesTenCycleMeanOnSlopes(float gravityTilt)
        {
            const float dt = .02f, speed = 6f, cycle = 3f;
            float3 up = new float3(0f, 1f, 0f), velocity = new float3(speed, 0f, 0f);
            float3 gravity = math.mul(quaternion.RotateZ(math.radians(gravityTilt)), new float3(0f, -20f, 0f));
            float distance = 0f, seconds = 0f, phase = 0f;
            while (distance < cycle * 10f)
            {
                float3 desired = new float3(speed * LocomotionRhythmSolver.Pulse(phase, true), 0f, 0f);
                float3 acceleration = LocomotionRhythmSolver.SupportTractionAcceleration(velocity, desired, up, gravity, 45f, dt);
                Assert.That(math.abs(math.dot(acceleration, up)), Is.LessThan(.00001f), "Tangential movement must not inject fake normal lift.");
                Assert.That(math.length(acceleration), Is.LessThanOrEqualTo(45.001f));
                velocity += (acceleration + gravity) * dt;
                velocity -= up * math.dot(velocity, up); // physical support normal constraint
                distance += math.length(velocity) * dt; seconds += dt;
                phase = LocomotionRhythmSolver.AdvancePhase(phase, math.length(velocity) * dt, cycle);
            }
            Assert.That(distance / seconds / speed, Is.EqualTo(1f).Within(.01f));
        }

        [Test]
        public void FootTrajectoryRepairIsBoundedAndNeverMasksHipsOrKneeSteps()
        {
            var target = UnityEngine.Quaternion.Euler(110f, 15f, 0f);
            var output = Elemental.Presentation.MotionMatching.EAMMBasePoseBridge.LimitFootTrajectoryRotation(
                UnityEngine.HumanBodyBones.LeftFoot, UnityEngine.Quaternion.identity, target, 1f / 60f, out bool corrected);
            Assert.That(corrected, Is.True);
            Assert.That(UnityEngine.Quaternion.Angle(UnityEngine.Quaternion.identity, output), Is.EqualTo(20f).Within(.001f));
            foreach (var bone in new[] { UnityEngine.HumanBodyBones.Hips, UnityEngine.HumanBodyBones.LeftLowerLeg })
            {
                output = Elemental.Presentation.MotionMatching.EAMMBasePoseBridge.LimitFootTrajectoryRotation(
                    bone, UnityEngine.Quaternion.identity, target, 1f / 60f, out corrected);
                Assert.That(corrected, Is.False);
                Assert.That(UnityEngine.Quaternion.Angle(output, target), Is.LessThan(.001f));
            }
        }

        [Test]
        public void SearchTransitionPreservesActualFractionalOutputEvenDuringAnotherBlend()
        {
            var skeleton = new global::MotionMatching.Skeleton();
            skeleton.AddJoint(new global::MotionMatching.Skeleton.Joint());
            skeleton.AddJoint(new global::MotionMatching.Skeleton.Joint());
            var inertial = new global::MotionMatching.Inertialization(skeleton);
            var pose = new global::MotionMatching.PoseVector(new float3[2],
                new[] { quaternion.identity, quaternion.RotateX(.73f) }, new float3[2], new float3[2], false, false);
            pose.JointLocalPositions[1] = new float3(.1f, 1.2f, .3f);
            inertial.Update(pose, .1f, .016f);
            for (int transition = 0; transition < 3; transition++)
            {
                quaternion displayed = inertial.InertializedRotations[1];
                float3 displayedHips = inertial.InertializedHips;
                pose.JointLocalRotations[1] = quaternion.EulerXYZ(.2f + transition, -.8f, .3f);
                pose.JointLocalPositions[1] += new float3(.1f, -.12f, .04f);
                inertial.PoseTransitionFromOutput(pose);
                inertial.Update(pose, .1f, 0f);
                Assert.That(math.abs(math.dot(displayed.value, inertial.InertializedRotations[1].value)), Is.EqualTo(1f).Within(.00001f));
                Assert.That(math.distance(displayedHips, inertial.InertializedHips), Is.LessThan(.00001f));
                inertial.Update(pose, .1f, .006f);
            }
        }

        [Test]
        public void FlatShuffleUsesMeasuredReturnStrokeInsteadOfInventedFootLift()
        {
            float3[] samples = { new float3(-.1f, 0f, 0f), float3.zero,
                new float3(.1f, 0f, 0f), float3.zero, new float3(-.1f, 0f, 0f) };
            float3 travel = new float3(-1f, 0f, 0f);
            Assert.That(MotionLibraryBuilder.DetectFootContact(samples, 1, .1f, true, travel), Is.True,
                "A low foot moving against travel is the planted shuffle stroke.");
            Assert.That(MotionLibraryBuilder.DetectFootContact(samples, 3, .1f, true, travel), Is.False,
                "The low forward return stroke still needs to release contact.");
            Assert.That(MotionLibraryBuilder.DetectFootContact(new float3[5], 2, .1f, true, travel), Is.True,
                "A stationary idle foot must not receive fabricated swing intervals.");
        }

        [Test]
        public void PulseIsBoundedAndMeanOneAcrossCompleteStrides()
        {
            float sum = 0f;
            for (int i = 0; i < 1000; i++)
            {
                float pulse = LocomotionRhythmSolver.Pulse(i / 1000f, true);
                Assert.That(pulse, Is.InRange(.95f, 1.05f)); sum += pulse;
            }
            Assert.That(sum / 1000f, Is.EqualTo(1f).Within(.00001f));
            Assert.That(LocomotionRhythmSolver.Pulse(.125f, false), Is.EqualTo(1f));
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void DistancePhaseDoesNotDependOnRenderRate(int fps)
        {
            float phase = .1f;
            for (int i = 0; i < fps; i++) phase = LocomotionRhythmSolver.AdvancePhase(phase, 3f / fps, 2f);
            Assert.That(phase, Is.EqualTo(.6f).Within(.00002f));
        }

        [TestCase(.7f)] [TestCase(2f)] [TestCase(3.5f)]
        public void TenDistanceCyclesKeepMeanTravelSpeedWithinOnePercent(float cycleDistance)
        {
            const float dt = .0005f, nominal = 5f;
            float distance = 0f, seconds = 0f;
            while (distance < cycleDistance * 10f)
            {
                distance += nominal * LocomotionRhythmSolver.Pulse(math.frac(distance / cycleDistance), true) * dt;
                seconds += dt;
            }
            Assert.That(distance / seconds / nominal, Is.EqualTo(1f).Within(.01f));
        }

        [TestCase(.1f, 1f, 1f)] [TestCase(.4f, .8f, .9f)]
        [TestCase(1f, 1f, 1f)] [TestCase(1.32f, 1.25f, 1.056f)] [TestCase(4f, 1.25f, 1.1f)]
        public void RateThenResidualStrideStayInsideApprovedEnvelope(float ratio, float rate, float stride)
        {
            var value = LocomotionRhythmSolver.Resolve(ratio, 1f, true);
            Assert.That(value.PlaybackRate, Is.EqualTo(rate).Within(.00001f));
            Assert.That(value.StrideScale, Is.EqualTo(stride).Within(.00001f));
        }

        [Test]
        public void MissingMetadataOrNonLocomotionCannotSpeedAnAction()
        {
            Assert.That(LocomotionRhythmSolver.Resolve(5f, 0f, true).PlaybackRate, Is.EqualTo(1f));
            Assert.That(LocomotionRhythmSolver.Resolve(5f, 2f, false).PlaybackRate, Is.EqualTo(1f));
            Assert.That(LocomotionRhythmSolver.Resolve(float.NaN, 2f, true).StrideScale, Is.EqualTo(1f));
        }

        [Test]
        public void IndependentContactEdgesRecoverMultipleUnevenCyclesInOneClip()
        {
            bool[] left = { true, true, false, false, true, false, false, false, false, false };
            bool[] right = { false, false, true, false, false, false, true, true, false, false };
            var a = new float[10]; var b = new float[10];
            LocomotionRhythmSolver.FillContactPhases(left, a);
            LocomotionRhythmSolver.FillContactPhases(right, b);
            Assert.That(a[0], Is.Zero); Assert.That(a[4], Is.Zero);
            Assert.That(a[2], Is.EqualTo(.5f)); Assert.That(a[7], Is.EqualTo(.5f));
            Assert.That(b[2], Is.Zero); Assert.That(b[6], Is.Zero);
            Assert.That(b[0], Is.EqualTo(4f / 6f).Within(.0001f));
            Assert.That(math.abs(b[2] - math.frac(a[2] + .5f)), Is.LessThan(.001f));
            Assert.That(b[7], Is.Not.EqualTo(math.frac(a[7] + .5f)));
        }
    }
}
