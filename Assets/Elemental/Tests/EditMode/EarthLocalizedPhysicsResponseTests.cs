using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthLocalizedPhysicsResponseTests
    {
        [Test]
        public void RegionalLimitsProtectHeadAndSupportLegsWithoutReducingTheArmResponse()
        {
            var tuning = EarthLocalizedPhysicsTuning.Default;
            Assert.That(tuning.LimitsFor(2).y, Is.LessThan(tuning.LimitsFor(1).y));
            Assert.That(tuning.LimitsFor(7).x, Is.LessThan(tuning.LimitsFor(3).x));
            Assert.That(tuning.LimitsFor(3), Is.EqualTo(tuning.LimitsFor(5)));
            Assert.That(tuning.LimitsFor(8), Is.EqualTo(tuning.LimitsFor(10)));
            var capped = new EarthLocalizedPhysicsTuning(.12f, .5f, .4f, .24f, 90f, .9f, .06f,
                .1f, 20f, new float2(1, 90), new float2(1, 90), new float2(1, 90), new float2(1, 90));
            for (int i = 0; i < 11; i++) Assert.That(capped.LimitsFor(i), Is.EqualTo(new float2(.1f, 20f)));
        }

        [Test]
        public void CapsuleRimFollowsIncomingHeadPathInsteadOfCloserShoulder()
        {
            float3 contact = new(0, 1.6f, -.55f);
            float3 head = new(0, 1.6f, 0);
            float3 shoulder = new(-.2f, 1.5f, -.35f);
            float3 direction = new(0, 0, 1);
            Assert.That(math.distancesq(contact, shoulder), Is.LessThan(math.distancesq(contact, head)));
            Assert.That(EarthLocalizedPhysicsResponse.RegionContactScore(head, contact, direction, 1.12f),
                Is.LessThan(EarthLocalizedPhysicsResponse.RegionContactScore(shoulder, contact, direction, 1.12f)));
            Assert.That(EarthLocalizedPhysicsResponse.RegionContactScore(new float3(0, 1.6f, 3), contact, direction, 1.12f),
                Is.GreaterThan(1f), "The ray must not pick a remote bone beyond the physical capsule.");
        }

        [Test]
        public void WeakDriveRecoversSmoothlyAndOnlyMediumHitsStun()
        {
            EarthLocalizedPhysicsTuning tuning = EarthLocalizedPhysicsTuning.Default;
            Assert.That(EarthLocalizedPhysicsResponse.DriveScale(.119f, in tuning), Is.EqualTo(.06f));
            Assert.That(EarthLocalizedPhysicsResponse.VisibleWeight(.119f, in tuning), Is.EqualTo(1f));
            Assert.That(EarthLocalizedPhysicsResponse.DriveScale(.37f, in tuning), Is.EqualTo(.53f).Within(.001f));
            Assert.That(EarthLocalizedPhysicsResponse.VisibleWeight(.62f, in tuning), Is.Zero.Within(.001f));
            Assert.That(EarthLocalizedPhysicsResponse.StunSeconds(EarthCharacterImpactResponse.Flinch, in tuning), Is.Zero);
            Assert.That(EarthLocalizedPhysicsResponse.StunSeconds(EarthCharacterImpactResponse.Stagger, in tuning), Is.EqualTo(.24f));
            Assert.That(EarthLocalizedPhysicsResponse.StunSeconds(EarthCharacterImpactResponse.RecoverableKnockdown, in tuning), Is.Zero);
            Assert.That(EarthLocalizedPhysicsResponse.LocalVelocity(.01f, EarthCharacterImpactResponse.Flinch), Is.GreaterThan(0f));
            Assert.That(EarthLocalizedPhysicsResponse.LocalVelocity(30f, EarthCharacterImpactResponse.Stagger), Is.EqualTo(2.2f));
        }

        [Test]
        public void RegionGraphIsAcyclicAndTransfersToTheCorrectParent()
        {
            int[] expected = { -1, 0, 1, 1, 3, 1, 5, 0, 7, 0, 9 };
            for (int region = 0; region < expected.Length; region++)
            {
                Assert.That(EarthLocalizedPhysicsResponse.Parent(region), Is.EqualTo(expected[region]));
                Assert.That(EarthLocalizedPhysicsResponse.Parent(region), Is.LessThan(region));
            }
            Assert.That(EarthLocalizedPhysicsTuning.Default.ParentTransfer, Is.EqualTo(.4f));
        }
    }
}
