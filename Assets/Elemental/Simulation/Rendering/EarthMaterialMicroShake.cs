using Elemental.Simulation.Bending;
using Unity.Mathematics;

namespace Elemental.Simulation.Rendering
{
    /// <summary>One bounded envelope for all material contacts, independent of particle counts.</summary>
    public struct EarthMaterialMicroShake
    {
        public const float MaximumPositionMeters = .0025f;
        public const float MaximumRotationDegrees = .055f;
        public const float PulseSpacingSeconds = .08f;
        private float _pending, _target, _envelope, _cooldown;
        public float Envelope => _envelope;

        public static float Weight(EarthMaterialFeedbackKind kind, float strength, float radius, float distance)
        {
            if (!math.isfinite(strength) || !math.isfinite(radius) || !math.isfinite(distance) ||
                distance < 0 || distance >= 24 || radius < .15f) return 0;
            float threshold, gain;
            switch (kind)
            {
                case EarthMaterialFeedbackKind.Footstep:
                case EarthMaterialFeedbackKind.Roll:
                case EarthMaterialFeedbackKind.RepairSeat: return 0;
                case EarthMaterialFeedbackKind.Friction: threshold=.8f; gain=.36f; break;
                case EarthMaterialFeedbackKind.Land: threshold=1f; gain=.85f; break;
                case EarthMaterialFeedbackKind.Impact: threshold=.65f; gain=.85f; break;
                case EarthMaterialFeedbackKind.Fracture: threshold=.6f; gain=.8f; break;
                case EarthMaterialFeedbackKind.Emerge:
                case EarthMaterialFeedbackKind.Assemble: threshold=.5f; gain=.55f; break;
                case EarthMaterialFeedbackKind.WaveSurfaceContact:
                case EarthMaterialFeedbackKind.WaveSurfaceBurst: threshold=.5f; gain=.65f; break;
                case EarthMaterialFeedbackKind.Extract:
                case EarthMaterialFeedbackKind.ExtractionSurfaceContact:
                case EarthMaterialFeedbackKind.Release:
                case EarthMaterialFeedbackKind.RepairComplete: threshold=.7f; gain=.5f; break;
                default: return 0;
            }
            if (strength < threshold) return 0;
            float falloff = 1 - math.smoothstep(2f, 24f, distance);
            return math.saturate(strength / 2f) * gain * falloff * falloff;
        }

        public void Observe(EarthMaterialFeedbackKind kind, float strength, float radius, float distance)
        { _pending = math.max(_pending, Weight(kind, strength, radius, distance)); }

        public float Step(float deltaTime, bool allowed, float shakeIntensity, bool reducedMotion)
        {
            if (!allowed || reducedMotion || !math.isfinite(shakeIntensity) || shakeIntensity <= 0)
            { this = default; return 0; }
            if (!math.isfinite(deltaTime) || deltaTime <= 0) return _envelope * math.saturate(shakeIntensity);
            float dt = math.min(deltaTime, .25f);
            _cooldown = math.max(0, _cooldown-dt);
            if (_cooldown <= 0 && _pending > 0)
            {
                _target = math.max(_target, _pending);
                _pending = 0;
                _cooldown = PulseSpacingSeconds;
            }
            _envelope = math.lerp(_envelope, _target, 1-math.exp(-45*dt));
            _target *= math.exp(-dt/.13f);
            if (_envelope < .0001f && _target < .0001f) _envelope = _target = 0;
            return math.saturate(_envelope) * math.saturate(shakeIntensity);
        }
    }
}
