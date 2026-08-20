using Unity.Mathematics;

namespace Elemental.Simulation.Matter
{
    public enum EarthReturnPhase : byte
    {
        Idle = 0,
        CapturedForReturn = 1,
        ReturnPathPlanning = 2,
        SubsurfaceTransfer = 3,
        Reintegrating = 4,
        SdfCommitPending = 5,
        Completed = 6,
        Cancelled = 7,
        Jammed = 8
    }

    public enum EarthReturnDestinationKind : byte
    {
        ProvenanceCavity = 0,
        SelectedCrater = 1,
        NearestStableSurface = 2,
        DormantStorage = 3
    }

    public readonly struct EarthReturnDestination
    {
        public EarthReturnDestination(EarthReturnDestinationKind kind, float3 planetLocalPoint)
        {
            Kind = kind;
            PlanetLocalPoint = planetLocalPoint;
        }
        public EarthReturnDestinationKind Kind { get; }
        public float3 PlanetLocalPoint { get; }
    }

    public readonly struct EarthReturnConfiguration
    {
        public EarthReturnConfiguration(
            float minimumDuration,
            float maximumDuration,
            float travelSpeed,
            float arrivalDistance,
            float corridorLift,
            float stiffness,
            float damping,
            float accelerationLimit)
        {
            MinimumDuration = math.max(0.05f, minimumDuration);
            MaximumDuration = math.max(MinimumDuration, maximumDuration);
            TravelSpeed = math.max(0.1f, travelSpeed);
            ArrivalDistance = math.max(0.02f, arrivalDistance);
            CorridorLift = math.max(0f, corridorLift);
            Stiffness = math.max(0f, stiffness);
            Damping = math.max(0f, damping);
            AccelerationLimit = math.max(0.1f, accelerationLimit);
        }

        public float MinimumDuration { get; }
        public float MaximumDuration { get; }
        public float TravelSpeed { get; }
        public float ArrivalDistance { get; }
        public float CorridorLift { get; }
        public float Stiffness { get; }
        public float Damping { get; }
        public float AccelerationLimit { get; }

        public static EarthReturnConfiguration Default => new EarthReturnConfiguration(
            0.32f, 1.35f, 11f, 0.14f, 0.55f, 48f, 13f, 75f);
    }

    public readonly struct EarthReturnFrame
    {
        public EarthReturnFrame(EarthReturnPhase phase, float3 target, float3 acceleration, bool requestCommit)
        {
            Phase = phase;
            Target = target;
            Acceleration = acceleration;
            RequestCommit = requestCommit;
        }
        public EarthReturnPhase Phase { get; }
        public float3 Target { get; }
        public float3 Acceleration { get; }
        public bool RequestCommit { get; }
    }

    public static class EarthReturnDestinationResolver
    {
        public static EarthReturnDestination Resolve(
            in EarthMatterRecord record,
            float3 selectedCrater,
            bool hasSelectedCrater,
            float3 nearestStableSurface,
            bool hasStableSurface)
        {
            if (record.Source.CanReturnExactly && math.all(math.isfinite(record.Source.SourceLocalPoint)))
                return new EarthReturnDestination(
                    EarthReturnDestinationKind.ProvenanceCavity,
                    record.Source.SourceLocalPoint);
            if (hasSelectedCrater && math.all(math.isfinite(selectedCrater)))
                return new EarthReturnDestination(EarthReturnDestinationKind.SelectedCrater, selectedCrater);
            if (hasStableSurface && math.all(math.isfinite(nearestStableSurface)))
                return new EarthReturnDestination(EarthReturnDestinationKind.NearestStableSurface, nearestStableSurface);
            return new EarthReturnDestination(EarthReturnDestinationKind.DormantStorage, record.CurrentPose.Position);
        }
    }

    public static class EarthReturnGeometry
    {
        public static float SphereRadiusForVolume(float volume) =>
            math.pow(math.max(0.000001f, volume) * 3f / (4f * math.PI), 1f / 3f);

        public static float SphereVolume(float radius) =>
            (4f / 3f) * math.PI * math.pow(math.max(0f, radius), 3f);
    }

    /// <summary>
    /// Deterministic, allocation-free return state machine. It plans one curved
    /// capture corridor from stable matter identity; the runtime remains responsible
    /// for collision response and for the atomic voxel receipt.
    /// </summary>
    public sealed class EarthReturnSession
    {
        private EarthReturnConfiguration _configuration;
        private float3 _start;
        private float3 _control;
        private float3 _destination;
        private float _elapsed;
        private float _duration;

        public EarthReturnPhase Phase { get; private set; }
        public EarthMatterId MatterId { get; private set; }
        public EarthReturnDestination Destination { get; private set; }
        public uint PendingTransactionId { get; private set; }
        public bool IsActive => Phase >= EarthReturnPhase.CapturedForReturn &&
                                Phase <= EarthReturnPhase.SdfCommitPending;

