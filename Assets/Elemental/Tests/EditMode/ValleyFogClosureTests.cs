using Elemental.Presentation.Rendering;
using Elemental.Simulation.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    // Pure closure and authored-profile contracts. These are not GPU pixel evidence.
    public sealed class ValleyFogClosureTests
    {
        private const double Start = 1800, End = 3200;

        [Test]
        public void ClosureHasExactClearAndOpaquePlateausAndNoBoundaryPop()
        {
            Assert.That(ValleyAtmosphereMath.DistanceClosure(0, Start, End), Is.Zero);
            Assert.That(ValleyAtmosphereMath.DistanceClosure(Start, Start, End), Is.Zero);
            Assert.That(ValleyAtmosphereMath.DistanceClosure(End, Start, End), Is.EqualTo(1));
            Assert.That(ValleyAtmosphereMath.DistanceClosure(12000, Start, End), Is.EqualTo(1));
            Assert.That(ValleyAtmosphereMath.DistanceClosure(Start + 1, Start, End), Is.LessThan(0.00001));
            Assert.That(1 - ValleyAtmosphereMath.DistanceClosure(End - 1, Start, End), Is.LessThan(0.00001));
            double previous = 0;
            for (int distance = (int)Start; distance <= End; distance++)
            {
                double closure = ValleyAtmosphereMath.DistanceClosure(distance, Start, End);
                Assert.That(closure, Is.InRange(previous, 1));
                Assert.That(closure - previous, Is.LessThan(0.0011), "A one-metre camera move must not create an opacity step.");
                previous = closure;
            }
        }

        [TestCase(-200)]
        [TestCase(0)]
        [TestCase(500)]
        public void DistantStoneFullyClosesEvenWithoutPhysicalHeightFog(double surfaceHeight)
        {
            double closure = ValleyAtmosphereMath.DistanceClosure(End, Start, End);
            double alpha = ValleyAtmosphereMath.ComposeSurfaceOpacity(0, 0.78, surfaceHeight, 4000, 55.1, 1, closure);
            Assert.That(alpha, Is.EqualTo(1), "High stone must not retain the aerial cap's residual visibility.");
        }

        [Test]
        public void ClosingDistanceCannotWashOutProtectedPlayablePlanetInReverseView()
        {
            const double radius = 55.1;
            double protection = ValleyAtmosphereMath.UpperWindowProtection(4000, radius, radius, radius, 300);
            double closure = ValleyAtmosphereMath.DistanceClosure(4000, Start, End);
            Assert.That(ValleyAtmosphereMath.ComposeSurfaceOpacity(1, 0.78, radius, radius, radius, protection, closure), Is.Zero);
            Assert.That(ValleyAtmosphereMath.ComposeSurfaceOpacity(0, 0, -radius, radius, radius, 1, closure), Is.EqualTo(1));
        }

        [Test]
        public void ClosureAttenuatesResidualTransmissionWithoutChangingEarlierFog()
        {
            double baseline = ValleyAtmosphereMath.ComposeSurfaceOpacity(0.3, 0.4, 100, 4000, 55.1, 1);
            double before = ValleyAtmosphereMath.ComposeSurfaceOpacity(0.3, 0.4, 100, 4000, 55.1, 1,
                ValleyAtmosphereMath.DistanceClosure(Start - 1, Start, End));
            double middle = ValleyAtmosphereMath.ComposeSurfaceOpacity(0.3, 0.4, 100, 4000, 55.1, 1,
                ValleyAtmosphereMath.DistanceClosure((Start + End) * 0.5, Start, End));
            Assert.That(before, Is.EqualTo(baseline));
            Assert.That(1 - middle, Is.EqualTo((1 - baseline) * 0.5).Within(1e-12));
        }

        [Test]
        public void InvalidClosureRangesRejectNanInfinityAndReversedDistances()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ValleyAtmosphereMath.DistanceClosure(double.NaN, Start, End));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ValleyAtmosphereMath.DistanceClosure(-1, Start, End));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ValleyAtmosphereMath.DistanceClosure(4000, Start, double.PositiveInfinity));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ValleyAtmosphereMath.DistanceClosure(4000, Start, Start));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ValleyAtmosphereMath.DistanceClosure(4000, End, Start));
        }

        [Test]
        public void SavedProfileAndFreshDefaultsHaveBlueFogAndClosureBeyondGameplay()
        {
            var saved = AssetDatabase.LoadAssetAtPath<ValleyAtmosphereProfile>("Assets/Elemental/Content/Profiles/ValleyAtmosphereV2.asset");
            Assert.That(saved, Is.Not.Null);
            var defaults = ScriptableObject.CreateInstance<ValleyAtmosphereProfile>();
            try
            {
                foreach (var profile in new[] { saved, defaults })
                {
                    Assert.That(profile.FarClosureStart, Is.GreaterThan(profile.NearClearRange + 100));
                    Assert.That(profile.FarClosureEnd, Is.GreaterThan(profile.FarClosureStart));
                    Assert.That(profile.FarClosureEnd, Is.LessThan(profile.SkyDistance));
                    Assert.That(profile.DayFog.b - profile.DayFog.r, Is.GreaterThan(0.23f), "Upper fog must be bluer than the previous saved .75/.865/.98 palette.");
                    Assert.That(profile.DayFogBottom.b - profile.DayFogBottom.r, Is.GreaterThan(0.216f));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }
        }
    }
}
