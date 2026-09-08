using Elemental.Online;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class OnlineProtocolTests
    {
        [Test]
        public void ReadyRequiresBothPeersSameEpochBuildWorldAndCompleteBindings()
        {
            var barrier = new OnlineStartBarrier(); barrier.Reset(5, 99, 123);
            Assert.That(barrier.SetReady(1, 5, 99, 123, OnlineCapabilities.All, true), Is.True);
            Assert.That(barrier.CanStart, Is.False);
            Assert.That(barrier.SetReady(2, 4, 99, 123, OnlineCapabilities.All, true), Is.False);
            Assert.That(barrier.SetReady(2, 5, 98, 123, OnlineCapabilities.All, true), Is.False);
            Assert.That(barrier.SetReady(2, 5, 99, 124, OnlineCapabilities.All, true), Is.False);
            Assert.That(barrier.SetReady(2, 5, 99, 123, OnlineCapabilities.Commands, true), Is.False);
            Assert.That(barrier.CanStart, Is.False);
            Assert.That(barrier.SetReady(2, 5, 99, 123, OnlineCapabilities.All, true), Is.True);
            Assert.That(barrier.CanStart, Is.True);
            barrier.SetReady(1, 5, 99, 123, OnlineCapabilities.All, false);
            Assert.That(barrier.CanStart, Is.False);
            barrier.Disconnect(); Assert.That(barrier.CanStart, Is.False);
        }

        [Test]
        public void SequenceRejectsDuplicatesAndReorderingWithoutBlockingOtherChannels()
        {
            var window = new OnlineSequenceWindow();
            Assert.That(window.Accept(OnlineMessage.BodyState, 3), Is.True);
            Assert.That(window.Accept(OnlineMessage.BodyState, 3), Is.False);
            Assert.That(window.Accept(OnlineMessage.BodyState, 2), Is.False);
            Assert.That(window.Accept(OnlineMessage.BodySpawn, 1), Is.True);
            Assert.That(window.Accept(OnlineMessage.BodyState, 0), Is.False);
            window.Clear(); Assert.That(window.Accept(OnlineMessage.BodyState, 1), Is.True);
        }

        [Test]
        public void CanonicalCommandRoundTripsAndRetimesWithoutChangingGeometry()
        {
            float3[] path = { new float3(2, 3, 4), new float3(4, 5, 6) };
            var command = new MagicCommand(100, 2, ElementId.Earth, EarthAbilityIds.LineWall,
                new float3(1, 2, 3), new float3(0, 0, 1), path, .75f, 17, 42);
            OnlinePacket packet = OnlineCommandCodec.Encode(command);
            packet.Version = OnlinePacket.Protocol; packet.Epoch = 8; packet.Sequence = 1;
            using var writer = new FastBufferWriter(OnlinePacket.MaximumBytes, Allocator.Temp);
            writer.WriteValueSafe(packet);
            Assert.That(writer.Length, Is.LessThan(OnlinePacket.MaximumBytes));
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadValueSafe(out OnlinePacket decoded);
            Assert.That(decoded.IsFiniteAndBounded(), Is.True);
            Assert.That(new OnlineCommandCodec().TryDecode(decoded, 2, 104, out MagicCommand canonical), Is.True);
            Assert.That(canonical.Tick, Is.EqualTo(104));
            Assert.That(canonical.CasterId, Is.EqualTo(2));
            Assert.That(canonical.Path[1], Is.EqualTo(path[1]));
            Assert.That(canonical.Modifiers, Is.EqualTo(17));
            Assert.That(canonical.Seed, Is.EqualTo(42));
        }

        [TestCase(1, 100, false)]
        [TestCase(2, 281, false)]
        [TestCase(2, 87, false)]
        [TestCase(2, 100, true)]
        public void ClientCommandCannotSpoofActorOrEscapeTickWindow(int actor, int hostTick, bool accepted)
        {
            var command = new MagicCommand(100, 2, ElementId.Earth, EarthAbilityIds.FlickThrow,
                float3.zero, new float3(0, 0, 1), null, 1, 0, 42);
            Assert.That(new OnlineCommandCodec().TryDecode(OnlineCommandCodec.Encode(command), (byte)actor,
                (uint)hostTick, out _), Is.EqualTo(accepted));
        }

        [Test]
        public void InvalidFloatingPointCommandIsRejectedBeforeConstructingSimulationCommand()
        {
            OnlinePacket packet = new OnlinePacket { Kind = OnlineMessage.MagicCommand, Tick = 10,
                Id = 2, Aux = 65539, B = Vector3.forward, Value = float.NaN };
            Assert.That(new OnlineCommandCodec().TryDecode(packet, 2, 10, out _), Is.False);
        }

        [Test]
        public void ClientAllowedMessageKindsExcludeDamageBodiesAndGeometry()
        {
            Assert.That(OnlinePacket.IsClientIntent(OnlineMessage.CombatState), Is.False);
            Assert.That(OnlinePacket.IsClientIntent(OnlineMessage.BodySpawn), Is.False);
            Assert.That(OnlinePacket.IsClientIntent(OnlineMessage.TerrainEdit), Is.False);
            Assert.That(OnlinePacket.IsClientIntent(OnlineMessage.StructureDelta), Is.False);
            Assert.That(OnlinePacket.IsClientIntent(OnlineMessage.MatchState), Is.False);
            Assert.That(OnlinePacket.IsClientIntent(OnlineMessage.ControlIntent), Is.True);
        }
    }
}
