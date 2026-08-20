using System;
using System.IO;
using Elemental.Authoring.Build;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public static class ElementalBuildPipeline
    {
        [MenuItem("Elemental/Build/Build WebLab WebGL2")]
        public static void BuildWebLab()
        {
            M9WebLabSetup.Configure();
            Build(
                BuildTarget.WebGL,
                new[] { M9WebLabSetup.ScenePath },
                Path.Combine(ProjectRoot(), "Builds", "WebLab"),
                "WebLab");
        }

        [MenuItem("Elemental/Build/Build Windows Native Playable")]
        public static void BuildWindows()
        {
            BuildWindowsDevelopment();
        }

        [MenuItem("Elemental/Build/Build Windows Development")]
        public static void BuildWindowsDevelopment()
        {
            M0ProjectSetup.Configure();
            Build(
                BuildTarget.StandaloneWindows64,
                NativePlayableScenes(),
                Path.Combine(ProjectRoot(), "Builds", "Windows", "ElEmental.exe"),
                "NativeWindows",
                cleanBuildCache: true,
                development: true);
        }

        [MenuItem("Elemental/Build/Build Windows Release")]
        public static void BuildWindowsRelease()
        {
            M0ProjectSetup.Configure();
            Build(
                BuildTarget.StandaloneWindows64,
                NativePlayableScenes(),
                Path.Combine(ProjectRoot(), "Builds", "WindowsRelease", "ElEmental.exe"),
                "NativeWindowsRelease",
                cleanBuildCache: true,
                development: false);
        }

        [MenuItem("Elemental/Build/Build Windows Release Candidate")]
        public static void BuildWindowsReleaseCandidate()
        {
            M0ProjectSetup.Configure();
            Build(
                BuildTarget.StandaloneWindows64,
                NativePlayableScenes(),
                Path.Combine(ProjectRoot(), "Builds", "WindowsReleaseCandidate", "ElEmental.exe"),
                "NativeWindowsReleaseCandidate",
                cleanBuildCache: true,
                development: false);
        }

        [MenuItem("Elemental/Build/Build macOS Native Smoke")]
        public static void BuildMacOS()
        {
            M0ProjectSetup.Configure();
            Build(
                BuildTarget.StandaloneOSX,
                NativePlayableScenes(),
                Path.Combine(ProjectRoot(), "Builds", "macOS", "ElEmental.app"),
                "NativeMacOS",
                cleanBuildCache: true);
        }

        private static void Build(
            BuildTarget target,
            string[] scenes,
            string destination,
            string profile,
            bool cleanBuildCache = false,
            bool development = true)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? ProjectRoot());
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = destination,
                target = target,
                options = (development ? BuildOptions.Development : BuildOptions.None) |
                    (cleanBuildCache ? BuildOptions.CleanBuildCache : BuildOptions.None)
            });
            WriteReport(report, profile, target, destination);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"{profile} build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            Debug.Log($"[Elemental] {profile} build succeeded: {report.summary.totalSize} bytes in {report.summary.totalTime}.");
        }

        private static void WriteReport(BuildReport report, string profile, BuildTarget target, string destination)
        {
            for (int stepIndex = 0; stepIndex < report.steps.Length; stepIndex++)
            {
                BuildStep step = report.steps[stepIndex];
                for (int messageIndex = 0; messageIndex < step.messages.Length; messageIndex++)
                {
                    BuildStepMessage message = step.messages[messageIndex];
                    if (message.type == LogType.Warning)
                        Debug.LogWarning($"[Elemental Build Warning] {step.name}: {message.content}");
                }
            }
            string folder = Path.Combine(ProjectRoot(), "BuildReports");
            Directory.CreateDirectory(folder);
            string json = JsonUtility.ToJson(new BuildEvidence
            {
                profile = profile,
                target = target.ToString(),
                result = report.summary.result.ToString(),
                totalBytes = report.summary.totalSize,
                durationSeconds = report.summary.totalTime.TotalSeconds,
                warnings = report.summary.totalWarnings,
                errors = report.summary.totalErrors,
                output = destination,
                unityVersion = Application.unityVersion,
                utc = DateTime.UtcNow.ToString("O")
            }, true);
            File.WriteAllText(Path.Combine(folder, profile + ".json"), json);
        }

        private static string[] EnabledScenes()
        {
            return Array.ConvertAll(
                Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled),
                scene => scene.path);
        }

        private static string[] NativePlayableScenes()
        {
            // M0 intentionally rewrites the shared scene list. Keep the non-shipping
            // QA lab available to editor PlayMode tests after a player build, then
            // filter it out of the actual player scene array below.
            EarthPolishLabSetup.EnsureEditorTestBuildEntry();
            return NativeBuildSceneOrder.Create(EnabledScenes(), M3EarthCoreSetup.EarthCoreScenePath);
        }

        private static string ProjectRoot() => Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve project root.");

        [Serializable]
        private sealed class BuildEvidence
        {
            public string profile;
            public string target;
            public string result;
            public ulong totalBytes;
            public double durationSeconds;
            public int warnings;
            public int errors;
            public string output;
            public string unityVersion;
            public string utc;
        }
    }
}
