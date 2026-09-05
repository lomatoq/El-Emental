using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public static class EarthStructureImpactRouter
    {
        public static bool Apply(Collider collider, in EarthStructureImpact impact)
        {
            if (collider == null) return false;
            var arenaPiece = collider.GetComponentInParent<EarthArenaPiece>();
            if (arenaPiece != null)
                return arenaPiece.IsEarthTargetValid ? arenaPiece.ApplyEarthImpact(in impact) :
                    arenaPiece.Owner != null && arenaPiece.Owner.ApplyEarthImpact(in impact);
            var wallPiece = collider.GetComponentInParent<EarthWallPiece>();
            if (wallPiece != null && wallPiece.Owner != null)
                return wallPiece.Owner.ApplyEarthImpact(in impact);
            var platformPiece = collider.GetComponentInParent<EarthPlatformPiece>();
            if (platformPiece != null && platformPiece.Owner != null)
                return platformPiece.Owner.ApplyEarthImpact(in impact);
            return collider.GetComponentInParent<IEarthDamageableStructure>()?.ApplyEarthImpact(in impact) ?? false;
        }
    }
}
