using System;
using System.IO;
using System.Security.Cryptography;
using Elemental.Authoring;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor.MotionMatching
{
    public static class CmuMotionManifestImporter
    {
        private const string StagingFolder = "Assets/Elemental/Content/Characters/CMUStaging";

        [MenuItem("Elemental Suite/Character/Validate Selected CMU Motion Manifest")]
        public static void ValidateSelected()
        {
            if (Selection.activeObject is not CmuMotionManifest manifest)
                throw new InvalidOperationException("Select a CMUMotionManifest asset first.");
            int valid = 0;
            foreach (CmuMotionManifestEntry entry in manifest.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.sourceUrl) ||
                    string.IsNullOrWhiteSpace(entry.licenseUrl) ||
                    string.IsNullOrWhiteSpace(entry.sha256) ||
                    string.IsNullOrWhiteSpace(entry.localFileName))
                    throw new InvalidDataException($"CMU manifest entry '{entry?.id}' has incomplete provenance.");
                string path = Path.Combine(StagingFolder, entry.localFileName);
                if (!File.Exists(path)) continue;
                using SHA256 sha = SHA256.Create();
                using FileStream stream = File.OpenRead(path);
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
                if (!actual.Equals(entry.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Hash mismatch for {entry.id}: {actual}");
                valid++;
            }
            Debug.Log($"[EAMM] CMU manifest provenance valid; {valid} staged files matched. Downloads are never implicit.");
        }
    }
}
