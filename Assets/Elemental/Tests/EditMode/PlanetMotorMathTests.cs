using Elemental.Simulation.Characters;
using Elemental.Simulation.Gravity;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class PlanetMotorMathTests
    {
        [TestCase(0f, 1f, 0f, 0f, 1f, 0f)]
        [TestCase(0f, -1f, 0f, 0f, -1f, 0f)]
        [TestCase(1f, 0f, 0f, 1f, 0f, 0f)]
        [TestCase(0.001f, 0.999f, 0.001f, 0f, 0f, 1f)]
        public void GravityFrame_IsFiniteAndOrthogonalAtPolesAndParallelReference(
            float upX,
            float upY,
            float upZ,
            float forwardX,
            float forwardY,
            float forwardZ)
        {
            float3 up = math.normalize(new float3(upX, upY, upZ));

            GravityFrame.BuildTangentBasis(
                up,
                new float3(forwardX, forwardY, forwardZ),
                out float3 forward,
                out float3 right);

            Assert.That(math.all(math.isfinite(forward)), Is.True);
            Assert.That(math.all(math.isfinite(right)), Is.True);
            Assert.That(math.length(forward), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(math.length(right), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(math.dot(forward, up), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(math.dot(right, up), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(math.dot(forward, right), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void PlanetMotorCommand_ClampsMoveAndRejectsNonFiniteInput()
        {
            PlanetMotorCommand clamped = new PlanetMotorCommand(10u, new float2(4f, 3f), true);
            PlanetMotorCommand safe = new PlanetMotorCommand(11u, new float2(float.NaN, 1f), false);

            Assert.That(math.length(clamped.Move), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(clamped.JumpPressed, Is.True);
            Assert.That(safe.Move, Is.EqualTo(float2.zero));
        }

        [Test]
        public void MouseFacingProjectsOntoLocalPlanetTangent()
        {
            float3 forward = PlanetFacingSolver.SolveTangentForward(
                new float3(0f, 1f, 0f),
                new float3(3f, 8f, 4f),
                new float3(0f, 0f, 1f));

            Assert.That(math.length(forward), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(math.dot(forward, new float3(0f, 1f, 0f)), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(forward.x, Is.GreaterThan(0f));
            Assert.That(forward.z, Is.GreaterThan(0f));
        }

        [Test]
        public void TankSteeringTurnsAroundLocalUpWithoutAddingMovement()
        {
            float3 rightTurn = PlanetTankSteeringSolver.Turn(
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                1f,
                90f,
                1f);
            float3 leftTurn = PlanetTankSteeringSolver.Turn(
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                -1f,
                90f,
                1f);

            Assert.That(rightTurn.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(leftTurn.x, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(math.dot(rightTurn, new float3(0f, 1f, 0f)), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(math.length(rightTurn), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void SmartCameraFramesAboveThePlayerAndLooksAheadAtTravelSpeed()
        {
            PlanetCameraFramingResult idle = PlanetCameraFramingSolver.Solve(
                float3.zero,
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                float3.zero,
                10.5f,
                6.8f,
                1.25f,
                4.5f,
                2.4f,
                6f,
                1.15f);
            PlanetCameraFramingResult moving = PlanetCameraFramingSolver.Solve(
                float3.zero,
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                new float3(0f, 0f, 6f),
                10.5f,
                6.8f,
                1.25f,
                4.5f,
                2.4f,
                6f,
                1.15f);

            Assert.That(idle.Position.y, Is.EqualTo(6.8f).Within(0.0001f));
            Assert.That(idle.Position.z, Is.EqualTo(-10.5f).Within(0.0001f));
            Assert.That(idle.Position.x, Is.EqualTo(1.15f).Within(0.0001f));
            Assert.That(idle.Focus.z, Is.EqualTo(4.5f).Within(0.0001f));
            Assert.That(moving.Focus.z, Is.EqualTo(6.9f).Within(0.0001f));
            Assert.That(math.all(math.isfinite(moving.Position)), Is.True);
            Assert.That(math.all(math.isfinite(moving.Focus)), Is.True);
        }

        [Test]
        public void WallStrokeHasNoHiddenTimeWindowAndWorksInAnyGroundDirection()
        {
            Assert.That(EarthWallGestureSolver.IsWallStroke(new float2(0.012f, 0f)), Is.True);
            Assert.That(EarthWallGestureSolver.IsWallStroke(new float2(-0.08f, 0.07f)), Is.True);
            Assert.That(EarthWallGestureSolver.IsWallStroke(new float2(0.002f, 0.013f)), Is.True);
            Assert.That(EarthWallGestureSolver.IsWallStroke(new float2(0.004f, 0.004f)), Is.False);
            Assert.That(EarthWallGestureSolver.IsWallStroke(new float2(float.NaN, 0f)), Is.False);
        }

        [Test]
        public void HeldSpaceContinuouslyIncreasesPillarHeightAndLaunchSpeed()
        {
            EarthPillarLaunchProfile profile = EarthPillarLaunchProfile.Default;
            EarthPillarLaunchResult tap = EarthPillarLaunchSolver.Solve(0f, in profile);
            EarthPillarLaunchResult medium = EarthPillarLaunchSolver.Solve(0.7f, in profile);
            EarthPillarLaunchResult full = EarthPillarLaunchSolver.Solve(profile.FullChargeSeconds, in profile);
            EarthPillarLaunchResult overheld = EarthPillarLaunchSolver.Solve(4f, in profile);

            Assert.That(medium.Height, Is.GreaterThan(tap.Height));
            Assert.That(full.Height, Is.GreaterThan(medium.Height));
            Assert.That(medium.VelocityChange, Is.GreaterThan(tap.VelocityChange));
            Assert.That(full.VelocityChange, Is.GreaterThan(medium.VelocityChange));
            Assert.That(overheld.Height, Is.EqualTo(full.Height).Within(0.0001f));
            Assert.That(overheld.VelocityChange, Is.EqualTo(full.VelocityChange).Within(0.0001f));
            Assert.That(medium.Charge01, Is.GreaterThan(0.6f),
                "The launch curve must be deliberately non-linear: a medium hold already feels substantial.");
            Assert.That(tap.VelocityChange, Is.GreaterThanOrEqualTo(10f),
                "Even a tap should produce a readable mobility burst.");
        }

        [Test]
        public void ShiftSectorAndSpacePowerBuildAHeightFallingPillarWave()
        {
            EarthPillarWaveSample[] narrow = EarthPillarWaveSolver.Build(0f, 0f);
            EarthPillarWaveSample[] full = EarthPillarWaveSolver.Build(1f, 1f);

            Assert.That(narrow.Length, Is.InRange(15, EarthPillarWaveSolver.MaximumColumns));
            Assert.That(narrow[0].AngleDegrees, Is.InRange(-23.5f, -21.5f));
            Assert.That(full.Length, Is.EqualTo(EarthPillarWaveSolver.MaximumColumns));
            float crestHeight = 0f;
            for (int index = 0; index < full.Length; index++)
                crestHeight = math.max(crestHeight, full[index].Height);
            Assert.That(crestHeight, Is.GreaterThan(full[0].Height * 2f));
            Assert.That(crestHeight, Is.GreaterThan(full[full.Length - 1].Height * 2f));
            Assert.That(full[full.Length - 1].Delay, Is.GreaterThan(full[0].Delay));
            Assert.That(full[0].ArcDistance, Is.LessThan(full[full.Length - 1].ArcDistance));
            Assert.That(full[full.Length - 1].Width, Is.GreaterThan(full[0].Width));
            Assert.That(full[0].HoldDuration, Is.GreaterThan(0f));
        }

    }
}
