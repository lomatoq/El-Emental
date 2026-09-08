using System.Collections;
using System.IO;
using System.Reflection;
using Elemental.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Elemental.Tests.PlayMode
{
    public sealed class FrontendMotionContinuityTests
    {
        private GameObject _root;
        private ElementalUITheme _theme;
        private ElementalStoneSkin _skin;
        private ElementalStoneReferenceProfile _reference;
        private TextAsset _presets;
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            Time.timeScale = 0;
            _root = new GameObject("Isolated frontend motion regression");
            _theme = ScriptableObject.CreateInstance<ElementalUITheme>();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _timeScale;
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_theme);
            if (_skin != null) Object.DestroyImmediate(_skin);
            if (_reference != null) Object.DestroyImmediate(_reference);
            if (_presets != null) Object.DestroyImmediate(_presets);
        }

        [UnityTest]
        public IEnumerator PointerPressSurvivesDeselectAndDisabledButtonCannotResumeStalePress()
        {
            var events = Child("Events").AddComponent<EventSystem>();
            var go = Child("Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1); rect.localScale = new Vector3(1.15f, .9f, 1);
            rect.sizeDelta = new Vector2(320, 70);
            var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            _theme.pressedScale = .94f;
            var motion = go.AddComponent<FrontendButton>(); motion.Configure(_theme, null, Color.white, Color.yellow);
            var visual = (RectTransform)rect.Find("Press Visual");
            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left };
            var anchor = rect.anchoredPosition; var scale = rect.localScale;
            motion.OnPointerDown(pointer); motion.OnDeselect(pointer); motion.OnPointerExit(pointer);
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(visual.localScale.x, Is.EqualTo(.94f).Within(.001f), "Deselect cannot release a mouse button still held down.");
            button.interactable = false;
            yield return new WaitForSecondsRealtime(.18f);
            button.interactable = true;
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(visual.localScale.x, Is.EqualTo(1).Within(.001f));
            Assert.That(rect.anchoredPosition, Is.EqualTo(anchor)); Assert.That(rect.localScale, Is.EqualTo(scale));
            motion.OnPointerDown(pointer); yield return new WaitForSecondsRealtime(.1f);
            motion.ReducedMotion = true; yield return null; yield return null;
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one), "Reduced Motion immediately removes scaling, including an active press.");
        }

        [UnityTest]
        public IEnumerator InterruptedPanelReversesFromRenderedPoseAndTracksPartialVisibilityWhilePaused()
        {
            var view = MakeMenu(out var group, out var panel, out var flow);
            view.SetVisibility(1, true);
            yield return new WaitForSecondsRealtime(.09f);
            Assert.That(group.alpha, Is.GreaterThan(0));
            float alpha = group.alpha; float x = panel.anchoredPosition.x;
            view.SetVisibility(0, false);
            // A zero-age evaluation isolates continuity from frame-rate differences.
            Set(view, "_panelTrackAge", -Time.unscaledDeltaTime);
            typeof(FrontendMenuView).GetMethod("UpdateReferenceMotion", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
            Assert.That(group.alpha, Is.EqualTo(alpha).Within(.0001f));
            Assert.That(panel.anchoredPosition.x, Is.EqualTo(x).Within(.0001f));
            Assert.That(group.interactable, Is.False); Assert.That(group.blocksRaycasts, Is.False);
            yield return new WaitForSecondsRealtime(.045f);
            alpha = group.alpha; x = panel.anchoredPosition.x;
            view.SetVisibility(1, true);
            Set(view, "_panelTrackAge", -Time.unscaledDeltaTime);
            typeof(FrontendMenuView).GetMethod("UpdateReferenceMotion", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
            Assert.That(group.alpha, Is.EqualTo(alpha).Within(.0001f));
            Assert.That(panel.anchoredPosition.x, Is.EqualTo(x).Within(.0001f));
            yield return new WaitForSecondsRealtime(.32f);
            Assert.That(group.alpha, Is.EqualTo(1).Within(.001f));
            view.SetVisibility(.4f, false);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(group.alpha, Is.EqualTo(.4f).Within(.001f), "Reference tracks must respect a caller's partial fade target.");
            flow.Preferences.Set(1, .7f, 1, true);
            view.SetVisibility(1, true);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(panel.anchoredPosition, Is.EqualTo(new Vector2(17, -23)));
            Assert.That(group.alpha, Is.EqualTo(1).Within(.001f));
        }

        private FrontendMenuView MakeMenu(out CanvasGroup group, out RectTransform panel, out FrontendFlowController flow)
        {
            _reference = ScriptableObject.CreateInstance<ElementalStoneReferenceProfile>();
            _presets = new TextAsset(File.ReadAllText(Path.Combine(Application.dataPath, "Elemental/Content/UI/Stone/animation_presets.json")));
            _reference.animationPresets = _presets;
            _skin = ScriptableObject.CreateInstance<ElementalStoneSkin>(); _skin.referenceProfile = _reference; _theme.stoneSkin = _skin;
            flow = Child("Disabled flow").AddComponent<FrontendFlowController>(); flow.enabled = false;
            var view = Child("Menu").AddComponent<FrontendMenuView>();
            panel = Child("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.anchoredPosition = new Vector2(17, -23);
            group = panel.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0;
            var pages = new GameObject[5];
            for (int i = 0; i < pages.Length; i++) { pages[i] = Child("Page " + i, typeof(RectTransform)); pages[i].SetActive(false); }
            Set(view, "_theme", _theme); Set(view, "_flow", flow); Set(view, "_group", group);
            Set(view, "_panel", panel); Set(view, "_panelBasePosition", panel.anchoredPosition); Set(view, "_pages", pages);
            return view;
        }

        private GameObject Child(string name, params System.Type[] components)
        { var go = new GameObject(name, components); go.transform.SetParent(_root.transform, false); return go; }
        private static void Set(object owner, string field, object value)
            => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    }
}
