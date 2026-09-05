using System.Reflection;
using Elemental.Authoring;
using Elemental.Authoring.Editor;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class SeptemberAnimationRescueTests
    {
        [Test]
        public void EverySemanticSlotProgressesThroughAllMarkersAtThirtySixtyAndOneTwentyHz()
        {
            foreach (int hz in new[] { 30, 60, 120 })
            for (int slot = 1; slot <= 11; slot++)
            {
                EarthMagicClipClock clock = default;
                EarthMagicClipTiming timing = EarthMagicClipTiming.Default;
                float previous = 0f;
                foreach (EarthCastPhase phase in new[] { EarthCastPhase.Acquire, EarthCastPhase.Root,
                    EarthCastPhase.Load, EarthCastPhase.Strike, EarthCastPhase.Sustain, EarthCastPhase.Recover })
                {
                    float maximumSpeed = EarthMagicClipClock.MaximumSpeedForSlot(slot);
                    int frames = Mathf.Max(
                        Mathf.CeilToInt(timing.Seconds(phase) * hz) + 1,
                        Mathf.CeilToInt(
                            (timing.End(phase) - previous) / maximumSpeed * hz) + 2);
                    int intermediate = 0;
                    for (int i = 0; i < frames; i++)
                    {
                        float value = clock.Step(slot, 1, phase, true, in timing, 1f / hz);
                        Assert.That(value, Is.GreaterThanOrEqualTo(previous - 0.00001f));
                        if (value > previous + 0.00001f && value < timing.End(phase) - 0.00001f) intermediate++;
                        previous = value;
                    }
                    Assert.That(intermediate, Is.GreaterThan(0), $"Slot {slot} phase {phase} was a static jump at {hz} Hz.");
                    Assert.That(clock.NormalizedTime, Is.EqualTo(timing.End(phase)).Within(0.0001f));
                }
                clock.Step(slot, 1, EarthCastPhase.Recover, false, in timing, 1f / hz);
                float restart = clock.Step(slot, 2, EarthCastPhase.Strike, true, in timing, 1f / hz);
                Assert.That(restart, Is.LessThan(timing.Contact), "A new accepted cast restarts its own clip clock.");
            }
        }

        [Test]
        public void LateStartingAndShortFixedTickPhasesCannotSkipAuthoredContact()
        {
            EarthMagicClipTiming timing = EarthMagicMotionProfile.CreateDefaults()[3].timing;
            EarthMagicClipClock clock = default;

            float firstVisibleStrike = clock.Step(
                (int)EarthHumanoidPoseSlot.HeavyThrow, 7u, EarthCastPhase.Strike,
                true, in timing, 1f / 60f);
            Assert.That(firstVisibleStrike, Is.GreaterThan(0f),
                "A post-commit event did not begin moving toward its authored contact.");
            Assert.That(firstVisibleStrike, Is.LessThan(timing.Contact));

            float previous = firstVisibleStrike;
            for (int frame = 0; frame < 20; frame++)
            {
                float sustain = clock.Step(
                    (int)EarthHumanoidPoseSlot.HeavyThrow, 7u, EarthCastPhase.Sustain,
                    true, in timing, 1f / 60f);
                Assert.That(sustain, Is.GreaterThanOrEqualTo(previous - .00001f),
                    "A phase handoff rewound the authored clip.");
                Assert.That(sustain - previous,
                    Is.LessThanOrEqualTo(
                        EarthMagicClipClock.MaximumNormalizedSpeedPerSecond / 60f + .0001f),
                    "A phase handoff skipped source animation in one rendered step.");
                previous = sustain;
            }
            Assert.That(previous, Is.GreaterThanOrEqualTo(timing.Contact),
                "Continuous progression never reached the authored contact marker.");
        }

        [Test]
        public void IndependentMagicBufferClockRestartsEverySequenceIncludingSameSlotPunches()
        {
            EarthMagicClipTiming timing = EarthMagicClipTiming.Default;
            EarthMagicClipClock clock = default;
            float contact = 0f;
            int contactBudget = Mathf.CeilToInt(
                timing.Contact /
                EarthMagicClipClock.MaximumSpeedForSlot(3) * 60f) + 60;
            for (int frame = 0;
                 frame < contactBudget && contact + .0001f < timing.Contact;
                 frame++)
                contact = clock.Step(3, 10u, EarthCastPhase.Strike, true, in timing, 1f / 60f);
            Assert.That(contact, Is.EqualTo(timing.Contact).Within(.0001f));

            float handoff = clock.Step(4, 11u, EarthCastPhase.Acquire, true, in timing, 1f / 60f);
            Assert.That(handoff, Is.LessThan(timing.AcquireEnd),
                "The inactive A/B buffer did not restart a new semantic clip from anticipation.");

            for (int frame = 0; frame < 40; frame++)
                clock.Step(11, 12u, EarthCastPhase.Strike, true, in timing, 1f / 60f);
            float repeatedPunch = clock.Step(
                11, 13u, EarthCastPhase.Acquire, true, in timing, 1f / 60f);
            Assert.That(repeatedPunch, Is.LessThan(timing.AcquireEnd),
                "A repeated same-slot punch stayed frozen at the prior contact pose.");

            clock.Step(11, 13u, EarthCastPhase.Recover, false, in timing, 1f / 60f);
            float independent = clock.Step(5, 12u, EarthCastPhase.Acquire, true, in timing, 1f / 60f);
            Assert.That(independent, Is.LessThan(timing.Contact),
                "An independent cast after layer release must still begin at anticipation.");
        }

        [Test]
        public void DensePullAndPunchClipsUseTimeBasedPerClipPlaybackLimits()
        {
            Assert.That(EarthMagicClipClock.MaximumSpeedForSlot(
                    (int)EarthHumanoidPoseSlot.PullStone),
                Is.EqualTo(EarthMagicClipClock.PullStoneMaximumNormalizedSpeedPerSecond));
            Assert.That(EarthMagicClipClock.MaximumSpeedForSlot(
                    (int)EarthHumanoidPoseSlot.GenericCast),
                Is.EqualTo(EarthMagicClipClock.QuickPunchMaximumNormalizedSpeedPerSecond));
            Assert.That(EarthMagicClipClock.MaximumSpeedForSlot(
                    (int)EarthHumanoidPoseSlot.RaiseWall),
                Is.EqualTo(EarthMagicClipClock.MaximumNormalizedSpeedPerSecond));

            foreach (int hz in new[] { 30, 60, 120 })
            {
                EarthMagicClipClock pull = default;
                EarthMagicClipTiming timing = EarthMagicClipTiming.Default;
                float previous = 0f;
                for (int frame = 0; frame < hz; frame++)
                {
                    float current = pull.Step(
                        (int)EarthHumanoidPoseSlot.PullStone,
                        10u,
                        EarthCastPhase.Strike,
                        true,
                        in timing,
                        1f / hz);
                    Assert.That(current - previous,
                        Is.LessThanOrEqualTo(
                            EarthMagicClipClock.PullStoneMaximumNormalizedSpeedPerSecond / hz +
                            .0001f));
                    previous = current;
                }
            }
        }

        [Test]
        public void QuickStonePunchHasAnExplicitFastSemanticSlot()
        {
            Assert.That(EarthHumanoidMotionResolver.Resolve(EarthTechniqueId.QuickStonePunch),
                Is.EqualTo(EarthHumanoidPoseSlot.GenericCast));
            Assert.That(MagicPresentationSemanticResolver.ResolveKind(
                    EarthTechniqueId.QuickStonePunch),
                Is.EqualTo(EarthTechniqueKind.Grip));
            Assert.That(EarthHumanoidMotionResolver.Resolve(EarthTechniqueId.ThrowStone),
                Is.EqualTo(EarthHumanoidPoseSlot.HeavyThrow));
        }

        [Test]
        public void DampedParametersProgressContinuouslyAndAgreeAcrossFrameRates()
        {
            float reference = 0f;
            foreach (int hz in new[] { 30, 60, 120 })
            {
                float value = 0f;
                float first = EarthAnimationDriver.DampParameter(value, 0.88f, 0.075f, 1f / hz);
                Assert.That(first, Is.InRange(0.001f, 0.87f), "The playable path must not jump to a phase sample.");
                for (int i = 0; i < hz; i++) value = EarthAnimationDriver.DampParameter(value, 0.88f, 0.075f, 1f / hz);
                if (hz == 30) reference = value;
                Assert.That(value, Is.EqualTo(reference).Within(0.00001f));
                Assert.That(EarthAnimationDriver.DampParameter(0.3f, 0.8f, 0.1f, 0f), Is.EqualTo(0.3f));
            }
        }

        [Test]
        public void CastConstraintsPreserveAuthoredArmsWhileSustainedAimRemainsAvailable()
        {
            Assert.That(HumanoidCharacterPresentation.ResolveHandConstraintTarget(
                false, true, 1f, .62f), Is.Zero,
                "A timed one-shot Sustain label is not persistent aim ownership.");
            Assert.That(HumanoidCharacterPresentation.ResolveHandConstraintTarget(
                true, false, 1f, .62f), Is.Zero,
                "Held aim must wait until the authored contact was rendered.");
            Assert.That(HumanoidCharacterPresentation.ResolveHandConstraintTarget(
                true, true, .8f, .62f), Is.EqualTo(.496f).Within(.0001f));
        }

        [Test]
        public void HigherPriorityAnimationOwnershipCancelsTransientPendingAndPayloadState()
        {
            GameObject root = new GameObject("Animation ownership cancellation test");
            try
            {
                EarthCharacterPoseController pose = root.AddComponent<EarthCharacterPoseController>();
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Grip,
                    EarthTechniqueId.PullStone,
                    71u,
                    Vector3.forward * 2f,
                    80f,
                    12f);
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Wall,
                    EarthTechniqueId.RaiseWall,
                    72u,
                    Vector3.forward * 3f,
                    320f,
                    6f);
                Assert.That(pose.HasAuthoritativePresentation, Is.True);
                Assert.That(pose.QueuedPresentationCount, Is.EqualTo(1));

                pose.SetPresentationSuppressed(true);

                Assert.That(pose.HasAuthoritativePresentation, Is.False);
                Assert.That(pose.PresentationSuppressed, Is.True);
                Assert.That(pose.QueuedPresentationCount, Is.Zero);
                Assert.That(pose.CurrentRequest.IsActive, Is.False);
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Grip,
                    EarthTechniqueId.QuickStonePunch,
                    73u,
                    Vector3.forward,
                    20f,
                    4f);
                Assert.That(pose.HasAuthoritativePresentation, Is.False,
                    "A late magic event revived presentation under the protected owner.");
                Assert.That(pose.QueuedPresentationCount, Is.Zero);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                Assert.That((float)typeof(EarthCharacterPoseController)
                    .GetField("_eventMass", flags).GetValue(pose), Is.Zero);
                Assert.That((float)typeof(EarthCharacterPoseController)
                    .GetField("_eventAcceleration", flags).GetValue(pose), Is.Zero);
                pose.SetPresentationSuppressed(false);
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Grip,
                    EarthTechniqueId.QuickStonePunch,
                    74u,
                    Vector3.forward,
                    20f,
                    4f);
                Assert.That(pose.HasAuthoritativePresentation, Is.True,
                    "Presentation did not resume after the protected owner released the skeleton.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CommittedActionBoundarySupersedesPreContactAnticipationAndPendingWork()
        {
            GameObject root = new GameObject("Committed magic boundary test");
            try
            {
                EarthCharacterPoseController pose = root.AddComponent<EarthCharacterPoseController>();
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Pillar, EarthTechniqueId.PillarJump, 101u,
                    Vector3.forward, 60f, 5f);
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Grip, EarthTechniqueId.PullStone, 102u,
                    Vector3.forward * 2f, 60f, 5f);
                Assert.That(pose.QueuedPresentationCount, Is.EqualTo(1));

                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Grip, EarthTechniqueId.QuickStonePunch, 103u,
                    Vector3.forward * 3f, 30f, 26f,
                    immediateActionBoundary: true);

                Assert.That(pose.LastAuthoritativeTick, Is.EqualTo(103u));
                Assert.That(pose.QueuedPresentationCount, Is.Zero);
            Assert.That(pose.HasAuthoritativePresentation, Is.True);
            Assert.That(pose.AuthoritativeStartsAtContact, Is.True,
                "A committed physical launch must enter at authored contact rather than replaying wind-up after release.");
            Assert.That(pose.SupersededPresentationRequests, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SameTickPhysicalConfirmationPromotesAnticipationToContact()
        {
            GameObject root = new GameObject("Same-tick magic commit test");
            try
            {
                EarthCharacterPoseController pose = root.AddComponent<EarthCharacterPoseController>();
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Wall, EarthTechniqueId.RaiseWall, 301u,
                    Vector3.forward, 300f, 8f);
                Assert.That(pose.AuthoritativeStartsAtContact, Is.False,
                    "The input/load admission must preserve authored anticipation.");
                uint anticipationGeneration = pose.AuthoritativePresentationGeneration;

                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Wall, EarthTechniqueId.RaiseWall, 301u,
                    Vector3.forward, 300f, 8f,
                    immediateActionBoundary: true);
                Assert.That(pose.LastAuthoritativeTick, Is.EqualTo(301u));
                Assert.That(pose.AuthoritativeStartsAtContact, Is.True,
                    "The accepted physical result was discarded as a same-tick duplicate.");
                Assert.That(pose.AuthoritativePresentationGeneration,
                    Is.GreaterThan(anticipationGeneration),
                    "The render owner cannot distinguish the promoted contact buffer from its same-tick anticipation buffer.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ShippingPhysicalWallPullAndRepairIngressAreContactBoundaries()
        {
            (string method, object payload, uint tick)[] admissions =
            {
                ("OnWallRaised", new WallRaisedEvent(
                    401u, 1u, float3.zero, new float3(2f, 0f, 0f), 1.5f, .25f), 401u),
                ("OnFragmentSpawned", new FragmentSpawnedEvent(
                    402u, 2u, 30f, new float3(0f, 1f, 2f), float3.zero,
                    float3.zero, .4f), 402u),
                ("OnMagicCommandExecuted", new MagicCommand(
                    403u, 1u, ElementId.Air, new AbilityId(102),
                    float3.zero, new float3(0f, 0f, 1f), null, .7f, 0u, 9u), 403u)
            };
            foreach ((string method, object payload, uint tick) in admissions)
            {
                GameObject root = new GameObject($"Committed {method} test");
                try
                {
                    EarthCharacterPoseController pose = root.AddComponent<EarthCharacterPoseController>();
                    MethodInfo ingress = typeof(EarthCharacterPoseController).GetMethod(
                        method, BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.That(ingress, Is.Not.Null);
                    ingress.Invoke(pose, new[] { payload });
                    Assert.That(pose.LastAuthoritativeTick, Is.EqualTo(tick), method);
                    Assert.That(pose.AuthoritativeStartsAtContact, Is.True,
                        $"{method} accepted a physical result but replayed wind-up after it.");
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void RenderedRecoveryCannotFinishBeforeTheAuthoredClipMarker()
        {
            Assert.That(EarthCharacterPoseController.RequiresRenderedRecovery(.50f, .98f), Is.True);
            Assert.That(EarthCharacterPoseController.RequiresRenderedRecovery(.9794f, .98f), Is.True);
            Assert.That(EarthCharacterPoseController.RequiresRenderedRecovery(.9796f, .98f), Is.False);
            Assert.That(EarthCharacterPoseController.RequiresRenderedRecovery(.99f, .98f), Is.False);
        }

        [Test]
        public void CommittedBoundaryClockStartsAtContactAndThenAdvancesFollowThrough()
        {
            EarthMagicClipTiming timing = EarthMagicClipTiming.Default;
            EarthMagicClipClock clock = default;
            float contact = clock.Step(
                (int)EarthHumanoidPoseSlot.GenericCast,
                501u,
                EarthCastPhase.Strike,
                true,
                in timing,
                1f / 60f,
                startAtContact: true);
            Assert.That(contact, Is.EqualTo(timing.Contact).Within(.0001f));

            float followThrough = contact;
            for (int frame = 0; frame < 12; frame++)
                followThrough = clock.Step(
                    (int)EarthHumanoidPoseSlot.GenericCast,
                    501u,
                    EarthCastPhase.Sustain,
                    true,
                    in timing,
                    1f / 60f,
                    startAtContact: true);
            Assert.That(followThrough, Is.GreaterThan(contact + .04f),
                "Release-aligned punch stayed frozen at contact instead of following through.");
        }

        [Test]
        public void RepeatedCommittedPunchesKeepCurrentExtensionAndCoalesceLatestFollowUp()
        {
            GameObject root = new GameObject("Rapid committed punch admission test");
            try
            {
                EarthCharacterPoseController pose = root.AddComponent<EarthCharacterPoseController>();
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Grip, EarthTechniqueId.QuickStonePunch, 201u,
                    Vector3.forward, 30f, 26f,
                    immediateActionBoundary: true);
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Grip, EarthTechniqueId.QuickStonePunch, 202u,
                    Vector3.forward * 2f, 30f, 26f,
                    immediateActionBoundary: true);
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Grip, EarthTechniqueId.QuickStonePunch, 203u,
                    Vector3.forward * 3f, 30f, 26f,
                    immediateActionBoundary: true);

                Assert.That(pose.LastAuthoritativeTick, Is.EqualTo(201u));
                Assert.That(pose.QueuedPresentationCount, Is.EqualTo(1));
                Assert.That(pose.SupersededPresentationRequests, Is.EqualTo(1));

                pose.NotifyRenderedMagicSample(201u, .48f, .47f, .96f, 1f, 1f);
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Grip, EarthTechniqueId.QuickStonePunch, 204u,
                    Vector3.forward * 4f, 30f, 26f,
                    immediateActionBoundary: true);

                Assert.That(pose.LastAuthoritativeTick, Is.EqualTo(204u));
                Assert.That(pose.AuthoritativeStartsAtContact, Is.False,
                    "A repeated same punch must retract from zero and extend again, not freeze at contact.");
                Assert.That(pose.QueuedPresentationCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UprightIdleIsSharedBySavedAnimatorAndEammRecipe()
        {
            var library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(EarthAnimationRescueSetup.LibraryPath);
            Assert.That(library, Is.Not.Null);
            int idleCount = 0;
            foreach (var recipe in library.clips)
            {
                if (recipe.role != MotionClipRole.Idle) continue;
                Assert.That(AssetDatabase.GetAssetPath(recipe.clip), Is.EqualTo(EarthHumanoidMotionSetup.IdlePath));
                Assert.That(recipe.clip.isHumanMotion, Is.True);
                Assert.That(recipe.clip.length, Is.GreaterThan(0.1f));
                idleCount++;
            }
            Assert.That(idleCount, Is.GreaterThan(0));
            int trees = 0;
            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(EarthHumanoidMotionSetup.ControllerPath))
            {
                if (obj is not BlendTree tree || (tree.name != "Earth Locomotion 2D" && tree.name != "Earth Turn In Place")) continue;
                foreach (var child in tree.children)
                {
                    bool neutral = tree.name == "Earth Turn In Place" ? Mathf.Abs(child.threshold) < 0.0001f : child.position.sqrMagnitude < 0.0001f;
                    if (neutral) Assert.That(AssetDatabase.GetAssetPath(child.motion), Is.EqualTo(EarthHumanoidMotionSetup.IdlePath));
                }
                trees++;
            }
            Assert.That(trees, Is.GreaterThanOrEqualTo(2));
        }
    }
}
