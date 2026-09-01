using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public enum EarthMotionProvenance : byte
    {
        Unknown = 0,
        KayKitCc0 = 1,
        Mixamo = 2
    }

    public enum EarthMotionSemanticAction : byte
    {
        Unknown = 0,
        Idle = 1,
        Locomotion = 2,
        Turn = 3,
        Jump = 4,
        Fall = 5,
        Landing = 6,
        Dodge = 7,
        Recovery = 8,
        Impact = 9,
        Cast = 10,
        Attack = 11,
        Crouch = 12,
        Surf = 13,
        Utility = 14
    }

    public enum EarthMotionStance : byte
    {
        Neutral = 0,
        Standing = 1,
        Crouched = 2,
        Airborne = 3,
        Knockdown = 4,
        Surf = 5
    }

    [Flags]
    public enum EarthMotionStyle : ushort
    {
        None = 0,
        Neutral = 1 << 0,
        Athletic = 1 << 1,
        Injured = 1 << 2,
        Heavy = 1 << 3,
        Magic = 1 << 4,
        Melee = 1 << 5,
        Ranged = 1 << 6,
        Defensive = 1 << 7,
        Recovery = 1 << 8
    }

    [Flags]
    public enum EarthMotionHandOccupancy : byte
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Both = Left | Right
    }

    [Flags]
    public enum EarthMotionEnvironmentTag : byte
    {
        None = 0,
        Grounded = 1 << 0,
        Airborne = 1 << 1,
        Landing = 1 << 2,
        Surf = 1 << 3,
        Combat = 1 << 4,
        Recovery = 1 << 5
    }

    [Flags]
    public enum EarthMotionActionTag : ushort
    {
        None = 0,
        Idle = 1 << 0,
        Locomotion = 1 << 1,
        Turn = 1 << 2,
        Jump = 1 << 3,
        Fall = 1 << 4,
        Land = 1 << 5,
        Dodge = 1 << 6,
        Cast = 1 << 7,
        Hit = 1 << 8,
        Recover = 1 << 9,
        Attack = 1 << 10,
        Crouch = 1 << 11,
        Surf = 1 << 12
    }

    [Flags]
    public enum EarthMotionManualCorrection : ushort
    {
        None = 0,
        SemanticAction = 1 << 0,
        Kinematics = 1 << 1,
        StanceAndStyle = 1 << 2,
        ContactCurves = 1 << 3,
        Windows = 1 << 4,
        HandAndMirroring = 1 << 5,
        Tags = 1 << 6
    }

    [Serializable]
    public struct EarthMotionPhaseWindow
    {
        public EarthMotionPhaseWindow(bool enabled, float start01, float end01)
        {
            Enabled = enabled;
            Start01 = Sanitize(start01);
            End01 = Sanitize(end01);
        }

        public bool Enabled;
        public float Start01;
        public float End01;

        public bool Contains(float phase01)
        {
            if (!Enabled) return false;
            float phase = Sanitize(phase01);
            return Start01 <= End01
                ? phase >= Start01 && phase <= End01
                : phase >= Start01 || phase <= End01;
        }

        private static float Sanitize(float value) =>
            math.saturate(math.isfinite(value) ? value : 0f);
    }
}
