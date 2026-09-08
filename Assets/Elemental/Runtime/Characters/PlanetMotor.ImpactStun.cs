using System;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public sealed partial class PlanetMotor
    {
        private float _impactStunUntil = float.NegativeInfinity;
        public bool IsImpactStunned => Time.time < _impactStunUntil;
        public float ImpactStunRemaining => Mathf.Max(0f, _impactStunUntil - Time.time);
        public event Action ImpactStunBegan;
        public void BeginImpactStun(float seconds)
        {
            if (!float.IsFinite(seconds) || seconds <= 0f) return;
            bool wasStunned = IsImpactStunned;
            _impactStunUntil = Mathf.Max(_impactStunUntil, Time.time + Mathf.Clamp(seconds, .01f, .6f));
            if (!wasStunned) ImpactStunBegan?.Invoke();
        }
        public void ClearImpactStun() => _impactStunUntil = float.NegativeInfinity;
    }
}
