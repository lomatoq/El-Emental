using Elemental.Simulation.Combat;

namespace Elemental.Simulation.Characters
{
    public enum EarthImpactPresentationOwner : byte
    {
        None = 0,
        ProceduralAngularSpring = 1,
        FullRagdoll = 2
    }

    public static class EarthImpactPresentationOwnership
    {
        public static EarthImpactPresentationOwner Resolve(
            EarthCharacterImpactResponse response) => response switch
        {
            EarthCharacterImpactResponse.Flinch or EarthCharacterImpactResponse.Stagger =>
                EarthImpactPresentationOwner.ProceduralAngularSpring,
            EarthCharacterImpactResponse.RecoverableKnockdown or EarthCharacterImpactResponse.Knockout =>
                EarthImpactPresentationOwner.FullRagdoll,
            _ => EarthImpactPresentationOwner.None
        };
    }
}
