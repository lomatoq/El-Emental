using System.Collections.Generic;
using Elemental.Runtime.World;
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

        private readonly List<EarthFragment> _fragments = new List<EarthFragment>(8);
        private int _reuseCursor;
        private uint _nextId = 1u;

        public int ActiveCount { get; private set; }
        public EarthFragment LastAcquired { get; private set; }

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
        }

        public void ConfigureHover(EarthHoverProfile profile)
        {
            hoverProfile = profile;
            for (int index = 0; index < _fragments.Count; index++)
                _fragments[index].ConfigureHover(profile);
        }

        public void ConfigurePhysicsFeel(EarthPhysicsFeelProfile profile) => physicsFeelProfile = profile;

        private void Awake()
        {
            for (int index = 0; index < capacity; index++)
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
                fragment = _fragments[_reuseCursor];
                _reuseCursor = (_reuseCursor + 1) % _fragments.Count;
            }
            else
            {
                ActiveCount = Mathf.Min(capacity, ActiveCount + 1);
            }

            uint fragmentId = _nextId++;
            Mesh[] variants = fragmentMeshVariants;
            Mesh shape = variants != null && variants.Length > 0
                ? variants[(int)((fragmentId - 1u) % (uint)variants.Length)]
                : fragmentMesh;
            fragment.Initialize(
                fragmentId, executor, position, radius, mass, holdTarget,
                this, rockProfile);
            // Assign after activation so PhysX cooks the convex collider immediately;
            // assigning while an inactive pooled object is waking can leave sharedMesh null.
            fragment.SetShape(shape);
            LastAcquired = fragment;
            return fragment;
        }

        public bool TryShatter(
            EarthFragment fragment,
            Vector3 point,
            Vector3 normal,
            float impulse)
        {
            if (fragment == null || rockProfile == null || debrisPool == null) return false;
            float specificImpulse = impulse / Mathf.Max(0.01f, fragment.Mass);
            if (impulse < rockProfile.MinimumShatterImpulse ||
                specificImpulse < rockProfile.ShatterSpecificImpulse) return false;
            Vector3 inheritedVelocity = fragment.Body != null
                ? fragment.Body.linearVelocity
                : Vector3.zero;
            debrisPool.EmitShatter(
                point,
                normal,
                inheritedVelocity,
                fragment.Radius,
                fragment.Mass,
                fragment.FragmentId);
            fragment.StopBendControl();
            fragment.gameObject.SetActive(false);
            return true;
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
            filter.sharedMesh = fragmentMesh;
            MeshCollider collider = fragmentObject.AddComponent<MeshCollider>();
            collider.sharedMesh = fragmentMesh;
            collider.convex = true;
            Rigidbody body = fragmentObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            physicsFeelProfile?.Apply(body, collider, EarthPhysicsBodyClass.HeavyBlock);
            EarthFragment fragment = fragmentObject.AddComponent<EarthFragment>();
            fragment.ConfigureHover(hoverProfile);
            GravityBody gravityBody = fragmentObject.AddComponent<GravityBody>();
            gravityBody.Configure(gravityWorld, body);
            fragmentObject.SetActive(false);
            _fragments.Add(fragment);
            return fragment;
        }
    }
}
