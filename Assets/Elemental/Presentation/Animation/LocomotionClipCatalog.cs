using System;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [CreateAssetMenu(menuName = "Elemental/Locomotion Clip Catalog")]
    public sealed class LocomotionClipCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public AnimationClip Clip;
            public float NominalSpeed;
            public float CycleSeconds;
            public AnimationCurve LeftContact, RightContact;
            public Vector2 BlendPosition;
            public string SourceQueryTag;
            public string Measurement;
        }
        public Entry[] Entries = Array.Empty<Entry>();
        public float ReferenceLegLengthMeters;
        public static float MeasureLegLength(Animator animator)
        {
            if (animator == null || !animator.isHuman) return 0f;
            Transform thigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform shin = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            return thigh != null && shin != null && foot != null
                ? Vector3.Distance(thigh.position, shin.position) + Vector3.Distance(shin.position, foot.position) : 0f;
        }
        public Entry Find(AnimationClip clip)
        {
            for (int i = 0; i < Entries.Length; i++) if (Entries[i].Clip == clip) return Entries[i];
            return null;
        }
    }
}
