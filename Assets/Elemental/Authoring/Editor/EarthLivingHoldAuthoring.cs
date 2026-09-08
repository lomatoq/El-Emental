using System;
using System.Linq;
using Elemental.Simulation.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public static class EarthLivingHoldAuthoring
    {
        public const string ClipPath = "Assets/Elemental/Content/Animation/Earth Living Hold.anim";
        public const string MaskPath = "Assets/Elemental/Content/Animation/Earth Living Hold.mask";

        [MenuItem("Elemental/Character/Install Authored Living Hold")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before installing Living Hold.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            var source = AssetDatabase.LoadAllAssetsAtPath(EarthHumanoidMotionSetup.IdlePath)
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (controller == null || source == null || !source.humanMotion || !source.isLooping)
                throw new InvalidOperationException("Living Hold requires the existing Humanoid controller and looping X Bot Idle import. No source or locomotion rebuild is performed.");
            // A real authored idle supplies small torso movement. Subtract its
            // own reference pose so its neutral arm pose never replaces a cast.
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = UnityEngine.Object.Instantiate(source);
                clip.name = "Earth Living Hold";
                int torsoCurves = 0;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    bool torso = binding.type == typeof(Animator) &&
                        (binding.propertyName.StartsWith("Spine ") ||
                         binding.propertyName.StartsWith("Chest ") ||
                         binding.propertyName.StartsWith("UpperChest "));
                    if (torso) torsoCurves++;
                    else AnimationUtility.SetEditorCurve(clip, binding, null);
                }
                if (torsoCurves == 0)
                {
                    UnityEngine.Object.DestroyImmediate(clip);
                    throw new InvalidOperationException("X Bot Idle has no accessible torso muscle curves; inspect the imported source before installing Living Hold.");
                }
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
                AssetDatabase.CreateAsset(clip, ClipPath);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAdditiveReferencePose(clip, source, 0f);
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null)
            {
                mask = new AvatarMask { name = "Living Hold Torso Only" };
                AssetDatabase.CreateAsset(mask, MaskPath);
            }
            for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)part, part == (int)AvatarMaskBodyPart.Body);
            var layers = controller.layers;
            int index = Array.FindIndex(layers, l => l.name == EarthLivingHoldPolicy.LayerName);
            if (index < 0)
            {
                controller.AddLayer(EarthLivingHoldPolicy.LayerName);
                layers = controller.layers;
                index = layers.Length - 1;
            }
            var layer = layers[index];
            layer.blendingMode = AnimatorLayerBlendingMode.Additive;
            layer.avatarMask = mask;
            layer.defaultWeight = 0f;
            layer.iKPass = false;
            var state = layer.stateMachine.states.Select(c => c.state)
                .FirstOrDefault(s => s.name == "Authored Torso Loop") ??
                layer.stateMachine.AddState("Authored Torso Loop");
            state.motion = clip;
            state.speed = 1f;
            state.writeDefaultValues = false;
            state.timeParameterActive = false;
            layer.stateMachine.defaultState = state;
            layers[index] = layer;
            controller.layers = layers;
            EditorUtility.SetDirty(clip);
            EditorUtility.SetDirty(mask);
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(layer.stateMachine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.SaveAssetIfDirty(mask);
            AssetDatabase.SaveAssetIfDirty(controller);
            Debug.Log($"[Elemental] Living Hold installed from {source.name}, {source.length:F3}s. Existing motion assignments retained. Rendered quality requires the Living Hold runtime audit.");
        }
    }
}
