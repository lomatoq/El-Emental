using System;
using Elemental.Simulation.Combat;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthBodyRegion : byte
    {
        Pelvis = 0,
        Spine = 1,
        Chest = 2,
        Head = 3,
        Arm = 4,
        Leg = 5
    }

    public enum EarthMuscleProfileId : byte
    {
        Stable = 0,
        Reactive = 1,
        Stagger = 2,
        FallProtect = 3,
        Ragdoll = 4,
        Recovery = 5
    }

    public enum EarthRecoveryFoot : byte
    {
        None = 0,
        Left = 1,
        Right = 2
    }

    public enum EarthPoweredImpactOwner : byte
    {
        None = 0,
        AgentAInertialResponse = 1,
        PoweredPhysicalAssist = 2,
        ExistingFullRagdoll = 3
    }

    [Flags]
    public enum EarthPoweredBehaviour : byte
    {
        None = 0,
        MaintainBalance = 1 << 0,
        StaggerStep = 1 << 1,
        BraceAgainstSurface = 1 << 2,
        ProtectHead = 1 << 3,
        FallArrest = 1 << 4,
        ReachForSupport = 1 << 5
    }

    public enum EarthPhysicalActionKind : byte
    {
        None = 0,
        AuthoredRecoveryStep = 1,
        BraceAgainstSurface = 2,
        ProtectHead = 3,
        FallArrest = 4,
        ReachForSupport = 5
    }

    public enum EarthSemanticSurfaceKind : byte
    {
        None = 0,
        Braceable = 1,
        ReachableSupport = 2,
        FallArrest = 3
    }

    public readonly struct EarthMuscleRegionTuning
    {
        public EarthMuscleRegionTuning(
            float frequency,
            float damping,
            float torqueCap,
            float angularLimitDegrees,
            float driveWeight,
            float transferWeight,
            float recoveryRate)
        {
            Frequency = math.max(0f, math.isfinite(frequency) ? frequency : 0f);
            Damping = math.max(0f, math.isfinite(damping) ? damping : 0f);
            TorqueCap = math.max(0f, math.isfinite(torqueCap) ? torqueCap : 0f);
            AngularLimitDegrees = math.clamp(
                math.isfinite(angularLimitDegrees) ? angularLimitDegrees : 1f,
                1f,
                90f);
            DriveWeight = math.saturate(math.isfinite(driveWeight) ? driveWeight : 0f);
            TransferWeight = math.saturate(
                math.isfinite(transferWeight) ? transferWeight : 0f);
            RecoveryRate = math.max(0f,
                math.isfinite(recoveryRate) ? recoveryRate : 0f);
        }

        public float Frequency { get; }
        public float Damping { get; }
        public float TorqueCap { get; }
        public float AngularLimitDegrees { get; }
        public float DriveWeight { get; }
        public float TransferWeight { get; }
        public float RecoveryRate { get; }
    }

    public readonly struct EarthMuscleProfile
    {
        public EarthMuscleProfile(
            EarthMuscleProfileId id,
            in EarthMuscleRegionTuning pelvis,
            in EarthMuscleRegionTuning spine,
            in EarthMuscleRegionTuning chest,
            in EarthMuscleRegionTuning head,
            in EarthMuscleRegionTuning arm,
            in EarthMuscleRegionTuning leg)
        {
            Id = id;
            Pelvis = pelvis;
            Spine = spine;
            Chest = chest;
            Head = head;
            Arm = arm;
            Leg = leg;
        }

        public EarthMuscleProfileId Id { get; }
        public EarthMuscleRegionTuning Pelvis { get; }
        public EarthMuscleRegionTuning Spine { get; }
        public EarthMuscleRegionTuning Chest { get; }
        public EarthMuscleRegionTuning Head { get; }
        public EarthMuscleRegionTuning Arm { get; }
        public EarthMuscleRegionTuning Leg { get; }

        public EarthMuscleRegionTuning For(EarthBodyRegion region) => region switch
        {
            EarthBodyRegion.Pelvis => Pelvis,
            EarthBodyRegion.Spine => Spine,
            EarthBodyRegion.Chest => Chest,
            EarthBodyRegion.Head => Head,
            EarthBodyRegion.Arm => Arm,
            EarthBodyRegion.Leg => Leg,
            _ => default
        };
    }

    public static class EarthMuscleProfiles
    {
        public static EarthMuscleProfile Resolve(EarthMuscleProfileId id)
        {
            EarthMuscleRegionTuning off = T(0f, 0f, 0f, 90f, 0f, 0f, 20f);
            return id switch
            {
                EarthMuscleProfileId.Reactive => new EarthMuscleProfile(
                    id,
                    T(3.2f, 0.95f, 150f, 14f, 0.45f, 0.30f, 8f),
                    T(4.2f, 0.82f, 190f, 28f, 0.72f, 0.78f, 9f),
                    T(4.8f, 0.78f, 210f, 34f, 0.76f, 0.92f, 10f),
                    T(5.5f, 0.92f, 90f, 22f, 0.82f, 0.55f, 12f),
                    T(4.0f, 0.76f, 115f, 48f, 0.68f, 0.88f, 8f),
                    T(0f, 0f, 0f, 45f, 0f, 0f, 12f)),
                EarthMuscleProfileId.Stagger => new EarthMuscleProfile(
                    id,
                    T(2.8f, 0.98f, 135f, 18f, 0.38f, 0.25f, 6f),
                    T(3.5f, 0.78f, 170f, 38f, 0.58f, 0.82f, 7f),
                    T(3.8f, 0.74f, 185f, 44f, 0.60f, 1f, 7f),
                    T(5.2f, 0.94f, 95f, 25f, 0.85f, 0.62f, 11f),
                    T(3.6f, 0.72f, 125f, 58f, 0.62f, 1f, 7f),
                    T(0f, 0f, 0f, 55f, 0f, 0f, 12f)),
                EarthMuscleProfileId.FallProtect => new EarthMuscleProfile(
                    id,
                    T(1.8f, 1f, 90f, 28f, 0.22f, 0.20f, 5f),
                    T(3.2f, 0.9f, 145f, 45f, 0.48f, 0.72f, 7f),
                    T(3.5f, 0.88f, 165f, 52f, 0.52f, 0.85f, 7f),
                    T(6.2f, 1f, 130f, 18f, 1f, 0.82f, 14f),
                    T(5.5f, 0.92f, 180f, 70f, 0.92f, 1f, 12f),
                    T(1.4f, 1f, 80f, 65f, 0.12f, 0.18f, 6f)),
                EarthMuscleProfileId.Ragdoll => new EarthMuscleProfile(
                    id, off, off, off, off, off, off),
                EarthMuscleProfileId.Recovery => new EarthMuscleProfile(
                    id,
                    T(3.5f, 1f, 160f, 20f, 0.55f, 0.45f, 3f),
                    T(4.0f, 0.95f, 180f, 28f, 0.62f, 0.58f, 3.5f),
                    T(4.2f, 0.9f, 190f, 32f, 0.65f, 0.65f, 4f),
                    T(5.0f, 1f, 100f, 20f, 0.75f, 0.50f, 5f),
                    T(3.5f, 0.9f, 105f, 48f, 0.50f, 0.55f, 4f),
                    T(2.5f, 1f, 130f, 32f, 0.35f, 0.30f, 3f)),
                _ => new EarthMuscleProfile(
                    EarthMuscleProfileId.Stable,
                    T(5.0f, 1f, 230f, 14f, 0.90f, 0.45f, 10f),
                    T(5.5f, 0.95f, 240f, 20f, 0.92f, 0.65f, 10f),
                    T(5.8f, 0.92f, 250f, 24f, 0.94f, 0.72f, 11f),
                    T(6.0f, 1f, 110f, 18f, 0.96f, 0.42f, 13f),
                    T(4.5f, 0.9f, 130f, 42f, 0.82f, 0.62f, 9f),
                    T(4.8f, 1f, 210f, 28f, 0.88f, 0.38f, 10f))
            };
        }

        public static float EstimateBoundedJointEnergy(
            float errorDegrees,
            float angularSpeedRadians,
            in EarthMuscleRegionTuning tuning)
        {
            float angle = math.min(
                math.radians(math.abs(errorDegrees)),
                math.radians(tuning.AngularLimitDegrees));
            float frequencyRadians = 2f * math.PI * tuning.Frequency;
            float springTorque = frequencyRadians * frequencyRadians * angle * tuning.DriveWeight;
            float dampingTorque = 2f * tuning.Damping * frequencyRadians *
                                  math.abs(angularSpeedRadians) * tuning.DriveWeight;
            float torque = math.min(tuning.TorqueCap, springTorque + dampingTorque);
            return math.max(0f, torque * angle * tuning.TransferWeight);
        }

        public static float StepDriveWeight(
            float current,
            float target,
            float recoveryRate,
            float deltaTime)
        {
            float blend = 1f - math.exp(-math.max(0f, recoveryRate) *
                                        math.max(0f, deltaTime));
            return math.lerp(math.saturate(current), math.saturate(target), blend);
        }

        private static EarthMuscleRegionTuning T(
            float frequency,
            float damping,
            float torque,
            float limit,
            float drive,
            float transfer,
            float recovery) =>
            new EarthMuscleRegionTuning(
                frequency, damping, torque, limit, drive, transfer, recovery);
    }

    public readonly struct EarthSupportPolygon
    {
        public EarthSupportPolygon(
            float3 point0,
            float3 point1,
            float3 point2,
            float3 point3,
            int count)
        {
            Point0 = point0;
            Point1 = point1;
            Point2 = point2;
            Point3 = point3;
            Count = math.clamp(count, 0, 4);
        }

        public float3 Point0 { get; }
        public float3 Point1 { get; }
        public float3 Point2 { get; }
        public float3 Point3 { get; }
        public int Count { get; }
        public bool IsValid => Count >= 3 &&
                               math.all(math.isfinite(Point0)) &&
                               math.all(math.isfinite(Point1)) &&
                               math.all(math.isfinite(Point2));

        public float3 GetPoint(int index) => index switch
        {
            0 => Point0,
            1 => Point1,
            2 => Point2,
            _ => Point3
        };

        public static EarthSupportPolygon FromFeet(
            float3 leftFoot,
            float3 rightFoot,
            float3 gravityUp,
            float3 facing,
            float footHalfLength,
            float footHalfWidth)
        {
            float3 up = math.normalizesafe(gravityUp, new float3(0f, 1f, 0f));
            float3 forward = math.normalizesafe(ProjectOnPlane(facing, up),
                math.normalizesafe(math.cross(new float3(1f, 0f, 0f), up), new float3(0f, 0f, 1f)));
            float3 right = math.normalizesafe(math.cross(up, forward), new float3(1f, 0f, 0f));
            float3 center = (leftFoot + rightFoot) * 0.5f;
            float halfWidth = math.max(footHalfWidth,
                math.abs(math.dot(rightFoot - leftFoot, right)) * 0.5f + footHalfWidth);
            float halfLength = math.max(0.02f, footHalfLength);
            return new EarthSupportPolygon(
                center - right * halfWidth - forward * halfLength,
                center + right * halfWidth - forward * halfLength,
                center + right * halfWidth + forward * halfLength,
                center - right * halfWidth + forward * halfLength,
                4);
        }

        internal static float3 ProjectOnPlane(float3 value, float3 normal) =>
            value - normal * math.dot(value, normal);
    }

    public readonly struct EarthBalanceDecision
    {
        public EarthBalanceDecision(float signedMargin, float3 correctionDirection)
        {
            SignedMargin = signedMargin;
            CorrectionDirection = correctionDirection;
        }

        public float SignedMargin { get; }
        public float3 CorrectionDirection { get; }
        public bool IsOutside => SignedMargin < 0f;
    }

    public static class EarthSupportPolygonSolver
    {
        public static EarthBalanceDecision Evaluate(
            float3 centerOfMass,
            float3 gravityUp,
            in EarthSupportPolygon polygon)
        {
            if (!polygon.IsValid || !math.all(math.isfinite(centerOfMass)))
                return new EarthBalanceDecision(float.NegativeInfinity, float3.zero);

            float3 up = math.normalizesafe(gravityUp, new float3(0f, 1f, 0f));
            float winding = 0f;
            for (int index = 0; index < polygon.Count; index++)
            {
                float3 a = polygon.GetPoint(index);
                float3 b = polygon.GetPoint((index + 1) % polygon.Count);
                winding += math.dot(math.cross(a, b), up);
            }
            float orientation = winding >= 0f ? 1f : -1f;
            float minimumMargin = float.PositiveInfinity;
            float3 correction = float3.zero;
            for (int index = 0; index < polygon.Count; index++)
            {
                float3 a = polygon.GetPoint(index);
                float3 b = polygon.GetPoint((index + 1) % polygon.Count);
                float3 edge = EarthSupportPolygon.ProjectOnPlane(b - a, up);
                float edgeLength = math.length(edge);
                if (edgeLength <= 0.0001f) continue;
                float3 inward = math.normalizesafe(math.cross(up, edge) * orientation);
                float margin = math.dot(
                    EarthSupportPolygon.ProjectOnPlane(centerOfMass - a, up), inward);
                if (margin < minimumMargin)
                {
                    minimumMargin = margin;
                    correction = inward;
                }
            }
            if (!float.IsFinite(minimumMargin))
                return new EarthBalanceDecision(float.NegativeInfinity, float3.zero);
            return new EarthBalanceDecision(minimumMargin, correction);
        }
    }

    public readonly struct EarthPhysicalSurfaceProbe
    {
        public EarthPhysicalSurfaceProbe(
            EarthSemanticSurfaceKind kind,
            float3 point,
            float3 normal,
            float distance,
            bool accepted)
        {
            Kind = kind;
            Point = point;
            Normal = math.normalizesafe(normal, new float3(0f, 1f, 0f));
            Distance = math.max(0f, distance);
            Accepted = accepted && kind != EarthSemanticSurfaceKind.None &&
                       math.all(math.isfinite(point)) && float.IsFinite(distance);
        }

        public EarthSemanticSurfaceKind Kind { get; }
        public float3 Point { get; }
        public float3 Normal { get; }
        public float Distance { get; }
        public bool Accepted { get; }

        public bool IsReachable(EarthSemanticSurfaceKind requiredKind, float maximumReach) =>
            Accepted && Kind == requiredKind && Distance <= math.max(0f, maximumReach);
    }

    public readonly struct EarthPoweredImpactDecision
    {
        public EarthPoweredImpactDecision(
            uint responseId,
            EarthPoweredImpactOwner owner,
            bool accepted,
            bool duplicate)
        {
            ResponseId = responseId;
            Owner = owner;
            Accepted = accepted;
            Duplicate = duplicate;
        }

        public uint ResponseId { get; }
        public EarthPoweredImpactOwner Owner { get; }
        public bool Accepted { get; }
        public bool Duplicate { get; }
        public bool EmitsImpulse => false;
        public bool RequestsRagdoll => false;
    }

    public readonly struct EarthPhysicalActionRequest
    {
        public EarthPhysicalActionRequest(
            uint responseId,
            EarthPhysicalActionKind kind,
            EarthRecoveryFoot foot,
            float3 worldPoint,
            float3 worldDirection)
        {
            ResponseId = responseId;
            Kind = kind;
            Foot = foot;
            WorldPoint = worldPoint;
            WorldDirection = math.normalizesafe(worldDirection, float3.zero);
        }

        public uint ResponseId { get; }
        public EarthPhysicalActionKind Kind { get; }
        public EarthRecoveryFoot Foot { get; }
        public float3 WorldPoint { get; }
        public float3 WorldDirection { get; }
        public bool IsValid => ResponseId != 0u && Kind != EarthPhysicalActionKind.None;
    }

    public readonly struct EarthPoweredAssistInput
    {
        public EarthPoweredAssistInput(
            float deltaTime,
            CharacterPhysicalMode canonicalMode,
            float3 gravityUp,
            float3 facing,
            float3 centerOfMass,
            float3 linearVelocity,
            bool stableSupport,
            bool feetValid,
            in EarthSupportPolygon supportPolygon,
            in EarthPhysicalSurfaceProbe braceProbe,
            in EarthPhysicalSurfaceProbe reachProbe,
            in EarthPhysicalSurfaceProbe fallArrestProbe)
        {
            DeltaTime = deltaTime;
            CanonicalMode = canonicalMode;
            GravityUp = gravityUp;
            Facing = facing;
            CenterOfMass = centerOfMass;
            LinearVelocity = linearVelocity;
            StableSupport = stableSupport;
            FeetValid = feetValid;
            SupportPolygon = supportPolygon;
            BraceProbe = braceProbe;
            ReachProbe = reachProbe;
            FallArrestProbe = fallArrestProbe;
        }

        public float DeltaTime { get; }
        public CharacterPhysicalMode CanonicalMode { get; }
        public float3 GravityUp { get; }
        public float3 Facing { get; }
        public float3 CenterOfMass { get; }
        public float3 LinearVelocity { get; }
        public bool StableSupport { get; }
        public bool FeetValid { get; }
        public EarthSupportPolygon SupportPolygon { get; }
        public EarthPhysicalSurfaceProbe BraceProbe { get; }
        public EarthPhysicalSurfaceProbe ReachProbe { get; }
        public EarthPhysicalSurfaceProbe FallArrestProbe { get; }
    }

    public readonly struct EarthPoweredAssistOutput
    {
        public EarthPoweredAssistOutput(
            EarthMuscleProfileId profile,
            EarthPoweredBehaviour behaviours,
            in EarthBalanceDecision balance,
            in EarthPhysicalActionRequest action,
            bool emitAction,
            float responseWeight)
        {
            Profile = profile;
            Behaviours = behaviours;
            Balance = balance;
            Action = action;
            EmitAction = emitAction;
            ResponseWeight = math.saturate(responseWeight);
        }

        public EarthMuscleProfileId Profile { get; }
        public EarthPoweredBehaviour Behaviours { get; }
        public EarthBalanceDecision Balance { get; }
        public EarthPhysicalActionRequest Action { get; }
        public bool EmitAction { get; }
        public float ResponseWeight { get; }
        public bool PreservesFeet => Profile == EarthMuscleProfileId.Reactive ||
                                     Profile == EarthMuscleProfileId.Stagger;
    }

    /// <summary>
    /// Deterministic, bounded temporal decision layer. It consumes the canonical
    /// CharacterPhysicalMode but never owns or mutates that mode.
    /// </summary>
    public sealed class EarthPoweredPhysicalAssist
    {
        public const int ResponseHistoryCapacity = 16;
        public const float MaximumSemanticReach = 1.35f;
        public const float MediumResponseSeconds = 0.48f;

        private readonly uint[] _recentResponseIds = new uint[ResponseHistoryCapacity];
        private int _responseCursor;
        private uint _activeResponseId;
        private float3 _activeDirection;
        private float _activeIntensity;
        private float _responseSeconds;
        private uint _emittedActionResponseId;

        public int AcceptedResponseCount { get; private set; }
        public uint ActiveResponseId => _activeResponseId;

        public EarthPoweredImpactDecision RouteAcceptedResponse(
            uint responseId,
            EarthCharacterImpactResponse response,
            float intensity01,
            float3 direction)
        {
            if (responseId == 0u || response == EarthCharacterImpactResponse.Ignore)
                return default;
            if (ContainsResponse(responseId))
                return new EarthPoweredImpactDecision(responseId, EarthPoweredImpactOwner.None, false, true);

            RememberResponse(responseId);
            AcceptedResponseCount++;
            EarthPoweredImpactOwner owner = response switch
            {
                EarthCharacterImpactResponse.Flinch => EarthPoweredImpactOwner.AgentAInertialResponse,
                EarthCharacterImpactResponse.Stagger => EarthPoweredImpactOwner.PoweredPhysicalAssist,
                EarthCharacterImpactResponse.RecoverableKnockdown or
                    EarthCharacterImpactResponse.Knockout => EarthPoweredImpactOwner.ExistingFullRagdoll,
                _ => EarthPoweredImpactOwner.None
            };
            if (owner == EarthPoweredImpactOwner.PoweredPhysicalAssist)
            {
                _activeResponseId = responseId;
                _activeDirection = math.all(math.isfinite(direction))
                    ? math.normalizesafe(direction, float3.zero)
                    : float3.zero;
                _activeIntensity = math.saturate(
                    math.isfinite(intensity01) ? intensity01 : 0f);
                _responseSeconds = MediumResponseSeconds;
                _emittedActionResponseId = 0u;
            }
            else if (owner == EarthPoweredImpactOwner.ExistingFullRagdoll)
            {
                ResetTemporalState();
            }
            return new EarthPoweredImpactDecision(responseId, owner, true, false);
        }

        public EarthPoweredAssistOutput Step(in EarthPoweredAssistInput input)
        {
            if (!float.IsFinite(input.DeltaTime) || input.DeltaTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(input),
                    "Powered-assist input requires a finite positive delta time.");

            float3 up = math.normalizesafe(input.GravityUp, new float3(0f, 1f, 0f));
            bool canonicalAssist = input.CanonicalMode == CharacterPhysicalMode.PhysicalAssist ||
                                   input.CanonicalMode == CharacterPhysicalMode.Stagger;
            if (!canonicalAssist) ResetTemporalState();
            if (_responseSeconds > 0f)
                _responseSeconds = math.max(0f, _responseSeconds - input.DeltaTime);
            float responseWeight = MediumResponseSeconds > 0f
                ? math.saturate(_responseSeconds / MediumResponseSeconds)
                : 0f;
            EarthMuscleProfileId profile = ResolveProfile(input.CanonicalMode, input.StableSupport);
            EarthPoweredBehaviour behaviours = EarthPoweredBehaviour.None;
            EarthBalanceDecision balance = default;
            EarthPhysicalActionRequest action = default;

            if (input.StableSupport && input.FeetValid && input.SupportPolygon.IsValid)
            {
                behaviours |= EarthPoweredBehaviour.MaintainBalance;
                EarthSupportPolygon supportPolygon = input.SupportPolygon;
                balance = EarthSupportPolygonSolver.Evaluate(
                    input.CenterOfMass, up, in supportPolygon);
                if (canonicalAssist && balance.IsOutside && _activeResponseId != 0u)
                {
                    behaviours |= EarthPoweredBehaviour.StaggerStep;
                    float3 right = math.normalizesafe(math.cross(
                        up,
                        math.normalizesafe(EarthSupportPolygon.ProjectOnPlane(input.Facing, up),
                            new float3(0f, 0f, 1f))),
                        new float3(1f, 0f, 0f));
                    EarthRecoveryFoot foot = math.dot(balance.CorrectionDirection, right) < 0f
                        ? EarthRecoveryFoot.Right
                        : EarthRecoveryFoot.Left;
                    action = new EarthPhysicalActionRequest(
                        _activeResponseId,
                        EarthPhysicalActionKind.AuthoredRecoveryStep,
                        foot,
                        input.CenterOfMass,
                        -balance.CorrectionDirection);
                }
                else if (canonicalAssist &&
                         input.BraceProbe.IsReachable(
                             EarthSemanticSurfaceKind.Braceable,
                             MaximumSemanticReach))
                {
                    behaviours |= EarthPoweredBehaviour.BraceAgainstSurface;
                    action = new EarthPhysicalActionRequest(
                        _activeResponseId,
                        EarthPhysicalActionKind.BraceAgainstSurface,
                        EarthRecoveryFoot.None,
                        input.BraceProbe.Point,
                        -input.BraceProbe.Normal);
                }
            }
            else if (canonicalAssist)
            {
                float downwardSpeed = -math.dot(input.LinearVelocity, up);
                if (downwardSpeed >= 2.5f &&
                    input.FallArrestProbe.IsReachable(
                        EarthSemanticSurfaceKind.FallArrest,
                        MaximumSemanticReach))
                {
                    profile = EarthMuscleProfileId.FallProtect;
                    behaviours |= EarthPoweredBehaviour.ProtectHead |
                                  EarthPoweredBehaviour.FallArrest;
                    action = new EarthPhysicalActionRequest(
                        _activeResponseId,
                        EarthPhysicalActionKind.FallArrest,
                        EarthRecoveryFoot.None,
                        input.FallArrestProbe.Point,
                        -input.FallArrestProbe.Normal);
                }
                else if (input.ReachProbe.IsReachable(
                             EarthSemanticSurfaceKind.ReachableSupport,
                             MaximumSemanticReach))
                {
                    profile = EarthMuscleProfileId.FallProtect;
                    behaviours |= EarthPoweredBehaviour.ProtectHead |
                                  EarthPoweredBehaviour.ReachForSupport;
                    action = new EarthPhysicalActionRequest(
                        _activeResponseId,
                        EarthPhysicalActionKind.ReachForSupport,
                        EarthRecoveryFoot.None,
                        input.ReachProbe.Point,
                        -input.ReachProbe.Normal);
                }
                else if (_activeIntensity >= 0.55f)
                {
                    profile = EarthMuscleProfileId.FallProtect;
                    behaviours |= EarthPoweredBehaviour.ProtectHead;
                    action = new EarthPhysicalActionRequest(
                        _activeResponseId,
                        EarthPhysicalActionKind.ProtectHead,
                        EarthRecoveryFoot.None,
                        input.CenterOfMass,
                        -_activeDirection);
                }
            }

            if (canonicalAssist && _activeIntensity >= 0.75f)
            {
                behaviours |= EarthPoweredBehaviour.ProtectHead;
                if (!action.IsValid)
                    action = new EarthPhysicalActionRequest(
                        _activeResponseId,
                        EarthPhysicalActionKind.ProtectHead,
                        EarthRecoveryFoot.None,
                        input.CenterOfMass,
                        -_activeDirection);
            }

            bool emit = action.IsValid && _emittedActionResponseId != action.ResponseId;
            if (emit) _emittedActionResponseId = action.ResponseId;
            return new EarthPoweredAssistOutput(
                profile, behaviours, in balance, in action, emit, responseWeight);
        }

        public void ResetTemporalState()
        {
            _activeResponseId = 0u;
            _activeDirection = float3.zero;
            _activeIntensity = 0f;
            _responseSeconds = 0f;
            _emittedActionResponseId = 0u;
        }

        private static EarthMuscleProfileId ResolveProfile(
            CharacterPhysicalMode mode,
            bool stableSupport) => mode switch
        {
            CharacterPhysicalMode.PhysicalAssist => stableSupport
                ? EarthMuscleProfileId.Reactive
                : EarthMuscleProfileId.FallProtect,
            CharacterPhysicalMode.Stagger => stableSupport
                ? EarthMuscleProfileId.Stagger
                : EarthMuscleProfileId.FallProtect,
            CharacterPhysicalMode.FullRagdoll => EarthMuscleProfileId.Ragdoll,
            CharacterPhysicalMode.Recovery => EarthMuscleProfileId.Recovery,
            _ => EarthMuscleProfileId.Stable
        };

        private bool ContainsResponse(uint responseId)
        {
            for (int index = 0; index < _recentResponseIds.Length; index++)
                if (_recentResponseIds[index] == responseId) return true;
            return false;
        }

        private void RememberResponse(uint responseId)
        {
            _recentResponseIds[_responseCursor] = responseId;
            _responseCursor = (_responseCursor + 1) % _recentResponseIds.Length;
        }
    }
}
