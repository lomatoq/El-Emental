using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [CreateAssetMenu(menuName = "Elemental/Character/Planet Motor Feel Profile", fileName = "PlanetMotorFeelProfile")]
    public sealed class PlanetMotorFeelProfile : ScriptableObject
    {
        [Header("Ground locomotion")]
        [SerializeField, Min(0.1f)] private float maximumGroundSpeed = 7.2f;
        [SerializeField, Min(0.1f)] private float acceleration = 48f;
        [SerializeField, Min(0.1f)] private float deceleration = 62f;
        [SerializeField, Range(0f, 1f)] private float airControl = 0.32f;
        [SerializeField, Min(0.1f)] private float jumpSpeed = 8f;
        [SerializeField, Min(10f)] private float turnResponseDegrees = 170f;
        [Header("Forgiveness")]
        [SerializeField, Range(0f, 0.3f)] private float coyoteSeconds = 0.12f;
        [SerializeField, Range(0f, 0.3f)] private float jumpBufferSeconds = 0.14f;
        [SerializeField, Range(0f, 1f)] private float castSpeedMultiplier = 0.36f;
        [SerializeField, Range(0f, 1f)] private float braceSpeedMultiplier = 0.18f;
        [Header("Surface")]
        [SerializeField, Range(1f, 89f)] private float maximumSlopeAngle = 55f;
        [SerializeField, Range(0f, 2f)] private float tractionMultiplier = 1f;
        [SerializeField, Min(0f)] private float groundSnapSpeed = 3.5f;

        public float MaximumGroundSpeed => maximumGroundSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float AirControl => airControl;
        public float JumpSpeed => jumpSpeed;
        public float TurnResponseDegrees => turnResponseDegrees;
        public float CoyoteSeconds => coyoteSeconds;
        public float JumpBufferSeconds => jumpBufferSeconds;
        public float CastSpeedMultiplier => castSpeedMultiplier;
        public float BraceSpeedMultiplier => braceSpeedMultiplier;
        public float MaximumSlopeAngle => maximumSlopeAngle;
        public float TractionMultiplier => tractionMultiplier;
        public float GroundSnapSpeed => groundSnapSpeed;
    }
}
