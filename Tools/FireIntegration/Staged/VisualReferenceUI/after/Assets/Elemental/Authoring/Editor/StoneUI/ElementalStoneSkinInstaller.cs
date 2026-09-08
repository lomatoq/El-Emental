using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Elemental.Presentation.UI;

namespace Elemental.Authoring.Editor
{
    public static class ElementalStoneSkinInstaller
    {
        private const string Root = "Assets/Elemental/Content/UI/Stone";
        private const string Theme = "Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset";
        [Serializable] private sealed class Manifest { public Entry[] assets; }
        [Serializable] private sealed class Entry
        { public string id, category, file; public int width, height; public bool sRGB; public int[] border_lbrt; public float[] pivot; }

        [MenuItem("Elemental/UI/Install Stone Artwork")]
        public static void Install()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ElementalUITheme>(Theme);
            if (theme == null) throw new InvalidOperationException("Existing ElementalUITheme is required; installer does not recreate the frontend.");
            string json = System.IO.File.ReadAllText(Root + "/asset_manifest.json");
            ValidateManifestJson(json);
            var manifest = JsonUtility.FromJson<Manifest>(json);
            // Validate every required texture/importer before the first mutation.
            foreach (var entry in manifest.assets)
                if (entry.category != "SourceOnly" && !(AssetImporter.GetAtPath(AssetPath(entry.file)) is TextureImporter))
                    throw new InvalidOperationException("Missing stone artwork: " + AssetPath(entry.file));
            foreach (var entry in manifest.assets)
            {
                if (entry.category == "SourceOnly") continue;
                var path = AssetPath(entry.file);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing stone artwork: " + path);
                string previous = EditorJsonUtility.ToJson(importer);
                bool data = entry.category == "Masks" && !entry.sRGB, world = entry.category == "Environment";
                importer.textureType = data || world ? TextureImporterType.Default : TextureImporterType.Sprite;
                importer.sRGBTexture = entry.sRGB; importer.alphaIsTransparency = !data && !world;
                importer.mipmapEnabled = world; importer.isReadable = false;
                importer.wrapMode = world || entry.id == "stone_center_tile" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                if (!data && !world)
                {
                    importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 100;
                    var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(entry.pivot[0], entry.pivot[1]); importer.SetTextureSettings(settings);
                    var b = entry.border_lbrt; importer.spriteBorder = new Vector4(b[0], b[1], b[2], b[3]);
                }
                if (previous != EditorJsonUtility.ToJson(importer)) importer.SaveAndReimport();
            }
            string skinPath = Root + "/ElementalStoneSkin.asset";
            var skin = AssetDatabase.LoadAssetAtPath<ElementalStoneSkin>(skinPath);
            if (skin == null) { skin = ScriptableObject.CreateInstance<ElementalStoneSkin>(); AssetDatabase.CreateAsset(skin, skinPath); }
            string oldSkin = EditorJsonUtility.ToJson(skin);
            skin.curtain = Sprite("Panels/curtain_quiet"); skin.normal = Sprite("Buttons/button_normal");
            skin.primary = Sprite("Buttons/button_earth"); skin.hover = Sprite("Buttons/button_hover");
            skin.pressed = Sprite("Buttons/button_pressed"); skin.disabled = Sprite("Buttons/button_disabled");
            skin.codeField = Sprite("Panels/code_field"); skin.health = Sprite("Icons/icon_health");
            skin.mana = Sprite("Icons/icon_mana"); skin.pause = Sprite("Icons/icon_pause");
            skin.window = Sprite("Panels/window_body"); skin.ribbon = Sprite("Panels/ribbon");
            skin.wordmark = Sprite("Brand/wordmark_master"); skin.divider = Sprite("Segments/divider");
            skin.botIcon = Sprite("Icons/icon_swords"); skin.hostIcon = Sprite("Icons/icon_host");
            skin.joinIcon = Sprite("Icons/icon_join"); skin.settingsIcon = Sprite("Icons/icon_settings");
            skin.exitIcon = Sprite("Icons/icon_exit"); skin.backIcon = Sprite("Icons/icon_back");
            skin.playIcon = Sprite("Icons/icon_play"); skin.copyIcon = Sprite("Icons/icon_copy");
            skin.checkIcon = Sprite("Icons/icon_check"); skin.volumeIcon = Sprite("Icons/icon_volume");
            skin.sensitivityIcon = Sprite("Icons/icon_crosshair"); skin.sliderDiamond = Sprite("Frames/diamond_active");
            if (oldSkin != EditorJsonUtility.ToJson(skin)) { EditorUtility.SetDirty(skin); AssetDatabase.SaveAssetIfDirty(skin); }
            if (theme.stoneSkin != skin) { Undo.RecordObject(theme, "Assign stone artwork"); theme.stoneSkin = skin; EditorUtility.SetDirty(theme); AssetDatabase.SaveAssetIfDirty(theme); }
            Debug.Log("Stone artwork installed on existing theme. Existing layout/fonts/input/network/scene are preserved.");
        }
        private static string AssetPath(string source) => Root + source.Substring("Assets/ElEmentalStoneUI".Length);
        public static void ValidateManifestJson(string json)
        {
            var manifest = JsonUtility.FromJson<Manifest>(json);
            if (manifest == null || manifest.assets == null || manifest.assets.Length == 0)
                throw new InvalidOperationException("Stone manifest must contain a nonempty assets array.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in manifest.assets)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id))
                    throw new InvalidOperationException("Stone manifest contains a missing or duplicate ID.");
                if (string.IsNullOrEmpty(entry.file) || !entry.file.StartsWith("Assets/ElEmentalStoneUI/Art/", StringComparison.Ordinal) ||
                    entry.file.Contains("..") || entry.file.Contains("\\") || !entry.file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || !files.Add(entry.file))
                    throw new InvalidOperationException("Invalid or duplicate stone artwork path: " + entry.file);
                if (entry.width <= 0 || entry.height <= 0 || entry.border_lbrt == null || entry.border_lbrt.Length != 4 ||
                    entry.pivot == null || entry.pivot.Length != 2 ||
                    !float.IsFinite(entry.pivot[0]) || !float.IsFinite(entry.pivot[1]) ||
                    entry.pivot[0] < 0 || entry.pivot[0] > 1 || entry.pivot[1] < 0 || entry.pivot[1] > 1)
                    throw new InvalidOperationException("Invalid dimensions, border or pivot for: " + entry.id);
                var b = entry.border_lbrt;
                if (b[0] < 0 || b[1] < 0 || b[2] < 0 || b[3] < 0 || b[0] + b[2] > entry.width || b[1] + b[3] > entry.height)
                    throw new InvalidOperationException("Stone sprite border exceeds its texture: " + entry.id);
                if (entry.id == "button_fx_packed" && entry.sRGB)
                    throw new InvalidOperationException("Packed masks must be linear data: " + entry.id);
            }
            foreach (string role in new[] { "Panels/curtain_quiet", "Buttons/button_normal", "Buttons/button_earth", "Buttons/button_hover",
                "Buttons/button_pressed", "Buttons/button_disabled", "Panels/code_field", "Icons/icon_health", "Icons/icon_mana",
                "Icons/icon_pause", "Panels/window_body", "Panels/ribbon" })
                if (!files.Contains("Assets/ElEmentalStoneUI/Art/" + role + ".png"))
                    throw new InvalidOperationException("Stone manifest lacks required UI role: " + role);
        }
        private static Sprite Sprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/" + name + ".png");
            return sprite != null ? sprite : throw new InvalidOperationException("Missing imported sprite: " + name);
        }
    }
}
