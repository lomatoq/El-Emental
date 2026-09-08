using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Elemental.Presentation.UI
{
    public sealed class UiChromaticEdges : BaseMeshEffect
    {
        public MenuScreenLayout Settings;
        private readonly List<UIVertex> original=new List<UIVertex>();
        public override void ModifyMesh(VertexHelper helper)
        {
            if(!IsActive()||Settings==null||Settings.chromaticPixels<=0)return;
            original.Clear();helper.GetUIVertexStream(original);helper.Clear();
            for(int side=0;side<2;side++)
            {
                Color tint=side==0?Settings.negativeFringe:Settings.positiveFringe;
                for(int i=0;i<original.Count;i+=3)
                {
                    int start=helper.currentVertCount;
                    for(int j=0;j<3;j++)
                    {
                        var v=original[i+j];v.position.x+=(side==0?-1:1)*Settings.chromaticPixels;
                        tint.a=((Color)v.color).a*Settings.chromaticOpacity;v.color=tint;helper.AddVert(v);
                    }
                    helper.AddTriangle(start,start+1,start+2);
                }
            }
            for(int i=0;i<original.Count;i+=3)
            {int start=helper.currentVertCount;helper.AddVert(original[i]);helper.AddVert(original[i+1]);helper.AddVert(original[i+2]);helper.AddTriangle(start,start+1,start+2);}
        }
        private int revision=-1;
        private void LateUpdate(){if(Settings!=null&&Settings.Revision!=revision){revision=Settings.Revision;graphic.SetVerticesDirty();}}
    }
}
