using System;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Fire;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Fire
{
    [DisallowMultipleComponent]
    public sealed class FireWorldBehaviour : MonoBehaviour
    {
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("Elemental.Fire.WorldTick");
        [SerializeField] private GravityWorldBehaviour gravityWorld;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private Transform ignoredEmitterRoot;
        private readonly FirePresentationSnapshot _scratch = new FirePresentationSnapshot();
        private readonly FireContactPatch[] _patches = new FireContactPatch[FireWorld.MaximumContacts];
        private FireContactCache[] _caches;
        private uint[] _generations;
        private FireFieldNode[][] _requested;
        private int[] _requestedCount;
        private GameObject _queryObject;
        private FireEnvironmentAdapter _environment;
        private uint _tick;
        public FireWorld World { get; private set; }
        public FireEnvironmentAdapter Environment => _environment;
        public bool IsReady => World != null && gravityWorld != null && gravityWorld.IsReady;

        public void Configure(GravityWorldBehaviour gravity, FireWorldSettings settings, int mask = ~0, Transform ignoredRoot = null)
        {
            if (World != null) throw new InvalidOperationException("FireWorld is already configured; retire its groups before replacing its owner.");
            if (gravity == null) throw new ArgumentNullException(nameof(gravity));
            gravityWorld = gravity; collisionMask = mask; ignoredEmitterRoot = ignoredRoot;
            Initialize(settings);
        }

        private void Start()
        {
            if (World == null && gravityWorld != null) Initialize(FireWorldSettings.Default);
            if (World == null)
            {
                Debug.LogError("FireWorld requires an explicitly configured GravityWorldBehaviour. Bind it in FireLab setup or call Configure before Start.", this);
                enabled = false;
            }
        }

        private void Initialize(FireWorldSettings settings)
        {
            World = new FireWorld(settings);
            _caches = new FireContactCache[World.Capacity]; _generations = new uint[World.Capacity];
            _requested = new FireFieldNode[World.Capacity][]; _requestedCount = new int[World.Capacity];
            for (int i = 0; i < _caches.Length; i++)
            { _caches[i] = new FireContactCache(); _requested[i] = new FireFieldNode[FireWorld.MaximumNodes]; }
            _queryObject = new GameObject("Fire overlap query (disabled collider)") { hideFlags = HideFlags.HideAndDontSave };
            SphereCollider sphere = _queryObject.AddComponent<SphereCollider>(); sphere.enabled = false;
            _environment = new FireEnvironmentAdapter(new FireSurfaceResolver(), sphere, collisionMask, ignoredEmitterRoot);
        }

        public bool TryCreate(uint seed, float energy, FireFieldNode node, out FireGroupHandle handle)
        {
            handle = default;
            if (!IsReady) return false;
            node.Up = gravityWorld.World.Sample(node.A, _tick).Up;
            if (!World.TryCreate(seed, energy, in node, out handle)) return false;
            _requested[handle.Slot][0] = node; _requestedCount[handle.Slot] = 1;
            _caches[handle.Slot].Clear(); _generations[handle.Slot] = handle.Generation;
            _scratch.Nodes[0] = node;
            _environment.Collect(_scratch.Nodes, 1, UnityEngine.Time.fixedDeltaTime, World.Time, _caches[handle.Slot]);
            World.TrySetNodes(handle, _scratch.Nodes, 1);
            int contacts = _caches[handle.Slot].CopyCurrent(_environment.Resolver, World.Time, _patches);
            World.TrySetContacts(handle, _patches, contacts);
            return true;
        }

        public bool TrySetNodes(FireGroupHandle group, FireFieldNode[] nodes, int count)
        {
            if (!IsReady || !World.IsCurrent(group) || nodes == null || count < 1 ||
                count > FireWorld.MaximumNodes || count > nodes.Length) return false;
            for (int i = 0; i < count; i++)
                if (!nodes[i].IsValid || nodes[i].MaxTargetSpeed > World.Settings.MaximumSpeed) return false;
            Array.Copy(nodes, _requested[group.Slot], count); _requestedCount[group.Slot] = count;
            return true;
        }

        public bool Stop(FireGroupHandle group) => World != null && World.Stop(group);

        private void FixedUpdate()
        {
            if (!IsReady) return;
            using (TickMarker.Auto())
            {
                _tick++;
                for (int slot = 0; slot < World.Capacity; slot++)
                {
                    if (!World.TryGetHandle(slot, out FireGroupHandle group)) continue;
                    if (_generations[slot] != group.Generation)
                    { _caches[slot].Clear(); _generations[slot] = group.Generation; }
                    World.TryCopySnapshot(group, _scratch);
                    if (_requestedCount[slot] > 0)
                    {
                        _scratch.NodeCount = _requestedCount[slot];
                        Array.Copy(_requested[slot], _scratch.Nodes, _scratch.NodeCount);
                    }
                    for (int i = 0; i < _scratch.NodeCount; i++)
                    {
                        FireFieldNode node = _scratch.Nodes[i];
                        node.Up = gravityWorld.World.Sample((node.A + node.B) * 0.5f, _tick).Up;
                        _scratch.Nodes[i] = node;
                    }
                    _environment.Collect(_scratch.Nodes, _scratch.NodeCount, UnityEngine.Time.fixedDeltaTime, World.Time, _caches[slot]);
                    World.TrySetNodes(group, _scratch.Nodes, _scratch.NodeCount);
                    int count = _caches[slot].CopyCurrent(_environment.Resolver, World.Time, _patches);
                    World.TrySetContacts(group, _patches, count);
                }
            }
        }

        private void Update()
        {
            if (IsReady) World.Advance(UnityEngine.Time.deltaTime);
        }

        // Revalidate known geometry synchronously at publication, including between physics ticks.
        public bool CopySnapshot(FireGroupHandle group, FirePresentationSnapshot output)
        {
            if (!IsReady || !World.IsCurrent(group))
            {
                if (output != null) { output.Lifecycle = FireLifecycle.Retired; output.NodeCount = output.ContactCount = 0; }
                return false;
            }
            if (_generations[group.Slot] != group.Generation)
            { _caches[group.Slot].Clear(); _generations[group.Slot] = group.Generation; }
            int count = _caches[group.Slot].CopyCurrent(_environment.Resolver, World.Time, _patches);
            World.TrySetContacts(group, _patches, count);
            return World.TryCopySnapshot(group, output);
        }

        private void OnDisable()
        {
            if (World == null) return;
            for (int i = 0; i < World.Capacity; i++)
                if (World.TryGetHandle(i, out FireGroupHandle group)) World.Stop(group);
        }
        private void OnDestroy()
        {
            if (_queryObject != null) Destroy(_queryObject);
        }
    }
}
