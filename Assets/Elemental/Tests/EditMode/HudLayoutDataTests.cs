using Elemental.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class HudLayoutDataTests
    {
        [Test]
        public void SavingLayoutRetainsIndependentNestedTransforms()
        {
            var source = ScriptableObject.CreateInstance<ElementalHudLayout>();
            var restored = ScriptableObject.CreateInstance<ElementalHudLayout>();
            try
            {
                source.health.underBar.anchor = new Vector2(.25f, .75f);
                source.health.underBar.position = new Vector2(-35, 72);
                source.health.icon.rotation = 37;
                source.health.value.scale = new Vector2(1.2f, 1.4f);
                source.pause.icon.size = new Vector2(31, 25);
                source.navigation.globe.pivot = new Vector2(.8f, .1f);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), restored);
                Assert.That(restored.health.underBar.anchor, Is.EqualTo(source.health.underBar.anchor));
                Assert.That(restored.health.underBar.position, Is.EqualTo(source.health.underBar.position));
                Assert.That(restored.health.icon.rotation, Is.EqualTo(37));
                Assert.That(restored.health.value.scale, Is.EqualTo(source.health.value.scale));
                Assert.That(restored.pause.icon.size, Is.EqualTo(source.pause.icon.size));
                Assert.That(restored.navigation.globe.pivot, Is.EqualTo(source.navigation.globe.pivot));
                Assert.That(restored.energy.icon.rotation, Is.Zero, "Editing health must not alter energy.");
                Assert.That(restored.pause.button.size, Is.EqualTo(new Vector2(44, 44)), "Icon size must not alter button hit area.");
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(restored); }
        }
    }
}
