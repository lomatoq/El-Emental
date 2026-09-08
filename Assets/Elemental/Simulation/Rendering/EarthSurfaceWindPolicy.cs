using Unity.Mathematics;
namespace Elemental.Simulation.Rendering
{
    public static class EarthSurfaceWindPolicy
    {
        /// <summary>Compact stone wake: tangent curl fades continuously outside its radius.</summary>
        public static float3 StoneWakeVelocity(float3 velocity, float3 normal, float3 fromStone,
            float radius, float strength, float timeSeconds)
        {
            normal=math.normalizesafe(normal,new float3(0,1,0));
            float3 radial=fromStone-normal*math.dot(fromStone,normal);
            float distance=math.length(radial);
            float falloff=math.saturate(1f-distance/math.max(.1f,radius));
            float3 curl=math.cross(normal,radial)/math.max(.35f,distance);
            float pulse=.85f+.15f*math.sin(timeSeconds*2.1f);
            float3 result=velocity+curl*(math.clamp(strength,0,2)*falloff*falloff*pulse);
            result-=normal*math.dot(result,normal);
            return result*math.min(1f,4f/math.max(.001f,math.length(result)));
        }
        /// <summary>One continuous cosmetic wind field; no particle identity enters its phase.</summary>
        public static float3 GustVelocity(float3 wind, float3 normal, float3 positionFromAnchor,
            float timeSeconds, float speed, float gustStrength, float gustPeriodSeconds,
            float gustWavelengthMetres, float turbulenceStrength)
        {
            normal = math.normalizesafe(normal, new float3(0, 1, 0));
            float3 forward = TangentVelocity(wind, normal, 1f);
            float3 side = math.cross(normal, forward);
            float3 propagation = math.normalizesafe(wind, new float3(1, 0, 0));
            float phase = 2f * math.PI * (timeSeconds / math.max(2f, gustPeriodSeconds) -
                math.dot(positionFromAnchor, propagation) / math.max(4f, gustWavelengthMetres));
            float strength = math.clamp(gustStrength, 0f, .4f);
            float gust = 1f + strength * math.sin(phase);
            // Gentle shared meander plus smaller local ripples. Both remain tangent to support.
            float bend = math.clamp(turbulenceStrength, 0f, .2f) *
                (.7f * math.sin(phase * .73f + 1.1f) +
                 .3f * math.sin(math.dot(positionFromAnchor, side) * .8f - timeSeconds * .9f));
            return TangentVelocity(forward + side * bend, normal, speed * gust);
        }

        public static float3 TangentVelocity(float3 wind, float3 normal, float speed)
        {
            normal = math.normalizesafe(normal, new float3(0, 1, 0));
            float3 tangent = wind - normal * math.dot(wind, normal);
            float3 alternate = math.cross(normal, math.abs(normal.y) < .9f ? new float3(0,1,0) : new float3(1,0,0));
            return math.normalizesafe(tangent, math.normalizesafe(alternate)) * (math.isfinite(speed) ? math.clamp(speed, 0f, 4f) : 0f);
        }
    }
}
