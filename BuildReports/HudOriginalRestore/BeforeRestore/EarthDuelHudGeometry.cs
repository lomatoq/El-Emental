using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    /// <summary>Resolution-independent HUD geometry; does not render or modify world lighting.</summary>
    public sealed class EarthDuelGauge : VisualElement
    {
        public float Fill = 1f;
        public float Trail = 1f;
        public bool Mirror;
        public Color Accent;
        private Color _outline = new Color(.86f, .73f, .50f, 1f);
        private static readonly CustomStyleProperty<Color> AccentProperty = new CustomStyleProperty<Color>("--duel-gauge-accent");
        private static readonly CustomStyleProperty<Color> OutlineProperty = new CustomStyleProperty<Color>("--duel-gauge-outline");
        public EarthDuelGauge()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
        }
        private void OnStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(AccentProperty, out Color accent)) Accent = accent;
            if (evt.customStyle.TryGetValue(OutlineProperty, out Color outline)) _outline = outline;
            MarkDirtyRepaint();
        }
        private Vector2 Point(float x, float y)
        {
            float scaledX = x * contentRect.width / 56f;
            return new Vector2(Mirror ? contentRect.width - scaledX : scaledX, y);
        }
        private void Shape(Painter2D p, float lower, float upper, float inset)
        {
            float h = contentRect.height;
            p.BeginPath();
            for (int side = 0; side < 2; side++)
            for (int i = 0; i <= 32; i++)
            {
                float t = Mathf.Lerp(lower, upper, side == 0 ? i / 32f : 1f - i / 32f);
                float bow = Mathf.Sin(t * Mathf.PI) * 19f;
                float x = 8f + bow + (side == 0 ? inset : 21f - inset);
                Vector2 v = Point(x, h * (1f - t));
                if (side == 0 && i == 0) p.MoveTo(v); else p.LineTo(v);
            }
            p.ClosePath();
        }
        private void Draw(MeshGenerationContext context)
        {
            Painter2D p = context.painter2D;
            Shape(p, .015f, .985f, 0f);
            p.fillColor = new Color(.047f, .09f, .118f, .94f); p.Fill();
            p.strokeColor = _outline; p.lineWidth = 2f; p.Stroke();
            Shape(p, .065f, Mathf.Lerp(.065f, .95f, Mathf.Clamp01(Trail)), 4f);
            p.fillColor = new Color(1f, .96f, .82f, .55f); p.Fill();
            Shape(p, .065f, Mathf.Lerp(.065f, .95f, Mathf.Clamp01(Fill)), 4f);
            p.fillColor = Accent; p.Fill();
            p.strokeColor = new Color(.047f, .09f, .118f, .8f); p.lineWidth = 2f;
            for (int i = 1; i < 5; i++)
            {
                float t = i / 5f, x = 8f + Mathf.Sin(t * Mathf.PI) * 19f;
                p.BeginPath(); p.MoveTo(Point(x + 4, contentRect.height * (1 - t)));
                p.LineTo(Point(x + 17, contentRect.height * (1 - t))); p.Stroke();
            }
        }
    }

    public sealed class EarthHologramGlobe : VisualElement
    {
        public Quaternion View = Quaternion.identity;
        public Vector3 PlayerRadial = Vector3.forward;
        public Vector3 PlayerForward = Vector3.up;
        public Vector3 ArenaRadial = Vector3.forward;
        public bool HasNavigation;
        private static readonly Color Gold = new Color(1f, .86f, .57f, .8f);
        private static readonly Quaternion GridFrame = Quaternion.Euler(32f, 17f, 24f);
        public EarthHologramGlobe() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
        private Vector2 Project(Vector3 point)
        {
            Vector3 v = View * point;
            float r = Mathf.Min(contentRect.width, contentRect.height) * .405f;
            return contentRect.center + new Vector2(v.x, -v.y) * r;
        }
        private void Draw(MeshGenerationContext context)
        {
            Painter2D p = context.painter2D;
            float r = Mathf.Min(contentRect.width, contentRect.height) * .405f;
            Vector2 c = contentRect.center;
            p.BeginPath(); p.Arc(c, r, 0f, 360f);
            p.fillColor = new Color(.015f, .045f, .06f, .35f); p.Fill();
            p.lineWidth = 1f; p.strokeColor = Gold; p.Stroke();
            for (int latitude = -2; latitude <= 2; latitude++)
            {
                float a = latitude * Mathf.PI / 6f;
                DrawCircle(p, a, true);
            }
            for (int longitude = 0; longitude < 6; longitude++) DrawCircle(p, longitude * Mathf.PI / 6f, false);
            p.strokeColor = new Color(1f, .87f, .6f, .35f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * .5f;
                Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.BeginPath(); p.MoveTo(c + v * (r + 6)); p.LineTo(c + v * (r + 13)); p.Stroke();
            }
            if (!HasNavigation) return;
            Vector2 arena = Project(ArenaRadial);
            float alpha = (View * ArenaRadial).z >= 0 ? 1f : .28f;
            Vector2 playerPoint = Project(PlayerRadial);
            if (Vector2.Distance(arena, playerPoint) < 24f)
            {
                Vector2 anchor = arena;
                arena += new Vector2(25f, -17f);
                p.strokeColor = new Color(1f, .9f, .67f, alpha * .5f); p.lineWidth = 1f;
                p.BeginPath(); p.MoveTo(anchor); p.LineTo(arena); p.Stroke();
            }
            p.strokeColor = new Color(1f, .9f, .67f, alpha); p.fillColor = new Color(.95f, .68f, .3f, alpha * .25f);
            p.lineWidth = 1.6f;
            p.BeginPath(); p.MoveTo(arena + new Vector2(-8, -5)); p.LineTo(arena + new Vector2(-8, 4));
            p.BezierCurveTo(arena + new Vector2(-8, 9), arena + new Vector2(8, 9), arena + new Vector2(8, 4));
            p.LineTo(arena + new Vector2(8, -5)); p.ClosePath(); p.Fill(); p.Stroke();
            p.BeginPath(); p.MoveTo(arena + new Vector2(-8, -5));
            p.BezierCurveTo(arena + new Vector2(-8, -10), arena + new Vector2(8, -10), arena + new Vector2(8, -5));
            p.BezierCurveTo(arena + new Vector2(8, 0), arena + new Vector2(-8, 0), arena + new Vector2(-8, -5)); p.Stroke();
            Vector2 player = Project(PlayerRadial);
            Vector3 forward = View * Vector3.ProjectOnPlane(PlayerForward, PlayerRadial).normalized;
            Vector2 d = new Vector2(forward.x, -forward.y).normalized;
            if (d.sqrMagnitude < .1f) d = Vector2.up;
            Vector2 side = new Vector2(-d.y, d.x);
            p.BeginPath(); p.MoveTo(player + d * 10); p.LineTo(player - d * 6 + side * 5);
            p.LineTo(player - d * 3); p.LineTo(player - d * 6 - side * 5); p.ClosePath();
            p.fillColor = new Color(.34f, .9f, 1f, 1); p.Fill();
            p.strokeColor = Color.white; p.lineWidth = 1; p.Stroke();
        }
        private void DrawCircle(Painter2D p, float angle, bool latitude)
        {
            Vector3 previous = default;
            for (int i = 0; i <= 64; i++)
            {
                float t = i * Mathf.PI * 2 / 64;
                Vector3 current = latitude
                    ? new Vector3(Mathf.Cos(angle) * Mathf.Cos(t), Mathf.Sin(angle), Mathf.Cos(angle) * Mathf.Sin(t))
                    : new Vector3(Mathf.Cos(angle) * Mathf.Cos(t), Mathf.Sin(t), Mathf.Sin(angle) * Mathf.Cos(t));
                // Arbitrary atlas meridian tilt keeps the north-pole spawn readable as a sphere.
                // Navigation directions still use the unchanged planet coordinate frame.
                current = GridFrame * current;
                if (i > 0)
                {
                    float alpha = (View * ((previous + current) * .5f)).z >= 0 ? .42f : .1f;
                    p.strokeColor = new Color(1f, .86f, .57f, alpha); p.lineWidth = .8f;
                    p.BeginPath(); p.MoveTo(Project(previous)); p.LineTo(Project(current)); p.Stroke();
                }
                previous = current;
            }
        }
    }
}
