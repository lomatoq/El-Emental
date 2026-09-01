using System;
using System.Collections.Generic;
using Elemental.Authoring.Editor;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthTransitionProfileTests
    {
        private EarthTransitionProfile _profile;

        [TearDown]
        public void TearDown()
        {
            if (_profile != null) UnityEngine.Object.DestroyImmediate(_profile);
        }

        [Test]
        public void DefaultProfileAndQueueAreOffAndLeaveLegacyResolutionUntouched()
        {
            _profile = ScriptableObject.CreateInstance<EarthTransitionProfile>();
            EarthAnimationTransitionContext context = Context();

            Assert.That(_profile.UseTransitionProfile, Is.False);
            Assert.That(_profile.UseTransitionQueue, Is.False);
            Assert.That(
                _profile.TryResolve(in context, out _, out _, out _),
                Is.False);
        }

        [Test]
        public void ExactPairWinsOverCategoryPairRegardlessOfSerializedOrder()
        {
            EarthTransitionRule categoryRule = Rule(
                EarthTransitionFamily.PhaseSynchronized,
                EarthAnimationTransitionPriority.Locomotion);
            EarthTransitionRule exactRule = Rule(
                EarthTransitionFamily.PoseInertialized,
                EarthAnimationTransitionPriority.LandingContact,
                halfLife: 0.055f,
                bodyMask: EarthTransitionBodyMask.Spine |
                          EarthTransitionBodyMask.LeftArm |
                          EarthTransitionBodyMask.RightArm);
            var pairs = new[]
            {
                new EarthTransitionPairOverride(
                    EarthMotionCategory.Turn,
                    EarthMotionCategory.Locomotion,
                    in categoryRule),
                new EarthTransitionPairOverride(
                    EarthMotionStateId.TurnInPlace,
                    EarthMotionStateId.Locomotion,
                    in exactRule)
            };
            _profile = EnabledProfile(pairs);
            EarthAnimationTransitionContext context = Context();

            Assert.That(
                _profile.TryResolve(
                    in context,
                    out EarthTransitionRule resolved,
                    out int pairIndex,
                    out bool fallback),
                Is.True);
            Assert.That(pairIndex, Is.EqualTo(1));
            Assert.That(fallback, Is.False);
            Assert.That(resolved.Family, Is.EqualTo(EarthTransitionFamily.PoseInertialized));
            Assert.That(resolved.HalfLifeSeconds, Is.EqualTo(0.055f).Within(0.0001f));
            Assert.That(resolved.BodyMask, Is.EqualTo(exactRule.BodyMask));
        }

        [Test]
        public void PairRoundTripCarriesEveryAuthoredRuntimeField()
        {
            EarthNormalizedAnimationWindow protectedWindow =
                new EarthNormalizedAnimationWindow(true, 0.82f, 0.18f);
            EarthNormalizedAnimationWindow cancelWindow =
                new EarthNormalizedAnimationWindow(true, 0.32f, 0.58f);
            EarthTransitionRule authored = new EarthTransitionRule(
                true,
                EarthTransitionFamily.ContactAligned,
                EarthAnimationTransitionPriority.HeavyImpact,
                0.047f,
                0.13f,
                EarthTransitionGaitPhaseRule.FixedTarget,
                EarthTransitionContactPolicy.AuthoredLandingContact,
                EarthTransitionCancelPolicy.InsideCancelWindow,
                in protectedWindow,
                in cancelWindow,
                0.41f,
                EarthTransitionBodyMask.Pelvis | EarthTransitionBodyMask.LeftLeg,
                EarthTransitionFootReleasePolicy.ReleaseAfterDelay,
                0.075f,
                true);
            var pair = new EarthTransitionPairOverride(
                EarthMotionStateId.Fall,
                EarthMotionStateId.HardLanding,
                in authored);

            EarthTransitionRule roundTrip = pair.ToRule();

            Assert.That(roundTrip.Family, Is.EqualTo(authored.Family));
            Assert.That(roundTrip.Priority, Is.EqualTo(authored.Priority));
            Assert.That(roundTrip.HalfLifeSeconds, Is.EqualTo(authored.HalfLifeSeconds));
            Assert.That(roundTrip.FallbackDurationSeconds,
                Is.EqualTo(authored.FallbackDurationSeconds));
            Assert.That(roundTrip.GaitPhaseRule, Is.EqualTo(authored.GaitPhaseRule));
            Assert.That(roundTrip.ContactPolicy, Is.EqualTo(authored.ContactPolicy));
            Assert.That(roundTrip.CancelPolicy, Is.EqualTo(authored.CancelPolicy));
            Assert.That(roundTrip.ProtectedWindow.Start01,
                Is.EqualTo(authored.ProtectedWindow.Start01));
            Assert.That(roundTrip.ProtectedWindow.End01,
                Is.EqualTo(authored.ProtectedWindow.End01));
            Assert.That(roundTrip.CancelWindow.Start01,
                Is.EqualTo(authored.CancelWindow.Start01));
            Assert.That(roundTrip.CancelWindow.End01,
                Is.EqualTo(authored.CancelWindow.End01));
            Assert.That(roundTrip.TargetPhase01, Is.EqualTo(authored.TargetPhase01));
            Assert.That(roundTrip.BodyMask, Is.EqualTo(authored.BodyMask));
            Assert.That(roundTrip.FootReleasePolicy, Is.EqualTo(authored.FootReleasePolicy));
            Assert.That(roundTrip.FootReleaseSeconds, Is.EqualTo(authored.FootReleaseSeconds));
            Assert.That(roundTrip.QueueWhenBlocked, Is.True);
        }

        [Test]
        public void MissingPairReturnsExplicitFiniteFixedCrossfadeFallback()
        {
            _profile = EnabledProfile(Array.Empty<EarthTransitionPairOverride>());
            EarthAnimationTransitionContext context = Context(
                EarthMotionStateId.Fall,
                EarthMotionStateId.Jump,
                EarthMotionCategory.Airborne,
                EarthMotionCategory.Airborne);

            Assert.That(
                _profile.TryResolve(
                    in context,
                    out EarthTransitionRule rule,
                    out int pairIndex,
                    out bool fallback),
                Is.True);
            EarthAnimationTransitionDecision decision =
                EarthTransitionRulePolicy.Resolve(in context, in rule);

            Assert.That(pairIndex, Is.EqualTo(-1));
            Assert.That(fallback, Is.True);
            Assert.That(rule.Family, Is.EqualTo(EarthTransitionFamily.FixedDurationFallback));
            Assert.That(decision.Kind,
                Is.EqualTo(EarthAnimationTransitionKind.FixedDurationFallback));
            Assert.That(decision.Reason,
                Is.EqualTo(EarthAnimationTransitionReason.ProfileFallback));
            Assert.That(decision.DurationSeconds, Is.EqualTo(0.09f).Within(0.0001f));
            Assert.That(float.IsFinite(decision.DurationSeconds), Is.True);
        }

        [Test]
        public void HotPairResolutionAllocatesNoManagedMemory()
        {
            EarthTransitionRule rule = Rule(
                EarthTransitionFamily.PhaseSynchronized,
                EarthAnimationTransitionPriority.Locomotion);
            _profile = EnabledProfile(new[]
            {
                new EarthTransitionPairOverride(
                    EarthMotionStateId.TurnInPlace,
                    EarthMotionStateId.Locomotion,
                    in rule)
            });
            EarthAnimationTransitionContext context = Context();
            _profile.TryResolve(in context, out _, out _, out _);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 10000; index++)
            {
                if (!_profile.TryResolve(
                        in context,
                        out EarthTransitionRule resolved,
                        out int pairIndex,
                        out bool fallback) ||
                    pairIndex != 0 || fallback || !resolved.Configured)
                    Assert.Fail("deterministic profile resolution changed");
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void HumanoidBodyMaskMappingNeverIncludesGameplayRootTranslation()
        {
            Assert.That(
                EarthAnimationBoneMask.BodyMaskFor(HumanBodyBones.Hips),
                Is.EqualTo(EarthTransitionBodyMask.Pelvis));
            Assert.That(
                EarthAnimationBoneMask.BodyMaskFor(HumanBodyBones.LeftFoot),
                Is.EqualTo(EarthTransitionBodyMask.LeftLeg));
            Assert.That(
                EarthAnimationBoneMask.BodyMaskFor(HumanBodyBones.RightHand),
                Is.EqualTo(EarthTransitionBodyMask.RightArm));
            for (int index = 0; index < EarthAnimationBoneMask.TrackedBoneCount; index++)
            {
                EarthTransitionBodyMask mask = EarthAnimationBoneMask.BodyMaskFor(
                    EarthAnimationBoneMask.BoneAt(index));
                Assert.That(mask & EarthTransitionBodyMask.Root,
                    Is.EqualTo(EarthTransitionBodyMask.None));
            }
        }

        [Test]
        public void ValidatorAcceptsUniquePairAndRejectsDuplicateSelector()
        {
            EarthTransitionRule rule = Rule(
                EarthTransitionFamily.PoseInertialized,
                EarthAnimationTransitionPriority.Locomotion);
            var pair = new EarthTransitionPairOverride(
                EarthMotionStateId.TurnInPlace,
                EarthMotionStateId.Locomotion,
                in rule);
            _profile = EnabledProfile(new[] { pair });
            var errors = new List<string>();

            Assert.That(EarthTransitionProfileValidator.Validate(_profile, errors), Is.True);
            Assert.That(errors, Is.Empty);

            _profile.Configure(true, false, 4, 0.08f, new[] { pair, pair });
            errors.Clear();
            Assert.That(EarthTransitionProfileValidator.Validate(_profile, errors), Is.False);
            Assert.That(
                errors.Exists(error => error.Contains("duplicates selector")),
                Is.True);
        }

        [Test]
        public void ProductionProfileResolvesEveryObservedRescuePairWithoutGenericFallback()
        {
            const string path =
                "Assets/Elemental/Content/Profiles/EarthTransitionProfile.asset";
            EarthTransitionProfile production =
                AssetDatabase.LoadAssetAtPath<EarthTransitionProfile>(path);
            Assert.That(production, Is.Not.Null, path);

            AssertProductionPair(
                production,
                EarthMotionStateId.None,
                EarthMotionStateId.Locomotion,
                EarthMotionCategory.None,
                EarthMotionCategory.Locomotion);
            AssertProductionPair(
                production,
                EarthMotionStateId.Jump,
                EarthMotionStateId.Fall,
                EarthMotionCategory.Airborne,
                EarthMotionCategory.Airborne);
            AssertProductionPair(
                production,
                EarthMotionStateId.Fall,
                EarthMotionStateId.Jump,
                EarthMotionCategory.Airborne,
                EarthMotionCategory.Airborne);
            AssertProductionPair(
                production,
                EarthMotionStateId.TurnInPlace,
                EarthMotionStateId.Jump,
                EarthMotionCategory.Turn,
                EarthMotionCategory.Airborne);
            AssertProductionPair(
                production,
                EarthMotionStateId.HardLanding,
                EarthMotionStateId.Fall,
                EarthMotionCategory.Landing,
                EarthMotionCategory.Airborne);
            AssertProductionPair(
                production,
                EarthMotionStateId.Locomotion,
                EarthMotionStateId.SoftLanding,
                EarthMotionCategory.Locomotion,
                EarthMotionCategory.Landing);
            AssertProductionPair(
                production,
                EarthMotionStateId.Locomotion,
                EarthMotionStateId.MovingLanding,
                EarthMotionCategory.Locomotion,
                EarthMotionCategory.Landing);
            AssertProductionPair(
                production,
                EarthMotionStateId.Locomotion,
                EarthMotionStateId.HardLanding,
                EarthMotionCategory.Locomotion,
                EarthMotionCategory.Landing);
            AssertProductionPair(
                production,
                EarthMotionStateId.Locomotion,
                EarthMotionStateId.KnockdownRecovery,
                EarthMotionCategory.Locomotion,
                EarthMotionCategory.RagdollRecovery);
            AssertProductionPair(
                production,
                EarthMotionStateId.SoftLanding,
                EarthMotionStateId.Fall,
                EarthMotionCategory.Landing,
                EarthMotionCategory.Airborne);
        }

        private EarthTransitionProfile EnabledProfile(
            EarthTransitionPairOverride[] pairs)
        {
            EarthTransitionProfile profile =
                ScriptableObject.CreateInstance<EarthTransitionProfile>();
            profile.Configure(true, true, 8, 0.09f, pairs);
            return profile;
        }

        private static void AssertProductionPair(
            EarthTransitionProfile profile,
            EarthMotionStateId source,
            EarthMotionStateId destination,
            EarthMotionCategory sourceCategory,
            EarthMotionCategory destinationCategory)
        {
            EarthAnimationTransitionContext context = Context(
                source,
                destination,
                sourceCategory,
                destinationCategory);
            Assert.That(
                profile.TryResolve(
                    in context,
                    out EarthTransitionRule rule,
                    out int pairIndex,
                    out bool usedGenericFallback),
                Is.True,
                $"{source} -> {destination}");
            Assert.That(usedGenericFallback, Is.False, $"{source} -> {destination}");
            Assert.That(pairIndex, Is.GreaterThanOrEqualTo(0), $"{source} -> {destination}");
            Assert.That(rule.Family, Is.Not.EqualTo(EarthTransitionFamily.FixedDurationFallback));
        }

        private static EarthTransitionRule Rule(
            EarthTransitionFamily family,
            EarthAnimationTransitionPriority priority,
            float halfLife = 0.08f,
            EarthTransitionBodyMask bodyMask = EarthTransitionBodyMask.FullBody)
        {
            EarthNormalizedAnimationWindow disabled = default;
            return new EarthTransitionRule(
                true,
                family,
                priority,
                halfLife,
                0.1f,
                EarthTransitionGaitPhaseRule.PreserveSource,
                EarthTransitionContactPolicy.PreserveCurrentPlants,
                EarthTransitionCancelPolicy.Always,
                in disabled,
                in disabled,
                0f,
                bodyMask,
                EarthTransitionFootReleasePolicy.PreservePlanted,
                0f,
                true);
        }

        private static EarthAnimationTransitionContext Context(
            EarthMotionStateId source = EarthMotionStateId.TurnInPlace,
            EarthMotionStateId destination = EarthMotionStateId.Locomotion,
            EarthMotionCategory sourceCategory = EarthMotionCategory.Turn,
            EarthMotionCategory destinationCategory = EarthMotionCategory.Locomotion) =>
            new EarthAnimationTransitionContext(
                source,
                destination,
                sourceCategory,
                destinationCategory,
                EarthAnimationTransitionPriority.Locomotion,
                EarthAnimationTransitionPriority.Idle,
                0.5f,
                0.25f,
                1.2f,
                0.6f,
                0.1f,
                false,
                true,
                false,
                true);
    }
}
