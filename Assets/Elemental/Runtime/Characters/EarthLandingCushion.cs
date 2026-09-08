using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
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
        [SerializeField] private EarthMaterialFeedbackHub materialFeedback;
        public void ConfigureMaterialFeedback(EarthMaterialFeedbackHub hub) => materialFeedback = hub;

        private bool _holding;
        private bool _cushioning;
        private float _retreatElapsed;
        private float _safeLandingUntil;
        private Vector3 _landingPoint;
        private Vector3 _landingUp;
        private bool _emergencePresented;
        private float _nextDustAt;
        private float _brakingAcceleration;
        private float _incomingSpeed;
        private float _fractureElapsed;
        private const int ChunkCount = 12;
        private readonly Transform[] _chunks = new Transform[ChunkCount];
        private readonly Mesh[] _chunkMeshes = new Mesh[ChunkCount];
        private readonly Vector3[] _chunkVelocities = new Vector3[ChunkCount];
        private readonly Vector3[] _chunkScales = new Vector3[ChunkCount];
        private readonly Vector3[] _chunkPositions = new Vector3[ChunkCount];
        private readonly Quaternion[] _chunkRotations = new Quaternion[ChunkCount];
        public bool HasFractured { get; private set; }
        public float IncomingLandingSpeed => _incomingSpeed;
        public int VisibleChunkCount => HasFractured && cushionVisual != null && cushionVisual.gameObject.activeSelf
            ? ChunkCount : 0;

        public bool IsHolding => _holding;
        public bool IsCushioning => _cushioning;
        public bool SuppressesHardLanding => (_holding && _cushioning) || Time.time <= _safeLandingUntil;
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
            PrepareStoneChunks();
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
            _emergencePresented = false;
            _nextDustAt = 0f;
            _incomingSpeed = 0f;
            _brakingAcceleration = 0f;
            HasFractured = false;
            ResetStoneChunks();
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
            PrepareStoneChunks();
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
                // Once contact owns the cushion, a stale/invalid ballistic
                // prediction cannot interrupt braking or strand its visual.
                if (!LastPrediction.Valid && !_cushioning) return;
                if (!_cushioning)
                {
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
                }
                else
                {
                    _landingPoint += ToVector3(LastLandingSurface.Velocity) * Time.fixedDeltaTime;
                    AlignCushionUnderFeet();
                }
                ShowPrediction();
                if (!_emergencePresented)
                {
                    _emergencePresented = true;
                    materialFeedback?.Emit(EarthMaterialFeedbackKind.Emerge, _landingPoint, _landingUp,
                        1f, PillarWidth * .5f, dustCount: 72, chipCount: 18);
                }

                float clearance = Vector3.Dot(motor.SupportFeetPoint(_landingUp) - _landingPoint, _landingUp);
                float currentUpSpeed = Vector3.Dot(targetBody.linearVelocity -
                    ToVector3(LastLandingSurface.Velocity), _landingUp);
                if (clearance <= Mathf.Min(ActivationHeight, PillarHeight + 0.08f) &&
                    currentUpSpeed < -MaximumLandingSpeed)
                {
                    if (!_cushioning)
                    {
                        AlignCushionUnderFeet();
                        _incomingSpeed = Mathf.Max(0f, -currentUpSpeed);
                        _brakingAcceleration = EarthLandingCushionSolver.BrakingAcceleration(
                            _incomingSpeed, clearance, MaximumLandingSpeed, CompressionSeconds);
                    }
                    float velocityChange = EarthLandingCushionSolver.CompressionVelocityChange(
                        currentUpSpeed, clearance, MaximumLandingSpeed, _brakingAcceleration,
                        GravityMagnitude, Time.fixedDeltaTime);
                    ApplyVelocityChange(_landingUp * velocityChange);
                    LastLandingSpeed = Mathf.Max(0f, -(currentUpSpeed + velocityChange));
                    _cushioning = true;
                    _safeLandingUntil = Time.time + 0.75f;
                }

                if (_cushioning)
                {
                    CompressVisual(clearance);
                    motor.SuppressLandingRoll(.9f);
                    if (Time.time >= _nextDustAt)
                    {
                        _nextDustAt = Time.time + .09f;
                        materialFeedback?.Emit(EarthMaterialFeedbackKind.Friction, _landingPoint, _landingUp,
                            1f, PillarWidth * .5f, dustCount: 20, chipCount: 4);
                    }
                }
                if (clearance > 0.22f && !motor.IsGrounded) return;
                LastLandingSpeed = Mathf.Max(0f, -Vector3.Dot(
                    targetBody.linearVelocity - ToVector3(LastLandingSurface.Velocity), _landingUp));
                _holding = false;
                _cushioning = true;
                _safeLandingUntil = Time.time + 0.75f;
                _retreatElapsed = 0f;
                motor.SuppressLandingRoll(.9f);
                materialFeedback?.Emit(EarthMaterialFeedbackKind.Land, _landingPoint, _landingUp,
                    1f, PillarWidth * .6f, dustCount: 80, chipCount: 20);
                if (_incomingSpeed >= (profile != null ? profile.FractureImpactSpeed : 12f))
                    FractureStoneChunks();
            }
        }

        private void Update()
        {
            if (!_cushioning || _holding || cushionVisual == null || !cushionVisual.gameObject.activeSelf) return;
            if (HasFractured)
            {
                UpdateFracturedChunks(Time.deltaTime);
                return;
            }
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
            float height = EarthLandingCushionSolver.CompressionHeight(clearance, PillarHeight);
            cushionVisual.localScale = new Vector3(PillarWidth, height, PillarWidth);
            cushionVisual.position = _landingPoint + (_landingUp * height * 0.5f);
        }

        private void AlignCushionUnderFeet()
        {
            // Braking lengthens flight time, so the old ballistic X/Z landing
            // prediction is no longer valid. Keep the cosmetic footprint under
            // the actual feet on the captured support plane; locomotion stays
            // authoritative and receives no lateral correction from the cushion.
            Vector3 feet = motor.SupportFeetPoint(_landingUp);
            _landingPoint += Vector3.ProjectOnPlane(feet - _landingPoint, _landingUp);
        }

        private void ApplyVelocityChange(Vector3 velocityChange)
        {
            if (puppet != null) puppet.ApplyUniformVelocityChange(velocityChange);
            else targetBody.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        private void PrepareStoneChunks()
        {
            if (!Application.isPlaying || cushionVisual == null || _chunks[0] != null) return;
            MeshRenderer original = cushionVisual.GetComponent<MeshRenderer>();
            Material stoneMaterial = original != null ? original.sharedMaterial : null;
            if (original != null) original.enabled = false;
            for (int index = 0; index < ChunkCount; index++)
            {
                var chunk = new GameObject($"Cushion stone {index + 1}");
                chunk.transform.SetParent(cushionVisual, false);
                _chunks[index] = chunk.transform;
                _chunkMeshes[index] = EarthWebWaveCellMeshFactory.Create(1201 + index);
                chunk.AddComponent<MeshFilter>().sharedMesh = _chunkMeshes[index];
                chunk.AddComponent<MeshRenderer>().sharedMaterial = stoneMaterial;
            }
            ResetStoneChunks();
        }

        private void ResetStoneChunks()
        {
            for (int index = 0; index < ChunkCount; index++)
            {
                Transform chunk = _chunks[index];
                if (chunk == null) continue;
                chunk.localPosition = new Vector3((index % 2 == 0 ? -1f : 1f) * 0.23f,
                    (index / 4 - 1) / 3f, ((index / 2) % 2 == 0 ? -1f : 1f) * 0.22f);
                chunk.localRotation = Quaternion.Euler((index % 3 - 1) * 4f,
                    index * 43f, (index % 2 == 0 ? -1f : 1f) * 3f);
                chunk.localScale = new Vector3(0.72f, 0.37f, 0.72f);
                chunk.gameObject.SetActive(true);
            }
        }

        private void FractureStoneChunks()
        {
            if (cushionVisual == null || _chunks[0] == null) return;
            HasFractured = true;
            _fractureElapsed = 0f;
            for (int index = 0; index < ChunkCount; index++)
            {
                _chunkPositions[index] = _chunks[index].position;
                _chunkRotations[index] = _chunks[index].rotation;
                Vector3 scale = _chunks[index].lossyScale;
                scale.y = Mathf.Max(0.22f, scale.y);
                _chunkScales[index] = scale;
            }
            cushionVisual.localScale = Vector3.one;
            Vector3 inherited = Vector3.ProjectOnPlane(targetBody.linearVelocity, _landingUp) * 0.22f;
            for (int index = 0; index < ChunkCount; index++)
            {
                Transform chunk = _chunks[index];
                chunk.SetPositionAndRotation(_chunkPositions[index], _chunkRotations[index]);
                chunk.localScale = _chunkScales[index];
                Vector3 outward = Vector3.ProjectOnPlane(chunk.position - cushionVisual.position, _landingUp);
                _chunkVelocities[index] = inherited + outward.normalized * (1.8f + index * 0.13f) +
                                          _landingUp * (1.4f + (index % 3) * 0.35f);
            }
            materialFeedback?.Emit(EarthMaterialFeedbackKind.Fracture, _landingPoint, _landingUp,
                1f, PillarWidth * 0.8f, dustCount: 220, chipCount: 64);
        }

        private void UpdateFracturedChunks(float deltaSeconds)
        {
            float previousElapsed = _fractureElapsed;
            _fractureElapsed += deltaSeconds;
            // A second small billow makes the collapse substantial without
            // demanding all particles from the shared budget in the contact frame.
            if (previousElapsed < .08f && _fractureElapsed >= .08f)
                materialFeedback?.Emit(EarthMaterialFeedbackKind.Fracture, _landingPoint, _landingUp,
                    .85f, PillarWidth, dustCount: 150, chipCount: 48);
            for (int index = 0; index < ChunkCount; index++)
            {
                Transform chunk = _chunks[index];
                if (chunk == null) continue;
                _chunkVelocities[index] -= _landingUp * (GravityMagnitude * deltaSeconds);
                Vector3 next = chunk.position + _chunkVelocities[index] * deltaSeconds;
                float height = Vector3.Dot(next - _landingPoint, _landingUp);
                if (height < 0.11f)
                {
                    next += _landingUp * (0.11f - height);
                    _chunkVelocities[index] = Vector3.ProjectOnPlane(_chunkVelocities[index], _landingUp) *
                                              Mathf.Exp(-7f * deltaSeconds);
                }
                chunk.position = next;
                if (height > 0.11f) chunk.Rotate(new Vector3(37f, 53f, 29f) * deltaSeconds, Space.Self);
                chunk.localScale = _chunkScales[index] * (1f - Mathf.Clamp01((_fractureElapsed - 1.2f) / 0.4f));
            }
            if (_fractureElapsed < 1.6f) return;
            cushionVisual.gameObject.SetActive(false);
            _cushioning = false;
        }

        private void OnDisable()
        {
            _holding = _cushioning = false;
            _safeLandingUntil = 0f;
            if (cushionVisual != null) cushionVisual.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            for (int index = 0; index < ChunkCount; index++)
            {
                if (_chunks[index] != null) Destroy(_chunks[index].gameObject);
                if (_chunkMeshes[index] != null) Destroy(_chunkMeshes[index]);
            }
        }

        private float PredictionSeconds => profile != null ? profile.PredictionSeconds : 4f;
        private float ActivationHeight => profile != null ? profile.ActivationHeight : 3.2f;
        private float MaximumLandingSpeed => profile != null ? profile.MaximumLandingSpeed : 4f;
        private float PillarHeight => profile != null ? profile.PillarHeight : 2.4f;
        private float PillarWidth => profile != null ? profile.PillarWidth : 1.7f;
        private float RetreatSeconds => profile != null ? profile.RetreatSeconds : 0.42f;
        private float CompressionSeconds => profile != null ? profile.CompressionSeconds : 0.28f;
        private float GravityMagnitude => profile != null ? profile.GravityMagnitude : 14f;
        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
