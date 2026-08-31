using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Tests.EditMode
{
    internal static class Mvp01FocusedTestLauncher
    {
        private const string ReportDirectory = "BuildReports";
        private const string PlayPendingKey = "Elemental.Mvp01Qa.PlayPending";
        private const string PlayXmlPathKey = "Elemental.Mvp01Qa.PlayXmlPath";
        private const string PlayJsonPathKey = "Elemental.Mvp01Qa.PlayJsonPath";
        private const string PlayOriginalScenePathKey = "Elemental.Mvp01Qa.PlayOriginalScenePath";
        private const string PlayRestoreScenePathKey = "Elemental.Mvp01Qa.PlayRestoreScenePath";
        private const string PlayPersistentOriginalScenePathKey =
            "Elemental.Mvp01Qa.PersistentOriginalScenePath";
        private const string ShippingScenePath =
            "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private static string _pendingSceneRestorePath;
        private static double _restoreNotBefore;

        [InitializeOnLoadMethod]
        private static void RestorePlayModeCallbacksAfterDomainReload()
        {
            string restorePath = SessionState.GetString(PlayRestoreScenePathKey, string.Empty);
            bool pending = SessionState.GetBool(PlayPendingKey, false);
            if (string.IsNullOrEmpty(restorePath) && !pending)
                restorePath = EditorPrefs.GetString(
                    PlayPersistentOriginalScenePathKey, string.Empty);
            if (!string.IsNullOrEmpty(restorePath))
            {
                _pendingSceneRestorePath = restorePath;
                _restoreNotBefore = EditorApplication.timeSinceStartup + 0.75d;
                EditorApplication.playModeStateChanged -= TryRestoreSceneOnPlayModeStateChanged;
                EditorApplication.playModeStateChanged += TryRestoreSceneOnPlayModeStateChanged;
                EditorApplication.update -= TryRestoreSceneAfterPlayRun;
                EditorApplication.update += TryRestoreSceneAfterPlayRun;
            }

            if (!pending)
            {
                return;
            }

            RegisterCallbacks(
                TestMode.PlayMode.ToString(),
                SessionState.GetString(PlayXmlPathKey, string.Empty),
                SessionState.GetString(PlayJsonPathKey, string.Empty));
        }

        [MenuItem("Elemental/QA/Run MVP 0.1 Focused EditMode Tests")]
        private static void RunEditMode()
        {
            Run(
                TestMode.EditMode,
                "Mvp01FocusedEdit",
                "Elemental.Tests.EditMode.EarthDuelRespawnSolverTests",
                "Elemental.Tests.EditMode.EarthMvpBotPlannerTests",
                "Elemental.Tests.EditMode.EarthOrganicIdleSolverTests",
                "Elemental.Tests.EditMode.EarthPlatformPreparationBudgetTests",
                "Elemental.Tests.EditMode.EarthParticleMaterialValidatorTests",
                "Elemental.Tests.EditMode.EarthVisualClaritySolverTests",
                "Elemental.Tests.EditMode.EarthCinematicDepthOfFieldSolverTests",
                "Elemental.Tests.EditMode.EarthSurfIntegritySolverTests",
                "Elemental.Tests.EditMode.EarthShapeGrammarTests",
                "Elemental.Tests.EditMode.EarthSurfacePlacementSolverTests",
                "Elemental.Tests.EditMode.CharacterOutcomeResolverTests",
                "Elemental.Tests.EditMode.EarthArmorDamageResolverTests",
                "Elemental.Tests.EditMode.EarthProjectileSurfaceContactSolverTests",
                "Elemental.Tests.EditMode.EarthActionRouterTests",
                "Elemental.Tests.EditMode.DualMouseEarthGestureSolverTests",
                "Elemental.Tests.EditMode.EarthLocalizedImpactAndDecorTests",
                "Elemental.Tests.EditMode.BrokenCrownArenaImporterTests",
                "Elemental.Tests.EditMode.EarthArenaFractureShadingTests",
                "Elemental.Tests.EditMode.EarthCharacterImpactSolverTests",
                "Elemental.Tests.EditMode.EarthMixamoPresentationTests",
                "Elemental.Tests.EditMode.EarthCharacterFeelTests",
                "Elemental.Tests.EditMode.EarthAnimationRescueTests",
                "Elemental.Tests.EditMode.EarthProceduralAnimationAndImpactTests",
                "Elemental.Tests.EditMode.EarthRuntimeRescueSolverTests",
                "Elemental.Tests.EditMode.CharacterSupportImpactSolverTests",
                "Elemental.Tests.EditMode.SecondaryBoneSpringSolverTests",
                "Elemental.Tests.EditMode.EarthFractureAssetTests",
                "Elemental.Tests.EditMode.EarthVolumetricFractureTests",
                "Elemental.Tests.EditMode.EarthMeshIntegrityValidatorTests",
                "Elemental.Tests.EditMode.EarthBondDamageSolverTests",
                "Elemental.Tests.EditMode.EarthMagicExpansionTests",
                "Elemental.Tests.EditMode.EarthWebWaveAndArmorTests",
                "Elemental.Tests.EditMode.EarthEffectsTuningProfileTests",
                "Elemental.Tests.EditMode.TerrainExtractionTransactionTests");
        }

        [MenuItem("Elemental/QA/Run Earth Effects Tuning EditMode Tests")]
        private static void RunEarthEffectsTuningEditMode()
        {
            Run(
                TestMode.EditMode,
                "EarthEffectsTuningEdit",
                "Elemental.Tests.EditMode.EarthEffectsTuningProfileTests");
        }

        [MenuItem("Elemental/QA/Run Performance Evidence EditMode Tests")]
        private static void RunPerformanceEvidenceEditMode()
        {
            Run(
                TestMode.EditMode,
                "PerformanceEvidenceEdit",
                "Elemental.Tests.EditMode.EarthPerformanceStatisticsTests");
        }

        [MenuItem("Elemental/QA/Run MVP 0.1 Focused PlayMode Tests")]
        private static void RunPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "Mvp01FocusedPlay",
                "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests",
                "Elemental.Tests.PlayMode.EarthProjectileSurfaceContactRuntimeTests",
                "Elemental.Tests.PlayMode.BrokenCrownArenaRuntimeTests",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.PullThenFlickWorksFromScreenInputWithoutProjectingThrowOntoPlanet",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.QuickStoneSurvivesBudgetedTerrainCommitAndLaunchesItsReservedRock",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.WallsCommitFromBothNearAndFarPlanetStrokes",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.PhysicalMouseRouteCommitsNearAndFarWallsThroughShippingRouter",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.PhysicalMouseClosedStrokeCommitsPlatformThroughShippingRouter",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.PhysicalMouseStationaryHoldStartsTerrainExtractionThroughShippingRouter",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.PhysicalMouseHoldMovesVisibleDecorRockThroughShippingRouter",
                "Elemental.Tests.PlayMode.EarthCoreV2FoundationTests.PushRaySkipsCasterAndMovesTheWallInsteadOfLaunchingTheMage",
                "Elemental.Tests.PlayMode.VoxelPlanetRuntimeTests");
        }

        [MenuItem("Elemental/QA/Run Projectile Surface Contact EditMode Tests")]
        private static void RunProjectileSurfaceContactEditMode()
        {
            Run(
                TestMode.EditMode,
                "ProjectileSurfaceContactEdit",
                "Elemental.Tests.EditMode.EarthProjectileSurfaceContactSolverTests");
        }

        [MenuItem("Elemental/QA/Run Projectile Surface Contact PlayMode Tests")]
        private static void RunProjectileSurfaceContactPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "ProjectileSurfaceContactPlay",
                "Elemental.Tests.PlayMode.EarthProjectileSurfaceContactRuntimeTests");
        }

        [MenuItem("Elemental/QA/Run Arena Fracture Shading EditMode Tests")]
        private static void RunArenaFractureShadingEditMode()
        {
            Run(
                TestMode.EditMode,
                "ArenaFractureShadingEdit",
                "Elemental.Tests.EditMode.EarthArenaFractureShadingTests");
        }

        [MenuItem("Elemental/QA/Run MVP 0.1 Physical Input PlayMode Test")]
        private static void RunPhysicalInputPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "Mvp01PhysicalInputPlay",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.PhysicalMouseRouteCommitsNearAndFarWallsThroughShippingRouter",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.PhysicalMouseClosedStrokeCommitsPlatformThroughShippingRouter",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.PhysicalMouseStationaryHoldStartsTerrainExtractionThroughShippingRouter",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.PhysicalMouseHoldMovesVisibleDecorRockThroughShippingRouter");
        }

        [MenuItem("Elemental/QA/Run Finite Surf PlayMode Tests")]
        private static void RunFiniteSurfPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "SurfFinitePlay",
                "Elemental.Tests.PlayMode.EarthSurfRuntimeTests");
        }

        [MenuItem("Elemental/QA/Run Lookdev PlayMode Test")]
        private static void RunLookdevPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "LookdevPlay",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.EarthCoreLoadsAsReadableDioramaWithHudAndFeedback");
        }

        [MenuItem("Elemental/QA/Run Linebreaker PlayMode Test")]
        private static void RunLinebreakerPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "LinebreakerPlay",
                "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.ShippingSceneContainsLargeRumbleDuelCourtAndOneActiveLinebreaker");
        }

        [MenuItem("Elemental/QA/Run Impact Pipeline PlayMode Test")]
        private static void RunImpactPipelinePlayMode()
        {
            Run(
                TestMode.PlayMode,
                "ImpactPipelinePlay",
                "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.SurfWaveAndBotProjectileUseTheSharedVisibleKnockoutPipeline");
        }

        [MenuItem("Elemental/QA/Run Accepted MVP Evidence PlayMode Test")]
        private static void RunAcceptedMvpEvidencePlayMode()
        {
            Run(
                TestMode.PlayMode,
                "AcceptedMvpEvidencePlay",
                "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.ZzzAcceptedMvpEvidenceCompletesWithProfilerAndCaptures");
        }

        [MenuItem("Elemental/QA/Run Landing Cushion PlayMode Test")]
        private static void RunLandingCushionPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "LandingCushionPlay",
                "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.HighFallOntoLandingCushionDoesNotRagdollOrKillPlayer");
        }

        [MenuItem("Elemental/QA/Run Stomp Stone PlayMode Test")]
        private static void RunStompStonePlayMode()
        {
            Run(
                TestMode.PlayMode,
                "StompStonePlay",
                "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests.StompStoneHoversThenPunchesAlongTheCrosshairWithoutPoolGrowth");
        }

        [MenuItem("Elemental/QA/Run Pull And Quick Stone PlayMode Tests")]
        private static void RunPullAndQuickStonePlayMode()
        {
            Run(
                TestMode.PlayMode,
                "PullAndQuickStonePlay",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.PullThenFlickWorksFromScreenInputWithoutProjectingThrowOntoPlanet",
                "Elemental.Tests.PlayMode.EarthCoreVisualRuntimeTests.QuickStoneSurvivesBudgetedTerrainCommitAndLaunchesItsReservedRock");
        }

        [MenuItem("Elemental/QA/Run Character Feel Golden Path PlayMode Tests")]
        private static void RunCharacterFeelGoldenPathPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "CharacterFeelGoldenPathPlay",
                "Elemental.Tests.PlayMode.EarthPlayerGoldenPathRuntimeTests",
                "Elemental.Tests.PlayMode.EarthCoreV2FoundationTests",
                "Elemental.Tests.PlayMode.PlanetMotorPlayModeTests");
        }

        [MenuItem("Elemental/QA/Run Production Character Locomotion PlayMode Test")]
        private static void RunProductionCharacterLocomotionPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "ProductionCharacterLocomotionPlay",
                "Elemental.Tests.PlayMode.EarthPlayerGoldenPathRuntimeTests.ProductionMageStandsWalksAndAnimatesWithoutCameraChaos");
        }

        [MenuItem("Elemental/QA/Run Character Animation Visual Audit")]
        private static void RunCharacterAnimationVisualAudit()
        {
            Run(
                TestMode.PlayMode,
                "CharacterAnimationVisualAuditPlay",
                "Elemental.Tests.PlayMode.EarthAnimationVisualAuditRuntimeTests.ProductionAnimationSequenceKeepsArenaAndBothActorsReadable");
        }

        [MenuItem("Elemental/QA/Run Animation Contact Acceptance EditMode Tests")]
        private static void RunAnimationContactAcceptanceEditMode()
        {
            Run(
                TestMode.EditMode,
                "AnimationContactAcceptanceEdit",
                "Elemental.Tests.EditMode.EarthAnimationContactAcceptanceTests");
        }

        [MenuItem("Elemental/QA/Run Animation Transitions VNext EditMode Tests")]
        private static void RunAnimationTransitionsVNextEditMode()
        {
            Run(
                TestMode.EditMode,
                "AnimationTransitionsVNextEdit",
                "Elemental.Tests.EditMode.EarthAnimationTransitionPolicyTests",
                "Elemental.Tests.EditMode.EarthAnimationClipMetadataTests",
                "Elemental.Tests.EditMode.EarthFootSupportAuthorityIntegrationTests",
                "Elemental.Tests.EditMode.EarthProceduralAnimationAndImpactTests",
                "Elemental.Tests.EditMode.EarthRagdollRecoveryPoseSolverTests");
        }

        [MenuItem("Elemental/QA/Run Animation Contact 30 60 120 Matrix")]
        private static void RunAnimationContactMatrixPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "AnimationContactMatrixPlay",
                "Elemental.Tests.PlayMode.EarthAnimationContactTelemetryRuntimeTests.ProductionActorsEmitRealThirtySixtyOneTwentyMatrix");
        }

        [MenuItem("Elemental/QA/Run KayKit Foot Support PlayMode Test")]
        private static void RunKayKitFootSupportPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "KayKitFootSupportPlay",
                "Elemental.Tests.PlayMode.EarthCoreV2FoundationTests.KayKitHumanoidConsumesLocomotionVelocityWithoutRootMotion");
        }

        [MenuItem("Elemental/QA/Run Broken Crown PlayMode Test")]
        private static void RunBrokenCrownPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "BrokenCrownPlay",
                "Elemental.Tests.PlayMode.BrokenCrownArenaRuntimeTests.LocalDamagePluckGravityAndProtectedFloorShareOneBoundedContract");
        }

        [MenuItem("Elemental/QA/Run Earth Magic Expansion PlayMode Tests")]
        private static void RunEarthMagicExpansionPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "EarthMagicExpansionPlay",
                "Elemental.Tests.PlayMode.EarthMagicExpansionRuntimeTests");
        }

        [MenuItem("Elemental/QA/Run Raised Pillar Control PlayMode Test")]
        private static void RunRaisedPillarControlPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "RaisedPillarControlPlay",
                "Elemental.Tests.PlayMode.EarthMagicExpansionRuntimeTests.RaisedPillarCanBeControlledThenFlickedAsAProjectile");
        }

        [MenuItem("Elemental/QA/Run Platform Then Pillar Isolation PlayMode Tests")]
        private static void RunPlatformThenPillarIsolationPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "PlatformThenPillarIsolationPlay",
                "Elemental.Tests.PlayMode.EarthMagicExpansionRuntimeTests.PlatformDrawnUnderPlayerCarriesWithoutFractureOrRagdoll",
                "Elemental.Tests.PlayMode.EarthMagicExpansionRuntimeTests.RaisedPillarCanBeControlledThenFlickedAsAProjectile");
        }

        [MenuItem("Elemental/QA/Run Pillar Reuse Isolation PlayMode Tests")]
        private static void RunPillarReuseIsolationPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "PillarReuseIsolationPlay",
                "Elemental.Tests.PlayMode.EarthMagicExpansionRuntimeTests.PillarWaveColumnIgnoresItsCasterUntilItReturnsToPool",
                "Elemental.Tests.PlayMode.EarthMagicExpansionRuntimeTests.RaisedPillarCanBeControlledThenFlickedAsAProjectile");
        }

        [MenuItem("Elemental/QA/Run Production Camera Push PlayMode Test")]
        private static void RunProductionCameraPushPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "ProductionCameraPushPlay",
                "Elemental.Tests.PlayMode.EarthCoreV2FoundationTests.ProductionCameraRayLocksAndQuicklyShovesVisibleWall");
        }

        [MenuItem("Elemental/QA/Run Production Armor Camera PlayMode Tests")]
        private static void RunProductionArmorCameraPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "ProductionArmorCameraPlay",
                "Elemental.Tests.PlayMode.EarthPlayerGoldenPathRuntimeTests.ProductionCameraDoesNotCollapseIntoArmorOrReleasedPlates",
                "Elemental.Tests.PlayMode.EarthPlayerGoldenPathRuntimeTests.ProductionArmorStartsOffAndCoversEveryVisibleBodyRegion");
        }

        private static void Run(TestMode mode, string reportStem, params string[] testNames)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[MVP QA] Stop Play Mode before starting the focused test run.");
                return;
            }

            string projectPath = Directory.GetCurrentDirectory();
            string reportDirectory = Path.Combine(projectPath, ReportDirectory);
            Directory.CreateDirectory(reportDirectory);

            string xmlPath = Path.Combine(reportDirectory, reportStem + ".xml");
            string jsonPath = Path.Combine(reportDirectory, reportStem + ".json");
            File.Delete(xmlPath);
            File.Delete(jsonPath);

            if (mode == TestMode.PlayMode)
            {
                Scene originalScene = SceneManager.GetActiveScene();
                if (originalScene.isDirty) EditorSceneManager.SaveOpenScenes();
                SessionState.SetBool(PlayPendingKey, true);
                SessionState.SetString(PlayXmlPathKey, xmlPath);
                SessionState.SetString(PlayJsonPathKey, jsonPath);
                string originalPath = ResolveRestorableScenePath(originalScene.path);
                SessionState.SetString(PlayOriginalScenePathKey, originalPath);
                EditorPrefs.SetString(
                    PlayPersistentOriginalScenePathKey, originalPath);

                // Every focused PlayMode test loads EarthCoreSlice additively. Leaving
                // the shipping scene open creates two scenes with the same path, so
                // GetSceneByPath can resolve the wrong copy and tests unload each
                // other's player/avatar/arena. Run from a clean editor scene and
                // restore the user's saved scene when the suite finishes.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            TestRunnerApi api = RegisterCallbacks(mode.ToString(), xmlPath, jsonPath);

            ExecutionSettings settings = new ExecutionSettings(new Filter
            {
                testMode = mode,
                testNames = testNames
            });

            if (mode == TestMode.EditMode)
            {
                settings.runSynchronously = true;
            }

            string runId = api.Execute(settings);
            Debug.Log($"[MVP QA] Started {mode} focused test run {runId}.");
        }

        internal static void ClearPendingPlayRun()
        {
            SessionState.EraseBool(PlayPendingKey);
            SessionState.EraseString(PlayXmlPathKey);
            SessionState.EraseString(PlayJsonPathKey);
        }

        internal static string TakeOriginalPlayScenePath()
        {
            string path = SessionState.GetString(PlayOriginalScenePathKey, string.Empty);
            if (string.IsNullOrEmpty(path))
                path = EditorPrefs.GetString(PlayPersistentOriginalScenePathKey, string.Empty);
            return path;
        }

        internal static void RestoreSceneAfterPlayRun(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
                scenePath = EditorPrefs.GetString(
                    PlayPersistentOriginalScenePathKey, string.Empty);
            scenePath = ResolveRestorableScenePath(scenePath);
            _pendingSceneRestorePath = scenePath;
            _restoreNotBefore = EditorApplication.timeSinceStartup + 0.75d;
            SessionState.SetString(PlayRestoreScenePathKey, scenePath);
            EditorApplication.playModeStateChanged -= TryRestoreSceneOnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += TryRestoreSceneOnPlayModeStateChanged;
            EditorApplication.update -= TryRestoreSceneAfterPlayRun;
            EditorApplication.update += TryRestoreSceneAfterPlayRun;
        }

        private static void TryRestoreSceneOnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            _restoreNotBefore = EditorApplication.timeSinceStartup + 0.75d;
            EditorApplication.update -= TryRestoreSceneAfterPlayRun;
            EditorApplication.update += TryRestoreSceneAfterPlayRun;
        }

        private static void TryRestoreSceneAfterPlayRun()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.timeSinceStartup < _restoreNotBefore) return;
            string scenePath = _pendingSceneRestorePath;
            if (string.IsNullOrEmpty(scenePath)) return;
            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            catch (InvalidOperationException)
            {
                // The Test Runner can report isPlaying=false one editor update
                // before scene operations become legal. Keep polling rather than
                // losing the user's shipping scene.
                return;
            }
            catch (ArgumentException)
            {
                // Test Runner creates transient InitTestScene paths that are not
                // project assets. Never poll a missing scene forever: restore the
                // shipping scene on the next update instead.
                _pendingSceneRestorePath = ShippingScenePath;
                _restoreNotBefore = EditorApplication.timeSinceStartup + 0.1d;
                return;
            }
            EditorApplication.update -= TryRestoreSceneAfterPlayRun;
            EditorApplication.playModeStateChanged -= TryRestoreSceneOnPlayModeStateChanged;
            _pendingSceneRestorePath = string.Empty;
            SessionState.EraseString(PlayRestoreScenePathKey);
            SessionState.EraseString(PlayOriginalScenePathKey);
            EditorPrefs.DeleteKey(PlayPersistentOriginalScenePathKey);
        }

        private static string ResolveRestorableScenePath(string requestedPath)
        {
            if (!string.IsNullOrEmpty(requestedPath) &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(requestedPath) != null)
                return requestedPath;
            return ShippingScenePath;
        }

        private static TestRunnerApi RegisterCallbacks(string mode, string xmlPath, string jsonPath)
        {
            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            Mvp01FocusedTestCallbacks callbacks = ScriptableObject.CreateInstance<Mvp01FocusedTestCallbacks>();
            callbacks.Configure(mode, xmlPath, jsonPath);
            api.RegisterCallbacks(callbacks, 100);
            return api;
        }
    }

    [Serializable]
    internal sealed class Mvp01FocusedTestCallbacks : ScriptableObject, ICallbacks
    {
        [SerializeField] private string mode;
        [SerializeField] private string xmlPath;
        [SerializeField] private string jsonPath;

        private readonly List<string> failures = new List<string>();

        public void Configure(string runMode, string resultXmlPath, string summaryJsonPath)
        {
            mode = runMode;
            xmlPath = resultXmlPath;
            jsonPath = summaryJsonPath;
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            Debug.Log($"[MVP QA] {mode} focused tests started.");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, xmlPath);

            Mvp01FocusedTestSummary summary = new Mvp01FocusedTestSummary
            {
                unityVersion = Application.unityVersion,
                utc = DateTime.UtcNow.ToString("O"),
                mode = mode,
                result = result.TestStatus.ToString(),
                total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount,
                passed = result.PassCount,
                failed = result.FailCount,
                skipped = result.SkipCount,
                inconclusive = result.InconclusiveCount,
                durationSeconds = result.Duration,
                failures = failures.ToArray()
            };

            File.WriteAllText(jsonPath, JsonUtility.ToJson(summary, true));
            Debug.Log(
                $"[MVP QA] {mode} focused tests finished: " +
                $"{summary.passed}/{summary.total} passed, {summary.failed} failed. " +
                $"Report: {jsonPath}");

            if (string.Equals(mode, TestMode.PlayMode.ToString(), StringComparison.Ordinal))
            {
                string originalScenePath = Mvp01FocusedTestLauncher.TakeOriginalPlayScenePath();
                Mvp01FocusedTestLauncher.ClearPendingPlayRun();
                Mvp01FocusedTestLauncher.RestoreSceneAfterPlayRun(originalScenePath);
            }

            TestRunnerApi.UnregisterTestCallback(this);
            DestroyImmediate(this);
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.FailCount <= 0 || result.HasChildren)
            {
                return;
            }

            failures.Add($"{result.FullName}: {result.Message}");
        }
    }

    [Serializable]
    internal sealed class Mvp01FocusedTestSummary
    {
        public string unityVersion;
        public string utc;
        public string mode;
        public string result;
        public int total;
        public int passed;
        public int failed;
        public int skipped;
        public int inconclusive;
        public double durationSeconds;
        public string[] failures;
    }
}
