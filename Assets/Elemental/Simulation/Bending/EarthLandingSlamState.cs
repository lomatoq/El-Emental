using System;
using Elemental.Simulation.Magic;

namespace Elemental.Simulation.Bending
{
    [Serializable]
    public struct EarthLandingSlamSettings
    {
        public float MinimumDrop, MinimumSpeed, CraterRadius, EjectionSpeed;
        public static readonly AbilityId Ability = new AbilityId(0x5301);
        public static EarthLandingSlamSettings Default => new EarthLandingSlamSettings
        { MinimumDrop = 2f, MinimumSpeed = 7.5f, CraterRadius = 1.6f, EjectionSpeed = 8f };
        public bool IsValid => float.IsFinite(MinimumDrop) && MinimumDrop >= .5f &&
            float.IsFinite(MinimumSpeed) && MinimumSpeed >= 3f &&
            float.IsFinite(CraterRadius) && CraterRadius >= .3f && CraterRadius <= 2.5f &&
            float.IsFinite(EjectionSpeed) && EjectionSpeed >= 1f && EjectionSpeed <= 15f;
    }

    // One physical support-to-flight-to-contact episode. Height is radial altitude;
    // speed is relative to the receiving support, not a world-space tangent speed.
    public struct EarthLandingSlamState
    {
        private bool _observedSupport, _airborne, _spentHold;
        private float _apex, _minimumSpeed;
        private int _supportContactGrace;
        public bool IsArmed { get; private set; }
        public bool HasObservedSupport => _observedSupport;
        public float LastDrop { get; private set; }
        public float LastSpeed { get; private set; }
        public bool Step(bool held, bool supported, bool actualContact, float altitude,
            float verticalSpeed, in EarthLandingSlamSettings settings)
        {
            if (!settings.IsValid || !float.IsFinite(altitude) || !float.IsFinite(verticalSpeed))
            { this = default; return false; }
            if (!held) _spentHold = false;
            if (!_observedSupport)
            {
                _observedSupport = supported; _apex = altitude;
                IsArmed = false; return false;
            }
            if (!supported)
            {
                if (!_airborne) { _airborne = true; _apex = altitude; _minimumSpeed = 0f; }
                _supportContactGrace = 0;
                _apex = Math.Max(_apex, altitude);
                _minimumSpeed = Math.Min(_minimumSpeed, verticalSpeed);
                IsArmed = held && !_spentHold;
                return false;
            }
            // A ground probe can become stable just before PhysX delivers its
            // real contact. Preserve the flight for at most four physics steps.
            if (_airborne && !actualContact && ++_supportContactGrace <= 4) return false;
            bool impact = _airborne && held && !_spentHold && actualContact;
            LastDrop = Math.Max(0f, _apex - altitude);
            LastSpeed = Math.Max(0f, -Math.Min(_minimumSpeed, verticalSpeed));
            _airborne = IsArmed = false; _apex = altitude; _minimumSpeed = 0f;
            bool commit = impact && LastDrop >= settings.MinimumDrop && LastSpeed >= settings.MinimumSpeed;
            if (commit) _spentHold = true;
            return commit;
        }
        public void Cancel() => this = default;
    }
}
