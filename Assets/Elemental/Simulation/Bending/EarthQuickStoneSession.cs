namespace Elemental.Simulation.Bending
{
    public enum EarthQuickStoneState : byte
    {
        Idle = 0,
        Extracting = 1,
        Primed = 2,
        Fired = 3,
        Expired = 4
    }

    public readonly struct EarthQuickCastProfileData
    {
        public EarthQuickCastProfileData(
            float doubleClickSeconds,
            float minimumSpeed,
            float maximumSpeed,
            float extractionSeconds = 0.15f)
        {
            DoubleClickSeconds = doubleClickSeconds < 0.08f ? 0.08f : doubleClickSeconds;
            MinimumSpeed = minimumSpeed < 1f ? 1f : minimumSpeed;
            MaximumSpeed = maximumSpeed < MinimumSpeed ? MinimumSpeed : maximumSpeed;
            ExtractionSeconds = extractionSeconds < 0.08f ? 0.08f :
                extractionSeconds > 0.25f ? 0.25f : extractionSeconds;
        }

        public float DoubleClickSeconds { get; }
        public float MinimumSpeed { get; }
        public float MaximumSpeed { get; }
        public float ExtractionSeconds { get; }
        public static EarthQuickCastProfileData Default =>
            new EarthQuickCastProfileData(0.42f, 75f, 95f, 0.15f);
    }

    /// <summary>Pure timing/state contract for the two-click quick stone grammar.</summary>
    public sealed class EarthQuickStoneSession
    {
        private readonly EarthQuickCastProfileData _profile;
        private float _primedAt;
        private bool _fireBuffered;

        public EarthQuickStoneSession(in EarthQuickCastProfileData profile) => _profile = profile;

        public EarthQuickStoneState State { get; private set; }
        public uint TargetId { get; private set; }
        public bool IsPrimed => State == EarthQuickStoneState.Extracting ||
                                State == EarthQuickStoneState.Primed;
        public bool IsExtracting => State == EarthQuickStoneState.Extracting;
        public bool HasBufferedFire => _fireBuffered;

        public bool TryPrime(float now, uint targetId)
        {
            if (targetId == 0u) return false;
            TargetId = targetId;
            _primedAt = now;
            _fireBuffered = false;
            State = EarthQuickStoneState.Extracting;
            return true;
        }

        /// <summary>
        /// Returns true when the second click belongs to this session. During the
        /// short extraction it is buffered and reports speed zero; once ready it
        /// becomes an immediate launch.
        /// </summary>
        public bool TryFire(float now, out float speed)
        {
            speed = 0f;
            if (!IsPrimed || now - _primedAt > _profile.DoubleClickSeconds) return false;
            if (now - _primedAt < _profile.ExtractionSeconds)
            {
                _fireBuffered = true;
                return true;
            }
            Refresh(now);
            speed = LaunchSpeed(now);
            _fireBuffered = false;
            State = EarthQuickStoneState.Fired;
            return true;
        }

        public bool TryConsumeBufferedFire(float now, out float speed)
        {
            speed = 0f;
            if (!_fireBuffered || !IsPrimed || now - _primedAt < _profile.ExtractionSeconds ||
                now - _primedAt > _profile.DoubleClickSeconds) return false;
            Refresh(now);
            speed = LaunchSpeed(now);
            _fireBuffered = false;
            State = EarthQuickStoneState.Fired;
            return true;
        }

        public void Refresh(float now)
        {
            if (State == EarthQuickStoneState.Extracting &&
                now - _primedAt >= _profile.ExtractionSeconds)
                State = EarthQuickStoneState.Primed;
        }

        /// <summary>
        /// Terrain remeshing is budgeted and can legitimately take longer than the
        /// double-click window. Keep the session in its extraction phase while the
        /// reserved rock is not visible yet; an early second click remains buffered.
        /// </summary>
        public void SuspendUntilVisible(float now)
        {
            if (!IsPrimed) return;
            _primedAt = now;
            State = EarthQuickStoneState.Extracting;
        }

        private float LaunchSpeed(float now)
        {
            float urgency01 = 1f - Clamp01((now - _primedAt) / _profile.DoubleClickSeconds);
            return Lerp(_profile.MinimumSpeed, _profile.MaximumSpeed, urgency01);
        }

        public bool ExpireIfNeeded(float now)
        {
            if (!IsPrimed || now - _primedAt <= _profile.DoubleClickSeconds) return false;
            _fireBuffered = false;
            State = EarthQuickStoneState.Expired;
            return true;
        }

        public float Extraction01(float now) => IsPrimed
            ? Clamp01((now - _primedAt) / _profile.ExtractionSeconds)
            : State == EarthQuickStoneState.Fired ? 1f : 0f;

        public float Remaining01(float now) => IsPrimed
            ? 1f - Clamp01((now - _primedAt) / _profile.DoubleClickSeconds)
            : 0f;

        public void Reset()
        {
            State = EarthQuickStoneState.Idle;
            TargetId = 0u;
            _primedAt = 0f;
            _fireBuffered = false;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        private static float Lerp(float a, float b, float t) => a + ((b - a) * Clamp01(t));
    }
}
