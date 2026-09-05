using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class AnimationSemanticAuditTestLauncher
    {
        [MenuItem("Elemental/QA/Animation Responsive Hands Edit Audit")]
        public static void RunResponsiveHandsEdit() => Run(
            TestMode.EditMode,
            "AnimationResponsiveHandsEdit",
            "Elemental.Tests.EditMode.EarthResponsiveHandTargetSolverTests");

        [MenuItem("Elemental/QA/Animation Responsive Carry Runtime Audit")]
        public static void RunResponsiveCarryPlay() => Run(
            TestMode.PlayMode,
            "AnimationResponsiveCarryPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.GravityGripCarryKeepsHandsBodyRelativeWhileWalkingAndTurning");

        [MenuItem("Elemental/QA/Animation Semantic Asset Audit")]
        public static void RunEdit() => Run(
            TestMode.EditMode,
            "AnimationSemanticAssetAuditEdit",
            "Elemental.Tests.EditMode.SeptemberAnimationSemanticAssetAuditTests");

        [MenuItem("Elemental/QA/Animation Semantic Runtime Audit")]
        public static void RunPlay() => Run(
            TestMode.PlayMode,
            "AnimationSemanticRuntimeAuditPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RapidBurstKeepsCurrentContactAndLatestRequestWithoutStaleReplay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordStartsPromptlyAndDoesNotReplayAfterRelease",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordIsContinuousAtThirtyHertz",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordIsContinuousAtSixtyHertz",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordIsContinuousAtOneTwentyHertz",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingShortLmbQuickStoneUsesPunchInsteadOfHeavyThrow",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingHeldBodyAimActivatesAfterContactAndReleasesWithoutAFlip",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RepeatedPunchRequestsAlternateBuffersAndExtendAgainWithoutASnap",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RapidCommittedPunchesRenderContactThenLatestRetractsAndExtends",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ConfirmedGameplayBoundariesRenderContactPromptlyForAllSemanticSlots",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.SemanticMagicSlotsBecomeOneHotAndKeepAValidUpperBodyPose",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.WalkStopKeepsKneesFiniteAndAvoidsAOneFrameLegSnap");

        [MenuItem("Elemental/QA/Animation Semantic Magic Edit Audit")]
        public static void RunMagicEdit() => Run(
            TestMode.EditMode,
            "AnimationSemanticMagicEdit",
            "Elemental.Tests.EditMode.SeptemberAnimationSemanticAssetAuditTests",
            "Elemental.Tests.EditMode.EarthChoreographyTests.VisualPoseConsumesEveryDeclaredChoreographyChannel",
            "Elemental.Tests.EditMode.EarthChoreographyTests.ElevenSemanticSlotsHaveFiniteDistinctBoundedUpperBodySignatures",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.LateStartingAndShortFixedTickPhasesCannotSkipAuthoredContact",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.IndependentMagicBufferClockRestartsEverySequenceIncludingSameSlotPunches",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.EveryMagicClipUsesItsMeasuredSourcePacingLimit",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.PersistentFieldAnticipationReachesContactWithinOneSecond",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.CommittedQuickPunchStartsAtContactAndFinishesFollowThroughResponsively",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.CastConstraintsPreserveAuthoredArmsWhileSustainedAimRemainsAvailable",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.HigherPriorityAnimationOwnershipCancelsTransientPendingAndPayloadState",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.RenderContactWatchdogUsesActualCuratedClipPacing",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.QuickStonePunchHasAnExplicitFastSemanticSlot",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.CommittedActionBoundarySupersedesPreContactAnticipationAndPendingWork",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.SameTickPhysicalConfirmationPromotesAnticipationToContact",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.ShippingPhysicalWallPullAndRepairIngressAreContactBoundaries",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.RenderedRecoveryCannotFinishBeforeTheAuthoredClipMarker",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.CommittedBoundaryClockStartsAtContactAndThenAdvancesFollowThrough",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.RepeatedCommittedPunchesKeepCurrentExtensionAndCoalesceLatestFollowUp");

        [MenuItem("Elemental/QA/Animation Semantic Magic Runtime Audit")]
        public static void RunMagicPlay() => Run(
            TestMode.PlayMode,
            "AnimationSemanticMagicPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RapidBurstKeepsCurrentContactAndLatestRequestWithoutStaleReplay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordStartsPromptlyAndDoesNotReplayAfterRelease",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingShortLmbQuickStoneUsesPunchInsteadOfHeavyThrow",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingHeldBodyAimActivatesAfterContactAndReleasesWithoutAFlip",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RepeatedPunchRequestsAlternateBuffersAndExtendAgainWithoutASnap",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RapidCommittedPunchesRenderContactThenLatestRetractsAndExtends",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ConfirmedGameplayBoundariesRenderContactPromptlyForAllSemanticSlots",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.SemanticMagicSlotsBecomeOneHotAndKeepAValidUpperBodyPose");

        [MenuItem("Elemental/QA/Animation Magic Burst Runtime Audit")]
        public static void RunMagicBurstPlay() => Run(
            TestMode.PlayMode,
            "AnimationMagicBurstPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RapidBurstKeepsCurrentContactAndLatestRequestWithoutStaleReplay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordStartsPromptlyAndDoesNotReplayAfterRelease",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingShortLmbQuickStoneUsesPunchInsteadOfHeavyThrow",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingHeldBodyAimActivatesAfterContactAndReleasesWithoutAFlip",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RepeatedPunchRequestsAlternateBuffersAndExtendAgainWithoutASnap",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RapidCommittedPunchesRenderContactThenLatestRetractsAndExtends");

        [MenuItem("Elemental/QA/Animation Held Aim Runtime Audit")]
        public static void RunHeldAimPlay() => Run(
            TestMode.PlayMode,
            "AnimationHeldAimPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingHeldBodyAimActivatesAfterContactAndReleasesWithoutAFlip");

        [MenuItem("Elemental/QA/Animation Ground Wave Commit Runtime Audit")]
        public static void RunGroundWaveCommitPlay() => Run(
            TestMode.PlayMode,
            "AnimationGroundWaveCommitPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingGroundWaveCommitRendersContactWithoutReplayingWindup");

        [MenuItem("Elemental/QA/Animation Gravity C1 Isolation Runtime Audit")]
        public static void RunGravityC1IsolationPlay() => Run(
            TestMode.PlayMode,
            "AnimationGravityC1IsolationPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.GravityGripPreContactWithoutGenericC1MatchesBoundedSourceAtThirtyHertz",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.GravityGripPreContactWithoutGenericC1MatchesBoundedSourceAtSixtyHertz");

        [MenuItem("Elemental/QA/Animation Protected Ownership Runtime Audit")]
        public static void RunProtectedOwnershipPlay() => Run(
            TestMode.PlayMode,
            "AnimationProtectedOwnershipPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProtectedMantleRejectsLateMagicAndDoesNotReplayItAfterLanding",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProtectedRagdollRecoveryRejectsLateMagicUntilCompletion");

        [MenuItem("Elemental/QA/Animation Magic Frame Rate Runtime Audit")]
        public static void RunMagicFrameRatePlay() => Run(
            TestMode.PlayMode,
            "AnimationMagicFrameRatePlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordIsContinuousAtThirtyHertz",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordIsContinuousAtSixtyHertz",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordIsContinuousAtOneTwentyHertz");

        [MenuItem("Elemental/QA/Animation Punch Continuity Runtime Audit")]
        public static void RunPunchContinuityPlay() => Run(
            TestMode.PlayMode,
            "AnimationPunchContinuityPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordStartsPromptlyAndDoesNotReplayAfterRelease",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RepeatedPunchRequestsAlternateBuffersAndExtendAgainWithoutASnap",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RapidCommittedPunchesRenderContactThenLatestRetractsAndExtends");

        [MenuItem("Elemental/QA/Animation Dual Mouse Punch Continuity Runtime Audit")]
        public static void RunDualMousePunchContinuityPlay() => Run(
            TestMode.PlayMode,
            "AnimationDualMousePunchContinuityPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordStartsPromptlyAndDoesNotReplayAfterRelease");

        [MenuItem("Elemental/QA/Animation Dual Mouse IK Isolation Runtime Audit")]
        public static void RunDualMouseIkIsolationPlay() => Run(
            TestMode.PlayMode,
            "AnimationDualMouseIkIsolationPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordWithoutHandConstraintsIsolatesSourceMotion");

        [MenuItem("Elemental/QA/Animation Walk Stop Runtime Audit")]
        public static void RunWalkStopPlay() => Run(
            TestMode.PlayMode,
            "AnimationWalkStopPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.WalkStopKeepsKneesFiniteAndAvoidsAOneFrameLegSnap");

        private static void Run(TestMode mode, string report, params string[] fixtures)
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod(
                "Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[] { mode, report, fixtures });
        }
    }
}
