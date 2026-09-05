using Elemental.Simulation.Bending;
using Elemental.Simulation.Magic;

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
        public static bool ShouldInterruptRecovery(EarthCastPhase phase, float moveMagnitude) => false;

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
            EarthTechniqueId.WebWave or EarthTechniqueId.FaultLine or
                EarthTechniqueId.Resonance => EarthHumanoidPoseSlot.WaveResonance,
            EarthTechniqueId.PillarJump => EarthHumanoidPoseSlot.Pillar,
            EarthTechniqueId.Armor or EarthTechniqueId.ArmorDome or EarthTechniqueId.ArmorOrbit or
                EarthTechniqueId.ArmorRepack => EarthHumanoidPoseSlot.ArmorAssemble,
            EarthTechniqueId.ArmorBarrage => EarthHumanoidPoseSlot.ArmorBarrage,
            EarthTechniqueId.QuickStonePunch => EarthHumanoidPoseSlot.GenericCast,
            EarthTechniqueId.None or EarthTechniqueId.Surf => EarthHumanoidPoseSlot.None,
            _ => EarthHumanoidPoseSlot.GenericCast
        };
    }

    /// <summary>
    /// Maps every shipping elemental command to the shared eleven-pose humanoid
    /// vocabulary. Gameplay remains element-specific; presentation consumes one
    /// stable semantic stream so a one-frame command cannot disappear between
    /// Update and FixedUpdate.
    /// </summary>
    public static class MagicPresentationSemanticResolver
    {
        public static EarthTechniqueId ResolveTechnique(ElementId element, AbilityId ability)
        {
            ushort value = ability.Value;
            return element switch
            {
                ElementId.Earth => value switch
                {
                    1 => EarthTechniqueId.RaiseWall,
                    2 => EarthTechniqueId.PullStone,
                    3 => EarthTechniqueId.ThrowStone,
                    4 => EarthTechniqueId.RaisePlatform,
                    5 => EarthTechniqueId.VectorPush,
                    6 => EarthTechniqueId.PillarJump,
                    _ => EarthTechniqueId.MeteorFinish
                },
                ElementId.Air => value switch
                {
                    101 => EarthTechniqueId.VectorPush,
                    102 => EarthTechniqueId.GravityGrip,
                    103 => EarthTechniqueId.PillarJump,
                    _ => EarthTechniqueId.MeteorFinish
                },
                ElementId.Fire => value switch
                {
                    201 => EarthTechniqueId.VectorPush,
                    202 => EarthTechniqueId.GravityGrip,
                    _ => EarthTechniqueId.MeteorFinish
                },
                ElementId.Water => value switch
                {
                    301 => EarthTechniqueId.PullStone,
                    302 => EarthTechniqueId.VectorPush,
                    303 => EarthTechniqueId.RaisePlatform,
                    304 => EarthTechniqueId.Resonance,
                    _ => EarthTechniqueId.MeteorFinish
                },
                _ => EarthTechniqueId.MeteorFinish
            };
        }

        public static EarthTechniqueKind ResolveKind(EarthTechniqueId technique) => technique switch
        {
            EarthTechniqueId.RaiseWall => EarthTechniqueKind.Wall,
            EarthTechniqueId.RaisePlatform => EarthTechniqueKind.Platform,
            EarthTechniqueId.PillarJump => EarthTechniqueKind.Pillar,
            EarthTechniqueId.WebWave or EarthTechniqueId.Resonance or EarthTechniqueId.FaultLine =>
                EarthTechniqueKind.GroundWave,
            EarthTechniqueId.GravityGrip or EarthTechniqueId.Repair => EarthTechniqueKind.Repair,
            EarthTechniqueId.QuickStonePunch => EarthTechniqueKind.Grip,
            _ => EarthTechniqueKind.Grip
        };
    }
}
