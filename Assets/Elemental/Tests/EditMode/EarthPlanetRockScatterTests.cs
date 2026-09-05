using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthPlanetRockScatterTests
    {
        [Test]
        public void BlockedSlotsRetryFarFromTheirOriginalExclusionDeterministically()
        {
            for (int i = 0; i < 24; i++)
            {
                Vector3 original = EarthPlanetRockScatter.CandidateDirection(i, 24, 20260904u, 0);
                for (int attempt = 1; attempt < 4; attempt++)
                {
                    Vector3 retry = EarthPlanetRockScatter.CandidateDirection(i, 24, 20260904u, attempt);
                    Assert.That(retry.magnitude, Is.EqualTo(1f).Within(.0001f));
                    Assert.That(Vector3.Angle(original, retry), Is.InRange(74.99f, 165.01f));
                    Assert.That(retry, Is.EqualTo(EarthPlanetRockScatter.CandidateDirection(i, 24, 20260904u, attempt)));
                }
            }
        }

        [TestCase(24)]
        [TestCase(160)]
        [TestCase(128)]
        public void SeededDistributionCoversBothSidesOfEveryWorldAxis(int count)
        {
            Vector3 sum = Vector3.zero;
            Vector3 minimum = Vector3.one;
            Vector3 maximum = -Vector3.one;
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = EarthPlanetRockScatter.DistributionDirection(i, count, 20260904u);
                Assert.That(direction.magnitude, Is.EqualTo(1f).Within(.0001f));
                Assert.That(direction, Is.EqualTo(EarthPlanetRockScatter.DistributionDirection(i, count, 20260904u)));
                sum += direction;
                minimum = Vector3.Min(minimum, direction);
                maximum = Vector3.Max(maximum, direction);
            }
            Assert.That((sum / count).magnitude, Is.LessThan(.025f));
            Assert.That(Mathf.Max(minimum.x, Mathf.Max(minimum.y, minimum.z)), Is.LessThan(-.8f));
            Assert.That(Mathf.Min(maximum.x, Mathf.Min(maximum.y, maximum.z)), Is.GreaterThan(.8f));
            Assert.That(EarthPlanetRockScatter.DistributionDirection(0, count, 1u),
                Is.Not.EqualTo(EarthPlanetRockScatter.DistributionDirection(0, count, 2u)));
        }

        [Test]
        public void DefaultBudgetUsesGameplayRocksAndBoundedCosmeticClusters()
        {
            var profile = ScriptableObject.CreateInstance<EarthPlanetRockScatterProfile>();
            try
            {
                Assert.That(profile.LargeCount, Is.EqualTo(24));
                Assert.That(profile.MediumCount, Is.EqualTo(160));
                Assert.That(profile.ClusterCount, Is.EqualTo(128));
                Assert.That(profile.ClusterMinimumStones, Is.EqualTo(8));
                Assert.That(profile.ClusterMaximumStones, Is.EqualTo(16));
                Assert.That(profile.GameplayObjectsPerFrame, Is.LessThanOrEqualTo(4));
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
