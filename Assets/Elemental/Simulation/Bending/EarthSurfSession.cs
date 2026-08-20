namespace Elemental.Simulation.Bending
{
    public enum EarthSurfSilhouetteFamily : byte
    {
        MantaSlab = 0,
        CrescentPlough = 1,
        SplitRail = 2,
        BrokenWedge = 3
    }

    public readonly struct EarthSurfControlSample
    {
        public EarthSurfControlSample(float bankDegrees, float ramp01, float brake01, float speedMultiplier)
        {
            BankDegrees = bankDegrees;
            Ramp01 = ramp01;
            Brake01 = brake01;
            SpeedMultiplier = speedMultiplier;
        }

        public float BankDegrees { get; }
        public float Ramp01 { get; }
        public float Brake01 { get; }
        public float SpeedMultiplier { get; }
    }

    public static class EarthSurfControlSolver
    {
        public static EarthSurfSilhouetteFamily SelectFamily(uint seed, EarthSurfSilhouetteFamily previous)
        {
            uint value = Hash(seed == 0u ? 1u : seed);
            EarthSurfSilhouetteFamily selected = (EarthSurfSilhouetteFamily)(value % 4u);
            if (selected == previous) selected = (EarthSurfSilhouetteFamily)(((int)selected + 1 + (int)(value % 2u)) % 4);
            return selected;
        }

        public static EarthSurfControlSample Solve(float steer, float wheel, float currentRamp01,
            float currentBrake01, float deltaSeconds)
        {
            float delta = Max(0f, deltaSeconds);
            float rampTarget = wheel > 0.01f ? 1f : 0f;
            float brakeTarget = wheel < -0.01f ? 1f : 0f;
            float ramp = MoveTowards(currentRamp01, rampTarget, delta * (rampTarget > 0f ? 5.8f : 2.7f));
            float brake = MoveTowards(currentBrake01, brakeTarget, delta * (brakeTarget > 0f ? 7f : 3.4f));
            float bank = Clamp(steer, -1f, 1f) * 11f;
            float speedMultiplier = Lerp(1f, 0.38f, brake);
            return new EarthSurfControlSample(bank, ramp, brake, speedMultiplier);
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private static float MoveTowards(float current, float target, float maximumDelta) =>
            current < target ? Min(current + maximumDelta, target) : Max(current - maximumDelta, target);
        private static float Clamp(float value, float minimum, float maximum) =>
            value < minimum ? minimum : value > maximum ? maximum : value;
        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp(t, 0f, 1f);
        private static float Min(float a, float b) => a < b ? a : b;
        private static float Max(float a, float b) => a > b ? a : b;
    }

    public readonly struct EarthSurfProfileData
    {
        public EarthSurfProfileData(
            float emergenceSeconds,
            float accelerationSeconds,
            float minimumSpeed,
            float maximumSpeed,
            float releaseSeconds,
            float speedExponent)
        {
            EmergenceSeconds = Max(0.05f, emergenceSeconds);
            AccelerationSeconds = Max(0.1f, accelerationSeconds);
            MinimumSpeed = Max(0f, minimumSpeed);
            MaximumSpeed = Max(MinimumSpeed, maximumSpeed);
            ReleaseSeconds = Max(0.1f, releaseSeconds);
            SpeedExponent = Max(1f, speedExponent);
        }

        public float EmergenceSeconds { get; }
        public float AccelerationSeconds { get; }
        public float MinimumSpeed { get; }
        public float MaximumSpeed { get; }
        public float ReleaseSeconds { get; }
        public float SpeedExponent { get; }
        public static EarthSurfProfileData Default => new EarthSurfProfileData(0.16f, 1.2f, 4f, 13f, 0.45f, 1.65f);
        private static float Max(float a, float b) => a > b ? a : b;
    }

    public readonly struct EarthSurfSample
    {
        public EarthSurfSample(float emergence01, float speed, bool releasing, bool complete)
        {
            Emergence01 = emergence01;
            Speed = speed;
            Releasing = releasing;
            Complete = complete;
        }
        public float Emergence01 { get; }
        public float Speed { get; }
        public bool Releasing { get; }
        public bool Complete { get; }
    }

    public sealed class EarthSurfSession
    {
        private readonly EarthSurfProfileData _profile;
        private float _startedAt;
        private float _releasedAt;
        public EarthSurfSession(in EarthSurfProfileData profile) => _profile = profile;
        public bool Active { get; private set; }
        public bool Releasing { get; private set; }

        public bool Begin(float now)
        {
            if (Active) return false;
            Active = true;
            Releasing = false;
            _startedAt = now;
            _releasedAt = 0f;
            return true;
        }

        public void Release(float now)
        {
            if (!Active || Releasing) return;
            Releasing = true;
            _releasedAt = now;
        }

        public EarthSurfSample Sample(float now)
        {
            if (!Active) return default;
            float held = Max(0f, now - _startedAt);
            float emerge = Clamp01(held / _profile.EmergenceSeconds);
            float charge = Clamp01(held / _profile.AccelerationSeconds);
            float speed01 = Pow(charge, _profile.SpeedExponent);
            float speed = Lerp(_profile.MinimumSpeed, _profile.MaximumSpeed, speed01);
            if (!Releasing) return new EarthSurfSample(emerge, speed, false, false);
            float release01 = Clamp01((now - _releasedAt) / _profile.ReleaseSeconds);
            bool complete = release01 >= 1f;
            if (complete) Active = false;
            return new EarthSurfSample(1f - release01, speed * (1f - release01), true, complete);
        }

        public void Cancel()
        {
            Active = false;
            Releasing = false;
        }
        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        private static float Max(float a, float b) => a > b ? a : b;
        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        private static float Pow(float value, float power) => (float)System.Math.Pow(value, power);
    }
}
