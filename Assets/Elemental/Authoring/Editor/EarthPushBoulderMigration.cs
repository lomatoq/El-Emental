using System;
using Elemental.Runtime.Physics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class EarthPushBoulderMigration
    {
        [MenuItem("Tools/Elemental/Repair Two Push Boulders")]
        public static void RepairShippingBoulders()
        {
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Repair the authored boulders outside Play Mode.");
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                GravityWorldBehaviour gravity = null;
                EarthRockDebrisPool debris = null;
                Transform group = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    gravity ??= root.GetComponentInChildren<GravityWorldBehaviour>(true);
                    debris ??= root.GetComponentInChildren<EarthRockDebrisPool>(true);
                    if (root.name == "Magic Push Boulders") group = root.transform;
                }
                if (gravity == null || debris == null || group == null)
                    throw new InvalidOperationException("Shipping gravity, debris pool or Magic Push Boulders root is missing.");
                Repair(group.Find("Light Push Boulder"), 0xD3B00001u, gravity, debris);
                Repair(group.Find("Heavy Push Boulder"), 0xD3B00002u, gravity, debris);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save repaired shipping boulders.");
                Debug.Log("[Elemental] Repaired only Light/Heavy Push Boulder: shared mass, gravity and destructible loose-rock owner.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void Repair(Transform target, uint id, GravityWorldBehaviour gravity, EarthRockDebrisPool debris)
        {
            if (target == null) throw new InvalidOperationException("Expected named push boulder is missing.");
            Rigidbody body = target.GetComponent<Rigidbody>();
            Collider collider = target.GetComponent<Collider>();
            if (body == null || collider == null) throw new InvalidOperationException($"{target.name} lacks its existing body/collider.");
            GravityBody adapter = target.GetComponent<GravityBody>() ?? Undo.AddComponent<GravityBody>(target.gameObject);
            EarthDestructibleDecorRock rock = target.GetComponent<EarthDestructibleDecorRock>() ??
                Undo.AddComponent<EarthDestructibleDecorRock>(target.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[] { body, adapter, rock }, "Repair push boulder");
            adapter.Configure(gravity, body);
            rock.Configure(id, body, collider, adapter, debris, Mathf.Max(.1f, collider.bounds.extents.magnitude * .58f), 720f, false);
            PhysicalImpactTarget legacy = target.GetComponent<PhysicalImpactTarget>();
            if (legacy != null) Undo.DestroyObjectImmediate(legacy);
            EditorUtility.SetDirty(body); EditorUtility.SetDirty(adapter); EditorUtility.SetDirty(rock);
        }
    }
}
