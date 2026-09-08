using System;

namespace Elemental.Simulation.Combat
{
    /// <summary>Expenditure feedback only. Never participates in action admission.</summary>
    public struct EarthDisplayMana
    {
        public float Value { get; private set; }
        private float _recoveryDelay;
        public void Reset() { Value = 100f; _recoveryDelay = 0f; }
        public void Spend(float amount)
        {
            if (!float.IsFinite(amount) || amount <= 0f) return;
            Value = Math.Max(15f, Value - amount);
            _recoveryDelay = .35f;
        }
        public void Step(float seconds)
        {
            if (!float.IsFinite(seconds) || seconds <= 0f) return;
            float recoverySeconds = Math.Max(0f, seconds - _recoveryDelay);
            _recoveryDelay = Math.Max(0f, _recoveryDelay - seconds);
            Value = Math.Min(100f, Value + recoverySeconds * 25f);
        }
    }
}
