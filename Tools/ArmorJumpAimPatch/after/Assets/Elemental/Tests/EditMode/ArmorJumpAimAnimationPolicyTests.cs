using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class ArmorJumpAimAnimationPolicyTests
    {
        [Test]
        public void EquippedArmorUsesOrdinaryLocomotionInsteadOfPermanentCastPose()
        {
            Assert.That(EarthPersistentAnimationPolicy.AllowsSustainedUpperBody(
                EarthActionOwner.Armor, false), Is.False);
            Assert.That(EarthPersistentAnimationPolicy.ResolveArmorEncumbrance(true, 0f),
                Is.EqualTo(EarthPersistentAnimationPolicy.MinimumArmorEncumbrance).Within(.0001f));
            Assert.That(EarthPersistentAnimationPolicy.ResolveArmorEncumbrance(true, 1f),
                Is.EqualTo(EarthPersistentAnimationPolicy.MaximumArmorEncumbrance).Within(.0001f));
            Assert.That(EarthPersistentAnimationPolicy.ResolveArmorEncumbrance(false, 1f), Is.Zero);

            float compactScale = EarthPersistentAnimationPolicy.ResolveArmorSpeedScale(
                EarthPersistentAnimationPolicy.MinimumArmorEncumbrance);
            float expandedScale = EarthPersistentAnimationPolicy.ResolveArmorSpeedScale(
                EarthPersistentAnimationPolicy.MaximumArmorEncumbrance);
            Assert.That(compactScale, Is.EqualTo(.826f).Within(.0001f));
            Assert.That(expandedScale, Is.EqualTo(.754f).Within(.0001f));
            Assert.That(expandedScale, Is.GreaterThan(.70f),
                "Armor should feel heavy without dropping to cast-stance speed.");
        }

        [Test]
        public void ShortJumpDoesNotAcquirePillarUpperBodyButRealChargeDoes()
        {
            Assert.That(EarthPersistentAnimationPolicy.AllowsSustainedUpperBody(
                EarthActionOwner.Pillar, false), Is.False,
                "The Space disambiguation window still belongs to ordinary jump locomotion.");
            Assert.That(EarthPersistentAnimationPolicy.AllowsSustainedUpperBody(
                EarthActionOwner.Pillar, true), Is.True);
            Assert.That(EarthPersistentAnimationPolicy.AllowsSustainedUpperBody(
                EarthActionOwner.LandingCushion, false), Is.True);
        }

        [Test]
        public void OrdinaryJumpClearsOldMagicOnceWithoutSuppressingLaterAirCasts()
        {
            Assert.That(EarthPersistentAnimationPolicy.ShouldClearMagicForOrdinaryJump(
                false, true, false, EarthAnimationPhase.Rising), Is.True);
            Assert.That(EarthPersistentAnimationPolicy.ShouldClearMagicForOrdinaryJump(
                false, true, true, EarthAnimationPhase.Rising), Is.False);
            Assert.That(EarthPersistentAnimationPolicy.ShouldClearMagicForOrdinaryJump(
                false, false, false, EarthAnimationPhase.Rising), Is.False,
                "A physical pillar launch is not an ordinary motor jump.");
            Assert.That(EarthPersistentAnimationPolicy.ShouldClearMagicForOrdinaryJump(
                true, true, false, EarthAnimationPhase.GroundedIdle), Is.False);
        }

        [Test]
        public void CenteredAimRemovesOnlyLateLateralBodyDrift()
        {
            var source = new EarthChoreographyPoseOffset(
                new float3(3f, 6f, -4f),
                new float3(-1f, -2f, 1.5f),
                new float3(1f, 2f, 3f),
                new float3(-1f, -2f, -3f));

            EarthChoreographyPoseOffset aligned =
                EarthChoreographyVisualSolver.AlignLateralBodyToAim(
                    in source,
                    new float3(0f, 0f, 1f));

            Assert.That(aligned.ChestEuler.x, Is.EqualTo(source.ChestEuler.x).Within(.0001f));
            Assert.That(aligned.ChestEuler.y, Is.Zero.Within(.0001f));
            Assert.That(aligned.ChestEuler.z, Is.Zero.Within(.0001f));
            Assert.That(aligned.HeadEuler.x, Is.EqualTo(source.HeadEuler.x).Within(.0001f));
            Assert.That(aligned.HeadEuler.y, Is.Zero.Within(.0001f));
            Assert.That(aligned.HeadEuler.z, Is.Zero.Within(.0001f));
            Assert.That(aligned.LeftShoulderEuler, Is.EqualTo(source.LeftShoulderEuler));
            Assert.That(aligned.RightShoulderEuler, Is.EqualTo(source.RightShoulderEuler));
        }

        [Test]
        public void LateralAimRetainsAuthoredBodyDirectionAndIsSymmetric()
        {
            var source = new EarthChoreographyPoseOffset(
                new float3(2f, 5f, 3f),
                new float3(-1f, -2f, -1f),
                float3.zero,
                float3.zero);
            EarthChoreographyPoseOffset left =
                EarthChoreographyVisualSolver.AlignLateralBodyToAim(
                    in source,
                    math.normalize(new float3(-.7f, 0f, 1f)));
            EarthChoreographyPoseOffset right =
                EarthChoreographyVisualSolver.AlignLateralBodyToAim(
                    in source,
                    math.normalize(new float3(.7f, 0f, 1f)));

            Assert.That(left.ChestEuler.y, Is.EqualTo(right.ChestEuler.y).Within(.0001f));
            Assert.That(left.ChestEuler.z, Is.EqualTo(right.ChestEuler.z).Within(.0001f));
            Assert.That(math.abs(left.ChestEuler.y), Is.GreaterThan(4.5f));
        }
    }
}
