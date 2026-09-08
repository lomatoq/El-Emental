using System;
using UnityEngine;

namespace Elemental.Presentation.UI
{
    [Serializable]
    public sealed class SidebarRevealSettings
    {
        [Min(.01f)] public float itemSeconds = .26f;
        [Min(0)] public float itemStagger = .045f;
        [Min(0)] public float panelLead = .14f;
        [Min(0)] public float pausePanelLead = .07f;
        [Min(0)] public float horizontalTravel = 34f;
        [Min(0)] public float panelTravel = 170f;
        [Min(.01f)] public float pausePanelSeconds = .22f;
    }

    // Presentation delta is removed before layout is evaluated, then composed once.
    // It never becomes an authored position and never moves a whole Buttons wrapper.
    public sealed class SidebarRevealNode
    {
        private readonly RectTransform rect;
        private readonly CanvasGroup group;
        private Vector2 delta;
        private Vector2 layoutPosition, renderedPosition;
        public readonly float Order;
        public float Alpha => group.alpha;
        public SidebarRevealNode(RectTransform target, float order)
        {
            rect = target; Order = order;
            var existingGroup = target.GetComponent<CanvasGroup>();
            group = existingGroup != null ? existingGroup : target.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = group.blocksRaycasts = false;
        }
        public void RemoveDelta()
        {
            if(delta!=Vector2.zero)
                rect.anchoredPosition=rect.anchoredPosition==renderedPosition?layoutPosition:rect.anchoredPosition-delta;
            delta=Vector2.zero;
        }
        public static float Progress(float age, float delay, float duration)
        {
            float t = Mathf.Clamp01((age - delay) / Mathf.Max(.001f, duration));
            return 1f - Mathf.Pow(1f - t, 3f);
        }
        public void Apply(float age, float lead, SidebarRevealSettings settings, bool reduced)
        {
            float progress = Progress(age, reduced ? 0 : lead + Order * settings.itemStagger,
                reduced ? .07f : settings.itemSeconds);
            group.alpha = progress;
            group.interactable = group.blocksRaycasts = progress > .85f;
            delta = reduced ? Vector2.zero : new Vector2(-settings.horizontalTravel * (1f - progress), 0);
            layoutPosition=rect.anchoredPosition;
            renderedPosition=layoutPosition+delta;
            rect.anchoredPosition=renderedPosition;
        }
    }
}
