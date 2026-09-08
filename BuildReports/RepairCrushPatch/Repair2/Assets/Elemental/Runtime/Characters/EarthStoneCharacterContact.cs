using Elemental.Simulation.Combat;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    /// <summary>Deliver before the source fractures/deactivates its collider.</summary>
    public static class EarthStoneCharacterContact
    {
        public static EarthCharacterImpactTarget ResolveTarget(Collider collider)
        {
            if (collider == null) return null;
            EarthCharacterImpactTarget target = collider.GetComponentInParent<EarthCharacterImpactTarget>();
            if (target != null) return target;
            // The physical visual rig is deliberately unparented at full handoff.
            // Keep its explicit motor-owner link for subsequent damage and death.
            HumanoidRagdollRig rig = collider.GetComponentInParent<HumanoidRagdollRig>();
            return rig != null ? rig.ImpactReceiver : null;
        }

        public static bool Deliver(Collision collision, Rigidbody source, uint sourceStableId)
        {
            if (collision == null || collision.contactCount == 0 || source == null || source.isKinematic ||
                collision.collider == null) return false;
            EarthCharacterImpactTarget target = ResolveTarget(collision.collider);
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
