using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Elemental.Presentation.UI;
namespace Elemental.Tests.EditMode
{
    public sealed class StoneReferenceProfileTests
    {
        [Test] public void ArchivePresetsDriveDurationAndReducedMotionLimit()
        {
            var profile=ScriptableObject.CreateInstance<ElementalStoneReferenceProfile>();
            var json=new TextAsset(File.ReadAllText("Assets/Elemental/Content/UI/Stone/animation_presets.json"));
            try
            {
                profile.animationPresets=json;Assert.That(profile.PresetCount,Is.EqualTo(20));
                Assert.That(profile.Duration("button_press",1),Is.EqualTo(.07f));
                Assert.That(profile.Duration("selected_breath",1),Is.EqualTo(3.6f));
                Assert.That(profile.Duration("success",1,true),Is.EqualTo(.08f));
                Assert.That(profile.Duration("button_press",1,true),Is.EqualTo(.07f));
                Assert.That(profile.Sample("panel_enter","x",0),Is.EqualTo(-28));
                Assert.That(profile.Sample("panel_exit","x",1),Is.EqualTo(-16));
                Assert.That(profile.Sample("panel_exit","alpha",1),Is.Zero);
                Assert.That(profile.Sample("content_enter","y",0),Is.EqualTo(8));
                Assert.That(profile.Sample("success","y",0),Is.EqualTo(12));
                Assert.That(profile.Sample("success","scale",0),Is.EqualTo(.96f));
                Assert.That(profile.Sample("defeat","y",0),Is.EqualTo(5));
                Assert.That(profile.Sample("defeat","scale",0,1),Is.EqualTo(1),"Omitted channels retain neutral defaults.");
                Assert.That(profile.Sample("button_hover","glow",1),Is.EqualTo(.42f));
                Assert.That(profile.Sample("button_hover","scale",1),Is.EqualTo(1.014f));
                Assert.That(profile.Sample("button_press","scale",1),Is.EqualTo(.984f));
                Assert.That(profile.Sample("focus_enter","focus",1),Is.EqualTo(1));
                Assert.That(profile.Sample("element_select","glow",1),Is.EqualTo(.7f));
                Assert.That(profile.Sample("selected_breath","opacity",0),Is.EqualTo(.28f));
                Assert.That(profile.Sample("selected_breath","opacity",1),Is.EqualTo(.42f));
                Assert.That(profile.Sample("error_feedback","x",0),Is.Zero);
                Assert.That(profile.Sample("error_feedback","x",.5f),Is.EqualTo(3).Within(.001));
                Assert.That(profile.Sample("error_feedback","x",1),Is.Zero);
                Assert.That(profile.Sample("shine_pass","phase",1),Is.EqualTo(1));
                Assert.That(profile.Sample("connect_pulse","opacity",0),Is.EqualTo(.35f));
                Assert.That(profile.Sample("connect_pulse","opacity",1),Is.EqualTo(.75f));
                Assert.That(profile.SampleValue("meter_damage_tail",1,0,.18f),Is.EqualTo(.25f).Within(.001));
            }
            finally{Object.DestroyImmediate(profile);Object.DestroyImmediate(json);}
        }
        [Test] public void ReferenceProfileOwnsSeparateLayoutAndFontWithoutReplacingOriginalTheme()
        {
            var theme=AssetDatabase.LoadAssetAtPath<ElementalUITheme>("Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset");
            var profile=theme.stoneSkin.referenceProfile;
            Assert.That(profile,Is.Not.Null);Assert.That(profile.enabled,Is.True);
            Assert.That(profile.hudLayout,Is.Not.SameAs(theme.hudLayout));
            var originalFont=new SerializedObject(theme).FindProperty("labelFont").objectReferenceValue;
            var referenceFont=new SerializedObject(profile).FindProperty("menuFont").objectReferenceValue;
            Assert.That(referenceFont,Is.Not.SameAs(originalFont));
            Assert.That(profile.hudLayout.scoreboard.size,Is.EqualTo(new Vector2(600,122)));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(theme.hudLayout)),Is.EqualTo("373ceaee55b6d9d419a33ce7bf2f6f1e"));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(originalFont)),Is.EqualTo("fbe6f191b03478e45a5588742711ce26"));
        }
    }
}
