using Elemental.Simulation.Bending;

namespace Elemental.Simulation.Characters
{
    /// <summary>Stable semantic slots shared by gameplay, Animator authoring and QA.</summary>
    public enum EarthHumanoidPoseSlot : byte
    {
        None = 0,
        RaiseWall = 1,
        RaisePlatform = 2,
        PullStone = 3,
        HeavyThrow = 4,
        VectorPush = 5,
        GravityRepair = 6,
        WaveResonance = 7,
        Pillar = 8,
        ArmorAssemble = 9,
        ArmorBarrage = 10,
        GenericCast = 11
    }

    public static class EarthHumanoidMotionResolver
    {
        public static float ResolveMotionTime(EarthCastPhase phase) => phase switch
        {
            EarthCastPhase.Acquire => 0.06f,
            EarthCastPhase.Root => 0.18f,
            EarthCastPhase.Load => 0.34f,
            EarthCastPhase.Strike => 0.52f,
            EarthCastPhase.Sustain => 0.68f,
            EarthCastPhase.Recover => 0.88f,
            _ => 0f
        };

        public static EarthHumanoidPoseSlot Resolve(EarthTechniqueId technique) => technique switch
        {
            EarthTechniqueId.RaiseWall => EarthHumanoidPoseSlot.RaiseWall,
            EarthTechniqueId.RaisePlatform => EarthHumanoidPoseSlot.RaisePlatform,
            EarthTechniqueId.PullStone or EarthTechniqueId.CrestPluck => EarthHumanoidPoseSlot.PullStone,
            EarthTechniqueId.ThrowStone => EarthHumanoidPoseSlot.HeavyThrow,
            EarthTechniqueId.VectorPush => EarthHumanoidPoseSlot.VectorPush,
            EarthTechniqueId.GravityGrip or EarthTechniqueId.Repair => EarthHumanoidPoseSlot.GravityRepair,
            EarthTechniqueId.WebWave or EarthTechniqueId.Resonance => EarthHumanoidPoseSlot.WaveResonance,
            EarthTechniqueId.PillarJump => EarthHumanoidPoseSlot.Pillar,
            EarthTechniqueId.Armor or EarthTechniqueId.ArmorDome or EarthTechniqueId.ArmorOrbit or
                EarthTechniqueId.ArmorRepack => EarthHumanoidPoseSlot.ArmorAssemble,
            EarthTechniqueId.ArmorBarrage => EarthHumanoidPoseSlot.ArmorBarrage,
            EarthTechniqueId.None or EarthTechniqueId.Surf => EarthHumanoidPoseSlot.None,
            _ => EarthHumanoidPoseSlot.GenericCast
        };
    }
}
