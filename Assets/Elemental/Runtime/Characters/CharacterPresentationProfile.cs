using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [CreateAssetMenu(menuName = "Elemental/Character/Presentation Profile", fileName = "CharacterPresentationProfile")]
    public sealed class CharacterPresentationProfile : ScriptableObject
    {
        [SerializeField] private GameObject humanoidPrefab;
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private Avatar avatar;
        [SerializeField] private Vector3 localPosition = new Vector3(0f, -1.05f, 0f);
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale = Vector3.one * 1.08f;
        [SerializeField, Range(0.01f, 0.5f)] private float locomotionBlendSeconds = 0.12f;
        [SerializeField, Range(0.01f, 0.5f)] private float castingBlendSeconds = 0.1f;
        [SerializeField, Range(0f, 1f)] private float handIkWeight = 0.92f;

        public GameObject HumanoidPrefab => humanoidPrefab;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public Avatar Avatar => avatar;
        public Vector3 LocalPosition => localPosition;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        public Vector3 LocalScale => localScale;
        public float LocomotionBlendSeconds => locomotionBlendSeconds;
        public float CastingBlendSeconds => castingBlendSeconds;
        public float HandIkWeight => handIkWeight;

        public void Configure(
            GameObject configuredPrefab,
            RuntimeAnimatorController configuredController,
            Avatar configuredAvatar,
            Vector3 configuredLocalPosition,
            Vector3 configuredLocalEulerAngles,
            Vector3 configuredLocalScale)
        {
            humanoidPrefab = configuredPrefab;
            animatorController = configuredController;
            avatar = configuredAvatar;
            localPosition = configuredLocalPosition;
            localEulerAngles = configuredLocalEulerAngles;
            localScale = configuredLocalScale;
        }
    }
}
