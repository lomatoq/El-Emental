using MotionMatching;
using Unity.Mathematics;

namespace Elemental.Presentation.MotionMatching
{
    /// <summary>
    /// Allocation-free rotation inertialization state shared by the animation job
    /// and pure EditMode tests. The state stores the visible output velocity, so a
    /// second transition can continue the motion that was actually rendered.
    /// </summary>
    public struct EarthRotationInertializationState
    {
        public quaternion PreviousTarget;
        public quaternion PreviousOutput;
        public quaternion OffsetRotation;
        public float3 PreviousOutputAngularVelocity;
        public float3 OffsetAngularVelocity;
        public byte Initialized;
        public byte AwaitingIncomingDerivative;
    }

    public static class EarthRotationInertialization
    {
        private const float MinimumDeltaTime = 0.000001f;
        private const float MinimumHalfLife = 0.01f;

        /// <summary>
        /// Advances one local-space bone. A zero-delta transition captures the
        /// boundary exactly. A rendered transition sample advances the preceding
        /// visible pose by its measured velocity for that frame before capturing
        /// the new offset, so returned poses and reported velocity remain C1.
        /// The target derivative is deliberately deferred until the next sample;
        /// this prevents the discontinuity between two animation states from being
        /// interpreted as an enormous target angular velocity.
        /// </summary>
        public static quaternion Step(
            ref EarthRotationInertializationState state,
            quaternion targetRotation,
            float halfLifeSeconds,
            float deltaTime,
            bool transition,
            bool bypass,
            out float3 outputAngularVelocity)
        {
            quaternion fallback = state.Initialized != 0
                ? state.PreviousOutput
                : quaternion.identity;
            quaternion target = SanitizeRotation(targetRotation, fallback);
            float safeDelta = math.isfinite(deltaTime) && deltaTime > MinimumDeltaTime
                ? deltaTime
                : 0f;
            float halfLife = math.isfinite(halfLifeSeconds)
                ? math.max(MinimumHalfLife, halfLifeSeconds)
                : MinimumHalfLife;

            if (state.Initialized == 0)
            {
                state.PreviousTarget = target;
                state.PreviousOutput = target;
                state.OffsetRotation = quaternion.identity;
                state.PreviousOutputAngularVelocity = float3.zero;
                state.OffsetAngularVelocity = float3.zero;
                state.Initialized = 1;
                state.AwaitingIncomingDerivative = 0;
                outputAngularVelocity = float3.zero;
                return target;
            }

            float3 previousOutputVelocity = SanitizeVelocity(
                state.PreviousOutputAngularVelocity);

            if (bypass)
            {
                // Planted feet and toes remain exact inputs to the dedicated final
                // contact owner, including on a semantic transition frame. Do not
                // let generic inertialization retain one transition sample here.
                float3 bypassVelocity = safeDelta > 0f && !transition
                    ? MeasureAngularVelocity(
                        SanitizeRotation(state.PreviousOutput, target), target, safeDelta)
                    : float3.zero;
                state.PreviousTarget = target;
                state.PreviousOutput = target;
                state.OffsetRotation = quaternion.identity;
                state.PreviousOutputAngularVelocity = bypassVelocity;
                state.OffsetAngularVelocity = float3.zero;
                // Bypass already supplied a real consecutive output sample. It
                // owns neither a stale generic offset nor an incoming-source
                // derivative rebase on the first frame after contact releases.
                state.AwaitingIncomingDerivative = 0;
                outputAngularVelocity = bypassVelocity;
                return target;
            }

            if (transition)
            {
                // PreviousOutput already includes any interrupted offset. Start a
                // fresh transition from that real visible state instead of adding
                // the old offset for a second time.
                state.OffsetRotation = quaternion.identity;
                state.OffsetAngularVelocity = float3.zero;
                quaternion previousSource = SanitizeRotation(state.PreviousOutput, target);
                quaternion source = safeDelta > 0f
                    ? Integrate(previousSource, previousOutputVelocity, safeDelta)
                    : previousSource;
                float3 fallbackTargetVelocity = previousOutputVelocity;
                quaternion offset = state.OffsetRotation;
                float3 offsetVelocity = state.OffsetAngularVelocity;
                Inertialization.InertializeJointTransition(
                    source,
                    previousOutputVelocity,
                    target,
                    fallbackTargetVelocity,
                    ref offset,
                    ref offsetVelocity);
                state.OffsetRotation = SanitizeRotation(offset, quaternion.identity);
                state.OffsetAngularVelocity = SanitizeVelocity(offsetVelocity);
                state.PreviousTarget = target;
                state.PreviousOutput = source;
                state.PreviousOutputAngularVelocity = previousOutputVelocity;
                state.AwaitingIncomingDerivative = 1;
                outputAngularVelocity = previousOutputVelocity;
                return source;
            }

            if (safeDelta <= 0f)
            {
                outputAngularVelocity = previousOutputVelocity;
                return state.PreviousOutput;
            }

            float3 targetVelocity = MeasureAngularVelocity(
                SanitizeRotation(state.PreviousTarget, target), target, safeDelta);

            if (state.AwaitingIncomingDerivative != 0)
            {
                // The current and previous targets are now consecutive samples of
                // the incoming state. Rebase once with that valid derivative while
                // retaining the actual outgoing pose and velocity.
                state.OffsetRotation = quaternion.identity;
                state.OffsetAngularVelocity = float3.zero;
                quaternion offset = state.OffsetRotation;
                float3 offsetVelocity = state.OffsetAngularVelocity;
                Inertialization.InertializeJointTransition(
                    SanitizeRotation(state.PreviousOutput, target),
                    previousOutputVelocity,
                    target,
                    targetVelocity,
                    ref offset,
                    ref offsetVelocity);
                state.OffsetRotation = SanitizeRotation(offset, quaternion.identity);
                state.OffsetAngularVelocity = SanitizeVelocity(offsetVelocity);
                state.AwaitingIncomingDerivative = 0;
            }

            quaternion updatedOffset = state.OffsetRotation;
            float3 updatedOffsetVelocity = state.OffsetAngularVelocity;
            Inertialization.InertializeJointUpdate(
                target,
                targetVelocity,
                halfLife,
                safeDelta,
                ref updatedOffset,
                ref updatedOffsetVelocity,
                out quaternion output,
                out float3 velocity);

            output = SanitizeRotation(output, target);
            velocity = SanitizeVelocity(velocity);
            state.PreviousTarget = target;
            state.PreviousOutput = output;
            state.OffsetRotation = SanitizeRotation(updatedOffset, quaternion.identity);
            state.PreviousOutputAngularVelocity = velocity;
            state.OffsetAngularVelocity = SanitizeVelocity(updatedOffsetVelocity);
            outputAngularVelocity = velocity;
            return output;
        }

