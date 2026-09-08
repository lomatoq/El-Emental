#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Combat;
using Elemental.Runtime.Physics;
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
        private int _oldGameSize=-1;
        [UnityTearDown] public IEnumerator Restore()
        {
            Time.timeScale = 1; RestoreGameView();
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }
        [UnityTest] public IEnumerator ProductionSkinKeepsButtonHitLayoutAndLiveHudIdentity()
        {
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            BindCapture(new Vector2Int(1920,1080));
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
            Assert.That(theme.stoneSkin.referenceProfile,Is.Not.Null);
            Assert.That(theme.stoneSkin.referenceProfile.PresetCount,Is.EqualTo(20));
            Assert.That(view.transform.Find("Menu contents/Left veil"),Is.Null,"Reference curtain must not retain rectangular veil.");
            Directory.CreateDirectory("BuildReports/StoneSkin");
            foreach (var page in new[] { FrontendPage.Main, FrontendPage.Settings, FrontendPage.Host, FrontendPage.Join })
            {
                // Presentation page inspection only; no fake network codes or service calls.
                view.Show(page); yield return new WaitForSecondsRealtime(.35f);
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
            view.Show(FrontendPage.Main); yield return new WaitForSecondsRealtime(.35f);
            var play = view.transform.Find("Menu contents/Menu column/Main/PLAY VS BOT").GetComponent<Button>();
            var rect = (RectTransform)play.transform; var position = rect.anchoredPosition; var size = rect.sizeDelta;
            var effect = play.GetComponent<FrontendButton>(); var graphic = (Image)play.targetGraphic;
            Assert.That(graphic.transform.name, Is.EqualTo("Press Visual"));
            Assert.That(graphic.sprite, Is.Not.Null);
            var role = graphic.transform.Find("Button role icon").GetComponent<Image>();
            Assert.That(role.sprite, Is.SameAs(theme.stoneSkin.botIcon)); Assert.That(role.raycastTarget, Is.False);
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(560, 78)));
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            effect.OnPointerEnter(pointer); yield return null;
            Assert.That(graphic.overrideSprite, Is.SameAs(theme.stoneSkin.referenceSelected != null ? theme.stoneSkin.referenceSelected : theme.stoneSkin.hover));
            effect.OnPointerDown(pointer); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(graphic.overrideSprite, Is.SameAs(theme.stoneSkin.referenceSelected != null ? theme.stoneSkin.referenceSelected : theme.stoneSkin.pressed));
            Assert.That(rect.anchoredPosition, Is.EqualTo(position)); Assert.That(rect.sizeDelta, Is.EqualTo(size));
            yield return Capture("Pressed"); effect.OnPointerUp(pointer); effect.OnPointerExit(pointer);
            play.interactable = false; yield return null; Assert.That(graphic.overrideSprite, Is.SameAs(theme.stoneSkin.disabled));
            play.interactable = true;
            effect.ReducedMotion=true; effect.OnPointerDown(pointer); yield return new WaitForSecondsRealtime(.12f);
            Assert.That(graphic.transform.localScale,Is.EqualTo(Vector3.one)); effect.OnPointerUp(pointer);effect.ReducedMotion=false;
            var hud = Find<EarthDuelHud>(); var hudRoot = hud.GetComponent<UIDocument>().rootVisualElement.Q("duel-hud");
            var health = hudRoot.Q("health-icon"); var gauge = hudRoot.Q("health-gauge"); var globe = hudRoot.Q("planet-globe");
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(hudRoot.Q("health-icon"), Is.SameAs(health));
            Assert.That(hudRoot.Q("health-gauge"), Is.SameAs(gauge));
            Assert.That(hudRoot.Q("planet-globe"), Is.SameAs(globe));
            Assert.That(health.style.backgroundImage.value.sprite, Is.SameAs(theme.stoneSkin.health));
            Assert.That(EditorJsonUtility.ToJson(theme.hudLayout), Is.EqualTo(savedLayout));
            Assert.That(hudRoot.Q("reference-hud-wordmark"),Is.Not.Null);
            Assert.That(hudRoot.Q("reference-element-wheel"),Is.Not.Null);
            Assert.That(hudRoot.Q("reference-map-frame").parent,Is.SameAs(globe));
            yield return Capture("Combat");
            flow.Pause(); yield return new WaitForSecondsRealtime(.2f); yield return Capture("Pause");
            Assert.That(EditorJsonUtility.ToJson(theme.hudLayout), Is.EqualTo(savedLayout));
            flow.Resume(); yield return new WaitForSecondsRealtime(.2f);
            var duel=Find<EarthMvpDuelController>();
            foreach(var sceneRoot in _scene.GetRootGameObjects()) foreach(var bot in sceneRoot.GetComponentsInChildren<EarthMvpBotController>(true)) bot.enabled=false;
            foreach(var outcome in new[]{"Victory","Defeat","Draw"})
            {
                duel.RestartRound();
                double readyDeadline=Time.realtimeSinceStartupAsDouble+30;
                while(duel.ArenaResetInProgress&&Time.realtimeSinceStartupAsDouble<readyDeadline)yield return null;
                Assert.That(duel.ArenaResetInProgress,Is.False);duel.SetRoundReady(true);yield return null;
                hud.SetLocalPerspective(EarthDuelFighterId.Player,true);
                if(outcome!="Draw")
                {
                    var loser=outcome=="Victory"?EarthDuelFighterId.Bot:EarthDuelFighterId.Player;
                    duel.RequestKnockout(loser,RagdollHandoff.Uniform(Vector3.zero));yield return null;
                }
                // Fast-forward only pure clock; production Update processes IsRoundOver transition.
                var match=(EarthDuelMatchState)typeof(EarthMvpDuelController).GetField("_match",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(duel);
                match.Step(Mathf.Max(0,match.RemainingSeconds-.03f));yield return new WaitForSeconds(.08f);
                Assert.That(duel.IsRoundOver,Is.True);yield return new WaitForSecondsRealtime(.6f);
                Assert.That(hudRoot.Q<Label>(className:"duel-result-title").text,Is.EqualTo(outcome.ToUpperInvariant()));
                yield return Capture(outcome);
                hud.SetLocalPerspective(EarthDuelFighterId.Player,false);yield return null;
                Assert.That(hudRoot.Q<UnityEngine.UIElements.Button>("restart-round").enabledSelf,Is.False);
            }
            Assert.That(EditorJsonUtility.ToJson(theme.hudLayout), Is.EqualTo(savedLayout));
        }
        private void BindCapture(Vector2Int size)
        {
#if UNITY_EDITOR
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { System.Enum.Parse(groupType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
            var constructor = sizeType.GetConstructor(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null,
                new[] {kindType, typeof(int), typeof(int), typeof(string)}, null);
            object item = constructor.Invoke(new[] {System.Enum.ToObject(kindType, 1), (object)size.x, size.y, "Alpha QA " + size.x + "x" + size.y});
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] {item});
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            var viewType = assembly.GetType("UnityEditor.GameView");
            var window = UnityEditor.EditorWindow.GetWindow(viewType);
            var selected = viewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (_oldGameSize < 0) _oldGameSize = (int)selected.GetValue(window);
            selected.SetValue(window, index); window.Focus(); window.Repaint();
#else
            Screen.SetResolution(size.x, size.y, false);
#endif
        }
        private void RestoreGameView()
        {
#if UNITY_EDITOR
            if (_oldGameSize < 0) return;
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            var window = UnityEditor.EditorWindow.GetWindow(type);
            type.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(window, _oldGameSize);
#endif
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
