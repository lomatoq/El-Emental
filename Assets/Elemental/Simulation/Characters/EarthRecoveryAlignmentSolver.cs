using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public readonly struct EarthRecoveryAlignmentInput
    {
        public EarthRecoveryAlignmentInput(
            float3 pelvisPosition,
            float3 chestPosition,
            float3 pelvisForward,
            float3 chestForward,
            float3 chestOutward,
            float3 chestRight,
            float3 localUp,
            float3 preferredForward,
            float3 candidatePelvisOffsetLocal)
        {
            PelvisPosition = pelvisPosition;
            ChestPosition = chestPosition;
            PelvisForward = pelvisForward;
            ChestForward = chestForward;
            ChestOutward = chestOutward;
            ChestRight = chestRight;
            LocalUp = localUp;
            PreferredForward = preferredForward;
            CandidatePelvisOffsetLocal = candidatePelvisOffsetLocal;
        }

        public float3 PelvisPosition { get; }
        public float3 ChestPosition { get; }
        public float3 PelvisForward { get; }
        public float3 ChestForward { get; }
        public float3 ChestOutward { get; }
        public float3 ChestRight { get; }
        public float3 LocalUp { get; }
        public float3 PreferredForward { get; }
        public float3 CandidatePelvisOffsetLocal { get; }
    }

    public readonly struct EarthRecoveryAlignmentResult
    {
        public EarthRecoveryAlignmentResult(
            float3 livePelvisPosition,
            float3 rootPosition,
            quaternion rootRotation,
            float3 radialUp,
            float3 radialFacing,
            EarthRecoveryOrientation orientation,
            bool usedFacingFallback)
        {
            LivePelvisPosition = livePelvisPosition;
            RootPosition = rootPosition;
            RootRotation = rootRotation;
            RadialUp = radialUp;
            RadialFacing = radialFacing;
            Orientation = orientation;
            UsedFacingFallback = usedFacingFallback;
        }

        public float3 LivePelvisPosition { get; }
        public float3 RootPosition { get; }
        public quaternion RootRotation { get; }
        public float3 RadialUp { get; }
        public float3 RadialFacing { get; }
        public EarthRecoveryOrientation Orientation { get; }
        public bool UsedFacingFallback { get; }
    }

    public static class EarthRecoveryAlignmentSolver
    {
        public const float FirstClearanceLiftMeters = 0.18f;
        public const float MaximumClearanceLiftMeters = 0.35f;
        public const float FaceClassificationThreshold = 0.08f;

        public static EarthRecoveryOrientation Classify(
            float3 chestOutward,
            float3 chestRight,
            float3 localUp)
        {
            float3 up = math.normalizesafe(SelectFinite(localUp), new float3(0f, 1f, 0f));
            float3 safeOutward = SelectFinite(chestOutward);
            if (math.lengthsq(safeOutward) >= 0.0001f)
            {
                float outwardDot = math.dot(math.normalize(safeOutward), up);
                if (outwardDot > FaceClassificationThreshold)
                    return EarthRecoveryOrientation.Back;
                if (outwardDot < -FaceClassificationThreshold)
                    return EarthRecoveryOrientation.Front;
            }

            float3 safeRight = math.normalizesafe(
                SelectFinite(chestRight),
                OrthogonalForward(up));
            float sideDot = math.dot(safeRight, up);
            return sideDot >= 0f
                ? EarthRecoveryOrientation.Right
                : EarthRecoveryOrientation.Left;
        }

        public static EarthRecoveryClearanceResult SelectClearance(
            bool basePoseClear,
            bool firstLiftClear,
            bool maximumLiftClear)
        {
            if (basePoseClear)
                return new EarthRecoveryClearanceResult(
                    EarthRecoveryClearanceKind.BasePose,
                    0f,
                    true);
            if (firstLiftClear)
                return new EarthRecoveryClearanceResult(
                    EarthRecoveryClearanceKind.FirstLift,
                    FirstClearanceLiftMeters,
                    true);
            if (maximumLiftClear)
                return new EarthRecoveryClearanceResult(
                    EarthRecoveryClearanceKind.MaximumLift,
                    MaximumClearanceLiftMeters,
                    true);
            return new EarthRecoveryClearanceResult(
                EarthRecoveryClearanceKind.BlockedAtMaximumLift,
                MaximumClearanceLiftMeters,
                false);
        }

        public static EarthRecoveryAlignmentResult Solve(
            in EarthRecoveryAlignmentInput input,
            in EarthRecoveryClearanceResult clearance)
        {
            float3 up = math.normalizesafe(SelectFinite(input.LocalUp), new float3(0f, 1f, 0f));
            float3 preferred = ProjectDirection(input.PreferredForward, up);
            bool usedFallback = math.lengthsq(preferred) < 0.0001f;
            if (usedFallback) preferred = OrthogonalForward(up);
            preferred = math.normalizesafe(preferred, OrthogonalForward(up));

            float3 actualFacing = ProjectDirection(input.PelvisForward + input.ChestForward, up);
            if (math.lengthsq(actualFacing) < 0.0001f)
                actualFacing = ProjectDirection(input.ChestPosition - input.PelvisPosition, up);
            if (math.lengthsq(actualFacing) < 0.0001f)
            {
                actualFacing = preferred;
                usedFallback = true;
            }
            actualFacing = math.normalizesafe(actualFacing, preferred);
            if (math.dot(actualFacing, preferred) < 0f)
                actualFacing = -actualFacing;

            quaternion rotation = quaternion.LookRotationSafe(actualFacing, up);
            float lift = math.clamp(
                math.isfinite(clearance.LiftMeters) ? clearance.LiftMeters : 0f,
                0f,
                MaximumClearanceLiftMeters);
            float3 pelvis = SelectFinite(input.PelvisPosition);
            float3 pelvisOffset = SelectFinite(input.CandidatePelvisOffsetLocal);
            float3 root = pelvis - math.rotate(rotation, pelvisOffset) + up * lift;
            if (!math.all(math.isfinite(root)))
            {
                root = pelvis + up * lift;
                usedFallback = true;
            }

            return new EarthRecoveryAlignmentResult(
                pelvis,
                root,
                rotation,
                up,
                actualFacing,
                Classify(input.ChestOutward, input.ChestRight, up),
                usedFallback);
        }

        public static EarthRecoveryResult ComposeResult(
            in EarthRecoveryPoseMatch match,
            in EarthRecoveryAlignmentResult alignment,
            in EarthRecoveryClearanceResult clearance)
        {
            EarthRecoveryPoseCandidate candidate = match.Candidate;
            EarthRecoveryMarkerProfile markers = candidate.Markers;
            return new EarthRecoveryResult(
                alignment.Orientation,
                candidate.ClipId,
                candidate.AnimationStateId,
                candidate.EntryPhase,
                match.Cost,
                alignment.LivePelvisPosition,
                alignment.RootPosition,
                alignment.RootRotation,
                alignment.RadialUp,
                alignment.RadialFacing,
                in clearance,
                in markers,
                alignment.UsedFacingFallback);
        }

        private static float3 ProjectDirection(float3 direction, float3 up)
        {
            float3 finite = SelectFinite(direction);
            return finite - up * math.dot(finite, up);
        }

        private static float3 OrthogonalForward(float3 up)
        {
            float3 axis = math.abs(up.y) < 0.92f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            return math.normalizesafe(math.cross(axis, up), new float3(0f, 0f, 1f));
        }

        private static float3 SelectFinite(float3 value) =>
            math.select(float3.zero, value, math.isfinite(value));
    }
}
