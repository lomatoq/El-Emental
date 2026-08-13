using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Authoring.Assets;
using Elemental.Runtime.Capabilities;
using Elemental.Simulation.Magic;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public static class ElementalProjectValidator
    {
        [MenuItem("Elemental/Diagnostics/Validate Project")]
        public static void ValidateFromMenu()
        {
            string report = Validate(out bool valid);
            if (valid) Debug.Log(report);
            else Debug.LogError(report);
        }

        public static void ValidateBatch()
        {
            string report = Validate(out bool valid);
            if (!valid) throw new UnityEditor.Build.BuildFailedException(report);
            Debug.Log(report);
        }

        public static string Validate(out bool valid)
        {
            var issues = new List<string>();
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string version = File.ReadAllText(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"));
            if (!version.Contains("6000.5.7f1", StringComparison.Ordinal))
                issues.Add("ProjectVersion.txt must pin Unity 6000.5.7f1.");

            string[] requiredScenes =
            {
                M0ProjectSetup.BootstrapScenePath,
                M1GravityToySetup.GravityToyScenePath,
                M2VoxelPlanetSetup.VoxelLabScenePath,
                M3EarthCoreSetup.EarthCoreScenePath,
                M4CharacterFeelSetup.CharacterFeelScenePath,
                M5WindLabSetup.WindLabScenePath,
                M6ElementLabSetup.ElementLabScenePath,
                M7VolcanoVillageSetup.ScenePath,
                M8OnlineSpikeSetup.ScenePath,
                M9WebLabSetup.ScenePath
            };
            for (int index = 0; index < requiredScenes.Length; index++)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(requiredScenes[index]) == null)
                    issues.Add("Missing milestone scene: " + requiredScenes[index]);

            var ids = new HashSet<ushort>();
            string[] abilityGuids = AssetDatabase.FindAssets("t:AbilityRecipeAsset");
            for (int index = 0; index < abilityGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(abilityGuids[index]);
                AbilityRecipeAsset asset = AssetDatabase.LoadAssetAtPath<AbilityRecipeAsset>(path);
                try
                {
                    CompiledAbilityRecipe compiled = new AbilityCompiler().Compile(asset.Bake());
                    if (!ids.Add(compiled.Id.Value)) issues.Add($"Duplicate ability ID {compiled.Id.Value}: {path}");
                }
                catch (Exception exception) { issues.Add(path + ": " + exception.Message); }
            }

            string[] profileGuids = AssetDatabase.FindAssets("t:CapabilityProfileAsset");
            if (profileGuids.Length < 3) issues.Add("NativeHigh, NativeLow, and WebLab capability assets are required.");
            for (int index = 0; index < profileGuids.Length; index++)
            {
                CapabilityProfileAsset asset = AssetDatabase.LoadAssetAtPath<CapabilityProfileAsset>(
                    AssetDatabase.GUIDToAssetPath(profileGuids[index]));
                if (asset.Bake().Budgets.ActiveChunks <= 0) issues.Add(asset.name + " has an invalid chunk budget.");
            }

            valid = issues.Count == 0;
            if (valid)
                return $"[Elemental] Validation passed: {requiredScenes.Length} scenes, {abilityGuids.Length} abilities, {profileGuids.Length} profiles.";
            return "[Elemental] Validation failed:\n- " + string.Join("\n- ", issues);
        }
    }
}
