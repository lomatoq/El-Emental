using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthResponsiveHandTargetSolverTests
    {
        [Test]
        public void RearTarget_RemainsInReachableFrontCone()
        {
            float3 aim = EarthResponsiveHandTargetSolver.ConstrainAim(new float3(-2f, 0.2f, -4f));
            float3 directlyBehind = EarthResponsiveHandTargetSolver.ConstrainAim(new float3(0f, 0f, -1f));

            Assert.That(aim.z, Is.GreaterThan(0f));
            Assert.That(math.degrees(math.abs(math.atan2(aim.x, aim.z))),
                Is.LessThanOrEqualTo(EarthResponsiveHandTargetSolver.MaximumYawDegrees + 0.001f));
            Assert.That(math.length(aim), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(directlyBehind.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(directlyBehind.z, Is.GreaterThan(0.99f));
        }

        [Test]
        public void MovingFocus_HasExplicitAngularAndReachSpeedBounds()
        {
            var state = default(EarthResponsiveHandTargetState);
            EarthResponsiveHandTargetSolver.Step(
                ref state, new float3(0f, 0f, 1f), 0.30f, 0.10f, true, 1f / 60f);

            float3 previousAim = state.LocalAim;
            float previousReach = state.ReachMeters;
            EarthResponsiveHandTargetSample sample = EarthResponsiveHandTargetSolver.Step(
                ref state, new float3(1f, 0f, 0f), 0.68f, 0.24f, true, 1f / 60f);

            float angle = math.degrees(math.acos(math.clamp(math.dot(previousAim, sample.LocalAim), -1f, 1f)));
            Assert.That(angle, Is.LessThanOrEqualTo(
                EarthResponsiveHandTargetSolver.MaximumAimDegreesPerSecond / 60f + 0.001f));
            Assert.That(sample.ReachMeters - previousReach, Is.LessThanOrEqualTo(
                EarthResponsiveHandTargetSolver.MaximumReachMetersPerSecond / 60f + 0.0001f));
        }

        [Test]
        public void RootTurn_DoesNotCreateWorldSpaceTargetLag()
        {
            EarthResponsiveHandTargetState state = CreateForwardState();
            float3 previousWorldAim = state.LocalAim;
            quaternion rootTurn = quaternion.RotateY(math.radians(60f));
            float3 fixedWorldFocusInTurnedBody = math.mul(math.inverse(rootTurn), previousWorldAim);

            EarthResponsiveHandTargetSample sample = EarthResponsiveHandTargetSolver.Step(
                ref state, fixedWorldFocusInTurnedBody, 0.52f, 0.16f, true, 1f / 60f);
            float3 nextWorldAim = math.mul(rootTurn, sample.LocalAim);
            float localStep = AngleDegrees(new float3(0f, 0f, 1f), sample.LocalAim);
            float worldStep = AngleDegrees(previousWorldAim, nextWorldAim);

            Assert.That(localStep, Is.LessThanOrEqualTo(
                EarthResponsiveHandTargetSolver.MaximumAimDegreesPerSecond / 60f + 0.001f));
            Assert.That(worldStep, Is.GreaterThan(50f),
                "The target stayed fixed on the old world focus instead of following the turning body.");
        }

        [Test]
        public void Release_HoldsLastBodyRelativeGoalInsteadOfChasingFallback()
        {
            var state = default(EarthResponsiveHandTargetState);
            EarthResponsiveHandTargetSample tracked = EarthResponsiveHandTargetSolver.Step(
                ref state, new float3(0.7f, 0.2f, 1f), 0.57f, 0.19f, true, 1f / 60f);
            EarthResponsiveHandTargetSample released = EarthResponsiveHandTargetSolver.Step(
                ref state, new float3(-1f, 1f, -1f), 0.25f, 0.08f, false, 0.05f);

            Assert.That(released.LocalAim, Is.EqualTo(tracked.LocalAim));
            Assert.That(released.ReachMeters, Is.EqualTo(tracked.ReachMeters));
            Assert.That(released.HandSpreadMeters, Is.EqualTo(tracked.HandSpreadMeters));
        }

        [Test]
        public void InvalidInput_AlwaysReturnsFiniteBoundedSample()
        {
            var state = default(EarthResponsiveHandTargetState);
            EarthResponsiveHandTargetSample sample = EarthResponsiveHandTargetSolver.Step(
                ref state,
                new float3(float.NaN, float.PositiveInfinity, float.NegativeInfinity),
                float.NaN,
                float.PositiveInfinity,
                true,
                float.NaN);

            Assert.That(math.all(math.isfinite(sample.LocalAim)), Is.True);
            Assert.That(math.length(sample.LocalAim), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(sample.ReachMeters, Is.InRange(
                EarthResponsiveHandTargetSolver.MinimumReachMeters,
                EarthResponsiveHandTargetSolver.MaximumReachMeters));
            Assert.That(sample.HandSpreadMeters, Is.InRange(
                EarthResponsiveHandTargetSolver.MinimumSpreadMeters,
                EarthResponsiveHandTargetSolver.MaximumSpreadMeters));
        }

        [Test]
        public void EqualElapsedTime_ProducesEquivalentResponseAtThirtyAndOneTwentyHz()
        {
            EarthResponsiveHandTargetState thirty = CreateForwardState();
            EarthResponsiveHandTargetState oneTwenty = CreateForwardState();

            StepFor(ref thirty, 30, 0.20f);
            StepFor(ref oneTwenty, 120, 0.20f);

            Assert.That(math.distance(thirty.LocalAim, oneTwenty.LocalAim), Is.LessThan(0.0002f));
            Assert.That(thirty.ReachMeters, Is.EqualTo(oneTwenty.ReachMeters).Within(0.0002f));
            Assert.That(thirty.HandSpreadMeters,
                Is.EqualTo(oneTwenty.HandSpreadMeters).Within(0.0002f));
        }

        [Test]
        public void TorsoAim_IsGentleBoundedAndDisabledBeforeConstraintOwnership()
        {
            float3 aim = EarthResponsiveHandTargetSolver.ConstrainAim(new float3(1f, 0f, 0f));

            Assert.That(EarthResponsiveHandTargetSolver.ResolveTorsoYawDegrees(aim, 0f), Is.EqualTo(0f));
            Assert.That(EarthResponsiveHandTargetSolver.ResolveTorsoYawDegrees(aim, 1f),
                Is.InRange(0.1f, EarthResponsiveHandTargetSolver.MaximumTorsoYawDegrees));
            Assert.That(math.abs(EarthResponsiveHandTargetSolver.ResolveTorsoYawDegrees(
                    new float3(-1f, 0f, 0f), 1f)),
                Is.LessThanOrEqualTo(EarthResponsiveHandTargetSolver.MaximumTorsoYawDegrees));
        }

        private static EarthResponsiveHandTargetState CreateForwardState()
        {
            var state = default(EarthResponsiveHandTargetState);
            EarthResponsiveHandTargetSolver.Step(
                ref state, new float3(0f, 0f, 1f), 0.30f, 0.10f, true, 1f / 60f);
            return state;
        }

        private static void StepFor(
            ref EarthResponsiveHandTargetState state,
            int framesPerSecond,
            float seconds)
        {
            int frameCount = (int)math.round(framesPerSecond * seconds);
            float deltaTime = 1f / framesPerSecond;
            for (int index = 0; index < frameCount; index++)
                EarthResponsiveHandTargetSolver.Step(
                    ref state, new float3(1f, 0f, 0f), 0.68f, 0.24f, true, deltaTime);
        }

        private static float AngleDegrees(float3 from, float3 to) =>
            math.degrees(math.acos(math.clamp(math.dot(from, to), -1f, 1f)));
    }
}
