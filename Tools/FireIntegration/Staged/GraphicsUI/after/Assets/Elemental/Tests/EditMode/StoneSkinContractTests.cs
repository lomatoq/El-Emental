using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Elemental.Presentation.UI;
using Elemental.Authoring.Editor;
namespace Elemental.Tests.EditMode
{
    public sealed class StoneSkinContractTests
    {
        [Test] public void OptionalSkinRestoresGlyphsAndPauseWithoutChangingManualLayout()
        {
            var root = new VisualElement();
            var health = new Label("HP") { name = "health-icon" }; root.Add(health);
            health.style.left = -22; health.style.top = 2.8f; health.style.width = 31;
            var pause = new VisualElement { name = "pause-icon" }; root.Add(pause);
            var stroke = new VisualElement(); pause.Add(stroke); stroke.style.visibility = Visibility.Visible;
            var gauge = new VisualElement { name = "existing-gauge" }; root.Add(gauge);
            var skin = ScriptableObject.CreateInstance<ElementalStoneSkin>();
            var texture = new Texture2D(8,8); var sprite = Sprite.Create(texture,new Rect(0,0,8,8),Vector2.one*.5f);
            try
            {
                skin.health=skin.pause=sprite;
                var binding = new ElementalStoneSkin.HudBinding(root); binding.Apply(skin);
                Assert.That(health.text, Is.Empty); Assert.That(stroke.style.visibility.value, Is.EqualTo(Visibility.Hidden));
                binding.Apply(null);
                Assert.That(health.text, Is.EqualTo("HP")); Assert.That(health.style.backgroundImage.value.sprite, Is.Null);
                Assert.That(stroke.style.visibility.value, Is.EqualTo(Visibility.Visible));
                Assert.That(health.style.left.value.value, Is.EqualTo(-22)); Assert.That(health.style.top.value.value, Is.EqualTo(2.8f));
                Assert.That(health.style.width.value.value, Is.EqualTo(31)); Assert.That(root.Q("existing-gauge"), Is.SameAs(gauge));
                binding.Apply(skin); skin.health=null; binding.Apply(skin); Assert.That(health.text, Is.EqualTo("HP"));
                Assert.That(binding.Matches(root), Is.True); Assert.That(binding.Matches(new VisualElement()), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(skin); }
        }
        [Test] public void ManifestPreflightAcceptsOriginalAndRejectsEscapingPathAndOversizedBorder()
        {
            string json=File.ReadAllText("Assets/Elemental/Content/UI/Stone/asset_manifest.json");
            Assert.DoesNotThrow(()=>ElementalStoneSkinInstaller.ValidateManifestJson(json));
            Assert.Throws<InvalidOperationException>(()=>ElementalStoneSkinInstaller.ValidateManifestJson(json.Replace("Assets/ElEmentalStoneUI/Art/", "Assets/ElEmentalStoneUI/Art/../")));
            Assert.Throws<InvalidOperationException>(()=>ElementalStoneSkinInstaller.ValidateManifestJson("{\"assets\":[{\"id\":\"bad\",\"file\":\"Assets/ElEmentalStoneUI/Art/bad.png\",\"width\":1,\"height\":1,\"border_lbrt\":[2,0,0,0],\"pivot\":[0.5,0.5]}]}"));
        }
    }
    public sealed class StoneSkinInstallerIdempotenceTests
    {
        [Test] public void ReinstallPreservesInstalledSkinThemeLayoutAndAllImportSettings()
        {
            const string folder="Assets/Elemental/Content/UI/Stone";
            var skin=AssetDatabase.LoadAssetAtPath<ElementalStoneSkin>(folder+"/ElementalStoneSkin.asset");
            Assert.That(skin, Is.Not.Null, "Run Install Stone Artwork before this explicit idempotence test.");
            var theme=AssetDatabase.LoadAssetAtPath<ElementalUITheme>("Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset");
            var paths=new System.Collections.Generic.List<string>(Directory.GetFiles(folder,"*",SearchOption.AllDirectories));
            paths.Add(AssetDatabase.GetAssetPath(theme)); paths.Add(AssetDatabase.GetAssetPath(theme.hudLayout));
            foreach(var scene in Directory.GetFiles("Assets/Elemental", "*.unity", SearchOption.AllDirectories)) paths.Add(scene);
            var before=new System.Collections.Generic.Dictionary<string,byte[]>();
            foreach(var path in paths) if(File.Exists(path)) before[path]=File.ReadAllBytes(path);
            string themeJson=EditorJsonUtility.ToJson(theme); string skinGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skin));
            ElementalStoneSkinInstaller.Install(); ElementalStoneSkinInstaller.Install();
            foreach(var item in before) Assert.That(File.ReadAllBytes(item.Key), Is.EqualTo(item.Value), item.Key);
            Assert.That(EditorJsonUtility.ToJson(theme), Is.EqualTo(themeJson));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skin)), Is.EqualTo(skinGuid));
        }
    }
}
