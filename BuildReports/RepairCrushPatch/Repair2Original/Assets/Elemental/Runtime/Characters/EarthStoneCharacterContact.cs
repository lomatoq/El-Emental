using Elemental.Simulation.Combat;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    /// <summary>Deliver before the source fractures/deactivates its collider.</summary>
    public static class EarthStoneCharacterContact
    {
        public static bool Deliver(Collision collision, Rigidbody source, uint sourceStableId)
        {
            if (collision == null || collision.contactCount == 0 || source == null || source.isKinematic ||
                collision.collider == null) return false;
            EarthCharacterImpactTarget target = collision.collider.GetComponentInParent<EarthCharacterImpactTarget>();
            if (target == null || target.Body == source) return false;
            ContactPoint contact = collision.GetContact(0);
            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            // This callback belongs to the stone. Its contact normal points out
            // of the receiver, so travel into the receiver has the opposite sign.
            Vector3 incoming = (Vector3)EarthCharacterImpactSolver.OrientIncomingContactVelocity(
                (float3)collision.relativeVelocity, -(float3)contact.normal);
            target.ApplyStoneImpact(contact.point, incoming, source.mass, speed,
                EarthCharacterImpactSourceKind.LooseStone, sourceStableId);
            return true;
        }
    }
}
