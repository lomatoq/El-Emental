using Elemental.Simulation.Fields;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Materials;

namespace Elemental.Input.Gestures
{
    public static class MagicGesturePolicy
    {
        /// <summary>
        /// Returns the first playable command ability for a configured elemental
        /// runtime. This is also the selection restored when a saved scene only
        /// serialized its element: MagicInputController's selected ability is
        /// intentionally runtime state and is not serialized.
        /// </summary>
        public static AbilityId DefaultAbility(ElementId element) => element switch
        {
            ElementId.Earth => EarthAbilityIds.LineWall,
            ElementId.Air => AirAbilityIds.GustCorridor,
            ElementId.Fire => FireAbilityIds.HeatJet,
            ElementId.Water => WaterAbilityIds.GatherWater,
            _ => default
        };

        public static bool BelongsToElement(ElementId element, AbilityId ability) => element switch
        {
            ElementId.Earth =>
                ability == EarthAbilityIds.LineWall ||
                ability == EarthAbilityIds.PullRock ||
                ability == EarthAbilityIds.FlickThrow ||
                ability == EarthAbilityIds.RaisePlatform ||
                ability == EarthAbilityIds.VectorFieldPush ||
                ability == EarthAbilityIds.LandingCushion,
            ElementId.Air =>
                ability == AirAbilityIds.GustCorridor ||
                ability == AirAbilityIds.Vortex ||
                ability == AirAbilityIds.LiftColumn ||
                ability == AirAbilityIds.AirBrake,
            ElementId.Fire =>
                ability == FireAbilityIds.HeatJet ||
                ability == FireAbilityIds.ThermalFocus,
            ElementId.Water =>
                ability == WaterAbilityIds.GatherWater ||
                ability == WaterAbilityIds.WaterJet ||
                ability == WaterAbilityIds.FreezeBridge ||
                ability == WaterAbilityIds.SteamBurst,
            _ => false
        };

        public static AbilityId NormalizeSelection(ElementId element, AbilityId current)
        {
            if (BelongsToElement(element, current)) return current;
            AbilityId fallback = DefaultAbility(element);
            return fallback.IsValid ? fallback : current;
        }

        public static bool Matches(GestureKind gesture, AbilityId ability)
        {
            if (gesture == GestureKind.Invalid) return false;

            if (ability == EarthAbilityIds.LineWall)
                return gesture == GestureKind.Line || gesture == GestureKind.Flick;
            if (ability == EarthAbilityIds.PullRock)
                return gesture == GestureKind.Pull || gesture == GestureKind.Line || gesture == GestureKind.Flick;
            if (ability == EarthAbilityIds.FlickThrow)
                return gesture == GestureKind.Flick;

            if (ability == AirAbilityIds.GustCorridor)
                return gesture == GestureKind.Line || gesture == GestureKind.Flick;
            if (ability == AirAbilityIds.Vortex || ability == AirAbilityIds.LiftColumn)
                return gesture == GestureKind.Pull || gesture == GestureKind.Line;
            if (ability == AirAbilityIds.AirBrake)
                return true;

            return ability == FireAbilityIds.HeatJet || ability == FireAbilityIds.ThermalFocus ||
                   ability == WaterAbilityIds.GatherWater || ability == WaterAbilityIds.WaterJet ||
                   ability == WaterAbilityIds.FreezeBridge || ability == WaterAbilityIds.SteamBurst;
        }
    }
}
