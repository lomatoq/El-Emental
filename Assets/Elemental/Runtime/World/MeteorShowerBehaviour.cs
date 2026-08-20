using System;
using Elemental.Runtime.Physics;
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
        private const int MaximumPhysicalPool = 4;
        private const int MaximumApproachPool = 6;

        [SerializeField] private MeteorShowerProfile profile;
        [SerializeField] private VoxelPlanetBehaviour voxelPlanet;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private MagicExecutor magicExecutor;
        [SerializeField] private ParticleSystem distantStreaks;
        [SerializeField] private Material meteorMaterial;
        [SerializeField] private EarthPhysicsFeelProfile physicsFeelProfile;

        private readonly PhysicalMeteor[] _meteors = new PhysicalMeteor[MaximumPhysicalPool];
        private readonly ApproachMeteor[] _approaches = new ApproachMeteor[MaximumApproachPool];
        private readonly Collider[] _impactHits = new Collider[32];
        private uint _randomState = 0x4D455445u;
        private float _nextSpawnAt;
        private float _terrainWindowStarted;
        private int _terrainEditsInWindow;
        private uint _nextMeteorId = 1u;

        private sealed class ApproachMeteor
        {
            public GameObject GameObject;
            public Transform Transform;
            public TrailRenderer Trail;
            public Vector3 Start;
            public Vector3 End;
            public Vector3 StartTangent;
            public Vector3 EndTangent;
            public Vector3 SpinAxis;
            public Vector3 EndVelocity;
            public float Radius;
            public float Duration;
            public float StartedAt;
            public uint MeteorId;
            public bool Active;
        }

        public int ActivePhysicalCount { get; private set; }
        public int ActiveApproachCount { get; private set; }

        public bool SpawnForQa(Vector3 position, Vector3 velocity, float radius = 0.55f)
        {
            EnsurePool();
            return ActivatePhysical(_nextMeteorId++, position, velocity, Mathf.Max(0.1f, radius));
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
            ScheduleNext(true);
        }

        public void ConfigurePhysicsFeel(EarthPhysicsFeelProfile configuredProfile) =>
            physicsFeelProfile = configuredProfile;

        private void Awake()
        {
            EnsurePool();
            ConfigureDistantStreaks();
            ScheduleNext(true);
        }

        private void Update()
        {
            UpdateApproaches();
            if (profile == null || !profile.Enabled || Time.time < _nextSpawnAt) return;
            SpawnApproachMeteor();
            ScheduleNext(false);
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
                MeshFilter filter = meteorObject.GetComponent<MeshFilter>();
                if (filter != null) filter.sharedMesh = EarthWebWaveCellMeshFactory.Create(6200 + index * 47);
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
                    EarthPhysicsBodyClass.HeavyBlock);
                TrailRenderer trail = meteorObject.AddComponent<TrailRenderer>();
                trail.time = 0.95f;
                trail.startWidth = 0.34f;
                trail.endWidth = 0f;
                trail.minVertexDistance = 0.10f;
                trail.sharedMaterial = meteorMaterial;
                trail.emitting = false;
                PhysicalMeteor meteor = meteorObject.AddComponent<PhysicalMeteor>();
                meteor.Configure(this, body);
                meteorObject.SetActive(false);
                _meteors[index] = meteor;
            }

            for (int index = 0; index < _approaches.Length; index++)
            {
                if (_approaches[index] != null) continue;
                var approachObject = new GameObject($"Scaled Approach Meteor {index + 1:00}");
                approachObject.transform.SetParent(transform, false);
                MeshFilter filter = approachObject.AddComponent<MeshFilter>();
                filter.sharedMesh = EarthWebWaveCellMeshFactory.Create(7100 + index * 59);
                MeshRenderer renderer = approachObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = meteorMaterial;
                TrailRenderer trail = approachObject.AddComponent<TrailRenderer>();
                trail.time = 1.55f;
                trail.startWidth = 0.25f;
                trail.endWidth = 0f;
                trail.minVertexDistance = 0.22f;
                trail.sharedMaterial = meteorMaterial;
                trail.emitting = false;
                approachObject.SetActive(false);
                _approaches[index] = new ApproachMeteor
                {
                    GameObject = approachObject,
                    Transform = approachObject.transform,
                    Trail = trail
                };
            }
        }

        private void SpawnApproachMeteor()
        {
            if (profile == null || planetCenter == null || voxelPlanet == null) return;
            if (ActivePhysicalCount + ActiveApproachCount >= profile.MaximumPhysical + 2) return;
            ApproachMeteor approach = null;
            for (int index = 0; index < _approaches.Length; index++)
            {
                if (_approaches[index] == null || _approaches[index].Active) continue;
                approach = _approaches[index];
                break;
            }
            if (approach == null) return;

            Vector3 arrivalDirection = RandomUnitVector();
            float planetRadius = Mathf.Max(1f, voxelPlanet.Radius);
            Vector2 radiusRange = profile.RadiusRange;
            Vector2 speedRange = profile.SpeedRange;
            float roll = Next01();
            float radiusMultiplier = roll < 0.14f
                ? Mathf.Lerp(1.75f, 2.75f, Next01())
                : roll > 0.78f
                    ? Mathf.Lerp(0.42f, 0.72f, Next01())
                    : Mathf.Lerp(0.85f, 1.35f, Next01());
            float radius = Mathf.Clamp(
                Mathf.Lerp(radiusRange.x, radiusRange.y, Next01()) * radiusMultiplier,
                0.12f,
                2.45f);
            float speed = Mathf.Lerp(speedRange.x, speedRange.y, Next01());
            Vector3 tangent = Vector3.Cross(
                arrivalDirection,
                Mathf.Abs(arrivalDirection.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            tangent = Quaternion.AngleAxis(Mathf.Lerp(-65f, 65f, Next01()), arrivalDirection) * tangent;
            float grazing = Mathf.Lerp(-0.34f, 0.34f, Next01());
            Vector3 endVelocity = (-arrivalDirection + tangent * grazing).normalized * speed;
            Vector3 end = planetCenter.position + arrivalDirection *
                          (planetRadius + Mathf.Lerp(planetRadius * 1.18f, planetRadius * 1.85f, Next01()));
            float startDistance = planetRadius * Mathf.Lerp(8f, 14f, Next01());
            Vector3 start = planetCenter.position + arrivalDirection * startDistance -
                            tangent * planetRadius * Mathf.Lerp(1.5f, 4.5f, Next01());
            float duration = Mathf.Lerp(2.6f, 5.8f, Next01());
            Vector3 chord = end - start;

            approach.MeteorId = _nextMeteorId++;
            approach.Start = start;
            approach.End = end;
            approach.StartTangent = chord * Mathf.Lerp(0.72f, 1.08f, Next01()) +
                                    tangent * planetRadius * Mathf.Lerp(1.2f, 3.2f, Next01());
            approach.EndTangent = endVelocity * duration;
            approach.EndVelocity = endVelocity;
            approach.SpinAxis = RandomUnitVector();
            approach.Radius = radius;
            approach.Duration = duration;
            approach.StartedAt = Time.time;
            approach.Active = true;
            approach.Transform.position = start;
            approach.Transform.localScale = Vector3.one * radius * 2f;
            approach.GameObject.SetActive(true);
            approach.Trail.Clear();
            approach.Trail.startWidth = Mathf.Clamp(radius * 0.78f, 0.16f, 1.4f);
            approach.Trail.time = Mathf.Lerp(1.1f, 2.2f, Mathf.InverseLerp(0.12f, 2.45f, radius));
            approach.Trail.emitting = true;
            ActiveApproachCount++;
        }

        private void UpdateApproaches()
        {
            ActiveApproachCount = 0;
            for (int index = 0; index < _approaches.Length; index++)
            {
                ApproachMeteor approach = _approaches[index];
                if (approach == null || !approach.Active) continue;
                ActiveApproachCount++;
                float t = Mathf.Clamp01((Time.time - approach.StartedAt) / approach.Duration);
                approach.Transform.position = Hermite(
                    approach.Start,
                    approach.StartTangent,
                    approach.End,
                    approach.EndTangent,
                    t);
                float spinSpeed = Mathf.Lerp(55f, 190f,
                    Mathf.InverseLerp(0.12f, 2.45f, approach.Radius));
                approach.Transform.Rotate(approach.SpinAxis, spinSpeed * Time.deltaTime, Space.World);
                float heat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.88f, t));
                approach.Trail.startWidth = Mathf.Clamp(
                    approach.Radius * Mathf.Lerp(0.48f, 1.12f, heat), 0.12f, 1.8f);
                if (t < 1f) continue;

                Vector3 handoffPosition = approach.Transform.position;
                bool activated = ActivatePhysical(
                    approach.MeteorId,
                    handoffPosition,
                    approach.EndVelocity,
                    approach.Radius);
                if (!activated)
                {
                    // Keep the visible body for a brief extra pass instead of popping
                    // if the physical pool is temporarily saturated.
                    approach.StartedAt = Time.time - approach.Duration * 0.92f;
                    continue;
                }
                approach.Trail.emitting = false;
                approach.Active = false;
                approach.GameObject.SetActive(false);
                ActiveApproachCount--;
            }
        }

        private bool ActivatePhysical(uint id, Vector3 position, Vector3 velocity, float radius)
        {
            PhysicalMeteor meteor = null;
            for (int index = 0; index < _meteors.Length; index++)
            {
                if (_meteors[index] != null && !_meteors[index].gameObject.activeSelf)
                {
                    meteor = _meteors[index];
                    break;
                }
            }
            if (meteor == null) return false;
            float safeRadius = Mathf.Max(0.1f, radius);
            float density = profile != null ? profile.Density : 1800f;
            float volume = 4f / 3f * Mathf.PI * safeRadius * safeRadius * safeRadius;
            TrailRenderer trail = meteor.GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.Clear();
                trail.startWidth = Mathf.Clamp(safeRadius * 0.68f, 0.16f, 1.4f);
                trail.emitting = true;
            }
            meteor.Activate(id, position, safeRadius, volume * density, velocity);
            return true;
        }

        private void ConfigureDistantStreaks()
        {
            if (distantStreaks == null || profile == null) return;
            ParticleSystem.EmissionModule emission = distantStreaks.emission;
            emission.rateOverTime = profile.Enabled ? Mathf.Max(0.24f, profile.DistantRatePerSecond) : 0f;
            ParticleSystem.MainModule main = distantStreaks.main;
            main.maxParticles = Mathf.Max(96, profile.DistantPoolSize);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(18f, 46f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.22f);
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

        private void ScheduleNext(bool initial)
        {
            if (profile == null)
            {
                _nextSpawnAt = float.PositiveInfinity;
                return;
            }
            float minimum = initial ? 3.5f : Mathf.Min(profile.PhysicalIntervalMin, 18f);
            float maximum = initial ? 7.5f : Mathf.Min(profile.PhysicalIntervalMax, 32f);
            _nextSpawnAt = Time.time + Mathf.Lerp(minimum, Mathf.Max(minimum, maximum), Next01());
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
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _randomState = x == 0u ? 0x4D455445u : x;
            return (_randomState & 0x00FFFFFFu) / 16777215f;
        }

        private static Vector3 Hermite(
            Vector3 start,
            Vector3 startTangent,
            Vector3 end,
            Vector3 endTangent,
            float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            float h00 = 2f * t3 - 3f * t2 + 1f;
            float h10 = t3 - 2f * t2 + t;
            float h01 = -2f * t3 + 3f * t2;
            float h11 = t3 - t2;
            return h00 * start + h10 * startTangent + h01 * end + h11 * endTangent;
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }
}
