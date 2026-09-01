using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Curated, replaceable Humanoid motion library. Gameplay publishes semantic
    /// pose slots; this editor adapter is the only place that knows FBX filenames.
    /// </summary>
    [InitializeOnLoad]
    public static class EarthHumanoidMotionSetup
    {
        public const string ControllerPath = "Assets/Elemental/Content/Animation/KayKitMage.controller";
        private const string Root = "Assets/ThirdParty/Mixamo/";
        public const string CanonicalCharacterPath = Root + "X Bot.fbx";
        public const string NeutralIdleClipName = "XBot Neutral Idle";
        public const string NeutralWalkClipName = "XBot Walk Neutral";

        public const string WalkPath = Root + "X Bot@Walking.fbx";
        public const string WalkBackPath = Root + "X Bot@Walking Backwards.fbx";
        public const string InjuredIdlePath = Root + "X Bot@Injured Idle.fbx";
        public const string WheelbarrowDumpPath = Root + "X Bot@Wheelbarrow Dump.fbx";
        public const string UppercutHitPath = Root + "X Bot@Receiving An Uppercut.fbx";
        public const string FallingPath = Root + "X Bot@Falling.fbx";
        public const string HardLandingPath = Root + "X Bot@Hard Landing.fbx";
        public const string LeftTurnPath = Root + "X Bot@Left Turn.fbx";
        public const string FallingRollPath = Root + "X Bot@Falling To Roll.fbx";
        public const string LeadJabPath = Root + "X Bot@Lead Jab.fbx";
        public const string PunchComboPath = Root + "X Bot@Punch Combo.fbx";
        public const string PunchingPath = Root + "X Bot@Punching.fbx";
        public const string MmaKickPath = Root + "X Bot@Mma Kick.fbx";
        public const string SideHitPath = Root + "X Bot@Hit To Side Of Body.fbx";
        public const string MagicAttack05Path = Root + "X Bot@Standing 2H Magic Attack 05.fbx";
        public const string Magic1HAttack03Path = Root + "X Bot@Standing 1H Magic Attack 03.fbx";
        public const string MagicArea02Path = Root + "X Bot@Standing 2H Magic Area Attack 02.fbx";
        public const string Magic2HAttack03Path = Root + "X Bot@Standing 2H Magic Attack 03.fbx";
        public const string Magic1HCast01Path = Root + "X Bot@Standing 1H Cast Spell 01.fbx";
        public const string Magic2HCast01Path = Root + "X Bot@Standing 2H Cast Spell 01.fbx";
        public const string StandToCrouchPath = Root + "X Bot@Standing Idle To Crouch.fbx";
        public const string CrouchIdlePath = Root + "X Bot@Crouch Idle.fbx";
        public const string KayKitDirectionalDodgePath =
            "Assets/ThirdParty/KayKit/Animations/Rig_Medium_MovementAdvanced.fbx";
        public const string KayKitMovementBasicPath =
            "Assets/ThirdParty/KayKit/Animations/Rig_Medium_MovementBasic.fbx";
        public const string KayKitGeneralPath =
            "Assets/ThirdParty/KayKit/Animations/Rig_Medium_General.fbx";
        public const string JumpStartClipName = "Jump_Start";
        public const string JumpLandClipName = "Jump_Land";
        public const string KayKitCombatRangedPath =
            "Assets/ThirdParty/KayKit/Animations/Rig_Medium_CombatRanged.fbx";
        public const string MagicRaiseClipName = "Ranged_Magic_Raise";
        public const string MagicShootClipName = "Ranged_Magic_Shoot";
        public const string MagicSpellcastingClipName = "Ranged_Magic_Spellcasting";
        public const string MagicSpellcastingLongClipName = "Ranged_Magic_Spellcasting_Long";
        public const string MagicSummonClipName = "Ranged_Magic_Summon";

        public static readonly string[] CuratedPaths =
        {
            WalkPath,
            WalkBackPath,
            InjuredIdlePath,
            WheelbarrowDumpPath,
            UppercutHitPath,
            FallingPath,
            HardLandingPath,
            LeftTurnPath,
            FallingRollPath,
            LeadJabPath,
            PunchComboPath,
            PunchingPath,
            MmaKickPath,
            SideHitPath,
            Root + "Standing 2H Magic Attack 05.fbx",
            Magic1HAttack03Path,
            MagicAttack05Path,
            MagicArea02Path,
            Magic2HAttack03Path,
            Magic1HCast01Path,
            Magic2HCast01Path,
            StandToCrouchPath,
            CrouchIdlePath
        };

        static EarthHumanoidMotionSetup()
        {
            EditorApplication.delayCall += EnsureCurrentTreeAfterScriptReload;
        }

        private static void EnsureCurrentTreeAfterScriptReload()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null || IsCurrent(controller)) return;
            ConfigureCuratedImporters();
            UpgradeController(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[Elemental] Earth Humanoid motion tree migrated to direct semantic crossfades.");
        }

        private static bool IsCurrent(AnimatorController controller)
        {
            if (!AreCuratedImportersCurrent()) return false;
            bool hasFinalWeight = false;
            bool hasMotionTime = false;
            bool hasGaitRate = false;
            bool hasImpactTrigger = false;
            bool hasDodgeTrigger = false;
            bool hasDodgeX = false;
            bool hasDodgeY = false;
            bool hasContactMetadata = false;
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].name == "EarthPose11" &&
                    parameters[index].type == AnimatorControllerParameterType.Float)
                    hasFinalWeight = true;
                if (parameters[index].name == "EarthMotionTime" &&
                    parameters[index].type == AnimatorControllerParameterType.Float)
                    hasMotionTime = true;
                if (parameters[index].name == "GaitRate" &&
                    parameters[index].type == AnimatorControllerParameterType.Float)
                    hasGaitRate = true;
                if (parameters[index].name == "Impact" &&
                    parameters[index].type == AnimatorControllerParameterType.Trigger)
                    hasImpactTrigger = true;
                if (parameters[index].name == "Dodge" &&
                    parameters[index].type == AnimatorControllerParameterType.Trigger)
                    hasDodgeTrigger = true;
                if (parameters[index].name == "DodgeX" &&
                    parameters[index].type == AnimatorControllerParameterType.Float)
                    hasDodgeX = true;
                if (parameters[index].name == "DodgeY" &&
                    parameters[index].type == AnimatorControllerParameterType.Float)
                    hasDodgeY = true;
                if (parameters[index].name ==
                        Elemental.Simulation.Characters.EarthAnimationClipMetadata.LeftFootContact &&
                    parameters[index].type == AnimatorControllerParameterType.Float)
                    hasContactMetadata = true;
            }
            if (!hasFinalWeight || !hasMotionTime || !hasGaitRate || !hasImpactTrigger ||
                !hasDodgeTrigger || !hasDodgeX || !hasDodgeY || !hasContactMetadata)
                return false;
            AnimatorState knockdownRecovery = controller.layers.Length > 0
                ? FindState(controller.layers[0].stateMachine, "Knockdown Recovery")
                : null;
            AnimatorState jump = controller.layers.Length > 0
                ? FindState(controller.layers[0].stateMachine, "Jump")
                : null;
            AnimatorState land = controller.layers.Length > 0
                ? FindState(controller.layers[0].stateMachine, "Land")
                : null;
            if (controller.layers.Length == 0 ||
                FindState(controller.layers[0].stateMachine, "Moving Land") == null ||
                jump?.motion is not AnimationClip jumpClip ||
                !string.Equals(jumpClip.name, JumpStartClipName, StringComparison.Ordinal) ||
                !string.Equals(
                    AssetDatabase.GetAssetPath(jumpClip),
                    KayKitMovementBasicPath,
                    StringComparison.Ordinal) ||
                land?.motion is not AnimationClip landClip ||
                !string.Equals(landClip.name, JumpLandClipName, StringComparison.Ordinal) ||
                !string.Equals(
                    AssetDatabase.GetAssetPath(landClip),
                    KayKitMovementBasicPath,
                    StringComparison.Ordinal) ||
                knockdownRecovery == null ||
                knockdownRecovery.transitions.Length != 0 ||
                FindState(controller.layers[0].stateMachine, "Dodge") == null ||
                FindState(controller.layers[0].stateMachine, "Turn In Place") == null)
                return false;
            if (controller.layers.Length < 3 ||
                FindState(controller.layers[2].stateMachine, "Recoil") == null)
                return false;
            AnimatorState locomotion = FindState(controller.layers[0].stateMachine, "Locomotion");
            if (locomotion == null || !locomotion.speedParameterActive ||
                locomotion.speedParameter != "GaitRate") return false;
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ControllerPath);
            bool hasAuthoredRun = false;
            for (int index = 0; index < assets.Length; index++)
                if (assets[index] is BlendTree locomotionTree &&
                    locomotionTree.name == "Earth Locomotion 2D")
                {
                    if (locomotionTree.blendType != BlendTreeType.Simple1D ||
                        locomotionTree.blendParameter != "Speed" ||
                        locomotionTree.children.Length != 4)
                        return false;
                    ChildMotion[] children = locomotionTree.children;
                    for (int childIndex = 0; childIndex < children.Length; childIndex++)
                        if (Mathf.Abs(children[childIndex].position.y - 6f) < 0.001f &&
                            children[childIndex].motion != null &&
                            children[childIndex].motion.name == "Running_A" &&
                            Mathf.Abs(children[childIndex].timeScale - 1f) < 0.001f)
                            hasAuthoredRun = true;
                }
            if (!hasAuthoredRun) return false;
            for (int index = 0; index < assets.Length; index++)
                if (assets[index] is BlendTree tree && tree.name == "Earth Curated Casts")
                {
                    AnimatorState cast = FindState(controller.layers[1].stateMachine, "Earth Cast");
                    return tree.blendType == BlendTreeType.Direct && tree.children.Length == 11 &&
                           cast != null && cast.timeParameterActive &&
                           cast.timeParameter == "EarthMotionTime" &&
                           HasDirectClip(tree, 1, MagicRaiseClipName) &&
                           HasDirectClip(tree, 2, MagicSummonClipName) &&
                           HasDirectClip(tree, 4, MagicShootClipName) &&
                           HasDirectClip(tree, 5, "Lead Jab") &&
                           HasDirectClip(tree, 6, MagicSpellcastingLongClipName) &&
                           HasDirectClip(tree, 8, MagicRaiseClipName) &&
                           HasDirectClip(tree, 9, MagicSummonClipName) &&
                           HasDirectClip(tree, 10, MagicShootClipName) &&
                           HasDirectClip(tree, 11, MagicSpellcastingClipName);
                }
            return false;
        }

        private static bool HasDirectClip(BlendTree tree, int slot, string expectedName)
        {
            ChildMotion[] children = tree.children;
            string expectedParameter = PoseWeightParameter(slot);
            for (int index = 0; index < children.Length; index++)
                if (string.Equals(
                        children[index].directBlendParameter,
                        expectedParameter,
                        StringComparison.Ordinal) &&
                    children[index].motion != null &&
                    string.Equals(
                        children[index].motion.name,
                        expectedName,
                        StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool AreCuratedImportersCurrent()
        {
            ModelImporter canonical = AssetImporter.GetAtPath(CanonicalCharacterPath) as ModelImporter;
            if (canonical == null || canonical.humanDescription.hasTranslationDoF)
                return false;
            Avatar sharedAvatar = LoadAvatar(CanonicalCharacterPath);
            if (sharedAvatar == null || !sharedAvatar.isValid || !sharedAvatar.isHuman) return false;
            for (int pathIndex = 0; pathIndex < CuratedPaths.Length; pathIndex++)
            {
                string path = CuratedPaths[pathIndex];
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null || importer.animationType != ModelImporterAnimationType.Human ||
                    importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther ||
                    importer.sourceAvatar != sharedAvatar || !importer.importAnimation ||
                    importer.humanDescription.hasTranslationDoF)
                    return false;
                ModelImporterClipAnimation[] clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                if (clips == null || clips.Length == 0) return false;
                bool loop = IsLooping(path);
                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    ModelImporterClipAnimation clip = clips[clipIndex];
                    bool clipLoop = string.Equals(clip.name, NeutralIdleClipName, StringComparison.Ordinal) ||
                                    (!string.Equals(path, StandToCrouchPath, StringComparison.Ordinal) && loop);
                    if (clip.loopTime != clipLoop || clip.loopPose != clipLoop ||
                        clip.lockRootRotation || clip.lockRootHeightY || clip.lockRootPositionXZ ||
                        !clip.keepOriginalOrientation || clip.keepOriginalPositionY ||
                        !clip.keepOriginalPositionXZ || !clip.heightFromFeet)
                        return false;
                }
            }
            return true;
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
        {
            if (machine == null) return null;
            ChildAnimatorState[] states = machine.states;
            for (int index = 0; index < states.Length; index++)
                if (states[index].state != null && states[index].state.name == stateName)
                    return states[index].state;
            return null;
        }

        [MenuItem("Elemental Suite/Character/Rebuild Curated Earth Motion Tree")]
        public static void RebuildCuratedEarthMotionTree()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureCuratedImporters();
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                throw new InvalidOperationException($"AnimatorController is missing: {ControllerPath}");
            UpgradeController(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Elemental] Curated Earth Humanoid motion tree rebuilt: locomotion, directional dodge, surf, air, casting and impact lanes.");
        }

        public static void ConfigureCuratedImporters()
        {
            ConfigureCanonicalAvatarImporter();
            for (int index = 0; index < CuratedPaths.Length; index++)
                ConfigureMotionImporter(CuratedPaths[index]);
        }

        private static void ConfigureCanonicalAvatarImporter()
        {
            ModelImporter importer = AssetImporter.GetAtPath(CanonicalCharacterPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException(
                    $"Canonical Mixamo character is missing: {CanonicalCharacterPath}");
            HumanDescription human = importer.humanDescription;
            bool dirty = importer.animationType != ModelImporterAnimationType.Human ||
                         importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                         importer.sourceAvatar != null ||
                         human.hasTranslationDoF;
            if (!dirty) return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            human.hasTranslationDoF = false;
            importer.humanDescription = human;
            importer.SaveAndReimport();
        }

        public static void UpgradeController(AnimatorController controller)
        {
            if (controller == null || controller.layers == null || controller.layers.Length < 3) return;
            AddParameterIfMissing(controller, "Turn", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "Surfing", AnimatorControllerParameterType.Bool);
            AddParameterIfMissing(controller, "HardLanding", AnimatorControllerParameterType.Bool);
            AddParameterIfMissing(controller, "Impact", AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, "Dodge", AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, "DodgeX", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "DodgeY", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "EarthMotionTime", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "GaitRate", AnimatorControllerParameterType.Float);
            for (int metadataIndex = 0;
                 metadataIndex < Elemental.Simulation.Characters.EarthAnimationClipMetadata.CurveCount;
                 metadataIndex++)
                AddParameterIfMissing(
                    controller,
                    Elemental.Simulation.Characters.EarthAnimationClipMetadata.CurveName(metadataIndex),
                    AnimatorControllerParameterType.Float);
            for (int slot = 1; slot <= 11; slot++)
                AddParameterIfMissing(controller, PoseWeightParameter(slot), AnimatorControllerParameterType.Float);
            ConfigureBaseLayer(controller);
            ConfigureMagicLayer(controller);
            ConfigureImpactLayer(controller);
            AnimatorControllerLayer[] layers = controller.layers;
            for (int index = 0; index < layers.Length; index++) layers[index].iKPass = true;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureMotionImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException($"Curated Mixamo motion is missing or not imported: {path}");

            Avatar sharedAvatar = LoadAvatar(CanonicalCharacterPath);
            if (sharedAvatar == null || !sharedAvatar.isValid || !sharedAvatar.isHuman)
                throw new InvalidOperationException(
                    "The canonical Mixamo X Bot Humanoid Avatar is missing or invalid.");
            HumanDescription human = importer.humanDescription;
            bool dirty = importer.animationType != ModelImporterAnimationType.Human ||
                         importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther ||
                         importer.sourceAvatar != sharedAvatar || !importer.importAnimation ||
                         human.hasTranslationDoF;
            importer.animationType = ModelImporterAnimationType.Human;
            // All downloaded clips target the exact same Mixamo X Bot skeleton.
            // Sharing the model Avatar prevents each FBX from inventing a subtly
            // different T-pose and twisting hips/knees while clips crossfade.
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sharedAvatar;
            importer.importAnimation = true;
            if (human.hasTranslationDoF)
            {
                human.hasTranslationDoF = false;
                importer.humanDescription = human;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (string.Equals(path, WalkPath, StringComparison.Ordinal))
            {
                clips = ConfigureWalkNeutral(clips, out bool walkNeutralAdded);
                dirty |= walkNeutralAdded;
            }
            if (string.Equals(path, StandToCrouchPath, StringComparison.Ordinal))
            {
                clips = ConfigureNeutralIdleAndCrouchTransition(clips);
                dirty = true;
            }
            bool loop = IsLooping(path);
            for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
            {
                ModelImporterClipAnimation clip = clips[clipIndex];
                bool clipLoop = string.Equals(clip.name, NeutralIdleClipName, StringComparison.Ordinal) ||
                                (!string.Equals(path, StandToCrouchPath, StringComparison.Ordinal) && loop);
                if (clip.loopTime != clipLoop || clip.loopPose != clipLoop || clip.lockRootRotation ||
                    clip.lockRootHeightY || clip.lockRootPositionXZ ||
                    !clip.keepOriginalOrientation || clip.keepOriginalPositionY ||
                    !clip.keepOriginalPositionXZ || !clip.heightFromFeet)
                    dirty = true;
                clip.loopTime = clipLoop;
                clip.loopPose = clipLoop;
                // Canonical translation and rotation belong to PlanetMotor. Extract
                // every FBX root track instead of baking it back into the Humanoid
                // bones; applyRootMotion=false then discards the track while preserving
                // the authored limb motion. Baking a landing's vertical trajectory
                // moved the visible hips metres away from the physics capsule.
                clip.lockRootRotation = false;
                clip.lockRootHeightY = false;
                clip.lockRootPositionXZ = false;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = false;
                clip.keepOriginalPositionXZ = true;
                clip.heightFromFeet = true;
            }
            importer.clipAnimations = clips;
            if (dirty) importer.SaveAndReimport();
        }

        private static ModelImporterClipAnimation[] ConfigureNeutralIdleAndCrouchTransition(
            ModelImporterClipAnimation[] sourceClips)
        {
            if (sourceClips == null || sourceClips.Length == 0)
                return sourceClips;
            bool hasNeutral = false;
            bool hasCrouchTransition = false;
            for (int index = 0; index < sourceClips.Length; index++)
            {
                hasNeutral |= string.Equals(
                    sourceClips[index].name,
                    NeutralIdleClipName,
                    StringComparison.Ordinal);
                hasCrouchTransition |= string.Equals(
                    sourceClips[index].name,
                    "Standing Idle To Crouch",
                    StringComparison.Ordinal);
            }
            // Replacing existing ModelImporterClipAnimation instances discards
            // their custom contact/phase/safe-exit curves. Once both authored
            // subclips exist, retain the importer objects and normalize only the
            // root/loop fields in ConfigureImporter below.
            if (hasNeutral && hasCrouchTransition)
                return sourceClips;
            ModelImporterClipAnimation source = sourceClips[0];
            for (int index = 0; index < sourceClips.Length; index++)
                if (!string.Equals(sourceClips[index].name, NeutralIdleClipName, StringComparison.Ordinal))
                {
                    source = sourceClips[index];
                    break;
                }
            var neutral = new ModelImporterClipAnimation
            {
                name = NeutralIdleClipName,
                takeName = source.takeName,
                firstFrame = source.firstFrame,
                lastFrame = Mathf.Min(source.lastFrame, source.firstFrame + 1f),
                loopTime = true,
                loopPose = true,
                lockRootRotation = false,
                lockRootHeightY = false,
                lockRootPositionXZ = false,
                keepOriginalOrientation = true,
                keepOriginalPositionY = false,
                keepOriginalPositionXZ = true,
                heightFromFeet = true
            };
            var crouch = new ModelImporterClipAnimation
            {
                name = "Standing Idle To Crouch",
                takeName = source.takeName,
                firstFrame = source.firstFrame,
                lastFrame = source.lastFrame,
                loopTime = false,
                loopPose = false,
                lockRootRotation = false,
                lockRootHeightY = false,
                lockRootPositionXZ = false,
                keepOriginalOrientation = true,
                keepOriginalPositionY = false,
                keepOriginalPositionXZ = true,
                heightFromFeet = true
            };
            return new[] { neutral, crouch };
        }

        private static ModelImporterClipAnimation[] ConfigureWalkNeutral(
            ModelImporterClipAnimation[] sourceClips,
            out bool added)
        {
            added = false;
            if (sourceClips == null || sourceClips.Length == 0) return sourceClips;
            for (int index = 0; index < sourceClips.Length; index++)
                if (string.Equals(
                        sourceClips[index].name,
                        NeutralWalkClipName,
                        StringComparison.Ordinal))
                    return sourceClips;

            ModelImporterClipAnimation source = sourceClips[0];
            var neutral = new ModelImporterClipAnimation
            {
                name = NeutralWalkClipName,
                takeName = source.takeName,
                firstFrame = source.firstFrame,
                lastFrame = Mathf.Min(source.lastFrame, source.firstFrame + 1f),
                loopTime = true,
                loopPose = true,
                lockRootRotation = false,
                lockRootHeightY = false,
                lockRootPositionXZ = false,
                keepOriginalOrientation = true,
                keepOriginalPositionY = false,
                keepOriginalPositionXZ = true,
                heightFromFeet = true
            };
            var expanded = new ModelImporterClipAnimation[sourceClips.Length + 1];
            Array.Copy(sourceClips, expanded, sourceClips.Length);
            expanded[expanded.Length - 1] = neutral;
            added = true;
            return expanded;
        }

        private static bool IsLooping(string path) =>
            string.Equals(path, WalkPath, StringComparison.Ordinal) ||
            string.Equals(path, WalkBackPath, StringComparison.Ordinal) ||
            string.Equals(path, InjuredIdlePath, StringComparison.Ordinal) ||
            string.Equals(path, CrouchIdlePath, StringComparison.Ordinal) ||
            string.Equals(path, FallingPath, StringComparison.Ordinal) ||
            string.Equals(path, LeftTurnPath, StringComparison.Ordinal);

        private static void ConfigureBaseLayer(AnimatorController controller)
        {
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = FindOrCreateState(machine, "Locomotion");
            AnimatorState jump = FindOrCreateState(machine, "Jump");
            AnimatorState fall = FindOrCreateState(machine, "Fall");
            AnimatorState land = FindOrCreateState(machine, "Land");
            AnimatorState movingLand = FindOrCreateState(machine, "Moving Land");
            AnimatorState hardLand = FindOrCreateState(machine, "Hard Land");
            AnimatorState knockdownRecovery = FindOrCreateState(machine, "Knockdown Recovery");
            AnimatorState dodge = FindOrCreateState(machine, "Dodge");
            AnimatorState turnInPlace = FindOrCreateState(machine, "Turn In Place");
            AnimatorState surfEnter = FindOrCreateState(machine, "Surf Enter");
            AnimatorState surf = FindOrCreateState(machine, "Surf Crouch");

            BlendTree tree = FindOrCreateBlendTree(controller, "Earth Locomotion 2D");
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            // The old Mixamo walking file is a one-step take: one foot never
            // leaves contact and the loop seam visibly snaps. Use complete,
            // licensed KayKit cycles for the locomotion lane; Mixamo remains in
            // the authored action/magic lanes where those clips are complete.
            AnimationClip idle = LoadRequiredClip(KayKitGeneralPath, "Idle_A");
            AnimationClip walkBack = LoadRequiredClip(
                KayKitDirectionalDodgePath,
                "Walking_Backwards");
            AnimationClip walk = LoadRequiredClip(KayKitMovementBasicPath, "Walking_A");
            AnimationClip run = LoadClip(KayKitMovementBasicPath, "Running_A") ?? walk;
            tree.children = new[]
            {
                Child(idle, 0f, 0f),
                Child(walkBack, 0f, -2f),
                Child(walk, 0f, 2f),
                // Running_A is a licensed authored Humanoid cycle. A real run at
                // the high-speed sample prevents the previous slow-walk moonwalk;
                // GaitRate remains bounded and PlanetMotor still owns displacement.
                Child(run, 0f, 6f)
            };
            locomotion.motion = tree;
            locomotion.speed = 1f;
            locomotion.speedParameter = "GaitRate";
            locomotion.speedParameterActive = true;
            machine.defaultState = locomotion;

            // Jump_Start is an exact licensed anticipation/take-off clip already in
            // the 51-profile catalog. PlanetMotor still owns displacement and the
            // Animator continues to discard imported root motion.
            jump.motion = LoadRequiredClip(KayKitMovementBasicPath, JumpStartClipName);
            fall.motion = LoadClip(FallingPath) ?? idle;
            land.motion = LoadRequiredClip(KayKitMovementBasicPath, JumpLandClipName);
            movingLand.motion = LoadClip(FallingRollPath) ?? LoadClip(HardLandingPath) ?? idle;
            hardLand.motion = LoadClip(HardLandingPath) ?? land.motion;
            // Ragdoll recovery must read as regaining stance, not as an intentional
            // acrobatic dodge. Keep Falling-To-Roll exclusively on the moving-land
            // lane and use the grounded hard-landing recovery here until dedicated
            // front/back get-up clips are imported.
            knockdownRecovery.motion = hardLand.motion;
            knockdownRecovery.speed = 1.15f;
            BlendTree dodgeTree = FindOrCreateBlendTree(controller, "Earth Directional Dodge");
            dodgeTree.blendType = BlendTreeType.FreeformDirectional2D;
            dodgeTree.blendParameter = "DodgeX";
            dodgeTree.blendParameterY = "DodgeY";
            dodgeTree.useAutomaticThresholds = false;
            dodgeTree.children = new[]
            {
                Child(LoadClip(KayKitDirectionalDodgePath, "Dodge_Forward") ?? idle, 0f, 1f),
                Child(LoadClip(KayKitDirectionalDodgePath, "Dodge_Backward") ?? idle, 0f, -1f),
                Child(LoadClip(KayKitDirectionalDodgePath, "Dodge_Left") ?? idle, -1f, 0f),
                Child(LoadClip(KayKitDirectionalDodgePath, "Dodge_Right") ?? idle, 1f, 0f)
            };
            dodge.motion = dodgeTree;
            dodge.speed = 1f;
            AnimationClip leftTurn = LoadClip(LeftTurnPath) ?? idle;
            BlendTree turnTree = FindOrCreateBlendTree(controller, "Earth Turn In Place");
            turnTree.blendType = BlendTreeType.Simple1D;
            turnTree.blendParameter = "Turn";
            turnTree.useAutomaticThresholds = false;
            turnTree.children = new[]
            {
                Child(leftTurn, 0f, -1f),
                Child(idle, 0f, 0f),
                Child(leftTurn, 0f, 1f, true)
            };
            turnInPlace.motion = turnTree;
            turnInPlace.speed = 1f;
            surfEnter.motion = LoadClip(StandToCrouchPath, "Standing Idle To Crouch") ??
                               LoadClip(CrouchIdlePath) ?? idle;
            surf.motion = LoadClip(CrouchIdlePath) ?? idle;

            var locomotionTransitions = new GeneratedStateTransitionWriter(locomotion);
            var jumpTransitions = new GeneratedStateTransitionWriter(jump);
            var fallTransitions = new GeneratedStateTransitionWriter(fall);
            var landTransitions = new GeneratedStateTransitionWriter(land);
            var movingLandTransitions = new GeneratedStateTransitionWriter(movingLand);
            var hardLandTransitions = new GeneratedStateTransitionWriter(hardLand);
            var knockdownRecoveryTransitions = new GeneratedStateTransitionWriter(knockdownRecovery);
            var dodgeTransitions = new GeneratedStateTransitionWriter(dodge);
            var turnInPlaceTransitions = new GeneratedStateTransitionWriter(turnInPlace);
            var surfEnterTransitions = new GeneratedStateTransitionWriter(surfEnter);
            var surfTransitions = new GeneratedStateTransitionWriter(surf);
            var anyStateTransitions = new GeneratedAnyStateTransitionWriter(machine);

            AddConditionTransition(locomotionTransitions, jump, "Grounded", AnimatorConditionMode.IfNot, 0f, 0.07f);
            AddConditionTransition(jumpTransitions, fall, "VerticalSpeed", AnimatorConditionMode.Less, 0f, 0.07f);
            AddConditionTransition(fallTransitions, hardLand, "Grounded", AnimatorConditionMode.If, 0f, 0.06f,
                "HardLanding", AnimatorConditionMode.If, 0f);
            AddConditionTransition(fallTransitions, land, "Grounded", AnimatorConditionMode.If, 0f, 0.06f,
                "HardLanding", AnimatorConditionMode.IfNot, 0f);
            AddExitTransition(landTransitions, locomotion, 0.70f, 0.10f);
            AddExitTransition(movingLandTransitions, locomotion, 0.58f, 0.08f);
            AddExitTransition(hardLandTransitions, locomotion, 0.82f, 0.12f);
            // No controller-owned exit here. HumanoidCharacterPresentation keeps
            // the selected pose-matched state authoritative until the supported
            // authored recovery-exit marker, then EarthTransitionDirector resumes
            // ordinary locomotion as the sole base-state writer.
            AnimatorStateTransition dodgeTransition = anyStateTransitions.Next(dodge);
            ResetTransition(dodgeTransition, dodge);
            dodgeTransition.hasExitTime = false;
            dodgeTransition.exitTime = 0.75f;
            dodgeTransition.duration = 0.045f;
            dodgeTransition.AddCondition(AnimatorConditionMode.If, 0f, "Dodge");
            AddExitTransition(dodgeTransitions, locomotion, 0.90f, 0.07f);
            AddConditionTransition(locomotionTransitions, surfEnter, "Surfing", AnimatorConditionMode.If, 0f, 0.10f);
            AddConditionTransition(landTransitions, surfEnter, "Surfing", AnimatorConditionMode.If, 0f, 0.10f);
            AddConditionTransition(movingLandTransitions, surfEnter, "Surfing", AnimatorConditionMode.If, 0f, 0.10f);
            AddConditionTransition(hardLandTransitions, surfEnter, "Surfing", AnimatorConditionMode.If, 0f, 0.10f);
            AddExitTransition(surfEnterTransitions, surf, 0.72f, 0.08f, "Surfing");
            AddConditionTransition(surfEnterTransitions, locomotion, "Surfing", AnimatorConditionMode.IfNot, 0f, 0.09f);
            AddConditionTransition(surfTransitions, locomotion, "Surfing", AnimatorConditionMode.IfNot, 0f, 0.14f);

            locomotionTransitions.Complete();
            jumpTransitions.Complete();
            fallTransitions.Complete();
            landTransitions.Complete();
            movingLandTransitions.Complete();
            hardLandTransitions.Complete();
            knockdownRecoveryTransitions.Complete();
            dodgeTransitions.Complete();
            turnInPlaceTransitions.Complete();
            surfEnterTransitions.Complete();
            surfTransitions.Complete();
            anyStateTransitions.Complete();
        }

        private static void ConfigureMagicLayer(AnimatorController controller)
        {
            AnimatorStateMachine machine = controller.layers[1].stateMachine;
            AnimatorState ready = FindOrCreateState(machine, "Ready");
            AnimatorState cast = FindOrCreateState(machine, "Earth Cast");
            BlendTree tree = FindOrCreateBlendTree(controller, "Earth Curated Casts");
            tree.blendType = BlendTreeType.Direct;
            SetDirectBlendNormalization(tree, true);
            AnimationClip generic = LoadClip(Magic1HCast01Path) ?? LoadFallbackClip("spell", "cast");
            AnimationClip raise = LoadRequiredClip(
                KayKitCombatRangedPath,
                MagicRaiseClipName);
            AnimationClip shoot = LoadRequiredClip(
                KayKitCombatRangedPath,
                MagicShootClipName);
            AnimationClip spellcasting = LoadRequiredClip(
                KayKitCombatRangedPath,
                MagicSpellcastingClipName);
            AnimationClip sustain = LoadRequiredClip(
                KayKitCombatRangedPath,
                MagicSpellcastingLongClipName);
            AnimationClip summon = LoadRequiredClip(
                KayKitCombatRangedPath,
                MagicSummonClipName);
            AnimationClip push = LoadClip(LeadJabPath) ?? generic;
            tree.children = new[]
            {
                DirectChild(raise, 1),
                DirectChild(summon, 2),
                DirectChild(LoadClip(Magic2HCast01Path) ?? generic, 3),
                DirectChild(shoot, 4),
                // Vector push needs a readable authored impulse, not a duplicate
                // projectile-release pose. Lead Jab is the licensed Mixamo lane
                // already used by the runtime VectorPush semantic.
                DirectChild(push, 5),
                DirectChild(sustain, 6),
                DirectChild(LoadClip(Magic2HAttack03Path) ?? generic, 7),
                DirectChild(raise, 8),
                DirectChild(summon, 9),
                DirectChild(shoot, 10),
                DirectChild(spellcasting, 11)
            };
            cast.motion = tree;
            ready.motion = LoadClip(StandToCrouchPath, NeutralIdleClipName) ?? ready.motion;
            cast.timeParameterActive = true;
            cast.timeParameter = "EarthMotionTime";
            var readyTransitions = new GeneratedStateTransitionWriter(ready);
            var castTransitions = new GeneratedStateTransitionWriter(cast);
            var anyStateTransitions = new GeneratedAnyStateTransitionWriter(machine);
            machine.defaultState = ready;
            AddConditionTransition(readyTransitions, cast, "Cast", AnimatorConditionMode.If, 0f, 0.10f);
            AddConditionTransition(castTransitions, ready, "Cast", AnimatorConditionMode.IfNot, 0f, 0.14f);

            readyTransitions.Complete();
            castTransitions.Complete();
            anyStateTransitions.Complete();
        }

        private static void ConfigureImpactLayer(AnimatorController controller)
        {
            AnimatorStateMachine machine = controller.layers[2].stateMachine;
            AnimatorState ready = FindOrCreateState(machine, "Ready");
            AnimatorState recoil = FindOrCreateState(machine, "Recoil");
            recoil.motion = LoadClip(SideHitPath) ?? LoadClip(UppercutHitPath) ?? recoil.motion;
            ready.motion = LoadClip(StandToCrouchPath, NeutralIdleClipName) ?? ready.motion;
            var readyTransitions = new GeneratedStateTransitionWriter(ready);
            var recoilTransitions = new GeneratedStateTransitionWriter(recoil);
            var anyStateTransitions = new GeneratedAnyStateTransitionWriter(machine);
            machine.defaultState = ready;
            AddConditionTransition(readyTransitions, recoil, "Impact", AnimatorConditionMode.If, 0f, 0.035f);
            AddExitTransition(recoilTransitions, ready, 0.72f, 0.16f);

            readyTransitions.Complete();
            recoilTransitions.Complete();
            anyStateTransitions.Complete();
            AnimatorControllerLayer[] layers = controller.layers;
            layers[2].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[2].defaultWeight = 0f;
            controller.layers = layers;
        }

        private static ChildMotion Child(
            Motion motion,
            float x,
            float y,
            bool mirror = false,
            float timeScale = 1f) =>
            new ChildMotion
            {
                motion = motion,
                position = new Vector2(x, y),
                threshold = y,
                timeScale = timeScale,
                mirror = mirror
            };

        private static ChildMotion DirectChild(Motion motion, int slot) =>
            new ChildMotion
            {
                motion = motion,
                directBlendParameter = PoseWeightParameter(slot),
                timeScale = 1f
            };

        private static void SetDirectBlendNormalization(BlendTree tree, bool normalized)
        {
            if (tree == null) return;
            var serializedTree = new SerializedObject(tree);
            SerializedProperty property = serializedTree.FindProperty("m_NormalizedBlendValues");
            if (property == null) return;
            property.boolValue = normalized;
            serializedTree.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string PoseWeightParameter(int slot) => $"EarthPose{slot:00}";

        private static BlendTree FindOrCreateBlendTree(AnimatorController controller, string name)
        {
            string controllerPath = AssetDatabase.GetAssetPath(controller);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(controllerPath);
            for (int index = 0; index < assets.Length; index++)
                if (assets[index] is BlendTree tree && tree.name == name) return tree;
            var created = new BlendTree { name = name, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(created, controller);
            return created;
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine machine, string name)
        {
            ChildAnimatorState[] states = machine.states;
            for (int index = 0; index < states.Length; index++)
                if (states[index].state != null && states[index].state.name == name) return states[index].state;
            return machine.AddState(name);
        }

        private static void AddConditionTransition(
            GeneratedStateTransitionWriter from,
            AnimatorState to,
            string parameter,
            AnimatorConditionMode mode,
            float threshold,
            float duration,
            string secondParameter = null,
            AnimatorConditionMode secondMode = AnimatorConditionMode.If,
            float secondThreshold = 0f)
        {
            AnimatorStateTransition transition = from.Next(to);
            ResetTransition(transition, to);
            transition.hasExitTime = false;
            transition.exitTime = 0.9f;
            transition.duration = duration;
            transition.AddCondition(mode, threshold, parameter);
            if (!string.IsNullOrEmpty(secondParameter))
                transition.AddCondition(secondMode, secondThreshold, secondParameter);
        }

        private static void AddExitTransition(
            GeneratedStateTransitionWriter from,
            AnimatorState to,
            float exitTime,
            float duration,
            string requiredBool = null)
        {
            AnimatorStateTransition transition = from.Next(to);
            ResetTransition(transition, to);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = duration;
            if (!string.IsNullOrEmpty(requiredBool))
                transition.AddCondition(AnimatorConditionMode.If, 0f, requiredBool);
        }

        private static void ResetTransition(
            AnimatorStateTransition transition,
            AnimatorState destination)
        {
            transition.name = string.Empty;
            transition.conditions = Array.Empty<AnimatorCondition>();
            transition.destinationStateMachine = null;
            transition.destinationState = destination;
            transition.isExit = false;
            transition.solo = false;
            transition.mute = false;
            transition.offset = 0f;
            transition.hasFixedDuration = true;
            transition.orderedInterruption = true;
            transition.canTransitionToSelf = false;
            transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
        }

        // Animator transitions are persistent controller subassets. Replacing the
        // transition arrays only detaches them and makes every rebuild append new
        // orphan subassets. Reuse the generated slots in order and use Unity's
        // removal APIs for excess slots so the persistent asset is deleted too.
        private sealed class GeneratedStateTransitionWriter
        {
            private readonly AnimatorState _state;
            private readonly AnimatorStateTransition[] _existing;
            private int _nextIndex;

            public GeneratedStateTransitionWriter(AnimatorState state)
            {
                _state = state ?? throw new ArgumentNullException(nameof(state));
                _existing = state.transitions ?? Array.Empty<AnimatorStateTransition>();
            }

            public AnimatorStateTransition Next(AnimatorState destination)
            {
                AnimatorStateTransition transition = _nextIndex < _existing.Length
                    ? _existing[_nextIndex]
                    : null;
                _nextIndex++;
                return transition != null ? transition : _state.AddTransition(destination);
            }

            public void Complete()
            {
                for (int index = _existing.Length - 1; index >= _nextIndex; index--)
                    if (_existing[index] != null)
                        _state.RemoveTransition(_existing[index]);
                if (_state.transitions.Length != _nextIndex)
                    throw new InvalidOperationException(
                        $"Failed to reconcile generated transitions for state '{_state.name}'.");
            }
        }

        private sealed class GeneratedAnyStateTransitionWriter
        {
            private readonly AnimatorStateMachine _stateMachine;
            private readonly AnimatorStateTransition[] _existing;
            private int _nextIndex;

            public GeneratedAnyStateTransitionWriter(AnimatorStateMachine stateMachine)
            {
                _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
                _existing = stateMachine.anyStateTransitions ?? Array.Empty<AnimatorStateTransition>();
            }

            public AnimatorStateTransition Next(AnimatorState destination)
            {
                AnimatorStateTransition transition = _nextIndex < _existing.Length
                    ? _existing[_nextIndex]
                    : null;
                _nextIndex++;
                return transition != null
                    ? transition
                    : _stateMachine.AddAnyStateTransition(destination);
            }

            public void Complete()
            {
                for (int index = _existing.Length - 1; index >= _nextIndex; index--)
                    if (_existing[index] != null)
                    {
                        if (!_stateMachine.RemoveAnyStateTransition(_existing[index]))
                            throw new InvalidOperationException(
                                $"Failed to remove generated AnyState transition from '{_stateMachine.name}'.");
                    }
                if (_stateMachine.anyStateTransitions.Length != _nextIndex)
                    throw new InvalidOperationException(
                        $"Failed to reconcile generated AnyState transitions for '{_stateMachine.name}'.");
            }
        }

        private static void AddParameterIfMissing(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int index = 0; index < parameters.Length; index++)
                if (parameters[index].name == name) return;
            controller.AddParameter(name, type);
        }

        private static AnimationClip LoadClip(string path, string preferredName = null)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            AnimationClip fallback = null;
            for (int index = 0; index < assets.Length; index++)
                if (assets[index] is AnimationClip clip &&
                    !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    fallback ??= clip;
                    if (!string.IsNullOrEmpty(preferredName) &&
                        string.Equals(clip.name, preferredName, StringComparison.OrdinalIgnoreCase))
                        return clip;
                }
            return fallback;
        }

        private static AnimationClip LoadRequiredClip(string path, string clipName)
        {
            AnimationClip clip = LoadClip(path, clipName);
            if (clip != null && string.Equals(clip.name, clipName, StringComparison.Ordinal))
                return clip;
            throw new InvalidOperationException(
                $"Required authored animation clip '{clipName}' is missing from '{path}'. " +
                "Controller rebuild stopped instead of relabeling or substituting content.");
        }

        private static Avatar LoadAvatar(string path)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int index = 0; index < assets.Length; index++)
                if (assets[index] is Avatar avatar) return avatar;
            return null;
        }

        private static AnimationClip LoadFallbackClip(params string[] terms)
        {
            string[] paths =
            {
                "Assets/ThirdParty/KayKit/Animations/Rig_Medium_General.fbx",
                "Assets/ThirdParty/KayKit/Animations/Rig_Medium_MovementBasic.fbx",
                "Assets/ThirdParty/KayKit/Animations/Rig_Medium_MovementAdvanced.fbx",
                "Assets/ThirdParty/KayKit/Animations/Rig_Medium_CombatRanged.fbx"
            };
            var clips = new List<AnimationClip>(32);
            for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(paths[pathIndex]);
                for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                    if (assets[assetIndex] is AnimationClip clip &&
                        !clip.name.StartsWith("__preview__", StringComparison.Ordinal)) clips.Add(clip);
            }
            for (int term = 0; term < terms.Length; term++)
                for (int index = 0; index < clips.Count; index++)
                    if (clips[index].name.IndexOf(terms[term], StringComparison.OrdinalIgnoreCase) >= 0)
                        return clips[index];
            return clips.Count > 0 ? clips[0] : null;
        }
    }
}
