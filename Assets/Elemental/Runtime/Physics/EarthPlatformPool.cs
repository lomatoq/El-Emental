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
        public double LastAcquireSolidMilliseconds { get; private set; }
        public double PeakAcquireSolidMilliseconds { get; private set; }
        public EarthPlatformProfile Profile => profile;
        public event Action<EarthPlatform> PlatformFractured;
        public EarthPlatform FindActive(uint structureId)
        {
            for (int index = 0; index < _platforms.Count; index++)
            {
                EarthPlatform platform = _platforms[index];
                if (platform.IsInUse && platform.PlatformId == structureId) return platform;
            }
            return null;
        }
        public int ActiveCount
        {
            get
            {
                int active = 0;
                for (int index = 0; index < _platforms.Count; index++)
                    if (_platforms[index].IsInUse) active++;
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
            RebuildPreparedCache();
            if (_platforms.Count == 0 && capacity > 0) CreatePlatform();
        }

        public void PrewarmAll()
        {
            RebuildPreparedCache();
            while (_platforms.Count < capacity) CreatePlatform();
        }

        public EarthPlatform Acquire(in EarthPlatformGeometry geometry, float height, float embedDepth)
        {
            EarthPlatform selected = null;
            for (int index = 0; index < _platforms.Count; index++)
            {
                EarthPlatform platform = _platforms[index];
                if (platform.IsInUse) continue;
                selected = platform;
                break;
            }
            if (selected == null && _platforms.Count < capacity) selected = CreatePlatform();
            if (selected != null)
            {
                double acquireStarted = Time.realtimeSinceStartupAsDouble;
                selected.Initialize(_nextId++, in geometry, height, embedDepth);
                LastAcquireSolidMilliseconds =
                    (Time.realtimeSinceStartupAsDouble - acquireStarted) * 1000.0;
                PeakAcquireSolidMilliseconds = Math.Max(
                    PeakAcquireSolidMilliseconds,
                    LastAcquireSolidMilliseconds);
                float volume = Mathf.Max(0.000001f, selected.Area * (selected.Height + Mathf.Max(0.08f, embedDepth)));
                float mass = Mathf.Max(1f, volume * 170f);
                var source = new EarthSourceProvenance(
                    EarthSourceKind.TerrainEdit,
                    selected.PlatformId,
                    selected.Generation >= ushort.MaxValue
                        ? ushort.MaxValue
                        : (ushort)Mathf.Max(1, (int)selected.Generation),
                    -1,
                    unchecked((uint)Time.frameCount),
                    geometry.Center,
                    volume,
                    EarthProvenanceFlags.ExactReturnSupported |
                    EarthProvenanceFlags.SourceCavityValid |
                    EarthProvenanceFlags.VolumeReserved);
                EarthMatterRuntimeBridge.EnsureIdentity(
                    selected,
                    matterKernel,
                    selected.GetComponent<Rigidbody>(),
                    EarthMatterPhase.Forming,
                    EarthRepresentationTier.HeroPhysical,
                    EarthMaterialKind.Stone,
                    EarthShapeSemantic.Slab,
                    volume,
                    mass,
                    source);
                LastAcquired = selected;
                return selected;
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
            BoxCollider collider = go.AddComponent<BoxCollider>();
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
            platform.PrepareForPool();
            _platforms.Add(platform);
            return platform;
        }

        private void RebuildPreparedCache()
        {
            _platforms.Clear();
            for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                EarthPlatform platform = transform.GetChild(childIndex).GetComponent<EarthPlatform>();
                if (platform == null) continue;
                platform.Fractured -= HandlePlatformFractured;
                platform.Fractured += HandlePlatformFractured;
                platform.Configure(platformMaterial, profile, physicsFeelProfile, pieceMeshVariants);
                platform.ConfigureFractureProfile(fractureProfile);
                EarthPlatformSurfaceProvider provider =
                    platform.GetComponent<EarthPlatformSurfaceProvider>();
                provider?.Configure(platform, surfaceQueries);
                if (!platform.IsInUse) platform.PrepareForPool();
                _platforms.Add(platform);
            }
        }

        private void HandlePlatformFractured(EarthPlatform platform) =>
            PlatformFractured?.Invoke(platform);
    }
}
