using System;
using System.Collections.Generic;
using Elemental.Core.Math;

namespace Elemental.Simulation.Networking
{
    public readonly struct TransportProfile
    {
        public TransportProfile(int oneWayLatencyTicks, int jitterTicks, float packetLoss01, int maximumQueuedPackets)
        {
            OneWayLatencyTicks = Math.Max(0, oneWayLatencyTicks); JitterTicks = Math.Max(0, jitterTicks);
            PacketLoss01 = Math.Clamp(packetLoss01, 0f, 0.95f); MaximumQueuedPackets = Math.Max(1, maximumQueuedPackets);
        }
        public int OneWayLatencyTicks { get; }
        public int JitterTicks { get; }
        public float PacketLoss01 { get; }
        public int MaximumQueuedPackets { get; }
    }

    public readonly struct TransportPacket<T>
    {
        public TransportPacket(NetworkPeerId from, NetworkPeerId to, uint sentTick, uint deliveryTick, uint sequence, T payload)
        {
            From = from; To = to; SentTick = sentTick; DeliveryTick = deliveryTick; Sequence = sequence; Payload = payload;
        }
        public NetworkPeerId From { get; }
        public NetworkPeerId To { get; }
        public uint SentTick { get; }
        public uint DeliveryTick { get; }
        public uint Sequence { get; }
        public T Payload { get; }
    }

    public sealed class SimulatedTransport<T>
    {
        private readonly TransportProfile _profile;
        private readonly List<TransportPacket<T>> _queued;
        private DeterministicRandom _random;
        private uint _sequence;

        public SimulatedTransport(in TransportProfile profile, uint seed)
        {
            _profile = profile; _queued = new List<TransportPacket<T>>(profile.MaximumQueuedPackets);
            _random = new DeterministicRandom(seed);
        }
        public int QueuedCount => _queued.Count;
        public int DroppedCount { get; private set; }
        public int QueueOverflowCount { get; private set; }

        public bool Send(NetworkPeerId from, NetworkPeerId to, uint tick, in T payload)
        {
            if (_random.NextFloat01() < _profile.PacketLoss01)
            { DroppedCount++; return false; }
            if (_queued.Count >= _profile.MaximumQueuedPackets)
            { QueueOverflowCount++; return false; }
            int jitter = _profile.JitterTicks == 0 ? 0 : (int)(_random.NextUInt() % (uint)(_profile.JitterTicks * 2 + 1)) - _profile.JitterTicks;
            uint delivery = tick + (uint)Math.Max(0, _profile.OneWayLatencyTicks + jitter);
            TransportPacket<T> packet = new TransportPacket<T>(from, to, tick, delivery, _sequence++, payload);
            int insertion = _queued.Count;
            while (insertion > 0 && (_queued[insertion - 1].DeliveryTick > packet.DeliveryTick ||
                (_queued[insertion - 1].DeliveryTick == packet.DeliveryTick && _queued[insertion - 1].Sequence > packet.Sequence))) insertion--;
            _queued.Insert(insertion, packet);
            return true;
        }

        public int Receive(uint tick, List<TransportPacket<T>> output, int budget)
        {
            int received = 0;
            while (received < budget && _queued.Count > 0 && _queued[0].DeliveryTick <= tick)
            {
                output.Add(_queued[0]); _queued.RemoveAt(0); received++;
            }
            return received;
        }
    }
}
