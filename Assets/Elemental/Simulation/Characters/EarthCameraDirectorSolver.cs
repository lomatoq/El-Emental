using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthCameraState : byte
    {
        Explore = 0,
        Aim = 1,
        BendLight = 2,
        BendHeavy = 3,
        DrawStructure = 4,
        HoldMass = 5,
        Airborne = 6,
        Impact = 7,
        Recovery = 8
    }

    public readonly struct EarthCameraContext
    {
        public EarthCameraContext(
            bool aiming, bool bending, bool drawingStructure, bool holdingMass,
            bool airborne, bool impact, bool recovering, float effort01)
        {
            Aiming = aiming;
            Bending = bending;
            DrawingStructure = drawingStructure;
            HoldingMass = holdingMass;
            Airborne = airborne;
            Impact = impact;
            Recovering = recovering;
            Effort01 = math.saturate(effort01);
        }
        public bool Aiming { get; }
        public bool Bending { get; }
        public bool DrawingStructure { get; }
        public bool HoldingMass { get; }
        public bool Airborne { get; }
        public bool Impact { get; }
        public bool Recovering { get; }
        public float Effort01 { get; }
    }

    public static class EarthCameraStateResolver
    {
        public static EarthCameraState Resolve(in EarthCameraContext context)
        {
            if (context.Impact) return EarthCameraState.Impact;
            if (context.Recovering) return EarthCameraState.Recovery;
            if (context.Airborne) return EarthCameraState.Airborne;
            if (context.HoldingMass) return EarthCameraState.HoldMass;
            if (context.DrawingStructure) return EarthCameraState.DrawStructure;
            if (context.Bending) return context.Effort01 >= 0.58f
                ? EarthCameraState.BendHeavy
                : EarthCameraState.BendLight;
            if (context.Aiming) return EarthCameraState.Aim;
            return EarthCameraState.Explore;
        }
    }

    public readonly struct EarthCameraFocusInput
    {
        public EarthCameraFocusInput(
            float3 player, float3 aim, float3 held, float3 construct,
            float playerWeight, float aimWeight, float heldWeight, float constructWeight)
        {
            Player = player;
            Aim = aim;
            Held = held;
            Construct = construct;
            PlayerWeight = math.max(0f, playerWeight);
            AimWeight = math.max(0f, aimWeight);
            HeldWeight = math.max(0f, heldWeight);
            ConstructWeight = math.max(0f, constructWeight);
        }
        public float3 Player { get; }
        public float3 Aim { get; }
        public float3 Held { get; }
        public float3 Construct { get; }
        public float PlayerWeight { get; }
        public float AimWeight { get; }
        public float HeldWeight { get; }
        public float ConstructWeight { get; }
    }

    public static class EarthCameraFocusSolver
    {
        public static float3 Solve(in EarthCameraFocusInput input, float maximumDistanceFromPlayer)
        {
            float total = input.PlayerWeight + input.AimWeight + input.HeldWeight + input.ConstructWeight;
            if (total <= 0.0001f) return input.Player;
            float3 focus = ((input.Player * input.PlayerWeight) + (input.Aim * input.AimWeight) +
                            (input.Held * input.HeldWeight) + (input.Construct * input.ConstructWeight)) / total;
            float3 offset = focus - input.Player;
            float limit = math.max(0.1f, maximumDistanceFromPlayer);
            float length = math.length(offset);
            return length > limit ? input.Player + offset * (limit / length) : focus;
        }
    }

    public readonly struct EarthCameraOcclusionState
    {
        public EarthCameraOcclusionState(float distance, float clearSeconds)
        {
            Distance = math.max(0f, distance);
            ClearSeconds = math.max(0f, clearSeconds);
        }
        public float Distance { get; }
        public float ClearSeconds { get; }
    }

    public static class EarthCameraOcclusionSolver
    {
        public static EarthCameraOcclusionState Step(
            in EarthCameraOcclusionState state,
            float desiredDistance,
            float hitDistance,
            bool hit,
            float deltaSeconds,
            float pullInSpeed,
            float releaseSpeed,
            float releaseDelay)
        {
            float desired = math.max(0.05f, desiredDistance);
            float delta = math.max(0f, deltaSeconds);
            if (hit)
            {
                float target = math.clamp(hitDistance, 0.05f, desired);
                return new EarthCameraOcclusionState(
                    MoveTowards(state.Distance <= 0f ? desired : state.Distance, target, pullInSpeed * delta), 0f);
            }
            float clear = state.ClearSeconds + delta;
            float distance = state.Distance <= 0f ? desired : state.Distance;
            if (clear >= math.max(0f, releaseDelay))
                distance = MoveTowards(distance, desired, releaseSpeed * delta);
            return new EarthCameraOcclusionState(distance, clear);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (math.abs(target - current) <= maxDelta) return target;
            return current + math.sign(target - current) * math.max(0f, maxDelta);
        }
    }

    public readonly struct EarthCameraAccessibilitySettings
    {
        public EarthCameraAccessibilitySettings(
            float shakeIntensity, float cameraLag, float fieldOfViewMotion, bool reducedMotion)
        {
            ShakeIntensity = math.saturate(shakeIntensity);
            CameraLag = math.saturate(cameraLag);
            FieldOfViewMotion = math.saturate(fieldOfViewMotion);
            ReducedMotion = reducedMotion;
        }

        public float ShakeIntensity { get; }
        public float CameraLag { get; }
        public float FieldOfViewMotion { get; }
        public bool ReducedMotion { get; }
        public float EffectiveShake => ReducedMotion ? ShakeIntensity * 0.2f : ShakeIntensity;
        public float EffectiveLag => ReducedMotion ? CameraLag * 0.25f : CameraLag;
        public float EffectiveFieldOfViewMotion => ReducedMotion ? 0f : FieldOfViewMotion;
    }

    public static class EarthCameraShoulderSolver
    {
        public static float Resolve(float currentSign, bool swapPressed)
        {
            float normalized = currentSign < 0f ? -1f : 1f;
            return swapPressed ? -normalized : normalized;
        }
    }
}
