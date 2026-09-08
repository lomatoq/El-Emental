using System.Linq;
using Elemental.Authoring.Editor;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthLivingHoldTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void CancelWithZeroSlotContinuesOriginalRecovery(int hz)
        {
            var clock = new EarthMagicClipClock();
            var timing = EarthMagicClipTiming.Default;
            for (int i = 0; i < hz; i++)
                clock.Step(3, 1, EarthCastPhase.Sustain, true, in timing, 1f / hz);
            float before = clock.NormalizedTime;
            float after = clock.Step(0, 0, EarthCastPhase.Idle, false, in timing, 1f / hz);
            Assert.That(after, Is.GreaterThan(before), "Released input must not freeze a visible outgoing pose.");
            Assert.That(after - before, Is.LessThanOrEqualTo(.65f / hz + .00001f));
            for (int i = 0; i < hz * 3; i++)
                clock.Step(0, 0, EarthCastPhase.Idle, false, in timing, 1f / hz);
            Assert.That(clock.NormalizedTime, Is.EqualTo(timing.RecoverEnd).Within(.0001f));
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ShortPhaseHandoffsDoNotRestartAnEaseFromRest(int hz)
        {
            var clock = new EarthMagicClipClock();
            var timing = EarthMagicClipTiming.Default;
            float before = clock.Step(3, 1, EarthCastPhase.Acquire, true, in timing, 1f / hz);
            float after = clock.Step(3, 1, EarthCastPhase.Root, true, in timing, 1f / hz);
            Assert.That(after - before, Is.EqualTo(.65f / hz).Within(.00001f));
            Assert.That(clock.Step(3, 1, EarthCastPhase.Load, true, in timing, 0f), Is.EqualTo(after));
        }

        [Test]
        public void OutgoingComboRecoveryStartsAtTheRenderedOverrideTime()
        {
            var clock = new EarthMagicClipClock();
            var timing = EarthMagicClipTiming.Default;
            clock.ResumeRecovery(12, .72f);
            float time = clock.Step(0, 0, EarthCastPhase.Idle, false, in timing, 1f / 60f);
            Assert.That(time, Is.GreaterThan(.72f));
            Assert.That(time, Is.LessThanOrEqualTo(.72f + 2f / 60f));
        }

        [Test]
        public void OnlyARealPostContactHoldAdmitsAuthoredTorsoMotion()
        {
            Assert.That(EarthLivingHoldPolicy.TargetWeight(true, true, true, false, 1f), Is.EqualTo(.35f));
            Assert.That(EarthLivingHoldPolicy.TargetWeight(true, false, true, false, 1f), Is.Zero);
            Assert.That(EarthLivingHoldPolicy.TargetWeight(true, true, false, false, 1f), Is.Zero);
            Assert.That(EarthLivingHoldPolicy.TargetWeight(false, true, true, false, 1f), Is.Zero);
            Assert.That(EarthLivingHoldPolicy.TargetWeight(true, true, true, true, 1f), Is.Zero);
            Assert.That(EarthLivingHoldPolicy.TargetWeight(true, true, true, false, float.NaN), Is.Zero);
        }

        [Test]
        public void InstalledContentIsAnAuthoredTorsoOnlyAdditiveLoop()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(EarthLivingHoldAuthoring.ClipPath);
            Assert.That(clip, Is.Not.Null, "Run EarthLivingHoldAuthoring.Install first.");
            Assert.That(clip.isLooping, Is.True);
            Assert.That(clip.length, Is.GreaterThan(1f));
            var bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.That(bindings.Length, Is.GreaterThan(0));
            foreach (var binding in bindings)
                Assert.That(binding.propertyName.StartsWith("Spine ") ||
                    binding.propertyName.StartsWith("Chest ") ||
                    binding.propertyName.StartsWith("UpperChest "), Is.True, binding.propertyName);
            Assert.That(bindings.Any(b => {
                var keys = AnimationUtility.GetEditorCurve(clip, b).keys;
                return keys.Max(k => k.value) - keys.Min(k => k.value) > .001f;
            }), Is.True, "A constant-pose clip is not living motion.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            var layer = controller.layers.Single(l => l.name == EarthLivingHoldPolicy.LayerName);
            Assert.That(layer.blendingMode, Is.EqualTo(AnimatorLayerBlendingMode.Additive));
            Assert.That(layer.defaultWeight, Is.Zero);
            Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root), Is.False);
            Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), Is.False);
            Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm), Is.False);
        }
    }
}
