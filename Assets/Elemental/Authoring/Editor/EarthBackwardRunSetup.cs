using System;
using Elemental.Authoring.Editor.MotionMatching;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>Narrow opt-in import and tree repair. Never rebuilds a controller or scene.</summary>
    public static class EarthBackwardRunSetup
    {
        public const string ClipPath = "Assets/ThirdParty/Mixamo/X Bot@Running Backward.fbx";
        public const string MenuPath = "Elemental/Animation/Add Existing Backward Run To Locomotion";
        public const string LibraryPath = "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibrary.asset";

        [MenuItem("Elemental/Animation/Bake Backward Run EAMM Library")]
        public static void BakeBackwardRunLibrary()
        {
            Configure();
            MotionLibraryBuilder.Bake(AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(LibraryPath));
        }

        [MenuItem(MenuPath)]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before importing the backward-run clip.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            BlendTree tree = controller != null && controller.layers.Length > 0
                ? FindLocomotionTree(controller.layers[0].stateMachine)
                : null;
            if (tree == null)
                throw new InvalidOperationException("Base Layer must contain the existing MoveX/MoveY Locomotion Blend Tree.");

            var importer = AssetImporter.GetAtPath(ClipPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing project clip: " + ClipPath);
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            bool implicitClip = clips.Length == 0;
            if (implicitClip) clips = importer.defaultClipAnimations;
            if (clips.Length != 1)
                throw new InvalidOperationException("Backward-run FBX must have one take; refusing to alter multiple authored clips.");

            ModelImporterClipAnimation settings = clips[0];
            ModelImporterClipAnimation[] sourceTakes = importer.defaultClipAnimations;
            if (sourceTakes.Length != 1)
                throw new InvalidOperationException("Backward-run FBX must expose exactly one original take to restore its full range.");
            bool restoredRange = RestoreFullTakeRange(settings, sourceTakes[0]);
            bool changed = implicitClip || restoredRange || !importer.importAnimation ||
                importer.animationType != ModelImporterAnimationType.Human ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                !settings.loopTime || !settings.loopPose || !settings.lockRootRotation ||
                !settings.lockRootHeightY || !settings.lockRootPositionXZ ||
                !settings.keepOriginalOrientation;
            if (changed)
            {
                importer.importAnimation = true;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                if (implicitClip) settings.name = "Running Backward";
                settings.loopTime = true;
                settings.loopPose = true;
                settings.lockRootRotation = true;
                settings.lockRootHeightY = true;
                settings.lockRootPositionXZ = true;
                settings.keepOriginalOrientation = true;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            AnimationClip clip = null;
            Avatar avatar = null;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ClipPath))
            {
                if (asset is Avatar candidateAvatar) avatar = candidateAvatar;
                if (asset is AnimationClip candidate && !candidate.name.StartsWith("__preview__", StringComparison.Ordinal))
                    clip = candidate;
            }
            if (avatar == null || !avatar.isValid || !avatar.isHuman ||
                clip == null || !clip.isHumanMotion || clip.length <= 0f)
                throw new InvalidOperationException("Running Backward must import as a valid Humanoid Avatar/clip. Controller was not changed.");

            bool inserted = EnsureBackwardRunChild(tree, clip);
            if (inserted)
            {
                EditorUtility.SetDirty(tree);
                AssetDatabase.SaveAssetIfDirty(tree);
            }
            var library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(LibraryPath);
            if (library == null)
                throw new InvalidOperationException("Backward run was added to the Animator, but the EAMM library is missing: " + LibraryPath);
            if (EnsureBackwardRunRecipe(library, clip))
            {
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssetIfDirty(library);
            }
            Debug.Log(inserted
                ? "Added Running Backward at MoveX 0 / MoveY -6 and registered its EAMM recipe. Bake Backward Run EAMM Library to refresh the active database."
                : "Running Backward is already present; authored tree settings preserved. Re-bake the EAMM library after importer/content changes.");
        }

        public static bool EnsureBackwardRunRecipe(MotionLibraryAsset library, AnimationClip clip)
        {
            if (library == null || clip == null) throw new ArgumentNullException(library == null ? nameof(library) : nameof(clip));
            const string stableId = "run.backward.mixamo";
            foreach (MotionClipRecipe recipe in library.clips)
            {
                if (recipe == null) continue;
                if (recipe.stableId == stableId && recipe.clip != clip)
                    throw new InvalidOperationException("Backward-run stable ID belongs to another authored clip; refusing to replace it.");
                if (recipe.clip == clip)
                {
                    if (recipe.role != MotionClipRole.Locomotion || recipe.nominalSpeed < 4f ||
                        Mathf.Abs(Mathf.DeltaAngle(recipe.nominalDirection, 180f)) > 1f)
                        throw new InvalidOperationException("Existing backward-run recipe must be searchable Locomotion, speed >=4 and direction 180; authored values were not overwritten.");
                    return false;
                }
            }
            Undo.RecordObject(library, "Register backward-run EAMM recipe");
            library.clips.Add(new MotionClipRecipe
            {
                stableId = stableId, clip = clip, role = MotionClipRole.Locomotion,
                semantic = MotionSemantic.RunBackward, nominalSpeed = 6f,
                nominalDirection = 180f, nominalYaw = 0f, loop = true
            });
            return true;
        }

        public static bool RestoreFullTakeRange(ModelImporterClipAnimation clip, ModelImporterClipAnimation originalTake)
        {
            if (clip == null || originalTake == null)
                throw new ArgumentNullException(clip == null ? nameof(clip) : nameof(originalTake));
            if (originalTake.lastFrame <= originalTake.firstFrame)
                throw new InvalidOperationException("Backward-run original take has no positive frame range.");
            bool changed = !Mathf.Approximately(clip.firstFrame, originalTake.firstFrame) ||
                           !Mathf.Approximately(clip.lastFrame, originalTake.lastFrame);
            clip.firstFrame = originalTake.firstFrame;
            clip.lastFrame = originalTake.lastFrame;
            return changed;
        }

        public static bool EnsureBackwardRunChild(BlendTree tree, AnimationClip clip)
        {
            if (tree == null || clip == null) throw new ArgumentNullException(tree == null ? nameof(tree) : nameof(clip));
            if (tree.blendParameter != "MoveX" || tree.blendParameterY != "MoveY" ||
                tree.blendType != BlendTreeType.FreeformCartesian2D)
                throw new InvalidOperationException("Expected the existing MoveX/MoveY 2D Freeform Cartesian tree.");
            ChildMotion[] children = tree.children;
            foreach (ChildMotion child in children)
                if (child.motion == clip) return false;
            foreach (ChildMotion child in children)
                if ((child.position - new Vector2(0f, -6f)).sqrMagnitude < 0.0001f)
                    throw new InvalidOperationException("MoveY -6 is occupied by another authored motion; no existing child was replaced.");

            Undo.RecordObject(tree, "Add backward-run locomotion child");
            tree.AddChild(clip, new Vector2(0f, -6f));
            return true;
        }

        private static BlendTree FindLocomotionTree(AnimatorStateMachine machine)
        {
            foreach (ChildAnimatorState child in machine.states)
                if (child.state.name == "Locomotion" && child.state.motion is BlendTree tree &&
                    tree.blendParameter == "MoveX" && tree.blendParameterY == "MoveY") return tree;
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                BlendTree tree = FindLocomotionTree(child.stateMachine);
                if (tree != null) return tree;
            }
            return null;
        }
    }
}
