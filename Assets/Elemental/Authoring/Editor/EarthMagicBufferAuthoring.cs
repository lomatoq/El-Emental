using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>Two independent clip clocks let a new cast blend out the actual outgoing pose.</summary>
    public static class EarthMagicBufferAuthoring
    {
        [MenuItem("Elemental/Character/Configure Independent Magic Buffers")]
        public static void ConfigureSavedController()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before configuring magic buffers.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            Configure(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            Debug.Log("[Elemental] Saved independent A/B magic clocks and blend weights.");
        }

        public static void Configure(AnimatorController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            AnimatorStateMachine machine = null;
            foreach (var layer in controller.layers)
                if (layer.name == "Earth Magic Upper Body") machine = layer.stateMachine;
            if (machine == null) throw new InvalidOperationException("Missing magic layer.");
            AnimatorState first = FindState(machine, "Earth Cast");
            if (first == null || first.motion is not BlendTree original || original.children.Length != 11)
                throw new InvalidOperationException("Expected the existing eleven curated magic clips.");
            ChildMotion[] authored = original.children;
            Undo.RegisterCompleteObjectUndo(controller, "Configure independent magic buffers");
            Undo.RegisterCompleteObjectUndo(machine, "Configure independent magic buffers");
            for (int buffer = 0; buffer < 2; buffer++)
            {
                string suffix = buffer == 0 ? "A" : "B";
                string name = buffer == 0 ? "Earth Cast" : "Earth Cast B";
                AnimatorState state = FindState(machine, name) ?? machine.AddState(name);
                BlendTree tree = buffer == 0 ? original : state.motion as BlendTree;
                if (tree == null || (buffer == 1 && tree == original))
                {
                    tree = new BlendTree { name = "Earth Curated Casts " + suffix };
                    AssetDatabase.AddObjectToAsset(tree, controller);
                }
                Undo.RegisterCompleteObjectUndo(state, "Configure independent magic buffers");
                Undo.RegisterCompleteObjectUndo(tree, "Configure independent magic buffers");
                tree.blendType = BlendTreeType.Direct;
                var children = (ChildMotion[])authored.Clone();
                for (int index = 0; index < children.Length; index++)
                {
                    string parameter = $"EarthPose{suffix}{index + 1:00}";
                    AddFloat(controller, parameter);
                    children[index].directBlendParameter = parameter;
                }
                tree.children = children;
                var serialized = new SerializedObject(tree);
                var normalized = serialized.FindProperty("m_NormalizedBlendValues");
                if (normalized == null) throw new InvalidOperationException("Direct blend normalization is unavailable.");
                normalized.boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                string clock = "EarthMotionTime" + suffix;
                AddFloat(controller, clock);
                state.motion = tree;
                state.timeParameterActive = true;
                state.timeParameter = clock;
                state.writeDefaultValues = first.writeDefaultValues;
                state.iKOnFeet = first.iKOnFeet;
                state.transitions = Array.Empty<AnimatorStateTransition>();
                EditorUtility.SetDirty(tree);
                EditorUtility.SetDirty(state);
            }
            // Code owns transitions; neither an AnyState edge nor a legacy Ready
            // transition may steal the incoming buffer during a rapid cast.
            machine.anyStateTransitions = Array.Empty<AnimatorStateTransition>();
            foreach (var child in machine.states)
            {
                child.state.transitions = Array.Empty<AnimatorStateTransition>();
                EditorUtility.SetDirty(child.state);
            }
            machine.defaultState = first;
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (var child in machine.states)
                if (child.state.name == name) return child.state;
            return null;
        }

        private static void AddFloat(AnimatorController controller, string name)
        {
            foreach (var parameter in controller.parameters)
                if (parameter.name == name)
                {
                    if (parameter.type != AnimatorControllerParameterType.Float)
                        throw new InvalidOperationException("Wrong parameter type: " + name);
                    return;
                }
            controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }
    }
}
