using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    internal static class Mvp01FocusedTestLauncher
    {
        private const string ReportDirectory = "BuildReports";
        private const string PlayPendingKey = "Elemental.Mvp01Qa.PlayPending";
        private const string PlayXmlPathKey = "Elemental.Mvp01Qa.PlayXmlPath";
        private const string PlayJsonPathKey = "Elemental.Mvp01Qa.PlayJsonPath";

        [InitializeOnLoadMethod]
        private static void RestorePlayModeCallbacksAfterDomainReload()
        {
            if (!SessionState.GetBool(PlayPendingKey, false))
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
                "Elemental.Tests.EditMode.EarthShapeGrammarTests",
                "Elemental.Tests.EditMode.EarthSurfacePlacementSolverTests",
                "Elemental.Tests.EditMode.CharacterOutcomeResolverTests",
                "Elemental.Tests.EditMode.EarthArmorDamageResolverTests",
                "Elemental.Tests.EditMode.EarthActionRouterTests",
                "Elemental.Tests.EditMode.DualMouseEarthGestureSolverTests",
                "Elemental.Tests.EditMode.EarthLocalizedImpactAndDecorTests",
                "Elemental.Tests.EditMode.EarthCharacterImpactSolverTests",
                "Elemental.Tests.EditMode.EarthMagicExpansionTests",
                "Elemental.Tests.EditMode.EarthWebWaveAndArmorTests",
                "Elemental.Tests.EditMode.TerrainExtractionTransactionTests");
        }

        [MenuItem("Elemental/QA/Run MVP 0.1 Focused PlayMode Tests")]
        private static void RunPlayMode()
        {
            Run(
                TestMode.PlayMode,
                "Mvp01FocusedPlay",
                "Elemental.Tests.PlayMode.EarthMvpEncounterRuntimeTests",
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
                SessionState.SetBool(PlayPendingKey, true);
                SessionState.SetString(PlayXmlPathKey, xmlPath);
                SessionState.SetString(PlayJsonPathKey, jsonPath);
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
                Mvp01FocusedTestLauncher.ClearPendingPlayRun();
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
