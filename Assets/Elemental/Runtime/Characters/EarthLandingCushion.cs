using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DisallowMultipleComponent]
    public sealed class EarthLandingCushion : MonoBehaviour
    {
        private static readonly ProfilerMarker CushionMarker =
            new ProfilerMarker("Elemental.Bending.EarthLandingCushion");

        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private ActiveRagdollPuppet puppet;
        [SerializeField] private Collider planetCollider;
        [SerializeField] private EarthLandingCushionProfile profile;
        [SerializeField] private Transform cushionVisual;
        [SerializeField] private EarthSurfaceQueryService surfaceQueries;

        private bool _holding;
        private bool _cushioning;
        private float _retreatElapsed;
        private Vector3 _landingPoint;
        private Vector3 _landingUp;

        public bool IsHolding => _holding;
        public bool IsCushioning => _cushioning;
        public Vector3 PredictedLandingPoint => _landingPoint;
        public EarthLandingPrediction LastPrediction { get; private set; }
        public float LastLandingSpeed { get; private set; }
        public EarthSurfaceSample LastLandingSurface { get; private set; }

        public bool TryResolveLandingSurface(
            Vector3 origin,
            Vector3 predictedPlanetPoint,
            out EarthSurfaceSample sample)
        {
            sample = default;
            if (surfaceQueries == null) return false;
            Vector3 direction = predictedPlanetPoint - origin;
            float distance = direction.magnitude;
            if (distance <= 0.05f) return false;
            var query = new EarthSurfaceQuery(
                ToFloat3(origin),
                ToFloat3(direction / distance),
                distance + 1.5f,
                EarthSurfaceCapabilities.Support | EarthSurfaceCapabilities.LandingCushion,
                0.16f);
            return surfaceQueries.TrySample(in query, out sample);
        }

        public void Configure(
            Rigidbody body,
            PlanetMotor configuredMotor,
            ActiveRagdollPuppet configuredPuppet,
            Collider configuredPlanetCollider,
            EarthLandingCushionProfile configuredProfile,
            Transform configuredVisual,
            EarthSurfaceQueryService configuredSurfaceQueries = null)
        {
            targetBody = body;
            motor = configuredMotor;
            puppet = configuredPuppet;
            planetCollider = configuredPlanetCollider;
            profile = configuredProfile;
            cushionVisual = configuredVisual;
            surfaceQueries = configuredSurfaceQueries;
            if (cushionVisual != null) cushionVisual.gameObject.SetActive(false);
        }

        public bool BeginHold()
        {
            if (_holding || targetBody == null || motor == null || motor.IsGrounded ||
                (planetCollider == null && surfaceQueries == null))
                return false;
            Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : transform.up;
            if (Vector3.Dot(targetBody.linearVelocity, up) >= -0.15f) return false;
            _holding = true;
            _cushioning = false;
            _retreatElapsed = 0f;
            return true;
        }

        public void EndHold()
        {
            _holding = false;
            if (cushionVisual != null && cushionVisual.gameObject.activeSelf)
                _cushioning = true;
        }

        private void Awake()
        {
            if (targetBody == null) targetBody = GetComponent<Rigidbody>();
            if (motor == null) motor = GetComponent<PlanetMotor>();
            if (puppet == null) puppet = GetComponent<ActiveRagdollPuppet>();
        }

        private void FixedUpdate()
        {
            if (!_holding || targetBody == null || motor == null ||
                (planetCollider == null && surfaceQueries == null)) return;
            if (puppet == null) puppet = GetComponent<ActiveRagdollPuppet>();
            using (CushionMarker.Auto())
            {
                Vector3 center = planetCollider != null ? planetCollider.bounds.center : Vector3.zero;
                Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f
                    ? motor.LocalUp.normalized
                    : (targetBody.worldCenterOfMass - center).normalized;
                Vector3 closest = planetCollider != null
                    ? planetCollider.ClosestPoint(targetBody.worldCenterOfMass)
                    : targetBody.worldCenterOfMass - (up * 24f);
                float surfaceRadius = Mathf.Max(0.1f, Vector3.Distance(center, closest));
                LastPrediction = EarthLandingCushionSolver.Predict(
                    ToFloat3(targetBody.worldCenterOfMass),
                    ToFloat3(targetBody.linearVelocity),
                    ToFloat3(center),
                    surfaceRadius,
                    GravityMagnitude,
                    PredictionSeconds);
                if (!LastPrediction.Valid) return;
                _landingPoint = ToVector3(LastPrediction.SurfacePoint);
                _landingUp = (_landingPoint - center).normalized;
                LastLandingSurface = default;
                if (TryResolveLandingSurface(
                        targetBody.worldCenterOfMass, _landingPoint, out EarthSurfaceSample sample))
                {
                    LastLandingSurface = sample;
                    _landingPoint = ToVector3(sample.Point);
                    _landingUp = ToVector3(sample.Normal);
                }
                ShowPrediction();

                float clearance = Vector3.Dot(
                    targetBody.worldCenterOfMass - _landingPoint, _landingUp) - 1.05f;
                float currentUpSpeed = Vector3.Dot(targetBody.linearVelocity -
                    ToVector3(LastLandingSurface.Velocity), _landingUp);
                if (clearance <= ActivationHeight && currentUpSpeed < -MaximumLandingSpeed)
                {
                    float velocityChange = EarthLandingCushionSolver.RequiredUpwardVelocityChange(
                        currentUpSpeed,
                        MaximumLandingSpeed);
                    ApplyVelocityChange(_landingUp * velocityChange);
                    LastLandingSpeed = Mathf.Max(0f, -(currentUpSpeed + velocityChange));
                    _cushioning = true;
                    motor.BeginExternalLaunch(3);
                }

                if (_cushioning) CompressVisual(clearance);
                if (clearance > 0.22f && !motor.IsGrounded) return;
                LastLandingSpeed = Mathf.Max(0f, -Vector3.Dot(
                    targetBody.linearVelocity - ToVector3(LastLandingSurface.Velocity), _landingUp));
                _holding = false;
                _cushioning = true;
                _retreatElapsed = 0f;
            }
        }

        private void Update()
        {
            if (!_cushioning || _holding || cushionVisual == null || !cushionVisual.gameObject.activeSelf) return;
            _retreatElapsed += Time.deltaTime;
            float retreat = Mathf.Clamp01(_retreatElapsed / RetreatSeconds);
            Vector3 scale = cushionVisual.localScale;
            scale.y = Mathf.Max(0.01f, Mathf.Lerp(scale.y, 0f, retreat));
            cushionVisual.localScale = scale;
            cushionVisual.position -= _landingUp * (PillarHeight * Time.deltaTime / RetreatSeconds);
            if (retreat < 1f) return;
            cushionVisual.gameObject.SetActive(false);
            _cushioning = false;
        }

        private void ShowPrediction()
        {
            if (cushionVisual == null) return;
            cushionVisual.gameObject.SetActive(true);
            cushionVisual.SetPositionAndRotation(
                _landingPoint + (_landingUp * PillarHeight * 0.5f),
                Quaternion.FromToRotation(Vector3.up, _landingUp));
            if (!_cushioning)
                cushionVisual.localScale = new Vector3(PillarWidth, PillarHeight, PillarWidth);
        }

        private void CompressVisual(float clearance)
        {
            if (cushionVisual == null) return;
            float compression = 1f - Mathf.Clamp01(clearance / ActivationHeight);
            float height = Mathf.Lerp(PillarHeight, PillarHeight * 0.18f, compression);
            cushionVisual.localScale = new Vector3(PillarWidth, height, PillarWidth);
            cushionVisual.position = _landingPoint + (_landingUp * height * 0.5f);
        }

        private void ApplyVelocityChange(Vector3 velocityChange)
        {
            if (puppet != null) puppet.ApplyUniformVelocityChange(velocityChange);
            else targetBody.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        private float PredictionSeconds => profile != null ? profile.PredictionSeconds : 4f;
        private float ActivationHeight => profile != null ? profile.ActivationHeight : 3.2f;
        private float MaximumLandingSpeed => profile != null ? profile.MaximumLandingSpeed : 4f;
        private float PillarHeight => profile != null ? profile.PillarHeight : 2.4f;
        private float PillarWidth => profile != null ? profile.PillarWidth : 1.7f;
        private float RetreatSeconds => profile != null ? profile.RetreatSeconds : 0.42f;
        private float GravityMagnitude => profile != null ? profile.GravityMagnitude : 14f;
        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
