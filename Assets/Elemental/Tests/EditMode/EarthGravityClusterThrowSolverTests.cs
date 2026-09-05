using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthGravityClusterThrowSolverTests
    {
        [Test]
        public void RadialBlastUsesActualOppositeOffsetsInsteadOfForwardFan()
        {
            EarthGravityClusterThrowTuning tuning = EarthGravityClusterThrowTuning.Default;
            float3 up = new float3(0f, 1f, 0f);
            float3 aim = new float3(0f, 0f, 1f);
            var left = EarthGravityClusterThrowSolver.SolveRadial(1u, 0, 2, 10f,
                new float3(-1f, 0f, 0f), aim, up, 1f, in tuning);
            var right = EarthGravityClusterThrowSolver.SolveRadial(2u, 1, 2, 200f,
                new float3(1f, 0f, 0f), aim, up, 1f, in tuning);
            Assert.That(left.Velocity.x, Is.LessThan(-30f));
            Assert.That(right.Velocity.x, Is.GreaterThan(30f));
            Assert.That(left.Speed, Is.EqualTo(31f));
            Assert.That(right.Speed, Is.EqualTo(31f));
            Assert.That(left.Velocity.y, Is.GreaterThan(0f));
        }

        [Test]
        public void CoincidentRadialBlastIsFiniteAndDeterministic()
        {
            EarthGravityClusterThrowTuning tuning = EarthGravityClusterThrowTuning.Default;
            var first = EarthGravityClusterThrowSolver.SolveRadial(31u, 0, 1, 10f,
                float3.zero, float3.zero, float3.zero, 0f, in tuning);
            var repeated = EarthGravityClusterThrowSolver.SolveRadial(31u, 0, 1, 10f,
                float3.zero, float3.zero, float3.zero, 0f, in tuning);
            Assert.That(math.all(math.isfinite(first.Velocity)), Is.True);
            Assert.That(first.Velocity, Is.EqualTo(repeated.Velocity));
            Assert.That(first.Speed, Is.EqualTo(19f));
        }

        [Test]
        public void DirectThrow_IsCoherentAndHeavyPiecesStayNearCentre()
        {
            EarthGravityClusterThrowTuning tuning = EarthGravityClusterThrowTuning.Default;
            EarthGravityClusterLaunchSample light = EarthGravityClusterThrowSolver.Solve(
                11u, 7, 16, 8f, new float3(0f, 0f, 1f), new float3(0f, 1f, 0f),
                EarthGravityClusterReleaseMode.Direct, 0f, in tuning);
            EarthGravityClusterLaunchSample heavy = EarthGravityClusterThrowSolver.Solve(
                11u, 7, 16, 180f, new float3(0f, 0f, 1f), new float3(0f, 1f, 0f),
                EarthGravityClusterReleaseMode.Direct, 0f, in tuning);

            Assert.That(light.Speed, Is.GreaterThan(heavy.Speed));
            Assert.That(math.dot(math.normalize(light.Velocity), new float3(0f, 0f, 1f)), Is.GreaterThan(0.95f));
            Assert.That(math.dot(math.normalize(heavy.Velocity), new float3(0f, 0f, 1f)),
                Is.GreaterThan(math.dot(math.normalize(light.Velocity), new float3(0f, 0f, 1f))));
        }

        [Test]
        public void CompressionCharge_IsNonLinearAndShrinksFormation()
        {
            float early = EarthGravityClusterThrowSolver.Charge01(0.25f, 1f);
            float late = EarthGravityClusterThrowSolver.Charge01(0.75f, 1f);
            Assert.That(early, Is.GreaterThan(0.25f));
            Assert.That(late - early, Is.LessThan(0.5f));
            Assert.That(EarthGravityClusterThrowSolver.CompressedRadius(1.35f, 1f), Is.LessThan(0.55f));
        }

        [Test]
        public void CompressionBlast_IsFasterThanTapAndDeterministic()
        {
            EarthGravityClusterThrowTuning tuning = EarthGravityClusterThrowTuning.Default;
            EarthGravityClusterLaunchSample tap = EarthGravityClusterThrowSolver.Solve(
                93u, 2, 8, 42f, new float3(1f, 0f, 0f), new float3(0f, 1f, 0f),
                EarthGravityClusterReleaseMode.Direct, 0f, in tuning);
            EarthGravityClusterLaunchSample charged = EarthGravityClusterThrowSolver.Solve(
                93u, 2, 8, 42f, new float3(1f, 0f, 0f), new float3(0f, 1f, 0f),
                EarthGravityClusterReleaseMode.CompressionBlast, 1f, in tuning);
            EarthGravityClusterLaunchSample repeated = EarthGravityClusterThrowSolver.Solve(
                93u, 2, 8, 42f, new float3(1f, 0f, 0f), new float3(0f, 1f, 0f),
                EarthGravityClusterReleaseMode.CompressionBlast, 1f, in tuning);

            Assert.That(charged.Speed, Is.GreaterThan(tap.Speed + 8f));
            Assert.That(charged.Velocity, Is.EqualTo(repeated.Velocity));
            Assert.That(charged.AngularVelocity, Is.EqualTo(repeated.AngularVelocity));
        }
    }
}
