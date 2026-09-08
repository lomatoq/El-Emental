using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Elemental.Online.Editor
{
    public static class OnlineGeometryCatalogAuthoring
    {
        public const string CatalogPath = "Assets/Elemental/NetworkingStage2/OnlineGeometryCatalog.asset";
        [MenuItem("Elemental/Online/Bake Geometry Catalog Only")]
        public static void Bake()
        {
            var entries = new SortedDictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
            foreach (string filter in new[] { "t:Mesh", "t:Material", "t:PhysicsMaterial" })
            foreach (string guid in AssetDatabase.FindAssets(filter, new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is Mesh || asset is Material || asset is PhysicsMaterial)) continue;
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string assetGuid, out long localId))
                        throw new InvalidOperationException("Cannot identify online asset: " + path);
                    entries[assetGuid + ":" + localId.ToString(System.Globalization.CultureInfo.InvariantCulture)] = asset;
                }
            }
            var meshes = new List<Mesh>(); var materials = new List<Material>(); var physics = new List<PhysicsMaterial>();
            var signature = new StringBuilder("ElementalOnlineGeometry/1\n");
            foreach (KeyValuePair<string, UnityEngine.Object> entry in entries)
            {
                signature.Append(entry.Key).Append(':').Append(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(entry.Value))).Append('\n');
                if (entry.Value is Mesh mesh) meshes.Add(mesh);
                else if (entry.Value is Material material) materials.Add(material);
                else if (entry.Value is PhysicsMaterial collision) physics.Add(collision);
            }
            if (materials.Count == 0) throw new InvalidOperationException("No authored Earth materials were found.");
            OnlineGeometryCatalog catalog = AssetDatabase.LoadAssetAtPath<OnlineGeometryCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<OnlineGeometryCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.Configure(meshes.ToArray(), materials.ToArray(), physics.ToArray(), OnlineMeshCodec.Hash(Encoding.UTF8.GetBytes(signature.ToString())));
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            Debug.Log($"Online geometry catalog: {meshes.Count} meshes, {materials.Count} materials, {physics.Count} physics materials, hash {catalog.ContentHash:X16}. No scene changed.");
        }
    }
}
