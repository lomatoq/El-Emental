using Elemental.Input.Gestures;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Networking;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Online
{
    public static class OnlineAbilityViewCodec
    {
        public static OnlinePacket Capture(MagicInputController input, MagicExecutor executor, uint actor, uint tick, uint heldBody)
        {
            var packet = new OnlinePacket { Kind = OnlineMessage.RegionState, Id = actor, Tick = tick,
                BuildHash = heldBody, Seed = input.SelectedAbility.Value,
                Aux = (uint)input.CurrentBendPhase | ((uint)input.BendOriginMode << 8) | ((uint)input.ActiveActionOwner << 16),
                Flags = (executor.IsVectorFieldActive ? 1u : 0) | (executor.IsGravityWellActive ? 2u : 0) |
                    (executor.IsRepairActive ? 4u : 0) | (input.IsArmorActive ? 8u : 0) | (input.IsQuickStonePrimed ? 16u : 0),
                A = input.BendTargetPosition, B = executor.GravityWellFocus, C = executor.VectorFieldPoint, D = executor.VectorFieldDirection,
                Value = input.BendAmount01, Value2 = input.BendCharge01, Value3 = input.BendFocus01,
                Value4 = executor.GravityWellStrength, Value5 = executor.VectorFieldCharge };
            packet.Path.Add(new float3(input.ArmorPhase01, input.QuickStonePrime01, input.ResonanceCharge01));
            packet.Path.Add(new float3(input.SurfSpeed, 0, 0)); return packet;
        }
        public static bool Decode(in OnlinePacket packet, out EarthOnlineAbilityView view)
        {
            view = default;
            if (packet.Kind != OnlineMessage.RegionState || packet.Path.Length != 2 || packet.Flags > 31 || packet.BuildHash > uint.MaxValue ||
                packet.Seed > ushort.MaxValue || (packet.Aux & 255) > (uint)BendPhase.Cancelled ||
                ((packet.Aux >> 8) & 255) > (uint)BendOriginMode.Self || (packet.Aux >> 16) > (uint)EarthActionOwner.DualMouseEarth) return false;
            view = new EarthOnlineAbilityView { Ability = packet.Seed, Phase = (BendPhase)(packet.Aux & 255),
                Origin = (BendOriginMode)((packet.Aux >> 8) & 255), Owner = (EarthActionOwner)(packet.Aux >> 16),
                VectorActive = (packet.Flags & 1) != 0, GravityActive = (packet.Flags & 2) != 0,
                RepairActive = (packet.Flags & 4) != 0, ArmorActive = (packet.Flags & 8) != 0, QuickPrimed = (packet.Flags & 16) != 0,
                Target = (float3)packet.A, GravityFocus = (float3)packet.B, VectorPoint = (float3)packet.C, VectorDirection = (float3)packet.D,
                Amount = packet.Value, Charge = packet.Value2, Focus = packet.Value3, GravityStrength = packet.Value4, VectorCharge = packet.Value5,
                ArmorPhase = packet.Path[0].x, QuickPrime = packet.Path[0].y, ResonanceCharge = packet.Path[0].z, SurfSpeed = packet.Path[1].x };
            return true;
        }
    }
}
