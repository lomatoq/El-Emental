using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public static class EarthWallSurfaceSetup
    {
        private const string Folder = "Assets/Elemental/Content/GraphicsV5/Materials/";
        [MenuItem("Elemental/VFX/Match Wall Fractures to Exterior")]
        public static void Apply()
        {
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(Folder + "RumbleSandstone.mat");
            Material arena = AssetDatabase.LoadAssetAtPath<Material>(Folder + "RumbleArenaSandstone.mat");
            Material interior = AssetDatabase.LoadAssetAtPath<Material>(Folder + "RumbleSandstoneFractureInterior.mat");
            if (stone == null || arena == null || interior == null)
                throw new System.InvalidOperationException("Saved sandstone materials are missing.");
            Undo.RecordObjects(new Object[] { stone, arena, interior }, "Match fracture surface");
            interior.CopyPropertiesFromMaterial(arena);
            foreach (Material material in new[] { stone, arena, interior })
            {
                material.SetFloat("_MatchFractureSurface", 1f);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }
            Selection.activeObject = stone;
        }
    }
}
