#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Elemental.Presentation.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Elemental.Tests.PlayMode
{
    public sealed class HudLayoutPlayTests
    {
        private GameObject _object;
        private PanelSettings _panel;
        private RenderTexture _texture;
        private ElementalUITheme _theme;
        private ElementalHudLayout _layout;
        private float _timeScale;

        [UnityTest]
        public IEnumerator LiveNestedLayoutKeepsAnchorsAndPausePickingAtThreeAspectRatios()
        {
            _timeScale = Time.timeScale;
            _theme = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ElementalUITheme>("Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset"));
            Assert.That(_theme.hudLayout, Is.Not.Null, "Saved production theme must reference the layout.");
            _layout = Object.Instantiate(_theme.hudLayout); _theme.hudLayout = _layout;
            // This fixture verifies the original authored layout; the reference profile has its own production fixture.
            _theme.stoneSkin = null;
            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            _panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panel.clearColor = true; _panel.colorClearValue = new Color(.022f, .035f, .055f, 1);
            _object = new GameObject("HUD Layout Acceptance", typeof(UIDocument));
            var document = _object.GetComponent<UIDocument>(); document.panelSettings = _panel;
            document.rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Elemental/Content/UI/EarthDuelHud.uss"));
            var hud = _object.AddComponent<EarthDuelHud>();
            int clicks = 0; hud.ConfigurePause(() => clicks++);
            hud.SetFrontendPresentation(_theme, true);
            var root = document.rootVisualElement.Q("duel-hud"); root.style.opacity = 1;
            string folder = "BuildReports/HudLayout"; Directory.CreateDirectory(folder);
            foreach (var resolution in new[] { new Vector2Int(1920,1080), new Vector2Int(1920,1200), new Vector2Int(2520,1080) })
            {
                document.rootVisualElement.style.width = resolution.x;
                document.rootVisualElement.style.height = resolution.y;
                if (_texture != null) { _panel.targetTexture = null; _texture.Release(); Object.Destroy(_texture); }
                _texture = new RenderTexture(resolution.x, resolution.y, 0); _texture.Create(); _panel.targetTexture = _texture;
                for (int i = 0; i < 5; i++) yield return null;
                foreach (string name in new[] { "health-panel", "mana-panel", "planet-panel", "pause-match" })
                {
                    var rect = root.Q(name).worldBound;
                    Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0), name);
                    Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0), name);
                    Assert.That(rect.xMax, Is.LessThanOrEqualTo(root.worldBound.xMax + 1), name);
                    Assert.That(rect.yMax, Is.LessThanOrEqualTo(root.worldBound.yMax + 1), name);
                }
                Capture(folder + "/Default-" + resolution.x + "x" + resolution.y + ".png");
            }

            // Change the asset while paused, without rebuilding/rebinding the HUD.
            Time.timeScale = 0;
            _layout.health.group.anchor = new Vector2(.25f, .2f);
            _layout.health.group.position = new Vector2(25, 30);
            _layout.health.group.rotation = 12;
            _layout.health.underBar.position = new Vector2(80, 90);
            _layout.health.underBar.rotation = -35;
            _layout.health.icon.position = new Vector2(10, 15);
            _layout.health.icon.scale = new Vector2(1.6f, 1.3f);
            _layout.health.value.anchor = Vector2.one;
            _layout.health.value.pivot = Vector2.one;
            _layout.health.value.position = new Vector2(30, 25);
            _layout.energy.underBar.position = new Vector2(-80, 120);
            _layout.energy.icon.rotation = 40;
            _layout.navigation.group.position = new Vector2(-180, -80);
            _layout.navigation.globe.size = new Vector2(140, 140);
            _layout.navigation.globe.rotation = 25;
            _layout.pause.button.anchor = new Vector2(.8f, .15f);
            _layout.pause.button.size = new Vector2(80, 64);
            _layout.pause.button.rotation = 20;
            _layout.pause.icon.size = new Vector2(34, 28);
            _layout.pause.icon.rotation = -40;
            using var layoutCpu = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "Elemental.DuelHud.ApplyLayout", 16);
            _layout.NotifyChanged();
            for (int i = 0; i < 5; i++) yield return null;
            var health = root.Q("health-panel");
            Assert.That(health.layout.x, Is.EqualTo(root.layout.width * .25f + 25).Within(.2f));
            Assert.That(health.resolvedStyle.rotate.angle.value, Is.EqualTo(12).Within(.1f));
            var under = root.Q("health-under-bar");
            Assert.That(under.parent, Is.SameAs(health));
            Assert.That(under.layout.position, Is.EqualTo(new Vector2(80, 90)));
            Assert.That(root.Q("health-icon").parent, Is.SameAs(under));
            Assert.That(root.Q("health-icon").resolvedStyle.scale.value.x, Is.EqualTo(1.6f).Within(.01f));
            var value = root.Q("health-value");
            Vector2 expected = under.LocalToWorld(new Vector2(under.layout.width + 30, under.layout.height + 25));
            Assert.That(Vector2.Distance(value.LocalToWorld(new Vector2(value.layout.width, value.layout.height)), expected), Is.LessThan(.25f));
            Assert.That(root.Q("planet-globe").resolvedStyle.width, Is.EqualTo(140));
            Assert.That(root.Q("energy-icon").resolvedStyle.rotate.angle.value, Is.EqualTo(40));
            var pause = root.Q<Button>("pause-match");
            Assert.That(pause.resolvedStyle.width, Is.EqualTo(80));
            Assert.That(root.Q("pause-icon").resolvedStyle.rotate.angle.value, Is.EqualTo(-40));
            var picked = root.panel.Pick(pause.worldBound.center);
            Assert.That(picked == pause || pause.Contains(picked), Is.True, "Rotated/resized pause button must remain pickable at its visible location.");
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = pause.worldBound.center }))
            { down.target = pause; pause.SendEvent(down); }
            yield return null;
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = pause.worldBound.center }))
            { up.target = pause; pause.SendEvent(up); }
            yield return null;
            Assert.That(clicks, Is.EqualTo(1));
            Capture(folder + "/Edited-Paused.png");
            long peakLayoutNs = 0;
            for (int i = 0; i < layoutCpu.Count; i++) peakLayoutNs = System.Math.Max(peakLayoutNs, layoutCpu.GetSample(i).Value);
            File.WriteAllText(folder + "/acceptance.txt", "Production HUD + saved theme. Three aspect ratios; nested anchors, offsets, size, scale, rotation; live change at timeScale=0; visible pause hit target and callback passed.\n" +
                "Peak layout apply CPU ns: " + peakLayoutNs + " (event-driven, not every frame).\n");
        }

        private void Capture(string path)
        {
            var old = RenderTexture.active; RenderTexture.active = _texture;
            var image = new Texture2D(_texture.width, _texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG()); Object.Destroy(image); RenderTexture.active = old;
        }
        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = _timeScale;
            if (_object != null) Object.Destroy(_object);
            yield return null;
            if (_panel != null) { _panel.targetTexture = null; Object.Destroy(_panel); }
            if (_texture != null) { _texture.Release(); Object.Destroy(_texture); }
            if (_layout != null) Object.Destroy(_layout);
            if (_theme != null) Object.Destroy(_theme);
        }
    }
}
#endif
