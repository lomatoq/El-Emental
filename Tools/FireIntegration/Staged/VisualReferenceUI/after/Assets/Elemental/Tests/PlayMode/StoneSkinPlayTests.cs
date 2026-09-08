#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Elemental.Presentation.UI;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Elemental.Tests.PlayMode
{
    public sealed class StoneSkinPlayTests
    {
        private Scene _scene, _previous;
        [UnityTearDown] public IEnumerator Restore()
        {
            Time.timeScale = 1;
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }
        [UnityTest] public IEnumerator ProductionSkinKeepsButtonHitLayoutAndLiveHudIdentity()
        {
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            _previous = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(path); SceneManager.SetActiveScene(_scene);
            var gate = Find<EarthSceneReadinessGate>();
            double deadline = Time.realtimeSinceStartupAsDouble + 130;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
            var flow = Find<FrontendFlowController>(); var view = Find<FrontendMenuView>();
            while (flow.State == FrontendState.Loading && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            yield return new WaitForSecondsRealtime(1);
            var theme = AssetDatabase.LoadAssetAtPath<ElementalUITheme>("Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset");
            Assert.That(theme.stoneSkin, Is.Not.Null);
            string savedLayout = EditorJsonUtility.ToJson(theme.hudLayout);
            Directory.CreateDirectory("BuildReports/StoneSkin");
            foreach (var page in new[] { FrontendPage.Main, FrontendPage.Settings, FrontendPage.Host, FrontendPage.Join })
            {
                // Presentation page inspection only; no fake network codes or service calls.
                view.Show(page); yield return null;
                var wordmark = view.transform.Find("Menu contents/Menu column/Stone wordmark").GetComponent<Image>();
                Assert.That(wordmark.gameObject.activeInHierarchy, Is.True, "Header remains outside page-specific visibility.");
                Assert.That(wordmark.sprite, Is.SameAs(theme.stoneSkin.wordmark));
                Assert.That(wordmark.raycastTarget, Is.False);
                if (page == FrontendPage.Settings)
                {
                    var settings = view.transform.Find("Menu contents/Menu column/Settings");
                    Assert.That(settings.Find("MASTER VOLUME value").GetComponent<TMPro.TMP_Text>().text,
                        Is.EqualTo(Mathf.RoundToInt(flow.Preferences.MasterVolume * 100f) + "%"));
                    Assert.That(settings.Find("CAMERA SENSITIVITY value").GetComponent<TMPro.TMP_Text>().text,
                        Is.EqualTo(flow.Preferences.Sensitivity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x"));
                    var slider = settings.Find("MASTER VOLUME slider").GetComponent<UnityEngine.UI.Slider>();
                    Assert.That(slider.handleRect.GetComponent<Image>().sprite, Is.SameAs(theme.stoneSkin.sliderDiamond));
                }
                yield return Capture(page.ToString());
            }
            view.Show(FrontendPage.Main);
            var play = view.transform.Find("Menu contents/Menu column/Main/PLAY VS BOT").GetComponent<Button>();
            var rect = (RectTransform)play.transform; var position = rect.anchoredPosition; var size = rect.sizeDelta;
            var effect = play.GetComponent<FrontendButton>(); var graphic = (Image)play.targetGraphic;
            Assert.That(graphic.transform.name, Is.EqualTo("Press Visual"));
            Assert.That(graphic.sprite, Is.Not.Null);
            var role = graphic.transform.Find("Button role icon").GetComponent<Image>();
            Assert.That(role.sprite, Is.SameAs(theme.stoneSkin.botIcon)); Assert.That(role.raycastTarget, Is.False);
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(470, 70)));
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            effect.OnPointerEnter(pointer); yield return null;
            Assert.That(graphic.overrideSprite, Is.SameAs(theme.stoneSkin.hover));
            effect.OnPointerDown(pointer); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(graphic.overrideSprite, Is.SameAs(theme.stoneSkin.pressed));
            Assert.That(rect.anchoredPosition, Is.EqualTo(position)); Assert.That(rect.sizeDelta, Is.EqualTo(size));
            yield return Capture("Pressed"); effect.OnPointerUp(pointer); effect.OnPointerExit(pointer);
            play.interactable = false; yield return null; Assert.That(graphic.overrideSprite, Is.SameAs(theme.stoneSkin.disabled));
            play.interactable = true;
            var hud = Find<EarthDuelHud>(); var hudRoot = hud.GetComponent<UIDocument>().rootVisualElement.Q("duel-hud");
            var health = hudRoot.Q("health-icon"); var gauge = hudRoot.Q("health-gauge"); var globe = hudRoot.Q("planet-globe");
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(hudRoot.Q("health-icon"), Is.SameAs(health));
            Assert.That(hudRoot.Q("health-gauge"), Is.SameAs(gauge));
            Assert.That(hudRoot.Q("planet-globe"), Is.SameAs(globe));
            Assert.That(health.style.backgroundImage.value.sprite, Is.SameAs(theme.stoneSkin.health));
            Assert.That(EditorJsonUtility.ToJson(theme.hudLayout), Is.EqualTo(savedLayout));
            yield return Capture("Combat");
            flow.Pause(); yield return new WaitForSecondsRealtime(.2f); yield return Capture("Pause");
            Assert.That(EditorJsonUtility.ToJson(theme.hudLayout), Is.EqualTo(savedLayout));
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes("BuildReports/StoneSkin/" + name + ".png", texture.EncodeToPNG()); }
            finally { Object.Destroy(texture); }
        }
        private T Find<T>() where T : Component
        {
            foreach (var root in _scene.GetRootGameObjects())
            { var value = root.GetComponentInChildren<T>(true); if (value != null) return value; }
            throw new System.InvalidOperationException(typeof(T).Name);
        }
    }
}
#endif
