using System.Collections;
using System.IO;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace Elemental.Tests.PlayMode
{
    public sealed class FeelFollowupUiPlayTests
    {
        private Scene _scene, _previous;
        private GameObject _buttonObject, _events;
        private ElementalUITheme _theme;
        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1;
            if (_buttonObject != null) Object.Destroy(_buttonObject);
            if (_events != null) Object.Destroy(_events);
            if (_theme != null) Object.Destroy(_theme);
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }
        [UnityTest]
        public IEnumerator ButtonHoldsCenteredPressWithoutChangingAuthoredAnchorOrScale()
        {
            _events = new GameObject("Press Test Events", typeof(EventSystem));
            _theme = ScriptableObject.CreateInstance<ElementalUITheme>(); _theme.pressedScale = .94f;
            _buttonObject = new GameObject("Authored Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
            var rect = _buttonObject.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1); rect.anchorMin = rect.anchorMax = new Vector2(.2f, .7f);
            rect.anchoredPosition = new Vector2(72, -145); rect.sizeDelta = new Vector2(470, 70);
            rect.localScale = new Vector3(1.15f, .9f, 1);
            var button = _buttonObject.GetComponent<UnityEngine.UI.Button>(); button.targetGraphic = _buttonObject.GetComponent<Image>();
            var effect = _buttonObject.AddComponent<FrontendButton>(); effect.Configure(_theme, null, Color.white, Color.yellow);
            var visual = (RectTransform)rect.Find("Press Visual");
            var pointer = new PointerEventData(_events.GetComponent<EventSystem>()) { button = PointerEventData.InputButton.Left };
            var baseline = rect.localScale; var position = rect.anchoredPosition;
            Vector3 visualCenter = visual.TransformPoint(visual.rect.center);
            effect.OnPointerDown(pointer); yield return new WaitForSecondsRealtime(.15f);
            effect.OnPointerExit(pointer); yield return new WaitForSecondsRealtime(.4f);
            Assert.That(visual.localScale.x, Is.EqualTo(.94f).Within(.001f));
            Assert.That(rect.localScale, Is.EqualTo(baseline)); Assert.That(rect.anchoredPosition, Is.EqualTo(position));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(0, 1)));
            Assert.That(Vector3.Distance(visual.TransformPoint(visual.rect.center), visualCenter), Is.LessThan(.01f));
            effect.OnPointerUp(pointer); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
            effect.OnPointerDown(pointer); yield return null; effect.enabled = false;
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one)); Assert.That(rect.localScale, Is.EqualTo(baseline));
        }
        [UnityTest]
        public IEnumerator ActualLifeLossesDisplayWinLoseAndMutualDrawBeforeRespawn()
        {
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            _previous = SceneManager.GetActiveScene(); Assert.That(SceneManager.GetSceneByPath(path).isLoaded, Is.False);
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(path); SceneManager.SetActiveScene(_scene);
            var gate = Find<EarthSceneReadinessGate>();
            double deadline = Time.realtimeSinceStartupAsDouble + 130;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);
            var duel = Find<EarthMvpDuelController>(); var hud = Find<EarthDuelHud>();
            var label = hud.GetComponent<UIDocument>().rootVisualElement.Q<Label>("life-result");
            foreach (var outcome in new[] { EarthLifeResult.Won, EarthLifeResult.Lost, EarthLifeResult.Draw, EarthLifeResult.Won })
            {
                // These are lives inside one match. Wait for the ordinary respawn;
                // RestartRound starts a different game and asynchronously restores terrain.
                double respawnDeadline = Time.realtimeSinceStartupAsDouble + 15d;
                while ((duel.PlayerHealth < 100f || duel.BotHealth < 100f || !duel.CombatAllowed) &&
                    duel.ArenaResetError == null && Time.realtimeSinceStartupAsDouble < respawnDeadline)
                { Find<EarthMvpBotController>().enabled = false; yield return null; }
                Assert.That(duel.ArenaResetError, Is.Null);
                Assert.That(duel.ArenaResetInProgress, Is.False);
                Assert.That(duel.CombatAllowed, Is.True);
                Assert.That(duel.PlayerHealth, Is.EqualTo(100f)); Assert.That(duel.BotHealth, Is.EqualTo(100f));
                Find<EarthMvpBotController>().enabled = false;
                if (outcome != EarthLifeResult.Won) duel.RequestKnockout(EarthDuelFighterId.Player, RagdollHandoff.Uniform(Vector3.zero));
                if (outcome != EarthLifeResult.Lost) duel.RequestKnockout(EarthDuelFighterId.Bot, RagdollHandoff.Uniform(Vector3.zero));
                string expectedText=outcome == EarthLifeResult.Won ? "YOU WON ROUND" : outcome == EarthLifeResult.Lost ? "YOU LOST ROUND" : "DRAW";
                double revealDeadline=Time.realtimeSinceStartupAsDouble+1.5d;
                while((label.text!=expectedText || label.resolvedStyle.display!=DisplayStyle.Flex || label.resolvedStyle.opacity<.995f)
                    && Time.realtimeSinceStartupAsDouble<revealDeadline)yield return null;
                // UI Toolkit resolves layout/styles after the gameplay Update that starts the toast.
                yield return new WaitForEndOfFrame();
                Assert.That(label.text, Is.EqualTo(expectedText));
                Assert.That(label.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(label.resolvedStyle.opacity, Is.GreaterThan(.9f));
                var theme=(ElementalUITheme)typeof(EarthDuelHud).GetField("_theme",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(hud);
                Assert.That(theme.stoneSkin,Is.Not.Null);
                var reference=theme.stoneSkin.referenceProfile;
                var expectedFont=reference!=null && reference.enabled ? reference.hudFont : theme.hudFont;
                Assert.That(label.style.unityFontDefinition.value.font,Is.EqualTo(expectedFont),"Round result must keep the display font, not the numeric HUD font.");
                var expectedPlate=theme.stoneSkin.roundWinPlate;
                Assert.That(expectedPlate,Is.Not.Null);
                Assert.That(label.style.backgroundImage.value.sprite.texture,Is.EqualTo(expectedPlate.texture));
                Assert.That(label.style.backgroundImage.value.sprite.rect,Is.EqualTo(expectedPlate.rect));
                Assert.That(label.style.backgroundImage.value.sprite.border,Is.EqualTo(Vector4.zero));
                Assert.That(label.style.color.value.r,Is.LessThan(.2f));
                AssertWholePlate(label);
                var layout=reference!=null&&reference.enabled&&reference.hudLayout!=null?reference.hudLayout:theme.hudLayout;
                Assert.That(label.style.scale.value.value.x,Is.EqualTo(layout.lifeResult.scale.x*.88f).Within(.005f));
                Assert.That(label.style.scale.value.value.y,Is.EqualTo(layout.lifeResult.scale.y*.88f).Within(.005f));
                Directory.CreateDirectory("BuildReports/FeelFollowup");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("BuildReports/FeelFollowup/Life-" + outcome + ".png");
                if(outcome==EarthLifeResult.Lost)
                {
                    var returning=hud.GetComponent<UIDocument>().rootVisualElement.Q<Label>(className:"duel-respawn");
                    double returningDeadline=Time.realtimeSinceStartupAsDouble+1d;
                    while((returning.resolvedStyle.display!=DisplayStyle.Flex || returning.resolvedStyle.opacity<.995f)
                        && Time.realtimeSinceStartupAsDouble<returningDeadline)yield return null;
                    yield return new WaitForEndOfFrame();
                    Assert.That(returning.resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
                    Assert.That(returning.resolvedStyle.opacity,Is.GreaterThan(.99f));
                    Assert.That(returning.style.backgroundImage.value.sprite,Is.Null);
                    var veil=hud.GetComponent<UIDocument>().rootVisualElement.Q("returning-veil");
                    Assert.That(veil.resolvedStyle.display,Is.EqualTo(DisplayStyle.Flex));
                    Assert.That(veil.resolvedStyle.backgroundColor.a,Is.InRange(.4f,.6f));
                    Assert.That(veil.parent.IndexOf(veil),Is.LessThan(returning.parent.IndexOf(returning)));
                    ScreenCapture.CaptureScreenshot("BuildReports/FeelFollowup/Returning.png");
                    var parentBounds=returning.parent.worldBound;
                    var center=returning.worldBound.center;
                    Assert.That(center.x,Is.EqualTo(parentBounds.center.x).Within(3f));
                    Assert.That(center.y,Is.EqualTo(parentBounds.center.y).Within(3f));
                    for(int stableFrame=0;stableFrame<6;stableFrame++)
                    {
                        yield return null;
                        Assert.That(Vector2.Distance(returning.worldBound.center,center),Is.LessThan(.1f),"Centered Returning must not accumulate layout offsets.");
                    }
                    var setting=theme.menuPresentation.Get(MenuScreenId.Returning).Find("Returning");
                    Assert.That(returning.style.scale.value.value.x,Is.EqualTo(setting.scale.x).Within(.005f));
                    Assert.That(returning.style.scale.value.value.y,Is.EqualTo(setting.scale.y).Within(.005f));
                    ScreenCapture.CaptureScreenshot("BuildReports/FeelFollowup/Returning.png");
                }
                yield return null;
            }
        }
        private static void AssertWholePlate(VisualElement element)
        {
            Assert.That(element.style.backgroundSize.value,Is.EqualTo(new BackgroundSize(BackgroundSizeType.Contain)));
            Assert.That(element.style.backgroundRepeat.value,Is.EqualTo(new BackgroundRepeat(Repeat.NoRepeat,Repeat.NoRepeat)));
            Assert.That(element.style.unitySliceLeft.value,Is.Zero);Assert.That(element.style.unitySliceRight.value,Is.Zero);
            Assert.That(element.style.unitySliceTop.value,Is.Zero);Assert.That(element.style.unitySliceBottom.value,Is.Zero);
            Assert.That(element.style.unitySliceScale.value,Is.EqualTo(1));
        }
        private T Find<T>() where T : Component
        {
            foreach (var root in _scene.GetRootGameObjects())
            { var found = root.GetComponentInChildren<T>(true); if (found != null) return found; }
            throw new System.InvalidOperationException(typeof(T).Name);
        }
    }
}
