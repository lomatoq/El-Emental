using Unity.Mathematics;
namespace Elemental.Simulation.Rendering
{
    public static class EarthDustSupportPolicy
    {
        public const int MaximumQueriesPerFrame=12;
        public const int MaximumRefreshQueries=8;
        public static float Priority(float age,bool visible,float edgeRisk,float distanceSquared)
        {
            float safeAge=math.isfinite(age)?math.max(0,age):1;
            return safeAge*8f+(visible?.2f:0)+math.saturate(edgeRisk)*.25f+
                .1f/(1f+math.max(0,distanceSquared)*.01f);
        }
        public static int Select(float[] ages,float[] priorities,bool[] selected,int count,bool oldestOnly)
        {
            int result=-1;float best=float.NegativeInfinity;
            for(int i=0;i<count;i++)
            {
                if(selected[i] || priorities[i]<0)continue;
                float score=oldestOnly?ages[i]:priorities[i];
                if(score>best){best=score;result=i;}
            }
            return result;
        }
        public static float Confidence(float age) => 1f-math.saturate((age-.3f)/.25f);
    }
}
