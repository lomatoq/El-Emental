using System;
using Elemental.Authoring.Editor;
using Elemental.Presentation.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthQuickStoneComboAssetTests
    {
        [Test]
        public void SavedComboExtendsBothBuffersAndOwnsFullBodyOnlyWhileWeighted()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            Assert.That(controller, Is.Not.Null);
            int magicIndex = Array.FindIndex(controller.layers, layer => layer.name == "Earth Magic Upper Body");
            int comboIndex = Array.FindIndex(controller.layers, layer => layer.name == "Earth Combo Full Body");
            Assert.That(comboIndex, Is.GreaterThanOrEqualTo(0));
            var combo = controller.layers[comboIndex];
            Assert.That(combo.syncedLayerIndex, Is.EqualTo(-1), "A synchronized upper-body mask can erase kick legs.");
            Assert.That(combo.stateMachine.states, Has.Length.EqualTo(2));
            foreach (var state in combo.stateMachine.states)
            {
                Assert.That(state.state.timeParameterActive, Is.True);
                Assert.That(state.state.motion, Is.TypeOf<BlendTree>());
            }
            Assert.That(combo.defaultWeight, Is.Zero);
            Assert.That(combo.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root), Is.True);
            Assert.That(combo.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), Is.True);
            Assert.That(combo.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg), Is.True);
            foreach (var child in controller.layers[magicIndex].stateMachine.states)
            {
                if (child.state.name != "Earth Cast" && child.state.name != "Earth Cast B") continue;
                var tree = child.state.motion as BlendTree;
                Assert.That(tree, Is.Not.Null);
                Assert.That(tree.children, Has.Length.EqualTo(14));
                Assert.That(tree.children[11].motion, Is.SameAs(tree.children[12].motion));
                Assert.That(tree.children[11].mirror, Is.Not.EqualTo(tree.children[12].mirror));
                Assert.That(AssetDatabase.GetAssetPath(tree.children[13].motion), Is.EqualTo(EarthQuickStoneComboAuthoring.SpinClipPath));
                Assert.That(tree.children[13].motion.isHumanMotion, Is.True);
                Assert.That(tree.children[13].motion.isLooping, Is.False);
            }
            var profile = AssetDatabase.LoadAssetAtPath<EarthMagicMotionProfile>("Assets/Elemental/Content/Profiles/EarthMagicMotionProfile.asset");
            Assert.That(profile.motions, Has.Length.EqualTo(14));
            for (int slot = 12; slot <= 14; slot++)
            {
                Assert.That(profile.Find(slot).timing.IsValid, Is.True);
                Assert.That(profile.Find(slot).actionHandInfluence, Is.Zero);
                Assert.That(profile.Find(slot).sustainedHandInfluence, Is.Zero);
            }
        }
    }
}
