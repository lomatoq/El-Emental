using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    public enum EarthDuelFighterId : byte
    {
        Player = 0,
        Bot = 1
    }

    public enum EarthCharacterImpactSourceKind : byte
    {
        Physics = 0,
        LooseStone = 1,
        ArmorProjectile = 2,
        SurfNose = 3,
        PillarWave = 4,
        BotProjectile = 5,
        StonePunch = 6,
        PillarCrest = 7,
        FallLanding = 8
    }

    public enum EarthCharacterImpactResponse : byte
    {
        Ignore = 0,
        Flinch = 1,
        Stagger = 2,
        RecoverableKnockdown = 3,
        Knockout = 4
    }

    public enum ImpactResponseMode : byte
    {
        Legacy = 0,
        Calibrated = 1
    }

    public readonly struct EarthCharacterImpact
    {
        public EarthCharacterImpact(
            uint sourceStableId,
            uint tick,
            EarthCharacterImpactSourceKind sourceKind,
            float3 point,
            float3 direction,
            float impulse,
            float targetMass,
            float closingSpeed = 0f,
            float strength01 = 0f)
        {
            SourceStableId = sourceStableId;
            Tick = tick;
            SourceKind = sourceKind;
            Point = point;
            Direction = math.normalizesafe(direction, new float3(0f, 1f, 0f));
            Impulse = math.max(0f, impulse);
            TargetMass = math.max(0.01f, targetMass);
            ClosingSpeed = math.max(0f, closingSpeed);
            Strength01 = math.saturate(strength01);
        }

        public uint SourceStableId { get; }
        public uint Tick { get; }
        public EarthCharacterImpactSourceKind SourceKind { get; }
        public float3 Point { get; }
        public float3 Direction { get; }
        public float Impulse { get; }
        public float TargetMass { get; }
        public float ClosingSpeed { get; }
        public float Strength01 { get; }
    }

    public readonly struct EarthCharacterImpactTuning
    {
        public EarthCharacterImpactTuning(
            float flinchVelocityChange,
            float staggerVelocityChange,
            float knockoutVelocityChange,
            float surfStaggerSpeed,
            float surfKnockoutSpeed,
            float maximumVelocityChange)
        {
            if (!float.IsFinite(flinchVelocityChange) || flinchVelocityChange < 0f)
                throw new ArgumentOutOfRangeException(nameof(flinchVelocityChange));
            if (!float.IsFinite(staggerVelocityChange) || staggerVelocityChange < flinchVelocityChange)
                throw new ArgumentOutOfRangeException(nameof(staggerVelocityChange));
            if (!float.IsFinite(knockoutVelocityChange) || knockoutVelocityChange < staggerVelocityChange)
                throw new ArgumentOutOfRangeException(nameof(knockoutVelocityChange));
            if (!float.IsFinite(surfStaggerSpeed) || surfStaggerSpeed < 0f)
                throw new ArgumentOutOfRangeException(nameof(surfStaggerSpeed));
            if (!float.IsFinite(surfKnockoutSpeed) || surfKnockoutSpeed < surfStaggerSpeed)
                throw new ArgumentOutOfRangeException(nameof(surfKnockoutSpeed));
            if (!float.IsFinite(maximumVelocityChange) || maximumVelocityChange < knockoutVelocityChange)
                throw new ArgumentOutOfRangeException(nameof(maximumVelocityChange));

            FlinchVelocityChange = flinchVelocityChange;
            StaggerVelocityChange = staggerVelocityChange;
            KnockoutVelocityChange = knockoutVelocityChange;
            SurfStaggerSpeed = surfStaggerSpeed;
            SurfKnockoutSpeed = surfKnockoutSpeed;
            MaximumVelocityChange = maximumVelocityChange;
        }

        public float FlinchVelocityChange { get; }
        public float StaggerVelocityChange { get; }
        public float KnockoutVelocityChange { get; }
        public float SurfStaggerSpeed { get; }
        public float SurfKnockoutSpeed { get; }
        public float MaximumVelocityChange { get; }

        public static EarthCharacterImpactTuning Default => new EarthCharacterImpactTuning(
            1f,
            2f,
            5f,
            3.5f,
            5f,
            12f);
    }

    public readonly struct EarthCharacterImpactResolution
    {
        public EarthCharacterImpactResolution(
            EarthCharacterImpactResponse response,
            float reactionVelocityChange,
            float appliedVelocityChange)
        {
            Response = response;
            ReactionVelocityChange = math.max(0f, reactionVelocityChange);
            AppliedVelocityChange = math.max(0f, appliedVelocityChange);
        }

        public EarthCharacterImpactResponse Response { get; }
        public float ReactionVelocityChange { get; }
        public float AppliedVelocityChange { get; }
        public float EffectiveVelocityChange => AppliedVelocityChange;
    }

    public readonly struct EarthCharacterImpactCalibration
    {
        public EarthCharacterImpactCalibration(
            float reactionMultiplier,
            float movementMultiplier,
            float maximumRootVelocityChange)
        {
            ReactionMultiplier = math.max(0f, reactionMultiplier);
            MovementMultiplier = math.max(0f, movementMultiplier);
            MaximumRootVelocityChange = math.max(0.1f, maximumRootVelocityChange);
        }

        public float ReactionMultiplier { get; }
        public float MovementMultiplier { get; }
        public float MaximumRootVelocityChange { get; }

        public static EarthCharacterImpactCalibration DefaultFor(
            EarthCharacterImpactSourceKind source) => source switch
        {
            EarthCharacterImpactSourceKind.LooseStone =>
                new EarthCharacterImpactCalibration(1f, 0.8f, 0.9f),
            EarthCharacterImpactSourceKind.ArmorProjectile =>
                new EarthCharacterImpactCalibration(1.1f, 0.85f, 1.35f),
            EarthCharacterImpactSourceKind.PillarWave =>
                new EarthCharacterImpactCalibration(1.28f, 0.48f, 2.3f),
            EarthCharacterImpactSourceKind.PillarCrest =>
                new EarthCharacterImpactCalibration(1.32f, 0.58f, 2.4f),
            EarthCharacterImpactSourceKind.StonePunch =>
                new EarthCharacterImpactCalibration(1.28f, 0.62f, 2f),
            EarthCharacterImpactSourceKind.SurfNose =>
                new EarthCharacterImpactCalibration(1.15f, 0.65f, 2.4f),
            EarthCharacterImpactSourceKind.BotProjectile =>
                new EarthCharacterImpactCalibration(1.1f, 0.85f, 1.35f),
            EarthCharacterImpactSourceKind.Physics =>
                new EarthCharacterImpactCalibration(1f, 0.8f, 1.5f),
            _ => new EarthCharacterImpactCalibration(1f, 1f, 4f)
        };
    }

    public static class EarthCharacterImpactSolver
    {
        public const uint DefaultDuplicateWindowTicks = 3u;

        public static EarthCharacterImpactResolution Resolve(
            in EarthCharacterImpact impact,
            in EarthCharacterImpactTuning tuning)
        {
            return Resolve(in impact, in tuning, ImpactResponseMode.Legacy);
        }

        public static EarthCharacterImpactResolution Resolve(
            in EarthCharacterImpact impact,
            in EarthCharacterImpactTuning tuning,
            ImpactResponseMode mode)
        {
            EarthCharacterImpactCalibration calibration =
                EarthCharacterImpactCalibration.DefaultFor(impact.SourceKind);
            return Resolve(in impact, in tuning, mode, in calibration);
        }

        public static EarthCharacterImpactResolution Resolve(
            in EarthCharacterImpact impact,
            in EarthCharacterImpactTuning tuning,
            ImpactResponseMode mode,
            in EarthCharacterImpactCalibration calibration)
        {
            return mode == ImpactResponseMode.Calibrated
                ? ResolveCalibrated(in impact, in tuning, in calibration)
                : ResolveLegacy(in impact, in tuning);
        }

        private static EarthCharacterImpactResolution ResolveLegacy(
            in EarthCharacterImpact impact,
            in EarthCharacterImpactTuning tuning)
        {
            float velocityChange = impact.Impulse / math.max(0.01f, impact.TargetMass);
            if (impact.SourceKind == EarthCharacterImpactSourceKind.SurfNose)
            {
                if (impact.ClosingSpeed >= tuning.SurfKnockoutSpeed)
                    velocityChange = math.max(velocityChange, tuning.KnockoutVelocityChange);
                else if (impact.ClosingSpeed >= tuning.SurfStaggerSpeed)
                    velocityChange = math.clamp(
                        velocityChange,
                        tuning.StaggerVelocityChange,
                        math.max(tuning.StaggerVelocityChange, tuning.KnockoutVelocityChange - 0.001f));
                else
                    velocityChange = math.min(
                        velocityChange,
                        math.max(0f, tuning.FlinchVelocityChange - 0.001f));
            }
            else if (impact.SourceKind == EarthCharacterImpactSourceKind.PillarWave)
            {
                velocityChange = math.max(velocityChange, tuning.KnockoutVelocityChange);
            }
            else if (impact.SourceKind == EarthCharacterImpactSourceKind.BotProjectile)
            {
                velocityChange = math.max(velocityChange, tuning.KnockoutVelocityChange);
            }
            else if (impact.SourceKind is EarthCharacterImpactSourceKind.StonePunch or
                     EarthCharacterImpactSourceKind.PillarCrest)
            {
                velocityChange = math.max(velocityChange, tuning.KnockoutVelocityChange);
            }
            else if (impact.SourceKind == EarthCharacterImpactSourceKind.Physics)
            {
                // Generic contacts can make a fighter stumble or enter the existing
                // recoverable physical-controller ragdoll, but never decide death.
                velocityChange = math.min(
                    velocityChange,
                    math.max(tuning.StaggerVelocityChange, tuning.KnockoutVelocityChange - 0.001f));
            }

            velocityChange = math.min(velocityChange, tuning.MaximumVelocityChange);
            EarthCharacterImpactResponse response = velocityChange >= tuning.KnockoutVelocityChange
                ? EarthCharacterImpactResponse.Knockout
                : velocityChange >= tuning.StaggerVelocityChange
                    ? EarthCharacterImpactResponse.Stagger
                    : velocityChange >= tuning.FlinchVelocityChange
                        ? EarthCharacterImpactResponse.Flinch
                        : EarthCharacterImpactResponse.Ignore;
            return new EarthCharacterImpactResolution(response, velocityChange, velocityChange);
        }

        private static EarthCharacterImpactResolution ResolveCalibrated(
            in EarthCharacterImpact impact,
            in EarthCharacterImpactTuning tuning,
            in EarthCharacterImpactCalibration calibration)
        {
            float physicalVelocityChange = impact.Impulse / math.max(0.01f, impact.TargetMass);
            float reactionSignal = impact.SourceKind == EarthCharacterImpactSourceKind.SurfNose
                ? math.max(physicalVelocityChange, impact.ClosingSpeed)
                : physicalVelocityChange;
            float reactionVelocityChange = math.min(
                reactionSignal * calibration.ReactionMultiplier,
                tuning.MaximumVelocityChange);
            if (impact.SourceKind == EarthCharacterImpactSourceKind.Physics)
            {
                reactionVelocityChange = math.min(
                    reactionVelocityChange,
                    math.max(tuning.StaggerVelocityChange, tuning.KnockoutVelocityChange - 0.001f));
            }

            float appliedVelocityChange = math.min(
                physicalVelocityChange * calibration.MovementMultiplier,
                math.min(tuning.MaximumVelocityChange, calibration.MaximumRootVelocityChange));
            EarthCharacterImpactResponse response = Classify(reactionVelocityChange, in tuning);
            return new EarthCharacterImpactResolution(
                response,
                reactionVelocityChange,
                appliedVelocityChange);
        }

        private static EarthCharacterImpactResponse Classify(
            float velocityChange,
            in EarthCharacterImpactTuning tuning)
        {
            return velocityChange >= tuning.KnockoutVelocityChange
                ? EarthCharacterImpactResponse.Knockout
                : velocityChange >= tuning.StaggerVelocityChange
                    ? EarthCharacterImpactResponse.Stagger
                    : velocityChange >= tuning.FlinchVelocityChange
                        ? EarthCharacterImpactResponse.Flinch
                        : EarthCharacterImpactResponse.Ignore;
        }

        public static bool IsDuplicate(
            uint sourceStableId,
            uint tick,
            uint previousSourceStableId,
            uint previousTick,
            uint windowTicks = DefaultDuplicateWindowTicks)
        {
            if (sourceStableId == 0u || sourceStableId != previousSourceStableId) return false;
            return unchecked(tick - previousTick) <= windowTicks;
        }
    }
}
