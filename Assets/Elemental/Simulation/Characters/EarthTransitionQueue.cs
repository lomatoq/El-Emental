namespace Elemental.Simulation.Characters
{
    public enum EarthTransitionQueueResult : byte
    {
        Enqueued = 0,
        ReplacedDuplicate = 1,
        RejectedInvalid = 2,
        RejectedNotQueueable = 3,
        RejectedDuplicateLowerPriority = 4,
        RejectedCapacity = 5
    }

    public readonly struct EarthTransitionQueueGate
    {
        public EarthTransitionQueueGate(
            EarthMotionStateId sourceState,
            float sourceNormalizedTime,
            EarthAnimationTransitionPriority activePriority,
            bool mayInterruptSource)
        {
            SourceState = sourceState;
            SourceNormalizedTime = sourceNormalizedTime;
            ActivePriority = activePriority;
            MayInterruptSource = mayInterruptSource;
        }

        public EarthMotionStateId SourceState { get; }
        public float SourceNormalizedTime { get; }
        public EarthAnimationTransitionPriority ActivePriority { get; }
        public bool MayInterruptSource { get; }
    }

    public readonly struct EarthQueuedTransition
    {
        public EarthQueuedTransition(
            int destinationHash,
            in EarthAnimationTransitionContext context,
            in EarthTransitionRule rule,
            float requestedAtSeconds,
            uint sequence)
        {
            DestinationHash = destinationHash;
            Context = context;
            Rule = rule;
            RequestedAtSeconds = requestedAtSeconds;
            Sequence = sequence;
        }

        public int DestinationHash { get; }
        public EarthAnimationTransitionContext Context { get; }
        public EarthTransitionRule Rule { get; }
        public float RequestedAtSeconds { get; }
        public uint Sequence { get; }
    }

    /// <summary>
    /// Fixed-capacity, allocation-free scheduler used only by EarthTransitionDirector.
    /// It does not own Animator state and cannot execute a transition itself.
    /// </summary>
    public sealed class EarthTransitionQueue
    {
        public const int DefaultCapacity = 8;
        public const int MaximumCapacity = 32;

        private readonly EarthQueuedTransition[] _entries;
        private int _count;
        private uint _nextSequence;

        public EarthTransitionQueue(int capacity = DefaultCapacity)
        {
            int boundedCapacity = capacity < 1
                ? 1
                : capacity > MaximumCapacity
                    ? MaximumCapacity
                    : capacity;
            _entries = new EarthQueuedTransition[boundedCapacity];
        }

        public int Count => _count;
        public int Capacity => _entries.Length;

        public EarthTransitionQueueResult Enqueue(
            int destinationHash,
            in EarthAnimationTransitionContext context,
            in EarthTransitionRule rule,
            float requestedAtSeconds,
            int capacityLimit = MaximumCapacity)
        {
            if (destinationHash == 0 || !rule.Configured ||
                context.DestinationState == EarthMotionStateId.None)
                return EarthTransitionQueueResult.RejectedInvalid;
            if (!rule.QueueWhenBlocked)
                return EarthTransitionQueueResult.RejectedNotQueueable;

            for (int index = 0; index < _count; index++)
            {
                EarthQueuedTransition existing = _entries[index];
                if (existing.DestinationHash != destinationHash ||
                    existing.Context.SourceState != context.SourceState)
                    continue;
                if (existing.Rule.Priority > rule.Priority)
                    return EarthTransitionQueueResult.RejectedDuplicateLowerPriority;

                _entries[index] = new EarthQueuedTransition(
                    destinationHash,
                    in context,
                    in rule,
                    requestedAtSeconds,
                    existing.Sequence);
                return EarthTransitionQueueResult.ReplacedDuplicate;
            }

            int boundedCapacity = capacityLimit < 1
                ? 1
                : capacityLimit > _entries.Length
                    ? _entries.Length
                    : capacityLimit;
            if (_count >= boundedCapacity)
                return EarthTransitionQueueResult.RejectedCapacity;

            _nextSequence = _nextSequence == uint.MaxValue ? 1u : _nextSequence + 1u;
            _entries[_count++] = new EarthQueuedTransition(
                destinationHash,
                in context,
                in rule,
                requestedAtSeconds,
                _nextSequence);
            return EarthTransitionQueueResult.Enqueued;
        }

        public bool TryPeekEligible(
            in EarthTransitionQueueGate gate,
            out EarthQueuedTransition transition)
        {
            int bestIndex = FindBestEligible(in gate);
            if (bestIndex < 0)
            {
                transition = default;
                return false;
            }

            transition = _entries[bestIndex];
            return true;
        }

        public bool TryDequeueEligible(
            in EarthTransitionQueueGate gate,
            out EarthQueuedTransition transition)
        {
            RemoveStaleSources(gate.SourceState);
            int bestIndex = FindBestEligible(in gate);
            if (bestIndex < 0)
            {
                transition = default;
                return false;
            }

            transition = _entries[bestIndex];
            RemoveAt(bestIndex);
            return true;
        }

        public bool CancelDestination(int destinationHash)
        {
            for (int index = 0; index < _count; index++)
            {
                if (_entries[index].DestinationHash != destinationHash) continue;
                RemoveAt(index);
                return true;
            }
            return false;
        }

        public int CancelAtOrBelow(EarthAnimationTransitionPriority priority)
        {
            int removed = 0;
            for (int index = _count - 1; index >= 0; index--)
            {
                if (_entries[index].Rule.Priority > priority) continue;
                RemoveAt(index);
                removed++;
            }
            return removed;
        }

        public void Clear()
        {
            for (int index = 0; index < _count; index++)
                _entries[index] = default;
            _count = 0;
        }

        private int FindBestEligible(in EarthTransitionQueueGate gate)
        {
            int bestIndex = -1;
            for (int index = 0; index < _count; index++)
            {
                EarthQueuedTransition candidate = _entries[index];
                if (gate.SourceState != EarthMotionStateId.None &&
                    candidate.Context.SourceState != gate.SourceState)
                    continue;
                EarthTransitionRule candidateRule = candidate.Rule;
                if (EarthTransitionRulePolicy.ResolveInterruptReason(
                        in candidateRule,
                        gate.SourceNormalizedTime,
                        gate.MayInterruptSource,
                        gate.ActivePriority) != EarthAnimationTransitionReason.Accepted)
                    continue;
                if (bestIndex < 0 || IsHigherRank(candidate, _entries[bestIndex]))
                    bestIndex = index;
            }
            return bestIndex;
        }

        private void RemoveStaleSources(EarthMotionStateId sourceState)
        {
            if (sourceState == EarthMotionStateId.None) return;
            for (int index = _count - 1; index >= 0; index--)
            {
                if (_entries[index].Context.SourceState == sourceState) continue;
                RemoveAt(index);
            }
        }

        private void RemoveAt(int index)
        {
            int last = _count - 1;
            for (int move = index; move < last; move++)
                _entries[move] = _entries[move + 1];
            _entries[last] = default;
            _count = last;
        }

        private static bool IsHigherRank(
            in EarthQueuedTransition candidate,
            in EarthQueuedTransition incumbent) =>
            candidate.Rule.Priority > incumbent.Rule.Priority ||
            (candidate.Rule.Priority == incumbent.Rule.Priority &&
             candidate.Sequence < incumbent.Sequence);
    }
}
