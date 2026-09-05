using System.Linq;
using Elemental.Authoring;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor.MotionMatching
{
    [CustomEditor(typeof(MotionLibraryAsset))]
    public sealed class MotionLibraryAssetInspector : UnityEditor.Editor
    {
        private const string DropFolder = "Assets/Elemental/Content/Characters/MotionDrop";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            Rect drop = GUILayoutUtility.GetRect(0f, 52f, GUILayout.ExpandWidth(true));
            GUI.Box(drop, "Drop Humanoid AnimationClips / FBX here");
            Event current = Event.current;
            if ((current.type == EventType.DragUpdated || current.type == EventType.DragPerform) &&
                drop.Contains(current.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    AddClips(DragAndDrop.objectReferences.OfType<AnimationClip>().ToArray());
                }
                current.Use();
            }

            if (GUILayout.Button("Scan MotionDrop And Add Clips")) ScanDropFolder();
            if (GUILayout.Button("Validate Library")) ValidateLibrary();
            if (GUILayout.Button("Build JLPM / EAMM Database"))
            {
                string libraryPath = AssetDatabase.GetAssetPath(target);
                EditorApplication.delayCall += () =>
                {
                    MotionLibraryAsset fresh = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(libraryPath);
                    if (fresh == null)
                    {
                        Debug.LogError($"[EAMM] Motion library disappeared before build: {libraryPath}");
                        return;
                    }
                    MotionLibraryBuilder.Bake(fresh);
                };
                GUIUtility.ExitGUI();
            }
        }

        private void ScanDropFolder()
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { DropFolder });
            AddClips(guids.SelectMany(guid =>
                    AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__"))
                .ToArray());
        }

        private void AddClips(AnimationClip[] clips)
        {
            MotionLibraryAsset library = (MotionLibraryAsset)target;
            Undo.RecordObject(library, "Add motion clips");
            foreach (AnimationClip clip in clips)
            {
                if (clip == null || library.clips.Any(recipe => recipe != null && recipe.clip == clip)) continue;
                library.clips.Add(new MotionClipRecipe { clip = clip, loop = clip.isLooping });
            }
            EditorUtility.SetDirty(library);
        }

        private void ValidateLibrary()
        {
            var errors = MotionLibraryBuilder.Validate((MotionLibraryAsset)target);
            if (errors.Count == 0) Debug.Log("[EAMM] Motion library validation passed.");
            else Debug.LogError("[EAMM] Motion library validation failed:\n" + string.Join("\n", errors));
        }
    }
}
