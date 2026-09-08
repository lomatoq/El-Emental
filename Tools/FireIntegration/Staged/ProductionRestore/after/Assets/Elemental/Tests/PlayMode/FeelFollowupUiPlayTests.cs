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
                yield return new WaitForSeconds(.4f);
                Assert.That(label.text, Is.EqualTo(outcome == EarthLifeResult.Won ? "YOU WON ROUND" : outcome == EarthLifeResult.Lost ? "YOU LOST ROUND" : "DRAW"));
                Assert.That(label.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(label.resolvedStyle.opacity, Is.GreaterThan(.9f));
                Directory.CreateDirectory("BuildReports/FeelFollowup");
                ScreenCapture.CaptureScreenshot("BuildReports/FeelFollowup/Life-" + outcome + ".png");
                yield return new WaitForEndOfFrame();
            }
        }
        private T Find<T>() where T : Component
        {
            foreach (var root in _scene.GetRootGameObjects())
            { var found = root.GetComponentInChildren<T>(true); if (found != null) return found; }
            throw new System.InvalidOperationException(typeof(T).Name);
        }
    }
}
