using System;
using System.Collections.Generic;
using Elemental.Runtime.Matter;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Matter;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthPlatformPool : MonoBehaviour
    {
        [SerializeField, Range(1, 12)] private int capacity = 6;
        [SerializeField] private Material platformMaterial;
        [SerializeField] private EarthPlatformProfile profile;
        [SerializeField] private EarthPhysicsFeelProfile physicsFeelProfile;
        [SerializeField] private Mesh[] pieceMeshVariants;
        [SerializeField] private EarthSurfaceQueryService surfaceQueries;
        [SerializeField] private EarthStructureFractureProfile fractureProfile;
        [SerializeField] private EarthMatterKernelBehaviour matterKernel;

        private readonly List<EarthPlatform> _platforms = new List<EarthPlatform>(6);
        private uint _nextId = 1u;

        public EarthPlatform LastAcquired { get; private set; }
        public EarthPlatformProfile Profile => profile;
        public event Action<EarthPlatform> PlatformFractured;
        public EarthPlatform FindActive(uint structureId)
        {
            for (int index = 0; index < _platforms.Count; index++)
            {
                EarthPlatform platform = _platforms[index];
                if (platform.gameObject.activeSelf && platform.PlatformId == structureId) return platform;
            }
            return null;
        }
        public int ActiveCount
        {
            get
            {
                int active = 0;
                for (int index = 0; index < _platforms.Count; index++)
                    if (_platforms[index].gameObject.activeSelf) active++;
                return active;
            }
        }

        public void Configure(int configuredCapacity, Material material, EarthPlatformProfile configuredProfile)
        {
            profile = configuredProfile;
            capacity = Mathf.Clamp(
                configuredProfile != null ? configuredProfile.MaximumActivePlatforms : configuredCapacity,
                1,
                12);
            platformMaterial = material;
        }

        public void ConfigurePhysicsFeel(EarthPhysicsFeelProfile configuredProfile) =>
            physicsFeelProfile = configuredProfile;

        public void ConfigureSurfaceQueries(EarthSurfaceQueryService configuredService)
        {
            surfaceQueries = configuredService;
            for (int index = 0; index < _platforms.Count; index++)
            {
                EarthPlatformSurfaceProvider provider =
                    _platforms[index].GetComponent<EarthPlatformSurfaceProvider>();
                provider?.Configure(_platforms[index], surfaceQueries);
            }
        }

        public void ConfigurePieceMeshes(Mesh[] configuredVariants)
        {
            pieceMeshVariants = configuredVariants;
            for (int index = 0; index < _platforms.Count; index++)
                _platforms[index].ConfigurePieceMeshes(pieceMeshVariants);
        }

        public void ConfigureFractureProfile(EarthStructureFractureProfile configuredProfile)
        {
            fractureProfile = configuredProfile;
            for (int index = 0; index < _platforms.Count; index++)
                _platforms[index].ConfigureFractureProfile(fractureProfile);
        }

        private void Awake()
        {
            if (matterKernel == null) matterKernel = EarthMatterKernelBehaviour.FindOrCreate(this);
            for (int index = 0; index < capacity; index++) CreatePlatform();
        }

        public EarthPlatform Acquire(in EarthPlatformGeometry geometry, float height, float embedDepth)
        {
            for (int index = 0; index < _platforms.Count; index++)
            {
                EarthPlatform platform = _platforms[index];
                if (platform.gameObject.activeSelf) continue;
                platform.Initialize(_nextId++, in geometry, height, embedDepth);
                float volume = Mathf.Max(0.000001f, platform.Area * (platform.Height + Mathf.Max(0.08f, embedDepth)));
                float mass = Mathf.Max(1f, volume * 170f);
                var source = new EarthSourceProvenance(
                    EarthSourceKind.TerrainEdit,
                    platform.PlatformId,
                    platform.Generation >= ushort.MaxValue
                        ? ushort.MaxValue
                        : (ushort)Mathf.Max(1, (int)platform.Generation),
                    -1,
                    unchecked((uint)Time.frameCount),
                    geometry.Center,
                    volume,
                    EarthProvenanceFlags.ExactReturnSupported |
                    EarthProvenanceFlags.SourceCavityValid |
                    EarthProvenanceFlags.VolumeReserved);
                EarthMatterRuntimeBridge.EnsureIdentity(
                    platform,
                    matterKernel,
                    platform.GetComponent<Rigidbody>(),
                    EarthMatterPhase.Forming,
                    EarthRepresentationTier.HeroPhysical,
                    EarthMaterialKind.Stone,
                    EarthShapeSemantic.Slab,
                    volume,
                    mass,
                    source);
                LastAcquired = platform;
                return platform;
            }
            return null;
        }

        private EarthPlatform CreatePlatform()
        {
            GameObject go = new GameObject($"Earth Platform {_platforms.Count + 1:00}");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = platformMaterial;
            MeshCollider collider = go.AddComponent<MeshCollider>();
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            physicsFeelProfile?.Apply(body, collider, EarthPhysicsBodyClass.Structure);
            EarthPlatform platform = go.AddComponent<EarthPlatform>();
            platform.Fractured += HandlePlatformFractured;
            platform.Configure(platformMaterial, profile, physicsFeelProfile, pieceMeshVariants);
            platform.ConfigureFractureProfile(fractureProfile);
            EarthPlatformSurfaceProvider provider = go.AddComponent<EarthPlatformSurfaceProvider>();
            provider.Configure(platform, surfaceQueries);
            go.SetActive(false);
            _platforms.Add(platform);
            return platform;
        }

        private void HandlePlatformFractured(EarthPlatform platform) =>
            PlatformFractured?.Invoke(platform);
    }
}
