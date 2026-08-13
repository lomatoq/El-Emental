using System.Collections.Generic;
using Elemental.Simulation.Bending;
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

        private readonly List<EarthPlatform> _platforms = new List<EarthPlatform>(6);
        private uint _nextId = 1u;

        public EarthPlatform LastAcquired { get; private set; }
        public EarthPlatformProfile Profile => profile;
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

        private void Awake()
        {
            for (int index = 0; index < capacity; index++) CreatePlatform();
        }

        public EarthPlatform Acquire(in EarthPlatformGeometry geometry, float height, float embedDepth)
        {
            for (int index = 0; index < _platforms.Count; index++)
            {
                EarthPlatform platform = _platforms[index];
                if (platform.gameObject.activeSelf) continue;
                platform.Initialize(_nextId++, in geometry, height, embedDepth);
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
            platform.Configure(platformMaterial, profile, physicsFeelProfile);
            go.SetActive(false);
            _platforms.Add(platform);
            return platform;
        }
    }
}
