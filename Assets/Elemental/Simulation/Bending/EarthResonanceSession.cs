namespace Elemental.Simulation.Bending
{
    public readonly struct EarthResonanceProfileData
    {
        public EarthResonanceProfileData(
            float thresholdSeconds,
            float fullChargeSeconds,
            int minimumStoneCount,
            int maximumStoneCount,
            float minimumRadius,
            float maximumRadius,
            float minimumLifetime,
            float maximumLifetime)
        {
            ThresholdSeconds = thresholdSeconds < 0.05f ? 0.05f : thresholdSeconds;
            FullChargeSeconds = fullChargeSeconds < ThresholdSeconds
                ? ThresholdSeconds
                : fullChargeSeconds;
            MinimumStoneCount = minimumStoneCount < 1 ? 1 : minimumStoneCount;
            MaximumStoneCount = maximumStoneCount < MinimumStoneCount
                ? MinimumStoneCount
                : maximumStoneCount;
            MinimumRadius = minimumRadius < 0.1f ? 0.1f : minimumRadius;
            MaximumRadius = maximumRadius < MinimumRadius ? MinimumRadius : maximumRadius;
            MinimumLifetime = minimumLifetime < 0.1f ? 0.1f : minimumLifetime;
            MaximumLifetime = maximumLifetime < MinimumLifetime ? MinimumLifetime : maximumLifetime;
        }

        public float ThresholdSeconds { get; }
        public float FullChargeSeconds { get; }
        public int MinimumStoneCount { get; }
        public int MaximumStoneCount { get; }
        public float MinimumRadius { get; }
        public float MaximumRadius { get; }
        public float MinimumLifetime { get; }
        public float MaximumLifetime { get; }
        public static EarthResonanceProfileData Default => new EarthResonanceProfileData(
            0.55f, 2.6f, 8, 28, 1.2f, 6.5f, 1.5f, 6f);
    }

    public readonly struct EarthResonanceChargeSample
    {
        public EarthResonanceChargeSample(bool activated, float charge01, int stoneCount, float radius, float lifetime)
        {
            Activated = activated;
            Charge01 = Clamp01(charge01);
            StoneCount = stoneCount;
            Radius = radius;
            Lifetime = lifetime;
        }

        public bool Activated { get; }
        public float Charge01 { get; }
        public int StoneCount { get; }
        public float Radius { get; }
        public float Lifetime { get; }
        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    public sealed class EarthResonanceSession
    {
        private readonly EarthResonanceProfileData _profile;
        private float _startedAt;
        private float _expiresAt;

        public EarthResonanceSession(in EarthResonanceProfileData profile) => _profile = profile;
        public bool IsCharging { get; private set; }
        public bool IsVolleyActive { get; private set; }
        public int RemainingStoneCount { get; private set; }

        public bool Begin(float now)
        {
            if (IsCharging || IsVolleyActive) return false;
            _startedAt = now;
            IsCharging = true;
            return true;
        }

        public EarthResonanceChargeSample Sample(float now)
        {
            float held = IsCharging ? Max(0f, now - _startedAt) : 0f;
            if (held < _profile.ThresholdSeconds)
                return new EarthResonanceChargeSample(false, 0f, 0, _profile.MinimumRadius, 0f);
            float linear = Clamp01((held - _profile.ThresholdSeconds) /
                                   Max(0.05f, _profile.FullChargeSeconds - _profile.ThresholdSeconds));
            float eased = 1f - Pow(1f - linear, 2.35f);
            return new EarthResonanceChargeSample(
                true,
                eased,
                Round(Lerp(_profile.MinimumStoneCount, _profile.MaximumStoneCount, eased)),
                Lerp(_profile.MinimumRadius, _profile.MaximumRadius, eased),
                Lerp(_profile.MinimumLifetime, _profile.MaximumLifetime, eased * eased));
        }

        public EarthResonanceChargeSample Release(float now)
        {
            EarthResonanceChargeSample sample = Sample(now);
            IsCharging = false;
            if (!sample.Activated) return sample;
            IsVolleyActive = true;
            RemainingStoneCount = sample.StoneCount;
            _expiresAt = now + sample.Lifetime;
            return sample;
        }

        public bool ConsumeStone()
        {
            if (!IsVolleyActive || RemainingStoneCount <= 0) return false;
            RemainingStoneCount--;
            if (RemainingStoneCount == 0) IsVolleyActive = false;
            return true;
        }

        public bool Expire(float now)
        {
            if (!IsVolleyActive || now < _expiresAt) return false;
            IsVolleyActive = false;
            RemainingStoneCount = 0;
            return true;
        }

        public void Cancel()
        {
            IsCharging = false;
            IsVolleyActive = false;
            RemainingStoneCount = 0;
            _startedAt = 0f;
            _expiresAt = 0f;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        private static float Max(float a, float b) => a > b ? a : b;
        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        private static int Round(float value) => (int)(value + 0.5f);
        private static float Pow(float value, float power) => (float)System.Math.Pow(value, power);
    }
}