        public bool Begin(
            EarthMatterId matterId,
            in EarthMatterRecord record,
            float3 startPlanetLocal,
            in EarthReturnDestination destination,
            in EarthReturnConfiguration configuration)
        {
            if (!matterId.IsValid || !math.all(math.isfinite(startPlanetLocal)) ||
                !math.all(math.isfinite(destination.PlanetLocalPoint))) return false;
            MatterId = matterId;
            Destination = destination;
            _configuration = configuration;
            _start = startPlanetLocal;
            _destination = destination.PlanetLocalPoint;
            float distance = math.distance(_start, _destination);
            _duration = math.clamp(distance / configuration.TravelSpeed,
                configuration.MinimumDuration, configuration.MaximumDuration);
            float3 radial = math.normalizesafe(_start + _destination, math.up());
            float3 chord = _destination - _start;
            float3 side = DeterministicPerpendicular(chord, radial, matterId.StableId);
            float signedSide = ((matterId.StableId & 1u) == 0u ? 1f : -1f) *
                               math.min(0.38f, distance * 0.08f);
            _control = (_start + _destination) * 0.5f +
                       radial * (configuration.CorridorLift + math.min(0.8f, distance * 0.06f)) +
                       side * signedSide;
            _elapsed = 0f;
            PendingTransactionId = 0u;
            Phase = EarthReturnPhase.CapturedForReturn;
            return true;
        }

        public EarthReturnFrame Step(float deltaTime, float3 currentPlanetLocal, float3 currentVelocity)
        {
            if (!IsActive || Phase == EarthReturnPhase.SdfCommitPending)
                return new EarthReturnFrame(Phase, _destination, float3.zero, false);
            float dt = math.max(0f, deltaTime);
            _elapsed += dt;
            if (Phase == EarthReturnPhase.CapturedForReturn)
                Phase = EarthReturnPhase.ReturnPathPlanning;
            if (Phase == EarthReturnPhase.ReturnPathPlanning)
                Phase = EarthReturnPhase.SubsurfaceTransfer;

            float t = math.saturate(_elapsed / math.max(0.0001f, _duration));
            float eased = t * t * (3f - 2f * t);
            float3 target = Quadratic(_start, _control, _destination, eased);
            float3 desiredVelocity = QuadraticDerivative(_start, _control, _destination, eased) /
                                     math.max(0.0001f, _duration);
            float3 acceleration = (target - currentPlanetLocal) * _configuration.Stiffness +
                                  (desiredVelocity - currentVelocity) * _configuration.Damping;
            acceleration = Limit(acceleration, _configuration.AccelerationLimit);
            bool arrival = (t >= 1f && math.distance(currentPlanetLocal, _destination) <=
                            _configuration.ArrivalDistance * 2f) ||
                           math.distance(currentPlanetLocal, _destination) <= _configuration.ArrivalDistance;
            if (arrival) Phase = EarthReturnPhase.Reintegrating;
            return new EarthReturnFrame(Phase, target, acceleration, arrival);
        }

        public bool ReverseBeforeCommit()
        {
            if (Phase == EarthReturnPhase.SdfCommitPending || Phase == EarthReturnPhase.Completed ||
                Phase == EarthReturnPhase.Idle) return false;
            Phase = EarthReturnPhase.Cancelled;
            return true;
        }

        public bool MarkSdfCommitPending(uint transactionId)
        {
            if (Phase != EarthReturnPhase.Reintegrating || transactionId == 0u) return false;
            PendingTransactionId = transactionId;
            Phase = EarthReturnPhase.SdfCommitPending;
            return true;
        }

        public bool ConfirmCommit(uint transactionId)
        {
            if (Phase != EarthReturnPhase.SdfCommitPending || transactionId != PendingTransactionId) return false;
            Phase = EarthReturnPhase.Completed;
            return true;
        }

        public bool MarkJammed()
        {
            if (!IsActive || Phase == EarthReturnPhase.SdfCommitPending) return false;
            Phase = EarthReturnPhase.Jammed;
            return true;
        }

        private static float3 Quadratic(float3 a, float3 b, float3 c, float t)
        {
            float oneMinus = 1f - t;
            return oneMinus * oneMinus * a + 2f * oneMinus * t * b + t * t * c;
        }

        private static float3 QuadraticDerivative(float3 a, float3 b, float3 c, float t) =>
            2f * (1f - t) * (b - a) + 2f * t * (c - b);

        private static float3 Limit(float3 value, float maximum)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > maximum * maximum
                ? value * (maximum / math.sqrt(lengthSq))
                : value;
        }

        private static float3 DeterministicPerpendicular(float3 direction, float3 radial, uint seed)
        {
            float3 tangent = math.cross(math.normalizesafe(direction, radial), radial);
            if (math.lengthsq(tangent) < 0.0001f)
                tangent = math.cross(radial, math.abs(radial.y) < 0.9f ? math.up() : new float3(1f, 0f, 0f));
            tangent = math.normalizesafe(tangent, new float3(1f, 0f, 0f));
            uint hash = seed * 747796405u + 2891336453u;
            float angle = ((hash >> 8) & 1023u) * (math.PI * 2f / 1024f);
            return math.normalizesafe(tangent * math.cos(angle) + math.cross(radial, tangent) * math.sin(angle), tangent);
        }
    }
}
