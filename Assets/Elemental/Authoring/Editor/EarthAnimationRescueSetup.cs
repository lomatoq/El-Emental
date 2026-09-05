using System;
using Elemental.Authoring.Editor.MotionMatching;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Elemental.Presentation.Animation;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Elemental.Authoring.Editor
{
    /// <summary>Changes animation content only; never rebuilds or rewrites the gameplay scene.</summary>
    public static class EarthAnimationRescueSetup
    {
        public const string LibraryPath = "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibrary.asset";
        public const string MagicProfilePath = "Assets/Elemental/Content/Profiles/EarthMagicMotionProfile.asset";

        [MenuItem("Elemental/Character/Repair September Animation Bindings")]
        public static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before repairing and baking animation bindings.");
            EarthHumanoidMotionSetup.ConfigureUprightIdleImporter();
            AnimationClip idle = null;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(EarthHumanoidMotionSetup.IdlePath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                { idle = clip; break; }
            if (idle == null || !idle.isHumanMotion || idle.length < 0.1f)
                throw new InvalidOperationException("X Bot Idle must import as a valid authored Humanoid cycle.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            if (controller == null) throw new InvalidOperationException("Earth character Animator controller is missing.");
            int repairedTrees = 0;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(EarthHumanoidMotionSetup.ControllerPath))
            {
                if (asset is not BlendTree tree ||
                    (tree.name != "Earth Locomotion 2D" && tree.name != "Earth Turn In Place")) continue;
                ChildMotion[] children = tree.children;
                for (int index = 0; index < children.Length; index++)
                {
                    bool neutral = tree.name == "Earth Turn In Place"
                        ? Mathf.Abs(children[index].threshold) < 0.0001f
                        : children[index].position.sqrMagnitude < 0.0001f;
                    if (neutral) children[index].motion = idle;
                }
                tree.children = children;
                EditorUtility.SetDirty(tree);
                repairedTrees++;
            }
            if (repairedTrees < 2) throw new InvalidOperationException("Expected both locomotion and turn blend trees.");
            EditorUtility.SetDirty(controller);
            EnsureMantleState(controller, idle, false);
            var library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(LibraryPath);
            if (library == null) throw new InvalidOperationException("Earth EAMM motion library is missing.");
            int idleRecipes = 0;
            foreach (MotionClipRecipe recipe in library.clips)
            {
                if (recipe.role != MotionClipRole.Idle) continue;
                recipe.clip = idle;
                recipe.semantic = MotionSemantic.NeutralIdle;
                recipe.loop = true;
                idleRecipes++;
            }
            if (idleRecipes == 0) throw new InvalidOperationException("The motion library has no explicit idle recipe.");
            EditorUtility.SetDirty(library);
            EnsureMagicProfile();
            AssetDatabase.SaveAssets();
            MotionLibraryBuilder.Bake(library);
            Debug.Log($"[Animation Rescue] Upright idle bound to {repairedTrees} trees and {idleRecipes} EAMM recipe(s); database rebaked. Scene untouched.");
        }

        public static EarthMagicMotionProfile EnsureMagicProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EarthMagicMotionProfile>(MagicProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<EarthMagicMotionProfile>();
                AssetDatabase.CreateAsset(profile, MagicProfilePath);
            }
            if (!profile.Validate(out string error)) throw new InvalidOperationException(error);
            return profile;
        }

        public static void BindMagicProfileToLoadedScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Bind profiles in Edit Mode.");
            var profile = EnsureMagicProfile();
            Scene scene = SceneManager.GetActiveScene();
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (var presentation in root.GetComponentsInChildren<HumanoidCharacterPresentation>(true))
                {
                    Undo.RecordObject(presentation, "Bind magic animation timing profile");
                    presentation.ConfigureMagicMotionProfile(profile);
                    EditorUtility.SetDirty(presentation); count++;
                }
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Animation Rescue] Bound the 11-slot timing profile to {count} presentations. Save the scene after review.");
        }

        public static void ConfigureMantleClip(string assetPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Bind mantle in Edit Mode.");
            EarthHumanoidMotionSetup.ConfigureAuthoredMotionImporter(assetPath);
            AnimationClip mantle = null;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                { mantle = clip; break; }
            if (mantle == null || !mantle.isHumanMotion) throw new InvalidOperationException("Mantle needs the imported Humanoid climb clip.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            if (controller == null) throw new InvalidOperationException("Missing animation controller.");
            EnsureMantleState(controller, mantle, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Animation Rescue] Motor-normalized Mantle state uses {assetPath}; root motion stays off.");
        }

        private static void EnsureMantleState(AnimatorController controller, AnimationClip clip, bool authored)
        {
            bool hasTime = false;
            foreach (var parameter in controller.parameters) if (parameter.name == "MantleTime") hasTime = true;
            if (!hasTime) controller.AddParameter("MantleTime", AnimatorControllerParameterType.Float);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = null;
            foreach (var child in machine.states) if (child.state.name == "Mantle") state = child.state;
            // Re-running the idle repair must not overwrite an imported climb.
            if (state != null && !authored && state.motion != null) return;
            if (state == null) state = machine.AddState("Mantle");
            state.motion = clip;
            state.tag = authored ? "AuthoredMantle" : "MantleFallback";
            state.timeParameterActive = true;
            state.timeParameter = "MantleTime";
            state.transitions = Array.Empty<AnimatorStateTransition>();
            EditorUtility.SetDirty(state); EditorUtility.SetDirty(controller);
            if (!authored) Debug.Log("[Animation Rescue] Mantle uses an upright idle + ledge-hand fallback. A reviewed authored climb clip is still required for final animation acceptance.");
        }
    }
}
