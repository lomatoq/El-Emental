using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    public readonly struct FireTongueShape
    {
        public readonly float RadiusScale,AspectScale,Roll,TemperatureScale,SmokeScale;
        public readonly float2 Bend;
        public FireTongueShape(float radius,float aspect,float roll,float temperature,float smoke,float2 bend)
        {RadiusScale=radius;AspectScale=aspect;Roll=roll;TemperatureScale=temperature;SmokeScale=smoke;Bend=bend;}
    }
    // Stable identity and absolute particle age: no frame randomness or cumulative scale/rotation drift.
    public static class FireTongueEvolution
    {
        public static FireTongueShape Evaluate(uint id,float phase,float age,float hotLifetime,float temperature)
        {
            age=math.max(0,math.isfinite(age)?age:0);phase=math.isfinite(phase)?phase:0;
            float seed=math.frac(id*.61803398875f+phase*.137f),seed2=math.frac(seed*7.731f+.217f);
            float life=math.max(.06f,math.isfinite(hotLifetime)?hotLifetime:.3f),t=age/life;
            float cooling=math.smoothstep(.55f,1.35f,t),evolve=math.smoothstep(0,1,t);
            float slow=age*math.lerp(1.15f,2.1f,seed2),wave=math.sin(phase+slow);
            float radius=math.lerp(.84f,1.12f,seed)*(1+.13f*wave)*math.lerp(1, .78f,cooling);
            float aspect=math.lerp(.7f,1.13f,seed2)*math.lerp(1.08f,.68f,evolve)*math.lerp(1,.9f,cooling);
            float roll=phase+(seed2-.5f)*age*2.1f+.28f*(math.sin(phase+slow)-math.sin(phase));
            float heat=math.lerp(.83f,1,seed2)*(1-.08f*evolve);
            float smoke=math.lerp(.64f,.91f,seed)*(1+.16f*math.sin(phase+age*1.3f));
            float2 bend=new float2(math.sin(phase+slow),math.cos(phase*.73f-slow*.77f))*math.lerp(.12f,.35f,evolve);
            return new FireTongueShape(radius,aspect,roll,heat,smoke,bend);
        }
        public static bool OutsideActor(float3 position,float radius,float aspect,float3 center,float fieldRadius)
            =>math.length(position-center)-radius*math.max(1,aspect)>=fieldRadius*.68f;
    }
}
