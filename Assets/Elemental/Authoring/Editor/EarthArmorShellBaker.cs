using Elemental.Runtime.Physics;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public static class EarthArmorShellBaker
    {
        private const string DefinitionPath =
            "Assets/Elemental/Content/Profiles/EarthArmorShellDefinition.asset";

        [MenuItem("Elemental/Armor/Rebake Default Humanoid Shell")]
        public static void RebakeDefault()
        {
            EarthArmorShellDefinition definition =
                AssetDatabase.LoadAssetAtPath<EarthArmorShellDefinition>(DefinitionPath);
            if (definition == null)
            {
                Debug.LogError($"Earth armor shell definition is missing at {DefinitionPath}.");
                return;
            }

            definition.BakeDefaultHumanoidShell();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            Debug.Log($"Rebaked {definition.Segments.Length} anatomical earth armor plates.");
        }
    }
}
