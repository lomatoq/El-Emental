using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Elemental.Online.Editor
{
    /// <summary>
    /// Clones only explicit authored owner roots. Cross-root references are remapped in
    /// one pass; animation assets and untouched world services retain their references.
    /// Caller supplies the player, magic runtime, camera and external owner visuals.
    /// </summary>
    public static class OnlineActorGraphAuthoring
    {
        public sealed class Result
        {
            public GameObject Container;
            public readonly Dictionary<UnityEngine.Object, UnityEngine.Object> Map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            public readonly List<string> ExternalSceneReferences = new List<string>();
            public T CloneOf<T>(T source) where T : UnityEngine.Object => Map.TryGetValue(source, out UnityEngine.Object clone) ? (T)clone : null;
        }

        public static Result CloneOwnerGraph(GameObject[] ownerRoots, Transform parent, Component[] retainSharedComponents)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || ownerRoots == null || ownerRoots.Length == 0)
                throw new InvalidOperationException("Clone the authored online actor graph in Edit mode with explicit roots.");
            for (int i = 0; i < ownerRoots.Length; i++)
            {
                if (ownerRoots[i] == null || EditorUtility.IsPersistent(ownerRoots[i])) throw new InvalidOperationException("Every owner root must belong to the authored scene.");
                for (int j = 0; j < i; j++)
                    if (ownerRoots[i] == ownerRoots[j] || ownerRoots[i].transform.IsChildOf(ownerRoots[j].transform) || ownerRoots[j].transform.IsChildOf(ownerRoots[i].transform))
                        throw new InvalidOperationException("Owner roots must be distinct, non-overlapping scene subtrees.");
            }
            var result = new Result { Container = new GameObject("Online Player Two Authoring") };
            result.Container.SetActive(false); result.Container.transform.SetParent(parent, false);
            var sharedCopies = new List<Component>();
            var copiedServices = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            try
            {
                foreach (GameObject original in ownerRoots)
                {
                    GameObject clone = UnityEngine.Object.Instantiate(original, result.Container.transform, true);
                    clone.name = original.name + " [Online Two]";
                    BuildMap(original.transform, clone.transform, result.Map);
                }
                if (retainSharedComponents != null)
                foreach (Component shared in retainSharedComponents)
                {
                    if (shared == null) throw new InvalidOperationException("A shared service is missing.");
                    if (result.Map.TryGetValue(shared, out UnityEngine.Object copied))
                    { sharedCopies.Add((Component)copied); copiedServices.Add(copied, shared); }
                    result.Map[shared] = shared;
                }
                foreach (Component component in result.Container.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || sharedCopies.Contains(component)) continue;
                    var serialized = new SerializedObject(component); SerializedProperty property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script") continue;
                        UnityEngine.Object reference = property.objectReferenceValue;
                        if (reference == null || EditorUtility.IsPersistent(reference)) continue;
                        // Instantiate already remaps within each root. Replace both original
                        // references across roots and copied shared-service references.
                        if (result.Map.TryGetValue(reference, out UnityEngine.Object mapped)) property.objectReferenceValue = mapped;
                        else if (copiedServices.TryGetValue(reference, out UnityEngine.Object sharedService))
                            property.objectReferenceValue = sharedService;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (Component copy in sharedCopies) UnityEngine.Object.DestroyImmediate(copy);
                foreach (Component component in result.Container.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) throw new InvalidOperationException("The actor graph contains a missing script.");
                    var serialized = new SerializedObject(component); SerializedProperty property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        UnityEngine.Object reference = property.objectReferenceValue;
                        Transform transform = reference is GameObject go ? go.transform : reference is Component target ? target.transform : null;
                        if (transform != null && !EditorUtility.IsPersistent(reference) && !transform.IsChildOf(result.Container.transform))
                            result.ExternalSceneReferences.Add(component.GetType().Name + "." + property.propertyPath + " -> " + transform.name);
                    }
                }
                return result;
            }
            catch { UnityEngine.Object.DestroyImmediate(result.Container); throw; }
        }
        private static void BuildMap(Transform original, Transform clone, Dictionary<UnityEngine.Object, UnityEngine.Object> map)
        {
            map.Add(original.gameObject, clone.gameObject);
            Component[] sources = original.GetComponents<Component>(), targets = clone.GetComponents<Component>();
            if (sources.Length != targets.Length || original.childCount != clone.childCount) throw new InvalidOperationException("The clone changed its hierarchy during authoring.");
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null || targets[i] == null || sources[i].GetType() != targets[i].GetType())
                    throw new InvalidOperationException("The authored actor contains a missing/mismatched script.");
                map.Add(sources[i], targets[i]);
            }
            for (int i = 0; i < original.childCount; i++) BuildMap(original.GetChild(i), clone.GetChild(i), map);
        }
    }
}
