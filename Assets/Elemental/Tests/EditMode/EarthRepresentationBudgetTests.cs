using Elemental.Simulation.Matter;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthRepresentationBudgetTests
    {
        [Test]
        public void ControlledMatterNeverDemotesUnderExtremePressure()
        {
            EarthRepresentationBudget budget = EarthRepresentationBudget.WebLab;
            var candidate = new EarthRepresentationCandidate(
                1000f, 0f, 0f, 0f, true, true, false, 0f);
            var pressure = new EarthRepresentationPressure(500, 500, 5000);
            EarthRepresentationDecision result = EarthRepresentationBudgetSolver.Evaluate(
                in candidate, in pressure, in budget);
            Assert.That(result.Admitted, Is.True);
            Assert.That(result.Tier, Is.EqualTo(EarthRepresentationTier.HeroPhysical));
        }

        [Test]
        public void PressureDegradesSecondaryToVisualThenDormant()
        {
            EarthRepresentationBudget budget = EarthRepresentationBudget.NativeLow;
            var debris = new EarthRepresentationCandidate(
                24f, 0.02f, 1f, 12f, false, false, false, 0.4f);
            var physicalFull = new EarthRepresentationPressure(
                budget.HeroPhysical, budget.SecondaryPhysical, 0);
            EarthRepresentationDecision visual = EarthRepresentationBudgetSolver.Evaluate(
                in debris, in physicalFull, in budget);
            Assert.That(visual.Tier, Is.EqualTo(EarthRepresentationTier.VisualOnlyGpu));

            var allFull = new EarthRepresentationPressure(
                budget.HeroPhysical, budget.SecondaryPhysical, budget.VisualGpu);
            EarthRepresentationDecision dormant = EarthRepresentationBudgetSolver.Evaluate(
                in debris, in allFull, in budget);
            Assert.That(dormant.Tier, Is.EqualTo(EarthRepresentationTier.DormantRecord));
            Assert.That(dormant.Admitted, Is.False);
        }

        [Test]
        public void SolverAllocatesZeroBytesInSteadyState()
        {
            EarthRepresentationBudget budget = EarthRepresentationBudget.NativeHigh;
            var candidate = new EarthRepresentationCandidate(
                8f, 0.12f, 14f, 1600f, true, false, true, 1f);
            var pressure = new EarthRepresentationPressure(12, 28, 80);
            EarthRepresentationBudgetSolver.Evaluate(in candidate, in pressure, in budget);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            EarthRepresentationDecision result = default;
            for (int index = 0; index < 10000; index++)
                result = EarthRepresentationBudgetSolver.Evaluate(in candidate, in pressure, in budget);
            long bytes = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero);
            Assert.That(result.Tier, Is.EqualTo(EarthRepresentationTier.HeroPhysical));
        }
    }
}
