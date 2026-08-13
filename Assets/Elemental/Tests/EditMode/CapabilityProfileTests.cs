using System;
using Elemental.Simulation.Capabilities;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class CapabilityProfileTests
    {
        [Test]
        public void ProfilesHaveStrictDescendingBudgets()
        {
            CapabilityProfileData high = CapabilityProfileData.NativeHigh;
            CapabilityProfileData low = CapabilityProfileData.NativeLow;
            CapabilityProfileData web = CapabilityProfileData.WebLab;
            Assert.That(high.Budgets.ActiveChunks, Is.GreaterThan(low.Budgets.ActiveChunks));
            Assert.That(low.Budgets.ActiveChunks, Is.GreaterThan(web.Budgets.ActiveChunks));
            Assert.That(high.Budgets.VfxParticles, Is.GreaterThan(low.Budgets.VfxParticles));
            Assert.That(low.Budgets.VfxParticles, Is.GreaterThan(web.Budgets.VfxParticles));
            Assert.That(web.SupportsCompute, Is.False);
            Assert.That(web.SupportsThreadedJobs, Is.False);
        }

        [Test]
        public void SchedulerDegradesPresentationFirst()
        {
            CapabilityProfileData profile = CapabilityProfileData.WebLab;
            var scheduler = new AdaptiveBudgetScheduler(in profile);
            var pressure = new BudgetPressure(1.5f, 0.8f, 0.9f, 0.7f);
            DegradationDecision decision = scheduler.Evaluate(in pressure);
            Assert.That(decision.Kind, Is.EqualTo(DegradationKind.ReducePresentation));
            Assert.That(decision.PresentationScale, Is.LessThan(1f));
            Assert.That(decision.DistantScale, Is.EqualTo(1f));
            Assert.That(decision.CanonicalActiveRulesChanged, Is.False);
        }

        [Test]
        public void SchedulerDegradesDistantSimulationSecond()
        {
            CapabilityProfileData profile = CapabilityProfileData.NativeLow;
            var scheduler = new AdaptiveBudgetScheduler(in profile);
            var pressure = new BudgetPressure(0.8f, 1.6f, 0.9f, 0.7f);
            DegradationDecision decision = scheduler.Evaluate(in pressure);
            Assert.That(decision.Kind, Is.EqualTo(DegradationKind.ReduceDistantSimulation));
            Assert.That(decision.DistantScale, Is.LessThan(1f));
            Assert.That(decision.CanonicalActiveRulesChanged, Is.False);
        }

        [Test]
        public void ActiveGameplayPressureNeverSilentlyChangesCanonicalRules()
        {
            CapabilityProfileData profile = CapabilityProfileData.WebLab;
            var scheduler = new AdaptiveBudgetScheduler(in profile);
            var pressure = new BudgetPressure(0.5f, 0.5f, 2f, 0.5f);
            DegradationDecision decision = scheduler.Evaluate(in pressure);
            Assert.That(decision.Kind, Is.EqualTo(DegradationKind.RejectNewDistantWork));
            Assert.That(decision.CanonicalActiveRulesChanged, Is.False);
            Assert.That(decision.Reason, Does.Contain("protected"));
        }

        [Test]
        public void SamePressureDecisionIsRepeatableAcrossProfiles()
        {
            var pressure = new BudgetPressure(0.7f, 0.8f, 0.9f, 0.6f);
            CapabilityProfileData native = CapabilityProfileData.NativeHigh;
            CapabilityProfileData web = CapabilityProfileData.WebLab;
            DegradationDecision a = new AdaptiveBudgetScheduler(in native).Evaluate(in pressure);
            DegradationDecision b = new AdaptiveBudgetScheduler(in web).Evaluate(in pressure);
            Assert.That(b.Kind, Is.EqualTo(a.Kind));
            Assert.That(b.CanonicalActiveRulesChanged, Is.EqualTo(a.CanonicalActiveRulesChanged));
        }

        [Test]
        public void SixtySimulatedMinutesOfBudgetEvaluationHasZeroSteadyStateAllocation()
        {
            CapabilityProfileData profile = CapabilityProfileData.WebLab;
            var scheduler = new AdaptiveBudgetScheduler(in profile);
            var pressure = new BudgetPressure(0.75f, 0.82f, 0.9f, 0.7f);
            scheduler.Evaluate(in pressure);
            long before = GC.GetAllocatedBytesForCurrentThread();

            DegradationDecision decision = default;
            for (int tick = 0; tick < 60 * 60 * 60; tick++)
                decision = scheduler.Evaluate(in pressure);

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(decision.Kind, Is.EqualTo(DegradationKind.None));
            Assert.That(decision.CanonicalActiveRulesChanged, Is.False);
        }
    }
}
