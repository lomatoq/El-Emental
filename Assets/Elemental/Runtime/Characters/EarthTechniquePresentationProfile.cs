using System;
using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [Serializable]
    public struct EarthTechniquePresentation
    {
        [SerializeField] private EarthTechniqueKind technique;
        [Header("Timing")]
        [SerializeField, Min(0f)] private float anticipationSeconds;
        [SerializeField, Min(0f)] private float releaseSeconds;
        [SerializeField, Min(0f)] private float impactSeconds;
        [SerializeField, Min(0f)] private float settleSeconds;
        [Header("Pose")]
        [SerializeField, Range(0f, 1f)] private float poseEffort;
        [SerializeField, Range(0f, 1f)] private float braceAmount;
        [Header("Camera")]
        [SerializeField, Min(0f)] private float cameraImpulse;
        [SerializeField, Min(0f)] private float cameraLookAhead;
        [Header("Feedback")]
        [SerializeField, Range(0f, 2f)] private float dustAmount;
        [SerializeField, Range(0f, 2f)] private float chipAmount;
        [SerializeField, Range(0f, 1f)] private float rumbleAmount;

        public EarthTechniqueKind Technique => technique;
        public EarthTechniqueTiming Timing => new EarthTechniqueTiming(
            anticipationSeconds, releaseSeconds, impactSeconds, settleSeconds);
        public float PoseEffort => poseEffort;
        public float BraceAmount => braceAmount;
        public float CameraImpulse => cameraImpulse;
        public float CameraLookAhead => cameraLookAhead;
        public float DustAmount => dustAmount;
        public float ChipAmount => chipAmount;
        public float RumbleAmount => rumbleAmount;

        public static EarthTechniquePresentation Default(EarthTechniqueKind technique)
        {
            float weight = technique == EarthTechniqueKind.GroundWave || technique == EarthTechniqueKind.Repair
                ? 1f
                : technique == EarthTechniqueKind.Wall || technique == EarthTechniqueKind.Platform ? 0.82f : 0.65f;
            return new EarthTechniquePresentation
            {
                technique = technique,
                anticipationSeconds = Mathf.Lerp(0.10f, 0.26f, weight),
                releaseSeconds = Mathf.Lerp(0.06f, 0.14f, weight),
                impactSeconds = Mathf.Lerp(0.08f, 0.18f, weight),
                settleSeconds = Mathf.Lerp(0.18f, 0.42f, weight),
                poseEffort = weight,
                braceAmount = Mathf.Clamp01(weight * 0.85f),
                cameraImpulse = Mathf.Lerp(0.12f, 0.55f, weight),
                cameraLookAhead = Mathf.Lerp(0.25f, 1.25f, weight),
                dustAmount = Mathf.Lerp(0.4f, 1.25f, weight),
                chipAmount = Mathf.Lerp(0.25f, 0.9f, weight),
                rumbleAmount = Mathf.Lerp(0.18f, 0.72f, weight)
            };
        }
    }

    [CreateAssetMenu(
        menuName = "Elemental/Magic/Earth Technique Presentation Profile",
        fileName = "EarthTechniquePresentationProfile")]
    public sealed class EarthTechniquePresentationProfile : ScriptableObject
    {
        [SerializeField] private EarthTechniquePresentation[] techniques =
        {
            EarthTechniquePresentation.Default(EarthTechniqueKind.Grip),
            EarthTechniquePresentation.Default(EarthTechniqueKind.Wall),
            EarthTechniquePresentation.Default(EarthTechniqueKind.Platform),
            EarthTechniquePresentation.Default(EarthTechniqueKind.Pillar),
            EarthTechniquePresentation.Default(EarthTechniqueKind.GroundWave),
            EarthTechniquePresentation.Default(EarthTechniqueKind.Repair)
        };

        public int Count => techniques != null ? techniques.Length : 0;

        public bool TryGet(EarthTechniqueKind technique, out EarthTechniquePresentation presentation)
        {
            if (techniques != null)
            {
                for (int index = 0; index < techniques.Length; index++)
                {
                    if (techniques[index].Technique != technique) continue;
                    presentation = techniques[index];
                    return true;
                }
            }
            presentation = default;
            return false;
        }

        private void OnValidate()
        {
            if (techniques == null || techniques.Length != 6)
            {
                techniques = new EarthTechniquePresentation[6];
                for (int index = 0; index < techniques.Length; index++)
                    techniques[index] = EarthTechniquePresentation.Default((EarthTechniqueKind)(index + 1));
                return;
            }

            for (int index = 0; index < techniques.Length; index++)
            {
                EarthTechniqueKind expected = (EarthTechniqueKind)(index + 1);
                if (techniques[index].Technique == expected) continue;
                techniques[index] = EarthTechniquePresentation.Default(expected);
            }
        }
    }
}
