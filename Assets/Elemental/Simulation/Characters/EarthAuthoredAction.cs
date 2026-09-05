using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthAuthoredActionId : byte
    {
        None = 0,
        Locomotion = 1,
        Jump = 2,
        Fall = 3,
        SoftLanding = 4,
        MovingLandingRoll = 5,
        HardLandingBrace = 6,
        RecoverableKnockdownRecovery = 7,
        HitRecoil = 8,
        MagicCast = 9,
        SurfCrouch = 10,
        DirectionalDodge = 11,
        Mantle = 12
    }

    public enum EarthAuthoredFootPolicy : byte
    {
        DefaultContact = 0,
        FlightIkOff = 1,
        AuthoredContact = 2,
        BraceBoth = 3
    }

    public readonly struct EarthAuthoredActionDefinition
    {
        public EarthAuthoredActionDefinition(
            EarthAuthoredActionId id,
            float minimumClearanceMeters,
            float flightEnd01,
            float contactStart01,
            float contactEnd01,
            float recoveryEnd01,
            EarthAuthoredFootPolicy contactPolicy)
        {
            Id = id;
            MinimumClearanceMeters = math.max(0f, minimumClearanceMeters);
            FlightEnd01 = math.saturate(flightEnd01);
            ContactStart01 = math.clamp(contactStart01, FlightEnd01, 1f);
            ContactEnd01 = math.clamp(contactEnd01, ContactStart01, 1f);
            RecoveryEnd01 = math.clamp(recoveryEnd01, ContactEnd01, 1f);
            ContactPolicy = contactPolicy;
        }

        public EarthAuthoredActionId Id { get; }
        public float MinimumClearanceMeters { get; }
        public float FlightEnd01 { get; }
        public float ContactStart01 { get; }
        public float ContactEnd01 { get; }
        public float RecoveryEnd01 { get; }
        public EarthAuthoredFootPolicy ContactPolicy { get; }

        public EarthAuthoredFootPolicy FootPolicyAt(float normalizedTime)
        {
            float time = math.saturate(normalizedTime);
            if (time < FlightEnd01) return EarthAuthoredFootPolicy.FlightIkOff;
            if (time >= ContactStart01 && time <= ContactEnd01) return ContactPolicy;
            if (time < RecoveryEnd01 &&
                (Id == EarthAuthoredActionId.MovingLandingRoll ||
                 Id == EarthAuthoredActionId.RecoverableKnockdownRecovery))
                return EarthAuthoredFootPolicy.AuthoredContact;
            return EarthAuthoredFootPolicy.DefaultContact;
        }
    }

    /// <summary>
    /// Stable action windows for the authored clips that are actually present in
    /// the project. Falling-To-Roll and Hard Landing come from Mixamo; the four
    /// short directional dodges come from the licensed KayKit Humanoid library.
    /// There is deliberately no procedural-only flip entry without an authored clip.
    /// </summary>
    public static class EarthAuthoredActionCatalog
    {
        public const float LandingRollExitPhase = 0.92f;
        public const float LandingRollExitBlendSeconds = 0.18f;
        public static EarthAuthoredActionDefinition Resolve(EarthAuthoredActionId id) => id switch
        {
            EarthAuthoredActionId.Mantle =>
                new EarthAuthoredActionDefinition(id, 0f, 0.82f, 0.82f, 1f, 1f,
                    EarthAuthoredFootPolicy.DefaultContact),
            EarthAuthoredActionId.Jump =>
                new EarthAuthoredActionDefinition(id, 0.18f, 1f, 1f, 1f, 1f,
                    EarthAuthoredFootPolicy.FlightIkOff),
            EarthAuthoredActionId.Fall =>
                new EarthAuthoredActionDefinition(id, 0.12f, 1f, 1f, 1f, 1f,
                    EarthAuthoredFootPolicy.FlightIkOff),
            EarthAuthoredActionId.MovingLandingRoll =>
                new EarthAuthoredActionDefinition(id, 0.32f, 0.20f, 0.20f, 0.62f, LandingRollExitPhase,
                    EarthAuthoredFootPolicy.AuthoredContact),
            EarthAuthoredActionId.HardLandingBrace =>
                new EarthAuthoredActionDefinition(id, 0.22f, 0.20f, 0.20f, 0.62f, 0.94f,
                    EarthAuthoredFootPolicy.BraceBoth),
            EarthAuthoredActionId.SoftLanding =>
                new EarthAuthoredActionDefinition(id, 0.12f, 0.20f, 0.20f, 0.58f, 0.90f,
                    EarthAuthoredFootPolicy.AuthoredContact),
            EarthAuthoredActionId.RecoverableKnockdownRecovery =>
                new EarthAuthoredActionDefinition(id, 0.30f, 0.18f, 0.18f, 0.70f, 0.94f,
                    EarthAuthoredFootPolicy.AuthoredContact),
            // KayKit's four authored dodges are short grounded evasions. Their
            // first frames lift/unweight the feet, the middle owns authored
            // contact, then the pair solver resumes after the body settles.
            EarthAuthoredActionId.DirectionalDodge =>
                new EarthAuthoredActionDefinition(id, 0.10f, 0.22f, 0.22f, 0.68f, 0.90f,
                    EarthAuthoredFootPolicy.AuthoredContact),
            _ => new EarthAuthoredActionDefinition(id, 0f, 0f, 0f, 0f, 1f,
                EarthAuthoredFootPolicy.DefaultContact)
        };

        public static bool CanInterrupt(
            EarthAuthoredActionId current,
            float normalizedTime,
            EarthAuthoredActionId requested)
        {
            float time = math.saturate(normalizedTime);
            if (current == EarthAuthoredActionId.RecoverableKnockdownRecovery)
                return false;
            if (current != EarthAuthoredActionId.DirectionalDodge) return true;
            if (requested == EarthAuthoredActionId.RecoverableKnockdownRecovery) return true;
            if (requested == EarthAuthoredActionId.HitRecoil) return time >= 0.28f;
            return time >= 0.78f;
        }
    }

    public enum EarthDirectionalDodgeDirection : byte
    {
        Forward = 0,
        Backward = 1,
        Left = 2,
        Right = 3
    }

    public enum EarthDirectionalDodgeRejectReason : byte
    {
        None = 0,
        NoDirection = 1,
        Airborne = 2,
        Surfing = 3,
        Casting = 4,
        PhysicalRecovery = 5,
        NonInterruptibleAction = 6
    }

    public readonly struct EarthDirectionalDodgeInput
    {
        public EarthDirectionalDodgeInput(
            float2 localDirection,
            bool grounded,
            bool surfing,
            bool casting,
            bool physicalRecovery,
            EarthAuthoredActionId currentAction,
            float currentNormalizedTime)
        {
            LocalDirection = localDirection;
            Grounded = grounded;
            Surfing = surfing;
            Casting = casting;
            PhysicalRecovery = physicalRecovery;
            CurrentAction = currentAction;
            CurrentNormalizedTime = math.saturate(currentNormalizedTime);
        }

        public float2 LocalDirection { get; }
        public bool Grounded { get; }
        public bool Surfing { get; }
        public bool Casting { get; }
        public bool PhysicalRecovery { get; }
        public EarthAuthoredActionId CurrentAction { get; }
        public float CurrentNormalizedTime { get; }
    }

    public readonly struct EarthDirectionalDodgeDecision
    {
        public EarthDirectionalDodgeDecision(
            bool accepted,
            EarthDirectionalDodgeDirection direction,
            float2 blendDirection,
            EarthDirectionalDodgeRejectReason rejectReason)
        {
            Accepted = accepted;
            Direction = direction;
            BlendDirection = blendDirection;
            RejectReason = rejectReason;
        }

        public bool Accepted { get; }
        public EarthDirectionalDodgeDirection Direction { get; }
        public float2 BlendDirection { get; }
        public EarthDirectionalDodgeRejectReason RejectReason { get; }
    }

    /// <summary>
    /// Pure gate for authored directional dodges. It selects one of four real
    /// clips and never adds displacement, invulnerability or gameplay authority.
    /// The movement system that requests the dodge remains the sole motor owner.
    /// </summary>
    public static class EarthDirectionalDodgeGate
    {
        public static EarthDirectionalDodgeDecision Resolve(
            in EarthDirectionalDodgeInput input)
        {
            if (math.lengthsq(input.LocalDirection) < 0.04f)
                return Reject(EarthDirectionalDodgeRejectReason.NoDirection);
            if (!input.Grounded)
                return Reject(EarthDirectionalDodgeRejectReason.Airborne);
            if (input.Surfing)
                return Reject(EarthDirectionalDodgeRejectReason.Surfing);
            if (input.Casting)
                return Reject(EarthDirectionalDodgeRejectReason.Casting);
            if (input.PhysicalRecovery)
                return Reject(EarthDirectionalDodgeRejectReason.PhysicalRecovery);
            if (!EarthAuthoredActionCatalog.CanInterrupt(
                    input.CurrentAction,
                    input.CurrentNormalizedTime,
                    EarthAuthoredActionId.DirectionalDodge))
                return Reject(EarthDirectionalDodgeRejectReason.NonInterruptibleAction);

            float2 direction = math.normalizesafe(input.LocalDirection, new float2(0f, 1f));
            if (math.abs(direction.x) > math.abs(direction.y))
                return new EarthDirectionalDodgeDecision(
                    true,
                    direction.x < 0f
                        ? EarthDirectionalDodgeDirection.Left
                        : EarthDirectionalDodgeDirection.Right,
                    direction.x < 0f ? new float2(-1f, 0f) : new float2(1f, 0f),
                    EarthDirectionalDodgeRejectReason.None);
            return new EarthDirectionalDodgeDecision(
                true,
                direction.y < 0f
                    ? EarthDirectionalDodgeDirection.Backward
                    : EarthDirectionalDodgeDirection.Forward,
                direction.y < 0f ? new float2(0f, -1f) : new float2(0f, 1f),
                EarthDirectionalDodgeRejectReason.None);
        }

        private static EarthDirectionalDodgeDecision Reject(
            EarthDirectionalDodgeRejectReason reason) =>
            new EarthDirectionalDodgeDecision(
                false,
                EarthDirectionalDodgeDirection.Forward,
                float2.zero,
                reason);
    }

    public static class EarthAuthoredActionResolver
    {
        public static bool IsLandingAction(EarthAuthoredActionId action) =>
            action == EarthAuthoredActionId.SoftLanding ||
            action == EarthAuthoredActionId.MovingLandingRoll ||
            action == EarthAuthoredActionId.HardLandingBrace;

        /// <summary>
        /// The rescue clock may finish while the Animator is still blending the
        /// authored landing out. Keep the base-layer clip as the contact owner
        /// until that blend has actually left the landing state; otherwise a
        /// recovery frame is misreported as ordinary locomotion and foot IK can
        /// resume against a pose that still owns its authored contacts.
        /// </summary>
        public static EarthAuthoredActionId ResolveBaseLayerContactOwnership(
            EarthAuthoredActionId resolvedAction,
            EarthAuthoredActionId activeBaseLayerAction)
        {
            if (!IsLandingAction(activeBaseLayerAction)) return resolvedAction;
            return resolvedAction == EarthAuthoredActionId.Locomotion ||
                   IsLandingAction(resolvedAction)
                ? activeBaseLayerAction
                : resolvedAction;
        }

        public static EarthAuthoredActionId Resolve(
            EarthAnimationPhase phase,
            EarthLandingStyle landingStyle,
            bool recoverableKnockdownRecovery,
            bool casting,
            bool impactReaction = false,
            bool directionalDodge = false)
        {
            if (recoverableKnockdownRecovery)
                return EarthAuthoredActionId.RecoverableKnockdownRecovery;
            // Base-layer contact ownership outranks additive upper-body lanes.
            // A hit or cast during flight/landing may still play above the base,
            // but must never restore foot IK or erase the authored contact window.
            switch (phase)
            {
                case EarthAnimationPhase.Rising:
                    return EarthAuthoredActionId.Jump;
                case EarthAnimationPhase.Apex:
                case EarthAnimationPhase.Falling:
                case EarthAnimationPhase.PreLanding:
                    return EarthAuthoredActionId.Fall;
                case EarthAnimationPhase.LandingContact:
                case EarthAnimationPhase.LandingRecovery:
                    return landingStyle switch
                    {
                        EarthLandingStyle.Moving => EarthAuthoredActionId.MovingLandingRoll,
                        EarthLandingStyle.Hard => EarthAuthoredActionId.HardLandingBrace,
                        _ => EarthAuthoredActionId.SoftLanding
                    };
                case EarthAnimationPhase.SurfLoop:
                    return EarthAuthoredActionId.SurfCrouch;
            }
            if (impactReaction) return EarthAuthoredActionId.HitRecoil;
            if (directionalDodge) return EarthAuthoredActionId.DirectionalDodge;
            if (casting) return EarthAuthoredActionId.MagicCast;
            return EarthAuthoredActionId.Locomotion;
        }
    }
}
