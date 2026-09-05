using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>Signed, support-relative Blend Tree coordinates; scalar Speed stays unsigned.</summary>
    public struct EarthLocomotionBlendState
    {
        public EarthScalarPresentationState Lateral;
        public EarthScalarPresentationState Forward;
    }

    public static class EarthLocomotionBlend
    {
        public static float2 Step(
            ref EarthLocomotionBlendState state,
            float3 supportRelativeVelocity,
            float3 localUp,
            float3 facing,
            bool travelEnabled,
            float deltaTime)
        {
            float2 target = float2.zero;
            if (travelEnabled && math.all(math.isfinite(supportRelativeVelocity)) &&
                math.all(math.isfinite(localUp)) && math.all(math.isfinite(facing)))
            {
                float3 up = math.normalizesafe(localUp, math.up());
                float3 forward = math.normalizesafe(facing - up * math.dot(facing, up));
                float3 right = math.normalizesafe(math.cross(up, forward));
                target = new float2(math.dot(supportRelativeVelocity, right),
                    math.dot(supportRelativeVelocity, forward));
            }

            // Explicit filtering is shared by the Animator and playable-controller paths;
            // Animator.SetFloat's damping is not available on AnimatorControllerPlayable.
            return new float2(
                EarthAnimationParameterFilter.StepSpeed(ref state.Lateral, target.x, 0.10f, 0.10f, deltaTime),
                EarthAnimationParameterFilter.StepSpeed(ref state.Forward, target.y, 0.10f, 0.10f, deltaTime));
        }
    }
}
