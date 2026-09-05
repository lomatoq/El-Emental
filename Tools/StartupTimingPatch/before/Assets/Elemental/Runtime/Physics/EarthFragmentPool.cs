using System.Collections.Generic;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Runtime.Matter;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Structures;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthFragmentPool : MonoBehaviour
    {
        [SerializeField, Range(1, 32)] private int capacity = 8;
        [SerializeField] private Material fragmentMaterial;
        [SerializeField] private Mesh fragmentMesh;
        [SerializeField] private Mesh[] fragmentMeshVariants;
        [SerializeField] private GravityWorldBehaviour gravityWorld;
        [SerializeField] private EarthRockProfile rockProfile;
        [SerializeField] private EarthRockDebrisPool debrisPool;
        [SerializeField] private EarthHoverProfile hoverProfile;
        [SerializeField] private EarthPhysicsFeelProfile physicsFeelProfile;
        [SerializeField] private EarthShapeGrammarProfile shapeGrammarProfile;
        [SerializeField] private EarthMaterialFeedbackHub materialFeedback;
        [SerializeField] private EarthStoneBevelProfile stoneBevelProfile;
        private readonly EarthStoneRenderBevelCache _renderBevels = new();

        public void ConfigureMaterialFeedback(EarthMaterialFeedbackHub hub)
        {
            materialFeedback = hub;
            debrisPool?.ConfigureMaterialFeedback(hub);
        }

        private readonly List<EarthFragment> _fragments = new List<EarthFragment>(8);
        private EarthShapeDiversityTracker _shapeDiversity;
        private Mesh[] _runtimeShapeVariants;
        private uint _nextId = 1u;

        public int ActiveCount { get; private set; }
        public EarthFragment LastAcquired { get; private set; }
        public Material SharedMaterial => fragmentMaterial;
        public GravityWorldBehaviour GravityWorld => gravityWorld;
        public EarthMaterialFeedbackHub MaterialFeedback => materialFeedback;

        public Mesh ResolveShapeVariant(int stableIndex)
        {
            Mesh[] variants = AvailableShapeVariants;
            if (variants != null && variants.Length > 0)
                return variants[Mathf.Abs(stableIndex) % variants.Length];
            return fragmentMesh;
        }

        /// <summary>Appends stable authored sources which the startup baker must cover.</summary>
        public int AppendAuthoredFractureSources(List<Mesh> destination)
        {
            if (destination == null) throw new System.ArgumentNullException(nameof(destination));
            int before = destination.Count;
            if (fragmentMeshVariants != null)
                for (int index = 0; index < fragmentMeshVariants.Length; index++)
                    AppendUnique(destination, fragmentMeshVariants[index]);
            AppendUnique(destination, fragmentMesh);
            return destination.Count - before;
        }

        private static void AppendUnique(List<Mesh> destination, Mesh candidate)
        {
            if (candidate != null && !destination.Contains(candidate)) destination.Add(candidate);
        }

        public void Configure(
            int configuredCapacity,
            Material material,
            GravityWorldBehaviour configuredGravityWorld,
            Mesh configuredMesh = null,
            EarthRockProfile configuredRockProfile = null,
            EarthRockDebrisPool configuredDebrisPool = null)
        {
            capacity = Mathf.Clamp(configuredCapacity, 1, 32);
            fragmentMaterial = material;
            gravityWorld = configuredGravityWorld;
            fragmentMesh = configuredMesh;
            fragmentMeshVariants = configuredMesh != null ? new[] { configuredMesh } : null;
            rockProfile = configuredRockProfile;
            debrisPool = configuredDebrisPool;
        }

        public void ConfigureMeshVariants(params Mesh[] meshes)
        {
            fragmentMeshVariants = meshes;
            if (fragmentMesh == null && meshes != null && meshes.Length > 0) fragmentMesh = meshes[0];
            if (meshes != null)
                for (int index = 0; index < meshes.Length; index++) _renderBevels.Get(meshes[index], stoneBevelProfile);
        }

        public void ConfigureHover(EarthHoverProfile profile)
        {
            hoverProfile = profile;
            for (int index = 0; index < _fragments.Count; index++)
                _fragments[index].ConfigureHover(profile);
        }

        public void ConfigurePhysicsFeel(EarthPhysicsFeelProfile profile)
        {
            physicsFeelProfile = profile;
            for (int index = 0; index < _fragments.Count; index++)
                _fragments[index].GetComponent<EarthProjectileSweepGuard>()?.Configure(_fragments[index], profile);
        }

        public void ConfigureShapeGrammar(EarthShapeGrammarProfile profile)
        {
            shapeGrammarProfile = profile;
            _shapeDiversity = new EarthShapeDiversityTracker(
                profile != null ? profile.LocalHistoryLength : 16);
        }

        private void Awake()
        {
            debrisPool?.ConfigureMaterialFeedback(materialFeedback);
            _shapeDiversity ??= new EarthShapeDiversityTracker(
                shapeGrammarProfile != null ? shapeGrammarProfile.LocalHistoryLength : 16);
            EnsureRuntimeShapeLibrary();
            Mesh[] preparedShapes = AvailableShapeVariants;
            if (preparedShapes != null)
                for (int index = 0; index < preparedShapes.Length; index++) _renderBevels.Get(preparedShapes[index], stoneBevelProfile);
            _renderBevels.Get(fragmentMesh, stoneBevelProfile);
            // The complete hero pool is authored up front. A cast may claim an existing
            // shell, but must never create a GameObject, collider or mesh at runtime.
            int warmCount = capacity;
            for (int index = 0; index < warmCount; index++)
            {
                CreateFragment();
            }
        }

        public EarthFragment Acquire(
            MagicExecutor executor,
            Vector3 position,
            float radius,
            float mass,
            Transform holdTarget = null)
        {
            EarthFragment fragment = null;
            for (int index = 0; index < _fragments.Count; index++)
            {
                if (!_fragments[index].gameObject.activeSelf)
                {
                    fragment = _fragments[index];
                    break;
                }
            }

            if (fragment == null)
            {
                if (_fragments.Count >= capacity)
                {
                    Debug.LogWarning("[EarthMatter] Hero fragment budget exhausted; acquisition rejected without reusing live matter.", this);
                    return null;
                }
                fragment = CreateFragment();
                ActiveCount++;
            }
            else
            {
                ActiveCount = Mathf.Min(capacity, ActiveCount + 1);
            }

            uint fragmentId = _nextId++;
            Mesh[] variants = AvailableShapeVariants;
            int shapeIndex = (int)_shapeDiversity.Select(
                EarthShapeSeed.Compose(
                    shapeGrammarProfile != null ? shapeGrammarProfile.LibrarySeed : 0xE17F0411u,
                    fragmentId, 1u, fragmentId, 0u).Value,
                shapeGrammarProfile != null ? shapeGrammarProfile.CandidateAttempts : 12);
            Mesh shape = variants != null && variants.Length > 0
                ? variants[shapeIndex % variants.Length]
                : fragmentMesh;
            fragment.Initialize(
                fragmentId, executor, position, radius, mass, holdTarget,
                this, rockProfile);
            // Assign after activation so PhysX cooks the convex collider immediately;
            // assigning while an inactive pooled object is waking can leave sharedMesh null.
            fragment.SetShape(shape, _renderBevels.Get(shape, stoneBevelProfile));
            debrisPool?.PrepareFracture(fragment.GetComponent<Collider>());
            LastAcquired = fragment;
            return fragment;
        }

        public EarthFragment ReserveExtraction(
            MagicExecutor executor,
            Vector3 position,
            float radius,
            float mass)
        {
            EarthFragment fragment = Acquire(executor, position, radius, mass, null);
            if (fragment != null) fragment.BeginExtractionReservation();
            return fragment;
        }

        public bool TryShatter(
            EarthFragment fragment,
            Vector3 point,
            Vector3 normal,
            float impulse,
            bool presentSubthresholdImpact = false)
        {
            if (fragment == null || !fragment.gameObject.activeSelf || fragment.IsHeld) return false;
            if (rockProfile == null || debrisPool == null)
            {
                if (presentSubthresholdImpact) PresentLooseImpact(fragment, point, normal, impulse);
                return false;
            }
            EarthRockBreakDecision decision = rockProfile.ResolveBreak(fragment.Radius, fragment.Mass, impulse);
            if (!decision.Breaks)
            {
                if (presentSubthresholdImpact) PresentLooseImpact(fragment, point, normal, impulse);
                return false;
            }
            Vector3 inheritedVelocity = fragment.Body != null
                ? fragment.Body.linearVelocity
                : Vector3.zero;
            if (!debrisPool.TryEmitBreak(
                point,
                normal,
                inheritedVelocity,
                fragment.Radius,
                fragment.Mass,
                fragment.FragmentId, decision, 0, fragment.MatterIdentity)) return false;
            fragment.StopBendControl();
            // The canonical split already consumed the parent; small dust remains
            // as dormant mass. Never independently consume its child provenance.
            NotifyReleased(fragment);
            fragment.gameObject.SetActive(false);
            return true;
        }

        private void PresentLooseImpact(EarthFragment fragment, Vector3 point, Vector3 normal, float impulse)
        {
            float specificImpulse = impulse / Mathf.Max(0.01f, fragment.Mass);
            if (specificImpulse < 0.75f) return;
            materialFeedback?.Emit(EarthMaterialFeedbackKind.Impact, point, normal,
                Mathf.Clamp(specificImpulse / 8f, 0.4f, 1f), fragment.Radius, fragment.FragmentId);
        }

        internal void NotifyReleased(EarthFragment fragment)
        {
            if (fragment == null) return;
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
        }

        public void EmitAccretion(
            EarthFragment fragment,
            Vector3 surfacePoint,
            Vector3 localUp,
            float volume)
        {
            if (debrisPool == null || fragment == null || volume <= 0f) return;
            debrisPool.EmitAccretion(
                fragment, surfacePoint, localUp, volume,
                fragment.FragmentId ^ unchecked((uint)Time.frameCount));
        }

        private EarthFragment CreateFragment()
        {
            GameObject fragmentObject = new GameObject();
            fragmentObject.name = $"Earth Fragment {_fragments.Count + 1:00}";
            fragmentObject.transform.SetParent(transform, false);
            MeshFilter filter = fragmentObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = fragmentObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = fragmentMaterial;
            filter.sharedMesh = _renderBevels.Get(fragmentMesh, stoneBevelProfile);
            MeshCollider collider = fragmentObject.AddComponent<MeshCollider>();
            collider.sharedMesh = fragmentMesh;
            collider.convex = true;
            Rigidbody body = fragmentObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            physicsFeelProfile?.Apply(body, collider, EarthPhysicsBodyClass.HeavyBlock);
            // Every source, including executor-less bot projectiles, has a prepared
            // identity shell. Contact-time fracture must never AddComponent.
            fragmentObject.AddComponent<EarthMatterIdentity>();
            EarthFragment fragment = fragmentObject.AddComponent<EarthFragment>();
            fragmentObject.AddComponent<EarthTypedCombatProjectile>();
            fragment.ConfigureHover(hoverProfile);
            EarthProjectileSweepGuard sweepGuard = fragmentObject.AddComponent<EarthProjectileSweepGuard>();
            sweepGuard.Configure(fragment, physicsFeelProfile);
            GravityBody gravityBody = fragmentObject.AddComponent<GravityBody>();
            gravityBody.Configure(gravityWorld, body);
            // Cache hits only bind plans. On an intentional cache miss this keeps the
            // canonical cold fallback inside scene loading instead of the first cast.
            debrisPool?.PrepareFracture(collider);
            fragmentObject.SetActive(false);
            _fragments.Add(fragment);
            return fragment;
        }

        private Mesh[] AvailableShapeVariants =>
            _runtimeShapeVariants != null && _runtimeShapeVariants.Length > 0
                ? _runtimeShapeVariants
                : fragmentMeshVariants;

        private void EnsureRuntimeShapeLibrary()
        {
            if (_runtimeShapeVariants != null) return;
            if (HasAuthoredV5PhysicsLibrary(fragmentMeshVariants))
            {
                fragmentMesh = fragmentMeshVariants[0];
                return;
            }
            bool legacyLibrary = shapeGrammarProfile != null ||
                                 fragmentMeshVariants == null || fragmentMeshVariants.Length == 0;
            if (!legacyLibrary)
            {
                Mesh first = fragmentMeshVariants[0];
                legacyLibrary = first != null && first.name.StartsWith(
                    "Beveled Earth Block", System.StringComparison.OrdinalIgnoreCase);
            }
            if (!legacyLibrary) return;

            _runtimeShapeVariants = new Mesh[EarthRockMeshFactory.ArchetypeCount];
            for (int index = 0; index < _runtimeShapeVariants.Length; index++)
            {
                uint seed = EarthShapeSeed.Compose(
                    shapeGrammarProfile != null ? shapeGrammarProfile.LibrarySeed : 0xE17F0411u,
                    (uint)(index + 1), 1u, 1u, 0u).Value;
                _runtimeShapeVariants[index] = EarthRockMeshFactory.Create((EarthRockArchetype)index, seed);
            }
            if (_runtimeShapeVariants.Length > 0) fragmentMesh = _runtimeShapeVariants[0];
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

        private void OnDestroy()
        {
            _renderBevels.Clear();
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
    }
}
