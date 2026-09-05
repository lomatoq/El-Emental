using UnityEngine;

namespace Elemental.Presentation.MotionMatching
{
    [CreateAssetMenu(fileName = "SurfaceMotionProfile", menuName = "Elemental/Animation/Surface Motion Profile")]
    public sealed class SurfaceMotionProfile : ScriptableObject
    {
        [SerializeField] private string surfaceId = "stone";
        [SerializeField, Range(0.5f, 1.5f)] private float strideScale = 1f;
        [SerializeField, Range(0f, 2f)] private float traction = 1f;
        [SerializeField, Range(0f, 1f)] private float caution;
        [SerializeField, Range(0f, 2f)] private float footstepIntensity = 1f;

        public string SurfaceId => string.IsNullOrWhiteSpace(surfaceId) ? "default" : surfaceId;
        public float StrideScale => Mathf.Clamp(strideScale, 0.5f, 1.5f);
        public float Traction => Mathf.Clamp(traction, 0f, 2f);
        public float Caution => Mathf.Clamp01(caution);
        public float FootstepIntensity => Mathf.Clamp(footstepIntensity, 0f, 2f);
    }

}
