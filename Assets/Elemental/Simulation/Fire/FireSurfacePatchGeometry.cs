using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    // Presentation sampling contract: positions on actual triangles, not a proxy hull.
    public static class FireSurfacePatchGeometry
    {
        public const int Capacity=16;
        public static float2 DiskOffset(int index,float radius)
        {
            if(index<=0)return float2.zero;
            float angle=index*2.39996323f;
            return new float2(math.cos(angle),math.sin(angle))*radius*math.sqrt(index/(float)(Capacity-1));
        }
        public static bool ClosestPoint(float3 p,float3 a,float3 b,float3 c,out float3 point)
        {
            point=a;float3 ab=b-a,ac=c-a;
            if(!math.all(math.isfinite(p+a+b+c))||math.lengthsq(math.cross(ab,ac))<1e-12f)return false;
            float3 ap=p-a;float d1=math.dot(ab,ap),d2=math.dot(ac,ap);
            if(d1<=0&&d2<=0)return true;
            float3 bp=p-b;float d3=math.dot(ab,bp),d4=math.dot(ac,bp);
            if(d3>=0&&d4<=d3){point=b;return true;}
            float vc=d1*d4-d3*d2;
            if(vc<=0&&d1>=0&&d3<=0){point=a+ab*(d1/(d1-d3));return true;}
            float3 cp=p-c;float d5=math.dot(ab,cp),d6=math.dot(ac,cp);
            if(d6>=0&&d5<=d6){point=c;return true;}
            float vb=d5*d2-d1*d6;
            if(vb<=0&&d2>=0&&d6<=0){point=a+ac*(d2/(d2-d6));return true;}
            float va=d3*d6-d5*d4;
            if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0){point=b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));return true;}
            float inv=1/(va+vb+vc);point=a+ab*(vb*inv)+ac*(vc*inv);return true;
        }
    }
}
