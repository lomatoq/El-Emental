using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Online
{
    [CreateAssetMenu(menuName = "Elemental/Online Geometry Catalog")]
    public sealed class OnlineGeometryCatalog : ScriptableObject
    {
        [SerializeField] private Mesh[] meshes = System.Array.Empty<Mesh>();
        [SerializeField] private Material[] materials = System.Array.Empty<Material>();
        [SerializeField] private PhysicsMaterial[] physicsMaterials = System.Array.Empty<PhysicsMaterial>();
        [SerializeField] private ulong contentHash;
        private readonly Dictionary<Mesh, uint> _meshIds = new Dictionary<Mesh, uint>();
        private readonly Dictionary<Material, uint> _materialIds = new Dictionary<Material, uint>();
        private readonly Dictionary<PhysicsMaterial, uint> _physicsIds = new Dictionary<PhysicsMaterial, uint>();
        public ulong ContentHash => contentHash;
        public void Configure(Mesh[] meshAssets, Material[] materialAssets, ulong hash)
        { meshes = meshAssets; materials = materialAssets; contentHash = hash; BuildIndex(); }
        public void Configure(Mesh[] meshAssets, Material[] materialAssets, PhysicsMaterial[] physicsAssets, ulong hash)
        { physicsMaterials = physicsAssets; Configure(meshAssets, materialAssets, hash); }
        private void OnEnable() => BuildIndex();
        private void BuildIndex()
        {
            _meshIds.Clear(); _materialIds.Clear(); _physicsIds.Clear();
            for (int i = 0; i < meshes.Length; i++) if (meshes[i] != null) _meshIds[meshes[i]] = (uint)i + 1;
            for (int i = 0; i < materials.Length; i++) if (materials[i] != null) _materialIds[materials[i]] = (uint)i + 1;
            for (int i = 0; i < physicsMaterials.Length; i++) if (physicsMaterials[i] != null) _physicsIds[physicsMaterials[i]] = (uint)i + 1;
        }
        public uint MeshId(Mesh mesh) => mesh != null && _meshIds.TryGetValue(mesh, out uint id) ? id : 0;
        public uint MaterialId(Material material) => material != null && _materialIds.TryGetValue(material, out uint id) ? id : 0;
        public Mesh ResolveMesh(uint id) => id > 0 && id <= meshes.Length ? meshes[id - 1] : null;
        public Material ResolveMaterial(uint id) => id > 0 && id <= materials.Length ? materials[id - 1] : null;
        public uint PhysicsId(PhysicsMaterial material) => material != null && _physicsIds.TryGetValue(material, out uint id) ? id : 0;
        public PhysicsMaterial ResolvePhysics(uint id) => id > 0 && id <= physicsMaterials.Length ? physicsMaterials[id - 1] : null;
    }
}
