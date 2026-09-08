using Unity.Mathematics;

namespace Elemental.Simulation.Rendering
{
    /// <summary>Read-only charge channels. Stored matter amount is deliberately not charge.</summary>
    public struct EarthChargeFeedbackInput
    {
        public bool Allowed;
        public float Bend, PushOrGroundWave, VectorField, GravityThrow, FractureThrow;
        public float Resonance, Pillar, PillarWave, PillarCrest;
        public float Accumulation;
    }

    public readonly struct EarthChargeFeedbackOutput
    {
        public readonly float FieldOfViewDelta, PositionShake, RotationShake, ChromaticAberration;
        public EarthChargeFeedbackOutput(float fov, float position, float rotation, float chromatic)
        {
            FieldOfViewDelta = fov;
            PositionShake = position;
            RotationShake = rotation;
            ChromaticAberration = chromatic;
        }
    }

    public static class EarthChargeFeedback
    {
        public static float Resolve(in EarthChargeFeedbackInput input)
        {
            if (!input.Allowed) return 0f;
            float value = math.max(Unit(input.Bend), Unit(input.PushOrGroundWave));
            value = math.max(value, math.max(Unit(input.VectorField), Unit(input.GravityThrow)));
            value = math.max(value, math.max(Unit(input.FractureThrow), Unit(input.Resonance)));
            value = math.max(value, math.max(Unit(input.Pillar), Unit(input.PillarWave)));
            return math.max(value, math.max(Unit(input.PillarCrest), Unit(input.Accumulation)));
        }

        // Exponential response stays equivalent across 30/60/120 Hz and never overshoots.
        public static float Step(float current, float requested, float seconds)
        {
            current = Unit(current);
            requested = Unit(requested);
            if (!math.isfinite(seconds) || seconds <= 0f) return current;
            float value = math.lerp(current, requested,
                1f - math.exp(-seconds / (requested > current ? .10f : .13f)));
            return requested == 0f && value < .0001f ? 0f : value;
        }

        public static EarthChargeFeedbackOutput Solve(float charge, float shakeIntensity,
            float fovMotion, bool reducedMotion, float maxFov = 10f, float maxChromatic = .30f)
        {
            charge = Unit(charge);
            // Keep the onset visible during brief charges too, while reaching
            // the same bounded maximum with a soft slope at full charge.
            float tension = charge * (2f - charge);
            float shake = charge * charge * Unit(shakeIntensity);
            return new EarthChargeFeedbackOutput(
                tension * math.clamp(maxFov, 0f, 12f) * (reducedMotion ? 0f : Unit(fovMotion)),
                .0065f * shake, .14f * shake,
                reducedMotion ? 0f : tension * math.clamp(maxChromatic, 0f, .4f));
        }

        private static float Unit(float value) => math.isfinite(value) ? math.saturate(value) : 0f;
    }
}
