using System;
using UnityEngine;

namespace Elemental.Presentation.UI
{
    // Plain serialized presentation data: no scene objects or UI Toolkit dependencies.
    [Serializable]
    public sealed class HudElementLayout
    {
        [Tooltip("Anchor in the parent group: (0,0) top-left; (1,1) bottom-right.")]
        public Vector2 anchor;
        [Tooltip("Point of this element placed on the anchor; also the rotation/scale origin.")]
        public Vector2 pivot;
        [Tooltip("Offset from the parent anchor, in UI pixels. X right, Y down.")]
        public Vector2 position;
        [Tooltip("Element box in UI pixels; group size also defines its children's anchor space.")]
        public Vector2 size;
        [Tooltip("Scales the whole element, including its text/icon and children.")]
        public Vector2 scale = Vector2.one;
        [Tooltip("Clockwise rotation in degrees around Pivot.")]
        public float rotation;

        public HudElementLayout(Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        { this.anchor = anchor; this.pivot = pivot; this.position = position; this.size = size; }
        public static HudElementLayout Box(float x, float y, float width, float height) =>
            new HudElementLayout(Vector2.zero, Vector2.zero, new Vector2(x, y), new Vector2(width, height));
    }

    [Serializable]
    public sealed class HudVitalLayout
    {
        public HudElementLayout group;
        public HudElementLayout bar = HudElementLayout.Box(0, 0, 58, 290);
        [Tooltip("Group underneath the bar. Move/rotate this to move the icon and number together.")]
        public HudElementLayout underBar = HudElementLayout.Box(0, 286, 58, 58);
        public HudElementLayout icon = HudElementLayout.Box(0, 0, 58, 32);
        public HudElementLayout value = HudElementLayout.Box(0, 32, 58, 22);
        public HudVitalLayout(bool right)
        {
            group = new HudElementLayout(new Vector2(right ? 1 : 0, .4f), new Vector2(right ? 1 : 0, 0),
                new Vector2(right ? -14 : 14, 0), new Vector2(58, 344));
        }
    }

    [Serializable]
    public sealed class HudNavigationLayout
    {
        public HudElementLayout group = new HudElementLayout(Vector2.one, Vector2.one, new Vector2(-27, -22), new Vector2(218, 252));
        public HudElementLayout caption = HudElementLayout.Box(0, 0, 218, 17);
        public HudElementLayout globe = HudElementLayout.Box(0, 17, 218, 218);
        public HudElementLayout legend = HudElementLayout.Box(0, 235, 218, 17);
    }

    [Serializable]
    public sealed class HudPauseLayout
    {
        public HudElementLayout button = new HudElementLayout(Vector2.right, new Vector2(.5f,.5f), new Vector2(-46, 46), new Vector2(44, 44));
        public HudElementLayout icon = new HudElementLayout(new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(20, 18));
    }

    [CreateAssetMenu(menuName = "Elemental/UI/HUD Layout", fileName = "ElementalHudLayout")]
    public sealed class ElementalHudLayout : ScriptableObject
    {
        [Header("Health / Здоровье")]
        public HudVitalLayout health = new HudVitalLayout(false);
        [Header("Energy / Энергия")]
        public HudVitalLayout energy = new HudVitalLayout(true);
        [Header("Navigation / Глобус")]
        public HudNavigationLayout navigation = new HudNavigationLayout();
        [Header("Pause / Пауза")]
        public HudPauseLayout pause = new HudPauseLayout();
        [Header("Score and timer / Счёт и таймер")]
        public HudElementLayout scoreboard = new HudElementLayout(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(390, 95));
        [Header("Per-life result / Результат жизни")]
        public HudElementLayout lifeResult = new HudElementLayout(new Vector2(.5f, .22f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(600, 64));

        [NonSerialized] private int _revision;
        public int Revision => _revision;
        public void NotifyChanged() { unchecked { _revision++; } }
        private void OnValidate() => NotifyChanged();
    }
}
