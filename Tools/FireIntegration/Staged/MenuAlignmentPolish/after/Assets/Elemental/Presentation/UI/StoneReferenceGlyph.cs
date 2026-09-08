using UnityEngine;
using UnityEngine.UI;

namespace Elemental.Presentation.UI
{
    /// <summary>Small reference silhouettes and rim-local light. No texture backdrop or input surface.</summary>
    public sealed class StoneReferenceGlyph : MaskableGraphic
    {
        public enum Shape { Diamond, DiamondGlow, ButtonGlow, People, PersonPlus, Gear, Door, Target, Rule }
        public Shape shape;
        public static StoneReferenceGlyph Create(Transform parent, string name, Shape shape, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(StoneReferenceGlyph));
            go.transform.SetParent(parent, false);
            var graphic = go.GetComponent<StoneReferenceGlyph>();
            graphic.shape = shape; graphic.color = color; graphic.raycastTarget = false;
            return graphic;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = GetPixelAdjustedRect();
            Vector2 P(float x, float y) => new Vector2(r.xMin + r.width * x, r.yMin + r.height * y);
            void Poly(Color tint, params Vector2[] points)
            {
                int first = vh.currentVertCount;
                foreach (var p in points) vh.AddVert(P(p.x,p.y), tint, Vector2.zero);
                for (int i=1;i<points.Length-1;i++) vh.AddTriangle(first,first+i,first+i+1);
            }
            void Line(Vector2 a, Vector2 b, float width, Color tint)
            {
                var n = new Vector2(-(b-a).y,(b-a).x).normalized * width * .5f;
                Poly(tint,a+n,b+n,b-n,a-n);
            }
            void Ring(float radius, float width, Color tint, int sides=4, float angle=0)
            {
                for(int i=0;i<sides;i++)
                {
                    float a=angle+i*Mathf.PI*2/sides,b=angle+(i+1)*Mathf.PI*2/sides;
                    Line(new Vector2(.5f+Mathf.Cos(a)*radius,.5f+Mathf.Sin(a)*radius),new Vector2(.5f+Mathf.Cos(b)*radius,.5f+Mathf.Sin(b)*radius),width,tint);
                }
            }
            void Disc(float x,float y,float radius,Color tint)
            {
                var points=new Vector2[24]; for(int i=0;i<24;i++){float a=i*Mathf.PI/12;points[i]=new Vector2(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius);} Poly(tint,points);
            }
            void Box(float x,float y,float w,float h,Color tint) => Poly(tint,new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h));
            void Person(float x,float y,float s)
            {
                Disc(x,y+s*.3f,s*.16f,color);
                Poly(color,new Vector2(x-s*.28f,y-s*.3f),new Vector2(x+s*.28f,y-s*.3f),new Vector2(x+s*.23f,y),new Vector2(x+s*.1f,y+s*.08f),new Vector2(x-s*.1f,y+s*.08f),new Vector2(x-s*.23f,y));
            }
            switch(shape)
            {
                case Shape.Rule:
                    // Geometry remains visible at fractional CanvasScaler ratios; no texture sampling.
                    Box(0,0,1,1,color); break;
                case Shape.Diamond:
                    Poly(new Color(.035f,.06f,.055f,color.a*.88f),new Vector2(.5f,.98f),new Vector2(.98f,.5f),new Vector2(.5f,.02f),new Vector2(.02f,.5f));
                    Ring(.47f,.016f,color);Ring(.425f,.008f,new Color(color.r,color.g,color.b,color.a*.5f)); break;
                case Shape.DiamondGlow:
                    // Light occupies only a thin band around the diamond perimeter, never a radial disk.
                    Ring(.45f,.10f,new Color(color.r,color.g,color.b,color.a*.07f));
                    Ring(.45f,.052f,new Color(color.r,color.g,color.b,color.a*.17f));
                    Ring(.45f,.018f,new Color(color.r,color.g,color.b,color.a*.8f)); break;
                case Shape.ButtonGlow:
                    var outline=new[]{new Vector2(0,.5f),new Vector2(.07f,.98f),new Vector2(.965f,.98f),new Vector2(1,.5f),new Vector2(.965f,.02f),new Vector2(.07f,.02f)};
                    foreach(float thickness in new[]{6f,3f,1f}) for(int i=0;i<outline.Length;i++)
                    {
                        var a=P(outline[i].x,outline[i].y);var b=P(outline[(i+1)%outline.Length].x,outline[(i+1)%outline.Length].y);
                        var n=new Vector2(-(b-a).y,(b-a).x).normalized*thickness*.5f;int first=vh.currentVertCount;
                        var tint=new Color(color.r,color.g,color.b,color.a*(thickness>4?.06f:thickness>1?.16f:.8f));
                        vh.AddVert(a+n,tint,Vector2.zero);vh.AddVert(b+n,tint,Vector2.zero);vh.AddVert(b-n,tint,Vector2.zero);vh.AddVert(a-n,tint,Vector2.zero);vh.AddTriangle(first,first+1,first+2);vh.AddTriangle(first,first+2,first+3);
                    }break;
                case Shape.People: Person(.23f,.48f,.66f);Person(.77f,.48f,.66f);Person(.5f,.45f,.92f); break;
                case Shape.PersonPlus: Person(.36f,.48f,.95f);Box(.68f,.43f,.29f,.085f,color);Box(.785f,.325f,.08f,.29f,color);break;
                case Shape.Gear:
                    // Twelve teeth, an open center and a solid annulus.
                    Ring(.28f,.14f,color,32);
                    for(int i=0;i<12;i++){float a=i*Mathf.PI/6;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Line(new Vector2(.5f,.5f)+d*.30f,new Vector2(.5f,.5f)+d*.46f,.11f,color);}break;
                case Shape.Door:
                    Box(.19f,.12f,.10f,.78f,color);Box(.19f,.82f,.6f,.08f,color);Box(.71f,.12f,.08f,.78f,color);
                    Poly(color,new Vector2(.30f,.15f),new Vector2(.61f,.04f),new Vector2(.61f,.79f),new Vector2(.30f,.79f));
                    Disc(.55f,.43f,.025f,new Color(.08f,.10f,.09f,color.a));break;
                case Shape.Target: Ring(.43f,.055f,color);Ring(.25f,.045f,color);Ring(.07f,.04f,color);break;
            }
        }
    }
}
