using Unity.Mathematics;

namespace Elemental.Simulation.Matter
{
    public enum EarthPrimitiveOperation : byte
    {
        Acquire = 0,
        Detach = 1,
        Extrude = 2,
        Sculpt = 3,
        Compress = 4,
        Split = 5,
        Merge = 6,
        Bind = 7,
        Anchor = 8,
        Unbind = 9,
        Translate = 10,
        Rotate = 11,
        Orbit = 12,
        Spin = 13,
        Propel = 14,
        Redirect = 15,
        Recall = 16,
        Repair = 17,
        Reintegrate = 18,
        Sense = 19
    }

    public readonly struct EarthPrimitiveCommand
    {
        public EarthPrimitiveCommand(
            uint tick,
            EarthPrimitiveOperation operation,
            EarthMatterId matter,
            EarthOwnerId owner,
            float3 point,
            float3 direction,
            float scalar,
            uint targetStableId = 0u)
        {
            Tick = tick;
            Operation = operation;
            Matter = matter;
            Owner = owner;
            Point = point;
            Direction = math.normalizesafe(direction);
            Scalar = scalar;
            TargetStableId = targetStableId;
        }
        public uint Tick { get; }
        public EarthPrimitiveOperation Operation { get; }
        public EarthMatterId Matter { get; }
        public EarthOwnerId Owner { get; }
        public float3 Point { get; }
        public float3 Direction { get; }
        public float Scalar { get; }
        public uint TargetStableId { get; }
    }
}
