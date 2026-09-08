using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;
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
            if(AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/Cinzel/Cinzel-Regular.ttf")==null ||
               AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/animation_presets.json")==null)
                throw new InvalidOperationException("Import reference Cinzel font and animation presets before installing artwork.");
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
            skin.earth=Sprite("Icons/element_earth");skin.fire=Sprite("Icons/element_fire");skin.water=Sprite("Icons/element_water");skin.air=Sprite("Icons/element_air");
            skin.diamond=Sprite("Frames/diamond_active");skin.ring=Sprite("Frames/circle_ring");skin.minimap=Sprite("Frames/minimap_frame");
            skin.timerPlate=Sprite("Frames/timer_plate");skin.teamWing=Sprite("Frames/team_wing");skin.halo=Sprite("FX/glow_soft");skin.shine=Sprite("FX/line_glint");skin.danger=Sprite("Buttons/button_danger");
            skin.referenceProfile=PrepareReferenceProfile(theme);
            skin.referenceSelected=PrepareReferenceSelected();
            skin.referenceCurtain=PrepareReferenceCurtain();
            if (oldSkin != EditorJsonUtility.ToJson(skin)) { EditorUtility.SetDirty(skin); AssetDatabase.SaveAssetIfDirty(skin); }
            if (theme.stoneSkin != skin) { Undo.RecordObject(theme, "Assign stone artwork"); theme.stoneSkin = skin; EditorUtility.SetDirty(theme); AssetDatabase.SaveAssetIfDirty(theme); }
            Debug.Log("Stone artwork installed on existing theme. Existing layout/fonts/input/network/scene are preserved.");
        }
        private static Sprite PrepareReferenceCurtain()
        {
            const string path=Root+"/Art/Panels/curtain_reference.png";
            if(!System.IO.File.Exists(path))return null;
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            string before=EditorJsonUtility.ToJson(importer);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.sRGBTexture=true;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.isReadable=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;
            if(before!=EditorJsonUtility.ToJson(importer))importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static Sprite PrepareReferenceSelected()
        {
            const string texturePath=Root+"/Art/Buttons/button_reference_selected.png";
            const string spritePath=Root+"/Art/Buttons/Reference Selected.asset";
            if(!System.IO.File.Exists(texturePath))return null;
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            string before=EditorJsonUtility.ToJson(importer);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.isReadable=false;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;
            if(before!=EditorJsonUtility.ToJson(importer))importer.SaveAndReimport();
            var existing=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(texture.width!=2007||texture.height!=783)throw new InvalidOperationException("Reference selected texture must retain native2007x783 pixels before applying its crop.");
            // Crop transparent delivery padding via sprite metadata; original raster stays untouched.
            var sprite=UnityEngine.Sprite.Create(texture,new Rect(27,273,1952,290),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(180,16,180,16));
            sprite.name="Reference Selected";
            if(existing!=null)
            {
                bool same=existing.rect==sprite.rect&&existing.border==sprite.border&&existing.texture==texture;
                var a=existing.uv;var b=sprite.uv;same&=a.Length==b.Length;
                for(int i=0;same&&i<a.Length;i++)same=a[i]==b[i];
                if(!same){EditorUtility.CopySerialized(sprite,existing);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);}
                UnityEngine.Object.DestroyImmediate(sprite);return existing;
            }
            AssetDatabase.CreateAsset(sprite,spritePath);return sprite;
        }
        private static TMP_FontAsset PrepareReferenceFont(TMP_FontAsset fallback)
        {
            const string path=Root+"/Fonts/Cinzel/Cinzel Reference SDF.asset";
            var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if(existing!=null)return existing;
            var source=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/Cinzel/Cinzel-Regular.ttf");
            if(source==null)throw new InvalidOperationException("Import the licensed Cinzel reference font before installing the reference profile.");
            var font=TMP_FontAsset.CreateFontAsset(source,80,8,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            font.name="Cinzel Reference SDF";
            font.fallbackFontAssetTable=new List<TMP_FontAsset>();if(fallback!=null)font.fallbackFontAssetTable.Add(fallback);
            font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 /:.!?+-()%",out string missing);
            if(!string.IsNullOrEmpty(missing))throw new InvalidOperationException("Reference font lacks required characters: "+missing);
            AssetDatabase.CreateAsset(font,path);
            if(font.material!=null)AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(var atlas in font.atlasTextures)if(atlas!=null)AssetDatabase.AddObjectToAsset(atlas,font);
            EditorUtility.SetDirty(font);AssetDatabase.SaveAssetIfDirty(font);return font;
        }
        private static ElementalStoneReferenceProfile PrepareReferenceProfile(ElementalUITheme theme)
        {
            const string profilePath=Root+"/StoneReferenceProfile.asset";
            var profile=AssetDatabase.LoadAssetAtPath<ElementalStoneReferenceProfile>(profilePath);
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<ElementalStoneReferenceProfile>();
                profile.menuFont=PrepareReferenceFont(theme.readableFont);
                profile.hudFont=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/Cinzel/Cinzel-Regular.ttf");
                AssetDatabase.CreateAsset(profile,profilePath);
            }
            string before=EditorJsonUtility.ToJson(profile);
            profile.animationPresets=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/animation_presets.json");
            if(profile.hudLayout==null)
            {
                const string layoutPath=Root+"/StoneReferenceHudLayout.asset";
                var layout=AssetDatabase.LoadAssetAtPath<ElementalHudLayout>(layoutPath);
                if(layout==null)
                {
                    layout=ScriptableObject.CreateInstance<ElementalHudLayout>();
                    foreach(var vital in new[]{layout.health,layout.energy})
                    {
                        vital.group.anchor=new Vector2(vital==layout.energy?1:0,.35f);
                        vital.group.position=new Vector2(vital==layout.energy?-10:10,0);
                        vital.group.size=new Vector2(82,420);vital.bar=HudElementLayout.Box(0,0,82,318);
                        vital.underBar=HudElementLayout.Box(0,326,82,90);vital.icon=HudElementLayout.Box(28,0,26,26);vital.value=HudElementLayout.Box(0,28,82,38);
                    }
                    layout.scoreboard.size=new Vector2(600,122);layout.scoreboard.position=new Vector2(0,20);
                    layout.navigation.group.position=new Vector2(-30,-40);layout.navigation.group.size=new Vector2(260,295);
                    layout.navigation.globe=HudElementLayout.Box(0,0,260,260);layout.navigation.caption=HudElementLayout.Box(0,265,260,24);layout.navigation.legend=HudElementLayout.Box(0,290,260,16);
                    layout.pause.button.position=new Vector2(-30,28);layout.pause.button.size=new Vector2(62,62);layout.pause.icon.size=new Vector2(28,28);
                    AssetDatabase.CreateAsset(layout,layoutPath);
                }
                profile.hudLayout=layout;
            }
            if(before!=EditorJsonUtility.ToJson(profile)){EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);}
            return profile;
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
