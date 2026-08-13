using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public static class ElementalBugBundle
    {
        [MenuItem("Elemental/Diagnostics/Create Bug Bundle")]
        public static void CreateFromMenu()
        {
            string path = Create();
            EditorUtility.RevealInFinder(path);
            Debug.Log("[Elemental] Bug bundle created: " + path);
        }

        public static string Create()
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Could not resolve project root.");
            string diagnostics = Path.Combine(root, "Diagnostics");
            Directory.CreateDirectory(diagnostics);
            string path = Path.Combine(diagnostics, $"BugBundle-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
            using FileStream stream = File.Create(path);
            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create);
            AddText(archive, "environment.txt", BuildEnvironmentReport());
            AddIfPresent(archive, root, "ProjectSettings/ProjectVersion.txt");
            AddIfPresent(archive, root, "Packages/manifest.json");
            AddIfPresent(archive, root, "Packages/packages-lock.json");
            AddIfPresent(archive, root, "Docs/blueprint-compliance.md");
            AddDirectory(archive, root, "BuildReports", "*.json");
            AddDirectory(archive, root, "TestResults", "*.xml");
            if (!string.IsNullOrWhiteSpace(Application.consoleLogPath) && File.Exists(Application.consoleLogPath))
                AddFile(archive, Application.consoleLogPath, "logs/Editor.log", 8 * 1024 * 1024);
            return path;
        }

        private static string BuildEnvironmentReport()
        {
            var builder = new StringBuilder(512);
            builder.AppendLine("utc=" + DateTime.UtcNow.ToString("O"));
            builder.AppendLine("unity=" + Application.unityVersion);
            builder.AppendLine("platform=" + Application.platform);
            builder.AppendLine("os=" + SystemInfo.operatingSystem);
            builder.AppendLine("cpu=" + SystemInfo.processorType);
            builder.AppendLine("cpuThreads=" + SystemInfo.processorCount);
            builder.AppendLine("memoryMB=" + SystemInfo.systemMemorySize);
            builder.AppendLine("gpu=" + SystemInfo.graphicsDeviceName);
            builder.AppendLine("gpuMB=" + SystemInfo.graphicsMemorySize);
            builder.AppendLine("graphicsApi=" + SystemInfo.graphicsDeviceType);
            builder.AppendLine("batchMode=" + Application.isBatchMode);
            return builder.ToString();
        }

        private static void AddDirectory(ZipArchive archive, string root, string relativeFolder, string pattern)
        {
            string folder = Path.Combine(root, relativeFolder);
            if (!Directory.Exists(folder)) return;
            string[] files = Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            for (int index = 0; index < files.Length; index++)
                AddFile(archive, files[index], relativeFolder.Replace('\\', '/') + "/" + Path.GetFileName(files[index]));
        }

        private static void AddIfPresent(ZipArchive archive, string root, string relativePath)
        {
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) AddFile(archive, path, relativePath);
        }

        private static void AddFile(ZipArchive archive, string source, string entryName, int maximumBytes = 2 * 1024 * 1024)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
            using Stream output = entry.Open();
            using FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long start = Math.Max(0L, input.Length - maximumBytes);
            input.Position = start;
            input.CopyTo(output);
        }

        private static void AddText(ZipArchive archive, string entryName, string text)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(text);
        }
    }
}
