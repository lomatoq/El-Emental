using Elemental.Simulation.Matter;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthCameraIntent : byte
    {
        None = 0,
        Aim = 1,
        LightCommit = 2,
        HeavyCommit = 3,
        Projectile = 4,
        Structure = 5,
        Armor = 6,
        Return = 7,
        Impact = 8,
        Airborne = 9
    }

    public readonly struct EarthCameraEnvelope
    {
        public EarthCameraEnvelope(float3 center, float3 extents)
        {
            Center = center;
            Extents = math.max(float3.zero, extents);
        }

        public float3 Center { get; }
        public float3 Extents { get; }
        public float Radius => math.length(Extents);
        public bool IsFinite => math.all(math.isfinite(Center)) && math.all(math.isfinite(Extents));
        public static EarthCameraEnvelope Point(float3 point, float radius = 0.1f) =>
            new EarthCameraEnvelope(point, new float3(math.max(0.01f, radius)));
    }

    /// <summary>
    /// A bounded presentation request. Abilities describe the action; the camera
    /// remains the only system allowed to decide framing, FOV and damping.
    /// </summary>
    public readonly struct EarthCameraRequest
    {
        public EarthCameraRequest(
            EarthCameraIntent intent,
            float3 actionAxis,
            in EarthCameraEnvelope actionBounds,
            float energy,
            float anticipation,
            float commit,
            float recovery,
            EarthMatterId focusMatter,
            byte priority = 0)
        {
            Intent = intent;
            ActionAxis = math.normalizesafe(actionAxis);
            ActionBounds = actionBounds;
            Energy = math.max(0f, energy);
            Anticipation = math.saturate(anticipation);
            Commit = math.saturate(commit);
            Recovery = math.saturate(recovery);
            FocusMatter = focusMatter;
            Priority = priority;
        }

        public EarthCameraIntent Intent { get; }
        public float3 ActionAxis { get; }
        public EarthCameraEnvelope ActionBounds { get; }
        public float Energy { get; }
        public float Anticipation { get; }
        public float Commit { get; }
        public float Recovery { get; }
        public EarthMatterId FocusMatter { get; }
        public byte Priority { get; }
        public bool IsValid => Intent != EarthCameraIntent.None && ActionBounds.IsFinite;
    }

    public readonly struct EarthCameraRequestResponse
    {
        public EarthCameraRequestResponse(
            float focusWeight,
            float distanceDelta,
            float fieldOfViewDelta,
            float verticalBias,
            float lookAhead,
            float holdSeconds)
        {
            FocusWeight = math.saturate(focusWeight);
            DistanceDelta = distanceDelta;
            FieldOfViewDelta = fieldOfViewDelta;
            VerticalBias = verticalBias;
            LookAhead = math.max(0f, lookAhead);
            HoldSeconds = math.max(0.02f, holdSeconds);
        }

        public float FocusWeight { get; }
        public float DistanceDelta { get; }
        public float FieldOfViewDelta { get; }
        public float VerticalBias { get; }
        public float LookAhead { get; }
        public float HoldSeconds { get; }
    }

    public static class EarthCameraRequestSolver
    {
        public static EarthCameraRequestResponse Solve(in EarthCameraRequest request)
        {
            float energy01 = 1f - math.exp(-math.max(0f, request.Energy) / 900f);
            float extent = math.saturate(request.ActionBounds.Radius / 8f);
            float commit = math.max(request.Commit, energy01);
            switch (request.Intent)
            {
                case EarthCameraIntent.Projectile:
                    return new EarthCameraRequestResponse(
                        0.76f, math.lerp(0.25f, 1.35f, extent),
                        math.lerp(-2.5f, 2f, request.Recovery), 0.12f,
                        math.lerp(0.8f, 2.8f, commit), 0.28f + request.Recovery * 0.24f);
                case EarthCameraIntent.Structure:
                    return new EarthCameraRequestResponse(
                        0.82f, math.lerp(0.45f, 2.4f, extent),
                        math.lerp(-1.8f, 2.8f, extent), math.lerp(0.1f, 0.9f, extent),
                        0.15f, 0.36f + extent * 0.35f);
                case EarthCameraIntent.Armor:
                    return new EarthCameraRequestResponse(
                        0.38f, math.lerp(1.6f, 3.4f, extent), 3.5f, 0.35f,
                        0f, 0.32f);
                case EarthCameraIntent.Return:
                    return new EarthCameraRequestResponse(
                        0.66f, 0.7f, -1f, -0.18f, 0.35f, 0.42f);
                case EarthCameraIntent.Impact:
                    return new EarthCameraRequestResponse(
                        0.72f, math.lerp(0.1f, 1.15f, energy01),
                        math.lerp(-2f, 1.5f, energy01), 0.08f,
                        0.2f, math.lerp(0.12f, 0.36f, energy01));
                case EarthCameraIntent.Airborne:
                    return new EarthCameraRequestResponse(0.48f, 1f, 2.5f, 1.15f, 0.3f, 0.32f);
                case EarthCameraIntent.HeavyCommit:
                    return new EarthCameraRequestResponse(
                        0.74f, 0.7f, math.lerp(-4f, 2f, request.Recovery),
                        0.22f, 0.55f, 0.28f);
                case EarthCameraIntent.LightCommit:
                    return new EarthCameraRequestResponse(0.52f, 0.25f, 1f, 0.08f, 0.4f, 0.16f);
                default:
                    return new EarthCameraRequestResponse(0.35f, 0f, 0f, 0f, 0f, 0.12f);
            }
        }

        public static bool ShouldReplace(
            in EarthCameraRequest active,
            float activeUntil,
            in EarthCameraRequest candidate,
            float now)
        {
            if (!candidate.IsValid) return false;
            if (!active.IsValid || now >= activeUntil) return true;
            if (candidate.Priority != active.Priority) return candidate.Priority > active.Priority;
            return candidate.Commit >= active.Commit || candidate.Energy >= active.Energy * 1.2f;
        }
    }
}
