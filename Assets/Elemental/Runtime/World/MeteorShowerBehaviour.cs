using System;
using Elemental.Simulation.Magic;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.World
{
    [DisallowMultipleComponent]
    public sealed class MeteorShowerBehaviour : MonoBehaviour
    {
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("Elemental.Meteors.FixedTick");
        private const int MaximumPool = 4;

        [SerializeField] private MeteorShowerProfile profile;
        [SerializeField] private VoxelPlanetBehaviour voxelPlanet;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private MagicExecutor magicExecutor;
        [SerializeField] private ParticleSystem distantStreaks;
        [SerializeField] private Material meteorMaterial;
        [SerializeField] private Elemental.Runtime.Physics.EarthPhysicsFeelProfile physicsFeelProfile;

        private readonly PhysicalMeteor[] _meteors = new PhysicalMeteor[MaximumPool];
        private readonly Collider[] _impactHits = new Collider[32];
        private uint _randomState = 0x4D455445u;
        private float _nextSpawnAt;
        private float _terrainWindowStarted;
        private int _terrainEditsInWindow;
        private uint _nextMeteorId = 1u;

        public int ActivePhysicalCount { get; private set; }

        public bool SpawnForQa(Vector3 position, Vector3 velocity, float radius = 0.55f)
        {
            EnsurePool();
            PhysicalMeteor meteor = null;
            for (int index = 0; index < _meteors.Length; index++)
                if (_meteors[index] != null && !_meteors[index].gameObject.activeSelf) { meteor = _meteors[index]; break; }
            if (meteor == null) return false;
            float safeRadius = Mathf.Max(0.1f, radius);
            float density = profile != null ? profile.Density : 1800f;
            float volume = 4f / 3f * Mathf.PI * safeRadius * safeRadius * safeRadius;
            meteor.Activate(_nextMeteorId++, position, safeRadius, volume * density, velocity);
            return true;
        }

        public void Configure(
            MeteorShowerProfile configuredProfile,
            VoxelPlanetBehaviour planet,
            Transform center,
            MagicExecutor executor,
            Material material,
            ParticleSystem streaks)
        {
            profile = configuredProfile;
            voxelPlanet = planet;
            planetCenter = center;
            magicExecutor = executor;
            meteorMaterial = material;
            distantStreaks = streaks;
            EnsurePool();
            ConfigureDistantStreaks();
            ScheduleNext();
        }

        public void ConfigurePhysicsFeel(Elemental.Runtime.Physics.EarthPhysicsFeelProfile configuredProfile) =>
            physicsFeelProfile = configuredProfile;

        private void Awake()
        {
            EnsurePool();
            ConfigureDistantStreaks();
            ScheduleNext();
        }

        private void Update()
        {
            if (profile == null || !profile.Enabled || Time.time < _nextSpawnAt) return;
            SpawnPhysicalMeteor();
            ScheduleNext();
        }

        private void FixedUpdate()
        {
            if (planetCenter == null) return;
            using (TickMarker.Auto())
            {
                ActivePhysicalCount = 0;
                for (int index = 0; index < _meteors.Length; index++)
                {
                    PhysicalMeteor meteor = _meteors[index];
                    if (meteor == null || !meteor.gameObject.activeSelf) continue;
                    ActivePhysicalCount++;
                    Vector3 inward = planetCenter.position - meteor.Body.worldCenterOfMass;
                    if (inward.sqrMagnitude > 0.001f)
                        meteor.Body.AddForce(inward.normalized * 11.5f, ForceMode.Acceleration);
                }
            }
        }

        internal void ResolveImpact(PhysicalMeteor meteor, Collision collision)
        {
            if (meteor == null || collision.contactCount == 0 || !meteor.gameObject.activeSelf) return;
            ContactPoint contact = collision.GetContact(0);
            float speed = collision.relativeVelocity.magnitude;
            float impulse = collision.impulse.magnitude;
            float energy = 0.5f * meteor.Body.mass * speed * speed;
            Vector2 craterRange = profile != null ? profile.CraterRadiusRange : new Vector2(0.4f, 2f);
            float crater01 = Mathf.InverseLerp(250f, 18000f, energy);
            float craterRadius = Mathf.Lerp(craterRange.x, craterRange.y, crater01);
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                contact.point,
                craterRadius * 2.2f,
                _impactHits,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < hitCount; index++)
            {
                Rigidbody body = _impactHits[index] != null ? _impactHits[index].attachedRigidbody : null;
                if (body == null || body == meteor.Body || body.isKinematic) continue;
                Vector3 direction = body.worldCenterOfMass - contact.point;
                float distance = Mathf.Max(0.35f, direction.magnitude);
                body.AddForce(direction.normalized * (impulse / (1f + distance)), ForceMode.Impulse);
            }
            bool terrainEdited = TryConsumeTerrainEdit();
            if (terrainEdited && voxelPlanet != null)
            {
                Vector3 local = voxelPlanet.transform.InverseTransformPoint(contact.point);
                voxelPlanet.ApplySphereEdit(local, craterRadius, false);
            }
            uint tick = unchecked((uint)Time.frameCount);
            magicExecutor?.Events.Emit(new MeteorImpactEvent(
                tick,
                meteor.MeteorId,
                ToFloat3(contact.point),
                ToFloat3(contact.normal),
                meteor.Radius,
                impulse,
                terrainEdited ? craterRadius : 0f));
            magicExecutor?.Events.Emit(new EarthImpactEvent(
                tick,
                meteor.MeteorId,
                impulse,
                energy,
                meteor.Body.mass,
                speed,
                ToFloat3(contact.point),
                ToFloat3(contact.normal),
                EarthImpactMaterialKind.Meteor));
            meteor.Deactivate();
        }

        private void EnsurePool()
        {
            for (int index = 0; index < _meteors.Length; index++)
            {
                if (_meteors[index] != null) continue;
                GameObject meteorObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                meteorObject.name = $"Physical Meteor {index + 1:00}";
                meteorObject.transform.SetParent(transform, false);
                Renderer renderer = meteorObject.GetComponent<Renderer>();
                if (renderer != null && meteorMaterial != null) renderer.sharedMaterial = meteorMaterial;
                Rigidbody body = meteorObject.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.maxAngularVelocity = 24f;
                physicsFeelProfile?.Apply(
                    body,
                    meteorObject.GetComponent<Collider>(),
                    Elemental.Runtime.Physics.EarthPhysicsBodyClass.HeavyBlock);
                TrailRenderer trail = meteorObject.AddComponent<TrailRenderer>();
                trail.time = 0.65f;
                trail.startWidth = 0.24f;
                trail.endWidth = 0f;
                trail.minVertexDistance = 0.12f;
                trail.sharedMaterial = meteorMaterial;
                PhysicalMeteor meteor = meteorObject.AddComponent<PhysicalMeteor>();
                meteor.Configure(this, body);
                meteorObject.SetActive(false);
                _meteors[index] = meteor;
            }
        }

        private void SpawnPhysicalMeteor()
        {
            if (profile == null || planetCenter == null || ActivePhysicalCount >= profile.MaximumPhysical) return;
            PhysicalMeteor meteor = null;
            for (int index = 0; index < _meteors.Length; index++)
                if (_meteors[index] != null && !_meteors[index].gameObject.activeSelf) { meteor = _meteors[index]; break; }
            if (meteor == null) return;
            Vector3 direction = RandomUnitVector();
            if (voxelPlanet == null) return;
            float planetRadius = voxelPlanet.Radius;
            Vector2 radiusRange = profile.RadiusRange;
            Vector2 speedRange = profile.SpeedRange;
            float radius = Mathf.Lerp(radiusRange.x, radiusRange.y, Next01());
            float speed = Mathf.Lerp(speedRange.x, speedRange.y, Next01());
            Vector3 tangent = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 position = planetCenter.position + direction *
                               (planetRadius + Mathf.Lerp(planetRadius, planetRadius * 1.58f, Next01()));
            Vector3 velocity = -direction * speed + tangent * Mathf.Lerp(-6f, 6f, Next01());
            float volume = 4f / 3f * Mathf.PI * radius * radius * radius;
            meteor.Activate(_nextMeteorId++, position, radius, volume * profile.Density, velocity);
        }

        private void ConfigureDistantStreaks()
        {
            if (distantStreaks == null || profile == null) return;
            ParticleSystem.EmissionModule emission = distantStreaks.emission;
            emission.rateOverTime = profile.Enabled ? profile.DistantRatePerSecond : 0f;
            ParticleSystem.MainModule main = distantStreaks.main;
            main.maxParticles = profile.DistantPoolSize;
        }

        private bool TryConsumeTerrainEdit()
        {
            if (profile == null || profile.MaximumTerrainEditsPerSecond <= 0) return false;
            if (Time.time - _terrainWindowStarted >= 1f)
            {
                _terrainWindowStarted = Time.time;
                _terrainEditsInWindow = 0;
            }
            if (_terrainEditsInWindow >= profile.MaximumTerrainEditsPerSecond) return false;
            _terrainEditsInWindow++;
            return true;
        }

        private void ScheduleNext()
        {
            if (profile == null) { _nextSpawnAt = float.PositiveInfinity; return; }
            _nextSpawnAt = Time.time + Mathf.Lerp(profile.PhysicalIntervalMin, profile.PhysicalIntervalMax, Next01());
        }

        private Vector3 RandomUnitVector()
        {
            float y = Mathf.Lerp(-1f, 1f, Next01());
            float angle = Next01() * Mathf.PI * 2f;
            float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            return new Vector3(Mathf.Cos(angle) * radial, y, Mathf.Sin(angle) * radial);
        }

        private float Next01()
        {
            uint x = _randomState;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            _randomState = x == 0u ? 0x4D455445u : x;
            return (_randomState & 0x00FFFFFFu) / 16777215f;
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }

}
