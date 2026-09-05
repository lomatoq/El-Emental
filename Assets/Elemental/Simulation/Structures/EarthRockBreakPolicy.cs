using Unity.Mathematics;
using Elemental.Simulation.Matter;

namespace Elemental.Simulation.Structures
{
    public readonly struct EarthRockBreakDecision
    {
        public EarthRockBreakDecision(bool breaks, int physicalPieces, int dust, int chips)
        { Breaks = breaks; PhysicalPieces = physicalPieces; DustCount = dust; ChipCount = chips; }
        public bool Breaks { get; }
        public int PhysicalPieces { get; }
        public int DustCount { get; }
        public int ChipCount { get; }
    }

    /// <summary>Physical loose-stone break policy; never consumes repairable structure cells.</summary>
    public static class EarthRockBreakPolicy
    {
        public static EarthMatterRecord PartitionChild(in EarthMatterRecord parent, int count,
            in EarthMatterPose pose, float3 velocity)
            => PartitionChild(parent, 1f / math.max(1, count), pose, velocity);

        public static EarthMatterRecord PartitionChild(in EarthMatterRecord parent, float fraction,
            in EarthMatterPose pose, float3 velocity)
        {
            EarthMatterRecord child = parent;
            fraction = math.clamp(fraction, 0f, 1f);
            child.Id = default;
            child.Volume *= fraction;
            child.Mass *= fraction;
            child.Phase = EarthMatterPhase.FreeDynamic;
            child.Representation = EarthRepresentationTier.SecondaryPhysical;
            child.Shape = EarthShapeSemantic.NaturalRock;
            child.CurrentPose = pose;
            child.LinearVelocity = velocity;
            EarthSourceProvenance source = parent.Source;
            child.Source = new EarthSourceProvenance(source.Kind, source.SourceStableId,
                source.SourceGeneration, source.SourceCellIndex, source.SourceRevision,
                source.SourceLocalPoint, source.ReservedVolume * fraction, source.Flags);
            return child;
        }

        public static EarthRockBreakDecision Resolve(float radius, float mass, float impulse,
            bool controlled, int depth = 0, float smallRadius = 0.35f, float hugeRadius = 1.2f,
            float minimumImpulse = 45f, float specificImpulse = 7.5f,
            int mediumPieces = 4, int hugePieces = 3, int maximumSplitDepth = 2,
            float smallImpactSpeed = 0.75f)
        {
            if (controlled || !math.isfinite(radius) || !math.isfinite(mass) ||
                !math.isfinite(impulse) || radius <= 0f || mass <= 0f || impulse <= 0f)
                return default;
            bool small = radius <= smallRadius;
            bool breaks = small ? impulse / mass >= math.max(0.75f, smallImpactSpeed) :
                impulse >= minimumImpulse && impulse / mass >= specificImpulse;
            if (!breaks) return new EarthRockBreakDecision(false, 0, 8, 2);
            bool huge = radius > math.max(smallRadius, hugeRadius);
            return new EarthRockBreakDecision(true,
                small || depth >= math.clamp(maximumSplitDepth, 0, 2) ? 0 :
                huge ? math.clamp(hugePieces, 2, 3) : math.clamp(mediumPieces, 2, 4),
                small ? 24 : huge ? 140 : 64, small ? 8 : huge ? 28 : 16);
        }
    }
}
