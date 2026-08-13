using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public sealed class BendSessionState
    {
        private readonly BendTuning _tuning;
        private float _chargeHeldSeconds;
        private float _chargeMemoryRemaining;

        public BendSessionState(BendTuning tuning)
        {
            _tuning = tuning;
            Reset();
        }

        public BendPhase Phase { get; private set; }
        public BendOriginMode OriginMode { get; private set; }
        public BendGestureIntent GestureIntent { get; private set; }
        public float Amount01 { get; private set; }
        public float Charge01 { get; private set; }
        public float Focus01 { get; private set; }
        public float ChargeHeldSeconds => _chargeHeldSeconds;
        public bool IsActive => Phase != BendPhase.Idle && Phase != BendPhase.Cancelled;

        public bool BeginAcquire(BendOriginMode originMode, float focus01 = 1f)
        {
            if (Phase != BendPhase.Idle && Phase != BendPhase.Cancelled) return false;
            Reset();
            OriginMode = originMode;
            Focus01 = math.saturate(focus01);
            Phase = BendPhase.Acquiring;
            return true;
        }

        public bool SourceAcquired()
        {
            if (Phase != BendPhase.Acquiring) return false;
            Phase = BendPhase.Forming;
            return true;
        }

        public bool SetAmount(float amount01)
        {
            if (Phase != BendPhase.Forming && Phase != BendPhase.Holding && Phase != BendPhase.Charging)
                return false;
            Amount01 = math.saturate(amount01);
            if (Phase == BendPhase.Forming && Amount01 > 0f) Phase = BendPhase.Holding;
            return true;
        }

        public bool BeginCharge()
        {
            if (Phase != BendPhase.Holding) return false;
            Phase = BendPhase.Charging;
            _chargeMemoryRemaining = _tuning.ChargeMemorySeconds;
            return true;
        }

        public bool EndCharge()
        {
            if (Phase != BendPhase.Charging) return false;
            Phase = BendPhase.Holding;
            _chargeMemoryRemaining = _tuning.ChargeMemorySeconds;
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            float delta = math.max(0f, deltaSeconds);
            if (Phase == BendPhase.Charging)
            {
                _chargeHeldSeconds = math.min(
                    _tuning.MaximumChargeHoldSeconds,
                    _chargeHeldSeconds + delta);
                Charge01 = ChargeFromSeconds(_chargeHeldSeconds, _tuning);
                _chargeMemoryRemaining = _tuning.ChargeMemorySeconds;
            }
            else if (Charge01 > 0f)
            {
                if (_chargeMemoryRemaining > 0f)
                {
                    float memoryConsumed = math.min(_chargeMemoryRemaining, delta);
                    _chargeMemoryRemaining -= memoryConsumed;
                    delta -= memoryConsumed;
                }
                if (delta > 0f)
                    Charge01 = math.max(0f, Charge01 - (_tuning.ChargeDecayPerSecond * delta));
            }
        }

        public bool Commit(BendGestureIntent gestureIntent)
        {
            if (Phase != BendPhase.Holding && Phase != BendPhase.Charging) return false;
            GestureIntent = gestureIntent;
            Phase = BendPhase.Committing;
            return true;
        }

        public bool Sustain()
        {
            if (Phase != BendPhase.Committing) return false;
            Phase = BendPhase.Sustaining;
            return true;
        }

        public bool BeginRecovery()
        {
            if (Phase != BendPhase.Committing && Phase != BendPhase.Sustaining) return false;
            Phase = BendPhase.Recovery;
            return true;
        }

        public void CompleteRecovery()
        {
            Reset();
        }

        public void Cancel()
        {
            Phase = BendPhase.Cancelled;
        }

        public static float ChargeFromSeconds(float seconds, in BendTuning tuning)
        {
            float normalized = math.saturate(seconds / tuning.FullChargeSeconds);
            return normalized * normalized * (3f - (2f * normalized));
        }

        private void Reset()
        {
            Phase = BendPhase.Idle;
            OriginMode = BendOriginMode.Aim;
            GestureIntent = BendGestureIntent.None;
            Amount01 = 0f;
            Charge01 = 0f;
            Focus01 = 1f;
            _chargeHeldSeconds = 0f;
            _chargeMemoryRemaining = 0f;
        }
    }
}
