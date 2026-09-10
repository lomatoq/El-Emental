using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    public readonly struct EarthStoneSweepResult
    {
        public EarthStoneSweepResult(float impulse,float3 sourceVelocity,float3 targetVelocity)
        { Accepted=true;Impulse=impulse;SourceVelocity=sourceVelocity;TargetVelocity=targetVelocity; }
        public bool Accepted { get; }
        public float Impulse { get; }
        public float3 SourceVelocity { get; }
        public float3 TargetVelocity { get; }
    }
    /// <summary>Fallback linear normal collision; finite pairs exchange equal/opposite momentum.</summary>
    public static class EarthStoneSweepResponse
    {
        public static EarthStoneSweepResult Resolve(float3 sourceVelocity,float3 targetVelocity,
            float sourceMass,float targetMass,bool targetDynamic,float3 contactNormal,float restitution)
        {
            if(!math.all(math.isfinite(sourceVelocity))||!math.all(math.isfinite(targetVelocity))||
                !math.all(math.isfinite(contactNormal))||math.lengthsq(contactNormal)<.5f||
                !math.isfinite(sourceMass)||sourceMass<=0f||
                targetDynamic&&(!math.isfinite(targetMass)||targetMass<=0f)||!math.isfinite(restitution)) return default;
            float3 normal=math.normalize(contactNormal);
            float closing=math.dot(sourceVelocity-targetVelocity,normal);
            if(closing>=-.01f)return default;
            float inverseSource=1f/sourceMass,inverseTarget=targetDynamic?1f/targetMass:0f;
            float impulse=-(1f+math.clamp(restitution,0f,1f))*closing/(inverseSource+inverseTarget);
            return new EarthStoneSweepResult(impulse,sourceVelocity+normal*(impulse*inverseSource),
                targetVelocity-normal*(impulse*inverseTarget));
        }
    }
}
