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
        public const string CharacterModelPath =
            "Assets/Elemental/Content/Characters/Linebreaker/Linebreaker.fbx";
        private const string CanonicalMixamoModelPath =
            "Assets/ThirdParty/Mixamo/X Bot.fbx";
        public const string ControllerPath = "Assets/Elemental/Content/Animation/KayKitMage.controller";
        public const string PresentationProfilePath = "Assets/Elemental/Content/Profiles/CharacterPresentationProfile.asset";

        private static readonly string[] MixamoAnimationPaths =
        {
            "Assets/ThirdParty/Mixamo/X Bot@Walking.fbx",
            "Assets/ThirdParty/Mixamo/X Bot@Walking Backwards.fbx",
            "Assets/ThirdParty/Mixamo/X Bot@Punching.fbx"
        };

        private static readonly string[] AnimationPaths =
        {
            "Assets/ThirdParty/KayKit/Animations/Rig_Medium_CombatRanged.fbx",
            "Assets/ThirdParty/KayKit/Animations/Rig_Medium_General.fbx",
            "Assets/ThirdParty/KayKit/Animations/Rig_Medium_MovementAdvanced.fbx",
            "Assets/ThirdParty/KayKit/Animations/Rig_Medium_MovementBasic.fbx"
        };

        private static readonly string[] SecondaryDeformBones =
        {
            "Secondary_Tail_01",
            "Secondary_Tail_02",
            "Secondary_Tail_03",
            "Secondary_HairLock",
            "Secondary_Belt_L_01",
            "Secondary_Belt_L_02",
            "Secondary_Belt_R_01",
            "Secondary_Belt_R_02"
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
            for (int index = 0; index < MixamoAnimationPaths.Length; index++)
                ValidatePayload(MixamoAnimationPaths[index], true, errors);
            for (int index = 0; index < EarthHumanoidMotionSetup.CuratedPaths.Length; index++)
                ValidatePayload(EarthHumanoidMotionSetup.CuratedPaths[index], true, errors);
            for (int index = 0; index < AnimationPaths.Length; index++)
                ValidatePayload(AnimationPaths[index], true, errors);
            ValidateAvatar(errors);
            ValidateSecondaryRig(errors);
            ValidateSharedMixamoAvatar(errors);
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
                errors.Add("Linebreaker Avatar is missing or not a valid Humanoid. Set Rig/Animation Type to Humanoid and reimport.");
                return;
            }
            HumanDescription description = avatar.humanDescription;
            string[] required = { "Hips", "LeftHand", "RightHand", "LeftFoot", "RightFoot" };
            for (int requiredIndex = 0; requiredIndex < required.Length; requiredIndex++)
            {
                bool found = false;
                for (int humanIndex = 0; humanIndex < description.human.Length; humanIndex++)
                    if (description.human[humanIndex].humanName == required[requiredIndex]) { found = true; break; }
                if (!found) errors.Add($"Linebreaker Humanoid mapping is missing required bone '{required[requiredIndex]}'.");
            }
        }

        private static void ValidateSecondaryRig(List<string> errors)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (prefab == null) return;

            Transform[] hierarchy = prefab.GetComponentsInChildren<Transform>(true);
            var byName = new Dictionary<string, Transform>(hierarchy.Length, StringComparer.Ordinal);
            for (int index = 0; index < hierarchy.Length; index++)
            {
                Transform item = hierarchy[index];
                if (!byName.TryAdd(item.name, item))
                    errors.Add($"Linebreaker secondary rig has duplicate transform name '{item.name}'.");
            }

            RequireParent(byName, "Secondary_HelmetAnchor", "mixamorig:Head", errors);
            RequireParent(byName, "Secondary_HairLock", "Secondary_HelmetAnchor", errors);
            RequireParent(byName, "Secondary_Tail_01", "Secondary_HelmetAnchor", errors);
            RequireParent(byName, "Secondary_Tail_02", "Secondary_Tail_01", errors);
            RequireParent(byName, "Secondary_Tail_03", "Secondary_Tail_02", errors);
            RequireParent(byName, "Secondary_Belt_L_01", "mixamorig:Hips", errors);
            RequireParent(byName, "Secondary_Belt_L_02", "Secondary_Belt_L_01", errors);
            RequireParent(byName, "Secondary_Belt_R_01", "mixamorig:Hips", errors);
            RequireParent(byName, "Secondary_Belt_R_02", "Secondary_Belt_R_01", errors);

            SkinnedMeshRenderer renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer == null || renderer.sharedMesh == null)
            {
                errors.Add("Linebreaker secondary rig has no readable SkinnedMeshRenderer asset.");
                return;
            }

            var weightedVertexCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            BoneWeight[] weights = renderer.sharedMesh.boneWeights;
            for (int vertexIndex = 0; vertexIndex < weights.Length; vertexIndex++)
            {
                BoneWeight weight = weights[vertexIndex];
                CountWeightedVertex(renderer, weightedVertexCounts, weight.boneIndex0, weight.weight0);
                CountWeightedVertex(renderer, weightedVertexCounts, weight.boneIndex1, weight.weight1);
                CountWeightedVertex(renderer, weightedVertexCounts, weight.boneIndex2, weight.weight2);
                CountWeightedVertex(renderer, weightedVertexCounts, weight.boneIndex3, weight.weight3);
            }

            for (int index = 0; index < SecondaryDeformBones.Length; index++)
            {
                string bone = SecondaryDeformBones[index];
                int minimumVertices = bone.StartsWith("Secondary_Belt_", StringComparison.Ordinal)
                    ? 24
                    : 100;
                weightedVertexCounts.TryGetValue(bone, out int actual);
                if (actual < minimumVertices)
                    errors.Add(
                        $"Linebreaker secondary bone '{bone}' influences {actual} imported vertices; " +
                        $"expected at least {minimumVertices}. Re-run the Blender secondary-weight gate.");
            }
        }

        private static void RequireParent(
            IReadOnlyDictionary<string, Transform> hierarchy,
            string childName,
            string expectedParent,
            List<string> errors)
        {
            if (!hierarchy.TryGetValue(childName, out Transform child))
            {
                errors.Add($"Linebreaker secondary rig is missing '{childName}'.");
                return;
            }
            string actualParent = child.parent != null ? child.parent.name : "<root>";
            if (!string.Equals(actualParent, expectedParent, StringComparison.Ordinal))
                errors.Add(
                    $"Linebreaker secondary bone '{childName}' must be parented to " +
                    $"'{expectedParent}', not '{actualParent}'.");
        }

        private static void CountWeightedVertex(
            SkinnedMeshRenderer renderer,
            IDictionary<string, int> counts,
            int boneIndex,
            float weight)
        {
            if (weight <= 0.001f || renderer.bones == null ||
                boneIndex < 0 || boneIndex >= renderer.bones.Length ||
                renderer.bones[boneIndex] == null)
                return;
            string name = renderer.bones[boneIndex].name;
            counts[name] = counts.TryGetValue(name, out int current) ? current + 1 : 1;
        }

        private static void ValidateSharedMixamoAvatar(List<string> errors)
        {
            // Motion clips share the source X Bot Avatar, then Mecanim retargets
            // them onto Linebreaker's separate Humanoid Avatar at runtime. Using
            // the presentation model here made the validator reject the exact
            // importer contract configured by EarthHumanoidMotionSetup.
            UnityEngine.Object[] modelAssets = AssetDatabase.LoadAllAssetsAtPath(
                CanonicalMixamoModelPath);
            Avatar canonical = null;
            for (int index = 0; index < modelAssets.Length; index++)
                if (modelAssets[index] is Avatar candidate)
                {
                    canonical = candidate;
                    break;
                }
            if (canonical == null) return;
            for (int index = 0; index < EarthHumanoidMotionSetup.CuratedPaths.Length; index++)
            {
                string path = EarthHumanoidMotionSetup.CuratedPaths[index];
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                if (importer.animationType != ModelImporterAnimationType.Human ||
                    importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther ||
                    importer.sourceAvatar != canonical)
                    errors.Add($"Curated motion does not reuse the canonical X Bot Avatar: {path}.");
            }

            UnityEngine.Object[] crouchAssets = AssetDatabase.LoadAllAssetsAtPath(
                EarthHumanoidMotionSetup.StandToCrouchPath);
            bool hasNeutral = false;
            bool hasTransition = false;
            for (int index = 0; index < crouchAssets.Length; index++)
            {
                if (crouchAssets[index] is not AnimationClip clip ||
                    clip.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
                if (clip.name == EarthHumanoidMotionSetup.NeutralIdleClipName && clip.isLooping)
                    hasNeutral = true;
                if (clip.name == "Standing Idle To Crouch" && !clip.isLooping)
                    hasTransition = true;
            }
            if (!hasNeutral || !hasTransition)
                errors.Add("StandToCrouch FBX must expose a looping neutral idle segment and a non-looping surf transition.");
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
            RequireParameter(controller, "GaitRate", AnimatorControllerParameterType.Float, errors);
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
            RequireParameter(controller, "Turn", AnimatorControllerParameterType.Float, errors);
            RequireParameter(controller, "Surfing", AnimatorControllerParameterType.Bool, errors);
            RequireParameter(controller, "HardLanding", AnimatorControllerParameterType.Bool, errors);
            RequireParameter(controller, "EarthMotionTime", AnimatorControllerParameterType.Float, errors);
            for (int slot = 1; slot <= 11; slot++)
                RequireParameter(controller, $"EarthPose{slot:00}", AnimatorControllerParameterType.Float, errors);
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
                if (controller.layers[2].defaultWeight > 0.001f)
                    errors.Add("Impact layer must be runtime-weighted from zero instead of overriding the hero continuously.");
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
                errors.Add("KayKitMage Earth Cast state must use the curated semantic BlendTree.");
                return;
            }
            if (!cast.timeParameterActive || cast.timeParameter != "EarthMotionTime")
                errors.Add("KayKitMage Earth Cast must be phase-scrubbed by EarthMotionTime so held magic cannot freeze on the final frame.");
            if (tree.blendType != BlendTreeType.Direct || !IsDirectBlendNormalized(tree) ||
                tree.children.Length < 11)
                errors.Add("KayKitMage hero cast BlendTree must expose eleven normalized direct pose weights.");
            var unique = new HashSet<Motion>();
            ChildMotion[] children = tree.children;
            for (int index = 0; index < children.Length; index++)
            {
                if (children[index].motion != null) unique.Add(children[index].motion);
                if (children[index].directBlendParameter != $"EarthPose{index + 1:00}")
                    errors.Add($"KayKitMage direct cast child {index} has the wrong semantic weight parameter.");
            }
            if (unique.Count < 8)
                errors.Add("KayKitMage hero cast BlendTree must use at least eight distinct authored clips.");
        }

        private static bool IsDirectBlendNormalized(BlendTree tree)
        {
            if (tree == null) return false;
            var serializedTree = new SerializedObject(tree);
            SerializedProperty property = serializedTree.FindProperty("m_NormalizedBlendValues");
            return property != null && property.boolValue;
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
            if (tree.blendType != BlendTreeType.Simple1D ||
                tree.blendParameter != "Speed")
                errors.Add("KayKitMage leg locomotion must be a phase-coherent Speed 1D BlendTree; procedural body steering owns Turn.");
            if (!state.speedParameterActive || state.speedParameter != "GaitRate")
                errors.Add("KayKitMage locomotion cadence must be driven by the bounded GaitRate parameter.");
            if (tree.useAutomaticThresholds)
                errors.Add("KayKitMage locomotion thresholds must use authored metre-per-second values, not automatic normalization.");
            ChildMotion[] children = tree.children;
            if (children.Length != 4)
            {
                errors.Add("KayKitMage leg locomotion must contain Idle/WalkBack/Walk/Run speed samples without one-shot turn clips.");
                return;
            }
            for (int index = 0; index < children.Length; index++)
            {
                if (children[index].motion == null)
                    errors.Add($"KayKitMage locomotion child {index} has no Motion.");
            }
            AnimatorState surf = FindState(stateMachine, "Surf Crouch");
            if (surf?.motion == null)
                errors.Add("KayKitMage base layer must contain a valid 'Surf Crouch' motion.");
            AnimatorStateTransition[] anyTransitions = stateMachine.anyStateTransitions;
            for (int index = 0; index < anyTransitions.Length; index++)
                if (anyTransitions[index].destinationState != null &&
                    anyTransitions[index].destinationState.name == "Surf Enter")
                    errors.Add("Surf Enter may not be an AnyState destination because it retriggers the crouch clip while surfing.");
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
