using Elemental.Runtime.Characters;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>Earth manipulates matter, never the character's physical body.</summary>
    public static class EarthBodyTargetFilter
    {
        public static bool IsCharacterBody(Rigidbody body)
        {
            if (body == null) return false;
            // Bone markers survive ragdoll activation, disabling and detachment.
            if (body.GetComponent<HumanoidRagdollBone>() != null ||
                body.GetComponent<ActiveRagdollJoint>() != null ||
                body.GetComponent<PlanetMotor>() != null ||
                body.GetComponent<EarthCharacterImpactTarget>() != null ||
                body.GetComponent<ActiveRagdollPuppet>() != null) return true;
            PhysicalImpactTarget receiver = body.GetComponent<PhysicalImpactTarget>();
            if (receiver != null && receiver.HasCharacterImpactReceiver) return true;
            // A separately simulated stone/armor piece can be parented to an actor.
            // Its explicit matter owner is not the actor's body.
            IEarthPhysicalTarget matter = body.GetComponent<IEarthPhysicalTarget>();
            if (matter != null && !(matter is PhysicalImpactTarget)) return false;
            return body.GetComponentInParent<PlanetMotor>(true) != null ||
                   body.GetComponentInParent<ActiveRagdollPuppet>(true) != null ||
                   body.GetComponentInParent<HumanoidRagdollRig>(true) != null ||
                   body.GetComponentInParent<EarthCharacterImpactTarget>(true) != null;
        }
    }
}
