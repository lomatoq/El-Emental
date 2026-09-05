using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthWaveSurfaceFollowTests
    {
        [TestCase(0f)] [TestCase(55.5f)]
        public void QueryStaysBelowOverheadArchAndStillReachesGround(float floor)
        {
            var query = EarthWaveSurfaceFollow.CreateQuery(new float3(0f, floor, 0f), new float3(0f, 1f, 0f));
            Assert.That(query.Origin.y, Is.LessThan(floor + 3.8f));
            Assert.That(query.Origin.y, Is.EqualTo(floor + 0.45f).Within(0.00001f));
            Assert.That(query.Direction.y, Is.EqualTo(-1f));
            Assert.That((query.Origin + query.Direction * query.MaximumDistance).y, Is.EqualTo(floor - 4f).Within(0.00001f));
        }

        [Test]
        public void RadialUpUsesTheSameBoundedGroundSheet()
        {
            float3 up = math.normalize(new float3(1f, 2f, 3f));
            var query = EarthWaveSurfaceFollow.CreateQuery(up * 55.5f, up);
            Assert.That(math.dot(query.Origin - up * 55.5f, up), Is.EqualTo(0.45f).Within(0.00001f));
            Assert.That(math.dot(query.Direction, up), Is.EqualTo(-1f).Within(0.00001f));
        }
    }
}
