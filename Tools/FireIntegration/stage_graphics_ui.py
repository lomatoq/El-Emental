from pathlib import Path
import json, shutil, hashlib

base = Path(__file__).resolve().parent
repo = base.parents[1]
source = base/'Reference/StoneUI/EL_Emental_StoneUI'
stage = base/'Staged/GraphicsUI'
after = stage/'after'

def write(path, content):
    p = after/path
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(content, encoding='utf-8')

def edit(path, old, new):
    target = after/path
    if not target.exists():
        original = repo/path
        before = stage/'before'/path
        before.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(original, before)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(original, target)
    text = target.read_text(encoding='utf-8-sig')
    assert text.count(old) == 1, (path, old[:80])
    target.write_text(text.replace(old, new), encoding='utf-8')

manifest = json.loads((source/'Source/asset_manifest.json').read_text())
artroot = Path('Assets/Elemental/Content/UI/Stone')
for item in manifest['assets']:
    if item['category'] == 'SourceOnly': continue
    rel = Path(item['file']).relative_to('Assets/ElEmentalStoneUI')
    dest = after/artroot/rel
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source/item['file'], dest)
write(artroot/'asset_manifest.json', json.dumps(manifest, indent=2))

write(Path('Assets/Elemental/Presentation/UI/ElementalStoneSkin.cs'), '''using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    [CreateAssetMenu(menuName = "Elemental/UI/Stone Skin", fileName = "ElementalStoneSkin")]
    public sealed class ElementalStoneSkin : ScriptableObject
    {
        public Sprite curtain, normal, primary, hover, pressed, disabled, codeField;
        public Sprite health, mana, pause, window, ribbon;

        public Sprite Button(bool active, bool down, bool hot, Sprite resting)
            => !active && disabled != null ? disabled : down && pressed != null ? pressed : hot && hover != null ? hover : resting;

        public void ApplyHud(VisualElement root)
        {
            Icon(root.Q("health-icon"), health);
            Icon(root.Q("energy-icon"), mana);
            var pauseElement = root.Q("pause-icon");
            Icon(pauseElement, pause);
            if (pauseElement != null && pause != null)
                foreach (var child in pauseElement.Children()) child.style.visibility = Visibility.Hidden;
            Panel(root.Q("round-result"), window);
            Panel(root.Q("duel-scoreboard"), ribbon);
        }
        private static void Icon(VisualElement element, Sprite sprite)
        {
            if (element == null || sprite == null) return;
            element.style.backgroundImage = new StyleBackground(sprite);
            element.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            element.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            element.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            element.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            if (element is Label label) label.text = "";
        }
        private static void Panel(VisualElement element, Sprite sprite)
        {
            if (element == null || sprite == null) return;
            element.style.backgroundImage = new StyleBackground(sprite);
            element.style.backgroundColor = Color.clear;
            var border = sprite.border;
            element.style.unitySliceLeft = (int)border.x; element.style.unitySliceBottom = (int)border.y;
            element.style.unitySliceRight = (int)border.z; element.style.unitySliceTop = (int)border.w;
        }
    }
}
''')

ui = 'Assets/Elemental/Presentation/UI/'
edit(ui+'ElementalUITheme.cs', 'public ElementalHudLayout hudLayout;', 'public ElementalHudLayout hudLayout;\n        [Header("Optional stone artwork (layout and fonts remain independent)")]\n        public ElementalStoneSkin stoneSkin;')
edit(ui+'FrontendButton.cs', 'private RectTransform _visual;', 'private RectTransform _visual;\n        private Sprite _restingSprite;')
edit(ui+'FrontendButton.cs', 'EnsureVisualRoot();\n            _button.transition', 'EnsureVisualRoot();\n            _restingSprite = (_graphic as Image)?.sprite;\n            _button.transition')
edit(ui+'FrontendButton.cs', '_graphic.color = Color.Lerp(_normal, _highlight, _blend) * (active ? Color.white : new Color(.6f, .6f, .6f, .65f));', '''if (_theme.stoneSkin != null && _restingSprite != null && _graphic is Image skinned)
            {
                var sprite = _theme.stoneSkin.Button(active, _pressed, _hot, _restingSprite);
                if (skinned.sprite != sprite) skinned.sprite = sprite;
                skinned.color = Color.white;
            }
            else _graphic.color = Color.Lerp(_normal, _highlight, _blend) * (active ? Color.white : new Color(.6f, .6f, .6f, .65f));''')
