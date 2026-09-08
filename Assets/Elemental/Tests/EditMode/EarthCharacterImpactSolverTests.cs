using Elemental.Simulation.Combat;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthCharacterImpactSolverTests
    {
        private static readonly EarthCharacterImpactTuning Tuning =
            EarthCharacterImpactTuning.Default;

        [Test]
        public void IncomingVelocityKeepsTravelDirectionForEitherCallbackSign()
        {
            float3 travel = new(0f, -.622f, 9.981f);
            Assert.That(EarthCharacterImpactSolver.OrientIncomingRelativeVelocity(travel, travel, float3.zero), Is.EqualTo(travel));
            Assert.That(EarthCharacterImpactSolver.OrientIncomingRelativeVelocity(-travel, travel, float3.zero), Is.EqualTo(travel));
            float3 receiver = travel * 2f;
            Assert.That(EarthCharacterImpactSolver.OrientIncomingRelativeVelocity(travel, travel, receiver), Is.EqualTo(-travel),
                "An overtaking receiver reverses incoming relative travel; absolute projectile direction alone is insufficient.");
        }

        [TestCase(EarthCharacterImpactSourceKind.LooseStone, 1.4f)]
        [TestCase(EarthCharacterImpactSourceKind.ArmorProjectile, 1.4f)]
        [TestCase(EarthCharacterImpactSourceKind.BotProjectile, 1.4f)]
        [TestCase(EarthCharacterImpactSourceKind.StonePunch, 1.4f)]
        [TestCase(EarthCharacterImpactSourceKind.PillarWave, 1f)]
        [TestCase(EarthCharacterImpactSourceKind.SurfNose, 1f)]
        public void StoneWeightTransferPreservesMassOrderingAndLaunchBudget(
            EarthCharacterImpactSourceKind source, float expectedGain)
        {
            float gain = EarthCharacterImpactSolver.WeightTransferMultiplier(source);
            Assert.That(gain, Is.EqualTo(expectedGain));
            float previous = 0f;
            foreach (float mass in new[] { 0.5f, 10f, 40f, 95f, 1200f })
            {
                float normalized = EarthCharacterImpactSolver.StoneImpulse(mass, 80f, 24f) / 80f;
                float shove = normalized * 0.65f * gain;
                Assert.That(shove, Is.GreaterThan(previous));
                previous = shove;
                float3 bounded = EarthRagdollLaunchLimiter.LimitVelocityChange(
                    float3.zero, new float3(shove, shove, 0f), new float3(0f, 1f, 0f),
                    EarthRagdollLaunchLimiter.DefaultGravityMagnitude, 2f, 4f);
                Assert.That(math.abs(bounded.x), Is.LessThanOrEqualTo(4.001f));
                Assert.That(bounded.y * bounded.y /
                    (2f * EarthRagdollLaunchLimiter.DefaultGravityMagnitude),
                    Is.LessThanOrEqualTo(2.001f));
            }
        }

        [Test]
        public void StoneMomentumAndDamageIncreaseWithMassAndSpeedWithoutTinyStoneKnockdown()
        {
            float previous = 0f;
            foreach (float mass in new[] { 0.5f, 10f, 40f, 95f, 400f, 1200f })
            {
                float velocity = EarthCharacterImpactSolver.StoneImpulse(mass, 80f, 24f) / 80f;
                Assert.That(velocity, Is.GreaterThan(previous));
                Assert.That(velocity, Is.LessThan(24f * 0.3f));
                Assert.That(EarthCharacterImpactSolver.StoneImpulse(mass, 80f, 12f) / 80f,
                    Is.LessThan(velocity));
                previous = velocity;
            }
            float tiny = EarthCharacterImpactSolver.StoneImpulse(0.5f, 80f, 60f) / 80f;
            float large = EarthCharacterImpactSolver.StoneImpulse(1200f, 80f, 24f) / 80f;
            Assert.That(tiny, Is.LessThan(0.65f));
            Assert.That(large, Is.GreaterThan(5f));
            Assert.That(EarthCharacterImpactSolver.StoneDamage(8f, tiny), Is.LessThan(1f));
            Assert.That(EarthCharacterImpactSolver.StoneDamage(8f, large), Is.EqualTo(24f));
            Assert.That(EarthCharacterImpactSolver.StoneImpulse(1200f, 80f, 0.5f), Is.Zero);
            Assert.That(EarthCharacterImpactSolver.StoneImpulse(float.NaN, 80f, 20f), Is.Zero);
        }

        [TestCase(41f, 42f, EarthCharacterImpactResponse.Ignore)]
        [TestCase(42f, 42f, EarthCharacterImpactResponse.Flinch)]
        [TestCase(84f, 42f, EarthCharacterImpactResponse.Stagger)]
        [TestCase(210f, 42f, EarthCharacterImpactResponse.Knockout)]
        public void PhysicalSeverityUsesTargetVelocityChange(
            float impulse,
            float mass,
            EarthCharacterImpactResponse expected)
        {
            EarthCharacterImpact impact = Impact(
                EarthCharacterImpactSourceKind.LooseStone,
                impulse,
                mass);

            EarthCharacterImpactResolution result = EarthCharacterImpactSolver.Resolve(
                in impact,
                in Tuning);

            Assert.That(result.Response, Is.EqualTo(expected));
        }

        [TestCase(3.49f, EarthCharacterImpactResponse.Ignore)]
        [TestCase(3.5f, EarthCharacterImpactResponse.Stagger)]
        [TestCase(5f, EarthCharacterImpactResponse.Knockout)]
        [TestCase(7.5f, EarthCharacterImpactResponse.Knockout)]
        public void SurfHasExplicitCommittedContactBands(
            float closingSpeed,
            EarthCharacterImpactResponse expected)
        {
            EarthCharacterImpact impact = Impact(
                EarthCharacterImpactSourceKind.SurfNose,
                0.1f,
                42f,
                closingSpeed);

            EarthCharacterImpactResolution result = EarthCharacterImpactSolver.Resolve(
                in impact,
                in Tuning);

            Assert.That(result.Response, Is.EqualTo(expected));
        }

        [Test]
        public void DirectWaveCrestKnocksOutWithoutCellStacking()
        {
            EarthCharacterImpact impact = Impact(
                EarthCharacterImpactSourceKind.PillarWave,
                1f,
                42f,
                strength01: 0.05f);

            EarthCharacterImpactResolution result = EarthCharacterImpactSolver.Resolve(
                in impact,
                in Tuning);

            Assert.That(result.Response, Is.EqualTo(EarthCharacterImpactResponse.Knockout));
            Assert.That(EarthCharacterImpactSolver.IsDuplicate(77u, 100u, 77u, 98u), Is.True);
            Assert.That(EarthCharacterImpactSolver.IsDuplicate(77u, 104u, 77u, 100u), Is.False);
            Assert.That(EarthCharacterImpactSolver.IsDuplicate(78u, 100u, 77u, 100u), Is.False);
        }

        [Test]
        public void RagdollLaunchIsBoundedToTwoMeterRiseAndFourMeterPerSecondTangent()
        {
            float3 limited = EarthRagdollLaunchLimiter.LimitVelocityChange(
                new float3(0f, 2f, 0f),
                new float3(30f, 40f, 0f),
                new float3(0f, 1f, 0f));
            float finalUpSpeed = 2f + limited.y;
            float rise = finalUpSpeed * finalUpSpeed /
                         (2f * EarthRagdollLaunchLimiter.DefaultGravityMagnitude);
            Assert.That(rise, Is.LessThanOrEqualTo(2.01f));
            Assert.That(math.length(new float2(limited.x, limited.z)), Is.LessThanOrEqualTo(4.001f));
        }

        [Test]
        public void PillarCrestUsesOneLowLaunchBudgetForTheWholeSequentialRow()
        {
            EarthCharacterLaunchBudget budget = EarthCharacterLaunchBudgetSolver.Resolve(
                EarthCharacterImpactSourceKind.PillarCrest,
                2f,
                4f);
            Assert.That(budget.MaximumRiseMeters, Is.EqualTo(0.75f));
            Assert.That(budget.MaximumTangentSpeed, Is.EqualTo(2.2f));
            Assert.That(EarthCharacterLaunchBudgetSolver.IsCastScopedDuplicate(
                EarthCharacterImpactSourceKind.PillarCrest,
                0x57000011u,
                4.72f,
                0x57000011u,
                4f), Is.True);
            Assert.That(EarthCharacterLaunchBudgetSolver.IsCastScopedDuplicate(
                EarthCharacterImpactSourceKind.PillarCrest,
                0x57000012u,
                4.72f,
                0x57000011u,
                4f), Is.False);
        }

        [Test]
        public void HandIkStateReleasesToZeroWithinBoundedRecovery()
        {
            HandIkSample sample = HandIkSolver.Step(
                HandIkState.Tracking, 0.92f, 0f, 0.08f, 0.10f, 0.08f);
            Assert.That(sample.State, Is.EqualTo(HandIkState.Inactive));
            Assert.That(sample.Weight, Is.Zero);
        }

        [Test]
        public void CatastrophicVelocityChangeIsClampedForRagdollSafety()
        {
            EarthCharacterImpact impact = Impact(
                EarthCharacterImpactSourceKind.LooseStone,
                50000f,
                10f);

            EarthCharacterImpactResolution result = EarthCharacterImpactSolver.Resolve(
                in impact,
                in Tuning);

            Assert.That(result.Response, Is.EqualTo(EarthCharacterImpactResponse.Knockout));
            Assert.That(result.EffectiveVelocityChange, Is.EqualTo(12f));
        }

        [TestCase(EarthCharacterImpactSourceKind.LooseStone, 42f, 42f, 0f)]
        [TestCase(EarthCharacterImpactSourceKind.LooseStone, 252f, 42f, 0f)]
        [TestCase(EarthCharacterImpactSourceKind.ArmorProjectile, 168f, 42f, 0f)]
        [TestCase(EarthCharacterImpactSourceKind.PillarWave, 1f, 42f, 0f)]
        [TestCase(EarthCharacterImpactSourceKind.PillarCrest, 1f, 42f, 0f)]
        [TestCase(EarthCharacterImpactSourceKind.SurfNose, 0.1f, 42f, 3.5f)]
        [TestCase(EarthCharacterImpactSourceKind.Physics, 50000f, 10f, 0f)]
        public void ExplicitLegacyModeMatchesCompatibilityEntryPoint(
            EarthCharacterImpactSourceKind source,
            float impulse,
            float mass,
            float closingSpeed)
        {
            EarthCharacterImpact impact = Impact(source, impulse, mass, closingSpeed);

            EarthCharacterImpactResolution compatibility = EarthCharacterImpactSolver.Resolve(
                in impact,
                in Tuning);
            EarthCharacterImpactResolution explicitLegacy = EarthCharacterImpactSolver.Resolve(
                in impact,
                in Tuning,
                ImpactResponseMode.Legacy);

            Assert.That(explicitLegacy.Response, Is.EqualTo(compatibility.Response));
            Assert.That(
                explicitLegacy.EffectiveVelocityChange,
                Is.EqualTo(compatibility.EffectiveVelocityChange).Within(0.000001f));
        }

        [Test]
        public void CalibratedLightStoneFlinchesWithoutMovingTheWholeFighterTooFar()
        {
            EarthCharacterImpact impact = Impact(
                EarthCharacterImpactSourceKind.LooseStone,
                42f,
                42f);

            EarthCharacterImpactResolution result = EarthCharacterImpactSolver.Resolve(
                in impact,
                in Tuning,
                ImpactResponseMode.Calibrated);

            Assert.That(result.Response, Is.EqualTo(EarthCharacterImpactResponse.Flinch));
            Assert.That(result.ReactionVelocityChange, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.AppliedVelocityChange, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(result.EffectiveVelocityChange, Is.EqualTo(result.AppliedVelocityChange));
        }

        [Test]
        public void CalibratedHeavyBoulderCanKnockOutWithoutLaunchingTheRoot()
        {
            EarthCharacterImpact impact = Impact(
                EarthCharacterImpactSourceKind.LooseStone,
                252f,
                42f);

            EarthCharacterImpactResolution result = EarthCharacterImpactSolver.Resolve(
                in impact,
                in Tuning,
                ImpactResponseMode.Calibrated);

            Assert.That(result.Response, Is.EqualTo(EarthCharacterImpactResponse.Knockout));
            Assert.That(result.ReactionVelocityChange, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(result.AppliedVelocityChange, Is.EqualTo(0.9f).Within(0.0001f));
        }

        [Test]
        public void CalibratedPillarWaveUsesReactionAndMovementAsSeparateChannels()
        {
            EarthCharacterImpact impact = Impact(
                EarthCharacterImpactSourceKind.PillarWave,
                105f,
                42f);

            EarthCharacterImpactResolution result = EarthCharacterImpactSolver.Resolve(
                in impact,
                in Tuning,
                ImpactResponseMode.Calibrated);

            Assert.That(result.Response, Is.EqualTo(EarthCharacterImpactResponse.Stagger));
            Assert.That(result.ReactionVelocityChange, Is.EqualTo(3.2f).Within(0.0001f));
            Assert.That(result.AppliedVelocityChange, Is.EqualTo(1.2f).Within(0.0001f));
        }

        [Test]
        public void CalibratedGenericPhysicsStillCannotChooseCombatKnockout()
        {
            EarthCharacterImpact impact = Impact(
                EarthCharacterImpactSourceKind.Physics,
                50000f,
                10f);

            EarthCharacterImpactResolution result = EarthCharacterImpactSolver.Resolve(
                in impact,
                in Tuning,
                ImpactResponseMode.Calibrated);

            Assert.That(result.Response, Is.EqualTo(EarthCharacterImpactResponse.Stagger));
            Assert.That(result.ReactionVelocityChange, Is.LessThan(Tuning.KnockoutVelocityChange));
            Assert.That(result.AppliedVelocityChange, Is.EqualTo(1.5f).Within(0.0001f));
        }

        private static EarthCharacterImpact Impact(
            EarthCharacterImpactSourceKind kind,
            float impulse,
            float mass,
            float closingSpeed = 0f,
            float strength01 = 0f) =>
            new EarthCharacterImpact(
                17u,
                100u,
                kind,
                float3.zero,
                new float3(1f, 0f, 0f),
                impulse,
                mass,
                closingSpeed,
                strength01);
    }
}
