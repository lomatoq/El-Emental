using System.Collections.Generic;
using Elemental.Simulation.Magic;
using Unity.Mathematics;

namespace Elemental.Simulation.Networking
{
    public sealed class OnlineSpikeHarness
    {
        private readonly CommandAuthority _authority = new CommandAuthority();
        private readonly SimulatedTransport<MagicCommand> _uplink;
        private readonly List<TransportPacket<MagicCommand>> _received = new List<TransportPacket<MagicCommand>>(64);
        private readonly int _clients;

        public OnlineSpikeHarness(int clients, in TransportProfile profile, uint seed)
        {
            _clients = math.clamp(clients, 2, 4);
            _uplink = new SimulatedTransport<MagicCommand>(in profile, seed);
        }
        public CommandAuthority Authority => _authority;
        public int ClientCount => _clients;
        public int DroppedCount => _uplink.DroppedCount;
        public int QueueDebt => _uplink.QueuedCount;
        public int SubmittedCount { get; private set; }

        public void Tick(uint tick)
        {
            for (byte client = 1; client <= _clients; client++)
            {
                if (tick % (uint)(5 + client) != 0) continue;
                AbilityId ability = client % 2 == 0 ? new AbilityId(101) : new AbilityId(1);
                ElementId element = client % 2 == 0 ? ElementId.Air : ElementId.Earth;
                MagicCommand command = new MagicCommand(
                    tick, client, element, ability,
                    new float3(client, 25f, tick * 0.01f), new float3(1f, 0f, 0f), null,
                    0.75f, 0u, tick * 2654435761u + client);
                if (_uplink.Send(new NetworkPeerId(client), new NetworkPeerId(255), tick, in command)) SubmittedCount++;
            }
            _received.Clear();
            _uplink.Receive(tick, _received, 32);
            for (int index = 0; index < _received.Count; index++)
            {
                TransportPacket<MagicCommand> packet = _received[index];
                MagicCommand command = packet.Payload;
                _authority.Submit(packet.From, in command, tick);
            }
        }
    }
}
