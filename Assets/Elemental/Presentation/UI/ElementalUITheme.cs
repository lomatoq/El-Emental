using TMPro;
using UnityEngine;

namespace Elemental.Presentation.UI
{
    [CreateAssetMenu(menuName = "Elemental/UI/Theme", fileName = "ElementalUITheme")]
    public sealed class ElementalUITheme : ScriptableObject
    {
        [Header("HUD positioning (open this asset to edit groups, icons, pause and globe)")]
        public ElementalHudLayout hudLayout;
        [Header("Menu layouts: sidebar, pages, victory and defeat")]
        public MenuPresentationLibrary menuPresentation;
        [Header("Optional stone artwork (layout and fonts remain independent)")]
        public ElementalStoneSkin stoneSkin;
        [Header("Per-life round result")]
        [Min(.3f)] public float lifeResultSeconds = 2.4f;
        [Range(0f, .3f), Tooltip("Deaths this close together display DRAW once.")]
        public float simultaneousDeathWindow = .12f;
        [Min(10f)] public float lifeResultFontSize = 37f;
        public Color ink = new Color(.031f, .082f, .129f, .94f);
        public Color text = new Color(1f, .957f, .867f);
        public Color muted = new Color(.69f, .72f, .71f);
        public Color accent = new Color(.961f, .843f, .631f);
        public Color energy = new Color(.255f, .812f, 1f);
        public Color damage = new Color(.949f, .318f, .325f);
        public TMP_FontAsset displayFont, labelFont, readableFont;
        [Header("In-game HUD font (all labels and numbers)")]
        public Font hudFont;
        public Sprite logo, panelDetail;
        public AudioClip hover, press, confirm, back, error, copy, connect;
        public FrontendAudioProfile frontendAudio;
        [Header("Interaction durations (seconds)")]
        [Min(.01f)] public float hoverSeconds = .1f, pressSeconds = .07f, releaseSeconds = .12f;
        [Range(.9f, 1f)] public float pressedScale = .98f;
        [Min(.1f)] public float transitionSeconds = .85f;
        [Header("Pre-match countdown")]
        [Min(1f)] public float countdownSeconds = 4f;
        [Min(.1f), Tooltip("Camera flies into gameplay during the last seconds of the countdown.")] public float countdownCameraBlendSeconds = 1.5f;
        [Header("Type size relative to the authored layout")]
        [Range(.8f, 1.2f)] public float menuFontScale = 1f;
        [Range(.8f, 1.2f)] public float hudFontScale = 1f;
        [Header("HUD type sizes (the existing enlarged layout)")]
        public float hudScoreSize = 51.5f, hudTimerSize = 38.1f, hudCaptionSize = 11.2f;
        public float hudSymbolSize = 26.9f, hudValueSize = 13.4f, hudLegendSize = 10.1f;
        public float hudRespawnSize = 17.9f, hudResultSize = 37f, hudButtonSize = 14f;
    }
}