        private static quaternion SanitizeRotation(quaternion value, quaternion fallback)
        {
            if (!math.all(math.isfinite(value.value)) ||
                math.lengthsq(value.value) < 0.00000001f)
                value = fallback;
            if (!math.all(math.isfinite(value.value)) ||
                math.lengthsq(value.value) < 0.00000001f)
                value = quaternion.identity;
            return MathExtensions.Abs(math.normalize(value));
        }

        private static float3 SanitizeVelocity(float3 value) =>
            math.all(math.isfinite(value)) ? value : float3.zero;

        private static quaternion Integrate(
            quaternion rotation,
            float3 angularVelocity,
            float deltaTime)
        {
            float3 velocity = SanitizeVelocity(angularVelocity);
            float speed = math.length(velocity);
            if (!math.isfinite(speed) || speed < 0.000001f || deltaTime <= 0f)
                return rotation;
            quaternion delta = quaternion.AxisAngle(velocity / speed, speed * deltaTime);
            return SanitizeRotation(math.mul(delta, rotation), rotation);
        }

        /// <summary>
        /// Stable shortest-path angular velocity. atan2 remains well-conditioned
        /// for the sub-degree deltas produced at 120 Hz; acos(w) loses enough
        /// float precision there to make the estimated physical velocity depend
        /// on frame rate.
        /// </summary>
        public static float3 MeasureAngularVelocity(
            quaternion current,
            quaternion next,
            float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= MinimumDeltaTime)
                return float3.zero;
            quaternion from = SanitizeRotation(current, quaternion.identity);
            quaternion to = SanitizeRotation(next, from);
            quaternion delta = MathExtensions.Abs(math.normalize(
                math.mul(to, math.inverse(from))));
            float3 vector = delta.value.xyz;
            float length = math.length(vector);
            if (!math.isfinite(length)) return float3.zero;
            if (length < 0.000001f)
                return SanitizeVelocity((2f / deltaTime) * vector);
            float halfAngle = math.atan2(length, math.clamp(delta.value.w, -1f, 1f));
            return SanitizeVelocity(vector * ((2f * halfAngle) / (length * deltaTime)));
        }
    }
}
