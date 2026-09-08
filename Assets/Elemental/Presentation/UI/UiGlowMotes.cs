using UnityEngine;
using UnityEngine.UI;
namespace Elemental.Presentation.UI
{
    public sealed class UiGlowMotes : MaskableGraphic
    {
        public MenuScreenLayout Settings;
        public bool ReducedMotion;
        private float clock;
        private int frame=-1;
        private int revision=-1;
        private void Update()
        {
            if(!ReducedMotion)clock+=Time.unscaledDeltaTime;
            int next=ReducedMotion?0:Mathf.FloorToInt(clock*30);
            int version=Settings!=null?Settings.Revision:0;
            if(next!=frame||version!=revision){frame=next;revision=version;SetVerticesDirty();}
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(Settings==null)return;
            var r=rectTransform.rect;
            for(int i=0;i<Settings.particleCount;i++)
            {
                float phase=i*2.39996f,age=Mathf.Repeat(clock*(.15f+(i%4)*.024f)+i*.618f,1);
                float a=Mathf.Sin(age*Mathf.PI)*Settings.glowStrength;
                Vector2 p=r.center+new Vector2(Mathf.Sin(phase)*(24+age*50),age*Settings.particleTravel-18);
                float size=Mathf.Lerp(Settings.particleSize.x,Settings.particleSize.y,(i%7)/6f)*(1-age*.5f);
                Disc(vh,p,size*4,new Color(color.r,color.g,color.b,a*.09f));
                Disc(vh,p,size*2,new Color(color.r,color.g,color.b,a*.17f));
                Disc(vh,p,size,new Color(color.r,color.g,color.b,a*.8f));
            }
        }
        private static void Disc(VertexHelper vh,Vector2 p,float radius,Color c)
        {
            int start=vh.currentVertCount;vh.AddVert(p,c,Vector2.zero);
            for(int i=0;i<=8;i++){float a=i*Mathf.PI*.25f;vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,c,Vector2.zero);}
            for(int i=0;i<8;i++)vh.AddTriangle(start,start+i+1,start+i+2);
        }
    }
}
