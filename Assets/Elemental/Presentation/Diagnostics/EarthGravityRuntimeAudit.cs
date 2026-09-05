using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using UnityEngine;

namespace Elemental.Presentation.Diagnostics
{
    /// <summary>Allocation-free live proof of radial gravity and support wiring.</summary>
    [DisallowMultipleComponent]
    public sealed class EarthGravityRuntimeAudit : MonoBehaviour
    {
        [SerializeField] private GravityBody gravityBody;
        [SerializeField] private PlanetMotor motor;

        public bool GravitySourceReady { get; private set; }
        public bool UsesUnityGravity { get; private set; }
        public Vector3 Acceleration { get; private set; }
        public float AccelerationMagnitude { get; private set; }
        public Vector3 LocalUp { get; private set; }
        public bool Grounded { get; private set; }
        public uint SupportId { get; private set; }
        public uint SupportGeneration { get; private set; }
        public bool AirborneAccelerationValid { get; private set; }

        public void Configure(GravityBody configuredGravityBody, PlanetMotor configuredMotor)
        {
            gravityBody = configuredGravityBody;
            motor = configuredMotor;
        }

        private void Awake()
        {
            if (gravityBody == null) gravityBody = GetComponent<GravityBody>();
            if (motor == null) motor = GetComponent<PlanetMotor>();
        }

        private void LateUpdate()
        {
            Rigidbody body = gravityBody != null ? gravityBody.TargetBody : null;
            GravitySourceReady = gravityBody != null && gravityBody.IsOperational;
            UsesUnityGravity = body != null && body.useGravity;
            Acceleration = gravityBody != null ? gravityBody.LastAcceleration : Vector3.zero;
            AccelerationMagnitude = Acceleration.magnitude;
            LocalUp = motor != null && motor.LocalUp.sqrMagnitude > 0.5f
                ? motor.LocalUp.normalized
                : transform.up;
            Grounded = motor != null && motor.HasStableSupport;
            if (motor != null && motor.GroundSupport.HasSupport)
            {
                SupportId = motor.GroundSupport.Candidate.SurfaceId;
                SupportGeneration = motor.GroundSupport.Candidate.Generation;
            }
            else
            {
                SupportId = motor != null ? motor.MovingSurfaceId : 0u;
                SupportGeneration = motor != null ? motor.MovingSurfaceGeneration : 0u;
            }
            AirborneAccelerationValid = Grounded ||
                (AccelerationMagnitude > 0.1f &&
                 Vector3.Dot(Acceleration.normalized, -LocalUp) > 0.8f);
        }
    }
}
