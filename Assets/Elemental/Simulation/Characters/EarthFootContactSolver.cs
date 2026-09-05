using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthFootContactReason : byte
    {
        InvalidSurface = 0,
        Swing = 1,
        Capture = 2,
        Stance = 3,
        SupportSwap = 4,
        Jump = 5,
        Impact = 6,
        Brace = 7
    }

    public enum EarthFootPlantState : byte
    {
        Free = 0,
        Candidate = 1,
        Planting = 2,
        Planted = 3,
        Releasing = 4,
        AirborneReset = 5
    }

    public readonly struct EarthFootContactInput
    {
        public EarthFootContactInput(
            bool isLeft,
            bool supported,
            bool locomoting,
            bool pivotingInPlace,
            bool poseLock,
            bool hasContact,
            float soleClearance,
            float verticalVelocity,
            float capturePriority,
            float gaitPhase01,
            float3 contactTargetLocal,
            float3 contactNormalLocal,
            float3 fallbackTargetLocal,
            float3 localUpLocal,
            uint supportId,
            uint supportGeneration,
            float deltaTime,
            float authoredContact01 = float.NaN)
        {
            IsLeft = isLeft;
            Supported = supported;
            Locomoting = locomoting;
            PivotingInPlace = pivotingInPlace;
            PoseLock = poseLock;
            HasContact = hasContact;
            SoleClearance = math.isfinite(soleClearance)
                ? soleClearance
                : float.PositiveInfinity;
            VerticalVelocity = math.isfinite(verticalVelocity) ? verticalVelocity : 0f;
            CapturePriority = math.isfinite(capturePriority) ? capturePriority : 0f;
            GaitPhase01 = math.frac(math.max(0f, math.isfinite(gaitPhase01) ? gaitPhase01 : 0f));
            ContactTargetLocal = SelectFinite(contactTargetLocal, fallbackTargetLocal);
            ContactNormalLocal = math.normalizesafe(contactNormalLocal, localUpLocal);
            FallbackTargetLocal = SelectFinite(fallbackTargetLocal, float3.zero);
            LocalUpLocal = math.normalizesafe(localUpLocal, new float3(0f, 1f, 0f));
            SupportId = supportId;
            SupportGeneration = supportGeneration;
            DeltaTime = math.clamp(math.isfinite(deltaTime) ? deltaTime : 0f, 0.0001f, 0.1f);
            HasAuthoredContact = math.isfinite(authoredContact01);
            AuthoredContact01 = HasAuthoredContact ? math.saturate(authoredContact01) : 0f;
        }

        public bool IsLeft { get; }
        public bool Supported { get; }
        public bool Locomoting { get; }
        public bool PivotingInPlace { get; }
        public bool PoseLock { get; }
        public bool HasContact { get; }
        public float SoleClearance { get; }
        public float VerticalVelocity { get; }
        public float CapturePriority { get; }
        public float GaitPhase01 { get; }
        public float3 ContactTargetLocal { get; }
        public float3 ContactNormalLocal { get; }
        public float3 FallbackTargetLocal { get; }
        public float3 LocalUpLocal { get; }
        public uint SupportId { get; }
        public uint SupportGeneration { get; }
        public float DeltaTime { get; }
        public bool HasAuthoredContact { get; }
        public float AuthoredContact01 { get; }

        private static float3 SelectFinite(float3 value, float3 fallback) =>
            math.select(fallback, value, math.isfinite(value));
    }

    public struct EarthFootContactState
    {
        public EarthFootPlantState PlantState;
        public bool Locked;
        public bool Armed;
        public bool PoseOwned;
        public bool HasPreviousClearance;
        public float PreviousClearance;
        public float ReleaseCooldownSeconds;
        public uint SupportId;
        public uint SupportGeneration;
        public float3 AnchorLocal;
        public bool HasFilteredTarget;
        public uint FilterSupportId;
        public uint FilterSupportGeneration;
        public float3 FilteredTargetLocal;
        public float3 FilterVelocityLocal;
        public float3 FilteredNormalLocal;
        public float3 FilterReferenceLocal;
        public bool FilterWasLocked;
        public bool FilterWasContactFollowing;
    }

    public readonly struct EarthFootContactDecision
    {
        internal EarthFootContactDecision(
            in EarthFootContactState state,
            EarthFootContactReason reason,
            float targetWeight,
            float3 targetLocal,
            float3 normalLocal,
            bool captured,
            bool maintained)
        {
            State = state;
            Reason = reason;
            TargetWeight = math.saturate(math.isfinite(targetWeight) ? targetWeight : 0f);
            TargetLocal = targetLocal;
            NormalLocal = math.normalizesafe(normalLocal, new float3(0f, 1f, 0f));
            Captured = captured;
            Maintained = maintained;
        }

        public EarthFootContactState State { get; }
        public EarthFootContactReason Reason { get; }
        public float TargetWeight { get; }
        public float3 TargetLocal { get; }
        public float3 NormalLocal { get; }
        public bool Locked => State.Locked;
        public EarthFootPlantState PlantState => State.PlantState;
        public bool Captured { get; }
        public bool Maintained { get; }
        public float ReleaseCooldownSeconds => State.ReleaseCooldownSeconds;
    }

    public readonly struct EarthFootContactPairDecision
    {
        public EarthFootContactPairDecision(
            in EarthFootContactDecision left,
            in EarthFootContactDecision right)
        {
            Left = left;
            Right = right;
        }

        public EarthFootContactDecision Left { get; }
        public EarthFootContactDecision Right { get; }
        public bool BothLocked => Left.Locked && Right.Locked;
    }

    /// <summary>
    /// Pure, pair-wise contact resolver. Both animated feet are sampled before
    /// arbitration, so gait ownership never depends on left/right update order.
    /// Targets are filtered in support-local space and therefore remain stable on
    /// translating or rotating supports.
    /// </summary>
    public static class EarthFootContactSolver
    {
        public const float ReleaseHysteresisSeconds = 0.12f;
        // A fresh pose transition can place the rendered sole slightly inside
        // the resolved support before IK gets ownership. Accept a shallow
        // penetration so the solver can lift the foot back onto the surface;
        // deeper intersections still stay authored instead of snapping.
        private const float MinimumCaptureClearance = -0.065f;
        private const float MaximumCaptureClearance = 0.055f;
        private const float ReleaseClearance = 0.135f;
        // A stopped Humanoid can still present one foot above the support while
        // its locomotion pose yields to idle. The contact controller can safely
        // recover 0.22 m through its bounded pelvis drop and roughly another
        // 0.10 m through the leg chain. Beyond this combined reach, keep the
        // authored swing pose so a high foot cannot straighten the other knee.
        public const float MaximumStationaryCaptureClearance = 0.32f;
        private const float RearmClearance = 0.11f;
        private const float DescendingTolerance = 0.002f;
        private const float MaximumLockReach = 0.24f;
        private const float MaximumTargetStepAt60Hz = 0.025f;
        private const float MaximumNormalStepDegreesAt60Hz = 8f;
        private const float TargetResponseSeconds = 0.055f;

        public static EarthFootContactPairDecision ResolvePair(
            ref EarthFootContactState leftState,
            ref EarthFootContactState rightState,
            in EarthFootContactInput leftInput,
            in EarthFootContactInput rightInput)
        {
            PreparedFoot left = Prepare(in leftState, in leftInput);
            PreparedFoot right = Prepare(in rightState, in rightInput);

            if (leftInput.Locomoting || rightInput.Locomoting)
            {
                bool leftWants = left.WantsLock;
                bool rightWants = right.WantsLock;
                if (leftWants && rightWants)
                {
                    bool chooseLeft;
                    if (left.Maintained != right.Maintained)
                        chooseLeft = left.Maintained;
                    else if (math.abs(leftInput.CapturePriority - rightInput.CapturePriority) > 0.0001f)
                        chooseLeft = leftInput.CapturePriority > rightInput.CapturePriority;
                    else
                        // The phase half-cycle is a deterministic final
                        // tiebreaker. Avoid testing cos() against zero at the
                        // quarter-cycle boundaries: floating-point sign noise
                        // there reintroduced left-first capture on some CPUs.
                        chooseLeft = leftInput.GaitPhase01 < 0.5f;

                    if (chooseLeft) DenyLocomotionLock(ref right);
                    else DenyLocomotionLock(ref left);
                }
            }

            EarthFootContactDecision leftDecision = Finalize(ref left, in leftInput);
            EarthFootContactDecision rightDecision = Finalize(ref right, in rightInput);
            leftState = leftDecision.State;
            rightState = rightDecision.State;
            return new EarthFootContactPairDecision(in leftDecision, in rightDecision);
        }

        private static PreparedFoot Prepare(
            in EarthFootContactState previous,
            in EarthFootContactInput input)
        {
            EarthFootContactState state = previous;
            state.ReleaseCooldownSeconds = math.max(
                0f,
                state.ReleaseCooldownSeconds - input.DeltaTime);
            bool valid = input.Supported && input.HasContact &&
                         math.isfinite(input.SoleClearance) &&
                         math.all(math.isfinite(input.ContactTargetLocal));
            bool sameSupport = previous.Locked &&
                               previous.SupportId == input.SupportId &&
                               previous.SupportGeneration == input.SupportGeneration;
            var prepared = new PreparedFoot
            {
                State = state,
                Valid = valid,
                SameSupport = sameSupport,
                RawTargetLocal = valid ? input.ContactTargetLocal : input.FallbackTargetLocal,
                RawNormalLocal = valid ? input.ContactNormalLocal : input.LocalUpLocal,
                Reason = valid ? EarthFootContactReason.Swing : EarthFootContactReason.InvalidSurface,
                TargetWeight = valid ? ContactWeight(input.Locomoting, input.SoleClearance) : 0f
            };

            if (!valid)
            {
                if (previous.Locked)
                    Release(ref prepared.State, EarthFootContactReason.SupportSwap, ref prepared);
                else
                {
                    prepared.State.PlantState = input.Supported
                        ? EarthFootPlantState.Free
                        : EarthFootPlantState.AirborneReset;
                    prepared.State.Locked = false;
                    prepared.State.PoseOwned = false;
                    prepared.State.Armed = false;
                }
                prepared.Reason = input.Supported
                    ? EarthFootContactReason.InvalidSurface
                    : EarthFootContactReason.Jump;
                prepared.TargetWeight = 0f;
                return prepared;
            }

            if (input.PoseLock)
            {
                prepared.WantsLock = true;
                prepared.Maintained = sameSupport && previous.PoseOwned;
                prepared.Captured = !prepared.Maintained;
                prepared.Reason = EarthFootContactReason.Brace;
                return prepared;
            }

            if (previous.PoseOwned)
            {
                Release(ref prepared.State, EarthFootContactReason.Swing, ref prepared);
                prepared.State.HasPreviousClearance = true;
                prepared.State.PreviousClearance = input.SoleClearance;
                return prepared;
            }

            if (!input.Locomoting)
            {
                prepared.State.Locked = false;
                prepared.State.PlantState = EarthFootPlantState.Free;
                prepared.State.PoseOwned = false;
                prepared.State.HasPreviousClearance = true;
                prepared.State.PreviousClearance = input.SoleClearance;
                bool withinStationaryReach =
                    input.SoleClearance >= MinimumCaptureClearance &&
                    input.SoleClearance <= MaximumStationaryCaptureClearance;
                prepared.State.Armed = withinStationaryReach;
                if (!withinStationaryReach)
                {
                    // A walk -> idle query may still be presenting a raised
                    // authored swing foot. Do not turn a ray hit far below that
                    // pose into immediate full-body IK: wait until the coherent
                    // idle leg chain brings the sole inside reachable capture.
                    prepared.TargetWeight = 0f;
                    prepared.Reason = EarthFootContactReason.Swing;
                    return prepared;
                }
                // Stationary feet still need to follow the resolved support.
                // Leaving both IK weights at zero exposed the imported clip's
                // floor offset and made the whole character appear to hover.
                // This is contact following, not a world-space lock, so both
                // feet may settle without violating locomotion arbitration.
                prepared.TargetWeight = 1f;
                prepared.Reason = EarthFootContactReason.Stance;
                return prepared;
            }

            float stancePhase = (input.IsLeft ? 1f : -1f) *
                                math.cos(input.GaitPhase01 * math.PI * 2f);
            bool phaseAllowsStance = input.PivotingInPlace ||
                                     (input.HasAuthoredContact
                                         ? input.AuthoredContact01 >= 0.22f
                                         : stancePhase >= -0.15f);
            float maximumLockReach = input.PivotingInPlace
                // A 180-degree authored pivot moves the uncorrected Humanoid
                // foot through a wide local arc even though the support anchor
                // itself is valid. Releasing at the ordinary walk reach caused
                // a 20 cm / 180 degree mid-turn foot swap in the live audit.
                ? 0.80f
                : MaximumLockReach;
            bool anchorReachValid = math.distance(
                input.FallbackTargetLocal,
                previous.AnchorLocal) <= maximumLockReach;
            // During an in-place pivot the authored swing arc is not evidence
            // that the support anchor vanished. Once one foot owns the same
            // support, retain it through the turn; releasing because the source
            // clip lifts that foot created a visible 120 ms no-contact gap and
            // then swapped the whole body to the other leg. Ordinary locomotion
            // still releases by clearance/phase/reach exactly as before.
            bool clearanceAllowsMaintenance = input.PivotingInPlace ||
                                              input.SoleClearance <= ReleaseClearance;
            bool reachAllowsMaintenance = input.PivotingInPlace || anchorReachValid;
            if (previous.Locked && sameSupport && phaseAllowsStance &&
                reachAllowsMaintenance && clearanceAllowsMaintenance)
            {
                prepared.WantsLock = true;
                prepared.Maintained = true;
                prepared.Reason = EarthFootContactReason.Stance;
                return prepared;
            }

            if (previous.Locked)
            {
                Release(ref prepared.State, sameSupport
                    ? EarthFootContactReason.Swing
                    : EarthFootContactReason.SupportSwap, ref prepared);
                prepared.State.HasPreviousClearance = true;
                prepared.State.PreviousClearance = input.SoleClearance;
                return prepared;
            }

            bool armed = input.PivotingInPlace || previous.Armed || !previous.HasPreviousClearance ||
                         input.SoleClearance >= RearmClearance;
            bool descending = !previous.HasPreviousClearance ||
                              input.VerticalVelocity <= 0.12f &&
                              input.SoleClearance <= previous.PreviousClearance + DescendingTolerance;
            float maximumCaptureClearance = input.PivotingInPlace
                ? 0.14f
                : MaximumCaptureClearance;
            bool capture = state.ReleaseCooldownSeconds <= 0f && armed && descending &&
                           phaseAllowsStance &&
                           (!input.HasAuthoredContact || input.AuthoredContact01 >= 0.55f) &&
                           input.SoleClearance >= MinimumCaptureClearance &&
                           input.SoleClearance <= maximumCaptureClearance;
            prepared.State.Armed = armed;
            prepared.State.HasPreviousClearance = true;
            prepared.State.PreviousClearance = input.SoleClearance;
            prepared.WantsLock = capture;
            prepared.Captured = capture;
            prepared.Reason = capture
                ? EarthFootContactReason.Capture
                : EarthFootContactReason.Swing;
            return prepared;
        }

        private static EarthFootContactDecision Finalize(
            ref PreparedFoot prepared,
            in EarthFootContactInput input)
        {
            EarthFootContactState state = prepared.State;
            if (prepared.WantsLock)
            {
                if (!prepared.Maintained)
                {
                    state.AnchorLocal = input.PivotingInPlace
                        ? CaptureRenderedPivotAnchor(
                            input.FallbackTargetLocal,
                            prepared.RawTargetLocal,
                            prepared.RawNormalLocal)
                        : prepared.RawTargetLocal;
                    state.SupportId = input.SupportId;
                    state.SupportGeneration = input.SupportGeneration;
                    if (input.PivotingInPlace)
                    {
                        // The unlocked filter follows raycast targets. Reusing
                        // that history on first pivot capture moves the visible
                        // planted foot before the IK weight has settled. Seed the
                        // new lock from the rendered support-local pose instead.
                        state.HasFilteredTarget = false;
                        state.FilterVelocityLocal = float3.zero;
                    }
                }
                state.Locked = true;
                state.PlantState = prepared.Maintained
                    ? EarthFootPlantState.Planted
                    : EarthFootPlantState.Planting;
                state.PoseOwned = input.PoseLock;
                state.Armed = false;
                state.ReleaseCooldownSeconds = 0f;
                prepared.RawTargetLocal = state.AnchorLocal;
                prepared.TargetWeight = 1f;
            }
            else
            {
                state.Locked = false;
                state.PoseOwned = false;
                state.SupportId = 0u;
                state.SupportGeneration = 0u;
                state.PlantState = !input.Supported
                    ? EarthFootPlantState.AirborneReset
                    : state.ReleaseCooldownSeconds > 0f
                        ? EarthFootPlantState.Releasing
                        : prepared.Valid && input.Locomoting
                            ? EarthFootPlantState.Candidate
                            : EarthFootPlantState.Free;
                if (input.Locomoting)
                    // Swing is authored animation. A residual procedural weight
                    // still pulls the whole humanoid chain toward the released
                    // world anchor and was the visible multi-metre leg snap.
                    prepared.TargetWeight = 0f;
            }

            FilterTarget(
                ref state,
                prepared.RawTargetLocal,
                prepared.RawNormalLocal,
                input.FallbackTargetLocal,
                prepared.Valid,
                input.SupportId,
                input.SupportGeneration,
                prepared.WantsLock || (!input.Locomoting && prepared.Valid),
                input.DeltaTime);
            return new EarthFootContactDecision(
                in state,
                prepared.Reason,
                prepared.TargetWeight,
                state.FilteredTargetLocal,
                state.FilteredNormalLocal,
                prepared.Captured && prepared.WantsLock,
                prepared.Maintained && prepared.WantsLock);
        }

        public static float3 CaptureRenderedPivotAnchor(
            float3 renderedFootLocal,
            float3 contactTargetLocal,
            float3 contactNormalLocal)
        {
            float3 normal = math.normalizesafe(
                contactNormalLocal,
                new float3(0f, 1f, 0f));
            if (!math.all(math.isfinite(renderedFootLocal)))
                return contactTargetLocal;
            // Keep the tangential position already rendered this frame and only
            // project it onto the probed contact plane. This gives the pivot a
            // grounded anchor without a horizontal capture snap.
            return renderedFootLocal + normal * math.dot(
                contactTargetLocal - renderedFootLocal,
                normal);
        }

        private static void DenyLocomotionLock(ref PreparedFoot prepared)
        {
            bool releasedExistingLock = prepared.State.Locked;
            prepared.WantsLock = false;
            prepared.Captured = false;
            prepared.Maintained = false;
            prepared.State.Locked = false;
            prepared.State.PlantState = releasedExistingLock
                ? EarthFootPlantState.Releasing
                : EarthFootPlantState.Candidate;
            prepared.State.PoseOwned = false;
            // Losing simultaneous *capture* arbitration is not a release and
            // must not continuously re-arm the 120 ms cooldown; doing so can
            // starve the same leg forever. Only an already-established lock
            // receives release hysteresis.
            if (releasedExistingLock)
            {
                prepared.State.Armed = false;
                prepared.State.ReleaseCooldownSeconds = math.max(
                    prepared.State.ReleaseCooldownSeconds,
                    ReleaseHysteresisSeconds);
            }
            prepared.Reason = EarthFootContactReason.Swing;
        }

        private static void Release(
            ref EarthFootContactState state,
            EarthFootContactReason reason,
            ref PreparedFoot prepared)
        {
            state.Locked = false;
            state.PlantState = EarthFootPlantState.Releasing;
            state.PoseOwned = false;
            state.Armed = false;
            state.SupportId = 0u;
            state.SupportGeneration = 0u;
            state.ReleaseCooldownSeconds = math.max(
                state.ReleaseCooldownSeconds,
                ReleaseHysteresisSeconds);
            prepared.Reason = reason;
            prepared.WantsLock = false;
            prepared.Captured = false;
            prepared.Maintained = false;
        }

        private static float ContactWeight(bool locomoting, float clearance)
        {
            if (!math.isfinite(clearance)) return 0f;
            if (!locomoting) return 0.78f;
            if (clearance <= 0.03f) return 0.82f;
            if (clearance <= 0.065f)
                return math.lerp(0.82f, 0.15f, math.saturate((clearance - 0.03f) / 0.035f));
            if (clearance >= 0.14f) return 0f;
            return math.lerp(0.15f, 0f, math.saturate((clearance - 0.065f) / 0.075f));
        }

        private static void FilterTarget(
            ref EarthFootContactState state,
            float3 rawTargetLocal,
            float3 rawNormalLocal,
            float3 animatedReferenceLocal,
            bool validContact,
            uint supportId,
            uint supportGeneration,
            bool contactFollowing,
            float deltaTime)
        {
            bool sameFilterSupport = state.HasFilteredTarget &&
                                     state.FilterSupportId == supportId &&
                                     state.FilterSupportGeneration == supportGeneration &&
                                     state.FilterWasLocked == state.Locked &&
                                     state.FilterWasContactFollowing == contactFollowing &&
                                     validContact;
            if (sameFilterSupport && !state.Locked && !contactFollowing)
            {
                // A free foot follows authored/root motion without a speed cap.
                // Filter only the surface correction relative to that motion.
                // Filtering the absolute support-local position at 1.5 m/s built
                // a multi-metre backlog during a 6 m/s run; idle then applied it
                // at full IK weight and stretched both legs toward the old target.
                // Stationary contact following is procedural ownership, not a
                // free foot. Feeding its already-solved motion back here makes
                // the target chase the IK result away from the current surface.
                state.FilteredTargetLocal += animatedReferenceLocal - state.FilterReferenceLocal;
            }
            state.FilterReferenceLocal = animatedReferenceLocal;
            state.FilterWasLocked = state.Locked;
            state.FilterWasContactFollowing = contactFollowing;
            if (!sameFilterSupport)
            {
                state.HasFilteredTarget = true;
                state.FilterSupportId = supportId;
                state.FilterSupportGeneration = supportGeneration;
                state.FilteredTargetLocal = rawTargetLocal;
                state.FilterVelocityLocal = float3.zero;
                state.FilteredNormalLocal = math.normalizesafe(
                    rawNormalLocal,
                    new float3(0f, 1f, 0f));
                return;
            }

            float3 filtered;
            if (contactFollowing && !state.Locked)
            {
                // This is a resolved surface follower, not an anchored foot.
                // Smoothing xyz as one vector is geometrically invalid on a
                // curved support: an old tangent coordinate paired with the new
                // sampled height can put the goal above or below the surface.
                // Keep position on the current ray target; ankle/normal rotation
                // remains inertialized independently below and in ApplyFoot.
                filtered = rawTargetLocal;
                state.FilterVelocityLocal = float3.zero;
            }
            else
            {
                float3 previous = state.FilteredTargetLocal;
                filtered = SmoothDamp(
                    previous,
                    rawTargetLocal,
                    ref state.FilterVelocityLocal,
                    TargetResponseSeconds,
                    deltaTime);
                float maximumStep = MaximumTargetStepAt60Hz * deltaTime * 60f;
                float3 delta = filtered - previous;
                float distance = math.length(delta);
                if (distance > maximumStep && distance > 0.000001f)
                {
                    filtered = previous + delta * (maximumStep / distance);
                    state.FilterVelocityLocal = (filtered - previous) /
                                                math.max(0.0001f, deltaTime);
                }
            }
            state.FilteredTargetLocal = filtered;

            float3 from = math.normalizesafe(state.FilteredNormalLocal, rawNormalLocal);
            float3 to = math.normalizesafe(rawNormalLocal, from);
            float angle = math.acos(math.clamp(math.dot(from, to), -1f, 1f));
            float maximumAngle = math.radians(MaximumNormalStepDegreesAt60Hz) * deltaTime * 60f;
            float t = angle > 0.0001f ? math.min(1f, maximumAngle / angle) : 1f;
            float responseT = 1f - math.exp(-deltaTime / TargetResponseSeconds);
            state.FilteredNormalLocal = math.normalizesafe(
                math.lerp(from, to, math.min(t, responseT)),
                to);
        }

        private static float3 SmoothDamp(
            float3 current,
            float3 target,
            ref float3 velocity,
            float smoothTime,
            float deltaTime)
        {
            float omega = 2f / math.max(0.0001f, smoothTime);
            float x = omega * math.max(0.0001f, deltaTime);
            float decay = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float3 change = current - target;
            float3 temporary = (velocity + omega * change) * deltaTime;
            velocity = (velocity - omega * temporary) * decay;
            return target + (change + temporary) * decay;
        }

        private struct PreparedFoot
        {
            public EarthFootContactState State;
            public bool Valid;
            public bool SameSupport;
            public bool WantsLock;
            public bool Captured;
            public bool Maintained;
            public EarthFootContactReason Reason;
            public float TargetWeight;
            public float3 RawTargetLocal;
            public float3 RawNormalLocal;
        }
    }
}
