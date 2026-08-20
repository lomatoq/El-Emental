using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>
    /// Deterministic transition math shared by the predictive landing director and
    /// EditMode tests. It never owns gameplay grounding or velocity.
    /// </summary>
    public static class EarthAnimationTransitionSolver
    {
        public static float PredictTimeToContact(
            float clearance,
            float relativeUpSpeed,
            float downwardAcceleration,
            float maximumTime)
        {
            clearance = math.max(0f, clearance);
            maximumTime = math.max(0f, maximumTime);
            if (clearance <= 0.0001f) return 0f;

            downwardAcceleration = math.max(0f, downwardAcceleration);
            if (downwardAcceleration <= 0.0001f)
            {
                if (relativeUpSpeed >= -0.0001f) return float.PositiveInfinity;
                float linearTime = clearance / -relativeUpSpeed;
                return linearTime <= maximumTime ? linearTime : float.PositiveInfinity;
            }

            float discriminant = relativeUpSpeed * relativeUpSpeed +
                                 2f * downwardAcceleration * clearance;
            if (discriminant < 0f) return float.PositiveInfinity;
            float time = (relativeUpSpeed + math.sqrt(discriminant)) / downwardAcceleration;
            return time >= 0f && time <= maximumTime
                ? time
                : float.PositiveInfinity;
        }

        public static float PredictImpactSpeed(
            float relativeUpSpeed,
            float downwardAcceleration,
            float timeToContact)
        {
            if (!math.isfinite(timeToContact) || timeToContact < 0f) return 0f;
            float impactUpSpeed = relativeUpSpeed - math.max(0f, downwardAcceleration) * timeToContact;
            return math.max(0f, -impactUpSpeed);
        }

        public static float LandingAnticipationLead(
            float impactSpeed,
            float minimumLeadSeconds,
            float maximumLeadSeconds,
            float lowImpactSpeed = 2.5f,
            float highImpactSpeed = 12f)
        {
            minimumLeadSeconds = math.max(0f, minimumLeadSeconds);
            maximumLeadSeconds = math.max(minimumLeadSeconds, maximumLeadSeconds);
            float upper = math.max(lowImpactSpeed + 0.001f, highImpactSpeed);
            float amount = (math.max(0f, impactSpeed) - lowImpactSpeed) /
                           math.max(0.001f, upper - lowImpactSpeed);
            return math.lerp(minimumLeadSeconds, maximumLeadSeconds, math.saturate(amount));
        }

        public static bool ShouldAnticipateLanding(
            bool hasCandidate,
            bool alreadyGrounded,
            float relativeUpSpeed,
            float timeToContact,
            float impactSpeed,
            float minimumLeadSeconds,
            float maximumLeadSeconds)
        {
            if (!hasCandidate || alreadyGrounded || !math.isfinite(timeToContact)) return false;
            if (relativeUpSpeed > 0.45f) return false;
            float lead = LandingAnticipationLead(
                impactSpeed,
                minimumLeadSeconds,
                maximumLeadSeconds);
            return timeToContact <= lead;
        }

        public static float LandingRecoverySeconds(
            bool hardLanding,
            float planarSpeed,
            float movingRecoverySeconds,
            float softRecoverySeconds,
            float hardRecoverySeconds)
        {
            if (hardLanding) return math.max(0.02f, hardRecoverySeconds);
            return math.abs(planarSpeed) >= 1.15f
                ? math.max(0.02f, movingRecoverySeconds)
                : math.max(0.02f, softRecoverySeconds);
        }
    }
}
