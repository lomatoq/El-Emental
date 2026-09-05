using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum EarthMaterialFeedbackKind { Impact, Fracture, Extract, Emerge, Assemble, RepairSeat, RepairComplete, Friction, Footstep, Land, Roll, Release, WaveSurfaceContact, WaveSurfaceBurst, ExtractionSurfaceContact }

    public readonly struct EarthMaterialFeedbackCue
    {
        public readonly EarthMaterialFeedbackKind Kind;
        public readonly float3 Point, Normal;
        public readonly float Strength, Radius, ParticleSizeScale;
        public readonly uint SourceId, Generation;
        public readonly int DustCount, ChipCount;
        public EarthMaterialFeedbackCue(EarthMaterialFeedbackKind kind, float3 point, float3 normal,
            float strength, float radius, uint sourceId, uint generation, int dustCount, int chipCount, float particleSizeScale = 1f)
        {
            Kind = kind; Point = point; Normal = math.normalizesafe(normal, new float3(0, 1, 0));
            Strength = strength; Radius = radius; SourceId = sourceId; Generation = generation;
            DustCount = dustCount; ChipCount = chipCount;
            ParticleSizeScale = particleSizeScale;
        }
        public EarthMaterialFeedbackCue WithCounts(int dust, int chips) =>
            new EarthMaterialFeedbackCue(Kind, Point, Normal, Strength, Radius, SourceId, Generation, dust, chips, ParticleSizeScale);
    }
}
