using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Voxel
{
    public enum SdfEditKind : byte
    {
        AddSphere = 1,
        SubtractSphere = 2,
        AddCapsule = 3,
        SubtractCapsule = 4
    }

    public readonly struct SdfEdit
    {
        public SdfEdit(
            uint sequence,
            SdfEditKind kind,
            float3 pointA,
            float3 pointB,
            float radius,
            VoxelMaterialId material)
        {
            if (!math.all(math.isfinite(pointA)) || !math.all(math.isfinite(pointB)))
            {
                throw new ArgumentException("Edit points must be finite.");
            }

            if (!math.isfinite(radius) || radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            Sequence = sequence;
            Kind = kind;
            PointA = pointA;
            PointB = kind == SdfEditKind.AddSphere || kind == SdfEditKind.SubtractSphere
                ? pointA
                : pointB;
            Radius = radius;
            Material = material.IsEmpty ? new VoxelMaterialId(1) : material;
        }

        public uint Sequence { get; }
        public SdfEditKind Kind { get; }
        public float3 PointA { get; }
        public float3 PointB { get; }
        public float Radius { get; }
        public VoxelMaterialId Material { get; }

        public bool IsAdditive => Kind == SdfEditKind.AddSphere || Kind == SdfEditKind.AddCapsule;

        public float SampleShapeDistance(float3 position)
        {
            if (Kind == SdfEditKind.AddSphere || Kind == SdfEditKind.SubtractSphere)
            {
                return math.distance(position, PointA) - Radius;
            }

            float3 segment = PointB - PointA;
            float denominator = math.lengthsq(segment);
            float interpolation = denominator > 0.000001f
                ? math.saturate(math.dot(position - PointA, segment) / denominator)
                : 0f;
            float3 closest = PointA + (segment * interpolation);
            return math.distance(position, closest) - Radius;
        }

        public VoxelBounds GetBounds()
        {
            float3 radiusVector = new float3(Radius);
            return new VoxelBounds(
                math.min(PointA, PointB) - radiusVector,
                math.max(PointA, PointB) + radiusVector);
        }
    }
}
