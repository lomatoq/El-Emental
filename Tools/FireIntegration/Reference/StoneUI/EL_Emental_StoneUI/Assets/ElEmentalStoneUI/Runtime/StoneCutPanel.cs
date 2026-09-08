using UnityEngine;
using UnityEngine.UIElements;

namespace ElEmental.StoneUI
{
    /// <summary>Resizable cut-stone panel. Geometry preserves the diagonal angle, not a stretched bevel.</summary>
    public sealed class StoneCutPanel : VisualElement
    {
        private Texture2D _texture;
        private float _angle=12.5f;
        private Color _rim=new Color(.80f,.72f,.49f,1);
        public Texture2D Texture { get=>_texture;set{_texture=value;MarkDirtyRepaint();} }
        public float AngleDegrees { get=>_angle;set{_angle=Mathf.Clamp(value,0,24);MarkDirtyRepaint();} }
        public Color Rim { get=>_rim;set{_rim=value;MarkDirtyRepaint();} }
        public StoneCutPanel(){pickingMode=PickingMode.Ignore;generateVisualContent+=Draw;}
        private void Draw(MeshGenerationContext context)
        {
            Rect r=contentRect;float w=r.width,h=r.height;if(w<4||h<4)return;
            float cut=Mathf.Min(24,Mathf.Min(w,h)*.08f);
            float slope=Mathf.Min(w*.42f,Mathf.Tan(_angle*Mathf.Deg2Rad)*h);
            Vector2[] p={new Vector2(cut,1),new Vector2(w-slope-1,1),new Vector2(w-1,h-cut),new Vector2(w-cut,h-1),new Vector2(1,h-1),new Vector2(1,cut)};
            MeshWriteData data=context.Allocate(p.Length,12,_texture);
            Rect uv=data.uvRegion;
            for(int i=0;i<p.Length;i++)data.SetNextVertex(new Vertex{position=new Vector3(r.x+p[i].x,r.y+p[i].y,Vertex.nearZ),tint=Color.white,uv=new Vector2(uv.xMin+p[i].x/w*uv.width,uv.yMin+(1-p[i].y/h)*uv.height)});
            for(ushort i=1;i<5;i++){data.SetNextIndex(0);data.SetNextIndex(i);data.SetNextIndex((ushort)(i+1));}
            var painter=context.painter2D;painter.strokeColor=_rim;painter.lineWidth=1.5f;
            painter.BeginPath();painter.MoveTo(p[0]+r.position);for(int i=1;i<p.Length;i++)painter.LineTo(p[i]+r.position);painter.ClosePath();painter.Stroke();
        }
    }
}
