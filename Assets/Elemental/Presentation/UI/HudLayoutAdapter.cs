using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    public static class HudLayoutAdapter
    {
        public static void Apply(VisualElement element, HudElementLayout layout)
        {
            element.style.position = Position.Absolute;
            element.style.right = StyleKeyword.Auto;
            element.style.bottom = StyleKeyword.Auto;
            element.style.left = Length.Percent(layout.anchor.x * 100);
            element.style.top = Length.Percent(layout.anchor.y * 100);
            element.style.marginLeft = layout.position.x;
            element.style.marginTop = layout.position.y;
            element.style.marginRight = element.style.marginBottom = 0;
            element.style.width = Mathf.Max(1, layout.size.x);
            element.style.height = Mathf.Max(1, layout.size.y);
            element.style.flexShrink = 0;
            element.style.translate = new Translate(Length.Percent(-100 * layout.pivot.x), Length.Percent(-100 * layout.pivot.y));
            element.style.transformOrigin = new TransformOrigin(Length.Percent(100 * layout.pivot.x), Length.Percent(100 * layout.pivot.y));
            element.style.rotate = new Rotate(Angle.Degrees(layout.rotation));
            element.style.scale = new Scale(new Vector3(Mathf.Max(.01f, layout.scale.x), Mathf.Max(.01f, layout.scale.y), 1));
        }
    }
}
