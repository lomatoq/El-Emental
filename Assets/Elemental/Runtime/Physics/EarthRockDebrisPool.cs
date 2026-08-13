using System.Collections.Generic;
using Elemental.Simulation.Structures;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthRockDebrisPool : MonoBehaviour
    {
        [SerializeField, Range(16, 128)] private int capacity = 72;
        [SerializeField] private Material material;
        [SerializeField] private Mesh mesh;
        [SerializeField] private Mesh[] meshVariants;
        [SerializeField] private GravityWorldBehaviour gravityWorld;
        [SerializeField] private EarthRockProfile profile;

        private readonly List<EarthRockDebris> _pieces = new List<EarthRockDebris>(72);
        private int _reuseCursor;
        private Mesh _fallbackMesh;

        public void Configure(
            int configuredCapacity,
            Material configuredMaterial,
            Mesh configuredMesh,
            GravityWorldBehaviour configuredGravityWorld,
            EarthRockProfile configuredProfile)
        {
            capacity = Mathf.Clamp(configuredCapacity, 16, 128);
            material = configuredMaterial;
            mesh = configuredMesh;
            meshVariants = configuredMesh != null ? new[] { configuredMesh } : null;
            gravityWorld = configuredGravityWorld;
            profile = configuredProfile;
        }

        public void ConfigureMeshVariants(params Mesh[] configuredMeshes)
        {
            meshVariants = configuredMeshes;
            if (mesh == null && configuredMeshes != null && configuredMeshes.Length > 0)
                mesh = configuredMeshes[0];
            for (int index = 0; index < _pieces.Count; index++)
                ApplyShape(_pieces[index].gameObject, ShapeForIndex(index));
        }

        private void Awake()
        {
            for (int index = 0; index < capacity; index++) CreatePiece();
        }

        public void EmitShatter(
            Vector3 position,
            Vector3 normal,
            Vector3 inheritedVelocity,
            float radius,
            float mass,
            uint seed)
        {
            int count = profile != null ? profile.ShatterPieceCount : 9;
            float spread = profile != null ? profile.ShatterSpreadSpeed : 3.8f;
            for (int index = 0; index < count; index++)
            {
                float weight = Mathf.Lerp(0.55f, 1.45f, Hash01(seed, index));
                float pieceRadius = radius * Mathf.Lerp(0.20f, 0.38f, weight / 1.45f);
                Vector3 direction = HashDirection(seed, index, normal);
                EarthRockDebris piece = Acquire();
                piece.BeginBallistic(
                    position + (direction * radius * 0.22f),
                    pieceRadius,
                    Mathf.Max(0.05f, mass * weight / count),
                    inheritedVelocity + (direction * spread * Mathf.Lerp(0.65f, 1.25f, Hash01(seed ^ 0x91u, index))),
                    profile);
            }
        }

        public void EmitAccretion(
            EarthFragment target,
            Vector3 surfacePoint,
            Vector3 localUp,
            float volume,
            uint seed)
        {
            int count = profile != null ? profile.AccretionChipCount : 4;
            for (int index = 0; index < count; index++)
            {
                Vector3 tangent = Vector3.Cross(localUp, HashDirection(seed, index, localUp)).normalized;
                Vector3 start = surfacePoint + (tangent * Mathf.Lerp(-0.28f, 0.28f, Hash01(seed ^ 0xA5u, index)));
                EarthRockDebris piece = Acquire();
                piece.BeginAccretion(
                    target,
                    start,
                    volume / count,
                    Mathf.Lerp(0.16f, 0.28f, Hash01(seed ^ 0xB7u, index)),
                    Mathf.Lerp(0.22f, 0.40f, Hash01(seed ^ 0xC9u, index)));
            }
        }

        private EarthRockDebris Acquire()
        {
            for (int index = 0; index < _pieces.Count; index++)
                if (!_pieces[index].gameObject.activeSelf) return _pieces[index];
            EarthRockDebris piece = _pieces[_reuseCursor];
            _reuseCursor = (_reuseCursor + 1) % _pieces.Count;
            piece.ResetPiece();
            return piece;
        }

        private EarthRockDebris CreatePiece()
        {
            GameObject go = new GameObject();
            go.name = $"Earth Rock Debris {_pieces.Count + 1:00}";
            go.transform.SetParent(transform, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            MeshCollider collider = go.AddComponent<MeshCollider>();
            Mesh shape = ShapeForIndex(_pieces.Count);
            filter.sharedMesh = shape;
            collider.sharedMesh = shape;
            collider.convex = true;
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            GravityBody gravity = go.AddComponent<GravityBody>();
            gravity.Configure(gravityWorld, body);
            EarthRockDebris piece = go.AddComponent<EarthRockDebris>();
            go.SetActive(false);
            _pieces.Add(piece);
            return piece;
        }

        private Mesh ShapeForIndex(int index)
        {
            if (meshVariants != null && meshVariants.Length > 0)
            {
                Mesh candidate = meshVariants[index % meshVariants.Length];
                if (candidate != null) return candidate;
            }
            if (mesh != null) return mesh;
            if (_fallbackMesh == null) _fallbackMesh = BuildFallbackDebrisMesh();
            return _fallbackMesh;
        }

        private static void ApplyShape(GameObject target, Mesh shape)
        {
            if (target == null || shape == null) return;
            MeshFilter filter = target.GetComponent<MeshFilter>();
            MeshCollider collider = target.GetComponent<MeshCollider>();
            if (filter != null) filter.sharedMesh = shape;
            if (collider == null) return;
            collider.sharedMesh = null;
            collider.sharedMesh = shape;
            collider.convex = true;
        }

        private static Mesh BuildFallbackDebrisMesh()
        {
            var fallback = new Mesh { name = "Irregular Earth Debris Fallback" };
            fallback.vertices = new[]
            {
                new Vector3(-0.48f, -0.34f, -0.38f),
                new Vector3( 0.46f, -0.29f, -0.43f),
                new Vector3( 0.39f, -0.41f,  0.45f),
                new Vector3(-0.43f, -0.31f,  0.36f),
                new Vector3(-0.34f,  0.42f, -0.31f),
                new Vector3( 0.37f,  0.34f, -0.28f),
                new Vector3( 0.31f,  0.39f,  0.37f),
                new Vector3(-0.39f,  0.31f,  0.43f)
            };
            fallback.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6,
                3, 0, 4, 3, 4, 7
            };
            fallback.RecalculateNormals();
            fallback.RecalculateBounds();
            return fallback;
        }

        private void OnDestroy()
        {
            if (_fallbackMesh == null) return;
            if (Application.isPlaying) Destroy(_fallbackMesh);
            else DestroyImmediate(_fallbackMesh);
            _fallbackMesh = null;
        }

        private static float Hash01(uint seed, int index)
        {
            uint value = seed ^ ((uint)(index + 1) * 0x9E3779B9u);
            value ^= value >> 16; value *= 0x7FEB352Du;
            value ^= value >> 15; value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private static Vector3 HashDirection(uint seed, int index, Vector3 preferredNormal)
        {
            Vector3 random = new Vector3(
                (Hash01(seed ^ 0x11u, index) * 2f) - 1f,
                (Hash01(seed ^ 0x33u, index) * 2f) - 1f,
                (Hash01(seed ^ 0x55u, index) * 2f) - 1f).normalized;
            Vector3 normal = preferredNormal.sqrMagnitude > 0.01f ? preferredNormal.normalized : Vector3.up;
            return (random + (normal * 0.65f)).normalized;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class EarthRockDebris : MonoBehaviour
    {
        private Rigidbody _body;
        private Collider _collider;
        private EarthRockProfile _profile;
        private EarthFragment _target;
        private Vector3 _start;
        private float _volume;
        private float _travelSeconds;
        private float _elapsed;
        private float _restSeconds;
        private float _shrinkSeconds;
        private Vector3 _fullScale;
        private bool _accreting;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
        }

        public void BeginBallistic(
            Vector3 position,
            float radius,
            float mass,
            Vector3 velocity,
            EarthRockProfile profile)
        {
            Resolve();
            _profile = profile;
            _target = null;
            _accreting = false;
            _elapsed = 0f;
            _restSeconds = profile != null ? profile.DebrisRestSeconds : 1.15f;
            _shrinkSeconds = profile != null ? profile.DebrisShrinkSeconds : 0.9f;
            transform.position = position;
            transform.localScale = Vector3.one * (radius * 2f);
            _fullScale = transform.localScale;
            gameObject.SetActive(true);
            _collider.enabled = true;
            _body.isKinematic = false;
            _body.detectCollisions = true;
            _body.mass = mass;
            _body.linearVelocity = velocity;
            _body.angularVelocity = velocity.sqrMagnitude > 0.01f
                ? Vector3.Cross(velocity.normalized, transform.up) * 4f
                : Vector3.zero;
        }

        public void BeginAccretion(
            EarthFragment target,
            Vector3 start,
            float volume,
            float visualRadius,
            float travelSeconds)
        {
            Resolve();
            _target = target;
            _start = start;
            _volume = volume;
            _travelSeconds = Mathf.Max(0.08f, travelSeconds);
            _elapsed = 0f;
            _accreting = true;
            transform.position = start;
            transform.localScale = Vector3.one * visualRadius;
            _fullScale = transform.localScale;
            _body.isKinematic = true;
            _body.detectCollisions = false;
            _collider.enabled = false;
            gameObject.SetActive(true);
        }

        public void ResetPiece()
        {
            Resolve();
            _target = null;
            _accreting = false;
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
            _body.isKinematic = true;
            _body.detectCollisions = false;
            _collider.enabled = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_accreting)
            {
                UpdateAccretion();
                return;
            }
            _elapsed += Time.deltaTime;
            DynamicDebrisLifecycleSample lifecycle = DynamicDebrisLifecycle.Evaluate(
                _elapsed,
                _restSeconds,
                _shrinkSeconds);
            if (!lifecycle.Shrinking) return;
            if (lifecycle.Complete)
            {
                ResetPiece();
                return;
            }
            // Shrink is purely visual. Keep the body dynamic and colliding so the
            // shard continues its fall, bounces and carries inherited momentum.
            _body.isKinematic = false;
            _body.detectCollisions = true;
            _collider.enabled = true;
            _body.WakeUp();
            transform.localScale = _fullScale * Mathf.Max(0.0125f, lifecycle.Scale01);
        }

        private void UpdateAccretion()
        {
            if (_target == null || !_target.gameObject.activeSelf)
            {
                ResetPiece();
                return;
            }
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _travelSeconds);
            float eased = t * t * (3f - (2f * t));
            Vector3 targetPosition = _target.transform.position;
            Vector3 arc = Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.22f);
            transform.position = Vector3.Lerp(_start, targetPosition, eased) + arc;
            transform.localScale = _fullScale * Mathf.Lerp(1f, 0.35f, eased);
            if (t < 1f) return;
            _target.AccreteVolume(_volume);
            ResetPiece();
        }

        private void Resolve()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (_collider == null) _collider = GetComponent<Collider>();
        }
    }
}
