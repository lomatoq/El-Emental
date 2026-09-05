using Elemental.Presentation.Animation;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthStableArmIkGeometryTests
    {
        [Test]
        public void NearlyStraightRequestRetainsARealBendTowardStablePole()
        {
            EarthStableArmIkSample sample = EarthStableArmIkGeometry.Resolve(
                Vector3.zero,
                new Vector3(0f, 0f, 4f),
                new Vector3(-1f, 0f, .4f),
                .5f,
                .5f,
                .92f);

            Assert.That(sample.Target.magnitude, Is.EqualTo(.92f).Within(.0001f));
            Assert.That(sample.Elbow.x, Is.LessThan(-.15f));
            Assert.That(Vector3.Distance(Vector3.zero, sample.Elbow), Is.EqualTo(.5f).Within(.0001f));
            Assert.That(Vector3.Distance(sample.Elbow, sample.Target), Is.EqualTo(.5f).Within(.0001f));
        }

        [Test]
        public void PoleSideStaysContinuousAcrossSmallTargetMotion()
        {
            Vector3 pole = new Vector3(-1f, -.1f, .3f);
            EarthStableArmIkSample before = EarthStableArmIkGeometry.Resolve(
                Vector3.zero, new Vector3(-.002f, .08f, 2f), pole, .48f, .46f, .92f);
            EarthStableArmIkSample after = EarthStableArmIkGeometry.Resolve(
                Vector3.zero, new Vector3(.002f, .081f, 2f), pole, .48f, .46f, .92f);

            Assert.That(Vector3.Dot(before.PoleDirection, after.PoleDirection), Is.GreaterThan(.999f));
            Assert.That(before.Elbow.x, Is.LessThan(-.1f));
            Assert.That(after.Elbow.x, Is.LessThan(-.1f));
            Assert.That(Vector3.Distance(before.Elbow, after.Elbow), Is.LessThan(.01f));
        }

        [Test]
        public void SmallRigWeightBlendsSolvedRotationInsteadOfFullyApplyingIt()
        {
            Quaternion source = Quaternion.identity;
            Quaternion solved = Quaternion.AngleAxis(167f, Vector3.right);
            Quaternion blended = EarthStableArmIkGeometry.BlendRotation(source, solved, .124f);

            Assert.That(Quaternion.Angle(source, blended), Is.InRange(20f, 21f));
            Assert.That(Quaternion.Angle(source,
                EarthStableArmIkGeometry.BlendRotation(source, solved, 0f)), Is.Zero);
            Assert.That(Quaternion.Angle(solved,
                EarthStableArmIkGeometry.BlendRotation(source, solved, 1f)), Is.Zero);
        }

        [Test]
        public void EvenOppositeFullSolutionsCannotProduceTheObservedLowWeightFlip()
        {
            Quaternion source = Quaternion.identity;
            Quaternion first = EarthStableArmIkGeometry.BlendRotation(
                source, Quaternion.AngleAxis(90f, Vector3.up), .124f);
            Quaternion second = EarthStableArmIkGeometry.BlendRotation(
                source, Quaternion.AngleAxis(-90f, Vector3.up), .124f);

            Assert.That(Quaternion.Angle(first, second), Is.LessThan(23f),
                "With the authored pose held constant, even a complete target-branch reversal is bounded by post-solve weighting.");
        }

        [Test]
        public void InvalidOrTooCloseTargetsRemainFiniteAndReachable()
        {
            EarthStableArmIkSample sample = EarthStableArmIkGeometry.Resolve(
                new Vector3(1f, 2f, 3f),
                new Vector3(float.NaN, 0f, 0f),
                new Vector3(float.PositiveInfinity, 0f, 0f),
                .42f,
                .38f,
                .92f);

            Assert.That(float.IsNaN(sample.Target.x) || float.IsInfinity(sample.Target.x), Is.False);
            Assert.That(float.IsNaN(sample.Elbow.x) || float.IsInfinity(sample.Elbow.x), Is.False);
            Assert.That(Vector3.Distance(new Vector3(1f, 2f, 3f), sample.Target),
                Is.LessThanOrEqualTo((.42f + .38f) * .92f + .0001f));
        }
    }
}
