using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Runtime.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public sealed class EarthAnimationValidationReport
    {
        internal EarthAnimationValidationReport(List<string> errors) => Errors = errors;
        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
    }

    public static class EarthAnimationAssetValidator
    {
        public const string CharacterModelPath = "Assets/ThirdParty/KayKit/Knight/Knight.fbx";
        public const string ControllerPath = "Assets/Elemental/Content/Animation/KayKitMage.controller";
        public const string PresentationProfilePath = "Assets/Elemental/Content/Profiles/CharacterPresentationProfile.asset";

        private static readonly string[] AnimationPaths =
        {
            "Assets/ThirdParty/KayKit/Animations/Rig_Medium_CombatRanged.fbx",
            "Assets/ThirdParty/KayKit/Animations/Rig_Medium_General.fbx",
            "Assets/ThirdParty/KayKit/Animations/Rig_Medium_MovementAdvanced.fbx",
            "Assets/ThirdParty/KayKit/Animations/Rig_Medium_MovementBasic.fbx"
        };

        [MenuItem("Elemental Suite/Validation/Validate Earth Animation Assets")]
        public static void ValidateAndLog()
        {
            EarthAnimationValidationReport report = ValidateProject();
            if (report.IsValid)
            {
                Debug.Log("[Elemental] Earth animation asset gate passed: LFS payloads, Humanoid Avatar, clips and controller are valid.");
                return;
            }
            for (int index = 0; index < report.Errors.Count; index++)
                Debug.LogError("[Elemental] " + report.Errors[index]);
        }

        public static EarthAnimationValidationReport ValidateProject()
        {
            var errors = new List<string>(16);
            ValidatePayload(CharacterModelPath, false, errors);
            for (int index = 0; index < AnimationPaths.Length; index++)
                ValidatePayload(AnimationPaths[index], true, errors);
            ValidateAvatar(errors);
            ValidateController(errors);
            ValidatePresentationProfile(errors);
            return new EarthAnimationValidationReport(errors);
        }

        public static bool IsGitLfsPointerText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.IndexOf("version https://git-lfs.github.com/spec/v1", StringComparison.Ordinal) >= 0;
        }

        public static bool HasUsableClipDurations(IReadOnlyList<float> durations)
        {
            if (durations == null || durations.Count == 0) return false;
            for (int index = 0; index < durations.Count; index++)
                if (!float.IsFinite(durations[index]) || durations[index] <= 0.01f) return false;
            return true;
        }

        private static void ValidatePayload(string assetPath, bool requireClips, List<string> errors)
        {
            string absolute = Path.GetFullPath(assetPath);
            if (!File.Exists(absolute))
            {
                errors.Add($"Animation asset is missing: {assetPath}. Run 'git lfs pull'.");
                return;
            }
            using (var stream = File.OpenRead(absolute))
            using (var reader = new StreamReader(stream))
            {
                char[] prefix = new char[160];
                int count = reader.Read(prefix, 0, prefix.Length);
                if (IsGitLfsPointerText(new string(prefix, 0, count)))
                {
                    errors.Add($"Animation asset is still a Git LFS pointer: {assetPath}. Run 'git lfs pull'.");
                    return;
                }
            }
            if (!requireClips) return;
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var durations = new List<float>(assets.Length);
            for (int index = 0; index < assets.Length; index++)
            {
                if (assets[index] is not AnimationClip clip || clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    continue;
                durations.Add(clip.length);
            }
            if (!HasUsableClipDurations(durations))
                errors.Add($"Animation FBX has no usable non-zero clips: {assetPath}. Check importer clips and LFS payload.");
        }

        private static void ValidateAvatar(List<string> errors)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(CharacterModelPath);
            Avatar avatar = null;
            for (int index = 0; index < assets.Length; index++)
                if (assets[index] is Avatar candidate) { avatar = candidate; break; }
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                errors.Add("KayKit Knight Avatar is missing or not a valid Humanoid. Set Rig/Animation Type to Humanoid and reimport.");
                return;
            }
            HumanDescription description = avatar.humanDescription;
            string[] required = { "Hips", "LeftHand", "RightHand", "LeftFoot", "RightFoot" };
            for (int requiredIndex = 0; requiredIndex < required.Length; requiredIndex++)
            {
                bool found = false;
                for (int humanIndex = 0; humanIndex < description.human.Length; humanIndex++)
                    if (description.human[humanIndex].humanName == required[requiredIndex]) { found = true; break; }
                if (!found) errors.Add($"KayKit Knight Humanoid mapping is missing required bone '{required[requiredIndex]}'.");
            }
        }

        private static void ValidateController(List<string> errors)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                errors.Add($"AnimatorController is missing: {ControllerPath}.");
                return;
            }
            RequireParameter(controller, "Speed", AnimatorControllerParameterType.Float, errors);
            RequireParameter(controller, "Grounded", AnimatorControllerParameterType.Bool, errors);
            RequireParameter(controller, "VerticalSpeed", AnimatorControllerParameterType.Float, errors);
            RequireParameter(controller, "Cast", AnimatorControllerParameterType.Bool, errors);
            RequireParameter(controller, "CastKind", AnimatorControllerParameterType.Int, errors);
            RequireParameter(controller, "Impact", AnimatorControllerParameterType.Trigger, errors);
            RequireParameter(controller, "EarthEffort", AnimatorControllerParameterType.Float, errors);
            RequireParameter(controller, "EarthBrace", AnimatorControllerParameterType.Float, errors);
            RequireParameter(controller, "EarthGrounding", AnimatorControllerParameterType.Float, errors);
            RequireParameter(controller, "EarthPrecision", AnimatorControllerParameterType.Float, errors);
            RequireParameter(controller, "EarthPhase", AnimatorControllerParameterType.Int, errors);
            RequireParameter(controller, "EarthDialect", AnimatorControllerParameterType.Int, errors);
            RequireParameter(controller, "EarthPose", AnimatorControllerParameterType.Float, errors);
            if (controller.layers == null || controller.layers.Length < 3)
                errors.Add("KayKitMage controller must contain base locomotion, upper-body casting and additive impact layers.");
            else
            {
                if (controller.layers[1].name != "Earth Magic Upper Body")
                    errors.Add("KayKitMage layer 1 must be named 'Earth Magic Upper Body' for runtime weight control.");
                if (controller.layers[1].avatarMask == null)
                    errors.Add("KayKitMage upper-body casting layer has no AvatarMask.");
                ValidateLocomotionBlendTree(controller.layers[0].stateMachine, errors);
                ValidateHeroCastBlendTree(controller.layers[1].stateMachine, errors);
            }
            if (controller.layers == null) return;
            for (int layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
                ValidateStateMachine(controller.layers[layerIndex].stateMachine, controller.layers[layerIndex].name, errors);
        }

        private static void ValidateHeroCastBlendTree(
            AnimatorStateMachine stateMachine,
            List<string> errors)
        {
            AnimatorState cast = FindState(stateMachine, "Earth Cast");
            if (cast == null || cast.motion is not BlendTree tree)
            {
                errors.Add("KayKitMage Earth Cast state must use the eight-pose hero BlendTree.");
                return;
            }
            if (tree.blendParameter != "EarthPose" || tree.children.Length < 8)
                errors.Add("KayKitMage hero cast BlendTree must expose at least eight EarthPose values.");
            var unique = new HashSet<Motion>();
            ChildMotion[] children = tree.children;
            for (int index = 0; index < children.Length; index++)
                if (children[index].motion != null) unique.Add(children[index].motion);
            if (unique.Count < 6)
                errors.Add("KayKitMage hero cast BlendTree must use at least six distinct authored clips.");
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
        {
            if (machine == null) return null;
            ChildAnimatorState[] states = machine.states;
            for (int index = 0; index < states.Length; index++)
                if (states[index].state != null && states[index].state.name == stateName)
                    return states[index].state;
            ChildAnimatorStateMachine[] children = machine.stateMachines;
            for (int index = 0; index < children.Length; index++)
            {
                AnimatorState found = FindState(children[index].stateMachine, stateName);
                if (found != null) return found;
            }
            return null;
        }

        private static void ValidateLocomotionBlendTree(
            AnimatorStateMachine stateMachine,
            List<string> errors)
        {
            AnimatorState state = stateMachine != null ? stateMachine.defaultState : null;
            if (state == null || state.motion is not BlendTree tree)
            {
                errors.Add("KayKitMage base default state must use a locomotion BlendTree.");
                return;
            }
            if (tree.blendParameter != "Speed")
                errors.Add("KayKitMage locomotion BlendTree must use the Speed parameter.");
            if (tree.useAutomaticThresholds)
                errors.Add("KayKitMage locomotion thresholds must use authored metre-per-second values, not automatic normalization.");
            ChildMotion[] children = tree.children;
            if (children.Length != 3 ||
                Mathf.Abs(children[0].threshold - 0f) > 0.01f ||
                Mathf.Abs(children[1].threshold - 2f) > 0.01f ||
                Mathf.Abs(children[2].threshold - 6f) > 0.01f)
                errors.Add("KayKitMage locomotion thresholds must be Idle/Walk/Run = 0/2/6 m/s.");
        }

        private static void RequireParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type,
            List<string> errors)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int index = 0; index < parameters.Length; index++)
                if (parameters[index].name == name && parameters[index].type == type) return;
            errors.Add($"KayKitMage controller is missing parameter '{name}' with type {type}.");
        }

        private static void ValidateStateMachine(
            AnimatorStateMachine stateMachine,
            string context,
            List<string> errors)
        {
            if (stateMachine == null)
            {
                errors.Add($"Animator layer '{context}' has no state machine.");
                return;
            }
            ChildAnimatorState[] states = stateMachine.states;
            for (int index = 0; index < states.Length; index++)
            {
                AnimatorState state = states[index].state;
                if (state == null || state.motion == null)
                    errors.Add($"Animator state '{context}/{state?.name ?? "<null>"}' has no Motion.");
            }
            ChildAnimatorStateMachine[] children = stateMachine.stateMachines;
            for (int index = 0; index < children.Length; index++)
                ValidateStateMachine(children[index].stateMachine, context + "/" + children[index].stateMachine.name, errors);
        }

        private static void ValidatePresentationProfile(List<string> errors)
        {
            CharacterPresentationProfile presentation =
                AssetDatabase.LoadAssetAtPath<CharacterPresentationProfile>(PresentationProfilePath);
            if (presentation == null)
            {
                errors.Add($"Character presentation profile is missing: {PresentationProfilePath}.");
                return;
            }
            if (presentation.HumanoidPrefab == null) errors.Add("CharacterPresentationProfile has no Humanoid prefab.");
            if (presentation.AnimatorController == null) errors.Add("CharacterPresentationProfile has no AnimatorController.");
            if (presentation.Avatar == null || !presentation.Avatar.isValid || !presentation.Avatar.isHuman)
                errors.Add("CharacterPresentationProfile Avatar is not a valid Humanoid.");
        }
    }
}
