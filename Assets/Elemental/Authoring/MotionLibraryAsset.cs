using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Authoring
{
    public enum MotionClipRole : byte
    {
        Idle,
        Start,
        Locomotion,
        Stop,
        Pivot,
        Recovery,
        Magic,
        Impact
    }

    public enum MotionSemantic : byte
    {
        Unspecified = 0,
        NeutralIdle,
        GuardedIdle,
        WalkForward,
        WalkBackward,
        RunForward,
        RunLeft,
        RunRight,
        PivotLeft,
        PivotRight,
        JumpStart,
        JumpLoop,
        SoftLand,
        HardLand,
        RecoverFront,
        RecoverBack,
        DodgeForward,
        DodgeBackward,
        DodgeLeft,
        DodgeRight,
        Gather,
        Pull,
        Push,
        Lift,
        Slam,
        Sustain,
        Release,
        LightImpact,
        MediumImpact,
        RunBackward
    }

    [Serializable]
    public sealed class MotionClipRecipe
    {
        public string stableId;
        public AnimationClip clip;
        public MotionClipRole role = MotionClipRole.Locomotion;
        public MotionSemantic semantic;
        [Min(0f)] public float nominalSpeed = 2f;
        [Range(-180f, 180f)] public float nominalYaw;
        [Range(-180f, 180f)] public float nominalDirection;
        [Range(0f, 1f)] public float contactStart;
        [Range(0f, 1f)] public float contactEnd = 1f;
        [Range(0f, 1f)] public float cancelStart = 0.15f;
        [Range(0f, 1f)] public float recoveryStart = 0.65f;
        public bool loop = true;
    }

    [Serializable]
    public sealed class MotionTransitionOverride
    {
        public MotionSemantic from;
        public MotionSemantic to;
        [Range(0.01f, 0.5f)] public float halfLifeSeconds = 0.10f;
        [Range(0f, 1f)] public float destinationStart01;
        public bool preserveGaitPhase;
    }

    [CreateAssetMenu(fileName = "EarthMotionLibrary", menuName = "Elemental/Animation/Motion Library")]
    public sealed class MotionLibraryAsset : ScriptableObject
    {
        [Tooltip("Humanoid prefab/model used to sample the clips. No source asset is copied into the database.")]
        public GameObject sourceRig;
        [Min(1f)] public float databaseRate = 30f;
        public List<MotionClipRecipe> clips = new();
        public List<MotionTransitionOverride> transitionOverrides = new();
    }
}
