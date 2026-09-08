using Elemental.Simulation.Combat;
using Unity.Mathematics;

namespace Elemental.Simulation.Networking
{
    public readonly struct EarthResolvedImpactSnapshot
    {
        public EarthResolvedImpactSnapshot(in EarthWorldResponseEvent response, float reactionVelocity,
            float stunSeconds, float3 rootVelocityChange)
        { Response = response; ReactionVelocity = reactionVelocity; StunSeconds = stunSeconds; RootVelocityChange = rootVelocityChange; }
        public EarthWorldResponseEvent Response { get; }
        public float ReactionVelocity { get; }
        public float StunSeconds { get; }
        public float3 RootVelocityChange { get; }
    }
}
