using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Input.Gestures
{
    [CreateAssetMenu(menuName = "Elemental/Input/Earth Gesture Profile", fileName = "EarthGestureProfile")]
    public sealed class EarthGestureProfile : ScriptableObject
    {
        [SerializeField, Range(16, 64)] private int resampleCount = 32;
        [SerializeField, Range(0f, 0.5f)] private float smoothing = 0.18f;
        [SerializeField, Range(0.002f, 0.2f)] private float minimumPathLength = 0.025f;
        [SerializeField, Range(0.02f, 0.5f)] private float closureRatio = 0.16f;
        [SerializeField, Range(0f, 1f)] private float minimumConfidence = 0.58f;
        [SerializeField, Range(0f, 0.5f)] private float minimumAmbiguityGap = 0.075f;
        [SerializeField] private EarthScrollDeviceProfile scrollDeviceProfile =
            EarthScrollDeviceProfile.DetentWheel;

        public EarthGestureSettings Settings => new EarthGestureSettings(
            resampleCount,
            smoothing,
            minimumPathLength,
            closureRatio,
            minimumConfidence,
            minimumAmbiguityGap);

        public EarthScrollDeviceProfile ScrollDeviceProfile => scrollDeviceProfile;
    }
}
