using System;
using System.Collections;
using System.Collections.Generic;
using Elemental.Simulation.Magic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>Reusable path view avoids allocating arrays while preserving the canonical command constructor.</summary>
    public sealed class OnlineCommandCodec
    {
        private sealed class PathView : IReadOnlyList<float3>
        {
            public FixedList512Bytes<float3> Points;
            public int Count => Points.Length;
            public float3 this[int index] => Points[index];
            public IEnumerator<float3> GetEnumerator() => throw new NotSupportedException("Indexed path only.");
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        private readonly PathView _path = new PathView();
        public static OnlinePacket Encode(in MagicCommand command) => new OnlinePacket
        {
            Kind = OnlineMessage.MagicCommand, Id = command.CasterId, Tick = command.Tick,
            Aux = command.Ability.Value | ((uint)command.Element << 16),
            A = (Vector3)command.Origin, B = (Vector3)command.Aim, Value = command.Intensity,
            Flags = command.Modifiers, Seed = command.Seed, Path = command.Path
        };
        public bool TryDecode(in OnlinePacket packet, byte owner, uint hostTick, out MagicCommand command)
        {
            command = default;
            if (packet.Kind != OnlineMessage.MagicCommand || packet.Id != owner || owner < 1 || owner > 2 ||
                (long)packet.Tick - hostTick > 12 || (long)hostTick - packet.Tick > 180 ||
                packet.Aux >> 16 != (uint)ElementId.Earth || (packet.Aux & 65535) < 1 ||
                (packet.Aux & 65535) > 6 || packet.Value < 0 || packet.Value > 1 ||
                packet.Path.Length > 32 || !math.isfinite(packet.Value) ||
                !math.all(math.isfinite((float3)packet.A)) || !math.all(math.isfinite((float3)packet.B)) ||
                packet.B.sqrMagnitude < .5f || packet.B.sqrMagnitude > 1.5f) return false;
            for (int i = 0; i < packet.Path.Length; i++)
                if (!math.all(math.isfinite(packet.Path[i]))) return false;
            _path.Points = packet.Path;
            command = new MagicCommand(hostTick, owner, ElementId.Earth,
                new AbilityId((ushort)packet.Aux), (float3)packet.A, (float3)packet.B,
                _path, packet.Value, packet.Flags, packet.Seed);
            return true;
        }
    }
}
