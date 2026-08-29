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
