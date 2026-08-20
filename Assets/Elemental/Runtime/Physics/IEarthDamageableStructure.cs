using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public enum EarthStructureImpactKind : byte
    {
        Projectile = 0,
        Construction = 1,
        Pluck = 2,
        Surf = 3
    }

    public readonly struct EarthStructureImpact
    {
        public EarthStructureImpact(
            Vector3 point,
            Vector3 direction,
            float impulse,
            EarthStructureImpactKind kind,
            uint sourceId = 0u)
        {
            Point = point;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
            Impulse = Mathf.Max(0f, impulse);
            Kind = kind;
            SourceId = sourceId;
        }

        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public float Impulse { get; }
        public EarthStructureImpactKind Kind { get; }
        public uint SourceId { get; }
    }

    public interface IEarthDamageableStructure
    {
        uint StructureId { get; }
        bool ApplyEarthImpact(in EarthStructureImpact impact);
    }

    public interface IEarthPluckableStructure
    {
        bool TryPluckCell(Vector3 point, out IEarthPhysicalTarget target);
    }
}
