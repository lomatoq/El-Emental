using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Elemental.Simulation.Characters;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Curated, replaceable Humanoid motion library. Gameplay publishes semantic
    /// pose slots; this editor adapter is the only place that knows FBX filenames.
    /// </summary>
    public static class EarthHumanoidMotionSetup
    {
        public const string ControllerPath = "Assets/Elemental/Content/Animation/KayKitMage.controller";
        private const string Root = "Assets/ThirdParty/Mixamo/";
        public const string CanonicalCharacterPath = Root + "X Bot.fbx";
        public const string NeutralIdleClipName = "XBot Neutral Idle";
        public const string NeutralWalkClipName = "XBot Walk Neutral";

        public const string WalkPath = Root + "X Bot@Walking.fbx";
        public const string IdlePath = Root + "X Bot@Idle.fbx";
        public const string WalkBackPath = Root + "X Bot@Walking Backwards.fbx";
        public const string InjuredIdlePath = Root + "X Bot@Injured Idle.fbx";
        public const string WheelbarrowDumpPath = Root + "X Bot@Wheelbarrow Dump.fbx";
        public const string UppercutHitPath = Root + "X Bot@Receiving An Uppercut.fbx";
        public const string FallingPath = Root + "X Bot@Falling.fbx";
        public const string HardLandingPath = Root + "X Bot@Hard Landing.fbx";
        public const string LeftTurnPath = Root + "X Bot@Left Turn.fbx";
        public const string FallingRollPath = Root + "X Bot@Falling To Roll.fbx";
        public const string BackwardRollClipPath = "Assets/Elemental/Content/Animation/XBot Landing Roll Back.anim";
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
            CrouchIdlePath,
            KayKitGeneralPath
        };

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
            bool hasMoveX = false;
            bool hasMoveY = false;
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
                if (parameters[index].name == "MoveX" &&
                    parameters[index].type == AnimatorControllerParameterType.Float)
                    hasMoveX = true;
                if (parameters[index].name == "MoveY" &&
                    parameters[index].type == AnimatorControllerParameterType.Float)
                    hasMoveY = true;
                if (parameters[index].name ==
                        Elemental.Simulation.Characters.EarthAnimationClipMetadata.LeftFootContact &&
                    parameters[index].type == AnimatorControllerParameterType.Float)
                    hasContactMetadata = true;
            }
            if (!hasFinalWeight || !hasMotionTime || !hasGaitRate || !hasImpactTrigger ||
                !hasDodgeTrigger || !hasDodgeX || !hasDodgeY || !hasMoveX || !hasMoveY ||
                !hasContactMetadata)
                return false;
            if (controller.layers.Length == 0 ||
                FindState(controller.layers[0].stateMachine, "Moving Land") == null ||
                FindState(controller.layers[0].stateMachine, "Moving Land Back")?.motion == null ||
                FindState(controller.layers[0].stateMachine, "Knockdown Recovery") == null ||
                FindState(controller.layers[0].stateMachine, "Knockdown Recovery Back") == null ||
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
                    if (locomotionTree.blendType != BlendTreeType.FreeformCartesian2D ||
                        locomotionTree.blendParameter != "MoveX" ||
                        locomotionTree.blendParameterY != "MoveY" ||
                        locomotionTree.children.Length != 6)
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
                           cast != null && cast.timeParameterActive && cast.timeParameter == "EarthMotionTimeA" &&
                           FindState(controller.layers[1].stateMachine, "Earth Cast B") != null &&
                           controller.layers[1].stateMachine.defaultState == cast &&
                           cast.transitions.Length == 0;
                }
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
                bool ownsAvatar = string.Equals(path, KayKitGeneralPath, StringComparison.Ordinal);
                if (ownsAvatar)
                {
                    HumanDescription kayKitHuman = importer != null
                        ? importer.humanDescription
                        : default;
                    if (importer == null || importer.animationType != ModelImporterAnimationType.Human ||
                        importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                        importer.sourceAvatar != null || !importer.importAnimation ||
                        kayKitHuman.hasTranslationDoF || kayKitHuman.human == null ||
                        kayKitHuman.human.Length == 0)
                        return false;
                    // KayKit owns authored ranges and curves. Do not normalize it
                    // with the Mixamo root/loop policy below.
                    continue;
                }
                if (importer == null || importer.animationType != ModelImporterAnimationType.Human ||
                    importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther ||
                    importer.sourceAvatar != sharedAvatar ||
                    !importer.importAnimation ||
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

        public static void ConfigureUprightIdleImporter() => ConfigureMotionImporter(IdlePath);
        public static void ConfigureAuthoredMotionImporter(string path) => ConfigureMotionImporter(path);

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
            AddParameterIfMissing(controller, "MoveX", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "MoveY", AnimatorControllerParameterType.Float);
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
            bool ownsAvatar = string.Equals(path, KayKitGeneralPath, StringComparison.Ordinal);
            if (ownsAvatar)
            {
                bool kayKitDirty = importer.animationType != ModelImporterAnimationType.Human ||
                                   importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                                   importer.sourceAvatar != null || !importer.importAnimation ||
                                   human.hasTranslationDoF;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                importer.importAnimation = true;
                if (human.hasTranslationDoF)
                {
                    human.hasTranslationDoF = false;
                    importer.humanDescription = human;
                }
                if (kayKitDirty) importer.SaveAndReimport();
                return;
            }
            bool dirty = importer.animationType != ModelImporterAnimationType.Human ||
                         importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther ||
                         importer.sourceAvatar != sharedAvatar ||
                         !importer.importAnimation ||
                         human.hasTranslationDoF;
            importer.animationType = ModelImporterAnimationType.Human;
            // Mixamo clips share the exact X Bot skeleton.
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
            string.Equals(path, IdlePath, StringComparison.Ordinal) ||
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
            AnimatorState movingLandBack = FindOrCreateState(machine, "Moving Land Back");
            AnimatorState hardLand = FindOrCreateState(machine, "Hard Land");
            AnimatorState knockdownRecovery = FindOrCreateState(machine, "Knockdown Recovery");
            AnimatorState knockdownRecoveryBack = FindOrCreateState(machine, "Knockdown Recovery Back");
            AnimatorState dodge = FindOrCreateState(machine, "Dodge");
            AnimatorState turnInPlace = FindOrCreateState(machine, "Turn In Place");
            AnimatorState surfEnter = FindOrCreateState(machine, "Surf Enter");
            AnimatorState surf = FindOrCreateState(machine, "Surf Crouch");

            BlendTree tree = FindOrCreateBlendTree(controller, "Earth Locomotion 2D");
            tree.blendType = BlendTreeType.FreeformCartesian2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveY";
            tree.useAutomaticThresholds = false;
            // Keep the complete base layer on one shared X Bot Avatar. The first
            // upright frame of StandToCrouch is a neutral temporary idle; the
            // provided Injured Idle belongs to the damage lane, not locomotion.
            AnimationClip idle = LoadClip(IdlePath);
            if (idle == null) throw new InvalidOperationException("Import the upright X Bot Idle clip before rebuilding locomotion.");
            AnimationClip walkBack = LoadClip(WalkBackPath);
            AnimationClip walk = LoadClip(WalkPath);
            AnimationClip run = LoadClip(KayKitMovementBasicPath, "Running_A") ?? walk;
            AnimationClip strafeLeft = LoadClip(KayKitDirectionalDodgePath, "Running_Strafe_Left") ?? walk;
            AnimationClip strafeRight = LoadClip(KayKitDirectionalDodgePath, "Running_Strafe_Right") ?? walk;
            tree.children = new[]
            {
                Child(idle, 0f, 0f),
                Child(walkBack, 0f, -2f),
                Child(walk, 0f, 2f),
                // Running_A is a licensed authored Humanoid cycle. A real run at
                // the high-speed sample prevents the previous slow-walk moonwalk;
                // GaitRate remains bounded and PlanetMotor still owns displacement.
                Child(run, 0f, 6f),
                Child(strafeLeft, -4f, 0f),
                Child(strafeRight, 4f, 0f)
            };
            locomotion.motion = tree;
            locomotion.speed = 1f;
            locomotion.speedParameter = "GaitRate";
            locomotion.speedParameterActive = true;
            machine.defaultState = locomotion;

            jump.motion = LoadClip(FallingPath) ?? idle;
            fall.motion = LoadClip(FallingPath) ?? idle;
            land.motion = LoadClip(HardLandingPath) ?? idle;
            movingLand.motion = LoadClip(FallingRollPath) ?? LoadClip(HardLandingPath) ?? idle;
            // Reversing a forward Humanoid roll produced a folded/twisting pose.
            // Retain a safe state for old references, but never generate/replay
            // that synthetic clip. A real backward roll will replace this fallback.
            movingLandBack.motion = land.motion;
            movingLandBack.speed = 1f;
            hardLand.motion = LoadClip(HardLandingPath) ?? land.motion;
            knockdownRecovery.motion = LoadClip(FallingRollPath) ?? hardLand.motion;
            knockdownRecoveryBack.motion = LoadClip(KayKitGeneralPath, "Spawn_Ground") ??
                                            knockdownRecovery.motion;
            // Falling-To-Roll is 64 authored frames. Starting at normalized 0.18
            // and playing at 1.9x reaches the 0.82 exit in ~0.72 s, matching the
            // deterministic recoverable-knockdown recovery stage instead of
            // returning controls halfway through the get-up.
            knockdownRecovery.speed = 1.9f;
            knockdownRecoveryBack.speed = 1.35f;
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

            locomotion.transitions = Array.Empty<AnimatorStateTransition>();
            jump.transitions = Array.Empty<AnimatorStateTransition>();
            fall.transitions = Array.Empty<AnimatorStateTransition>();
            land.transitions = Array.Empty<AnimatorStateTransition>();
            movingLand.transitions = Array.Empty<AnimatorStateTransition>();
            movingLandBack.transitions = Array.Empty<AnimatorStateTransition>();
            hardLand.transitions = Array.Empty<AnimatorStateTransition>();
            knockdownRecovery.transitions = Array.Empty<AnimatorStateTransition>();
            knockdownRecoveryBack.transitions = Array.Empty<AnimatorStateTransition>();
            dodge.transitions = Array.Empty<AnimatorStateTransition>();
            turnInPlace.transitions = Array.Empty<AnimatorStateTransition>();
            surfEnter.transitions = Array.Empty<AnimatorStateTransition>();
            surf.transitions = Array.Empty<AnimatorStateTransition>();
            machine.anyStateTransitions = Array.Empty<AnimatorStateTransition>();

            AddConditionTransition(locomotion, jump, "Grounded", AnimatorConditionMode.IfNot, 0f, 0.07f);
            AddConditionTransition(jump, fall, "VerticalSpeed", AnimatorConditionMode.Less, 0f, 0.07f);
            AddConditionTransition(fall, hardLand, "Grounded", AnimatorConditionMode.If, 0f, 0.06f,
                "HardLanding", AnimatorConditionMode.If, 0f);
            AddConditionTransition(fall, land, "Grounded", AnimatorConditionMode.If, 0f, 0.06f,
                "HardLanding", AnimatorConditionMode.IfNot, 0f);
            AddExitTransition(land, locomotion, 0.70f, 0.10f);
            AddExitTransition(movingLand, locomotion, EarthAuthoredActionCatalog.LandingRollExitPhase,
                EarthAuthoredActionCatalog.LandingRollExitBlendSeconds);
            AddExitTransition(movingLandBack, locomotion, 1f, 0.08f);
            AddExitTransition(hardLand, locomotion, 0.82f, 0.12f);
            AddExitTransition(knockdownRecovery, locomotion, 0.82f, 0.12f);
            AddExitTransition(knockdownRecoveryBack, locomotion, 0.86f, 0.12f);
            AnimatorStateTransition dodgeTransition = machine.AddAnyStateTransition(dodge);
            dodgeTransition.hasExitTime = false;
            dodgeTransition.duration = 0.045f;
            dodgeTransition.canTransitionToSelf = false;
            dodgeTransition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            dodgeTransition.AddCondition(AnimatorConditionMode.If, 0f, "Dodge");
            AddExitTransition(dodge, locomotion, 0.90f, 0.07f);
            AddConditionTransition(locomotion, surfEnter, "Surfing", AnimatorConditionMode.If, 0f, 0.10f);
            AddConditionTransition(land, surfEnter, "Surfing", AnimatorConditionMode.If, 0f, 0.10f);
            AddConditionTransition(movingLand, surfEnter, "Surfing", AnimatorConditionMode.If, 0f, 0.10f);
            AddConditionTransition(movingLandBack, surfEnter, "Surfing", AnimatorConditionMode.If, 0f, 0.10f);
            AddConditionTransition(hardLand, surfEnter, "Surfing", AnimatorConditionMode.If, 0f, 0.10f);
            AddExitTransition(surfEnter, surf, 0.72f, 0.08f, "Surfing");
            AddConditionTransition(surfEnter, locomotion, "Surfing", AnimatorConditionMode.IfNot, 0f, 0.09f);
            AddConditionTransition(surf, locomotion, "Surfing", AnimatorConditionMode.IfNot, 0f, 0.14f);
        }

        private static AnimationClip BakeBackwardLandingRoll(AnimationClip source)
        {
            if (source == null) return null;
            // Reverse the actual grounded roll, excluding the original airborne
            // lead and idle tail. Its first recovered pose is contact=0, then it
            // rolls backwards to the original impact pose and blends to locomotion.
            // Both the controller clock and all other animation layers run forward.
            float start = Mathf.Min(0.533f, source.length * 0.25f);
            float end = source.length * 0.58f;
            float duration = end - start;
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BackwardRollClipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, BackwardRollClipPath);
            }
            clip.ClearCurves();
            clip.name = "XBot Landing Roll Back";
            clip.frameRate = source.frameRate;
            clip.legacy = false;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve original = AnimationUtility.GetEditorCurve(source, binding);
                if (original == null) continue;
                var keys = new List<Keyframe>
                {
                    ReversedBoundary(original, end, 0f),
                    ReversedBoundary(original, start, duration)
                };
                foreach (Keyframe key in original.keys)
                {
                    if (key.time <= start || key.time >= end) continue;
                    Keyframe reversed = key;
                    reversed.time = end - key.time;
                    reversed.inTangent = -key.outTangent;
                    reversed.outTangent = -key.inTangent;
                    reversed.inWeight = key.outWeight;
                    reversed.outWeight = key.inWeight;
                    reversed.weightedMode =
                        ((key.weightedMode & WeightedMode.In) != 0 ? WeightedMode.Out : WeightedMode.None) |
                        ((key.weightedMode & WeightedMode.Out) != 0 ? WeightedMode.In : WeightedMode.None);
                    keys.Add(reversed);
                }
                keys.Sort((a, b) => a.time.CompareTo(b.time));
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys.ToArray()));
            }
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
            {
                ObjectReferenceKeyframe[] original = AnimationUtility.GetObjectReferenceCurve(source, binding);
                var keys = new List<ObjectReferenceKeyframe>();
                UnityEngine.Object endValue = null;
                for (int index = 0; index < original.Length; index++)
                {
                    ObjectReferenceKeyframe key = original[index];
                    if (key.time <= end) endValue = key.value;
                    // Object curves are steps: crossing a key in reverse must
                    // restore the preceding value, not repeat the following one.
                    if (key.time > start && key.time < end && index > 0)
                        keys.Add(new ObjectReferenceKeyframe
                        { time = end - key.time, value = original[index - 1].value });
                }
                keys.Add(new ObjectReferenceKeyframe { time = 0f, value = endValue });
                keys.Sort((a, b) => a.time.CompareTo(b.time));
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
            }
            var events = new List<AnimationEvent>();
            foreach (AnimationEvent animationEvent in AnimationUtility.GetAnimationEvents(source))
            {
                if (animationEvent.time < start || animationEvent.time > end) continue;
                animationEvent.time = end - animationEvent.time;
                events.Add(animationEvent);
            }
            events.Sort((a, b) => a.time.CompareTo(b.time));
            AnimationUtility.SetAnimationEvents(clip, events.ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.startTime = 0f;
            settings.stopTime = duration;
            settings.loopTime = false;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static Keyframe ReversedBoundary(AnimationCurve curve, float sourceTime, float targetTime)
        {
            const float epsilon = 0.0001f;
            float tangent = -(curve.Evaluate(sourceTime + epsilon) -
                              curve.Evaluate(sourceTime - epsilon)) / (2f * epsilon);
            return new Keyframe(targetTime, curve.Evaluate(sourceTime), tangent, tangent);
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
            tree.children = new[]
            {
                DirectChild(LoadClip(MagicAttack05Path) ?? generic, 1),
                DirectChild(LoadClip(MagicArea02Path) ?? generic, 2),
                DirectChild(LoadClip(Magic2HCast01Path) ?? generic, 3),
                DirectChild(LoadClip(WheelbarrowDumpPath) ?? generic, 4),
                DirectChild(LoadClip(LeadJabPath) ?? generic, 5),
                DirectChild(LoadClip(Magic1HCast01Path) ?? generic, 6),
                DirectChild(LoadClip(Magic2HAttack03Path) ?? generic, 7),
                DirectChild(LoadClip(MmaKickPath) ?? LoadClip(Magic1HAttack03Path) ?? generic, 8),
                DirectChild(LoadClip(Magic2HCast01Path) ?? generic, 9),
                DirectChild(LoadClip(PunchComboPath) ?? LoadClip(MagicAttack05Path) ?? generic, 10),
                DirectChild(LoadClip(PunchingPath) ?? LoadClip(Magic1HAttack03Path) ?? generic, 11)
            };
            cast.motion = tree;
            ready.motion = LoadClip(StandToCrouchPath, NeutralIdleClipName) ?? ready.motion;
            cast.timeParameterActive = true;
            cast.timeParameter = "EarthMotionTime";
            ready.transitions = Array.Empty<AnimatorStateTransition>();
            cast.transitions = Array.Empty<AnimatorStateTransition>();
            machine.anyStateTransitions = Array.Empty<AnimatorStateTransition>();
            // The layer weight is the sole visibility gate. Keeping the direct
            // tree resident removes an extra 100 ms state transition from every
            // short or rapidly queued action while EarthMotionTime still owns the
            // authored phase.
            machine.defaultState = cast;
            EarthMagicBufferAuthoring.Configure(controller);
        }

        private static void ConfigureImpactLayer(AnimatorController controller)
        {
            AnimatorStateMachine machine = controller.layers[2].stateMachine;
            AnimatorState ready = FindOrCreateState(machine, "Ready");
            AnimatorState recoil = FindOrCreateState(machine, "Recoil");
            recoil.motion = LoadClip(SideHitPath) ?? LoadClip(UppercutHitPath) ?? recoil.motion;
            ready.motion = LoadClip(StandToCrouchPath, NeutralIdleClipName) ?? ready.motion;
            ready.transitions = Array.Empty<AnimatorStateTransition>();
            recoil.transitions = Array.Empty<AnimatorStateTransition>();
            machine.anyStateTransitions = Array.Empty<AnimatorStateTransition>();
            machine.defaultState = ready;
            AddConditionTransition(ready, recoil, "Impact", AnimatorConditionMode.If, 0f, 0.035f);
            AddExitTransition(recoil, ready, 0.72f, 0.16f);
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
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ControllerPath);
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
            AnimatorState from,
            AnimatorState to,
            string parameter,
            AnimatorConditionMode mode,
            float threshold,
            float duration,
            string secondParameter = null,
            AnimatorConditionMode secondMode = AnimatorConditionMode.If,
            float secondThreshold = 0f)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = duration;
            transition.canTransitionToSelf = false;
            transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            transition.AddCondition(mode, threshold, parameter);
            if (!string.IsNullOrEmpty(secondParameter))
                transition.AddCondition(secondMode, secondThreshold, secondParameter);
        }

        private static void AddExitTransition(
            AnimatorState from,
            AnimatorState to,
            float exitTime,
            float duration,
            string requiredBool = null)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = duration;
            transition.canTransitionToSelf = false;
            transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            if (!string.IsNullOrEmpty(requiredBool))
                transition.AddCondition(AnimatorConditionMode.If, 0f, requiredBool);
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
