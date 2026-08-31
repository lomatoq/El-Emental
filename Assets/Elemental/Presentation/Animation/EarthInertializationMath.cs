using Unity.Mathematics;

namespace Elemental.Presentation.Animation
{
    public struct EarthInertializationVectorState
    {
        public float3 Offset;
        public float3 Velocity;
    }

    public static class EarthInertializationMath
    {
        public const float MinimumHalfLifeSeconds = 1f / 240f;
        public const float MaximumStepSeconds = 0.1f;
        private const float Ln2 = 0.6931471805599453f;
        private const float Epsilon = 1e-8f;

        public static EarthInertializationVectorState ComposePosition(
            float3 sourceOutput,
            float3 sourceVelocity,
            float3 destination,
            float3 destinationVelocity,
            float maximumOffset,
            float maximumVelocity)
        {
            return new EarthInertializationVectorState
            {
                Offset = ClampMagnitude(
                    Sanitize(sourceOutput) - Sanitize(destination),
                    math.max(0f, maximumOffset)),
                Velocity = ClampMagnitude(
                    Sanitize(sourceVelocity) - Sanitize(destinationVelocity),
                    math.max(0f, maximumVelocity))
            };
        }

        public static EarthInertializationVectorState ComposeRotation(
            quaternion sourceOutput,
            float3 sourceAngularVelocity,
            quaternion destination,
            float3 destinationAngularVelocity,
            float maximumAngleRadians,
            float maximumAngularVelocity)
        {
            quaternion source = Sanitize(sourceOutput);
            quaternion target = Sanitize(destination);
            quaternion delta = Shortest(math.mul(source, math.inverse(target)));
            return new EarthInertializationVectorState
            {
                Offset = ClampMagnitude(
                    ToRotationVector(delta),
                    math.max(0f, maximumAngleRadians)),
                Velocity = ClampMagnitude(
                    Sanitize(sourceAngularVelocity) - Sanitize(destinationAngularVelocity),
                    math.max(0f, maximumAngularVelocity))
            };
        }

        public static void StepCriticallyDamped(
            ref EarthInertializationVectorState state,
            float halfLifeSeconds,
            float deltaTime)
        {
            float dt = math.clamp(SanitizeScalar(deltaTime, 0f), 0f, MaximumStepSeconds);
            float halfLife = math.max(
                MinimumHalfLifeSeconds,
                SanitizeScalar(halfLifeSeconds, MinimumHalfLifeSeconds));
            float decay = Ln2 / halfLife;
            float3 offset = Sanitize(state.Offset);
            float3 velocity = Sanitize(state.Velocity);
            float3 coupling = velocity + decay * offset;
            float exponential = math.exp(-decay * dt);
            state.Offset = Sanitize((offset + coupling * dt) * exponential);
            state.Velocity = Sanitize((velocity - decay * coupling * dt) * exponential);
        }

        public static float3 ApplyPosition(float3 destination, in EarthInertializationVectorState state) =>
            Sanitize(destination) + Sanitize(state.Offset);

        public static quaternion ApplyRotation(
            quaternion destination,
            in EarthInertializationVectorState state) =>
            Sanitize(math.mul(FromRotationVector(state.Offset), Sanitize(destination)));

        public static float3 LinearVelocity(float3 current, float3 previous, float deltaTime)
        {
            float dt = SanitizeScalar(deltaTime, 0f);
            return dt > Epsilon
                ? Sanitize((Sanitize(current) - Sanitize(previous)) / dt)
                : float3.zero;
        }

        public static float3 AngularVelocity(
            quaternion current,
            quaternion previous,
            float deltaTime)
        {
            float dt = SanitizeScalar(deltaTime, 0f);
            if (dt <= Epsilon) return float3.zero;
            quaternion delta = Shortest(math.mul(Sanitize(current), math.inverse(Sanitize(previous))));
            return Sanitize(ToRotationVector(delta) / dt);
        }

        public static quaternion Shortest(quaternion value)
        {
            quaternion result = Sanitize(value);
            return result.value.w < 0f ? new quaternion(-result.value) : result;
        }

        public static float3 ToRotationVector(quaternion value)
        {
            quaternion shortest = Shortest(value);
            float3 imaginary = shortest.value.xyz;
            float imaginaryLength = math.length(imaginary);
            if (imaginaryLength <= Epsilon) return Sanitize(imaginary * 2f);
            float angle = 2f * math.atan2(imaginaryLength, math.clamp(shortest.value.w, -1f, 1f));
            return Sanitize(imaginary * (angle / imaginaryLength));
        }

        public static quaternion FromRotationVector(float3 value)
        {
            float3 rotation = Sanitize(value);
            float angle = math.length(rotation);
            if (angle <= Epsilon)
                return Sanitize(new quaternion(new float4(rotation * 0.5f, 1f)));
            float halfAngle = angle * 0.5f;
            float scale = math.sin(halfAngle) / angle;
            return Sanitize(new quaternion(new float4(rotation * scale, math.cos(halfAngle))));
        }

        public static bool IsFinite(float3 value) => math.all(math.isfinite(value));

        public static bool IsFinite(quaternion value) => math.all(math.isfinite(value.value));

        public static float3 Sanitize(float3 value) => IsFinite(value) ? value : float3.zero;

        public static quaternion Sanitize(quaternion value)
        {
            float lengthSquared = math.lengthsq(value.value);
            return IsFinite(value) && lengthSquared > Epsilon
                ? math.normalize(value)
                : quaternion.identity;
        }

        private static float3 ClampMagnitude(float3 value, float maximum)
        {
            float lengthSquared = math.lengthsq(value);
            if (!math.isfinite(lengthSquared) || maximum <= 0f) return float3.zero;
            float maximumSquared = maximum * maximum;
            return lengthSquared > maximumSquared
                ? value * (maximum * math.rsqrt(lengthSquared))
                : value;
        }

        private static float SanitizeScalar(float value, float fallback) =>
            math.isfinite(value) ? value : fallback;
    }
}
