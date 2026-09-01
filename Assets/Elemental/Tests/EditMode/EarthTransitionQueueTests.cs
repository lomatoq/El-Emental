using System;
using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthTransitionQueueTests
    {
        [Test]
        public void QueueSelectsPriorityThenFifoAndSupportsExplicitCancel()
        {
            EarthTransitionQueue queue = new EarthTransitionQueue(4);
            EarthAnimationTransitionContext firstContext = Context(EarthMotionStateId.Locomotion);
            EarthAnimationTransitionContext secondContext = Context(EarthMotionStateId.HardLanding);
            EarthAnimationTransitionContext thirdContext = Context(EarthMotionStateId.DirectionalDodge);
            EarthTransitionRule firstRule = Rule(EarthAnimationTransitionPriority.Locomotion);
            EarthTransitionRule secondRule = Rule(EarthAnimationTransitionPriority.HeavyImpact);
            EarthTransitionRule thirdRule = Rule(EarthAnimationTransitionPriority.HeavyImpact);

            Assert.That(queue.Enqueue(101, in firstContext, in firstRule, 1f),
                Is.EqualTo(EarthTransitionQueueResult.Enqueued));
            Assert.That(queue.Enqueue(202, in secondContext, in secondRule, 2f),
                Is.EqualTo(EarthTransitionQueueResult.Enqueued));
            Assert.That(queue.Enqueue(303, in thirdContext, in thirdRule, 3f),
                Is.EqualTo(EarthTransitionQueueResult.Enqueued));

            EarthTransitionQueueGate gate = Gate(0.8f);
            Assert.That(queue.TryDequeueEligible(in gate, out EarthQueuedTransition high), Is.True);
            Assert.That(high.DestinationHash, Is.EqualTo(202), "equal priority must remain FIFO");
            Assert.That(queue.CancelDestination(303), Is.True);
            Assert.That(queue.TryDequeueEligible(in gate, out EarthQueuedTransition low), Is.True);
            Assert.That(low.DestinationHash, Is.EqualTo(101));
            Assert.That(queue.Count, Is.Zero);
        }

        [Test]
        public void DuplicateReplacementRetainsOrderAndRejectsLowerPriority()
        {
            EarthTransitionQueue queue = new EarthTransitionQueue(2);
            EarthAnimationTransitionContext context = Context(EarthMotionStateId.Locomotion);
            EarthTransitionRule high = Rule(EarthAnimationTransitionPriority.HeavyImpact);
            EarthTransitionRule low = Rule(EarthAnimationTransitionPriority.Locomotion);

            Assert.That(queue.Enqueue(7, in context, in high, 1f),
                Is.EqualTo(EarthTransitionQueueResult.Enqueued));
            Assert.That(queue.Enqueue(7, in context, in low, 2f),
                Is.EqualTo(EarthTransitionQueueResult.RejectedDuplicateLowerPriority));
            Assert.That(queue.Enqueue(7, in context, in high, 3f),
                Is.EqualTo(EarthTransitionQueueResult.ReplacedDuplicate));
            Assert.That(queue.Count, Is.EqualTo(1));

            EarthTransitionQueueGate gate = Gate(0.8f);
            Assert.That(queue.TryPeekEligible(in gate, out EarthQueuedTransition queued), Is.True);
            Assert.That(queued.RequestedAtSeconds, Is.EqualTo(3f));
            Assert.That(queued.Sequence, Is.EqualTo(1u));
        }

        [Test]
        public void CapacityAndQueueabilityAreBoundedAndExplicit()
        {
            EarthTransitionQueue queue = new EarthTransitionQueue(1);
            EarthAnimationTransitionContext first = Context(EarthMotionStateId.Locomotion);
            EarthAnimationTransitionContext second = Context(EarthMotionStateId.HardLanding);
            EarthTransitionRule queueable = Rule(EarthAnimationTransitionPriority.Locomotion);
            EarthTransitionRule immediateOnly = Rule(
                EarthAnimationTransitionPriority.Locomotion,
                queueWhenBlocked: false);

            Assert.That(queue.Enqueue(1, in first, in immediateOnly, 0f),
                Is.EqualTo(EarthTransitionQueueResult.RejectedNotQueueable));
            Assert.That(queue.Enqueue(1, in first, in queueable, 0f),
                Is.EqualTo(EarthTransitionQueueResult.Enqueued));
            Assert.That(queue.Enqueue(2, in second, in queueable, 0f),
                Is.EqualTo(EarthTransitionQueueResult.RejectedCapacity));
            Assert.That(queue.Capacity, Is.EqualTo(1));
        }

        [Test]
        public void ProtectedWindowReleaseIsEquivalentAtThirtySixtyAndOneTwentyHz()
        {
            float release30 = SimulateRelease(30);
            float release60 = SimulateRelease(60);
            float release120 = SimulateRelease(120);

            Assert.That(release30, Is.GreaterThanOrEqualTo(0.6f));
            Assert.That(release60, Is.GreaterThanOrEqualTo(0.6f));
            Assert.That(release120, Is.GreaterThanOrEqualTo(0.6f));
            Assert.That(Math.Abs(release30 - release60), Is.LessThanOrEqualTo(1f / 30f + 0.0001f));
            Assert.That(Math.Abs(release60 - release120), Is.LessThanOrEqualTo(1f / 60f + 0.0001f));
        }

        [Test]
        public void StaleSourceRequestsAreDiscardedBeforeSelection()
        {
            EarthTransitionQueue queue = new EarthTransitionQueue(2);
            EarthAnimationTransitionContext stale = Context(EarthMotionStateId.Locomotion);
            EarthTransitionRule rule = Rule(EarthAnimationTransitionPriority.Locomotion);
            Assert.That(queue.Enqueue(99, in stale, in rule, 0f),
                Is.EqualTo(EarthTransitionQueueResult.Enqueued));

            EarthTransitionQueueGate gate = new EarthTransitionQueueGate(
                EarthMotionStateId.Fall,
                0.8f,
                EarthAnimationTransitionPriority.Idle,
                true);
            Assert.That(queue.TryDequeueEligible(in gate, out _), Is.False);
            Assert.That(queue.Count, Is.Zero);
        }

        [Test]
        public void HotPeekAndCancelMathAllocatesNoManagedMemory()
        {
            EarthTransitionQueue queue = new EarthTransitionQueue(4);
            EarthAnimationTransitionContext context = Context(EarthMotionStateId.Locomotion);
            EarthTransitionRule rule = Rule(EarthAnimationTransitionPriority.Locomotion);
            queue.Enqueue(5, in context, in rule, 0f);
            EarthTransitionQueueGate gate = Gate(0.8f);
            queue.TryPeekEligible(in gate, out _);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 10000; index++)
            {
                if (!queue.TryPeekEligible(in gate, out EarthQueuedTransition result) ||
                    result.DestinationHash != 5)
                    Assert.Fail("eligible transition changed during allocation window");
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        private static float SimulateRelease(int frequency)
        {
            EarthTransitionQueue queue = new EarthTransitionQueue(1);
            EarthAnimationTransitionContext context = Context(EarthMotionStateId.Locomotion);
            EarthNormalizedAnimationWindow protectedWindow =
                new EarthNormalizedAnimationWindow(true, 0f, 0.6f);
            EarthTransitionRule rule = Rule(
                EarthAnimationTransitionPriority.Locomotion,
                protectedWindow: protectedWindow);
            queue.Enqueue(42, in context, in rule, 0f);

            float elapsed = 0f;
            float step = 1f / frequency;
            for (int frame = 0; frame <= frequency; frame++)
            {
                EarthTransitionQueueGate gate = Gate(elapsed);
                if (queue.TryDequeueEligible(in gate, out EarthQueuedTransition result))
                {
                    Assert.That(result.DestinationHash, Is.EqualTo(42));
                    return elapsed;
                }
                elapsed += step;
            }
            Assert.Fail($"queue did not release at {frequency} Hz");
            return float.NaN;
        }

        private static EarthTransitionQueueGate Gate(float phase) =>
            new EarthTransitionQueueGate(
                EarthMotionStateId.TurnInPlace,
                phase,
                EarthAnimationTransitionPriority.Idle,
                true);

        private static EarthAnimationTransitionContext Context(
            EarthMotionStateId destination) =>
            EarthTransitionRuleTests.Context(destinationState: destination);

        private static EarthTransitionRule Rule(
            EarthAnimationTransitionPriority priority,
            EarthNormalizedAnimationWindow protectedWindow = default,
            bool queueWhenBlocked = true) =>
            EarthTransitionRuleTests.Rule(
                EarthTransitionFamily.PhaseSynchronized,
                priority,
                cancelPolicy: EarthTransitionCancelPolicy.OutsideProtectedWindow,
                protectedWindow: protectedWindow,
                queueWhenBlocked: queueWhenBlocked);
    }
}
