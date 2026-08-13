using Elemental.Simulation.Magic;
using Unity.Mathematics;

namespace Elemental.Simulation.Fields
{
    public static class AirAbilityIds
    {
        public static readonly AbilityId GustCorridor = new AbilityId(101);
        public static readonly AbilityId Vortex = new AbilityId(102);
        public static readonly AbilityId LiftColumn = new AbilityId(103);
        public static readonly AbilityId AirBrake = new AbilityId(104);
    }

    public static class AirAbilityBuilder
    {
        public static bool TryBuild(in MagicCommand command, FieldRegionId regionId, out FieldRegion region)
        {
            float intensity = math.lerp(0.5f, 1.5f, command.Intensity);
            float3 anchor = command.Path.Length > 0 ? command.Path[0] : command.Origin;
            if (command.Ability == AirAbilityIds.GustCorridor)
            {
                float3 end = command.Path.Length > 1
                    ? command.Path[command.Path.Length - 1]
                    : anchor + (command.Aim * 8f);
                float3 delta = end - anchor;
                region = new FieldRegion(
                    regionId, command.CasterId, AirFieldKind.GustCorridor, anchor,
                    math.normalizesafe(delta, command.Aim), 2.5f, math.max(2f, math.length(delta)),
                    16f * intensity, 0.7f, 4f, 180);
                return true;
            }

            if (command.Ability == AirAbilityIds.Vortex)
            {
                region = new FieldRegion(
                    regionId, command.CasterId, AirFieldKind.Vortex, anchor, command.Aim,
                    5f, 8f, 14f * intensity, -0.6f, 5f, 160);
                return true;
            }

            if (command.Ability == AirAbilityIds.LiftColumn)
            {
                region = new FieldRegion(
                    regionId, command.CasterId, AirFieldKind.LiftColumn, anchor, command.Aim,
                    3f, 10f, 18f * intensity, 0.8f, 5f, 190);
                return true;
            }

            if (command.Ability == AirAbilityIds.AirBrake)
            {
                region = new FieldRegion(
                    regionId, command.CasterId, AirFieldKind.AirBrake, anchor, command.Aim,
                    5f, 0f, 7f * intensity, 0.4f, 3f, 220);
                return true;
            }

            region = default;
            return false;
        }
    }
}
