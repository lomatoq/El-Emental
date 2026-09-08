using Elemental.Online;
using Elemental.Simulation.Networking;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Netcode;

namespace Elemental.Tests.EditMode
{
    public sealed class OnlineSemanticInputTests
    {
        [Test]
        public void ContinuousHeldReleaseModifierAndCameraRoundTripThroughActualWire()
        {
            var frame = new EarthSemanticInputFrame { Sequence = 57, Tick = 200,
                Held = EarthInputBits.Force | EarthInputBits.Modifier, Pressed = EarthInputBits.Field,
                Released = EarthInputBits.Primary | EarthInputBits.Jump, PointerViewport = new float2(.23f, .81f),
                Move = new float2(.5f, -.3f), Scroll = -120, CameraPosition = new float3(2, 75, 3),
                CameraRotation = quaternion.Euler(.1f, .2f, .3f), FieldOfView = 70, Aspect = 16f / 9 };
            OnlinePacket packet = OnlineEarthInputBridge.Encode(frame);
            packet.Version = OnlinePacket.Protocol; packet.Epoch = 7; packet.Id = 2; packet.Sequence = 10;
            using var writer = new FastBufferWriter(OnlinePacket.MaximumBytes, Allocator.Temp); writer.WriteValueSafe(packet);
            using var reader = new FastBufferReader(writer, Allocator.Temp); reader.ReadValueSafe(out OnlinePacket received);
            EarthSemanticInputFrame result = OnlineEarthInputBridge.Decode(received);
            Assert.That(result.Valid, Is.True);
            Assert.That(result.Held, Is.EqualTo(frame.Held)); Assert.That(result.Pressed, Is.EqualTo(frame.Pressed));
            Assert.That(result.Released, Is.EqualTo(frame.Released)); Assert.That(result.PointerViewport, Is.EqualTo(frame.PointerViewport));
            Assert.That(result.CameraRotation, Is.EqualTo(frame.CameraRotation)); Assert.That(result.CameraPosition, Is.EqualTo(frame.CameraPosition));
            Assert.That(result.Scroll, Is.EqualTo(-120)); Assert.That(result.Sequence, Is.EqualTo(57));
        }
        [Test]
        public void BinaryGeometryPacketFitsTransportBudgetAndCannotCarryHiddenCommandPath()
        {
            var packet = new OnlinePacket { Version = OnlinePacket.Protocol, Epoch = 1, Kind = OnlineMessage.MeshChunk };
            for (int i = 0; i < 500; i++) packet.Bytes.Add((byte)i);
            using var writer = new FastBufferWriter(OnlinePacket.MaximumBytes, Allocator.Temp); writer.WriteValueSafe(packet);
            Assert.That(writer.Length, Is.LessThanOrEqualTo(OnlinePacket.MaximumBytes));
            using var reader = new FastBufferReader(writer, Allocator.Temp); reader.ReadValueSafe(out OnlinePacket replica);
            Assert.That(replica.Bytes.Length, Is.EqualTo(500)); Assert.That(replica.Bytes[499], Is.EqualTo((byte)(499 % 256)));
            packet.Path.Add(float3.zero); Assert.That(packet.IsFiniteAndBounded(), Is.False);
        }
    }
}
