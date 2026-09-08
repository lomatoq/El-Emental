using System;
using Elemental.Presentation.DistantScenery;
using Elemental.Runtime.Physics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    /// <summary>Explicit appearance-only update. Reuses the actual arena material without modifying it.</summary>
    public static class DistantBackdropArenaMaterialBinding
    {
        private const string ArenaMaterialPath = "Assets/Elemental/Content/GraphicsV5/Materials/RumbleArenaSandstone.mat";
        private const string ProfilePath = "Assets/Elemental/Content/Environment/DistantStone/DistantBackdropProfile.asset";

        [MenuItem("Elemental/Environment/Bind Distant Backdrop To Arena Material")]
        public static void BindActiveScene() => Bind(SceneManager.GetActiveScene());

        public static int Bind(Scene scene)
        {
            if(Application.isPlaying || !scene.IsValid() || !scene.isLoaded || scene.name!="EarthCoreSlice")
                throw new InvalidOperationException("Open EarthCoreSlice in Edit Mode to bind the existing backdrop material.");
            Material expected=AssetDatabase.LoadAssetAtPath<Material>(ArenaMaterialPath);
            if(expected==null)throw new InvalidOperationException("The actual arena sandstone material is missing.");
            DistantBackdrop backdrop=null;
            Material actual=null;
            foreach(GameObject root in scene.GetRootGameObjects())
            {
                foreach(var candidate in root.GetComponentsInChildren<DistantBackdrop>(true))
                {
                    if(backdrop!=null)throw new InvalidOperationException("Multiple backdrop owners; resolve ownership first.");
                    backdrop=candidate;
                }
                foreach(var owner in root.GetComponentsInChildren<EarthArenaStructure>(true))
                    foreach(var renderer in owner.GetComponentsInChildren<MeshRenderer>(true))
                        foreach(var shared in renderer.sharedMaterials)
                            if(shared==expected)actual=shared;
            }
            if(actual==null)throw new InvalidOperationException("No existing EarthArenaStructure renderer uses the canonical arena material. No fallback palette was created.");
            if(backdrop==null || backdrop.profile==null || AssetDatabase.GetAssetPath(backdrop.profile)!=ProfilePath)
                throw new InvalidOperationException("Install the owned distant backdrop profile before binding it.");
            var serialized=new SerializedObject(backdrop);
            Transform generated=serialized.FindProperty("generatedRoot").objectReferenceValue as Transform;
            if(generated==null || generated.parent!=backdrop.transform)
                throw new InvalidOperationException("The backdrop's owned generated root is missing or reparented.");
            MeshRenderer[] targets=generated.GetComponentsInChildren<MeshRenderer>(true);
            if(targets.Length==0)throw new InvalidOperationException("The existing backdrop has no generated renderers.");
            // Rebuild already uses profile.material for every LOD, so this binding persists.
            Undo.RecordObject(backdrop.profile,"Bind backdrop to actual arena material");
            backdrop.profile.material=actual;
            EditorUtility.SetDirty(backdrop.profile);
            foreach(var renderer in targets)
            {
                Undo.RecordObject(renderer,"Bind backdrop to actual arena material");
                renderer.sharedMaterial=actual;
                EditorUtility.SetDirty(renderer);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject=backdrop;
            Debug.Log("Bound "+targets.Length+" distant backdrop renderers to the existing arena shared material: "+AssetDatabase.GetAssetPath(actual),backdrop);
            return targets.Length;
        }
    }
}
