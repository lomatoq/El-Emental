using Elemental.Runtime.Physics;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public enum HumanoidRagdollBoneRole : byte
    {
        Pelvis = 0,
        Chest = 1,
        Head = 2,
        LeftUpperArm = 3,
        LeftLowerArm = 4,
        RightUpperArm = 5,
        RightLowerArm = 6,
        LeftUpperLeg = 7,
        LeftLowerLeg = 8,
        RightUpperLeg = 9,
        RightLowerLeg = 10
    }

    /// <summary>
    /// Serializable handle for one authored bone in the visible Humanoid ragdoll.
    /// This MonoBehaviour intentionally lives in its own file so Unity can persist
    /// every bone reference when the shipping scene is saved and loaded.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HumanoidRagdollBone : MonoBehaviour
    {
        [SerializeField] private HumanoidRagdollBoneRole role;
        [SerializeField] private Rigidbody body;
        [SerializeField] private Collider shape;
        [SerializeField] private ConfigurableJoint joint;
        [SerializeField] private GravityBody gravityBody;

        public HumanoidRagdollBoneRole Role => role;
        public Rigidbody Body => body;
        public Collider Shape => shape;
        public GravityBody GravityBody => gravityBody;

        public void Configure(
            HumanoidRagdollBoneRole configuredRole,
            Rigidbody configuredBody,
            Collider configuredShape,
            ConfigurableJoint configuredJoint,
            GravityBody configuredGravityBody)
        {
            role = configuredRole;
            body = configuredBody;
            shape = configuredShape;
            joint = configuredJoint;
            gravityBody = configuredGravityBody;
        }
    }
}
