using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public readonly struct EarthRecoveryAnimatorContinuityResult
    {
        public EarthRecoveryAnimatorContinuityResult(
            bool hashMatches,
            bool timingIsValid,
            float measuredAdvance,
            float allowedAdvance,
            float normalizedRate,
            float evaluationLeadSeconds,
            float effectiveElapsedSeconds)
        {
            HashMatches = hashMatches;
            TimingIsValid = timingIsValid;
            MeasuredAdvance = measuredAdvance;
            AllowedAdvance = allowedAdvance;
            NormalizedRate = normalizedRate;
            EvaluationLeadSeconds = evaluationLeadSeconds;
            EffectiveElapsedSeconds = effectiveElapsedSeconds;
        }

        public bool HashMatches { get; }
        public bool TimingIsValid { get; }
        public float MeasuredAdvance { get; }
        public float AllowedAdvance { get; }
        public float NormalizedRate { get; }
        public float EvaluationLeadSeconds { get; }
        public float EffectiveElapsedSeconds { get; }
        public bool IsValid => HashMatches && TimingIsValid;
    }

    public static class EarthRecoveryAnimatorContinuityGate
    {
        public const float DefaultPhaseSlack = 0.015f;
        public const float MaximumEvaluationLeadSeconds = 1f / 15f;

        public static EarthRecoveryAnimatorContinuityResult Evaluate(
            int expectedStateHash,
            int observedStateHash,
            float expectedEntryPhase,
            float observedPhase,
            float elapsedSeconds,
            float stateLengthSeconds,
            float stateSpeed,
            float stateSpeedMultiplier,
            bool stateLoops,
            float evaluationLeadSeconds = 0f,
            float phaseSlack = DefaultPhaseSlack)
        {
            bool finite = math.isfinite(expectedEntryPhase) &&
                          math.isfinite(observedPhase) &&
                          math.isfinite(elapsedSeconds) &&
                          math.isfinite(stateLengthSeconds) &&
                          math.isfinite(stateSpeed) &&
                          math.isfinite(stateSpeedMultiplier) &&
                          math.isfinite(evaluationLeadSeconds) &&
                          math.isfinite(phaseSlack);
            if (!finite || expectedStateHash == 0 || stateLengthSeconds <= 0.0001f ||
                elapsedSeconds < 0f || evaluationLeadSeconds < 0f || phaseSlack < 0f)
            {
                return new EarthRecoveryAnimatorContinuityResult(
                    expectedStateHash == observedStateHash,
                    false,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f);
            }

            float playbackSpeed = stateSpeed * stateSpeedMultiplier;
            if (!math.isfinite(playbackSpeed) || playbackSpeed < 0f)
            {
                return new EarthRecoveryAnimatorContinuityResult(
                    expectedStateHash == observedStateHash,
                    false,
                    0f,
                    phaseSlack,
                    0f,
                    0f,
                    elapsedSeconds);
            }

            float normalizedRate = playbackSpeed / stateLengthSeconds;
            float appliedEvaluationLeadSeconds = math.min(
                evaluationLeadSeconds,
                MaximumEvaluationLeadSeconds);
            float effectiveElapsedSeconds = elapsedSeconds + appliedEvaluationLeadSeconds;
            float allowedAdvance = effectiveElapsedSeconds * normalizedRate + phaseSlack;
            float measuredAdvance = observedPhase - expectedEntryPhase;
            if (stateLoops && measuredAdvance < -phaseSlack)
                measuredAdvance += 1f;

            bool timingIsValid = measuredAdvance >= -phaseSlack &&
                                 measuredAdvance <= allowedAdvance;
            return new EarthRecoveryAnimatorContinuityResult(
                expectedStateHash == observedStateHash,
                timingIsValid,
                measuredAdvance,
                allowedAdvance,
                normalizedRate,
                appliedEvaluationLeadSeconds,
                effectiveElapsedSeconds);
        }
    }
}
