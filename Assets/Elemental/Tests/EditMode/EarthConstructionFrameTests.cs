using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthConstructionFrameTests
    {
        [Test]
        public void CantileverBuildsAPlayableDeckFromAVerticalSupportFace()
        {
            float3 support = new float3(0f, 24f, 0f);
            EarthPlatformGeometry geometry = EarthCantileverPlatformSolver.Build(
                support,
                new float3(0f, 0f, 1f),
                new float3(1f, 0f, 0f),
                float3.zero,
                4.2f,
                2.4f,
                0.72f);

            Assert.That(geometry.IsValid, Is.True);
            Assert.That(geometry.Polygon.Length, Is.EqualTo(6));
            Assert.That(math.dot(geometry.Up, new float3(0f, 1f, 0f)), Is.GreaterThan(0.999f));
            Assert.That(math.dot(geometry.Forward, new float3(0f, 0f, 1f)), Is.GreaterThan(0.999f));
            Assert.That(geometry.Area, Is.GreaterThan(6f));
            Assert.That((geometry.Center + geometry.Up * 0.72f).y, Is.EqualTo(support.y).Within(0.001f),
                "The finished deck top must meet the authored support height.");
            float minimumDepth = float.PositiveInfinity;
            float maximumDepth = float.NegativeInfinity;
            for (int index = 0; index < geometry.Polygon.Length; index++)
            {
                minimumDepth = math.min(minimumDepth, geometry.Polygon[index].y);
                maximumDepth = math.max(maximumDepth, geometry.Polygon[index].y);
            }
            float rootWorldDepth = geometry.Center.z + minimumDepth;
            float noseWorldDepth = geometry.Center.z + maximumDepth;
            Assert.That(rootWorldDepth, Is.LessThan(-0.17f), "The root plate must embed into the wall.");
            Assert.That(noseWorldDepth, Is.GreaterThan(1.3f), "The deck must project away from the wall.");
        }

        [Test]
        public void AuthoredFrameOrthonormalizesAndKeepsSupportGeneration()
        {
            var frame = new EarthConstructionFrame(
                17u,
                5u,
                new float3(2f, 3f, 4f),
                new float3(0f, 2f, 0f),
                new float3(2f, 1f, 0f),
                quaternion.identity,
                quaternion.identity,
                ConstructionOrientationMode.PreserveAuthoredFrame);

            Assert.That(frame.HasSupport, Is.True);
            Assert.That(frame.SupportId, Is.EqualTo(17u));
            Assert.That(frame.SupportGeneration, Is.EqualTo(5u));
            Assert.That(math.dot(frame.Normal, frame.Tangent), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(math.length(frame.Bitangent), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(frame.OrientationMode, Is.EqualTo(ConstructionOrientationMode.PreserveAuthoredFrame));
        }
    }
}
