using System.Collections.Generic;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.World;
using Elemental.Runtime.Matter;
using Elemental.Simulation.Matter;
using Unity.Mathematics;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Structures;
using UnityEngine;
using Unity.Profiling;

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
        [SerializeField] private EarthShapeGrammarProfile shapeGrammarProfile;
        [SerializeField] private EarthMaterialFeedbackHub materialFeedback;
        [SerializeField] private EarthMatterKernelBehaviour matterKernel;
        [SerializeField] private EarthStoneBevelProfile stoneBevelProfile;
        private readonly EarthStoneRenderBevelCache _renderBevels = new();
        public int RejectedBreakCount { get; private set; }
        public string LastBreakRejection { get; private set; } = "None";
        public void ConfigureMatterKernel(EarthMatterKernelBehaviour kernel) => matterKernel = kernel;
        public EarthRockBreakDecision ResolveBreak(float radius, float mass, float impulse,
            bool controlled = false, int depth = 0) => profile != null
                ? profile.ResolveBreak(radius, mass, impulse, controlled, depth)
                : EarthRockBreakPolicy.Resolve(radius, mass, impulse, controlled, depth);
        public EarthMaterialFeedbackHub MaterialFeedback => materialFeedback;
        public void ConfigureMaterialFeedback(EarthMaterialFeedbackHub hub) => materialFeedback = hub;

        private readonly List<EarthRockDebris> _pieces = new List<EarthRockDebris>(72);
        private Mesh _fallbackMesh;
        private Mesh[] _runtimeShapeVariants;
        private readonly EarthRockDebris[] _breakPieces = new EarthRockDebris[4];
        private readonly EarthMatterRecord[] _childRecords = new EarthMatterRecord[4];
        private readonly EarthMatterId[] _childIds = new EarthMatterId[4];
        private readonly float[] _childRadii = new float[4];
        private readonly EarthConvexFragmentCache _convexCells = new();
        private static readonly ProfilerMarker BakedCacheLoadMarker = new("Elemental.Earth.Fracture.LoadBakedCache");
        [SerializeField] private EarthConvexFractureCacheAsset bakedFractureCache;
        private bool _bakedCacheLoaded;
        private bool _awakeComplete;
        private bool _startComplete;
        [SerializeField, Min(0.1f)] private float startupCookingBudgetMilliseconds = 3f;
        public bool PhysicsPrepared => _awakeComplete && _startComplete && _convexCells.PendingCookingCount == 0;
        public int PendingBakedCookingCount => _convexCells.PendingCookingCount;
        public int CookedBakedMeshCount => _convexCells.CookedBakedMeshCount;
        public int ScheduledBakedMeshCount => _convexCells.ScheduledBakedMeshCount;
        public bool BackgroundCookingActive => _convexCells.BackgroundCookingActive;
        public double BackgroundCookingWallMilliseconds => _convexCells.BackgroundCookingWallMilliseconds;
        public double PeakStartupCookingMilliseconds => _convexCells.PeakCookingSliceMilliseconds;
        public int BakedFracturePlanCount => _convexCells.BakedPlanCount;
        public int RejectedBakedFracturePlanCount => _convexCells.BakedRejectedPlanCount;
        public int BakedFracturePlanMissCount => _convexCells.BakedPlanMissCount;
        public double BakedCacheLoadMilliseconds { get; private set; }
        public void ConfigureBakedFractureCache(EarthConvexFractureCacheAsset asset)
        {
            if (Application.isPlaying && _awakeComplete && bakedFractureCache != asset)
                throw new System.InvalidOperationException("Configure fracture cache before activating the debris pool.");
            bakedFractureCache = asset;
            _bakedCacheLoaded = false;
        }
        private void EnsureBakedCache()
        {
            if (_bakedCacheLoaded) return;
            _bakedCacheLoaded = true;
            double started = Time.realtimeSinceStartupAsDouble;
            using (BakedCacheLoadMarker.Auto()) _convexCells.LoadBaked(bakedFractureCache);
            BakedCacheLoadMilliseconds = (Time.realtimeSinceStartupAsDouble - started) * 1000.0;
        }
        public int PreparedFracturePlanCount => _convexCells.PreparationCount;
        public int PreparedFractureMeshCount => _convexCells.OwnedMeshCount;
        private static readonly ProfilerMarker ContainedSplitMarker = new("Elemental.Earth.Fracture.ContainedChildren");

        /// <summary>Explicit cold preparation at source creation. Impacts only bind cached meshes.</summary>
        public void PrepareFracture(Collider source, int forcedCount = 0)
        {
            if (source == null) return;
            EnsureBakedCache();
            Mesh sourceMesh = _convexCells.SourceMesh(source);
            float volumeScale = Mathf.Abs(source.transform.localToWorldMatrix.determinant);
            int first = forcedCount > 0 ? forcedCount : 3, last = forcedCount > 0 ? forcedCount : 4;
            for (int count = first; count <= last; count++)
            {
                var children = _convexCells.Get(sourceMesh, count);
                foreach (var child in children)
                {
                    float radius = Mathf.Pow(child.Volume * volumeScale * .2387324f, 1f / 3f);
                    int nextCount = ResolveBreak(radius, 1f, 100000f, false, 1).PhysicalPieces;
                    if (nextCount > 0) _convexCells.Get(child.ColliderMesh, nextCount);
                }
            }
        }

        public Mesh ResolveShapeVariant(int stableIndex) { EnsureRuntimeShapeLibrary(); return ShapeForIndex(stableIndex); }
        public Material StoneMaterial => material;

        /// <summary>Appends stable authored sources which the startup baker must cover.</summary>
        public int AppendAuthoredFractureSources(List<Mesh> destination)
        {
            if (destination == null) throw new System.ArgumentNullException(nameof(destination));
            int before = destination.Count;
            if (meshVariants != null)
                for (int index = 0; index < meshVariants.Length; index++)
                    AppendUnique(destination, meshVariants[index]);
            AppendUnique(destination, mesh);
            return destination.Count - before;
        }

        private static void AppendUnique(List<Mesh> destination, Mesh candidate)
        {
            if (candidate != null && !destination.Contains(candidate)) destination.Add(candidate);
        }

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
            {
                ApplyShape(_pieces[index].gameObject, ShapeForIndex(index));
                _pieces[index].CacheTemplateShapes();
            }
        }

        public void ConfigureShapeGrammar(EarthShapeGrammarProfile configuredProfile) =>
            shapeGrammarProfile = configuredProfile;

        private void Awake()
        {
            EnsureBakedCache();
            EnsureRuntimeShapeLibrary();
            // All split shells cook before play: collisions never create/cook bodies.
            int warmCount = capacity;
            for (int index = 0; index < warmCount; index++) CreatePiece();
            _awakeComplete = true;
        }

        private void Start() => _startComplete = true;
        private void Update()
        {
            if (_convexCells.PendingCookingCount > 0)
                _convexCells.PrepareBakedPhysics(startupCookingBudgetMilliseconds);
        }

        public void EmitShatter(
            Vector3 position,
            Vector3 normal,
            Vector3 inheritedVelocity,
            float radius,
            float mass,
            uint seed)
        {
            EarthRockBreakDecision decision = ResolveBreak(radius, Mathf.Max(0.01f, mass),
                Mathf.Max(100000f, mass * 100f));
            TryEmitBreak(position, normal, inheritedVelocity, radius, mass, seed, decision, 0);
        }

        public bool TryEmitBreak(Vector3 position, Vector3 normal, Vector3 inheritedVelocity,
            float radius, float mass, uint seed, EarthRockBreakDecision decision, int depth,
            EarthMatterIdentity parentIdentity = null)
        {
            int count = decision.PhysicalPieces;
            if (!decision.Breaks || count < 0 || count > _breakPieces.Length ||
                !float.IsFinite(radius) || !float.IsFinite(mass) || radius <= 0f || mass <= 0f)
                return false;
            int free = 0;
            for (int index = 0; index < _pieces.Count; index++)
                if (_pieces[index].CanReuse && free < count) _breakPieces[free++] = _pieces[index];
            if (free < count)
                return RejectBreak("PhysicalPoolFull: parent retained", position, normal, radius, seed);
            if (parentIdentity == null)
                return RejectBreak("MissingParentIdentity: parent retained", position, normal, radius, seed);
            if (parentIdentity.IsDormantProxyReleased)
                return RejectBreak("ArchivedParent: duplicate dust rejected", position, normal, radius, seed);
            EarthMatterKernelBehaviour kernel = parentIdentity.Kernel != null ? parentIdentity.Kernel : matterKernel;
            if (kernel == null)
                return RejectBreak("MissingMatterKernel: configure debris pool", position, normal, radius, seed);
            bool hasParent = parentIdentity.TryRead(out EarthMatterRecord parent);
            if (parentIdentity.MatterId.IsValid && (!hasParent || parent.Phase == EarthMatterPhase.Consumed))
                return RejectBreak("RetiredParent: duplicate split rejected", position, normal, radius, seed);
            if (!hasParent)
            {
                // Authored decor/meteor sources are real world matter, not a fabricated terrain excavation.
                Rigidbody parentBody = parentIdentity.GetComponent<Rigidbody>();
                if (parentBody == null)
                    return RejectBreak("MissingParentBody: parent retained", position, normal, radius, seed);
                float volume = (4f / 3f) * Mathf.PI * radius * radius * radius;
                var pose = new EarthMatterPose((float3)parentBody.position,
                    new quaternion(parentBody.rotation.x, parentBody.rotation.y, parentBody.rotation.z, parentBody.rotation.w));
                parent = new EarthMatterRecord { Phase = EarthMatterPhase.FreeDynamic,
                    Representation = EarthRepresentationTier.SecondaryPhysical, Material = EarthMaterialKind.Stone,
                    Volume = volume, Mass = mass, Integrity = 1f, Shape = EarthShapeSemantic.NaturalRock,
                    RestPose = pose, CurrentPose = pose, LinearVelocity = (float3)inheritedVelocity,
                    Source = new EarthSourceProvenance(EarthSourceKind.Fragment, seed, 1, -1, 0,
                        float3.zero, 0f, EarthProvenanceFlags.None) };
                if (!parentIdentity.Configure(kernel, parent, parentBody) || !parentIdentity.TryRead(out parent))
                    return RejectBreak("ParentRegistrationRejected: parent retained", position, normal, radius, seed);
            }
            if (parent.Phase != EarthMatterPhase.FreeDynamic && parent.Phase != EarthMatterPhase.Sleeping)
                return RejectBreak("ParentControlledOrReturning: parent retained", position, normal, radius, seed);
            if (count == 0)
            {
                // Recursion budget must never turn an otherwise large stone into disappearing gameplay mass.
                if (radius > (profile != null ? profile.SmallShatterRadius : 0.35f))
                    return RejectBreak("SplitDepthLimit: persistent stone retained", position, normal, radius, seed);
                if (parent.Phase != EarthMatterPhase.Sleeping && !parentIdentity.TryTransition(EarthMatterPhase.Sleeping))
                    return RejectBreak("DustArchiveRejected: parent retained", position, normal, radius, seed);
                kernel.Registry.TrySetRepresentation(parentIdentity.MatterId, EarthRepresentationTier.DormantRecord);
                if (!parentIdentity.ReleaseDormantRepresentation()) return false;
                LastBreakRejection = "None";
                materialFeedback?.Emit(EarthMaterialFeedbackKind.Fracture, position, normal, 1f, radius,
                    seed, 0, decision.DustCount, decision.ChipCount);
                return true;
            }
            float spread = profile != null ? profile.ShatterSpreadSpeed : 3.8f;
            Collider parentCollider = parentIdentity.GetComponent<Collider>();
            if (parentCollider == null) parentCollider = parentIdentity.GetComponentInChildren<Collider>();
            using (ContainedSplitMarker.Auto())
            {
                EarthConvexFragmentCache.Child[] cells;
                try { cells = _convexCells.Get(_convexCells.SourceMesh(parentCollider), count); }
                catch (System.Exception error)
                { return RejectBreak("ConvexPartitionRejected: parent retained; " + error.Message, position, normal, radius, seed); }
                float totalCellVolume = 0f;
                foreach (var cell in cells) totalCellVolume += cell.Volume;
                for (int index = 0; index < count; index++)
                {
                    if (!_breakPieces[index].PreparePartitionChild(cells[index], parentCollider, out _childRadii[index]))
                        return RejectBreak("UnsupportedShearedFractureFrame: parent retained", position, normal, radius, seed);
                    Transform child = _breakPieces[index].transform;
                    Quaternion childRotation = child.rotation;
                    var pose = new EarthMatterPose((float3)child.position,
                        new quaternion(childRotation.x, childRotation.y, childRotation.z, childRotation.w));
                    Vector3 direction = HashDirection(seed, index, normal);
                    Vector3 velocity = inheritedVelocity + direction * spread * Mathf.Lerp(0.65f, 1.25f, Hash01(seed ^ 0x91u, index));
                    _childRecords[index] = EarthRockBreakPolicy.PartitionChild(parent, cells[index].Volume / totalCellVolume, pose, (float3)velocity);
                }
            }
            // Registry validates the complete partition before changing the parent or any child.
            if (!kernel.Registry.TrySplit(parentIdentity.MatterId, _childRecords, count, _childIds, 0.001f))
                return RejectBreak("CanonicalSplitRejected: parent retained", position, normal, radius, seed);
            for (int index = 0; index < count; index++)
            {
                EarthRockDebris piece = _breakPieces[index];
                piece.ConfigureBreak(this, seed ^ (uint)(index + 1), depth + 1, _childRadii[index]);
                piece.BindPersistentMatter(kernel, _childIds[index]);
                piece.BeginBallistic(
                    (Vector3)_childRecords[index].CurrentPose.Position,
                    _childRadii[index],
                    _childRecords[index].Mass,
                    (Vector3)_childRecords[index].LinearVelocity,
                    profile, true);
                for (int previous = 0; previous < index; previous++)
                    piece.IgnoreSplitSibling(_breakPieces[previous]);
            }
            LastBreakRejection = "None";
            materialFeedback?.Emit(EarthMaterialFeedbackKind.Fracture, position, normal, 1f, radius,
                seed, 0, decision.DustCount, decision.ChipCount);
            return true;
        }

        private bool RejectBreak(string reason, Vector3 point, Vector3 normal, float radius, uint seed)
        {
            LastBreakRejection = reason;
            RejectedBreakCount++;
            materialFeedback?.Emit(EarthMaterialFeedbackKind.Impact, point, normal, 1f, radius, seed);
            return false;
        }

        internal bool HandleDebrisImpact(EarthRockDebris piece, Collision collision, float radius, uint seed, int depth)
        {
            if (collision == null || collision.contactCount == 0 || collision.collider.isTrigger) return false;
            if (collision.relativeVelocity.sqrMagnitude < .5625f) return false;
            ContactPoint contact = collision.GetContact(0);
            Rigidbody body = piece.GetComponent<Rigidbody>();
            float approach = Mathf.Max(0f, -Vector3.Dot(collision.relativeVelocity, contact.normal));
            float impulse = Mathf.Max(collision.impulse.magnitude, approach * body.mass);
            var hit = new EarthStructureImpact(contact.point, -contact.normal, impulse,
                EarthStructureImpactKind.Projectile, piece.StableEarthId);
            EarthStructureImpactRouter.Apply(collision.collider, in hit);
            impulse = piece.AccumulateImpact(impulse);
            EarthRockBreakDecision decision = ResolveBreak(radius, body.mass, impulse, false, depth);
            if (!decision.Breaks)
            {
                if (approach >= 0.75f) materialFeedback?.Emit(EarthMaterialFeedbackKind.Impact,
                    contact.point, contact.normal, 0.4f, radius, seed);
                return false;
            }
            return TryEmitBreak(contact.point, contact.normal, body.linearVelocity, radius, body.mass,
                seed, decision, depth, piece.MatterIdentity);
        }

        public void EmitAccretion(
            EarthFragment target,
            Vector3 surfacePoint,
            Vector3 localUp,
            float volume,
            uint seed)
        {
            if (target == null || volume <= 0f) return;
            int count = profile != null ? profile.AccretionChipCount : 4;
            for (int index = 0; index < count; index++)
            {
                Vector3 tangent = Vector3.Cross(localUp, HashDirection(seed, index, localUp)).normalized;
                Vector3 start = surfacePoint + (tangent * Mathf.Lerp(-0.28f, 0.28f, Hash01(seed ^ 0xA5u, index)));
                EarthRockDebris piece = Acquire();
                if (piece == null)
                {
                    // Terrain was already excavated. Cosmetic capacity must not lose
                    // that volume or steal an airborne split chunk to show a chip.
                    target.AccreteVolume(volume / count);
                    continue;
                }
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
                if (_pieces[index].CanReuse) return _pieces[index];
            return null;
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
            filter.sharedMesh = _renderBevels.Get(shape, stoneBevelProfile);
            collider.sharedMesh = shape;
            collider.convex = true;
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            GravityBody gravity = go.AddComponent<GravityBody>();
            gravity.Configure(gravityWorld, body);
            go.AddComponent<EarthMatterIdentity>();
            EarthRockDebris piece = go.AddComponent<EarthRockDebris>();
            piece.ConfigureVisualSeed(unchecked((uint)_pieces.Count + 1u));
            piece.CacheTemplateShapes();
            PrepareFracture(collider);
            go.SetActive(false);
            _pieces.Add(piece);
            return piece;
        }

        private Mesh ShapeForIndex(int index)
        {
            if (_runtimeShapeVariants != null && _runtimeShapeVariants.Length > 0)
                return _runtimeShapeVariants[Mathf.Abs(index) % _runtimeShapeVariants.Length];
            if (meshVariants != null && meshVariants.Length > 0)
            {
                Mesh candidate = meshVariants[index % meshVariants.Length];
                if (candidate != null) return candidate;
            }
            if (mesh != null) return mesh;
            if (_fallbackMesh == null) _fallbackMesh = BuildFallbackDebrisMesh();
            return _fallbackMesh;
        }

        private void ApplyShape(GameObject target, Mesh shape)
        {
            if (target == null || shape == null) return;
            MeshFilter filter = target.GetComponent<MeshFilter>();
            MeshCollider collider = target.GetComponent<MeshCollider>();
            if (filter != null) filter.sharedMesh = _renderBevels.Get(shape, stoneBevelProfile);
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
            EarthMeshIntegrityGate.ValidateInPlaceOrUseFallback(
                fallback,
                EarthMeshIntegrityPolicy.ConvexCollider,
                fallback.name,
                fallback.bounds);
            return fallback;
        }

        private void OnDestroy()
        {
            _renderBevels.Clear();
            _convexCells.Dispose();
            if (_fallbackMesh != null)
            {
                if (Application.isPlaying) Destroy(_fallbackMesh);
                else DestroyImmediate(_fallbackMesh);
                _fallbackMesh = null;
            }
            if (_runtimeShapeVariants == null) return;
            for (int index = 0; index < _runtimeShapeVariants.Length; index++)
            {
                Mesh generated = _runtimeShapeVariants[index];
                if (generated == null) continue;
                if (Application.isPlaying) Destroy(generated);
                else DestroyImmediate(generated);
            }
            _runtimeShapeVariants = null;
        }

        private void EnsureRuntimeShapeLibrary()
        {
            if (_runtimeShapeVariants != null) return;
            if (HasAuthoredV5PhysicsLibrary(meshVariants))
            {
                mesh = meshVariants[0];
                return;
            }
            bool legacyLibrary = shapeGrammarProfile != null || meshVariants == null || meshVariants.Length == 0;
            if (!legacyLibrary)
            {
                Mesh first = meshVariants[0];
                legacyLibrary = first != null && first.name.StartsWith(
                    "Beveled Earth Block", System.StringComparison.OrdinalIgnoreCase);
            }
            if (!legacyLibrary) return;
            _runtimeShapeVariants = new Mesh[EarthRockMeshFactory.ArchetypeCount];
            for (int index = 0; index < _runtimeShapeVariants.Length; index++)
            {
                uint seed = EarthShapeSeed.Compose(
                    shapeGrammarProfile != null ? shapeGrammarProfile.LibrarySeed : 0xE17F0411u,
                    (uint)(index + 1), 2u, 1u, 0xD3B415u).Value;
                _runtimeShapeVariants[index] = EarthRockMeshFactory.Create((EarthRockArchetype)index, seed);
            }
            if (_runtimeShapeVariants.Length > 0) mesh = _runtimeShapeVariants[0];
        }

        private static bool HasAuthoredV5PhysicsLibrary(Mesh[] variants)
        {
            if (variants == null || variants.Length == 0) return false;
            for (int index = 0; index < variants.Length; index++)
            {
                Mesh variant = variants[index];
                if (variant == null || !variant.name.StartsWith(
                        "V5_Physics_", System.StringComparison.Ordinal))
                    return false;
            }
            return true;
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
    public sealed class EarthRockDebris : MonoBehaviour, IEarthPhysicalTarget
    {
        private EarthImpactDamage _impactDamage;
        public float AccumulateImpact(float impulse) { _impactDamage.Add(impulse); return _impactDamage.Impulse; }
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
        private Renderer _visualRenderer;
        private MaterialPropertyBlock _visualProperties;
        private uint _visualSeed = 1u;
        private EarthRockDebrisPool _breakOwner;
        private uint _breakSeed;
        private int _breakDepth;
        private float _breakRadius;
        private float _breakArmedAt;
        private readonly EarthContactFrictionFeedback _frictionFeedback = new();
        private void OnCollisionStay(Collision collision) =>
            _frictionFeedback.Emit(_breakOwner != null ? _breakOwner.MaterialFeedback : null,
                collision, StableEarthId != 0u ? StableEarthId : _breakSeed, TargetHandle.Generation);
        private readonly Collider[] _splitIgnores = new Collider[4];
        private int _splitIgnoreCount;
        private EarthMatterIdentity _matterIdentity;
        private bool _persistent;
        private int _gripCount;
        private MeshFilter _shapeFilter;
        private Mesh _templateRenderMesh, _templateColliderMesh;

        // A consumed partition shell can later serve cosmetic accretion; restore its
        // normalized template instead of inheriting the previous parent's cell scale.
        internal void CacheTemplateShapes()
        {
            _shapeFilter = GetComponent<MeshFilter>();
            var collider = GetComponent<MeshCollider>();
            _templateRenderMesh = _shapeFilter != null ? _shapeFilter.sharedMesh : null;
            _templateColliderMesh = collider != null ? collider.sharedMesh : null;
        }
        private void RestoreTemplateShape()
        {
            if (_shapeFilter != null && _templateRenderMesh != null) _shapeFilter.sharedMesh = _templateRenderMesh;
            if (_collider is MeshCollider collider && _templateColliderMesh != null) collider.sharedMesh = _templateColliderMesh;
        }

        internal bool PreparePartitionChild(EarthConvexFragmentCache.Child cell, Collider parent, out float radius)
        {
            Resolve();
            Transform source = parent.transform;
            Vector3 parentScale = source.lossyScale;
            Vector3 ownerScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.SetPositionAndRotation(source.TransformPoint(cell.Center), source.rotation);
            transform.localScale = new Vector3(parentScale.x / ownerScale.x, parentScale.y / ownerScale.y, parentScale.z / ownerScale.z);
            Matrix4x4 expected = source.localToWorldMatrix;
            Matrix4x4 actual = transform.localToWorldMatrix;
            radius = Mathf.Pow(cell.Volume * Mathf.Abs(expected.determinant) * .2387324f, 1f / 3f);
            if ((expected.MultiplyVector(Vector3.right)-actual.MultiplyVector(Vector3.right)).sqrMagnitude > 1e-8f ||
                (expected.MultiplyVector(Vector3.up)-actual.MultiplyVector(Vector3.up)).sqrMagnitude > 1e-8f ||
                (expected.MultiplyVector(Vector3.forward)-actual.MultiplyVector(Vector3.forward)).sqrMagnitude > 1e-8f) return false;
            _shapeFilter.sharedMesh = cell.RenderMesh;
            ((MeshCollider)_collider).sharedMesh = cell.ColliderMesh;
            return true;
        }
        public EarthMatterIdentity MatterIdentity => _matterIdentity;
        public Rigidbody Body => _body;
        public uint StableEarthId => _matterIdentity != null && _matterIdentity.MatterId.IsValid
            ? 0xE0000000u | _matterIdentity.MatterId.StableId : 0u;
        public EarthPhysicalTargetHandle TargetHandle => new EarthPhysicalTargetHandle(StableEarthId,
            _matterIdentity != null ? _matterIdentity.MatterId.Generation : 0u);
        public float EarthMass => _body != null ? _body.mass : 0f;
        public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.Rock;
        public bool IsEarthTargetValid => _persistent && gameObject.activeInHierarchy && _body != null &&
            _matterIdentity != null && _matterIdentity.TryRead(out EarthMatterRecord record) &&
            (record.Phase == EarthMatterPhase.FreeDynamic || record.Phase == EarthMatterPhase.Sleeping ||
             record.Phase == EarthMatterPhase.Controlled);
        public bool CanReuse => !gameObject.activeSelf && (_matterIdentity == null ||
            !_matterIdentity.TryRead(out EarthMatterRecord record) || record.Phase == EarthMatterPhase.Consumed);

        public void BindPersistentMatter(EarthMatterKernelBehaviour kernel, EarthMatterId id)
        {
            Resolve();
            _matterIdentity = EarthMatterRuntimeBridge.BindExistingRecord(this, kernel, id, _body);
            _persistent = _matterIdentity != null;
            _gripCount = 0;
        }

        public void OnEarthMagicGrabbed(EarthMagicGripKind grip)
        {
            if (!IsEarthTargetValid) return;
            _gripCount++;
            _matterIdentity.TryTransition(EarthMatterPhase.Controlled);
            _body.WakeUp();
        }

        public void OnEarthMagicReleased(EarthMagicGripKind grip)
        {
            _gripCount = Mathf.Max(0, _gripCount - 1);
            if (_gripCount == 0) _matterIdentity?.TryTransition(EarthMatterPhase.FreeDynamic);
            _body?.WakeUp();
        }

        public void IgnoreSplitSibling(EarthRockDebris sibling)
        {
            if (sibling == null || sibling._collider == null || _collider == null || _splitIgnoreCount >= _splitIgnores.Length) return;
            _splitIgnores[_splitIgnoreCount++] = sibling._collider;
            UnityEngine.Physics.IgnoreCollision(_collider, sibling._collider, true);
        }

        private void RestoreSplitCollisions()
        {
            for (int i = 0; i < _splitIgnoreCount; i++)
            {
                if (_collider != null && _splitIgnores[i] != null)
                    UnityEngine.Physics.IgnoreCollision(_collider, _splitIgnores[i], false);
                _splitIgnores[i] = null;
            }
            _splitIgnoreCount = 0;
        }

        public void ConfigureBreak(EarthRockDebrisPool owner, uint seed, int depth, float radius)
        {
            _impactDamage = default;
            _breakOwner = owner;
            _breakSeed = seed;
            _breakDepth = depth;
            _breakRadius = radius;
            _breakArmedAt = Time.fixedTime + 0.12f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_accreting || _gripCount > 0 || _breakOwner == null || Time.fixedTime < _breakArmedAt) return;
            if (_breakOwner.HandleDebrisImpact(this, collision, _breakRadius, _breakSeed, _breakDepth))
                ResetPiece();
        }

        public void ConfigureVisualSeed(uint value) => _visualSeed = value == 0u ? 1u : value;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _visualRenderer = GetComponent<Renderer>();
            _visualProperties = new MaterialPropertyBlock();
            _matterIdentity = GetComponent<EarthMatterIdentity>();
        }

        public void BeginBallistic(
            Vector3 position,
            float radius,
            float mass,
            Vector3 velocity,
            EarthRockProfile profile, bool preservePreparedShape = false)
        {
            Resolve();
            _profile = profile;
            _target = null;
            _accreting = false;
            _elapsed = 0f;
            _restSeconds = profile != null ? profile.DebrisRestSeconds : 1.15f;
            _shrinkSeconds = profile != null ? profile.DebrisShrinkSeconds : 0.9f;
            transform.position = position;
            if (!preservePreparedShape)
            { RestoreTemplateShape(); transform.localScale = Vector3.one * (radius * 2f); }
            _fullScale = transform.localScale;
            gameObject.SetActive(true);
            _collider.enabled = true;
            _body.isKinematic = false;
            _body.detectCollisions = true;
            _body.mass = mass;
            _body.linearVelocity = velocity;
            uint spinSeed = _breakSeed ^ (_visualSeed * 193u);
            if (!preservePreparedShape)
                _body.rotation = Quaternion.Euler(SpinHash(spinSeed) * 360f,
                    SpinHash(spinSeed + 17u) * 360f, SpinHash(spinSeed + 31u) * 360f);
            _body.angularVelocity = new Vector3(Mathf.Lerp(-7f, 7f, SpinHash(spinSeed + 47u)),
                Mathf.Lerp(-9f, 9f, SpinHash(spinSeed + 61u)), Mathf.Lerp(-6f, 6f, SpinHash(spinSeed + 79u)));
            uint visualSeed = _visualSeed ^
                              unchecked((uint)Mathf.RoundToInt(position.sqrMagnitude * 997f));
            EarthStoneVisualVariant.Apply(_visualRenderer, visualSeed, _visualProperties);
        }

        private static float SpinHash(uint seed)
        {
            seed ^= seed >> 16; seed *= 0x7FEB352Du; seed ^= seed >> 15;
            return (seed & 0x00FFFFFFu) / 16777216f;
        }

        public void BeginAccretion(
            EarthFragment target,
            Vector3 start,
            float volume,
            float visualRadius,
            float travelSeconds)
        {
            Resolve();
            _breakOwner = null;
            RestoreTemplateShape();
            _persistent = false;
            _gripCount = 0;
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
            uint visualSeed = target != null ? target.FragmentId ^ _visualSeed : _visualSeed;
            EarthStoneVisualVariant.Apply(_visualRenderer, visualSeed, _visualProperties);
        }

        public void ResetPiece()
        {
            Resolve();
            if (_persistent && _matterIdentity != null && _matterIdentity.TryRead(out EarthMatterRecord record) &&
                record.Phase != EarthMatterPhase.Consumed) return;
            _persistent = false;
            RestoreSplitCollisions();
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
            if (_splitIgnoreCount > 0 && Time.fixedTime >= _breakArmedAt) RestoreSplitCollisions();
            if (_accreting)
            {
                UpdateAccretion();
                return;
            }
            if (_persistent) return; // Canonical chunks persist until explicit split or terrain return.
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
