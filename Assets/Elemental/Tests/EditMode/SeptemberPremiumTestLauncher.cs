using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class SeptemberPremiumTestLauncher
    {
        [MenuItem("Elemental/QA/September Premium Edit")]
        public static void Edit() => Run(TestMode.EditMode, "SeptemberPremiumEdit",
            "Elemental.Tests.EditMode.SeptemberPivotContactTests",
            "Elemental.Tests.EditMode.EarthFootSupportAuthorityIntegrationTests",
            "Elemental.Tests.EditMode.EarthDuelRespawnSolverTests");

        public static void TransitionsEdit() => Run(TestMode.EditMode, "SeptemberTransitionsEdit",
            "Elemental.Tests.EditMode.EarthShortTransitionPolicyTests",
            "Elemental.Tests.EditMode.EarthChargeFeedbackTests");

        public static void DropPlay() => Run(TestMode.PlayMode, "SeptemberShortDropPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionBackwardShortDropUsesStepDownAndYieldsAtRealContact");

        public static void RemainingPlay() => Run(TestMode.PlayMode, "SeptemberPremiumRemainingPlay",
            "Elemental.Tests.PlayMode.EarthChargeFeedbackRuntimeTests",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionGroundedPillarCancellationUsesCrouchExitWithoutLaunching",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionBackwardShortDropUsesStepDownAndYieldsAtRealContact");

        public static void FocusedPlay() => Run(TestMode.PlayMode, "SeptemberPremiumFocusedPlay",
            "Elemental.Tests.PlayMode.EarthChargeFeedbackRuntimeTests",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionStartWalkPlaysOnceReturnsToLoopAndCancelsOnBackpedal",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionGroundedPillarCancellationUsesCrouchExitWithoutLaunching",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionBackwardShortDropUsesStepDownAndYieldsAtRealContact",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.BackwardArenaDescentReleasesOldFloorAndKeepsLegLengths");

        [MenuItem("Elemental/QA/September Premium Play")]
        public static void Play() => Run(TestMode.PlayMode, "SeptemberPremiumPlay",
            "Elemental.Tests.PlayMode.SeptemberRespawnRuntimeTests",
            "Elemental.Tests.PlayMode.EarthDuelHudPlayTests",
            "Elemental.Tests.PlayMode.EarthChargeFeedbackRuntimeTests",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionStartWalkPlaysOnceReturnsToLoopAndCancelsOnBackpedal",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionGroundedPillarCancellationUsesCrouchExitWithoutLaunching",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionBackwardShortDropUsesStepDownAndYieldsAtRealContact",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionIdleTurnsAndStopsKeepFinalPoseAndAuthoredTurnOwnership",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.BackwardArenaDescentReleasesOldFloorAndKeepsLegLengths",
            "Elemental.Tests.PlayMode.EarthMagicExpansionRuntimeTests.FallingSpaceCushionCapsDescentWithoutInjectingImpactDamage",
            "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.HighFallOntoLandingCushionDoesNotRagdollOrKillPlayer");

        private static void Run(TestMode mode, string report, params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mode, report, fixtures });
    }
}
