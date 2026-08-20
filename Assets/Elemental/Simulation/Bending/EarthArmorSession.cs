namespace Elemental.Simulation.Bending
{
    public enum EarthArmorInputResult : byte
    {
        None = 0,
        PhaseChanged = 1,
        OverscrollArmed = 2,
        RadialRelease = 3
    }

    public readonly struct EarthArmorProfileData
    {
        public EarthArmorProfileData(float phasePerWheelStep, float confirmationSeconds)
        {
            PhasePerWheelStep = Clamp(phasePerWheelStep, 0.04f, 0.30f);
            ConfirmationSeconds = Clamp(confirmationSeconds, 0.1f, 0.8f);
        }

        public float PhasePerWheelStep { get; }
        public float ConfirmationSeconds { get; }
        public static EarthArmorProfileData Default => new EarthArmorProfileData(0.14f, 0.35f);

        private static float Clamp(float value, float minimum, float maximum) =>
            value < minimum ? minimum : value > maximum ? maximum : value;
    }

    /// <summary>Pure scroll grammar. Space is intentionally absent from this contract.</summary>
    public sealed class EarthArmorSession
    {
        private readonly EarthArmorProfileData _profile;
        private int _overscrollSteps;
        private float _lastOverscrollAt;

        public EarthArmorSession(in EarthArmorProfileData profile) => _profile = profile;

        public bool Active { get; private set; }
        public float Phase01 { get; private set; }
        public int OverscrollSteps => _overscrollSteps;

        public void Begin(float initialPhase01 = 0f)
        {
            Active = true;
            Phase01 = Clamp01(initialPhase01);
            _overscrollSteps = 0;
            _lastOverscrollAt = float.NegativeInfinity;
        }

        public EarthArmorInputResult ApplyWheelSteps(float signedSteps, float now)
        {
            if (!Active || signedSteps == 0f) return EarthArmorInputResult.None;
            if (signedSteps < 0f)
            {
                Phase01 = Clamp01(Phase01 + signedSteps * _profile.PhasePerWheelStep);
                _overscrollSteps = 0;
                return EarthArmorInputResult.PhaseChanged;
            }
            if (Phase01 < 1f)
            {
                Phase01 = Clamp01(Phase01 + signedSteps * _profile.PhasePerWheelStep);
                _overscrollSteps = 0;
                return EarthArmorInputResult.PhaseChanged;
            }
            if (now - _lastOverscrollAt > _profile.ConfirmationSeconds) _overscrollSteps = 0;
            _lastOverscrollAt = now;
            _overscrollSteps++;
            if (_overscrollSteps < 2) return EarthArmorInputResult.OverscrollArmed;
            Active = false;
            return EarthArmorInputResult.RadialRelease;
        }

        public void End()
        {
            Active = false;
            _overscrollSteps = 0;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
