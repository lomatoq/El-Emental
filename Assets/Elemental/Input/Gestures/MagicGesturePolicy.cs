using Elemental.Simulation.Fields;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Materials;

namespace Elemental.Input.Gestures
{
    public static class MagicGesturePolicy
    {
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
