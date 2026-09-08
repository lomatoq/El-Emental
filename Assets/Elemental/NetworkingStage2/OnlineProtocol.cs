using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

namespace Elemental.Online
{
    public enum OnlineMessage : byte
    {
        Hello = 1, Ready, Begin, MotorInput, MagicCommand, ControlIntent,
        CommandDecision, BodySpawn, BodyState, BodyDespawn, CharacterState,
        CombatState, TerrainEdit, StructureDelta, RegionState, MatchState,
        WorldBegin, WorldAck, MeshBegin, MeshChunk, MeshEnd, BodyVisual, BodyCollider, BodyMaterial,
        TerrainBegin, TerrainCommit, TerrainChunkHash, WorldEnd, NodeCommit, ArenaReset, ArenaPose
    }

    [Flags]
    public enum OnlineCapabilities : uint
    {
        None = 0, TwoActors = 1, Commands = 2, ContinuousControls = 4,
        HostDamage = 8, Bodies = 16, Terrain = 32, Structures = 64,
        CharacterReaction = 128, Match = 256, Prediction = 512,
        All = 1023
    }

    /// <summary>Explicit primitive wire layout; never transmit native struct padding or object IDs.</summary>
    public struct OnlinePacket : INetworkSerializable
    {
        public const ushort Protocol = 3;
        public const int MaximumBytes = 768;
        public ushort Version;
        public OnlineMessage Kind;
        public uint Epoch, Sequence, Tick, Id, Aux, Flags, Seed;
        public ulong BuildHash, WorldHash;
        public Vector3 A, B, C, D;
        public Quaternion Rotation;
        public float Value, Value2, Value3, Value4, Value5;
        public FixedList512Bytes<float3> Path;
        public FixedList512Bytes<byte> Bytes;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Version);
            byte kind = (byte)Kind; serializer.SerializeValue(ref kind); Kind = (OnlineMessage)kind;
            serializer.SerializeValue(ref Epoch); serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref Tick); serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Aux); serializer.SerializeValue(ref Flags);
            serializer.SerializeValue(ref Seed); serializer.SerializeValue(ref BuildHash);
            serializer.SerializeValue(ref WorldHash);
            serializer.SerializeValue(ref A); serializer.SerializeValue(ref B);
            serializer.SerializeValue(ref C); serializer.SerializeValue(ref D);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Value); serializer.SerializeValue(ref Value2);
            serializer.SerializeValue(ref Value3); serializer.SerializeValue(ref Value4); serializer.SerializeValue(ref Value5);
            byte count = serializer.IsReader ? (byte)0 : checked((byte)Path.Length);
            serializer.SerializeValue(ref count);
            if (count > 32) throw new InvalidOperationException("Online path exceeds 32 points.");
            if (serializer.IsReader) Path.Clear();
            for (int i = 0; i < count; i++)
            {
                Vector3 point = serializer.IsReader ? default : (Vector3)Path[i];
                serializer.SerializeValue(ref point);
                if (serializer.IsReader) Path.Add((float3)point);
            }
            ushort byteCount = serializer.IsReader ? (ushort)0 : checked((ushort)Bytes.Length);
            serializer.SerializeValue(ref byteCount);
            if (byteCount > 500 || byteCount > 0 && count > 0) throw new InvalidOperationException("Invalid binary packet size.");
            if (serializer.IsReader) Bytes.Clear();
            for (int i = 0; i < byteCount; i++)
            {
                byte value = serializer.IsReader ? (byte)0 : Bytes[i]; serializer.SerializeValue(ref value);
                if (serializer.IsReader) Bytes.Add(value);
            }
        }

        public bool IsFiniteAndBounded()
        {
            if (Version != Protocol || Kind < OnlineMessage.Hello || Kind > OnlineMessage.ArenaPose ||
                Epoch == 0 || !Finite(A) || !Finite(B) || !Finite(C) || !Finite(D) ||
                !math.all(math.isfinite(new float4(Rotation.x, Rotation.y, Rotation.z, Rotation.w))) ||
                !math.isfinite(Value) || !math.isfinite(Value2) || !math.isfinite(Value3) ||
                !math.isfinite(Value4) || !math.isfinite(Value5) || Path.Length > 32 || Bytes.Length > 500 ||
                Bytes.Length > 0 && Path.Length > 0) return false;
            for (int i = 0; i < Path.Length; i++)
                if (!math.all(math.isfinite(Path[i])) || math.lengthsq(Path[i]) > 1e10f) return false;
            return true;
        }
        private static bool Finite(Vector3 v) => math.all(math.isfinite((float3)v)) && v.sqrMagnitude <= 1e10f;
        public static bool IsClientIntent(OnlineMessage kind) => kind == OnlineMessage.MotorInput ||
            kind == OnlineMessage.MagicCommand || kind == OnlineMessage.ControlIntent || kind == OnlineMessage.WorldAck;
        public static bool IsTransient(OnlineMessage kind) => kind == OnlineMessage.MotorInput ||
            kind == OnlineMessage.BodyState || kind == OnlineMessage.CharacterState || kind == OnlineMessage.RegionState;
    }

    /// <summary>Two-party start barrier, independent of UI and Unity scene readiness timing.</summary>
    public sealed class OnlineStartBarrier
    {
        private byte _ready;
        public uint Epoch { get; private set; }
        public ulong BuildHash { get; private set; }
        public ulong WorldHash { get; private set; }
        public bool CanStart => Epoch != 0 && _ready == 3;
        public void Reset(uint epoch, ulong buildHash, ulong worldHash)
        {
            if (epoch == 0 || buildHash == 0 || worldHash == 0) throw new ArgumentOutOfRangeException(nameof(epoch));
            Epoch = epoch; BuildHash = buildHash; WorldHash = worldHash; _ready = 0;
        }
        public bool SetReady(byte actor, uint epoch, ulong buildHash, ulong worldHash,
            OnlineCapabilities capabilities, bool ready)
        {
            if (actor < 1 || actor > 2 || epoch != Epoch || buildHash != BuildHash ||
                worldHash != WorldHash || capabilities != OnlineCapabilities.All) return false;
            byte bit = (byte)(1 << (actor - 1));
            _ready = ready ? (byte)(_ready | bit) : (byte)(_ready & ~bit);
            return true;
        }
        public void Disconnect() { _ready = 0; Epoch = 0; }
    }

    /// <summary>Preserves reliable outcomes that arrive after Begin but before the local countdown deadline.</summary>
    public sealed class OnlineCountdownStateBuffer
    {
        public const int Capacity = 256;
        private readonly Queue<OnlinePacket> _pending = new Queue<OnlinePacket>(Capacity);
        public int Count => _pending.Count;
        public static bool RequiresRound(OnlineMessage kind) => kind == OnlineMessage.MatchState || kind == OnlineMessage.CombatState;
        public void Enqueue(in OnlinePacket packet)
        {
            if (!RequiresRound(packet.Kind) || _pending.Count >= Capacity)
                throw new InvalidOperationException("Invalid or overflowing countdown authority-state buffer.");
            _pending.Enqueue(packet);
        }
        public bool TryDequeue(out OnlinePacket packet)
        {
            if (_pending.Count == 0) { packet = default; return false; }
            packet = _pending.Dequeue(); return true;
        }
        public void Clear() => _pending.Clear();
    }

    public sealed class OnlineSequenceWindow
    {
        private readonly uint[] _last = new uint[32];
        public void Clear() => Array.Clear(_last, 0, _last.Length);
        public bool Accept(OnlineMessage kind, uint sequence)
        {
            int lane = (int)kind;
            if (lane < 1 || lane >= _last.Length || sequence == 0 || sequence <= _last[lane]) return false;
            _last[lane] = sequence; return true;
        }
    }
}
