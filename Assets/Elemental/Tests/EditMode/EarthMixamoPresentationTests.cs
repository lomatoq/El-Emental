using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elemental.Authoring.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMixamoPresentationTests
    {
        private const string CharacterPath =
            "Assets/Elemental/Content/Characters/Linebreaker/Linebreaker.fbx";
        private const string WalkPath = "Assets/ThirdParty/Mixamo/X Bot@Walking.fbx";
        private const string WalkBackPath = "Assets/ThirdParty/Mixamo/X Bot@Walking Backwards.fbx";
        private const string PushPath = "Assets/ThirdParty/Mixamo/X Bot@Lead Jab.fbx";
        private const string ControllerPath = "Assets/Elemental/Content/Animation/KayKitMage.controller";

        [Test]
        public void LinebreakerImportsAsAValidSkinnedHumanoid()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,
                Is.GreaterThan(0), "The runtime character must deform; a rigid-part fallback is not acceptable.");

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(CharacterPath)
                .OfType<Avatar>()
                .FirstOrDefault();
            Assert.That(avatar, Is.Not.Null);
            Assert.That(avatar.isValid, Is.True);
            Assert.That(avatar.isHuman, Is.True);
        }

        [TestCase(WalkPath, true)]
        [TestCase(WalkBackPath, true)]
        [TestCase(PushPath, false)]
        public void CuratedMixamoMotionsRemainValidHumanoidSources(string path, bool shouldLoop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CopyFromOther));
            Assert.That(importer.sourceAvatar, Is.Not.Null);
            Assert.That(importer.sourceAvatar.isValid, Is.True);
            Assert.That(importer.sourceAvatar.isHuman, Is.True,
                "Motion FBXs may keep their canonical Mixamo avatar; Mecanim retargets them onto Linebreaker at runtime.");

            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__"));
            Assert.That(clip, Is.Not.Null, path);
            Assert.That(clip.isHumanMotion, Is.True, path);

            if (!shouldLoop) return;
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            Assert.That(clips, Is.Not.Empty, path);
            Assert.That(clips.All(candidate => candidate.loopTime), Is.True,
                "Walking clips must not freeze after their first pass.");
        }

        [Test]
        public void RuntimeControllerReferencesTheCuratedMixamoMotions()
        {
            string[] dependencies = AssetDatabase.GetDependencies(ControllerPath, true);
            Assert.That(dependencies, Does.Contain(WalkPath));
            Assert.That(dependencies, Does.Contain(WalkBackPath));
            Assert.That(dependencies, Does.Contain(PushPath));
            Assert.That(dependencies, Does.Contain(EarthHumanoidMotionSetup.FallingRollPath));
            Assert.That(dependencies, Does.Contain(EarthHumanoidMotionSetup.HardLandingPath));
            Assert.That(dependencies, Does.Contain(EarthHumanoidMotionSetup.PunchComboPath));
            Assert.That(dependencies, Does.Contain(EarthHumanoidMotionSetup.MmaKickPath));
            Assert.That(dependencies, Does.Contain(EarthHumanoidMotionSetup.PunchingPath));
            Assert.That(dependencies, Does.Contain(EarthHumanoidMotionSetup.SideHitPath));
            Assert.That(dependencies, Does.Contain(EarthHumanoidMotionSetup.KayKitDirectionalDodgePath));
            Assert.That(dependencies, Does.Contain(EarthHumanoidMotionSetup.KayKitMovementBasicPath),
                "High-speed locomotion must use the licensed authored Running_A cycle.");
            Assert.That(dependencies, Does.Contain(EarthHumanoidMotionSetup.LeftTurnPath),
                "Tank steering needs an authored turn-in-place instead of rotating a neutral idle.");
        }

        [Test]
        public void ControllerContainsExplicitRecoverableKnockdownRecoveryState()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.layers, Is.Not.Empty);
            Assert.That(controller.layers[0].stateMachine.states.Any(child =>
                child.state != null && child.state.name == "Knockdown Recovery"), Is.True);
            AnimatorState recovery = controller.layers[0].stateMachine.states
                .Select(child => child.state)
                .First(state => state != null && state.name == "Knockdown Recovery");
            Assert.That(recovery.transitions, Is.Empty,
                "Recovery markers and EarthTransitionDirector own the exit; the Animator Controller must not leave recovery at an earlier fixed exit time.");
            Assert.That(controller.layers, Has.Length.GreaterThanOrEqualTo(3));
            Assert.That(controller.layers[2].stateMachine.states.Any(child =>
                child.state != null && child.state.name == "Recoil" &&
                child.state.motion != null), Is.True,
                "Player and bot need the same authored hit-recoil clip on the impact lane.");
            Assert.That(controller.parameters.Any(parameter =>
                parameter.name == "Impact" &&
                parameter.type == AnimatorControllerParameterType.Trigger), Is.True);
            Assert.That(controller.layers[0].stateMachine.states.Any(child =>
                child.state != null && child.state.name == "Dodge" &&
                child.state.motion is BlendTree tree && tree.children.Length == 4), Is.True,
                "The base graph must contain the four-way authored KayKit dodge tree.");
            Assert.That(controller.layers[0].stateMachine.states.Any(child =>
                child.state != null && child.state.name == "Turn In Place" &&
                child.state.motion is BlendTree tree && tree.children.Length == 3), Is.True,
                "The base graph must contain mirrored authored left/right pivot motion.");
            Assert.That(controller.parameters.Any(parameter =>
                parameter.name == "Dodge" &&
                parameter.type == AnimatorControllerParameterType.Trigger), Is.True);
            Assert.That(controller.parameters.Any(parameter =>
                parameter.name == "DodgeX" &&
                parameter.type == AnimatorControllerParameterType.Float), Is.True);
            Assert.That(controller.parameters.Any(parameter =>
                parameter.name == "DodgeY" &&
                parameter.type == AnimatorControllerParameterType.Float), Is.True);
        }

        [Test]
        public void ControllerUpgradeIsTransitionSubAssetIdempotent()
        {
            string temporaryPath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/EarthHumanoidMotionSetupIdempotency.controller");
            try
            {
                AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(
                    temporaryPath);
                Assert.That(controller, Is.Not.Null);
                controller.AddLayer("Magic");
                controller.AddLayer("Impact");

                EarthHumanoidMotionSetup.UpgradeController(controller);
                AssetDatabase.SaveAssets();
                string firstGraph = CaptureTransitionGraph(controller);
                long[] firstTransitionIds = CaptureReferencedTransitionIds(controller);
                int firstSubAssetCount = CountTransitionSubAssets(temporaryPath);
                AssertNoOrphanTransitionSubAssets(controller, temporaryPath);

                EarthHumanoidMotionSetup.UpgradeController(controller);
                AssetDatabase.SaveAssets();
                string secondGraph = CaptureTransitionGraph(controller);
                long[] secondTransitionIds = CaptureReferencedTransitionIds(controller);
                int secondSubAssetCount = CountTransitionSubAssets(temporaryPath);

                Assert.That(secondGraph, Is.EqualTo(firstGraph),
                    "Two consecutive upgrades must have an empty normalized transition diff.");
                Assert.That(secondTransitionIds, Is.EqualTo(firstTransitionIds),
                    "A current graph must reuse its generated transition subassets.");
                Assert.That(secondSubAssetCount, Is.EqualTo(firstSubAssetCount),
                    "A rebuild must not append orphan AnimatorStateTransition subassets.");
                AssertNoOrphanTransitionSubAssets(controller, temporaryPath);

                AnimatorState locomotion = controller.layers[0].stateMachine.states
                    .Select(child => child.state)
                    .First(state => state != null && state.name == "Locomotion");
                locomotion.AddTransition(locomotion);
                controller.layers[0].stateMachine.AddAnyStateTransition(locomotion);
                AssetDatabase.SaveAssets();
                Assert.That(CountTransitionSubAssets(temporaryPath), Is.EqualTo(firstSubAssetCount + 2));

                EarthHumanoidMotionSetup.UpgradeController(controller);
                AssetDatabase.SaveAssets();
                Assert.That(CaptureTransitionGraph(controller), Is.EqualTo(firstGraph),
                    "Rebuild must remove excess state and AnyState transitions owned by the generated graph.");
                Assert.That(CountTransitionSubAssets(temporaryPath), Is.EqualTo(firstSubAssetCount),
                    "Removing an excess generated transition must delete its persistent subasset.");
                AssertNoOrphanTransitionSubAssets(controller, temporaryPath);
            }
            finally
            {
                AssetDatabase.DeleteAsset(temporaryPath);
            }
        }

        [Test]
        public void DirectionalKayKitDodgesRemainFourValidHumanoidClips()
        {
            ModelImporter importer = AssetImporter.GetAtPath(
                EarthHumanoidMotionSetup.KayKitDirectionalDodgePath) as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            string[] required = { "Dodge_Forward", "Dodge_Backward", "Dodge_Left", "Dodge_Right" };
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(
                    EarthHumanoidMotionSetup.KayKitDirectionalDodgePath)
                .OfType<AnimationClip>()
                .Where(candidate => !candidate.name.StartsWith("__preview__"))
                .ToArray();
            for (int index = 0; index < required.Length; index++)
                Assert.That(clips.Any(candidate =>
                    candidate.name == required[index] && candidate.isHumanMotion), Is.True,
                    required[index]);
        }

        [TestCase(EarthHumanoidMotionSetup.HardLandingPath)]
        [TestCase(EarthHumanoidMotionSetup.FallingRollPath)]
        public void CanonicalPhysicsClipsExtractRootTracksInsteadOfBakingThemIntoBones(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null, path);
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            Assert.That(clips, Is.Not.Empty, path);
            Assert.That(clips.All(candidate =>
                !candidate.lockRootRotation &&
                !candidate.lockRootHeightY &&
                !candidate.lockRootPositionXZ), Is.True,
                "PlanetMotor owns canonical motion; baked FBX root tracks visually detach the rig from its capsule.");
        }

        [Test]
        public void NeutralIdleAndSurfTransitionAreDistinctSubclips()
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(
                    EarthHumanoidMotionSetup.StandToCrouchPath)
                .OfType<AnimationClip>()
                .Where(candidate => !candidate.name.StartsWith("__preview__"))
                .ToArray();
            Assert.That(clips.Any(candidate =>
                candidate.name == EarthHumanoidMotionSetup.NeutralIdleClipName && candidate.isLooping), Is.True);
            Assert.That(clips.Any(candidate =>
                candidate.name == "Standing Idle To Crouch" && !candidate.isLooping), Is.True);
        }

        private static int CountTransitionSubAssets(string controllerPath) =>
            AssetDatabase.LoadAllAssetsAtPath(controllerPath)
                .OfType<AnimatorStateTransition>()
                .Count();

        private static long[] CaptureReferencedTransitionIds(AnimatorController controller)
        {
            HashSet<AnimatorStateTransition> referenced = CaptureReferencedTransitions(controller);
            return referenced
                .Select(GetPersistentLocalId)
                .OrderBy(localId => localId)
                .ToArray();
        }

        private static long GetPersistentLocalId(AnimatorStateTransition transition)
        {
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    transition,
                    out string _,
                    out long localId))
                return localId;
            throw new InvalidOperationException(
                $"Transition '{transition.name}' is not a persistent controller subasset.");
        }

        private static void AssertNoOrphanTransitionSubAssets(
            AnimatorController controller,
            string controllerPath)
        {
            HashSet<AnimatorStateTransition> referenced = CaptureReferencedTransitions(controller);
            AnimatorStateTransition[] subAssets = AssetDatabase.LoadAllAssetsAtPath(controllerPath)
                .OfType<AnimatorStateTransition>()
                .ToArray();
            Assert.That(subAssets, Is.Not.Empty);
            Assert.That(subAssets.All(referenced.Contains), Is.True,
                "Every persistent transition subasset must be referenced by the generated graph.");
            Assert.That(subAssets.Length, Is.EqualTo(referenced.Count));
        }

        private static HashSet<AnimatorStateTransition> CaptureReferencedTransitions(
            AnimatorController controller)
        {
            var referenced = new HashSet<AnimatorStateTransition>();
            AnimatorControllerLayer[] layers = controller.layers;
            for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
                CollectReferencedTransitions(layers[layerIndex].stateMachine, referenced);
            return referenced;
        }

        private static void CollectReferencedTransitions(
            AnimatorStateMachine machine,
            ISet<AnimatorStateTransition> referenced)
        {
            AnimatorStateTransition[] anyStateTransitions = machine.anyStateTransitions;
            for (int index = 0; index < anyStateTransitions.Length; index++)
                if (anyStateTransitions[index] != null)
                    referenced.Add(anyStateTransitions[index]);
            ChildAnimatorState[] states = machine.states;
            for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
            {
                AnimatorState state = states[stateIndex].state;
                if (state == null) continue;
                AnimatorStateTransition[] transitions = state.transitions;
                for (int transitionIndex = 0; transitionIndex < transitions.Length; transitionIndex++)
                    if (transitions[transitionIndex] != null)
                        referenced.Add(transitions[transitionIndex]);
            }
            ChildAnimatorStateMachine[] children = machine.stateMachines;
            for (int index = 0; index < children.Length; index++)
                if (children[index].stateMachine != null)
                    CollectReferencedTransitions(children[index].stateMachine, referenced);
        }

        private static string CaptureTransitionGraph(AnimatorController controller)
        {
            var snapshot = new StringBuilder(2048);
            AnimatorControllerLayer[] layers = controller.layers;
            for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
            {
                snapshot.Append("layer=").Append(layers[layerIndex].name).AppendLine();
                AppendTransitionGraph(layers[layerIndex].stateMachine, snapshot);
            }
            return snapshot.ToString();
        }

        private static void AppendTransitionGraph(
            AnimatorStateMachine machine,
            StringBuilder snapshot)
        {
            snapshot.Append("machine=").Append(machine.name).AppendLine();
            AppendTransitions("any", machine.anyStateTransitions, snapshot);
            AnimatorState[] states = machine.states
                .Select(child => child.state)
                .Where(state => state != null)
                .OrderBy(state => state.name, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < states.Length; index++)
            {
                snapshot.Append("state=").Append(states[index].name).AppendLine();
                AppendTransitions(states[index].name, states[index].transitions, snapshot);
            }
            AnimatorStateMachine[] children = machine.stateMachines
                .Select(child => child.stateMachine)
                .Where(child => child != null)
                .OrderBy(child => child.name, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < children.Length; index++)
                AppendTransitionGraph(children[index], snapshot);
        }

        private static void AppendTransitions(
            string source,
            AnimatorStateTransition[] transitions,
            StringBuilder snapshot)
        {
            for (int index = 0; index < transitions.Length; index++)
            {
                AnimatorStateTransition transition = transitions[index];
                snapshot.Append(source).Append('>').Append(
                    transition.destinationState != null
                        ? transition.destinationState.name
                        : transition.destinationStateMachine != null
                            ? transition.destinationStateMachine.name
                            : transition.isExit ? "exit" : "none");
                AppendFloatBits(snapshot, transition.duration);
                AppendFloatBits(snapshot, transition.offset);
                AppendFloatBits(snapshot, transition.exitTime);
                snapshot.Append('|').Append(transition.hasExitTime)
                    .Append('|').Append(transition.hasFixedDuration)
                    .Append('|').Append(transition.isExit)
                    .Append('|').Append(transition.solo)
                    .Append('|').Append(transition.mute)
                    .Append('|').Append((int)transition.interruptionSource)
                    .Append('|').Append(transition.orderedInterruption)
                    .Append('|').Append(transition.canTransitionToSelf);
                AnimatorCondition[] conditions = transition.conditions;
                for (int conditionIndex = 0; conditionIndex < conditions.Length; conditionIndex++)
                {
                    AnimatorCondition condition = conditions[conditionIndex];
                    snapshot.Append("|condition=").Append((int)condition.mode)
                        .Append(':').Append(condition.parameter);
                    AppendFloatBits(snapshot, condition.threshold);
                }
                snapshot.AppendLine();
            }
        }

        private static void AppendFloatBits(StringBuilder snapshot, float value) =>
            snapshot.Append('|').Append(BitConverter.SingleToInt32Bits(value));
    }
}
