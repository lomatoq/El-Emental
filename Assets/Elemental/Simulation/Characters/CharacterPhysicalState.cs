using Elemental.Core.IDs;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum CharacterPhysicalMode : byte
    {
        AnimatedMotor = 0,
        PhysicalAssist = 1,
        Stagger = 2,
        FullRagdoll = 3,
        Recovery = 4
    }

    public enum RecoveryCandidate : byte
    {
        None = 0,
        FaceUp = 1,
        FaceDown = 2,
        LeftSide = 3,
        RightSide = 4
    }

    public readonly struct CharacterPhysicalState
    {
        public CharacterPhysicalState(
            ActorId actor,
            CharacterPhysicalMode mode,
            float3 linearVelocity,
            float3 angularVelocity,
            float3 gravityUp,
            float balanceError,
            float staggerDebt,
            float muscleStrength,
            RecoveryCandidate recovery)
        {
            Actor = actor;
            Mode = mode;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
            GravityUp = gravityUp;
            BalanceError = balanceError;
            StaggerDebt = staggerDebt;
            MuscleStrength = muscleStrength;
            Recovery = recovery;
        }

        public ActorId Actor { get; }
        public CharacterPhysicalMode Mode { get; }
        public float3 LinearVelocity { get; }
        public float3 AngularVelocity { get; }
        public float3 GravityUp { get; }
        public float BalanceError { get; }
        public float StaggerDebt { get; }
        public float MuscleStrength { get; }
        public RecoveryCandidate Recovery { get; }
    }

    public readonly struct CharacterPhysicalFrame
    {
        public CharacterPhysicalFrame(
            float deltaTime,
            float3 gravityUp,
            float3 centerOfMass,
            float3 supportCenter,
            int contactCount,
            float3 linearVelocity,
            float3 angularVelocity,
            float3 chestUp,
            float3 chestRight)
        {
            DeltaTime = deltaTime;
            GravityUp = gravityUp;
            CenterOfMass = centerOfMass;
            SupportCenter = supportCenter;
            ContactCount = contactCount;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
            ChestUp = chestUp;
            ChestRight = chestRight;
        }

        public float DeltaTime { get; }
        public float3 GravityUp { get; }
        public float3 CenterOfMass { get; }
        public float3 SupportCenter { get; }
        public int ContactCount { get; }
        public float3 LinearVelocity { get; }
        public float3 AngularVelocity { get; }
        public float3 ChestUp { get; }
        public float3 ChestRight { get; }
    }
}
