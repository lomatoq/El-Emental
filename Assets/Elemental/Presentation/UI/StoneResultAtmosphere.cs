using UnityEngine;
using UnityEngine.UIElements;
namespace Elemental.Presentation.UI
{
    // Only decorates a real round-end view. Never changes result authority or camera/game state.
    internal sealed class StoneResultAtmosphere : VisualElement
    {
        private bool _victory=true;
        private float _time;
        private int _frame=-1;
        private int _revision=-1;
        private Vector2 _lastCenter;
        public MenuScreenLayout Settings;
        public Vector2 SourceCenter;
        public float SourceRadius;
        public StoneResultAtmosphere(){generateVisualContent+=Draw;}
        public void Tick(bool victory,bool reduced,float age)
        {
            int frame=reduced?0:Mathf.FloorToInt(age*30);
            int revision=Settings!=null?Settings.Revision:0;
            if(frame==_frame&&victory==_victory&&revision==_revision&&SourceCenter==_lastCenter)return;
            _revision=revision;_lastCenter=SourceCenter;
            _victory=victory;_frame=frame;_time=reduced?0:age;MarkDirtyRepaint();
        }
        private void Draw(MeshGenerationContext context)
        {
            float w=contentRect.width,h=contentRect.height;if(w<1||h<1)return;
            // Smooth local dimming: transparent scene edges, darker center behind title and buttons.
            const int nx=24,ny=16;
            var mesh=context.Allocate((nx+1)*(ny+1),nx*ny*6);
            for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)
            {
                float u=x/(float)nx,v=y/(float)ny;
                float radius=Mathf.Sqrt(Mathf.Pow((u-.5f)/.36f,2)+Mathf.Pow((v-.57f)/.85f,2));
                float a=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,1.25f,radius)))*(_victory?.70f:.57f);
                mesh.SetNextVertex(new Vertex{position=new Vector3(u*w,v*h,Vertex.nearZ),tint=_victory?new Color(.025f,.04f,.04f,a):new Color(.09f,.022f,.012f,a)});
            }
            for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)
            {ushort i=(ushort)(y*(nx+1)+x);mesh.SetNextIndex(i);mesh.SetNextIndex((ushort)(i+1));mesh.SetNextIndex((ushort)(i+nx+1));mesh.SetNextIndex((ushort)(i+1));mesh.SetNextIndex((ushort)(i+nx+2));mesh.SetNextIndex((ushort)(i+nx+1));}
            var p=context.painter2D;
            Color accent=_victory?new Color(.82f,1,.43f):new Color(1,.43f,.20f);
            Vector2 center=SourceRadius>0?SourceCenter:new Vector2(w*.5f,h*.325f);float r=SourceRadius>0?SourceRadius:h*.105f;
            float strength=Settings!=null?Settings.glowStrength:.85f;
            for(int ring=16;ring>0;ring--)
            {
                float t=ring/16f;
                p.fillColor=new Color(accent.r,accent.g,accent.b,strength*.018f*(1-t*.75f));
                p.BeginPath();p.Arc(center,r*(.65f+t*(.85f+(Settings!=null?Settings.glowRadius:18)/60f)),0,360);p.Fill();
            }
            // Fine broken halo ornament and an edge-lit diamond, never a central gradient disk.
            p.lineWidth=1; p.strokeColor=new Color(accent.r,accent.g,accent.b,.30f);
            p.BeginPath();p.Arc(center,r*1.45f,195,345);p.Stroke();
            p.BeginPath();p.Arc(center,r*1.45f,15,165);p.Stroke();
            Diamond(p,center,r*.98f,accent,1.4f);
            int count=Settings!=null?Settings.particleCount:28;
            for(int i=0;i<count;i++)
            {
                float phase=i*2.39996f;
                float age=Mathf.Repeat(i*.618f+_time*(.14f+(i%4)*.018f),1);
                float travel=Settings!=null?Settings.particleTravel:135;
                Vector2 point=center+new Vector2(Mathf.Sin(phase)*(r*.5f+age*r),r*.35f-age*travel);
                float size=Settings!=null?Mathf.Lerp(Settings.particleSize.x,Settings.particleSize.y,(i%7)/6f):2;
                float fade=Mathf.Sin(age*Mathf.PI)*strength;
                for(int layer=3;layer>=1;layer--)
                {
                    p.fillColor=new Color(accent.r,accent.g,accent.b,fade*(layer==1?.85f:layer==2?.12f:.045f));
                    p.BeginPath();p.Arc(point,size*layer,0,360);p.Fill();
                }
            }
        }
        internal static void Diamond(Painter2D p,Vector2 c,float r,Color color,float width)
        {p.strokeColor=color;p.lineWidth=width;p.BeginPath();p.MoveTo(c+new Vector2(0,-r));p.LineTo(c+new Vector2(r,0));p.LineTo(c+new Vector2(0,r));p.LineTo(c+new Vector2(-r,0));p.ClosePath();p.Stroke();}
    }
    internal sealed class StoneResultButtonEdge : VisualElement
    {
        private Color _color;private bool _primary,_hot;
        public StoneResultButtonEdge(){generateVisualContent+=Draw;}
        public void Set(Color color,bool primary,bool hot)
        {if(color==_color&&primary==_primary&&hot==_hot)return;_color=color;_primary=primary;_hot=hot;MarkDirtyRepaint();}
        private void Draw(MeshGenerationContext context)
        {
            float w=contentRect.width,h=contentRect.height;if(w<1||h<1)return;
            var p=context.painter2D;float bevel=20;
            for(int pass=0;pass<3;pass++)
            {
                float alpha=pass==2?(_primary||_hot?.95f:.42f):(_primary||_hot?.10f:.02f);
                p.strokeColor=new Color(_color.r,_color.g,_color.b,alpha);p.lineWidth=pass==0?10:pass==1?5:1.4f;
                p.BeginPath();p.MoveTo(new Vector2(bevel,2));p.LineTo(new Vector2(w-bevel,2));p.LineTo(new Vector2(w-2,h/2));p.LineTo(new Vector2(w-bevel,h-2));p.LineTo(new Vector2(bevel,h-2));p.LineTo(new Vector2(2,h/2));p.ClosePath();p.Stroke();
            }
        }
    }
}

