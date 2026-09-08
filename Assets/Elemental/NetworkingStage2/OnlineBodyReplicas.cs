using System;
using System.Collections.Generic;
using Elemental.Simulation.Networking;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>Explicit stable-ID body registry. Spawn/despawn and geometry are separate reliable transactions.</summary>
    public sealed class OnlineBodyReplicas : MonoBehaviour
    {
        private struct Entry { public uint Id; public Rigidbody Body; public bool LocalPrediction; }
        private readonly List<Entry> _entries = new List<Entry>(512);
        private readonly Dictionary<uint, int> _index = new Dictionary<uint, int>(512);
        private NgoGameplayTransport _transport;
        private bool _authority;
        private int _cursor;
        private float _nextSend;
        public void Configure(NgoGameplayTransport transport, bool authority)
        { _transport = transport; _authority = authority; }
        public void Register(uint stableId, Rigidbody body, bool localPrediction)
        {
            if (stableId == 0 || body == null || _index.ContainsKey(stableId) || _entries.Count >= 512)
                throw new InvalidOperationException("Invalid, duplicate or excessive online body registration.");
            _index.Add(stableId, _entries.Count);
            _entries.Add(new Entry { Id = stableId, Body = body, LocalPrediction = localPrediction });
            if (!_authority && !localPrediction) body.isKinematic = true;
        }
        public void Unregister(uint stableId)
        {
            if (!_index.TryGetValue(stableId, out int at)) return;
            int last = _entries.Count - 1;
            Entry moved = _entries[last]; _entries[at] = moved; _index[moved.Id] = at;
            _entries.RemoveAt(last); _index.Remove(stableId);
        }
        public void Clear() { _entries.Clear(); _index.Clear(); _cursor = 0; }
        public void PublishTick(uint tick)
        {
            if (!_authority || _transport == null || !_transport.Running || Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + .05f;
            // Bounded initial budget. Character state uses its separate higher-priority channel.
            int count = Mathf.Min(16, _entries.Count);
            for (int i = 0; i < count; i++)
            {
                if (_cursor >= _entries.Count) _cursor = 0;
                Entry entry = _entries[_cursor++];
                if (entry.Body == null) continue;
                _transport.Publish(new OnlinePacket { Kind = OnlineMessage.BodyState, Id = entry.Id,
                    Tick = tick, A = entry.Body.position, Rotation = entry.Body.rotation,
                    B = entry.Body.linearVelocity, C = entry.Body.angularVelocity });
            }
        }
        public bool Apply(in OnlinePacket packet)
        {
            if (_authority || packet.Kind != OnlineMessage.BodyState) return false;
            // Unreliable state may precede a reliable spawn or follow despawn.
            // The next state repairs this; never recreate an entity from a pose.
            if (!_index.TryGetValue(packet.Id, out int at)) return true;
            Entry entry = _entries[at]; if (entry.Body == null) return false;
            float norm = Quaternion.Dot(packet.Rotation, packet.Rotation);
            if (norm < .9f || norm > 1.1f) return false;
            if (entry.LocalPrediction && !entry.Body.isKinematic)
            {
                var snapshot = new RigidbodySnapshot(packet.Id, packet.Tick, (float3)packet.A,
                    new quaternion(packet.Rotation.x, packet.Rotation.y, packet.Rotation.z, packet.Rotation.w),
                    (float3)packet.B, (float3)packet.C, 255);
                CorrectionResult correction = PredictionReconciler.Reconcile((float3)entry.Body.position,
                    (float3)entry.Body.linearVelocity, snapshot);
                entry.Body.position = (Vector3)correction.Position;
                entry.Body.linearVelocity = (Vector3)correction.Velocity;
            }
            else
            { entry.Body.MovePosition(packet.A); entry.Body.MoveRotation(packet.Rotation); }
            return true;
        }
    }
}
