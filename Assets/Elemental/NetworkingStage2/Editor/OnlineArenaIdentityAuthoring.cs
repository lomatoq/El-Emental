using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Elemental.Online.Editor
{
    /// <summary>Deterministic content identities; never hashes scene YAML containing these same fields.</summary>
    public static class OnlineArenaIdentityAuthoring
    {
        public static void Stamp(EarthOnlineGameplayBinding binding, Scene scene, ulong worldHash = 0)
        {
            if (worldHash == 0) worldHash = WorldHash(scene);
            OnlineArenaAuthoring.Set(binding, "initialWorldHash", worldHash);
            OnlineArenaAuthoring.Set(binding, "buildHash", BuildHash(scene));
        }
        public static ulong BuildHash(Scene scene)
        {
            var signature = new StringBuilder("ElementalOnlineBuild/1\n").Append(Application.unityVersion).Append('\n');
            foreach (string path in CodePaths())
                signature.Append(path.Replace('\\', '/')).Append(':').Append(HashCodeFile(path)).Append('\n');
            foreach (string path in new[] { "Packages/manifest.json", "Packages/packages-lock.json", "ProjectSettings/ProjectVersion.txt" })
            {
                if (!File.Exists(path)) throw new InvalidOperationException("Missing compatibility input: " + path);
                signature.Append(path).Append(':').Append(OnlineMeshCodec.Hash(File.ReadAllBytes(path))).Append('\n');
            }
            foreach (string path in AssetDatabase.GetDependencies(scene.path, true).OrderBy(path => path, StringComparer.Ordinal))
            {
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                signature.Append(AssetDatabase.AssetPathToGUID(path)).Append(':').Append(AssetDatabase.GetAssetDependencyHash(path)).Append('\n');
            }
            var catalog = AssetDatabase.LoadAssetAtPath<OnlineGeometryCatalog>(OnlineGeometryCatalogAuthoring.CatalogPath);
            if (catalog == null || catalog.ContentHash == 0) throw new InvalidOperationException("Bake the exact online geometry catalog first.");
            signature.Append("catalog:").Append(catalog.ContentHash.ToString("X16", CultureInfo.InvariantCulture));
            // Inline authored runtime/input tuning belongs to compatibility too;
            // profile assets alone do not cover serialized executor/motor overrides.
            foreach (GameObject root in scene.GetRootGameObjects().OrderBy(root => root.name, StringComparer.Ordinal))
            {
                if (root.GetComponent<EarthOnlineGameplayBinding>() != null || root.GetComponent<NetworkManager>() != null) continue;
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) throw new InvalidOperationException("Missing authored gameplay script.");
                    string type = component.GetType().FullName;
                    if (!type.StartsWith("Elemental.Runtime.", StringComparison.Ordinal) && !type.StartsWith("Elemental.Input.", StringComparison.Ordinal)) continue;
                    signature.Append(Identity(component)); AppendSerialized(signature, component);
                }
            }
            return OnlineMeshCodec.Hash(Encoding.UTF8.GetBytes(signature.ToString()));
        }
        private static IEnumerable<string> CodePaths()
        {
            var roots = new List<string> { "Assets" };
            // Embedded packages are editable source. Their package version may stay
            // unchanged while motion-matching runtime code changes. Never scan Library.
            if (Directory.Exists("Packages"))
                roots.AddRange(Directory.GetDirectories("Packages").Where(path => File.Exists(Path.Combine(path, "package.json"))));
            return roots.SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories)).Where(path => {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                return extension == ".cs" || extension == ".asmdef" || extension == ".asmref" || extension == ".dll";
            }).OrderBy(path => path.Replace('\\', '/'), StringComparer.Ordinal);
        }
        private static ulong HashCodeFile(string path)
        {
            if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return OnlineMeshCodec.Hash(File.ReadAllBytes(path));
            string canonical = File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n");
            return OnlineMeshCodec.Hash(Encoding.UTF8.GetBytes(canonical));
        }
        public static ulong WorldHash(Scene scene)
        {
            var signature = new StringBuilder("ElementalAuthoredWorld/1\n");
            bool planetFound = false;
            foreach (GameObject root in scene.GetRootGameObjects().OrderBy(root => root.name, StringComparer.Ordinal))
            {
                if (root.GetComponent<EarthOnlineGameplayBinding>() != null || root.GetComponent<NetworkManager>() != null) continue;
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) throw new InvalidOperationException("Missing scene script in " + root.name);
                    if (component is VoxelPlanetBehaviour) planetFound = true;
                    // Settings include the seed/profile, scatter layout and intact/destructible geometry.
                    // All authored transforms include both initial fighter poses. Online helper state is excluded.
                    if (!(component is Transform || component is MeshFilter || component is Renderer || component is Collider ||
                          component is VoxelPlanetBehaviour || component is EarthPlanetRockScatter || component is EarthArenaStructure ||
                          component is EarthDestructibleDecorRock)) continue;
                    signature.Append(Identity(component)).Append('\n'); AppendSerialized(signature, component);
                }
            }
            if (!planetFound) throw new InvalidOperationException("World identity requires the actual authored voxel planet and seed.");
            return OnlineMeshCodec.Hash(Encoding.UTF8.GetBytes(signature.ToString()));
        }
        private static void AppendSerialized(StringBuilder text, Object owner)
        {
            var serialized = new SerializedObject(owner); var property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                // Typed values below are already serialized canonically. Only
                // containers need traversal; reference internals must never add
                // Unity's transient object/entity identity a second time.
                enterChildren = property.propertyType == SerializedPropertyType.Generic;
                if (property.propertyPath == "m_RootOrder" || property.propertyPath == "m_PrefabInstance" ||
                    property.propertyPath == "m_PrefabAsset" || property.propertyPath == "m_CorrespondingSourceObject" ||
                    property.propertyPath == "m_ObjectHideFlags" || property.propertyPath == "m_EditorHideFlags") continue;
                text.Append(property.propertyPath).Append(':').Append((int)property.propertyType).Append('=');
                switch (property.propertyType)
                {
                    case SerializedPropertyType.ObjectReference:
                        Object target = property.objectReferenceValue; text.Append(Identity(target));
                        if (target != null && EditorUtility.IsPersistent(target)) text.Append('@').Append(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(target)));
                        break;
                    case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: case SerializedPropertyType.ArraySize: case SerializedPropertyType.Character:
                    case SerializedPropertyType.LayerMask: text.Append(property.longValue.ToString(CultureInfo.InvariantCulture)); break;
                    case SerializedPropertyType.Boolean: text.Append(property.boolValue ? '1' : '0'); break;
                    case SerializedPropertyType.Float: text.Append(property.doubleValue.ToString("R", CultureInfo.InvariantCulture)); break;
                    case SerializedPropertyType.String: text.Append(property.stringValue.Length).Append(':').Append(property.stringValue); break;
                    case SerializedPropertyType.Vector2: Vector2 v2 = property.vector2Value; Floats(text, v2.x, v2.y); break;
                    case SerializedPropertyType.Vector3: Vector3 v3 = property.vector3Value; Floats(text, v3.x, v3.y, v3.z); break;
                    case SerializedPropertyType.Vector4: Vector4 v4 = property.vector4Value; Floats(text, v4.x, v4.y, v4.z, v4.w); break;
                    case SerializedPropertyType.Quaternion: Quaternion q = property.quaternionValue; Floats(text, q.x, q.y, q.z, q.w); break;
                    case SerializedPropertyType.Color: Color c = property.colorValue; Floats(text, c.r, c.g, c.b, c.a); break;
                    case SerializedPropertyType.Rect: Rect r = property.rectValue; Floats(text, r.x, r.y, r.width, r.height); break;
                    case SerializedPropertyType.Bounds: Bounds b = property.boundsValue; Floats(text, b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z); break;
                    case SerializedPropertyType.AnimationCurve:
                        var curve = property.animationCurveValue; text.Append(curve.preWrapMode).Append('/').Append(curve.postWrapMode);
                        foreach (var key in curve.keys) { Floats(text, key.time, key.value, key.inTangent, key.outTangent, key.inWeight, key.outWeight); text.Append((int)key.weightedMode); }
                        break;
                    // Compound fields are traversed into their primitive children. No instance IDs or locale-dependent JSON.
                }
                text.Append('\n');
            }
        }
        private static void Floats(StringBuilder text, params float[] values)
        { foreach (float value in values) text.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(','); }
        private static string Identity(Object target)
        {
            if (target == null) return "null";
            if (EditorUtility.IsPersistent(target))
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string guid, out long id))
                    throw new InvalidOperationException("Asset has no stable identity: " + target.name);
                return guid + ":" + id.ToString(CultureInfo.InvariantCulture);
            }
            // Saved procedural platform meshes are embedded scene objects, not
            // imported assets. Their content, not a process-local object ID or
            // generated display name, belongs in compatibility.
            if (target is Mesh mesh)
            {
                OnlineMeshCodec.Encode(mesh, out int rawLength, out ulong geometryHash);
                var geometry = new StringBuilder("scene-mesh:").Append(rawLength).Append(':')
                    .Append(geometryHash.ToString("X16", CultureInfo.InvariantCulture)).Append(':');
                Bounds bounds = mesh.bounds;
                Floats(geometry, bounds.center.x, bounds.center.y, bounds.center.z, bounds.size.x, bounds.size.y, bounds.size.z);
                return geometry.ToString();
            }
            if (target is Material || target is PhysicsMaterial)
            {
                var content = new StringBuilder(target.GetType().FullName).Append(':');
                AppendSerialized(content, target);
                return "scene-resource:" + OnlineMeshCodec.Hash(Encoding.UTF8.GetBytes(content.ToString())).ToString("X16", CultureInfo.InvariantCulture);
            }
            Transform transform = target is GameObject go ? go.transform : target is Component component ? component.transform : null;
            if (transform == null) throw new InvalidOperationException("Unsupported authored reference: " + target.name + " (" + target.GetType().FullName + ")");
            string path = transform.name;
            while (transform.parent != null) { path = transform.GetSiblingIndex() + ":" + path; transform = transform.parent; path = transform.name + "/" + path; }
            if (target is Component typed) path += ":" + typed.GetType().FullName + ":" + Array.IndexOf(typed.GetComponents(typed.GetType()), typed);
            return path;
        }
    }
}
