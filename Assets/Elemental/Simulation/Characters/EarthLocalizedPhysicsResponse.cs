using Elemental.Simulation.Combat;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public readonly struct EarthLocalizedPhysicsTuning
    {
        public readonly float WeakDriveSeconds, RecoverySeconds, ParentTransfer, MediumStunSeconds;
        public readonly float DriveSpring, DriveDamping, WeakDriveScale, MaximumDisplacement, MaximumAngle;
        public readonly float2 HeadLimits, TorsoLimits, ArmLimits, LegLimits;
        public EarthLocalizedPhysicsTuning(float weakDriveSeconds, float recoverySeconds, float parentTransfer,
            float mediumStunSeconds, float driveSpring, float driveDamping, float weakDriveScale,
            float maximumDisplacement, float maximumAngle)
            : this(weakDriveSeconds, recoverySeconds, parentTransfer, mediumStunSeconds, driveSpring,
                driveDamping, weakDriveScale, maximumDisplacement, maximumAngle,
                new float2(.065f, 18f), new float2(.12f, 28f), new float2(.12f, 28f), new float2(.08f, 22f)) { }
        public EarthLocalizedPhysicsTuning(float weakDriveSeconds, float recoverySeconds, float parentTransfer,
            float mediumStunSeconds, float driveSpring, float driveDamping, float weakDriveScale,
            float maximumDisplacement, float maximumAngle, float2 headLimits, float2 torsoLimits,
            float2 armLimits, float2 legLimits)
        {
            WeakDriveSeconds = math.clamp(weakDriveSeconds, .04f, .4f);
            RecoverySeconds = math.clamp(recoverySeconds, .1f, 1.5f);
            ParentTransfer = math.saturate(parentTransfer);
            MediumStunSeconds = math.clamp(mediumStunSeconds, .05f, .6f);
            DriveSpring = math.clamp(driveSpring, 10f, 400f);
            DriveDamping = math.clamp(driveDamping, .2f, 2f);
            WeakDriveScale = math.clamp(weakDriveScale, .01f, .5f);
            MaximumDisplacement = math.clamp(maximumDisplacement, .02f, .25f);
            MaximumAngle = math.clamp(maximumAngle, 5f, 45f);
            float2 minimum = new(.005f, 1f), maximum = new(MaximumDisplacement, MaximumAngle);
            HeadLimits = math.clamp(headLimits, minimum, maximum);
            TorsoLimits = math.clamp(torsoLimits, minimum, maximum);
            ArmLimits = math.clamp(armLimits, minimum, maximum);
            LegLimits = math.clamp(legLimits, minimum, maximum);
        }
        // X: world metres; Y: degrees. Indices share the eleven-bone response contract.
        public float2 LimitsFor(int region) => region == 2 ? HeadLimits :
            region >= 3 && region <= 6 ? ArmLimits : region >= 7 ? LegLimits : TorsoLimits;
        public static EarthLocalizedPhysicsTuning Default => new(.12f, .5f, .4f, .24f, 90f, .9f, .06f, .12f, 28f);
    }

    public static class EarthLocalizedPhysicsResponse
    {
        public const int BoneCount = 11;
        // Animated characters collide through their motor capsule. Its outer rim
        // is not the struck bone: follow the accepted incoming path into that
        // capsule, with bounded depth and a small preference for the near side.
        public static float RegionContactScore(float3 bone, float3 contact, float3 incomingDirection, float depth)
        {
            float3 direction = math.normalizesafe(incomingDirection);
            float along = math.clamp(math.dot(bone - contact, direction), 0f, math.max(0f, depth));
            return math.lengthsq(bone - contact - direction * along) + along * along * .025f;
        }
        public static int Parent(int bone) => bone switch
        {
            1 => 0, 2 => 1, 3 => 1, 4 => 3, 5 => 1, 6 => 5,
            7 => 0, 8 => 7, 9 => 0, 10 => 9, _ => -1
        };
        public static bool IsLocal(EarthCharacterImpactResponse response) =>
            response is EarthCharacterImpactResponse.Flinch or EarthCharacterImpactResponse.Stagger;
        public static float Recovery01(float age, in EarthLocalizedPhysicsTuning tuning) =>
            math.smoothstep(0f, 1f, math.saturate((age - tuning.WeakDriveSeconds) / tuning.RecoverySeconds));
        public static float DriveScale(float age, in EarthLocalizedPhysicsTuning tuning) =>
            math.lerp(tuning.WeakDriveScale, 1f, Recovery01(age, in tuning));
        public static float VisibleWeight(float age, in EarthLocalizedPhysicsTuning tuning) =>
            age < 0f ? 0f : 1f - Recovery01(age, in tuning);
        public static float LocalVelocity(float reactionVelocity, EarthCharacterImpactResponse response) =>
            !IsLocal(response) || !math.isfinite(reactionVelocity) ? 0f :
            math.clamp(reactionVelocity * .7f, .22f, response == EarthCharacterImpactResponse.Stagger ? 2.2f : .95f);
        public static float StunSeconds(EarthCharacterImpactResponse response, in EarthLocalizedPhysicsTuning tuning) =>
            response == EarthCharacterImpactResponse.Stagger ? tuning.MediumStunSeconds : 0f;
    }
}
