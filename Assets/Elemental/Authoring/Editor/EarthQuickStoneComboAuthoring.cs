using System;
using System.Collections.Generic;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>Extends the saved controller in place; never rebuilds scene or source art.</summary>
    public static class EarthQuickStoneComboAuthoring
    {
        public const string SpinClipPath = "Assets/Elemental/Content/Animation/XBot Earth Spin Kick.anim";
        private const string LegMaskPath = "Assets/Elemental/Content/Animation/Earth Combo Full Body.mask";

        [MenuItem("Elemental/Character/Configure Quick Stone Combo")]
        public static void ConfigureSavedController()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before installing combo clips.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            var kick = AssetDatabase.LoadAssetAtPath<AnimationClip>(EarthSpinKickClipAuthoring.KickClipPath);
            var spin = AssetDatabase.LoadAssetAtPath<AnimationClip>(SpinClipPath);
            if (controller == null || kick == null || spin == null)
                throw new InvalidOperationException("Combo requires saved controller, baked canonical right-foot kick and authored spinning kick at " + SpinClipPath);
            var layers = new List<AnimatorControllerLayer>(controller.layers);
            int magic = layers.FindIndex(layer => layer.name == "Earth Magic Upper Body");
            if (magic < 0) throw new InvalidOperationException("Missing shared magic animation layer.");
            foreach (var state in layers[magic].stateMachine.states)
            {
                if (state.state.name != "Earth Cast" && state.state.name != "Earth Cast B") continue;
                if (state.state.motion is not BlendTree tree) throw new InvalidOperationException("Shared cast must be a Direct BlendTree.");
                string buffer = state.state.name == "Earth Cast" ? "A" : "B";
                var children = new List<ChildMotion>(tree.children);
                while (children.Count > 11) children.RemoveAt(children.Count - 1);
                for (int slot = 12; slot <= 14; slot++)
                {
                    string parameter = $"EarthPose{buffer}{slot:00}";
                    AddFloat(controller, parameter);
                    AddFloat(controller, $"EarthPose{slot:00}");
                    children.Add(new ChildMotion { motion = slot == 14 ? spin : kick,
                        directBlendParameter = parameter, timeScale = 1f, mirror = slot == 12 });
                }
                tree.children = children.ToArray();
                EditorUtility.SetDirty(tree);
            }
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(LegMaskPath);
            if (mask == null) { mask = new AvatarMask { name = "Earth Combo Full Body" }; AssetDatabase.CreateAsset(mask, LegMaskPath); }
            for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)part,
                    part < (int)AvatarMaskBodyPart.LeftFootIK);
            int legLayer = layers.FindIndex(layer => layer.name == "Earth Combo Full Body");
            AnimatorStateMachine comboMachine = legLayer >= 0 && layers[legLayer].syncedLayerIndex < 0
                ? layers[legLayer].stateMachine : null;
            if (comboMachine == null)
            {
                comboMachine = new AnimatorStateMachine { name = "Earth Combo Full Body" };
                AssetDatabase.AddObjectToAsset(comboMachine, controller);
            }
            foreach (var sourceState in layers[magic].stateMachine.states)
            {
                if (sourceState.state.name != "Earth Cast" && sourceState.state.name != "Earth Cast B") continue;
                string name = sourceState.state.name == "Earth Cast" ? "Combo A" : "Combo B";
                AnimatorState target = null;
                foreach (var entry in comboMachine.states) if (entry.state.name == name) target = entry.state;
                if (target == null) target = comboMachine.AddState(name);
                target.motion = sourceState.state.motion;
                target.timeParameterActive = true;
                target.timeParameter = sourceState.state.timeParameter;
                target.writeDefaultValues = false;
                target.iKOnFeet = false;
                target.transitions = Array.Empty<AnimatorStateTransition>();
                if (name == "Combo A") comboMachine.defaultState = target;
                EditorUtility.SetDirty(target);
            }
            comboMachine.anyStateTransitions = Array.Empty<AnimatorStateTransition>();
            EditorUtility.SetDirty(comboMachine);
            var leg = new AnimatorControllerLayer { name = "Earth Combo Full Body", avatarMask = mask,
                defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = comboMachine, syncedLayerIndex = -1 };
            if (legLayer < 0) layers.Add(leg); else layers[legLayer] = leg;
            controller.layers = layers.ToArray();
            var profile = AssetDatabase.LoadAssetAtPath<EarthMagicMotionProfile>("Assets/Elemental/Content/Profiles/EarthMagicMotionProfile.asset");
            if (profile == null) throw new InvalidOperationException("Missing saved magic motion profile.");
            var entries = new List<EarthMagicMotionEntry>(profile.motions);
            entries.RemoveAll(entry => entry != null && (int)entry.slot >= 12);
            for (int slot = 12; slot <= 14; slot++)
            {
                var timing = EarthMagicClipTiming.Default;
                timing.Contact = slot == 14 ? .6f : .5f;
                entries.Add(new EarthMagicMotionEntry { slot = (EarthHumanoidPoseSlot)slot,
                    timing = timing, actionHandInfluence = 0f, sustainedHandInfluence = 0f });
            }
            profile.motions = entries.ToArray();
            EditorUtility.SetDirty(mask); EditorUtility.SetDirty(profile); EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static void AddFloat(AnimatorController controller, string name)
        {
            foreach (var parameter in controller.parameters) if (parameter.name == name) return;
            controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }
    }
}
