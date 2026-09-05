using System;
using System.Collections.Generic;
using Elemental.Authoring.Editor;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    /// <summary>
    /// Staged under Tools. Copy into Assets/Elemental/Tests/EditMode before running.
    /// Guards the saved controller/profile rather than only the pure fallback clock.
    /// </summary>
    public sealed class SeptemberAnimationSemanticAssetAuditTests
    {
        private static readonly string[] ExpectedMagicPaths =
        {
            EarthHumanoidMotionSetup.MagicAttack05Path,
            EarthHumanoidMotionSetup.MagicArea02Path,
            EarthHumanoidMotionSetup.Magic2HCast01Path,
            EarthHumanoidMotionSetup.WheelbarrowDumpPath,
            EarthHumanoidMotionSetup.LeadJabPath,
            EarthHumanoidMotionSetup.Magic1HCast01Path,
            EarthHumanoidMotionSetup.Magic2HAttack03Path,
            EarthHumanoidMotionSetup.MmaKickPath,
            EarthHumanoidMotionSetup.Magic2HCast01Path,
            EarthHumanoidMotionSetup.PunchComboPath,
            EarthHumanoidMotionSetup.PunchingPath
        };

        [Test]
        public void SavedMagicTreeMapsEverySemanticSlotToItsCuratedClip()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                EarthHumanoidMotionSetup.ControllerPath);
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.layers, Has.Length.GreaterThanOrEqualTo(3));

            AnimatorControllerLayer magicLayer = controller.layers[1];
            Assert.That(magicLayer.name, Is.EqualTo("Earth Magic Upper Body"));
            Assert.That(magicLayer.blendingMode, Is.EqualTo(AnimatorLayerBlendingMode.Override));
            Assert.That(magicLayer.defaultWeight, Is.Zero);
            AssertUpperBodyOnly(magicLayer.avatarMask);

            AnimatorState cast = FindState(magicLayer.stateMachine, "Earth Cast");
            Assert.That(cast, Is.Not.Null);
            Assert.That(magicLayer.stateMachine.defaultState, Is.SameAs(cast),
                "The phase-scrubbed cast tree must stay resident; layer weight owns visibility.");
            Assert.That(cast.transitions, Is.Empty,
                "A second state transition consumes short casts after runtime blending.");
            Assert.That(cast.timeParameterActive, Is.True);
            Assert.That(cast.timeParameter, Is.EqualTo("EarthMotionTimeA"));
            Assert.That(cast.motion, Is.TypeOf<BlendTree>());
            var tree = (BlendTree)cast.motion;
            Assert.That(tree.name, Is.EqualTo("Earth Curated Casts"));
            Assert.That(tree.blendType, Is.EqualTo(BlendTreeType.Direct));
            Assert.That(tree.children, Has.Length.EqualTo(11));

            for (int index = 0; index < tree.children.Length; index++)
            {
                ChildMotion child = tree.children[index];
                int slot = index + 1;
                Assert.That(child.directBlendParameter, Is.EqualTo($"EarthPoseA{slot:00}"),
                    $"Semantic slot {slot} is driven by the wrong parameter.");
                Assert.That(child.motion, Is.Not.Null, $"Semantic slot {slot} has no motion.");
                Assert.That(AssetDatabase.GetAssetPath(child.motion), Is.EqualTo(ExpectedMagicPaths[index]),
                    $"Semantic slot {slot} resolves to the wrong authored clip.");
                Assert.That(child.motion.isHumanMotion, Is.True, $"Slot {slot} is not Humanoid motion.");
                Assert.That(child.motion.isLooping, Is.False, $"One-shot slot {slot} must not loop.");
            }
            AnimatorState alternate = FindState(magicLayer.stateMachine, "Earth Cast B");
            Assert.That(alternate, Is.Not.Null, "Repeated punches need an independent incoming clip clock.");
            Assert.That(alternate.timeParameterActive, Is.True);
            Assert.That(alternate.timeParameter, Is.EqualTo("EarthMotionTimeB"));
            Assert.That(alternate.transitions, Is.Empty);
            Assert.That(alternate.motion, Is.TypeOf<BlendTree>());
            Assert.That(alternate.motion, Is.Not.SameAs(tree), "Reusing one tree rewinds the outgoing cast.");
            var incoming = (BlendTree)alternate.motion;
            Assert.That(incoming.blendType, Is.EqualTo(BlendTreeType.Direct));
            Assert.That(incoming.children, Has.Length.EqualTo(11));
            for (int index = 0; index < incoming.children.Length; index++)
            {
                Assert.That(incoming.children[index].motion, Is.SameAs(tree.children[index].motion));
                Assert.That(incoming.children[index].directBlendParameter,
                    Is.EqualTo($"EarthPoseB{index + 1:00}"));
            }
            foreach (string suffix in new[] { "A", "B" })
            {
                Assert.That(Array.Exists(controller.parameters, p => p.name == "EarthMotionTime" + suffix &&
                    p.type == AnimatorControllerParameterType.Float), Is.True);
                for (int slot = 1; slot <= 11; slot++)
                    Assert.That(Array.Exists(controller.parameters, p => p.name == $"EarthPose{suffix}{slot:00}" &&
                        p.type == AnimatorControllerParameterType.Float), Is.True);
            }
        }

        [Test]
        public void SavedMagicProfileHasExactlyOneValidEntryPerSemanticSlot()
        {
            EarthMagicMotionProfile profile = AssetDatabase.LoadAssetAtPath<EarthMagicMotionProfile>(
                EarthAnimationRescueSetup.MagicProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.Validate(out string error), Is.True, error);
            Assert.That(profile.motions, Has.Length.EqualTo(11));
            var seen = new HashSet<EarthHumanoidPoseSlot>();
            foreach (EarthMagicMotionEntry entry in profile.motions)
            {
                Assert.That(entry, Is.Not.Null);
                Assert.That((int)entry.slot, Is.InRange(1, 11));
                Assert.That(seen.Add(entry.slot), Is.True, $"Duplicate saved timing for {entry.slot}.");
                Assert.That(entry.timing.IsValid, Is.True, $"Invalid timing for {entry.slot}.");
                Assert.That(entry.timing.Contact, Is.GreaterThan(entry.timing.LoadEnd));
                Assert.That(entry.timing.RecoverEnd, Is.GreaterThan(entry.timing.Sustain));
            }
            Assert.That(seen.Count, Is.EqualTo(11));
        }

        [Test]
        public void SavedMagicProfileUsesCuratedPerClipBeatsAndHandInfluence()
        {
            EarthMagicMotionProfile profile = AssetDatabase.LoadAssetAtPath<EarthMagicMotionProfile>(
                EarthAnimationRescueSetup.MagicProfilePath);
            EarthMagicMotionEntry[] defaults = EarthMagicMotionProfile.CreateDefaults();
            var contacts = new HashSet<int>();
            foreach (EarthMagicMotionEntry expected in defaults)
            {
                EarthMagicMotionEntry actual = profile.Find((int)expected.slot);
                Assert.That(actual, Is.Not.Null, expected.slot.ToString());
                AssertTiming(actual.timing, expected.timing, expected.slot.ToString());
                Assert.That(actual.actionHandInfluence,
                    Is.EqualTo(expected.actionHandInfluence).Within(.00001f), expected.slot.ToString());
                Assert.That(actual.sustainedHandInfluence,
                    Is.EqualTo(expected.sustainedHandInfluence).Within(.00001f), expected.slot.ToString());
                contacts.Add(Mathf.RoundToInt(actual.timing.Contact * 1000f));
            }

            Assert.That(contacts.Count, Is.EqualTo(11),
                "Each current source clip/semantic reuse needs its own reviewed contact beat.");
            Assert.That(profile.Find((int)EarthHumanoidPoseSlot.VectorPush).timing.Contact,
                Is.LessThan(profile.Find((int)EarthHumanoidPoseSlot.HeavyThrow).timing.Contact),
                "The lead-jab push must contact before the late wheelbarrow-dump release.");
            Assert.That(profile.Find((int)EarthHumanoidPoseSlot.PullStone).timing.Contact,
                Is.Not.EqualTo(profile.Find((int)EarthHumanoidPoseSlot.ArmorAssemble).timing.Contact),
                "Pull and armor share a clip, so semantic timing must keep them visually distinct.");
        }

        [Test]
        public void TurnTreeHasNeutralIdleAndMirroredAuthoredTurnWithNoAutomaticOwner()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                EarthHumanoidMotionSetup.ControllerPath);
            AnimatorState turn = FindState(controller.layers[0].stateMachine, "Turn In Place");
            Assert.That(turn, Is.Not.Null);
            Assert.That(turn.transitions, Is.Empty,
                "HumanoidCharacterPresentation/EarthTransitionDirector must remain the only turn owner.");
            Assert.That(turn.motion, Is.TypeOf<BlendTree>());
            var tree = (BlendTree)turn.motion;
            Assert.That(tree.blendType, Is.EqualTo(BlendTreeType.Simple1D));
            Assert.That(tree.blendParameter, Is.EqualTo("Turn"));
            ChildMotion[] children = tree.children;
            Assert.That(children, Has.Length.EqualTo(3));
            Array.Sort(children, (a, b) => a.threshold.CompareTo(b.threshold));
            Assert.That(children[0].threshold, Is.EqualTo(-1f));
            Assert.That(AssetDatabase.GetAssetPath(children[0].motion),
                Is.EqualTo(EarthHumanoidMotionSetup.LeftTurnPath));
            Assert.That(children[0].mirror, Is.False);
            Assert.That(children[1].threshold, Is.Zero);
            Assert.That(AssetDatabase.GetAssetPath(children[1].motion),
                Is.EqualTo(EarthHumanoidMotionSetup.IdlePath));
            Assert.That(children[2].threshold, Is.EqualTo(1f));
            Assert.That(AssetDatabase.GetAssetPath(children[2].motion),
                Is.EqualTo(EarthHumanoidMotionSetup.LeftTurnPath));
            Assert.That(children[2].mirror, Is.True);
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
                if (child.state != null && child.state.name == name) return child.state;
            return null;
        }

        private static void AssertTiming(
            EarthMagicClipTiming actual,
            EarthMagicClipTiming expected,
            string slot)
        {
            Assert.That(actual.AcquireEnd, Is.EqualTo(expected.AcquireEnd).Within(.00001f), slot);
            Assert.That(actual.RootEnd, Is.EqualTo(expected.RootEnd).Within(.00001f), slot);
            Assert.That(actual.LoadEnd, Is.EqualTo(expected.LoadEnd).Within(.00001f), slot);
            Assert.That(actual.Contact, Is.EqualTo(expected.Contact).Within(.00001f), slot);
            Assert.That(actual.Sustain, Is.EqualTo(expected.Sustain).Within(.00001f), slot);
            Assert.That(actual.RecoverEnd, Is.EqualTo(expected.RecoverEnd).Within(.00001f), slot);
            Assert.That(actual.AcquireSeconds, Is.EqualTo(expected.AcquireSeconds).Within(.00001f), slot);
            Assert.That(actual.RootSeconds, Is.EqualTo(expected.RootSeconds).Within(.00001f), slot);
            Assert.That(actual.LoadSeconds, Is.EqualTo(expected.LoadSeconds).Within(.00001f), slot);
            Assert.That(actual.StrikeSeconds, Is.EqualTo(expected.StrikeSeconds).Within(.00001f), slot);
            Assert.That(actual.SustainSeconds, Is.EqualTo(expected.SustainSeconds).Within(.00001f), slot);
            Assert.That(actual.RecoverSeconds, Is.EqualTo(expected.RecoverSeconds).Within(.00001f), slot);
        }

        private static void AssertUpperBodyOnly(AvatarMask mask)
        {
            Assert.That(mask, Is.Not.Null);
            Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body), Is.True);
            Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Head), Is.True);
            Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm), Is.True);
            Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm), Is.True);
            Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root), Is.False);
            Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), Is.False);
            Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg), Is.False);
            Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK), Is.False);
            Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK), Is.False);
        }
    }
}
