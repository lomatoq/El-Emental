using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Elemental.Presentation.Diagnostics;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public static class HardPolishPerformanceBuild
    {
        [Serializable] private sealed class Dependency { public string path, guid, sha256; }
        [Serializable] private sealed class Manifest { public string revision, scene, utc; public Dependency[] dependencies; }
        public static void PrepareFinalAssets()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Elemental/Content/Scenes/EarthCoreSlice.unity");
            HardPolishHeavyReleaseInstall.Install();
            StartupCacheBaker.BakeCurrentScene();
        }
        public static void Build()
        {
            const string scene = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            string revision = HardPolishPerformanceProbe.Argument("--perf-revision");
            string folder = Path.GetFullPath(HardPolishPerformanceProbe.Argument("--perf-build-output", "BuildReports/HardPolishPlayer"));
            BuildVerified(revision, folder);
        }
        // Same build path for the already-open editor, without changing process arguments.
        public static void BuildVerified(string revision, string folder)
        {
            const string scene = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            if (string.IsNullOrWhiteSpace(revision)) throw new InvalidOperationException("Provide the verified worktree identity.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Build only from an idle editor.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save scene changes before building.");
            folder = Path.GetFullPath(folder);
            if (File.Exists(Path.Combine(folder, "HardPolishPerf.exe")))
                throw new IOException("Use a fresh folder to preserve prior build evidence.");
            Directory.CreateDirectory(folder);
            var paths = AssetDatabase.GetDependencies(scene, true).OrderBy(p => p).ToArray();
            var manifest = new Manifest { revision = revision, scene = scene, utc = DateTime.UtcNow.ToString("O"), dependencies = paths.Select(p => new Dependency {
                path = p, guid = AssetDatabase.AssetPathToGUID(p), sha256 = Hash(p) }).ToArray() };
            File.WriteAllText(Path.Combine(folder, "scene-profile-manifest.json"), JsonUtility.ToJson(manifest, true));
            bool timing = PlayerSettings.enableFrameTimingStats;
            try
            {
                PlayerSettings.enableFrameTimingStats = true;
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { scene }, locationPathName = Path.Combine(folder, "HardPolishPerf.exe"),
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
                if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(result.summary.result.ToString());
            }
            finally { PlayerSettings.enableFrameTimingStats = timing; }
        }
        private static string Hash(string path)
        {
            if (!File.Exists(path)) return "not-a-filesystem-asset";
            using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
    }
}
