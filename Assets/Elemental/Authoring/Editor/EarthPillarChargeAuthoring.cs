using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public static class EarthPillarChargeAuthoring
    {
        [MenuItem("Elemental/Animation/Install Pillar Charge Crouch")]
        public static void Install()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            if (controller == null) throw new InvalidOperationException("Missing character controller.");
            var idle = AssetDatabase.LoadAllAssetsAtPath(EarthHumanoidMotionSetup.StandToCrouchPath)
                .OfType<AnimationClip>().First(x => x.name == EarthHumanoidMotionSetup.NeutralIdleClipName);
            var crouch = AssetDatabase.LoadAllAssetsAtPath(EarthHumanoidMotionSetup.CrouchIdlePath)
                .OfType<AnimationClip>().First(x => !x.name.StartsWith("__preview__"));
            if (!controller.parameters.Any(x => x.name == "PillarCrouch"))
                controller.AddParameter("PillarCrouch", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(x => x.state).FirstOrDefault(x => x.name == "Pillar Charge")
                        ?? machine.AddState("Pillar Charge");
            var tree = state.motion as BlendTree;
            if (tree == null)
            {
                tree = new BlendTree { name = "Pillar Half Crouch" };
                AssetDatabase.AddObjectToAsset(tree, controller);
            }
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "PillarCrouch";
            tree.useAutomaticThresholds = false;
            tree.children = new[]
            {
                new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = crouch, threshold = 1f, timeScale = 1f }
            };
            state.motion = tree;
            state.speed = 1f;
            state.timeParameterActive = false;
            state.speedParameterActive = false;
            state.iKOnFeet = true;
            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
    }
}
