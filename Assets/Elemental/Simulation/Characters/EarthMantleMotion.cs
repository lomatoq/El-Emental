using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthMantlePhase : byte { None, Reach, Raise, Transfer, Settle }

    /// <summary>Pure lift-then-transfer path; the motor owns collision and support validation.</summary>
    public static class EarthMantleMotion
    {
        public const float MinimumAirborneCatchRelativeUpSpeed = -3.25f;
        public const float MaximumAirborneCatchRelativeUpSpeed = 6.5f;
        public const float AirborneCatchReachBonus = .35f;

        public static float ResolveWallProbeDistance(float radius, float authoredReach, bool airborneCatch) =>
            math.max(.01f, radius) + math.max(0f, authoredReach) +
            (airborneCatch ? AirborneCatchReachBonus : 0f);

        public static EarthMantlePhase Phase(float progress) => progress < .12f ? EarthMantlePhase.Reach :
            progress < .60f ? EarthMantlePhase.Raise : progress < .94f ? EarthMantlePhase.Transfer : EarthMantlePhase.Settle;

        public static bool CanStart(float intent, bool supported, bool protectedAction,
            float height, float minimumHeight, float maximumHeight, float topUpDot, float slopeCosine,
            bool destinationClear) => math.isfinite(height) && intent >= .6f && supported &&
            !protectedAction && height >= minimumHeight && height <= maximumHeight &&
            topUpDot >= slopeCosine && destinationClear;

        public static bool CanCatchAirborne(
            float intent,
            bool protectedAction,
            float relativeUpSpeed,
            float height,
            float minimumHeight,
            float maximumHeight,
            float topUpDot,
            float slopeCosine,
            bool destinationClear) =>
            math.isfinite(relativeUpSpeed) && math.isfinite(height) &&
            intent >= .6f && !protectedAction &&
            relativeUpSpeed >= MinimumAirborneCatchRelativeUpSpeed &&
            relativeUpSpeed <= MaximumAirborneCatchRelativeUpSpeed &&
            height >= minimumHeight && height <= maximumHeight &&
            topUpDot >= slopeCosine && destinationClear;

        public static float3 Evaluate(float3 start, float3 end, float3 up, float progress)
        {
            up = math.normalizesafe(up, new float3(0, 1, 0));
            float3 travel = end-start;
            float height = math.dot(travel, up);
            float3 planarTravel = travel-up*height;
            // Pull the body toward the ledge near the top of the rise. Beginning
            // that transfer at ground level intersects the wall; delaying it
            // until the capsule is mostly above the lip keeps the swept path clear
            // while still bringing the shoulders into hand-contact range.
            // By the end of Raise the shoulders must be close enough for a
            // normally proportioned Humanoid arm to make real ledge contact.
            // The transfer still begins only after most of the vertical lift,
            // so the motor's capsule sweep cannot cut through the wall.
            const float RaisedPlanarTransfer = .55f;
            float3 lifted = start + up*(height+.06f) + planarTravel*RaisedPlanarTransfer;
            float3 aboveEnd = end + up*.06f;
            float t = math.saturate(progress);
            if (t <= .12f) return start;
            if (t < .60f)
            {
                float raise = Smooth((t-.12f)/.48f);
                float transfer = RaisedPlanarTransfer*Smooth(math.saturate((t-.38f)/.22f));
                return start + up*(height+.06f)*raise + planarTravel*transfer;
            }
            if (t < .94f) return math.lerp(lifted, aboveEnd, Smooth((t-.60f)/.34f));
            return math.lerp(aboveEnd, end, Smooth((t-.94f)/.06f));
        }

        private static float Smooth(float t) => t*t*(3f-2f*t);
    }
}
