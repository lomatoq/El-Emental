using System;
using Elemental.Simulation.Combat;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthBodyRegion : byte
    {
        Unassigned = 0,
        Pelvis = 1,
        Spine = 2,
        Chest = 3,
        Head = 4,
        Arm = 5,
        Leg = 6
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

    public enum EarthPoweredAssistRejection : byte
    {
        None = 0,
        FeatureDisabled = 1,
        InvalidBodyRegionBinding = 2,
        CanonicalModeRejected = 3,
        UnstableSupport = 4,
        MissingFeet = 5,
        NoPlantedFoot = 6,
        InvalidSupportPolygon = 7,
        ControllerRejected = 8
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
            Leg = new EarthMuscleRegionTuning(
                0f,
                0f,
                0f,
                leg.AngularLimitDegrees,
                0f,
                0f,
                leg.RecoveryRate);
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
                    off),
                EarthMuscleProfileId.Ragdoll => new EarthMuscleProfile(
                    id, off, off, off, off, off, off),
                EarthMuscleProfileId.Recovery => new EarthMuscleProfile(
                    id,
                    T(3.5f, 1f, 160f, 20f, 0.55f, 0.45f, 3f),
                    T(4.0f, 0.95f, 180f, 28f, 0.62f, 0.58f, 3.5f),
                    T(4.2f, 0.9f, 190f, 32f, 0.65f, 0.65f, 4f),
                    T(5.0f, 1f, 100f, 20f, 0.75f, 0.50f, 5f),
                    T(3.5f, 0.9f, 105f, 48f, 0.50f, 0.55f, 4f),
                    off),
                _ => new EarthMuscleProfile(
                    EarthMuscleProfileId.Stable,
                    T(5.0f, 1f, 230f, 14f, 0.90f, 0.45f, 10f),
                    T(5.5f, 0.95f, 240f, 20f, 0.92f, 0.65f, 10f),
                    T(5.8f, 0.92f, 250f, 24f, 0.94f, 0.72f, 11f),
                    T(6.0f, 1f, 110f, 18f, 0.96f, 0.42f, 13f),
                    T(4.5f, 0.9f, 130f, 42f, 0.82f, 0.62f, 9f),
                    off)
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
            : this(
                point0,
                point1,
                point2,
                point3,
                float3.zero,
                float3.zero,
                float3.zero,
                float3.zero,
                count)
        {
        }

        private EarthSupportPolygon(
            float3 point0,
            float3 point1,
            float3 point2,
            float3 point3,
            float3 point4,
            float3 point5,
            float3 point6,
            float3 point7,
            int count)
        {
            Point0 = point0;
            Point1 = point1;
            Point2 = point2;
            Point3 = point3;
            Point4 = point4;
            Point5 = point5;
            Point6 = point6;
            Point7 = point7;
            Count = math.clamp(count, 0, 8);
        }

        public float3 Point0 { get; }
        public float3 Point1 { get; }
        public float3 Point2 { get; }
        public float3 Point3 { get; }
        public float3 Point4 { get; }
        public float3 Point5 { get; }
        public float3 Point6 { get; }
        public float3 Point7 { get; }
        public int Count { get; }
        public bool IsValid
        {
            get
            {
                if (Count < 3) return false;
                for (int index = 0; index < Count; index++)
                    if (!math.all(math.isfinite(GetPoint(index)))) return false;
                return true;
            }
        }

        public float3 GetPoint(int index) => index switch
        {
            0 => Point0,
            1 => Point1,
            2 => Point2,
            3 => Point3,
            4 => Point4,
            5 => Point5,
            6 => Point6,
            _ => Point7
        };

        public static EarthSupportPolygon FromFeet(
            float3 leftFoot,
            float3 rightFoot,
            float3 gravityUp,
            float3 facing,
            float footHalfLength,
            float footHalfWidth)
        {
            return FromPlantedFeet(
                leftFoot,
                rightFoot,
                true,
                true,
                gravityUp,
                facing,
                footHalfLength,
                footHalfWidth);
        }

        public static EarthSupportPolygon FromPlantedFeet(
            float3 leftFoot,
            float3 rightFoot,
            bool leftPlanted,
            bool rightPlanted,
            float3 gravityUp,
            float3 facing,
            float footHalfLength,
            float footHalfWidth)
        {
            if ((!leftPlanted && !rightPlanted) ||
                (leftPlanted && !math.all(math.isfinite(leftFoot))) ||
                (rightPlanted && !math.all(math.isfinite(rightFoot))))
                return default;

            float3 up = math.normalizesafe(gravityUp, new float3(0f, 1f, 0f));
            float3 forward = math.normalizesafe(ProjectOnPlane(facing, up),
                math.normalizesafe(math.cross(new float3(1f, 0f, 0f), up), new float3(0f, 0f, 1f)));
            float3 right = math.normalizesafe(math.cross(up, forward), new float3(1f, 0f, 0f));
            float halfLength = math.max(0.02f, footHalfLength);
            float halfWidth = math.max(0.02f, footHalfWidth);
            int candidateCount = (leftPlanted ? 4 : 0) + (rightPlanted ? 4 : 0);
            int first = FindHullStart(
                leftFoot,
                rightFoot,
                leftPlanted,
                rightPlanted,
                right,
                forward,
                halfLength,
                halfWidth,
                candidateCount);
            if (first < 0) return default;

            float3 point0 = float3.zero;
            float3 point1 = float3.zero;
            float3 point2 = float3.zero;
            float3 point3 = float3.zero;
            float3 point4 = float3.zero;
            float3 point5 = float3.zero;
            float3 point6 = float3.zero;
            float3 point7 = float3.zero;
            int hullCount = 0;
            int current = first;
            float3 firstPoint = GetCandidate(
                first,
                leftFoot,
                rightFoot,
                leftPlanted,
                rightPlanted,
                right,
                forward,
                halfLength,
                halfWidth);
            do
            {
                float3 currentPoint = GetCandidate(
                    current,
                    leftFoot,
                    rightFoot,
                    leftPlanted,
                    rightPlanted,
                    right,
                    forward,
                    halfLength,
                    halfWidth);
                SetHullPoint(
                    hullCount++,
                    currentPoint,
                    ref point0,
                    ref point1,
                    ref point2,
                    ref point3,
                    ref point4,
                    ref point5,
                    ref point6,
                    ref point7);

                int next = -1;
                float3 nextPoint = float3.zero;
                for (int candidate = 0; candidate < candidateCount; candidate++)
                {
                    float3 candidatePoint = GetCandidate(
                        candidate,
                        leftFoot,
                        rightFoot,
                        leftPlanted,
                        rightPlanted,
                        right,
                        forward,
                        halfLength,
                        halfWidth);
                    float candidateDistance = math.lengthsq(candidatePoint - currentPoint);
                    if (candidateDistance <= 0.00000001f) continue;
                    if (next < 0)
                    {
                        next = candidate;
                        nextPoint = candidatePoint;
                        continue;
                    }

                    float turn = math.dot(
                        math.cross(nextPoint - currentPoint, candidatePoint - currentPoint),
                        up);
                    if (turn < -0.000001f ||
                        (math.abs(turn) <= 0.000001f &&
                         candidateDistance > math.lengthsq(nextPoint - currentPoint)))
                    {
                        next = candidate;
                        nextPoint = candidatePoint;
                    }
                }

                if (next < 0 || math.lengthsq(nextPoint - firstPoint) <= 0.00000001f)
                    break;
                current = next;
            }
            while (hullCount < 8);

            return new EarthSupportPolygon(
                point0,
                point1,
                point2,
                point3,
                point4,
                point5,
                point6,
                point7,
                hullCount);
        }

        private static int FindHullStart(
            float3 leftFoot,
            float3 rightFoot,
            bool leftPlanted,
            bool rightPlanted,
            float3 right,
            float3 forward,
            float halfLength,
            float halfWidth,
            int candidateCount)
        {
            int first = -1;
            float firstRight = float.PositiveInfinity;
            float firstForward = float.PositiveInfinity;
            for (int index = 0; index < candidateCount; index++)
            {
                float3 point = GetCandidate(
                    index,
                    leftFoot,
                    rightFoot,
                    leftPlanted,
                    rightPlanted,
                    right,
                    forward,
                    halfLength,
                    halfWidth);
                float rightCoordinate = math.dot(point, right);
                float forwardCoordinate = math.dot(point, forward);
                if (rightCoordinate < firstRight - 0.000001f ||
                    (math.abs(rightCoordinate - firstRight) <= 0.000001f &&
                     forwardCoordinate < firstForward))
                {
                    first = index;
                    firstRight = rightCoordinate;
                    firstForward = forwardCoordinate;
                }
            }
            return first;
        }

        private static float3 GetCandidate(
            int index,
            float3 leftFoot,
            float3 rightFoot,
            bool leftPlanted,
            bool rightPlanted,
            float3 right,
            float3 forward,
            float halfLength,
            float halfWidth)
        {
            bool useLeft = leftPlanted && (!rightPlanted || index < 4);
            int corner = useLeft ? index : index - (leftPlanted ? 4 : 0);
            float3 center = useLeft ? leftFoot : rightFoot;
            float rightSign = corner == 1 || corner == 2 ? 1f : -1f;
            float forwardSign = corner >= 2 ? 1f : -1f;
            return center + right * (rightSign * halfWidth) +
                   forward * (forwardSign * halfLength);
        }

        private static void SetHullPoint(
            int index,
            float3 value,
            ref float3 point0,
            ref float3 point1,
            ref float3 point2,
            ref float3 point3,
            ref float3 point4,
            ref float3 point5,
            ref float3 point6,
            ref float3 point7)
        {
            switch (index)
            {
                case 0: point0 = value; break;
                case 1: point1 = value; break;
                case 2: point2 = value; break;
                case 3: point3 = value; break;
                case 4: point4 = value; break;
                case 5: point5 = value; break;
                case 6: point6 = value; break;
                default: point7 = value; break;
            }
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
            float3 origin = polygon.Point0;
            for (int index = 0; index < polygon.Count; index++)
            {
                float3 a = polygon.GetPoint(index) - origin;
                float3 b = polygon.GetPoint((index + 1) % polygon.Count) - origin;
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
            bool duplicate,
            EarthPoweredAssistRejection rejection = EarthPoweredAssistRejection.None)
        {
            ResponseId = responseId;
            Owner = owner;
            Accepted = accepted;
            Duplicate = duplicate;
            Rejection = rejection;
        }

        public uint ResponseId { get; }
        public EarthPoweredImpactOwner Owner { get; }
        public bool Accepted { get; }
        public bool Duplicate { get; }
        public EarthPoweredAssistRejection Rejection { get; }
        public bool FallsBackToAgentA =>
            !Accepted && !Duplicate && Owner == EarthPoweredImpactOwner.AgentAInertialResponse;
        public bool EmitsImpulse => false;
        public bool RequestsRagdoll => false;
    }

    public static class EarthPoweredAssistEligibility
    {
        public static EarthPoweredAssistRejection Evaluate(
            CharacterPhysicalMode canonicalMode,
            bool stableSupport,
            bool feetConfigured,
            bool leftPlanted,
            bool rightPlanted,
            bool supportPolygonValid)
        {
            if (canonicalMode != CharacterPhysicalMode.AnimatedMotor &&
                canonicalMode != CharacterPhysicalMode.PhysicalAssist &&
                canonicalMode != CharacterPhysicalMode.Stagger)
                return EarthPoweredAssistRejection.CanonicalModeRejected;
            if (!stableSupport)
                return EarthPoweredAssistRejection.UnstableSupport;
            if (!feetConfigured)
                return EarthPoweredAssistRejection.MissingFeet;
            if (!leftPlanted && !rightPlanted)
                return EarthPoweredAssistRejection.NoPlantedFoot;
            if (!supportPolygonValid)
                return EarthPoweredAssistRejection.InvalidSupportPolygon;
            return EarthPoweredAssistRejection.None;
        }
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
            float3 direction,
            bool poweredAssistAccepted,
            EarthPoweredAssistRejection rejection)
        {
            if (responseId == 0u || response == EarthCharacterImpactResponse.Ignore)
                return default;
            if (ContainsResponse(responseId))
                return new EarthPoweredImpactDecision(responseId, EarthPoweredImpactOwner.None, false, true);

            EarthPoweredImpactOwner owner = response switch
            {
                EarthCharacterImpactResponse.Flinch => EarthPoweredImpactOwner.AgentAInertialResponse,
                EarthCharacterImpactResponse.Stagger => EarthPoweredImpactOwner.PoweredPhysicalAssist,
                EarthCharacterImpactResponse.RecoverableKnockdown or
                    EarthCharacterImpactResponse.Knockout => EarthPoweredImpactOwner.ExistingFullRagdoll,
                _ => EarthPoweredImpactOwner.None
            };
            if (owner == EarthPoweredImpactOwner.PoweredPhysicalAssist &&
                !poweredAssistAccepted)
            {
                EarthPoweredAssistRejection reason = rejection ==
                    EarthPoweredAssistRejection.None
                    ? EarthPoweredAssistRejection.ControllerRejected
                    : rejection;
                return new EarthPoweredImpactDecision(
                    responseId,
                    EarthPoweredImpactOwner.AgentAInertialResponse,
                    false,
                    false,
                    reason);
            }
            if (owner == EarthPoweredImpactOwner.None)
                return default;

            RememberResponse(responseId);
            AcceptedResponseCount++;
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

        public bool IsResponseKnown(uint responseId) =>
            responseId != 0u && ContainsResponse(responseId);

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
