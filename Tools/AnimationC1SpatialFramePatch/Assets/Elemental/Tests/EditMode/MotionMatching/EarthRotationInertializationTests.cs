using Elemental.Presentation.MotionMatching;
using MotionMatching;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode.MotionMatching
{
    public sealed class EarthRotationInertializationTests
    {
        private const float HalfLife = 0.075f;

        [Test]
        public void StationarySwitchKeepsTransitionSampleAndSettlesToTarget()
        {
            EarthRotationInertializationState state = default;
            quaternion before = Step(ref state, Rotation(0f), 1f / 60f);

            quaternion switched = Transition(
                ref state,
                Rotation(95f),
                1f / 60f,
                out float3 switchVelocity);

            Assert.That(AngleDegrees(before, switched), Is.LessThan(0.0001f));
            Assert.That(math.length(switchVelocity), Is.LessThan(0.0001f));

            quaternion output = switched;
            for (int index = 0; index < 120; index++)
                output = Step(ref state, Rotation(95f), 1f / 60f);

            Assert.That(AngleDegrees(output, Rotation(95f)), Is.LessThan(0.05f));
            Assert.That(math.length(state.PreviousOutputAngularVelocity), Is.LessThan(0.01f));
        }

        [Test]
        public void MovingSwitchPreservesOutgoingPoseAndAngularVelocity()
        {
            EarthRotationInertializationState state = default;
            const float delta = 1f / 60f;
            quaternion output = Step(ref state, Rotation(0f), delta);
            for (int index = 1; index <= 18; index++)
                output = Step(ref state, Rotation(index), delta);

            float3 outgoingVelocity = state.PreviousOutputAngularVelocity;
            quaternion outgoingPose = output;
            quaternion switched = Transition(
                ref state,
                Rotation(130f),
                delta,
                out float3 switchVelocity);

            float3 renderedSwitchVelocity = EarthRotationInertialization.MeasureAngularVelocity(
                outgoingPose,
                switched,
                delta);
            Assert.That(math.distance(renderedSwitchVelocity, outgoingVelocity),
                Is.LessThan(0.001f),
                "The returned transition pose must carry the outgoing derivative; metadata alone is not C1.");
            Assert.That(math.distance(switchVelocity, renderedSwitchVelocity), Is.LessThan(0.001f));

            quaternion next = Step(ref state, Rotation(131.5f), delta);
            // A 112-degree source-pose discontinuity is reduced to a bounded
            // first advancing sample while retaining the measured outgoing
            // velocity at the actual transition boundary above.
            Assert.That(AngleDegrees(switched, next), Is.LessThan(6f));
            Assert.That(math.all(math.isfinite(state.PreviousOutputAngularVelocity)), Is.True);
        }

        [Test]
        public void InterruptedTransitionStartsFromVisibleMotionInsteadOfOldOffset()
        {
            EarthRotationInertializationState state = default;
            const float delta = 1f / 60f;
            Step(ref state, Rotation(0f), delta);
            Transition(ref state, Rotation(100f), delta, out _);
            for (int index = 0; index < 5; index++)
                Step(ref state, Rotation(100f + index * 0.5f), delta);

            quaternion visibleBeforeInterrupt = state.PreviousOutput;
            float3 velocityBeforeInterrupt = state.PreviousOutputAngularVelocity;
            quaternion interrupted = Transition(
                ref state,
                Rotation(-80f),
                delta,
                out float3 interruptedVelocity);

            float3 renderedInterruptVelocity = EarthRotationInertialization.MeasureAngularVelocity(
                visibleBeforeInterrupt,
                interrupted,
                delta);
            Assert.That(math.distance(velocityBeforeInterrupt, renderedInterruptVelocity),
                Is.LessThan(0.001f));
            Assert.That(math.distance(interruptedVelocity, renderedInterruptVelocity),
                Is.LessThan(0.001f));

            quaternion output = interrupted;
            for (int index = 0; index < 150; index++)
                output = Step(ref state, Rotation(-80f), delta);
            Assert.That(AngleDegrees(output, Rotation(-80f)), Is.LessThan(0.05f));
        }

        [Test]
        public void TimeBasedDecayConvergesAcrossThirtySixtyAndOneTwentyHertz()
        {
            Sample at30 = SimulateMovingSwitch(30);
            Sample at60 = SimulateMovingSwitch(60);
            Sample at120 = SimulateMovingSwitch(120);

            Assert.That(AngleDegrees(at30.Rotation, at120.Rotation), Is.LessThan(0.35f));
            Assert.That(AngleDegrees(at60.Rotation, at120.Rotation), Is.LessThan(0.2f));
            Assert.That(math.degrees(math.distance(at30.AngularVelocity, at120.AngularVelocity)),
                Is.LessThan(0.75f));
            Assert.That(math.degrees(math.distance(at60.AngularVelocity, at120.AngularVelocity)),
                Is.LessThan(0.5f));
            Assert.That(math.degrees(math.distance(
                    at30.RenderedBoundaryVelocity,
                    at120.RenderedBoundaryVelocity)),
                Is.LessThan(0.02f),
                "The returned transition poses must preserve the same physical derivative at 30 and 120 Hz.");
            Assert.That(math.degrees(math.distance(
                    at60.RenderedBoundaryVelocity,
                    at120.RenderedBoundaryVelocity)),
                Is.LessThan(0.02f));
        }

        [Test]
        public void PlantedContactBypassClearsGenericOffsetAndTracksExactTarget()
        {
            EarthRotationInertializationState state = default;
            const float delta = 1f / 60f;
            Step(ref state, Rotation(0f), delta);
            Transition(ref state, Rotation(90f), delta, out _);
            Step(ref state, Rotation(90f), delta);

            quaternion planted = EarthRotationInertialization.Step(
                ref state,
                Rotation(24f),
                HalfLife,
                delta,
                transition: true,
                bypass: true,
                out float3 plantedVelocity);
            Assert.That(AngleDegrees(planted, Rotation(24f)), Is.LessThan(0.0001f));
            Assert.That(AngleDegrees(state.OffsetRotation, quaternion.identity),
                Is.LessThan(0.0001f));
            Assert.That(math.length(state.OffsetAngularVelocity), Is.LessThan(0.0001f));
            Assert.That(math.length(plantedVelocity), Is.LessThan(0.0001f),
                "A final-contact snap is authority transfer, not outgoing authored angular velocity.");

            quaternion following = Step(ref state, Rotation(26f), delta);
            Assert.That(AngleDegrees(following, Rotation(26f)), Is.LessThan(0.0001f));
        }

        [Test]
        public void NonCommutingAxesComposeInTheSameSpatialVelocityFrame()
        {
            quaternion offset = quaternion.EulerXYZ(math.radians(
                new float3(37f, -52f, 81f)));
            quaternion target = quaternion.EulerXYZ(math.radians(
                new float3(-28f, 63f, 19f)));
            float3 offsetVelocity = new float3(-.7f, .45f, 1.1f);
            float3 targetVelocity = new float3(.8f, -1.25f, .35f);
            float3 composed = EarthRotationInertialization.ComposeSpatialAngularVelocity(
                offset,
                offsetVelocity,
                targetVelocity);

            const float derivativeStep = .0001f;
            quaternion output = math.mul(offset, target);
            quaternion nextOffset = IntegrateSpatial(offset, offsetVelocity, derivativeStep);
            quaternion nextTarget = IntegrateSpatial(target, targetVelocity, derivativeStep);
            quaternion nextOutput = math.mul(nextOffset, nextTarget);
            float3 measured = EarthRotationInertialization.MeasureAngularVelocity(
                output,
                nextOutput,
                derivativeStep);

            Assert.That(math.distance(composed, measured), Is.LessThan(.003f),
                "C1 reported velocity must equal the derivative of the actual non-commuting output pose.");
            Assert.That(math.distance(composed, offsetVelocity + targetVelocity), Is.GreaterThan(.25f),
                "The regression must exercise rotated axes; a single-axis case hides the frame mismatch.");

            float3 reconstructedOffsetVelocity = composed - math.rotate(offset, targetVelocity);
            Assert.That(math.distance(reconstructedOffsetVelocity, offsetVelocity), Is.LessThan(.0001f));
        }

        private static Sample SimulateMovingSwitch(int fps)
        {
            EarthRotationInertializationState state = default;
            float delta = 1f / fps;
            int totalSteps = fps * 2;
            int switchStep = fps / 5;
            quaternion output = quaternion.identity;
            float3 renderedBoundaryVelocity = float3.zero;
            for (int step = 0; step <= totalSteps; step++)
            {
                float time = step * delta;
                bool afterSwitch = step >= switchStep;
                float targetDegrees = afterSwitch
                    ? 105f + (time - 0.2f) * 42f
                    : time * 60f;
                quaternion previous = output;
                output = EarthRotationInertialization.Step(
                    ref state,
                    Rotation(targetDegrees),
                    HalfLife,
                    delta,
                    step == switchStep,
                    bypass: false,
                    out _);
                if (step == switchStep)
                    renderedBoundaryVelocity = EarthRotationInertialization.MeasureAngularVelocity(
                        previous,
                        output,
                        delta);
            }

            return new Sample(
                output,
                state.PreviousOutputAngularVelocity,
                renderedBoundaryVelocity);
        }

        private static quaternion Transition(
            ref EarthRotationInertializationState state,
            quaternion target,
            float deltaTime,
            out float3 velocity) =>
            EarthRotationInertialization.Step(
                ref state,
                target,
                HalfLife,
                deltaTime,
                transition: true,
                bypass: false,
                out velocity);

        private static quaternion Step(
            ref EarthRotationInertializationState state,
            quaternion target,
            float deltaTime,
            bool transition = false)
        {
            return EarthRotationInertialization.Step(
                ref state,
                target,
                HalfLife,
                deltaTime,
                transition,
                bypass: false,
                out _);
        }

        private static quaternion Rotation(float degrees) =>
            quaternion.AxisAngle(new float3(0f, 1f, 0f), math.radians(degrees));

        private static quaternion IntegrateSpatial(
            quaternion rotation,
            float3 angularVelocity,
            float deltaTime)
        {
            float speed = math.length(angularVelocity);
            if (speed < .000001f) return rotation;
            return math.normalize(math.mul(
                quaternion.AxisAngle(angularVelocity / speed, speed * deltaTime),
                rotation));
        }

        private static float AngleDegrees(quaternion from, quaternion to)
        {
            quaternion delta = MathExtensions.Abs(math.mul(to, math.inverse(from)));
            return math.degrees(math.length(MathExtensions.QuaternionToScaledAngleAxis(delta)));
        }

        private readonly struct Sample
        {
            public Sample(
                quaternion rotation,
                float3 angularVelocity,
                float3 renderedBoundaryVelocity)
            {
                Rotation = rotation;
                AngularVelocity = angularVelocity;
                RenderedBoundaryVelocity = renderedBoundaryVelocity;
            }

            public quaternion Rotation { get; }
            public float3 AngularVelocity { get; }
            public float3 RenderedBoundaryVelocity { get; }
        }
    }
}
