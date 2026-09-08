using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    [CreateAssetMenu(menuName = "Elemental/UI/Stone Skin", fileName = "ElementalStoneSkin")]
    public sealed class ElementalStoneSkin : ScriptableObject
    {
        public ElementalStoneReferenceProfile referenceProfile;
        public Sprite earth, fire, water, air, diamond, ring, minimap, timerPlate, teamWing, halo, shine, danger;
        public Sprite curtain, normal, primary, hover, pressed, disabled, codeField;
        public Sprite referenceSelected, referenceCurtain, referenceNormal, referenceCard;
        public Sprite roundWinPlate, roundLossPlate;
        // Native pixel insets from the complete construction bounds to its cream button face.
        public Vector4 referenceSelectedInsets;
        public Sprite health, mana, pause, window, ribbon;
        public Sprite wordmark, divider, botIcon, hostIcon, joinIcon, settingsIcon, exitIcon;
        public Sprite backIcon, playIcon, copyIcon, checkIcon, volumeIcon, sensitivityIcon, sliderDiamond;

        public Sprite Button(bool active, bool down, bool hot, Sprite resting)
            => !active && referenceProfile != null && referenceProfile.enabled && referenceNormal != null ? referenceNormal : !active && disabled != null ? disabled : referenceProfile != null && referenceProfile.enabled && referenceSelected != null && (hot || down) ? referenceSelected : down && pressed != null ? pressed : hot && hover != null ? hover : resting;

        // Owned by one live HUD tree, not by a shared asset or static registry.
        public sealed class HudBinding
        {
            private readonly VisualElement _root;
            private readonly Snapshot[] _roles;
            public HudBinding(VisualElement root)
            {
                _root = root;
                _roles = new[] { new Snapshot(root.Q("health-icon")), new Snapshot(root.Q("energy-icon")),
                    new Snapshot(root.Q("pause-icon")), new Snapshot(root.Q("round-result")), new Snapshot(root.Q("duel-scoreboard")) };
            }
            public bool Matches(VisualElement root) => ReferenceEquals(_root, root);
            public void Apply(ElementalStoneSkin skin)
            {
                foreach (var role in _roles) role.Restore();
                if (skin != null) skin.ApplyHud(_root);
            }
            private sealed class Snapshot
            {
                private readonly VisualElement _element;
                private readonly StyleBackground _image;
                private readonly StyleColor _color;
                private readonly StyleBackgroundSize _size;
                private readonly StyleBackgroundRepeat _repeat;
                private readonly StyleBackgroundPosition _x, _y;
                private readonly StyleInt _left, _right, _top, _bottom;
                private readonly string _text;
                private readonly List<VisualElement> _children = new List<VisualElement>();
                private readonly List<StyleEnum<Visibility>> _visibility = new List<StyleEnum<Visibility>>();
                public Snapshot(VisualElement element)
                {
                    _element = element; if (element == null) return;
                    _image=element.style.backgroundImage; _color=element.style.backgroundColor;
                    _size=element.style.backgroundSize; _repeat=element.style.backgroundRepeat;
                    _x=element.style.backgroundPositionX; _y=element.style.backgroundPositionY;
                    _left=element.style.unitySliceLeft; _right=element.style.unitySliceRight;
                    _top=element.style.unitySliceTop; _bottom=element.style.unitySliceBottom;
                    _text=(element as Label)?.text;
                    foreach(var child in element.Children()) { _children.Add(child); _visibility.Add(child.style.visibility); }
                }
                public void Restore()
                {
                    if (_element == null) return;
                    _element.style.backgroundImage=_image; _element.style.backgroundColor=_color;
                    _element.style.backgroundSize=_size; _element.style.backgroundRepeat=_repeat;
                    _element.style.backgroundPositionX=_x; _element.style.backgroundPositionY=_y;
                    _element.style.unitySliceLeft=_left; _element.style.unitySliceRight=_right;
                    _element.style.unitySliceTop=_top; _element.style.unitySliceBottom=_bottom;
                    if (_element is Label label) label.text=_text;
                    for(int i=0;i<_children.Count;i++) _children[i].style.visibility=_visibility[i];
                }
            }
        }

        public void ApplyHud(VisualElement root)
        {
            Icon(root.Q("health-icon"), health);
            Icon(root.Q("energy-icon"), mana);
            var pauseElement = root.Q("pause-icon");
            Icon(pauseElement, pause);
            if (pauseElement != null && pause != null)
                foreach (var child in pauseElement.Children()) child.style.visibility = Visibility.Hidden;
            Panel(root.Q("round-result"), window);
            Panel(root.Q("duel-scoreboard"), ribbon);
        }
        private static void Icon(VisualElement element, Sprite sprite)
        {
            if (element == null || sprite == null) return;
            element.style.backgroundImage = new StyleBackground(sprite);
            element.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            element.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            element.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            element.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            if (element is Label label) label.text = "";
        }
        private static void Panel(VisualElement element, Sprite sprite)
        {
            if (element == null || sprite == null) return;
            element.style.backgroundImage = new StyleBackground(sprite);
            element.style.backgroundColor = Color.clear;
            var border = sprite.border;
            element.style.unitySliceLeft = (int)border.x; element.style.unitySliceBottom = (int)border.y;
            element.style.unitySliceRight = (int)border.z; element.style.unitySliceTop = (int)border.w;
        }
    }
}
