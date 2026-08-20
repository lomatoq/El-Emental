using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class SupportFrameSnapshotTests
    {
        [Test]
        public void VelocityAt_IncludesAngularContactVelocity()
        {
            var support = new SupportFrameSnapshot(
                7u, 3u, float3.zero, quaternion.identity,
                new float3(2f, 0f, 0f),
                new float3(0f, 1f, 0f),
                new float3(2f, 0f, 0f),
                new float3(0f, 1f, 0f), false);

            float3 velocity = support.VelocityAt(new float3(0f, 0f, 2f));

            Assert.That(velocity.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(velocity.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(velocity.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void GenerationChange_IsNotClassifiedAsStableSupport()
        {
            SupportFrameSnapshot previous = Frame(4u, 9u, float3.zero, quaternion.identity);
            SupportFrameSnapshot current = Frame(4u, 10u, new float3(0.01f, 0f, 0f), quaternion.identity);

            SupportFrameContinuity continuity = MovingSurfaceSolver.ClassifyContinuity(
                previous, current, 1f, math.radians(45f));

            Assert.That(continuity, Is.EqualTo(SupportFrameContinuity.NewGeneration));
        }

        [Test]
        public void SmallPhysicalStep_RemainsStable()
        {
            SupportFrameSnapshot previous = Frame(4u, 9u, float3.zero, quaternion.identity);
            SupportFrameSnapshot current = Frame(
                4u, 9u, new float3(0.05f, 0f, 0f), quaternion.RotateY(math.radians(2f)));

            SupportFrameContinuity continuity = MovingSurfaceSolver.ClassifyContinuity(
                previous, current, 0.5f, math.radians(10f));

            Assert.That(continuity, Is.EqualTo(SupportFrameContinuity.Stable));
        }

        [Test]
        public void TeleportDelta_IsClassifiedAsDiscontinuous()
        {
            SupportFrameSnapshot previous = Frame(4u, 9u, float3.zero, quaternion.identity);
            SupportFrameSnapshot current = Frame(4u, 9u, new float3(8f, 0f, 0f), quaternion.identity);

            SupportFrameContinuity continuity = MovingSurfaceSolver.ClassifyContinuity(
                previous, current, 0.5f, math.radians(10f));

            Assert.That(continuity, Is.EqualTo(SupportFrameContinuity.Discontinuous));
        }

        [Test]
        public void AngularVelocity_ReconstructsQuarterTurn()
        {
            float3 velocity = MovingSurfaceSolver.AngularVelocity(
                quaternion.identity,
                quaternion.RotateY(math.PI * 0.5f),
                0.5f);

            Assert.That(velocity.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(velocity.y, Is.EqualTo(math.PI).Within(0.0001f));
            Assert.That(velocity.z, Is.EqualTo(0f).Within(0.0001f));
        }

        private static SupportFrameSnapshot Frame(
            uint id,
            uint generation,
            float3 position,
            quaternion rotation) =>
            new SupportFrameSnapshot(
                id, generation, position, rotation,
                float3.zero, float3.zero, float3.zero,
                new float3(0f, 1f, 0f), false);
    }
}
