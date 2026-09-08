using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class GameplayTuningTestLauncher
    {
        [MenuItem("Elemental/QA/Stone Stagger Capture Play")]
        public static void RunStoneStaggerCapturePlay() => Run(TestMode.PlayMode, "StoneStaggerCapturePlay",
            "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.ProductionStoneStaggerBendsAndRecoversWithCaptures");
        [MenuItem("Elemental/QA/Impact Readability Play")]
        public static void RunImpactReadabilityPlay() => Run(TestMode.PlayMode, "ImpactReadabilityPlay",
            "Elemental.Tests.PlayMode.EarthDuelHealthPlayTests",
            "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.ProductionStoneStaggerBendsAndRecoversWithCaptures",
            "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.SurfWaveAndBotProjectileUseTheSharedVisibleKnockoutPipeline");
        [MenuItem("Elemental/QA/Seismic Capacity Repair Play")]
        public static void RunSeismicCapacityPlay() => Run(TestMode.PlayMode, "SeismicCapacityRepairPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RunningKeepsSeismicReadabilityWithoutDensePulseSpam",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.SeismicVisionRevealsNightGeometryAndImmediatelyLosesAirborneSupport",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.SeismicEnemyWaitsForWaveAndRendersThroughOpaqueWall");
        [MenuItem("Elemental/QA/Accumulation Camera Play")]
        public static void RunAccumulationCameraPlay() => Run(TestMode.PlayMode, "AccumulationCameraPlay",
            "Elemental.Tests.PlayMode.EarthAccumulationChargeRuntimeTests");
        [MenuItem("Elemental/QA/Impact Readability Edit")]
        public static void RunImpactReadabilityEdit() => Run(TestMode.EditMode, "ImpactReadabilityEdit",
            "Elemental.Tests.EditMode.EarthProceduralAnimationAndImpactTests",
            "Elemental.Tests.EditMode.EarthCharacterImpactSolverTests");
        [MenuItem("Elemental/QA/Impact Sonar Repair Play")]
        public static void RunImpactSonarPlay() => Run(TestMode.PlayMode, "ImpactSonarRepairPlay",
            "Elemental.Tests.PlayMode.EarthDuelHealthPlayTests",
            "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.SurfWaveAndBotProjectileUseTheSharedVisibleKnockoutPipeline",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RunningKeepsSeismicReadabilityWithoutDensePulseSpam",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.SeismicVisionRevealsNightGeometryAndImmediatelyLosesAirborneSupport",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.SeismicEnemyWaitsForWaveAndRendersThroughOpaqueWall");
        [MenuItem("Elemental/QA/Hud Sonar Readability Play")]
        public static void RunHudSonarReadabilityPlay() => Run(TestMode.PlayMode, "HudSonarReadabilityPlay",
            "Elemental.Tests.PlayMode.EarthDuelHudPlayTests.SavedHudRendersAndTracksHealthManaScoreAndRoundRestart",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RunningKeepsSeismicReadabilityWithoutDensePulseSpam");
        [MenuItem("Elemental/QA/Readability Repair Edit")]
        public static void RunReadabilityRepairEdit() => Run(TestMode.EditMode, "ReadabilityRepairEdit",
            "Elemental.Tests.EditMode.EarthVolumetricFractureTests");

        [MenuItem("Elemental/QA/Readability Repair Play")]
        public static void RunReadabilityRepairPlay() => Run(TestMode.PlayMode, "ReadabilityRepairPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionBriefTurnTapArticulatesLegs",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionTurnsMoveLegsAndOrdinaryGaitMovesAccessories",
            "Elemental.Tests.PlayMode.EarthDuelHudPlayTests.SavedHudRendersAndTracksHealthManaScoreAndRoundRestart",
            "Elemental.Tests.PlayMode.EarthBakedFractureRuntimeTests.NarrowAndWideWallsKeepTheSameBeveledSilhouetteWhenCracked",
            "Elemental.Tests.PlayMode.EarthBakedFractureRuntimeTests.CrackedWallKeepsFullCellsAndNeedsRepeatedHitsToDetach",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.SeismicVisionRevealsNightGeometryAndImmediatelyLosesAirborneSupport",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.SeismicEnemyWaitsForWaveAndRendersThroughOpaqueWall");

        [MenuItem("Elemental/QA/Readability Diagnostic Play")]
        public static void RunReadabilityDiagnostic() => Run(TestMode.PlayMode, "ReadabilityDiagnosticPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionTurnsMoveLegsAndOrdinaryGaitMovesAccessories",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RunningKeepsSeismicReadabilityWithoutDensePulseSpam");
        [MenuItem("Elemental/QA/Mobility Wall Followup Edit")]
        public static void RunMobilityWallEdit() => Run(TestMode.EditMode, "MobilityWallFollowupEdit",
            "Elemental.Tests.EditMode.EarthWallSurfaceFitTests",
            "Elemental.Tests.EditMode.EarthStoneBevelProfileTests",
            "Elemental.Tests.EditMode.EarthBondDamageSolverTests",
            "Elemental.Tests.EditMode.ArmorJumpAimAnimationPolicyTests",
            "Elemental.Tests.EditMode.EarthMagicExpansionTests.CushionBrakesOverAvailableTravelAndItsTopMatchesTheFeet",
            "Elemental.Tests.EditMode.EarthSurfSessionTests.BoardWaitsForBlockedRiderAndReleasesAfterRealSeparation");

        [MenuItem("Elemental/QA/Mobility Wall Followup Play")]
        public static void RunMobilityWallPlay() => Run(TestMode.PlayMode, "MobilityWallFollowupPlay",
            "Elemental.Tests.PlayMode.LinePillarPlacementRuntimeTests",
            "Elemental.Tests.PlayMode.SurfPillarOwnerRegressionTests",
            "Elemental.Tests.PlayMode.EarthBakedFractureRuntimeTests.CrackedWallKeepsFullCellsAndNeedsRepeatedHitsToDetach",
            "Elemental.Tests.PlayMode.EarthMagicExpansionRuntimeTests.FallingSpaceCushionCapsDescentWithoutInjectingImpactDamage",
            "Elemental.Tests.PlayMode.EarthSurfRuntimeTests.ProductionPloughCarriesTheVisibleHeroInsteadOfLeavingHimBehind",
            "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.HighFallOntoLandingCushionDoesNotRagdollOrKillPlayer");

        [MenuItem("Elemental/QA/Gameplay Tuning Edit")]
        public static void RunEdit() => Run(TestMode.EditMode, "GameplayTuningEdit",
            "Elemental.Tests.EditMode.EarthBondDamageSolverTests",
            "Elemental.Tests.EditMode.EarthWallSurfaceFitTests",
            "Elemental.Tests.EditMode.EarthCharacterImpactSolverTests",
            "Elemental.Tests.EditMode.EarthDuelMatchStateTests",
            "Elemental.Tests.EditMode.EarthSpinKickClipTests",
            "Elemental.Tests.EditMode.EarthSeismicPerceptionTests",
            "Elemental.Tests.EditMode.SeismicVisionTemporalPixelTests");

        [MenuItem("Elemental/QA/Gameplay Tuning Play")]
        public static void RunPlay() => Run(TestMode.PlayMode, "GameplayTuningPlay",
            "Elemental.Tests.PlayMode.EarthBakedFractureRuntimeTests.CrackedWallKeepsFullCellsAndNeedsRepeatedHitsToDetach",
            "Elemental.Tests.PlayMode.EarthGravityGripSessionTests.MiddleMouseKeepsWholeWallAnchoredAndStillAllowsStructureGestures",
            "Elemental.Tests.PlayMode.EarthDuelHealthPlayTests",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionPairedStoneSeries60Fps",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.SeismicEnemyWaitsForWaveAndRendersThroughOpaqueWall");

        [MenuItem("Elemental/QA/Gameplay Tuning Animation Play")]
        public static void RunAnimationPlay() => Run(TestMode.PlayMode, "GameplayTuningAnimationPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionPairedStoneSeries60Fps");

        [MenuItem("Elemental/QA/Alpha Physics Rhythm Edit")]
        public static void RunAlphaEdit() => Run(TestMode.EditMode, "AlphaPhysicsRhythmEdit",
            "Elemental.Tests.EditMode.EarthBondDamageSolverTests",
            "Elemental.Tests.EditMode.EarthStoneBevelProfileTests",
            "Elemental.Tests.EditMode.LocomotionRhythmTests",
            "Elemental.Tests.EditMode.LocomotionCatalogAssetTests");
        [MenuItem("Elemental/QA/Alpha Walls Rhythm Play")]
        public static void RunAlphaPlay() => Run(TestMode.PlayMode, "AlphaWallsRhythmPlay",
            "Elemental.Tests.PlayMode.EarthWallStoneContactTests",
            "Elemental.Tests.PlayMode.EarthBakedFractureRuntimeTests.NarrowAndWideWallsKeepTheSameBeveledSilhouetteWhenCracked",
            "Elemental.Tests.PlayMode.EarthBakedFractureRuntimeTests.CrackedWallKeepsFullCellsAndNeedsRepeatedHitsToDetach",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionLocomotionUsesActualSpeedPhaseAndKeepsFinalFootAnchors",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionPresentationClockSlowsActualEammWithoutChangingSimulationTime");
        [MenuItem("Elemental/QA/Alpha Local Impacts Edit")]
        public static void RunAlphaImpactsEdit() => Run(TestMode.EditMode, "AlphaLocalImpactsEdit",
            "Elemental.Tests.EditMode.EarthLocalizedPhysicsResponseTests",
            "Elemental.Tests.EditMode.EarthCharacterImpactSolverTests",
            "Elemental.Tests.EditMode.EarthProceduralAnimationAndImpactTests");
        [MenuItem("Elemental/QA/Alpha Local Impacts Play")]
        public static void RunAlphaImpactsPlay() => Run(TestMode.PlayMode, "AlphaLocalImpactsPlay",
            "Elemental.Tests.PlayMode.EarthLocalizedPhysicsRuntimeTests",
            "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.ProductionStoneStaggerBendsAndRecoversWithCaptures");
        [MenuItem("Elemental/QA/Alpha Frontend Play")]
        public static void RunAlphaFrontendPlay() => Run(TestMode.PlayMode, "AlphaFrontendPlay",
            "Elemental.Tests.PlayMode.AlphaFrontendPlayTests");
        [MenuItem("Elemental/QA/HUD Layout Play")]
        public static void RunHudLayoutPlay() => Run(TestMode.PlayMode, "HudLayoutPlay",
            "Elemental.Tests.PlayMode.HudLayoutPlayTests");
        [MenuItem("Elemental/QA/Feel Followup UI Edit")]
        public static void RunFeelUiEdit() => Run(TestMode.EditMode, "FeelFollowupUiEdit", "Elemental.Tests.EditMode.EarthLifeResultTests");
        [MenuItem("Elemental/QA/Feel Followup UI Play")]
        public static void RunFeelUiPlay() => Run(TestMode.PlayMode, "FeelFollowupUiPlay", "Elemental.Tests.PlayMode.FeelFollowupUiPlayTests");
        [MenuItem("Elemental/QA/HUD Layout Edit")]
        public static void RunHudLayoutEdit() => Run(TestMode.EditMode, "HudLayoutEdit",
            "Elemental.Tests.EditMode.HudLayoutDataTests");
        [MenuItem("Elemental/QA/Alpha Locomotion World Play")]
        public static void RunAlphaLocomotionWorld() => Run(TestMode.PlayMode, "AlphaLocomotionWorldPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionIdleTurnsAndStopsKeepFinalPoseAndAuthoredTurnOwnership",
            "Elemental.Tests.PlayMode.EarthIdleFootOrientationTests.ForwardAndBackwardStopsDoNotDragOrStretchAnkles",
            "Elemental.Tests.PlayMode.EarthAnimationContactPredictorTests.PredictorUsesMovingSupportPointVelocity",
            "Elemental.Tests.PlayMode.ArmorJumpAimAnimationRuntimeTests.PhysicalArmorHoldReturnsToWeightedOrdinaryPoseAndShortSpaceUsesJumpLane",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.FinalHumanoidFeetTraverseRealPitHumpAndSlopeAtControlledThirtySixtyOneTwentySteps");
        [MenuItem("Elemental/QA/Alpha Latest Edit")]
        public static void RunAlphaLatestEdit() => Run(TestMode.EditMode, "AlphaLatestEdit",
            "Elemental.Tests.EditMode.EarthTurnStepSequenceTests",
            "Elemental.Tests.EditMode.EarthCharacterBodyTargetTests",
            "Elemental.Tests.EditMode.EarthStoneBevelProfileTests",
            "Elemental.Tests.EditMode.EarthRepairCrushTests",
            "Elemental.Tests.EditMode.EarthInactiveMatterShellTests",
            "Elemental.Tests.EditMode.EarthCharacterFeelTests.SwingCollisionFloorPreservesTangentTravelAndLeavesRaisedStepUnchanged",
            "Elemental.Tests.EditMode.EarthCharacterFeelTests.UnreachablePlantReleasesWithoutSlidingAnchorAndCannotRecaptureNextTick",
            "Elemental.Tests.EditMode.EarthCharacterFeelTests.TrailingPitAnchorOutsideRestLegLengthCannotBeKeptByExtraPelvisDrop",
            "Elemental.Tests.EditMode.LocomotionRhythmTests.ReloadedInitializationFlagCannotAuthorizeMissingNativeBuffers",
            "Elemental.Tests.EditMode.EarthCharacterFeelTests.TrailingStanceRequestsGeometricReachWithoutMovingAnchorOrExceedingPelvisBudget");
        [MenuItem("Elemental/QA/Alpha Repair Crush Play")]
        public static void RunAlphaRepairCrush() => Run(TestMode.PlayMode, "AlphaRepairCrushPlay",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.ActualLargeArenaCellThrownIntoBotPublishesPhysicalHit",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.ActualLargeWallCellThrownIntoBotPublishesPhysicalHit",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.RepeatedActualStonesDamageUnparentedRagdollAndKillBot",
            "Elemental.Tests.PlayMode.EarthLocalizedPhysicsRuntimeTests.DestroyedBoneLifetimeStopsPoseWritesAndExplicitConfigureRebuilds",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.RepeatedCancelledQuickStonesRetireAndReuseOnlyTheirOwnMatter",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.HeavyFallingStoneCrushesPlayerIntoDynamicRagdoll",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.HeavyFallingStoneCrushesBotIntoDynamicRagdoll",
            "Elemental.Tests.PlayMode.OuterStoneRingRuntimeTests.RepairFliesOneCellAtATimeAndReleasePreservesCurrentPhysicalPose",
            "Elemental.Tests.PlayMode.EarthReassemblyRuntimeTests.BakedWallPhysicallyReassemblesAndRestoresIntactProxy");
        [MenuItem("Elemental/QA/Alpha Turn Steps Play")]
        public static void RunAlphaTurnSteps() => Run(TestMode.PlayMode, "AlphaTurnStepsPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionTurnStepsCompleteShortTapsAndSustainedHalfTurns");
        [MenuItem("Elemental/QA/Alpha Repair Visual Play")]
        public static void RunAlphaRepairVisual() => Run(TestMode.PlayMode, "AlphaRepairVisualPlay",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.RepeatedActualStonesDamageUnparentedRagdollAndKillBot",
            "Elemental.Tests.PlayMode.EarthReassemblyRuntimeTests.BakedWallPhysicallyReassemblesAndRestoresIntactProxy",
            "Elemental.Tests.PlayMode.EarthBakedFractureRuntimeTests.NarrowAndWideWallsKeepTheSameBeveledSilhouetteWhenCracked",
            "Elemental.Tests.PlayMode.EarthBakedFractureRuntimeTests.CrackedWallKeepsFullCellsAndNeedsRepeatedHitsToDetach");
        [MenuItem("Elemental/QA/Alpha Slope Trace Play")]
        public static void RunAlphaSlopeTrace() => Run(TestMode.PlayMode, "AlphaSlopeTracePlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.FinalHumanoidFeetTraverseRealPitHumpAndSlopeAtControlledThirtySixtyOneTwentySteps");
        [MenuItem("Elemental/QA/Alpha Wall Repair Play")]
        public static void RunAlphaWallRepair() => Run(TestMode.PlayMode, "AlphaWallRepairPlay",
            "Elemental.Tests.PlayMode.EarthReassemblyRuntimeTests.BakedWallPhysicallyReassemblesAndRestoresIntactProxy");
        [MenuItem("Elemental/QA/Alpha Cadence Play")]
        public static void RunAlphaCadence() => Run(TestMode.PlayMode, "AlphaCadencePlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionLocomotionUsesActualSpeedPhaseAndKeepsFinalFootAnchors",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionPresentationClockSlowsActualEammWithoutChangingSimulationTime");
        [MenuItem("Elemental/QA/Sustained Crush Edit")]
        public static void RunSustainedCrushEdit() => Run(TestMode.EditMode, "SustainedCrushEdit",
            "Elemental.Tests.EditMode.EarthSustainedCrushTests",
            "Elemental.Tests.EditMode.EarthRepairCrushTests",
            "Elemental.Tests.EditMode.EarthProceduralAnimationAndImpactTests");
        [MenuItem("Elemental/QA/Sustained Crush Play")]
        public static void RunSustainedCrushPlay() => Run(TestMode.PlayMode, "SustainedCrushPlay",
            "Elemental.Tests.PlayMode.EarthSustainedCrushRuntimeTests",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.RestingSlabDamagesProductionBotWithoutRepeatedImpactAndPreventsRecovery",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.HeavyFallingStoneCrushesPlayerIntoDynamicRagdoll",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.HeavyFallingStoneCrushesBotIntoDynamicRagdoll");
        [MenuItem("Elemental/QA/Grounding Followup Edit")]
        public static void RunGroundingFollowupEdit() => Run(TestMode.EditMode, "GroundingFollowupEdit",
            "Elemental.Tests.EditMode.SeptemberPivotContactTests",
            "Elemental.Tests.EditMode.SettledMatterSupportTests",
            "Elemental.Tests.EditMode.CharacterSupportAuthorityTests",
            "Elemental.Tests.EditMode.EarthFootSupportAuthorityIntegrationTests",
            "Elemental.Tests.EditMode.EarthTurnStepSequenceTests");
        [MenuItem("Elemental/QA/Grounding Followup Play")]
        public static void RunGroundingFollowupPlay() => Run(TestMode.PlayMode, "GroundingFollowupPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionMirroredTurnRawPoseAndNamedContactCurvesProbe",
            "Elemental.Tests.PlayMode.PlanetMotorPlayModeTests.MotorUsesActualAnchoredRockTopInsteadOfFloorBelow",
            "Elemental.Tests.PlayMode.PlanetMotorPlayModeTests.ReleasedEarthRockSupportsAfterSleepAndKeepsIdentityAcrossContactWake",
            "Elemental.Tests.PlayMode.PlanetMotorPlayModeTests.Motor_RejectsCloserDynamicDebrisAsSupport",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionTurnStepsCompleteShortTapsAndSustainedHalfTurns");
        [MenuItem("Elemental/QA/Raw Mirrored Turn Probe Play")]
        public static void RunRawMirroredTurnProbePlay() => Run(TestMode.PlayMode, "RawMirroredTurnProbePlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionMirroredTurnRawPoseAndNamedContactCurvesProbe");
        [MenuItem("Elemental/QA/Stone Lethality Edit")]
        public static void RunStoneLethalityEdit() => Run(TestMode.EditMode, "StoneLethalityEdit",
            "Elemental.Tests.EditMode.EarthStoneLethalityTests",
            "Elemental.Tests.EditMode.EarthSustainedCrushTests");
        [MenuItem("Elemental/QA/Stone Lethality Play")]
        public static void RunStoneLethalityPlay() => Run(TestMode.PlayMode, "StoneLethalityPlay",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.WeakStoneHealthDamageKeepsNoFlinchAndDedupeGuards",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.HeldTypedStonePhysicallyContactsWithoutHealthDamage",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.ReleasedArmorDamagesDetachedProductionRagdoll",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.ActualLooseRockPileKillsProductionBotAndAwardsExactlyOnePoint",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.RemovingActualLooseRockPileStopsPinnedDamage");
        [MenuItem("Elemental/QA/Stone Lethality Release Play")]
        public static void RunStoneLethalityReleasePlay() => Run(TestMode.PlayMode, "StoneLethalityReleasePlay",
            "Elemental.Tests.PlayMode.LocalPhysicsProductionAcceptanceTests.RemovingActualLooseRockPileStopsPinnedDamage");

        private static void Run(TestMode mode, string report, params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mode, report, fixtures });
    }
}
