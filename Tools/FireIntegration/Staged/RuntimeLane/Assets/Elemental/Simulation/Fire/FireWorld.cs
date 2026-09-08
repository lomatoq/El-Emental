using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Fire
{
    // Explicitly owned, bounded canonical domain state. No particle, physics-object or render authority.
    public sealed class FireWorld
    {
        public const int MaximumNodes = 6;
        public const int MaximumContacts = 8;
        private struct GroupState
        {
            public uint Generation, Seed;
            public FireLifecycle Lifecycle;
            public float Energy, DrainUntil;
            public float3 Origin, FreeUp;
            public int Nodes, Contacts;
        }
        private readonly FireWorldSettings _settings;
        private readonly GroupState[] _groups;
        private readonly FireFieldNode[] _nodes;
        private readonly FireContactPatch[] _contacts;
        private const int EnvelopeBins = 16;
        private readonly float3[] _envelopeMin, _envelopeMax;
        private readonly int[] _envelopeEpoch;
        private readonly float _envelopeStep;
        public float Time { get; private set; }
        public int RejectedSpawns { get; private set; }
        public int RejectedUpdates { get; private set; }
        public int Capacity => _groups.Length;
        public FireWorldSettings Settings => _settings;

        public FireWorld(FireWorldSettings settings)
        {
            if (settings.MaximumGroups < 1) throw new ArgumentException("Use FireWorldSettings.Default or an explicit valid settings value.", nameof(settings));
            _settings = settings;
            _groups = new GroupState[settings.MaximumGroups];
            _nodes = new FireFieldNode[settings.MaximumGroups * MaximumNodes];
            _contacts = new FireContactPatch[settings.MaximumGroups * MaximumContacts];
            _envelopeMin = new float3[settings.MaximumGroups * EnvelopeBins];
            _envelopeMax = new float3[settings.MaximumGroups * EnvelopeBins];
            _envelopeEpoch = new int[settings.MaximumGroups * EnvelopeBins];
            for (int i = 0; i < _envelopeEpoch.Length; i++) _envelopeEpoch[i] = -1;
            _envelopeStep = (settings.MaximumParticleLifetime + settings.TailMargin) / 8f;
        }

        public bool TryCreate(uint seed, float energy, in FireFieldNode node, out FireGroupHandle handle)
        {
            handle = default;
            if (!node.IsValid || node.MaxTargetSpeed > _settings.MaximumSpeed || !math.isfinite(energy) || energy < 0)
            { RejectedSpawns++; return false; }
            for (int i = 0; i < _groups.Length; i++)
            {
                if (_groups[i].Lifecycle != FireLifecycle.Retired) continue;
                uint generation = _groups[i].Generation + 1;
                if (generation == 0) continue; // Never wrap an exhausted slot back into an old identity.
                _groups[i] = new GroupState { Generation = generation, Seed = seed, Energy = energy,
                    Lifecycle = FireLifecycle.Active, Origin = node.A, FreeUp = node.Up, Nodes = 1 };
                Array.Clear(_nodes, i * MaximumNodes, MaximumNodes);
                Array.Clear(_contacts, i * MaximumContacts, MaximumContacts);
                _nodes[i * MaximumNodes] = node;
                _nodes[i * MaximumNodes].Density = math.min(node.Density, 1f);
                for (int j = 0; j < EnvelopeBins; j++) _envelopeEpoch[i * EnvelopeBins + j] = -1;
                RecordEnvelope(i, in node);
                handle = new FireGroupHandle(i, generation);
                return true;
            }
            RejectedSpawns++; return false;
        }

        public bool IsCurrent(FireGroupHandle handle) => handle.IsValid && handle.Slot < Capacity &&
            _groups[handle.Slot].Generation == handle.Generation && _groups[handle.Slot].Lifecycle != FireLifecycle.Retired;

        public bool TryGetHandle(int slot, out FireGroupHandle handle)
        {
            handle = default;
            if (slot < 0 || slot >= Capacity || _groups[slot].Lifecycle == FireLifecycle.Retired) return false;
            handle = new FireGroupHandle(slot, _groups[slot].Generation); return true;
        }

        // Caller specifies stable node order. Atomic validation prevents partial failed commands.
        public bool TrySetNodes(FireGroupHandle handle, FireFieldNode[] nodes, int count)
        {
            if (!IsCurrent(handle) || nodes == null || count < 1 || count > MaximumNodes || count > nodes.Length)
            { RejectedUpdates++; return false; }
            for (int i = 0; i < count; i++)
                if (!nodes[i].IsValid || nodes[i].MaxTargetSpeed > _settings.MaximumSpeed)
                { RejectedUpdates++; return false; }
            ref GroupState group = ref _groups[handle.Slot];
            int offset = handle.Slot * MaximumNodes;
            float densitySum = 0;
            for (int i = 0; i < count; i++) densitySum += nodes[i].Density;
            float densityScale = 1f / math.max(1f, densitySum);
            for (int i = 0; i < count; i++)
            {
                _nodes[offset + i] = nodes[i];
                _nodes[offset + i].Density *= densityScale;
                RecordEnvelope(handle.Slot, in nodes[i]);
            }
            Array.Clear(_nodes, offset + count, MaximumNodes - count);
            group.Nodes = count; group.FreeUp = nodes[count - 1].Up;
            return true;
        }

        public bool TrySetContacts(FireGroupHandle handle, FireContactPatch[] contacts, int count)
        {
            if (!IsCurrent(handle) || contacts == null || count < 0 || count > MaximumContacts || count > contacts.Length)
            { RejectedUpdates++; return false; }
            for (int i = 0; i < count; i++) if (!FireContactMath.IsValid(contacts[i])) { RejectedUpdates++; return false; }
            Array.Copy(contacts, 0, _contacts, handle.Slot * MaximumContacts, count);
            Array.Clear(_contacts, handle.Slot * MaximumContacts + count, MaximumContacts - count);
            _groups[handle.Slot].Contacts = count; return true;
        }

        public bool Stop(FireGroupHandle handle)
        {
            if (!IsCurrent(handle)) return false;
            ref GroupState group = ref _groups[handle.Slot];
            if (group.Lifecycle == FireLifecycle.Active)
            {
                group.Lifecycle = FireLifecycle.Draining;
                group.DrainUntil = Time + _settings.MaximumParticleLifetime + _settings.TailMargin;
            }
            return true;
        }

        public void Advance(float scaledDeltaTime)
        {
            if (!math.isfinite(scaledDeltaTime) || scaledDeltaTime < 0 || !math.isfinite(Time + scaledDeltaTime))
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaTime));
            Time += scaledDeltaTime;
            for (int i = 0; i < Capacity; i++)
                if (_groups[i].Lifecycle == FireLifecycle.Draining && Time >= _groups[i].DrainUntil)
                {
                    _groups[i].Lifecycle = FireLifecycle.Retired;
                    _groups[i].Nodes = _groups[i].Contacts = 0;
                    Array.Clear(_nodes, i * MaximumNodes, MaximumNodes);
                    Array.Clear(_contacts, i * MaximumContacts, MaximumContacts);
                }
        }

        public bool TryCopySnapshot(FireGroupHandle handle, FirePresentationSnapshot output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.NodeCount = output.ContactCount = 0; output.Lifecycle = FireLifecycle.Retired;
            Array.Clear(output.Nodes, 0, MaximumNodes); Array.Clear(output.Contacts, 0, MaximumContacts);
            if (!IsCurrent(handle)) return false;
            GroupState group = _groups[handle.Slot];
            output.Group = handle; output.Lifecycle = group.Lifecycle; output.Seed = group.Seed;
            output.Time = Time; output.Energy = group.Energy; output.Origin = group.Origin; output.FreeUp = group.FreeUp;
            // Fixed temporal bins expire the old emitter path after the actual particle tail.
            float tail = _settings.MaximumSpeed * (_settings.MaximumParticleLifetime + _settings.TailMargin);
            float3 minimum = _nodes[handle.Slot * MaximumNodes].A, maximum = minimum;
            for (int i = 0; i < group.Nodes; i++)
            {
                FireFieldNode node = _nodes[handle.Slot * MaximumNodes + i];
                minimum = math.min(minimum, math.min(node.A, node.B) - node.Radius);
                maximum = math.max(maximum, math.max(node.A, node.B) + node.Radius);
            }
            int currentEpoch = (int)math.floor(Time / _envelopeStep);
            for (int i = 0; i < EnvelopeBins; i++)
            {
                int index = handle.Slot * EnvelopeBins + i;
                if (_envelopeEpoch[index] < 0 || currentEpoch - _envelopeEpoch[index] > 9) continue;
                minimum = math.min(minimum, _envelopeMin[index]); maximum = math.max(maximum, _envelopeMax[index]);
            }
            output.BoundsMin = minimum - tail; output.BoundsMax = maximum + tail;
            output.NodeCount = group.Nodes; output.ContactCount = group.Contacts;
            Array.Copy(_nodes, handle.Slot * MaximumNodes, output.Nodes, 0, group.Nodes);
            Array.Copy(_contacts, handle.Slot * MaximumContacts, output.Contacts, 0, group.Contacts);
            return true;
        }

        private void RecordEnvelope(int slot, in FireFieldNode node)
        {
            int epoch = (int)math.floor(Time / _envelopeStep);
            int index = slot * EnvelopeBins + epoch % EnvelopeBins;
            float3 minimum = math.min(node.A, node.B) - node.Radius;
            float3 maximum = math.max(node.A, node.B) + node.Radius;
            if (_envelopeEpoch[index] != epoch)
            { _envelopeEpoch[index] = epoch; _envelopeMin[index] = minimum; _envelopeMax[index] = maximum; }
            else
            { _envelopeMin[index] = math.min(_envelopeMin[index], minimum); _envelopeMax[index] = math.max(_envelopeMax[index], maximum); }
        }
    }
}
