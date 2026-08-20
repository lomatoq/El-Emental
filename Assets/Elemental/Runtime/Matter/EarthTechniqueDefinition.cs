using System;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Matter;
using UnityEngine;

namespace Elemental.Runtime.Matter
{
    [Flags]
    public enum EarthMatterRequirement : ushort
    {
        None = 0, Terrain = 1 << 0, Controlled = 1 << 1, Dynamic = 1 << 2,
        Structure = 1 << 3, Fractured = 1 << 4, Reintegrating = 1 << 5
    }

    public enum EarthGroundingRequirement : byte
    {
        Any = 0, StableSupport = 1, Grounded = 2, Airborne = 3
    }

    [Serializable]
    public struct EarthPrimitiveNode
    {
        public EarthPrimitiveOperation Operation;
        public float Scalar;
        public ushort RelativeTick;
    }

    [Serializable]
    public struct EarthFollowUpWindow
    {
        public EarthTechniqueId Technique;
        public float OpensAtSeconds;
        public float ClosesAtSeconds;
        public EarthEventTag RequiredResult;
        public bool RequireSameMatter;
    }

    [Serializable]
    public struct EarthCancelRule
    {
        public EarthGestureTokenKind Token;
        public float OpensAtSeconds;
        public float ClosesAtSeconds;
        public bool PreserveMatterMomentum;
    }

    [CreateAssetMenu(menuName = "Elemental/Earth/Technique Definition", fileName = "EarthTechnique")]
    public sealed class EarthTechniqueDefinition : ScriptableObject
    {
        [SerializeField] private EarthTechniqueId id;
        [SerializeField] private EarthGestureTokenKind entryToken;
        [SerializeField] private EarthMatterRequirement matterRequirement;
        [SerializeField] private EarthGroundingRequirement grounding;
        [SerializeField] private EarthPrimitiveNode[] graph = Array.Empty<EarthPrimitiveNode>();
        [SerializeField] private EarthFollowUpWindow[] followUps = Array.Empty<EarthFollowUpWindow>();
        [SerializeField] private EarthCancelRule[] cancels = Array.Empty<EarthCancelRule>();
        [SerializeField] private ScriptableObject poseProfile;
        [SerializeField] private ScriptableObject vfxProfile;
        [SerializeField] private ScriptableObject audioProfile;
        [SerializeField] private ScriptableObject cameraProfile;

        public EarthTechniqueId Id => id;
        public EarthGestureTokenKind EntryToken => entryToken;
        public EarthMatterRequirement MatterRequirement => matterRequirement;
        public EarthGroundingRequirement Grounding => grounding;
        public EarthPrimitiveNode[] Graph => graph;
        public EarthFollowUpWindow[] FollowUps => followUps;
        public EarthCancelRule[] Cancels => cancels;
        public ScriptableObject PoseProfile => poseProfile;
        public ScriptableObject VfxProfile => vfxProfile;
        public ScriptableObject AudioProfile => audioProfile;
        public ScriptableObject CameraProfile => cameraProfile;
    }
}
