using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Hover Profile", fileName = "EarthHoverProfile")]
    public sealed class EarthHoverProfile : ScriptableObject
    {
        [Header("Angular control")]
        [SerializeField, Min(0f)] private float orientationStrength = 38f;
        [SerializeField, Min(0f)] private float angularDamping = 11f;
        [SerializeField, Min(0.05f)] private float maximumAngularSpeed = 0.85f;
        [SerializeField, Min(0f)] private float maximumAngularAcceleration = 45f;
        [Header("Living hover")]
        [SerializeField, Range(0f, 12f)] private float idleYawDegrees = 3.5f;
        [SerializeField, Min(0.05f)] private float idleYawFrequency = 0.45f;
        [SerializeField, Range(0f, 0.2f)] private float bobAmplitude = 0.055f;
        [SerializeField, Min(0.05f)] private float bobFrequency = 1.25f;

        public float OrientationStrength => orientationStrength;
        public float AngularDamping => angularDamping;
        public float MaximumAngularSpeed => maximumAngularSpeed;
        public float MaximumAngularAcceleration => maximumAngularAcceleration;
        public float IdleYawDegrees => idleYawDegrees;
        public float IdleYawFrequency => idleYawFrequency;
        public float BobAmplitude => bobAmplitude;
        public float BobFrequency => bobFrequency;
    }
}
