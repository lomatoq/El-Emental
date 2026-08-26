using System;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMvpBotPlannerTests
    {
        [Test]
        public void LinebreakerLocksWindupDirectionAndPulsesStrikeExactlyOnce()
        {
            EarthMvpBotTuning tuning = TestTuning();
            EarthMvpBotPlannerState state = EarthMvpBotPlannerState.Initial;
            EarthMvpBotFrame frame = Frame(targetPosition: new float3(0f, 0f, 2f));

            EarthMvpBotPlan plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);
            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Windup));
            Assert.That(plan.StrikeThisTick, Is.False);
            float3 locked = plan.State.LockedStrikeDirection;

            state = plan.State;
            frame = Frame(targetPosition: new float3(0.3f, 0f, 1.95f), deltaTime: 0.2f);
            plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);
            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Windup));
            Assert.That(math.distance(plan.State.LockedStrikeDirection, locked), Is.LessThan(0.000001f));

            state = plan.State;
            plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);
            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Strike));
            Assert.That(plan.StrikeThisTick, Is.True);
            Assert.That(math.distance(plan.State.LockedStrikeDirection, locked), Is.LessThan(0.000001f));

            state = plan.State;
            plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);
            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Recover));
            Assert.That(plan.StrikeThisTick, Is.False, "The strike command must be a one-tick pulse.");

            state = plan.State;
            plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);
            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Cooldown));
            Assert.That(plan.StrikeThisTick, Is.False);

            state = plan.State;
            plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);
            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Approach));
            Assert.That(plan.StrikeThisTick, Is.False);
        }

        [Test]
        public void TargetOutsideRangeProducesOnlyTangentApproach()
        {
            EarthMvpBotTuning tuning = TestTuning();
            EarthMvpBotPlannerState state = EarthMvpBotPlannerState.Initial;
            EarthMvpBotFrame frame = Frame(targetPosition: new float3(3f, 1.5f, 4f));

            EarthMvpBotPlan plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);

            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Approach));
            Assert.That(plan.GuardReason, Is.EqualTo(EarthMvpBotGuardReason.TargetOutOfRange));
            Assert.That(math.dot(plan.DesiredMoveDirection, new float3(0f, 1f, 0f)), Is.EqualTo(0f).Within(0.000001f));
            Assert.That(math.length(plan.DesiredMoveDirection), Is.EqualTo(1f).Within(0.000001f));
            Assert.That(plan.StrikeThisTick, Is.False);
        }

        [Test]
        public void ElevatedTargetOutsideFullRangeCannotStartOrCompleteStrike()
        {
            EarthMvpBotTuning tuning = TestTuning();
            EarthMvpBotPlannerState state = EarthMvpBotPlannerState.Initial;
            EarthMvpBotFrame frame = Frame(targetPosition: new float3(0f, 3f, 1f));

            EarthMvpBotPlan approach = EarthMvpBotPlanner.Step(in state, in frame, in tuning);
            Assert.That(approach.State.Phase, Is.EqualTo(EarthMvpBotPhase.Approach));
            Assert.That(approach.GuardReason, Is.EqualTo(EarthMvpBotGuardReason.TargetOutOfRange));
            Assert.That(approach.StrikeThisTick, Is.False);

            state = new EarthMvpBotPlannerState(
                EarthMvpBotPhase.Windup,
                tuning.WindupSeconds,
                new float3(0f, 0f, 1f));
            frame = Frame(targetPosition: new float3(0f, 3f, 1f), deltaTime: 0.5f);
            EarthMvpBotPlan armed = EarthMvpBotPlanner.Step(in state, in frame, in tuning);

            Assert.That(armed.State.Phase, Is.EqualTo(EarthMvpBotPhase.Approach));
            Assert.That(armed.GuardReason, Is.EqualTo(EarthMvpBotGuardReason.TargetOutOfRange));
            Assert.That(armed.StrikeThisTick, Is.False);
        }

        [Test]
        public void TargetInsideRangeButOutsideConeTurnsWithoutAdvancingOrStriking()
        {
            EarthMvpBotTuning tuning = TestTuning();
            EarthMvpBotPlannerState state = EarthMvpBotPlannerState.Initial;
            EarthMvpBotFrame frame = Frame(
                targetPosition: new float3(2f, 0f, 0f),
                selfForward: new float3(0f, 0f, 1f));

            EarthMvpBotPlan plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);

            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Approach));
            Assert.That(plan.GuardReason, Is.EqualTo(EarthMvpBotGuardReason.TargetOutOfCone));
            Assert.That(plan.DesiredMoveDirection, Is.EqualTo(float3.zero));
            Assert.That(math.dot(plan.DesiredFacingDirection, new float3(1f, 0f, 0f)), Is.GreaterThan(0.999f));
            Assert.That(plan.StrikeThisTick, Is.False);
        }

        [Test]
        public void DodgeOutsideLockedConeCancelsStrikeAtEndOfWindup()
        {
            EarthMvpBotTuning tuning = TestTuning();
            EarthMvpBotPlannerState state = EarthMvpBotPlannerState.Initial;
            EarthMvpBotFrame frame = Frame(targetPosition: new float3(0f, 0f, 2f));
            EarthMvpBotPlan plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);

            state = plan.State;
            frame = Frame(targetPosition: new float3(2f, 0f, 0f), deltaTime: 0.5f);
            plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);

            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Approach));
            Assert.That(plan.GuardReason, Is.EqualTo(EarthMvpBotGuardReason.TargetOutOfCone));
            Assert.That(plan.StrikeThisTick, Is.False);
        }

        [Test]
        public void DodgeOutsideRangeCancelsStrikeAtEndOfWindup()
        {
            EarthMvpBotTuning tuning = TestTuning();
            EarthMvpBotPlannerState state = EarthMvpBotPlannerState.Initial;
            EarthMvpBotFrame frame = Frame(targetPosition: new float3(0f, 0f, 2f));
            EarthMvpBotPlan plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);

            state = plan.State;
            frame = Frame(targetPosition: new float3(0f, 0f, 3f), deltaTime: 0.5f);
            plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);

            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Approach));
            Assert.That(plan.GuardReason, Is.EqualTo(EarthMvpBotGuardReason.TargetOutOfRange));
            Assert.That(plan.StrikeThisTick, Is.False);
        }

        [Test]
        public void SelfOutsideArenaCancelsWindupAndReturnsInward()
        {
            EarthMvpBotTuning tuning = TestTuning();
            var state = new EarthMvpBotPlannerState(
                EarthMvpBotPhase.Windup,
                tuning.WindupSeconds,
                new float3(0f, 0f, 1f));
            EarthMvpBotFrame frame = Frame(
                selfPosition: new float3(7f, 0f, 0f),
                targetPosition: new float3(6f, 0f, 0f),
                selfForward: new float3(-1f, 0f, 0f));

            EarthMvpBotPlan plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);

            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Approach));
            Assert.That(plan.GuardReason, Is.EqualTo(EarthMvpBotGuardReason.SelfOutsideArena));
            Assert.That(math.dot(plan.DesiredMoveDirection, new float3(-1f, 0f, 0f)), Is.GreaterThan(0.999f));
            Assert.That(plan.StrikeThisTick, Is.False);
        }

        [Test]
        public void TargetOutsideArenaIsNeverChasedOrStruck()
        {
            EarthMvpBotTuning tuning = TestTuning();
            EarthMvpBotPlannerState state = EarthMvpBotPlannerState.Initial;
            EarthMvpBotFrame frame = Frame(targetPosition: new float3(0f, 0f, 7f));

            EarthMvpBotPlan plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);

            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Approach));
            Assert.That(plan.GuardReason, Is.EqualTo(EarthMvpBotGuardReason.TargetOutsideArena));
            Assert.That(plan.DesiredMoveDirection, Is.EqualTo(float3.zero));
            Assert.That(plan.StrikeThisTick, Is.False);
        }

        [Test]
        public void ArenaLeashRejectsAntipodeAndRadialEscape()
        {
            EarthMvpBotTuning tuning = TestTuning();
            EarthMvpBotPlannerState state = EarthMvpBotPlannerState.Initial;
            var antipode = new EarthMvpBotFrame(
                1f / 60f,
                new float3(-24f, 0f, 0f),
                new float3(0f, 0f, 1f),
                new float3(-23f, 0f, 0f),
                new float3(-1f, 0f, 0f),
                new float3(24f, 0f, 0f));
            var radialEscape = new EarthMvpBotFrame(
                1f / 60f,
                new float3(0f, 34f, 0f),
                new float3(0f, 0f, 1f),
                new float3(0f, 33f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0f, 24f, 0f));

            EarthMvpBotPlan antipodePlan = EarthMvpBotPlanner.Step(in state, in antipode, in tuning);
            EarthMvpBotPlan radialPlan = EarthMvpBotPlanner.Step(in state, in radialEscape, in tuning);

            AssertBoundaryRecovery(antipodePlan, new float3(-1f, 0f, 0f));
            AssertBoundaryRecovery(radialPlan, new float3(0f, 1f, 0f));
        }

        [Test]
        public void SelfOutsideArenaReturnsInwardWhenTargetIsUnavailable()
        {
            EarthMvpBotTuning tuning = TestTuning();
            EarthMvpBotPlannerState state = EarthMvpBotPlannerState.Initial;
            EarthMvpBotFrame frame = Frame(
                selfPosition: new float3(7f, 0f, 0f),
                targetPosition: float3.zero,
                targetAvailable: false);

            EarthMvpBotPlan plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);

            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Approach));
            Assert.That(plan.GuardReason, Is.EqualTo(EarthMvpBotGuardReason.SelfOutsideArena));
            Assert.That(math.dot(plan.DesiredMoveDirection, new float3(-1f, 0f, 0f)), Is.GreaterThan(0.999f));
            Assert.That(plan.StrikeThisTick, Is.False);
        }

        [TestCase(EarthMvpBotBodyState.Staggered)]
        [TestCase(EarthMvpBotBodyState.Ragdolled)]
        [TestCase(EarthMvpBotBodyState.Recovering)]
        [TestCase(EarthMvpBotBodyState.Disabled)]
        public void UnavailableBodyCancelsWindupAndProducesNoCommands(EarthMvpBotBodyState bodyState)
        {
            EarthMvpBotTuning tuning = TestTuning();
            var state = new EarthMvpBotPlannerState(
                EarthMvpBotPhase.Windup,
                tuning.WindupSeconds,
                new float3(0f, 0f, 1f));
            EarthMvpBotFrame frame = Frame(
                targetPosition: new float3(0f, 0f, 2f),
                bodyState: bodyState);

            EarthMvpBotPlan plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);

            AssertDisabled(plan, EarthMvpBotGuardReason.BodyUnavailable);
        }

        [Test]
        public void PlannerAndTargetGuardsCancelPendingStrike()
        {
            EarthMvpBotTuning tuning = TestTuning();
            var state = new EarthMvpBotPlannerState(
                EarthMvpBotPhase.Windup,
                tuning.WindupSeconds,
                new float3(0f, 0f, 1f));
            EarthMvpBotFrame plannerDisabled = Frame(
                targetPosition: new float3(0f, 0f, 2f),
                plannerEnabled: false);
            EarthMvpBotFrame targetUnavailable = Frame(
                targetPosition: new float3(0f, 0f, 2f),
                targetAvailable: false);

            EarthMvpBotPlan disabled = EarthMvpBotPlanner.Step(in state, in plannerDisabled, in tuning);
            EarthMvpBotPlan missing = EarthMvpBotPlanner.Step(in state, in targetUnavailable, in tuning);

            AssertDisabled(disabled, EarthMvpBotGuardReason.PlannerDisabled);
            AssertDisabled(missing, EarthMvpBotGuardReason.TargetUnavailable);
        }

        [Test]
        public void InvalidDataReturnsFiniteDisabledPlan()
        {
            EarthMvpBotPlannerState state = EarthMvpBotPlannerState.Initial;
            EarthMvpBotTuning tuning = TestTuning();
            EarthMvpBotFrame invalidFrame = Frame(targetPosition: new float3(float.NaN, 0f, 2f));

            EarthMvpBotPlan plan = EarthMvpBotPlanner.Step(in state, in invalidFrame, in tuning);
            AssertDisabled(plan, EarthMvpBotGuardReason.InvalidFrame);
            Assert.That(math.all(math.isfinite(plan.DesiredMoveDirection)), Is.True);
            Assert.That(math.all(math.isfinite(plan.DesiredFacingDirection)), Is.True);

            var invalidTuning = new EarthMvpBotTuning(float.NaN, 55f, 6.5f, 0.4f, 0.2f, 0.2f);
            EarthMvpBotFrame validFrame = Frame(targetPosition: new float3(0f, 0f, 2f));
            plan = EarthMvpBotPlanner.Step(in state, in validFrame, in invalidTuning);
            AssertDisabled(plan, EarthMvpBotGuardReason.InvalidTuning);

            EarthMvpBotFrame overflowFrame = Frame(
                selfPosition: new float3(float.MaxValue, 0f, 0f),
                targetPosition: new float3(-float.MaxValue, 0f, 0f));
            plan = EarthMvpBotPlanner.Step(in state, in overflowFrame, in tuning);
            AssertDisabled(plan, EarthMvpBotGuardReason.InvalidFrame);

            var overflowState = new EarthMvpBotPlannerState(
                EarthMvpBotPhase.Windup,
                0.1f,
                new float3(float.MaxValue, float.MaxValue, 0f));
            plan = EarthMvpBotPlanner.Step(in overflowState, in validFrame, in tuning);
            AssertDisabled(plan, EarthMvpBotGuardReason.InvalidState);
            Assert.That(math.all(math.isfinite(plan.DesiredMoveDirection)), Is.True);
            Assert.That(math.all(math.isfinite(plan.DesiredFacingDirection)), Is.True);
        }

        [Test]
        public void IdenticalFixedTickInputsProduceIdenticalPlans()
        {
            EarthMvpBotTuning tuning = TestTuning();
            EarthMvpBotPlannerState first = EarthMvpBotPlannerState.Initial;
            EarthMvpBotPlannerState second = EarthMvpBotPlannerState.Initial;
            EarthMvpBotFrame frame = Frame(targetPosition: new float3(0.2f, 0f, 2f), deltaTime: 1f / 60f);

            for (int tick = 0; tick < 1000; tick++)
            {
                EarthMvpBotPlan a = EarthMvpBotPlanner.Step(in first, in frame, in tuning);
                EarthMvpBotPlan b = EarthMvpBotPlanner.Step(in second, in frame, in tuning);
                Assert.That(a.State.Phase, Is.EqualTo(b.State.Phase));
                Assert.That(a.State.PhaseSeconds, Is.EqualTo(b.State.PhaseSeconds));
                Assert.That(a.State.LockedStrikeDirection, Is.EqualTo(b.State.LockedStrikeDirection));
                Assert.That(a.DesiredMoveDirection, Is.EqualTo(b.DesiredMoveDirection));
                Assert.That(a.DesiredFacingDirection, Is.EqualTo(b.DesiredFacingDirection));
                Assert.That(a.StrikeThisTick, Is.EqualTo(b.StrikeThisTick));
                Assert.That(a.GuardReason, Is.EqualTo(b.GuardReason));
                first = a.State;
                second = b.State;
            }
        }

        [Test]
        public void SteadyStateStepAllocatesZeroManagedBytes()
        {
            EarthMvpBotTuning tuning = TestTuning();
            EarthMvpBotFrame frame = Frame(targetPosition: new float3(0f, 0f, 2f), deltaTime: 1f / 60f);
            EarthMvpBotPlannerState state = EarthMvpBotPlannerState.Initial;
            for (int tick = 0; tick < 128; tick++)
            {
                EarthMvpBotPlan warmup = EarthMvpBotPlanner.Step(in state, in frame, in tuning);
                state = warmup.State;
            }

            int strikePulses = 0;
            int strikeEntries = 0;
            EarthMvpBotPhase previousPhase = state.Phase;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int tick = 0; tick < 10000; tick++)
            {
                EarthMvpBotPlan plan = EarthMvpBotPlanner.Step(in state, in frame, in tuning);
                if (plan.StrikeThisTick) strikePulses++;
                if (plan.State.Phase == EarthMvpBotPhase.Strike && previousPhase != EarthMvpBotPhase.Strike)
                    strikeEntries++;
                previousPhase = plan.State.Phase;
                state = plan.State;
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(after - before, Is.EqualTo(0L));
            Assert.That(strikePulses, Is.GreaterThan(0));
            Assert.That(strikePulses, Is.EqualTo(strikeEntries),
                "Every completed combat cycle must emit exactly one strike pulse.");
        }

        private static EarthMvpBotTuning TestTuning() => new EarthMvpBotTuning(
            attackRange: 2.25f,
            attackConeDegrees: 55f,
            arenaRadius: 6.5f,
            windupSeconds: 0.3f,
            recoverSeconds: 0.2f,
            cooldownSeconds: 0.2f);

        private static EarthMvpBotFrame Frame(
            float3? selfPosition = null,
            float3? selfForward = null,
            float3? targetPosition = null,
            float deltaTime = 1f / 60f,
            bool plannerEnabled = true,
            bool targetAvailable = true,
            EarthMvpBotBodyState bodyState = EarthMvpBotBodyState.Ready)
        {
            return new EarthMvpBotFrame(
                deltaTime,
                selfPosition ?? float3.zero,
                selfForward ?? new float3(0f, 0f, 1f),
                targetPosition ?? new float3(0f, 0f, 2f),
                new float3(0f, 1f, 0f),
                float3.zero,
                plannerEnabled,
                targetAvailable,
                bodyState);
        }

        private static void AssertDisabled(EarthMvpBotPlan plan, EarthMvpBotGuardReason reason)
        {
            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Disabled));
            Assert.That(plan.GuardReason, Is.EqualTo(reason));
            Assert.That(plan.DesiredMoveDirection, Is.EqualTo(float3.zero));
            Assert.That(plan.DesiredFacingDirection, Is.EqualTo(float3.zero));
            Assert.That(plan.StrikeThisTick, Is.False);
        }

        private static void AssertBoundaryRecovery(EarthMvpBotPlan plan, float3 localUp)
        {
            Assert.That(plan.State.Phase, Is.EqualTo(EarthMvpBotPhase.Approach));
            Assert.That(plan.GuardReason, Is.EqualTo(EarthMvpBotGuardReason.SelfOutsideArena));
            Assert.That(math.all(math.isfinite(plan.DesiredMoveDirection)), Is.True);
            Assert.That(math.length(plan.DesiredMoveDirection), Is.EqualTo(1f).Within(0.000001f));
            Assert.That(math.abs(math.dot(plan.DesiredMoveDirection, localUp)), Is.LessThan(0.000001f));
            Assert.That(plan.StrikeThisTick, Is.False);
        }
    }
}
