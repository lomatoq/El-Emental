using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum EarthArmorCoverageZone : byte
    {
        NeckCollar = 0,
        LeftShoulder = 1,
        RightShoulder = 2,
        UpperTorsoSeam = 3,
        LowerTorsoSeam = 4
    }

    public readonly struct EarthArmorCoverageFiller
    {
        public readonly EarthArmorCoverageZone Zone;
        public readonly float3 Direction;
        public readonly float3 Scale;

        public EarthArmorCoverageFiller(
            EarthArmorCoverageZone zone,
            float3 direction,
            float3 scale)
        {
            Zone = zone;
            Direction = math.normalizesafe(direction, new float3(0f, 0f, 1f));
            Scale = scale;
        }
    }

    /// <summary>
    /// Additional small plates for anatomical junctions that cannot be covered by
    /// the 96-piece projectile shell without taking stones away from limbs. Values
    /// are body-relative and contain no pose state, so runtime follows the final
    /// animated Humanoid bones instead of baking an assembly stance.
    /// </summary>
    public static class EarthArmorCoverageShell
    {
        public const int FillerCount = 28;

        public static EarthArmorCoverageFiller Filler(int index)
        {
            int safe = math.clamp(index, 0, FillerCount - 1);
            if (safe < 8)
            {
                float angle = math.radians(22.5f + safe * 45f);
                return new EarthArmorCoverageFiller(
                    EarthArmorCoverageZone.NeckCollar,
                    new float3(math.sin(angle), -.18f, math.cos(angle)),
                    new float3(.088f, .042f, .108f));
            }

            if (safe < 16)
            {
                int shoulderIndex = (safe - 8) % 4;
                bool left = safe < 12;
                float side = left ? -1f : 1f;
                float3 direction = shoulderIndex switch
                {
                    0 => new float3(side, .58f, 0f),
                    1 => new float3(side * .86f, .10f, .52f),
                    2 => new float3(side * .86f, .10f, -.52f),
                    _ => new float3(-side * .55f, .72f, 0f)
                };
                return new EarthArmorCoverageFiller(
                    left ? EarthArmorCoverageZone.LeftShoulder : EarthArmorCoverageZone.RightShoulder,
                    direction,
                    new float3(.130f, .046f, .155f));
            }

            int torsoIndex = safe - 16;
            bool upper = torsoIndex < 6;
            int ringIndex = torsoIndex % 6;
            float ringAngle = math.radians((upper ? 30f : 0f) + ringIndex * 60f);
            return new EarthArmorCoverageFiller(
                upper ? EarthArmorCoverageZone.UpperTorsoSeam : EarthArmorCoverageZone.LowerTorsoSeam,
                new float3(math.sin(ringAngle), upper ? .28f : -.24f, math.cos(ringAngle)),
                new float3(.185f, .052f, .205f));
        }
    }
}