edit(ui+'FrontendMenuView.cs', 'Stretch(backing.rectTransform, new Vector2(0, 0), new Vector2(.35f, 1)); backing.raycastTarget = false;', '''Stretch(backing.rectTransform, new Vector2(0, 0), new Vector2(.35f, 1)); backing.raycastTarget = false;
            if (_theme.stoneSkin != null && _theme.stoneSkin.curtain != null)
            {
                var stoneCurtain = Image(backing.transform, "Stone curtain", Color.white);
                stoneCurtain.sprite = _theme.stoneSkin.curtain; stoneCurtain.preserveAspect = true;
                stoneCurtain.raycastTarget = false; Stretch(stoneCurtain.rectTransform, Vector2.zero, Vector2.one);
            }''')
edit(ui+'FrontendMenuView.cs', 'rect.GetComponent<Image>().color = _theme.ink; _codeInput = rect.GetComponent<TMP_InputField>();', '''rect.GetComponent<Image>().color = _theme.ink; _codeInput = rect.GetComponent<TMP_InputField>();
            if (_theme.stoneSkin != null && _theme.stoneSkin.codeField != null)
            { var field = rect.GetComponent<Image>(); field.sprite = _theme.stoneSkin.codeField; field.type = UnityEngine.UI.Image.Type.Sliced; field.color = Color.white; }''')
edit(ui+'FrontendMenuView.cs', 'var graphic = r.GetComponent<Image>(); graphic.color = normal;', '''var graphic = r.GetComponent<Image>(); graphic.color = normal;
            var skin = _theme.stoneSkin;
            bool skinned = skin != null && skin.normal != null;
            if (skinned) { graphic.sprite = tier == 0 && skin.primary != null ? skin.primary : skin.normal; graphic.type = UnityEngine.UI.Image.Type.Sliced; graphic.color = Color.white; }''')
edit(ui+'FrontendMenuView.cs', 'tier == 0 ? _theme.ink : _theme.text);', 'tier == 0 && !skinned ? _theme.ink : _theme.text);')
edit(ui+'EarthDuelHud.cs', 'ApplyThemeTextSizes();\n                ApplyLayoutIfChanged(true);', 'ApplyThemeTextSizes();\n                ApplyLayoutIfChanged(true);\n                if (theme.stoneSkin != null) theme.stoneSkin.ApplyHud(_root);')

write(Path('Assets/Elemental/Authoring/Editor/StoneUI/ElementalStoneSkinInstaller.cs'), '''using System;
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
        { public string id, category, file; public bool sRGB; public int[] border_lbrt; public float[] pivot; }

        [MenuItem("Elemental/UI/Install Stone Artwork")]
        public static void Install()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ElementalUITheme>(Theme);
            if (theme == null) throw new InvalidOperationException("Existing ElementalUITheme is required; installer does not recreate the frontend.");
            var manifest = JsonUtility.FromJson<Manifest>(System.IO.File.ReadAllText(Root + "/asset_manifest.json"));
            foreach (var entry in manifest.assets)
            {
                if (entry.category == "SourceOnly") continue;
                var path = Root + entry.file.Substring("Assets/ElEmentalStoneUI".Length);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing stone artwork: " + path);
                string previous = EditorJsonUtility.ToJson(importer);
                bool data = entry.category == "Masks", world = entry.category == "Environment";
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
            if (oldSkin != EditorJsonUtility.ToJson(skin)) { EditorUtility.SetDirty(skin); AssetDatabase.SaveAssetIfDirty(skin); }
            if (theme.stoneSkin != skin) { Undo.RecordObject(theme, "Assign stone artwork"); theme.stoneSkin = skin; EditorUtility.SetDirty(theme); AssetDatabase.SaveAssetIfDirty(theme); }
            Debug.Log("Stone artwork installed on existing theme. Existing layout/fonts/input/network/scene are preserved.");
        }
        private static Sprite Sprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/" + name + ".png");
            return sprite != null ? sprite : throw new InvalidOperationException("Missing imported sprite: " + name);
        }
    }
}
''')
print('Staged', len(list(after.rglob('*.*'))), 'files')
