using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    public enum EarthProjectileSurfaceContactDecision : byte
    {
        NoContact = 0,
        InvalidGeometry = 1,
        OutsideClearance = 2,
        Grazing = 3,
        Duplicate = 4,
        Impact = 5
    }

    public readonly struct EarthProjectileSurfaceContactTuning
    {
        public EarthProjectileSurfaceContactTuning(
            float minimumApproachSpeed,
            float minimumApproachFraction,
            float maximumSurfaceClearance,
            float maximumClearanceRadiusRatio,
            float contactEpisodeSeconds)
        {
            MinimumApproachSpeed = math.max(0f, minimumApproachSpeed);
            MinimumApproachFraction = math.clamp(minimumApproachFraction, 0f, 1f);
            MaximumSurfaceClearance = math.max(0f, maximumSurfaceClearance);
            MaximumClearanceRadiusRatio = math.max(0f, maximumClearanceRadiusRatio);
            ContactEpisodeSeconds = math.max(0f, contactEpisodeSeconds);
        }

        public float MinimumApproachSpeed { get; }
        public float MinimumApproachFraction { get; }
        public float MaximumSurfaceClearance { get; }
        public float MaximumClearanceRadiusRatio { get; }
        public float ContactEpisodeSeconds { get; }

        public static EarthProjectileSurfaceContactTuning Default => new(
            0.75f,
            0.08f,
            0.035f,
            0.08f,
            0.12f);
    }

    public readonly struct EarthProjectileSurfaceContactSample
    {
        public EarthProjectileSurfaceContactSample(
            bool hasContact,
            uint surfaceId,
            float time,
            float3 projectileVelocity,
            float3 surfaceVelocity,
            float3 surfaceNormal,
            float surfaceClearance,
            float projectileRadius)
        {
            HasContact = hasContact;
            SurfaceId = surfaceId;
            Time = time;
            ProjectileVelocity = projectileVelocity;
            SurfaceVelocity = surfaceVelocity;
            SurfaceNormal = surfaceNormal;
            SurfaceClearance = surfaceClearance;
            ProjectileRadius = projectileRadius;
        }

        public bool HasContact { get; }
        public uint SurfaceId { get; }
        public float Time { get; }
        public float3 ProjectileVelocity { get; }
        public float3 SurfaceVelocity { get; }
        public float3 SurfaceNormal { get; }
        public float SurfaceClearance { get; }
        public float ProjectileRadius { get; }
    }

    public readonly struct EarthProjectileSurfaceContactState
    {
        public EarthProjectileSurfaceContactState(uint lastImpactSurfaceId, float lastImpactTime, bool hasImpact)
        {
            LastImpactSurfaceId = lastImpactSurfaceId;
            LastImpactTime = lastImpactTime;
            HasImpact = hasImpact;
        }

        public uint LastImpactSurfaceId { get; }
        public float LastImpactTime { get; }
        public bool HasImpact { get; }
    }

    public readonly struct EarthProjectileSurfaceContactResult
    {
        public EarthProjectileSurfaceContactResult(
            EarthProjectileSurfaceContactState state,
            EarthProjectileSurfaceContactDecision decision,
            float approachSpeed,
            float relativeSpeed,
            float allowedClearance)
        {
            State = state;
            Decision = decision;
            ApproachSpeed = approachSpeed;
            RelativeSpeed = relativeSpeed;
            AllowedClearance = allowedClearance;
        }

        public EarthProjectileSurfaceContactState State { get; }
        public EarthProjectileSurfaceContactDecision Decision { get; }
        public float ApproachSpeed { get; }
        public float RelativeSpeed { get; }
        public float AllowedClearance { get; }
        public bool AcceptImpact => Decision == EarthProjectileSurfaceContactDecision.Impact;
        public bool PreserveTangentialTravel =>
            Decision is EarthProjectileSurfaceContactDecision.Grazing or
                EarthProjectileSurfaceContactDecision.OutsideClearance;
    }

    /// <summary>
    /// Pure semantic boundary between an actual projectile impact and a broad-phase
    /// proximity/grazing contact. Presentation and PhysX remain runtime concerns.
    /// </summary>
    public static class EarthProjectileSurfaceContactSolver
    {
        private const float NormalEpsilonSq = 0.0001f;

        public static EarthProjectileSurfaceContactResult Resolve(
            in EarthProjectileSurfaceContactState state,
            in EarthProjectileSurfaceContactSample sample,
            in EarthProjectileSurfaceContactTuning tuning)
        {
            if (!sample.HasContact)
                return Result(in state, EarthProjectileSurfaceContactDecision.NoContact, 0f, 0f, 0f);

            float normalLengthSq = math.lengthsq(sample.SurfaceNormal);
            if (!math.all(math.isfinite(sample.ProjectileVelocity)) ||
                !math.all(math.isfinite(sample.SurfaceVelocity)) ||
                !math.all(math.isfinite(sample.SurfaceNormal)) ||
                !math.isfinite(sample.SurfaceClearance) ||
                !math.isfinite(sample.ProjectileRadius) ||
                normalLengthSq < NormalEpsilonSq)
            {
                return Result(in state, EarthProjectileSurfaceContactDecision.InvalidGeometry, 0f, 0f, 0f);
            }

            float3 normal = sample.SurfaceNormal * math.rsqrt(normalLengthSq);
            float3 relativeVelocity = sample.ProjectileVelocity - sample.SurfaceVelocity;
            float relativeSpeed = math.length(relativeVelocity);
            float approachSpeed = math.max(0f, -math.dot(relativeVelocity, normal));
            float allowedClearance = math.max(
                tuning.MaximumSurfaceClearance,
                math.max(0f, sample.ProjectileRadius) * tuning.MaximumClearanceRadiusRatio);
            if (sample.SurfaceClearance > allowedClearance)
            {
                return Result(
                    in state,
                    EarthProjectileSurfaceContactDecision.OutsideClearance,
                    approachSpeed,
                    relativeSpeed,
                    allowedClearance);
            }

            float requiredApproach = math.max(
                tuning.MinimumApproachSpeed,
                relativeSpeed * tuning.MinimumApproachFraction);
            if (approachSpeed < requiredApproach)
            {
                return Result(
                    in state,
                    EarthProjectileSurfaceContactDecision.Grazing,
                    approachSpeed,
                    relativeSpeed,
                    allowedClearance);
            }

            float time = math.isfinite(sample.Time) ? sample.Time : 0f;
            if (state.HasImpact &&
                state.LastImpactSurfaceId == sample.SurfaceId &&
                time >= state.LastImpactTime &&
                time - state.LastImpactTime <= tuning.ContactEpisodeSeconds)
            {
                return Result(
                    in state,
                    EarthProjectileSurfaceContactDecision.Duplicate,
                    approachSpeed,
                    relativeSpeed,
                    allowedClearance);
            }

            var nextState = new EarthProjectileSurfaceContactState(sample.SurfaceId, time, true);
            return Result(
                in nextState,
                EarthProjectileSurfaceContactDecision.Impact,
                approachSpeed,
                relativeSpeed,
                allowedClearance);
        }

        private static EarthProjectileSurfaceContactResult Result(
            in EarthProjectileSurfaceContactState state,
            EarthProjectileSurfaceContactDecision decision,
            float approachSpeed,
            float relativeSpeed,
            float allowedClearance) =>
            new(state, decision, approachSpeed, relativeSpeed, allowedClearance);
    }
}
