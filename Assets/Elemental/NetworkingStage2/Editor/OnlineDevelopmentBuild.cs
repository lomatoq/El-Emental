using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Elemental.Online.Editor
{
    public static class OnlineDevelopmentBuild
    {
        [Serializable]
        private sealed class Summary
        {
            public string utc, result, output;
            public int errors, warnings;
            public double seconds;
            public ulong bytes;
        }

        [MenuItem("Elemental/Build/Build Online Development From Saved Arena")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode and save the installed arena before building online.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string destination = Path.Combine(project, "Builds", "OnlineDevelopment", "ElEmental.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { OnlineArenaAuthoring.ScenePath },
                locationPathName = destination,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            var summary = new Summary
            {
                utc = DateTime.UtcNow.ToString("O"), result = report.summary.result.ToString(), output = destination,
                errors = (int)report.summary.totalErrors, warnings = (int)report.summary.totalWarnings,
                seconds = report.summary.totalTime.TotalSeconds, bytes = report.summary.totalSize
            };
            string reports = Path.Combine(project, "BuildReports"); Directory.CreateDirectory(reports);
            File.WriteAllText(Path.Combine(reports, "OnlineDevelopmentSavedArena.json"), JsonUtility.ToJson(summary, true));
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Online development build failed. Read BuildReports/OnlineDevelopmentSavedArena.json and the build errors.");
            Debug.Log("Online development build saved: " + destination + ". No scene authoring or regeneration ran.");
        }
    }
}
