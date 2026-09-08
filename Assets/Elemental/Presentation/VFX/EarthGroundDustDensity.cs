using UnityEngine;

namespace Elemental.Presentation.VFX
{
    /// <summary>Bounded local cosmetic weighting; no physics or gameplay writes.</summary>
    public static class EarthGroundDustDensity
    {
        public static float Evaluate(Vector3[] bases,int count,Vector3 up,float radius,float isolatedWeight,
            int saturatedNeighbours,int[] neighbours,int[] nearest,float[] weights,out float meanNeighbours)
        {
            count=Mathf.Clamp(count,0,Mathf.Min(96,Mathf.Min(bases.Length,Mathf.Min(neighbours.Length,Mathf.Min(nearest.Length,weights.Length)))));
            radius=Mathf.Clamp(radius,.5f,8f);up=up.sqrMagnitude>.5f?up.normalized:Vector3.up;
            float total=0;int sum=0;
            for(int i=0;i<count;i++)
            {
                neighbours[i]=0;nearest[i]=-1;float best=radius*radius;
                for(int j=0;j<count;j++)
                {
                    if(i==j)continue;var delta=bases[j]-bases[i];float height=Vector3.Dot(delta,up);
                    float d=(delta-up*height).sqrMagnitude;
                    if(Mathf.Abs(height)>1.5f||d>radius*radius)continue;
                    neighbours[i]++;if(d<best){best=d;nearest[i]=j;}
                }
                weights[i]=Mathf.Lerp(Mathf.Clamp(isolatedWeight,.05f,1),1,Mathf.Clamp01(neighbours[i]/(float)Mathf.Max(1,saturatedNeighbours)));
                total+=weights[i];sum+=neighbours[i];
            }
            meanNeighbours=count>0?sum/(float)count:0;return total;
        }
        public static int Select(float[] weights,int count,float unitSample)
        {
            count=Mathf.Clamp(count,0,Mathf.Min(96,weights.Length));float total=0;
            for(int i=0;i<count;i++)total+=Mathf.Max(0,weights[i]);
            if(total<=0)return -1;float target=Mathf.Clamp01(unitSample)*total;
            for(int i=0;i<count;i++){target-=Mathf.Max(0,weights[i]);if(target<=0&&weights[i]>0)return i;}
            return count-1;
        }
    }
}
