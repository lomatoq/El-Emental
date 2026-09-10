using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public static class EarthStructureImpactRouter
    {
        // Only structural damage uses this admission. Character contact and cosmetic
        // self-break retain their existing routes. Normal momentum bounds solver spikes.
        public static float CollisionStrength(Collision collision, Rigidbody source)
        {
            if(collision==null||collision.contactCount==0||source==null)return 0f;
            float closing=Mathf.Max(0f,Vector3.Dot(collision.relativeVelocity,collision.GetContact(0).normal));
            float mass=source.mass;
            Rigidbody other=collision.rigidbody;
            if(other!=null&&!other.isKinematic)mass=mass*other.mass/(mass+other.mass);
            return Elemental.Simulation.Structures.EarthArenaFractureGate.NormalContactImpulse(
                closing,mass,collision.impulse.magnitude);
        }
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
