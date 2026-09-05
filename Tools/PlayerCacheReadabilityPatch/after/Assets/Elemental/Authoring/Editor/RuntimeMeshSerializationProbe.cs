using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Builds three real mesh assets into a tiny standalone-target AssetBundle and
    /// reloads them. This exercises Player serialization without rebuilding the game
    /// or changing asset labels/import settings.
    /// </summary>
    public static class RuntimeMeshSerializationProbe
    {
        private static readonly string[] Paths =
        {
            "Assets/Elemental/Content/GraphicsV5/Physics/V5_Physics_Pebble_17_CenteredUnit.asset",
            "Assets/Elemental/Content/Generated/MaterialPass/Rock0Bevel.asset",
            "Assets/Elemental/Content/Generated/MaterialPass/Rock0Collider.asset"
        };

        [MenuItem("Elemental/QA/Probe Runtime Mesh Serialization")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before probing mesh serialization.");

            string project = Directory.GetParent(Application.dataPath)?.FullName ?? ".";
            string folder = Path.Combine(project, "BuildReports", "MeshSerializationProbe",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(folder);
            var report = new ProbeReport
            {
                unityVersion = Application.unityVersion,
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                results = new ProbeResult[Paths.Length]
            };

            var expected = new Dictionary<string, ProbeResult>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < Paths.Length; index++)
            {
                Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(Paths[index]);
                if (source == null) throw new InvalidOperationException("Missing probe mesh: " + Paths[index]);
                var result = new ProbeResult
                {
                    path = Paths[index],
                    sourceName = source.name,
                    editorReadable = source.isReadable,
                    editorHideFlags = (int)source.hideFlags,
                    expectedVertexCount = source.vertexCount,
                    expectedIndexCount = IndexCount(source)
                };
                report.results[index] = result;
                expected[Paths[index]] = result;
            }

            string bundleName = "runtime-mesh-serialization-probe";
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                folder,
                new[] { new AssetBundleBuild { assetBundleName = bundleName, assetNames = Paths } },
                BuildAssetBundleOptions.ForceRebuildAssetBundle |
                BuildAssetBundleOptions.UncompressedAssetBundle |
                BuildAssetBundleOptions.StrictMode,
                EditorUserBuildSettings.activeBuildTarget);
            if (manifest == null)
                throw new InvalidOperationException("Mesh serialization probe AssetBundle build failed.");

            AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(folder, bundleName));
            if (bundle == null)
                throw new InvalidOperationException("Mesh serialization probe AssetBundle could not be loaded.");
            try
            {
                foreach (string bundledPath in bundle.GetAllAssetNames())
                {
                    string requested = null;
                    foreach (string candidate in Paths)
                        if (string.Equals(candidate, bundledPath, StringComparison.OrdinalIgnoreCase))
                        {
                            requested = candidate;
                            break;
                        }
                    if (requested == null) continue;
                    ProbeResult result = expected[requested];
                    Mesh loaded = bundle.LoadAsset<Mesh>(bundledPath);
                    result.included = loaded != null;
                    if (loaded == null) continue;
                    result.playerSerializedReadable = loaded.isReadable;
                    result.loadedHideFlags = (int)loaded.hideFlags;
                    try
                    {
                        result.loadedVertexCount = loaded.vertices.Length;
                        result.loadedIndexCount = loaded.triangles.Length;
                        result.cpuArraysAccessible = true;
                    }
                    catch (Exception exception)
                    {
                        result.error = exception.GetType().Name + ":" + exception.Message;
                    }
                }
            }
            finally
            {
                bundle.Unload(true);
            }

            report.passed = true;
            foreach (ProbeResult result in report.results)
            {
                bool exact = result.included && result.playerSerializedReadable &&
                    result.cpuArraysAccessible && result.loadedVertexCount == result.expectedVertexCount &&
                    result.loadedIndexCount == result.expectedIndexCount;
                result.passed = exact;
                report.passed &= exact;
            }
            string reportPath = Path.Combine(folder, "MeshSerializationProbe.json");
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log($"[MeshSerializationProbe] {(report.passed ? "Passed" : "Failed")}: {reportPath}");
            if (!report.passed)
                throw new InvalidOperationException("Player-target mesh serialization probe failed. See " + reportPath);
        }

        private static int IndexCount(Mesh mesh)
        {
            int count = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                count += checked((int)mesh.GetIndexCount(subMesh));
            return count;
        }

        [Serializable]
        private sealed class ProbeReport
        {
            public bool passed;
            public string unityVersion;
            public string buildTarget;
            public ProbeResult[] results;
        }

        [Serializable]
        private sealed class ProbeResult
        {
            public bool passed;
            public string path;
            public string sourceName;
            public bool editorReadable;
            public int editorHideFlags;
            public int expectedVertexCount;
            public int expectedIndexCount;
            public bool included;
            public bool playerSerializedReadable;
            public int loadedHideFlags;
            public bool cpuArraysAccessible;
            public int loadedVertexCount;
            public int loadedIndexCount;
            public string error;
        }
    }
}
