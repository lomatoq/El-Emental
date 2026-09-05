using System;
using System.Collections;
using System.Collections.Generic;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Runtime.Physics
{
    /// <summary>Additional seeded planet dressing; never relocates or deletes authored objects.</summary>
    [DisallowMultipleComponent]
    public sealed class EarthPlanetRockScatter : MonoBehaviour
    {
        private static readonly ProfilerMarker Marker = new ProfilerMarker("Elemental.Earth.PlanetRockScatter");
        [SerializeField] private EarthPlanetRockScatterProfile profile;
        [SerializeField] private VoxelPlanetBehaviour planet;
        [SerializeField] private EarthSurfaceQueryService surfaces;
        [SerializeField] private GravityWorldBehaviour gravity;
        [SerializeField] private EarthRockDebrisPool debris;
        [SerializeField] private EarthMaterialFeedbackHub hub;
        [SerializeField] private Material material;
        [SerializeField] private Mesh[] visualMeshes = Array.Empty<Mesh>();
        [SerializeField] private Mesh[] colliderMeshes = Array.Empty<Mesh>();
        [SerializeField] private Bounds[] exclusionBounds = Array.Empty<Bounds>();
        private readonly Collider[] overlaps = new Collider[32];
        private readonly List<Mesh> ownedClusterMeshes = new List<Mesh>(128);
        private Vector4[] accepted;
        private int acceptedCount, largeCursor, mediumCursor, clusterCursor;
        private Transform generatedRoot;
        private Coroutine build;
        private bool initialized, completed;
        public int AcceptedLarge { get; private set; }
        public int AcceptedMedium { get; private set; }
        public int AcceptedClusters { get; private set; }
        public int AcceptedSmallStones { get; private set; }
        public int RejectedSurface { get; private set; }
        public int RejectedOverlap { get; private set; }
        public int RejectedSlots { get; private set; }
        public bool IsComplete => completed;

        public void Configure(EarthPlanetRockScatterProfile configuredProfile, VoxelPlanetBehaviour configuredPlanet,
            EarthSurfaceQueryService configuredSurfaces, GravityWorldBehaviour configuredGravity,
            EarthRockDebrisPool configuredDebris, EarthMaterialFeedbackHub configuredHub,
            Material configuredMaterial, Mesh[] configuredVisualMeshes, Mesh[] configuredColliderMeshes,
            Bounds[] configuredExclusionBounds = null)
        {
            if (initialized) return; // Idempotent: reconfiguration never duplicates an existing generation.
            profile = configuredProfile; planet = configuredPlanet; surfaces = configuredSurfaces;
            gravity = configuredGravity; debris = configuredDebris; hub = configuredHub; material = configuredMaterial;
            visualMeshes = configuredVisualMeshes != null ? (Mesh[])configuredVisualMeshes.Clone() : Array.Empty<Mesh>();
            colliderMeshes = configuredColliderMeshes != null ? (Mesh[])configuredColliderMeshes.Clone() : Array.Empty<Mesh>();
            exclusionBounds = configuredExclusionBounds != null ? (Bounds[])configuredExclusionBounds.Clone() : Array.Empty<Bounds>();
            if (Application.isPlaying && isActiveAndEnabled) Begin();
        }

        private void Start() => Begin();
        private void OnEnable() { if (initialized && !completed && build == null) build = StartCoroutine(Build()); }
        private void OnDisable() { if (build != null) StopCoroutine(build); build = null; }
        private void OnDestroy()
        {
            foreach (Mesh mesh in ownedClusterMeshes) if (mesh != null) Destroy(mesh);
            if (generatedRoot != null) Destroy(generatedRoot.gameObject);
        }

        private void Begin()
        {
            if (completed || build != null) return;
            if (!initialized)
            {
                if (profile == null || planet == null || surfaces == null || gravity == null || debris == null ||
                    hub == null || material == null || visualMeshes.Length == 0 || visualMeshes.Length != colliderMeshes.Length)
                {
                    Debug.LogError("Planet rock scatter needs an explicit profile, planet, surface service, gravity, debris, feedback, material and matched mesh arrays.", this);
                    return;
                }
                for (int i = 0; i < visualMeshes.Length; i++)
                    if (visualMeshes[i] == null || colliderMeshes[i] == null || !visualMeshes[i].isReadable)
                    {
                        Debug.LogError("Planet scatter needs readable visual meshes and valid cached convex collider meshes.", this);
                        return;
                    }
                accepted = new Vector4[profile.LargeCount + profile.MediumCount + profile.ClusterCount];
                var root = new GameObject("Additional Planet Rock Scatter");
                root.transform.SetParent(transform, false);
                generatedRoot = root.transform;
                initialized = true;
            }
            build = StartCoroutine(Build());
        }

        private IEnumerator Build()
        {
            float deadline = Time.realtimeSinceStartup + profile.StartupWaitSeconds;
            while (planet.State == null || surfaces.ProviderCount == 0 || !gravity.IsReady)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogError("Planet rock scatter timed out waiting for canonical terrain/surface/gravity readiness; no floating fallback was spawned.", this);
                    build = null;
                    yield break;
                }
                yield return null;
            }
            int frameObjects = 0;
            while (largeCursor < profile.LargeCount)
            {
                using (Marker.Auto()) SpawnGameplay(largeCursor++, profile.LargeCount, true);
                if (++frameObjects >= profile.GameplayObjectsPerFrame) { frameObjects = 0; yield return null; }
            }
            while (mediumCursor < profile.MediumCount)
            {
                using (Marker.Auto()) SpawnGameplay(mediumCursor++, profile.MediumCount, false);
                if (++frameObjects >= profile.GameplayObjectsPerFrame) { frameObjects = 0; yield return null; }
            }
            while (clusterCursor < profile.ClusterCount)
            {
                using (Marker.Auto()) SpawnCluster(clusterCursor++);
                yield return null; // One combined cosmetic cluster per frame; no bodies or colliders.
            }
            completed = true; build = null;
            Debug.Log($"Planet rock scatter seed {profile.Seed}: large {AcceptedLarge}/{profile.LargeCount}, medium {AcceptedMedium}/{profile.MediumCount}, clusters {AcceptedClusters}/{profile.ClusterCount} ({AcceptedSmallStones} cosmetic stones); rejected slots {RejectedSlots}, surface attempts {RejectedSurface}, overlap attempts {RejectedOverlap}.", this);
        }

        private void SpawnGameplay(int index, int count, bool large)
        {
            uint categorySeed = profile.Seed ^ (large ? 0xA13u : 0xBC71u);
            uint rng = Mix(categorySeed + (uint)index * 97u);
            int meshIndex = (int)(Next(ref rng) * visualMeshes.Length) % visualMeshes.Length;
            Mesh mesh = visualMeshes[meshIndex];
            Vector2 range = large ? profile.LargeDiameter : profile.MediumDiameter;
            float diameter = Mathf.Lerp(range.x, range.y, Next(ref rng));
            Vector3 scale = ScaleFor(mesh, diameter, ref rng);
            float radius = Vector3.Scale(mesh.bounds.extents, scale).magnitude;
            for (int attempt = 0; attempt < profile.PlacementAttempts; attempt++)
            {
                Vector3 direction = CandidateDirection(index, count, categorySeed, attempt);
                if (!TrySurface(direction, out EarthSurfaceSample sample)) continue;
                Vector3 normal = sample.Normal;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Next(ref rng) * 360f, 0f);
                EarthSurfacePlacementResult seat = EarthSurfacePlacementSolver.Solve(mesh, sample.Point, normal, rotation, scale, profile.SurfaceInset, sample.Handle);
                if (!seat.IsValid) { RejectedSurface++; continue; }
                Vector3 center = seat.RootPosition + rotation * Vector3.Scale(mesh.bounds.center, scale);
                if (!IsClear(center, radius, sample.Point, normal)) continue;
                var rock = new GameObject($"Planet {(large ? "Large" : "Medium")} Rock {index + 1:000}");
                rock.SetActive(false);
                rock.transform.SetParent(generatedRoot, false);
                rock.transform.SetPositionAndRotation(seat.RootPosition, rotation);
                rock.transform.localScale = scale;
                rock.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = rock.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                MeshCollider shape = rock.AddComponent<MeshCollider>();
                shape.convex = true; shape.sharedMesh = colliderMeshes[meshIndex];
                Rigidbody body = rock.AddComponent<Rigidbody>();
                body.useGravity = false; body.isKinematic = true;
                GravityBody radialGravity = rock.AddComponent<GravityBody>();
                radialGravity.Configure(gravity, body);
                EarthDestructibleDecorRock decor = rock.AddComponent<EarthDestructibleDecorRock>();
                uint stableId = 0xD4000000u | ((profile.Seed & 0x3FFFu) << 10) | (uint)(1 + index + (large ? 0 : 128));
                decor.Configure(stableId, body, shape, radialGravity, debris, diameter * .5f, large ? 1800f : 600f);
                decor.ConfigureMaterialFeedback(hub);
                rock.SetActive(true);
                Remember(center, radius);
                if (large) AcceptedLarge++; else AcceptedMedium++;
                return;
            }
            RejectedSlots++;
        }

        private void SpawnCluster(int index)
        {
            uint seed = profile.Seed ^ 0xCE170u;
            uint rng = Mix(seed + (uint)index * 97u);
            Vector3 anchor = default, normal = default;
            bool found = false;
            for (int attempt = 0; attempt < profile.PlacementAttempts; attempt++)
            {
                if (!TrySurface(CandidateDirection(index, profile.ClusterCount, seed, attempt), out EarthSurfaceSample sample)) continue;
                anchor = sample.Point; normal = sample.Normal;
                if (!IsClear(anchor + normal * .15f, profile.ClusterRadius, anchor, normal)) continue;
                found = true; break;
            }
            if (!found) { RejectedSlots++; return; }
            int desired = profile.ClusterMinimumStones + (int)(Next(ref rng) * (profile.ClusterMaximumStones - profile.ClusterMinimumStones + 1));
            var combine = new List<CombineInstance>(desired);
            Vector3 right = Vector3.Cross(normal, Mathf.Abs(normal.y) < .9f ? Vector3.up : Vector3.forward).normalized;
            Vector3 forward = Vector3.Cross(right, normal);
            Vector2 sizes = profile.SmallDiameter;
            for (int i = 0; i < desired; i++)
            {
                float angle = Next(ref rng) * Mathf.PI * 2f;
                Vector3 candidate = anchor + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * Mathf.Sqrt(Next(ref rng)) * profile.ClusterRadius;
                if (!TrySurface((candidate - planet.transform.position).normalized, out EarthSurfaceSample sample)) continue;
                int meshIndex = (int)(Next(ref rng) * visualMeshes.Length) % visualMeshes.Length;
                Mesh mesh = visualMeshes[meshIndex];
                Vector3 scale = ScaleFor(mesh, Mathf.Lerp(sizes.x, sizes.y, Next(ref rng)), ref rng);
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, sample.Normal) * Quaternion.Euler(0f, Next(ref rng) * 360f, 0f);
                EarthSurfacePlacementResult seat = EarthSurfacePlacementSolver.Solve(mesh, sample.Point, sample.Normal, rotation, scale, profile.SurfaceInset, sample.Handle);
                if (!seat.IsValid) { RejectedSurface++; continue; }
                combine.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(seat.RootPosition - anchor, rotation, scale) });
            }
            if (combine.Count < profile.ClusterMinimumStones) { RejectedSlots++; return; }
            var cluster = new GameObject($"Planet Cosmetic Rock Cluster {index + 1:000}");
            cluster.transform.SetParent(generatedRoot, false);
            cluster.transform.SetPositionAndRotation(anchor, Quaternion.identity);
            var combinedMesh = new Mesh { name = cluster.name, indexFormat = IndexFormat.UInt32 };
            combinedMesh.CombineMeshes(combine.ToArray(), true, true, false);
            combinedMesh.RecalculateBounds();
            ownedClusterMeshes.Add(combinedMesh);
            cluster.AddComponent<MeshFilter>().sharedMesh = combinedMesh;
            MeshRenderer renderer = cluster.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            Remember(anchor, profile.ClusterRadius);
            AcceptedClusters++; AcceptedSmallStones += combine.Count;
        }

        private bool TrySurface(Vector3 direction, out EarthSurfaceSample sample)
        {
            Vector3 origin = planet.transform.position + direction * (planet.Radius + 10f);
            var query = new EarthSurfaceQuery(origin, -direction, 24f, EarthSurfaceCapabilities.Support);
            if (!surfaces.TrySample(in query, out sample) || sample.Handle.Kind != EarthSurfaceKind.Planet ||
                Vector3.Dot(sample.Normal, direction) < .75f)
            { RejectedSurface++; return false; }
            return true;
        }

        private bool IsClear(Vector3 center, float radius, Vector3 surface, Vector3 normal)
        {
            float padded = radius + profile.Spacing;
            foreach (Bounds excluded in exclusionBounds)
                if ((excluded.ClosestPoint(center) - center).sqrMagnitude < padded * padded)
                { RejectedOverlap++; return false; }
            for (int i = 0; i < acceptedCount; i++)
            {
                Vector4 entry = accepted[i];
                float separation = padded + entry.w;
                if ((new Vector3(entry.x, entry.y, entry.z) - center).sqrMagnitude < separation * separation)
                { RejectedOverlap++; return false; }
            }
            int hits = UnityEngine.Physics.OverlapSphereNonAlloc(center, padded, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (hits == overlaps.Length) { RejectedOverlap++; return false; }
            for (int i = 0; i < hits; i++)
            {
                Collider other = overlaps[i];
                if (other == null) continue;
                // PhysX ClosestPoint is unsupported for non-convex MeshColliders.
                // Canonical planet chunks are already accounted for by the sampled
                // surface; any other non-convex authored collider stays an obstacle.
                if (other is MeshCollider terrainMesh && !terrainMesh.convex)
                {
                    if (other.transform.IsChildOf(planet.transform) && other.attachedRigidbody == null) continue;
                    RejectedOverlap++; return false;
                }
                // Ground itself is expected; anything protruding above the sampled plane
                // blocks placement, including pre-existing authored decor and moving bodies.
                Vector3 nearest = other.ClosestPoint(center);
                if (Vector3.Dot(nearest - surface, normal) <= .04f && other.attachedRigidbody == null) continue;
                RejectedOverlap++; return false;
            }
            return true;
        }

        private void Remember(Vector3 center, float radius)
        {
            if (acceptedCount < accepted.Length) accepted[acceptedCount++] = new Vector4(center.x, center.y, center.z, radius);
        }

        private static Vector3 ScaleFor(Mesh mesh, float diameter, ref uint seed)
        {
            Vector3 bounds = mesh.bounds.size;
            float unit = diameter / Mathf.Max(.001f, Mathf.Max(bounds.x, Mathf.Max(bounds.y, bounds.z)));
            return new Vector3(unit, unit * Mathf.Lerp(.65f, 1f, Next(ref seed)), unit * Mathf.Lerp(.75f, 1f, Next(ref seed)));
        }

        public static Vector3 DistributionDirection(int index, int count, uint seed)
        {
            if (count <= 0) return Vector3.up;
            float y = 1f - 2f * (index + .5f) / count;
            float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float angle = index * 2.39996323f;
            Quaternion rotation = Quaternion.Euler((Mix(seed) & 0xFFFFu) * (360f / 65536f),
                (Mix(seed + 1u) & 0xFFFFu) * (360f / 65536f), (Mix(seed + 2u) & 0xFFFFu) * (360f / 65536f));
            return rotation * new Vector3(Mathf.Cos(angle) * radial, y, Mathf.Sin(angle) * radial);
        }

        public static Vector3 CandidateDirection(int index, int count, uint seed, int attempt)
        {
            Vector3 direction = DistributionDirection(index, count, seed);
            if (attempt <= 0) return direction;
            // Retrying only a few degrees away repeatedly hit the same arena/spawn
            // exclusion. Keep the original whole-sphere stratum, but send retries
            // to deterministic distant candidates without relaxing any clearance.
            Vector3 tangent = Vector3.Cross(direction, Mathf.Abs(direction.y) < .9f ? Vector3.up : Vector3.forward).normalized;
            Vector3 bitangent = Vector3.Cross(direction, tangent).normalized;
            uint retrySeed = Mix(seed ^ ((uint)index * 0x9E3779B9u) ^ ((uint)attempt * 0x85ebca6bu));
            float azimuth = Next(ref retrySeed) * Mathf.PI * 2f;
            Vector3 axis = tangent * Mathf.Cos(azimuth) + bitangent * Mathf.Sin(azimuth);
            return Quaternion.AngleAxis(Mathf.Lerp(75f, 165f, Next(ref retrySeed)), axis) * direction;
        }
        private static uint Mix(uint x) { x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; return x ^ (x >> 16); }
        private static float Next(ref uint state) { state = Mix(state + 0x9E3779B9u); return (state & 0xFFFFFFu) / 16777216f; }
    }
}
