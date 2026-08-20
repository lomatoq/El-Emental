using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class PlanetOrientationSolverTests
    {
        [Test]
        public void LargeTiltConvergesWithoutOvershoot()
        {
            quaternion current = quaternion.RotateZ(math.radians(70f));
            quaternion desired = quaternion.identity;
            float previous = AngleDegrees(current, desired);
            for (int tick = 0; tick < 60; tick++)
            {
                current = PlanetOrientationSolver.Step(current, desired, 14f, 540f, 1f / 60f);
                float angle = AngleDegrees(current, desired);
                Assert.That(angle, Is.LessThanOrEqualTo(previous + 0.001f));
                previous = angle;
            }
            Assert.That(previous, Is.LessThan(0.01f));
        }

        [Test]
        public void PerTickRotationHonoursMaximumSpeed()
        {
            quaternion current = quaternion.identity;
            quaternion desired = quaternion.RotateY(math.radians(180f));
            quaternion next = PlanetOrientationSolver.Step(current, desired, 100f, 120f, 0.02f);
            Assert.That(AngleDegrees(current, next), Is.LessThanOrEqualTo(2.401f));
        }

        private static float AngleDegrees(quaternion a, quaternion b)
        {
            float dot = math.clamp(math.abs(math.dot(math.normalize(a).value, math.normalize(b).value)), 0f, 1f);
            return math.degrees(2f * math.acos(dot));
        }
    }
}
