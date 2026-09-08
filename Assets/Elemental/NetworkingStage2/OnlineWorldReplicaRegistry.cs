using System;
using System.Collections.Generic;
using Elemental.Runtime.Physics;
using Unity.Collections;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>
    /// Explicit Earth world roots, canonical host node IDs, exact mesh/material/collider
    /// descriptions. Client source simulation is suspended; proxies never run fracture/damage.
    /// </summary>
    public sealed class OnlineWorldReplicaRegistry : MonoBehaviour
    {
        [SerializeField] private Transform[] sourceRoots = Array.Empty<Transform>();
        [SerializeField] private Behaviour[] clientSuspendedMutators = Array.Empty<Behaviour>();
        [SerializeField] private Transform replicaRoot;
        [SerializeField] private OnlineGeometryCatalog catalog;
        [SerializeField] private EarthArmorController[] armorOwners = Array.Empty<EarthArmorController>();
        [SerializeField] private EarthSurfController[] surfOwners = Array.Empty<EarthSurfController>();
        [SerializeField] private EarthPlanetRockScatter rockScatter;
        private readonly List<Transform> _activeRoots = new List<Transform>(512);
        private readonly EarthArmorPiece[] _armorPieces = new EarthArmorPiece[512];
        [SerializeField, Min(64)] private int maximumNodes = 4096;
        [SerializeField, Min(65536)] private int geometryBytesPerSecond = 524288;
        private sealed class SourceNode
        {
            public Component Source; public uint Id; public Mesh Mesh; public ulong Signature;
            public EarthPillarWaveColumn Wave; public int ListIndex;
            public OnlinePacket LastPose; public int VisualSignature; public bool PoseSent, VisualSent;
            public float LastMovement;
        }
        private sealed class ReplicaNode
        {
            public GameObject Object; public MeshRenderer Renderer; public Collider Collider;
            public Rigidbody Body; public Material[] Materials; public PhysicsMaterial OwnedCollisionMaterial;
        }
        private sealed class Work
        {
            public OnlinePacket Packet; public byte[] Blob; public int Offset;
        }
        private sealed class IncomingMesh
        {
            public byte[] Bytes; public int Offset, RawLength; public ulong Hash;
        }
        private readonly Dictionary<Component, SourceNode> _sources = new Dictionary<Component, SourceNode>(4096);
        private readonly List<SourceNode> _ordered = new List<SourceNode>(4096);
        private readonly Dictionary<uint, ReplicaNode> _replicas = new Dictionary<uint, ReplicaNode>(4096);
        private readonly Dictionary<Mesh, uint> _meshIds = new Dictionary<Mesh, uint>(2048);
        private readonly Dictionary<ulong, uint> _meshHashes = new Dictionary<ulong, uint>(2048);
        private readonly Dictionary<uint, Mesh> _receivedMeshes = new Dictionary<uint, Mesh>(2048);
        private readonly Dictionary<uint, IncomingMesh> _incomingMeshes = new Dictionary<uint, IncomingMesh>();
        private readonly Queue<Work> _outbox = new Queue<Work>(4096);
        private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>(4096);
        private readonly List<Collider> _colliders = new List<Collider>(4096);
        private readonly List<Rigidbody> _bodies = new List<Rigidbody>(4096);
        private readonly List<MonoBehaviour> _behaviours = new List<MonoBehaviour>(4096);
        private readonly List<Material> _materials = new List<Material>(16);
        private MaterialPropertyBlock _properties;
        private readonly HashSet<Component> _seen = new HashSet<Component>();
        private readonly List<Component> _removed = new List<Component>(4096);
        private readonly Dictionary<Renderer, bool> _originalRenderers = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Collider, bool> _originalColliders = new Dictionary<Collider, bool>();
        private readonly Dictionary<Rigidbody, bool> _originalBodies = new Dictionary<Rigidbody, bool>();
        private readonly Dictionary<MonoBehaviour, bool> _originalBehaviours = new Dictionary<MonoBehaviour, bool>();
        private bool[] _mutatorEnabled;
        private NgoGameplayTransport _transport;
        private bool _authority, _configured, _syncStarted, _originalsHidden, _initialEndSent;
        private uint _nextNode = 1000, _nextMesh = 1000000;
        private float _nextScan, _nextPose, _tokens, _lastPump;
        private int _poseCursor;
        private uint _syncRevision = 1;
        private bool _pendingCheckpointAck;
        public Func<bool> CheckpointGeometryReady { private get; set; }
        public bool WorldSynchronized { get; private set; }
        public bool HasReferences => sourceRoots.Length > 0 && clientSuspendedMutators.Length > 0 && replicaRoot != null &&
            catalog != null && catalog.ContentHash != 0 && armorOwners.Length == 2 && surfOwners.Length == 2 &&
            armorOwners[0] != null && armorOwners[1] != null && surfOwners[0] != null && surfOwners[1] != null;
        public bool InitialSourcesReady => rockScatter == null || rockScatter.IsComplete;
        public ulong CatalogHash => catalog != null ? catalog.ContentHash : 0;
        public event Action<string> Failed;
        private void Awake() => _properties ??= new MaterialPropertyBlock();
        public bool ContainsSource(Transform target)
        {
            if (target == null) return false;
            for (int i = 0; i < sourceRoots.Length; i++)
                if (sourceRoots[i] != null && (target == sourceRoots[i] || target.IsChildOf(sourceRoots[i]))) return true;
            return false;
        }
        public uint SourceBodyId(Rigidbody body)
        {
            if (body == null) return 0;
            foreach (SourceNode node in _ordered)
                if (node.Source is Collider collider && collider.attachedRigidbody == body) return node.Id;
            return 0;
        }
        public Rigidbody ReplicaBody(uint id) => _replicas.TryGetValue(id, out ReplicaNode node) ? node.Body : null;

        public void Configure(bool authority, NgoGameplayTransport transport)
        {
            // Configure is also an explicit main-thread entry after a domain
            // reload; native rendering handles never belong in a constructor.
            _properties ??= new MaterialPropertyBlock();
            if (!HasReferences) throw new InvalidOperationException("Bind online world roots, replica root, catalogue and all client mutators.");
            if (ContainsSource(replicaRoot) || ContainsSource(transform)) throw new InvalidOperationException("World source roots cannot contain the network registry or replica root.");
            if (replicaRoot.position.sqrMagnitude > .000001f || Quaternion.Angle(replicaRoot.rotation, Quaternion.identity) > .001f ||
                (replicaRoot.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new InvalidOperationException("The online replica root must have an identity world transform.");
            _authority = authority; _transport = transport; _configured = true;
            WorldSynchronized = false; _syncRevision = 1; _pendingCheckpointAck = false; _syncStarted = _initialEndSent = false; _nextNode = 1000; _nextMesh = 1000000;
            _nextScan = _nextPose = 0; _tokens = 0; _lastPump = Time.unscaledTime;
            replicaRoot.gameObject.SetActive(false);
        }
        public void BeginSynchronization()
        {
            if (!_configured || _syncStarted) return;
            _syncStarted = true;
            if (!_authority) return;
            Enqueue(new OnlinePacket { Kind = OnlineMessage.WorldBegin, WorldHash = catalog.ContentHash, Id = 1 });
            Scan();
            Enqueue(new OnlinePacket { Kind = OnlineMessage.WorldEnd, Id = 1, Aux = (uint)_ordered.Count });
        }
        public void CheckpointRestoredArena()
        {
            if (!_configured || !_syncStarted || !_authority) throw new InvalidOperationException("Only the connected host checkpoints an arena reset.");
            ++_syncRevision; WorldSynchronized = false; _initialEndSent = false;
            Enqueue(new OnlinePacket { Kind = OnlineMessage.WorldBegin, WorldHash = catalog.ContentHash, Id = _syncRevision });
            Scan();
            foreach (SourceNode node in _ordered)
            {
                if (node.Source == null) continue;
                OnlinePacket pose = Pose(node, OnlineMessage.ArenaPose); pose.Seed = _syncRevision; Enqueue(pose);
                node.PoseSent = false;
            }
            Enqueue(new OnlinePacket { Kind = OnlineMessage.WorldEnd, Id = _syncRevision, Aux = (uint)_ordered.Count });
        }
        private void Update()
        {
            if (!_configured || !_syncStarted) return;
            if (!_authority)
            {
                if (_pendingCheckpointAck && (CheckpointGeometryReady == null || CheckpointGeometryReady()))
                { _pendingCheckpointAck = false; WorldSynchronized = true; _transport.Submit(new OnlinePacket { Kind = OnlineMessage.WorldAck, Aux = _syncRevision }); }
                return;
            }
            try
            {
                if (Time.unscaledTime >= _nextScan)
                { _nextScan = Time.unscaledTime + .1f; Scan(); }
                Pump();
                if (WorldSynchronized && Time.unscaledTime >= _nextPose)
                {
                    _nextPose = Time.unscaledTime + .05f;
                    int sent = 0;
                    for (int i = 0; i < _ordered.Count && sent < 128; i++)
                    {
                        if (_poseCursor >= _ordered.Count) _poseCursor = 0;
                        SourceNode node = _ordered[_poseCursor++];
                        if (node.Source == null) continue;
                        OnlinePacket pose = Pose(node, OnlineMessage.BodyState); pose.Seed = _syncRevision;
                        bool unchanged = node.PoseSent && (pose.A - node.LastPose.A).sqrMagnitude < .000001f &&
                            Quaternion.Angle(pose.Rotation, node.LastPose.Rotation) < .02f &&
                            (pose.D - node.LastPose.D).sqrMagnitude < .000001f;
                        if (unchanged && Time.unscaledTime - node.LastMovement > .5f) continue;
                        if (!unchanged) node.LastMovement = Time.unscaledTime;
                        _transport.Publish(pose); node.LastPose = pose; node.PoseSent = true; ++sent;
                    }
                }
            }
            catch (Exception error) { _configured = false; Failed?.Invoke(error.Message); }
        }
        private void Scan()
        {
            _seen.Clear();
            GatherRoots();
            for (int root = 0; root < _activeRoots.Count; root++)
            {
                if (_activeRoots[root] == null) throw new InvalidOperationException("An online world root is missing.");
                _renderers.Clear(); _activeRoots[root].GetComponentsInChildren(true, _renderers);
                for (int i = 0; i < _renderers.Count; i++)
                {
                    MeshRenderer renderer = _renderers[i];
                    if (!renderer.gameObject.activeInHierarchy) continue;
                    EarthPillarWaveColumn wave = renderer.GetComponent<EarthPillarWaveColumn>();
                    bool instanced = wave != null && wave.TryGetInstancedRenderMatrix(out _);
                    if (!renderer.enabled && !instanced) continue;
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    Observe(renderer, filter.sharedMesh, wave);
                }
                _colliders.Clear(); _activeRoots[root].GetComponentsInChildren(true, _colliders);
                for (int i = 0; i < _colliders.Count; i++)
                {
                    Collider collider = _colliders[i];
                    if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    if (!(collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider || collider is MeshCollider))
                        throw new InvalidOperationException("Unsupported collider under an online Earth root: " + collider.GetType().Name);
                    Observe(collider, collider is MeshCollider mesh ? mesh.sharedMesh : null, null);
                }
            }
            _removed.Clear();
            foreach (KeyValuePair<Component, SourceNode> entry in _sources)
                if (!_seen.Contains(entry.Key)) _removed.Add(entry.Key);
            for (int i = 0; i < _removed.Count; i++)
            {
                SourceNode node = _sources[_removed[i]];
                Enqueue(new OnlinePacket { Kind = OnlineMessage.BodyDespawn, Id = node.Id });
                int last = _ordered.Count - 1; SourceNode moved = _ordered[last];
                _ordered[node.ListIndex] = moved; moved.ListIndex = node.ListIndex;
                _ordered.RemoveAt(last); _sources.Remove(_removed[i]);
            }
        }
        private void GatherRoots()
        {
            _activeRoots.Clear(); _activeRoots.AddRange(sourceRoots);
            if (rockScatter != null && rockScatter.GeneratedRoot != null) _activeRoots.Add(rockScatter.GeneratedRoot);
            for (int i = 0; i < armorOwners.Length; i++)
            {
                int count = armorOwners[i].CopyActivePiecesNonAlloc(_armorPieces);
                for (int piece = 0; piece < count; piece++) _activeRoots.Add(_armorPieces[piece].transform);
            }
            for (int i = 0; i < surfOwners.Length; i++) surfOwners[i].AppendOnlineWorldRoots(_activeRoots);
        }
        private void Observe(Component component, Mesh mesh, EarthPillarWaveColumn wave)
        {
            if (!_seen.Add(component)) return;
            ulong signature = Signature(component, mesh);
            if (component is Collider signatureCollider)
                signature ^= OnlineCollisionMaterialState.Signature(signatureCollider.sharedMaterial);
            _materials.Clear();
            if (component is MeshRenderer signatureRenderer)
            {
                signatureRenderer.GetSharedMaterials(_materials);
                unchecked { for (int i = 0; i < _materials.Count; i++) signature = (signature ^ catalog.MaterialId(_materials[i])) * 1099511628211UL; }
            }
            if (!_sources.TryGetValue(component, out SourceNode node))
            {
                if (_sources.Count >= maximumNodes) throw new InvalidOperationException("Online Earth node budget exhausted.");
                node = new SourceNode { Source = component, Id = _nextNode++, ListIndex = _ordered.Count };
                _sources.Add(component, node); _ordered.Add(node);
            }
            if (node.Signature == signature && node.Mesh == mesh) { node.Wave = wave; CaptureVisual(node); return; }
            if (node.Mesh == mesh && mesh != null && node.Signature != signature) _meshIds.Remove(mesh);
            node.Signature = signature; node.Mesh = mesh; node.Wave = wave;
            uint meshId = EnsureMesh(mesh);
            OnlinePacket spawn = Pose(node, OnlineMessage.BodySpawn);
            uint kind = component is MeshRenderer ? 1u : component is BoxCollider ? 2u : component is SphereCollider ? 3u : component is CapsuleCollider ? 4u : 5u;
            spawn.Aux = meshId; spawn.Flags = kind | ((uint)component.gameObject.layer << 8);
            if (component is MeshRenderer shadows)
                spawn.Flags |= ((uint)shadows.shadowCastingMode << 18) | (shadows.receiveShadows ? 1u << 20 : 0);
            if (component is Collider collision)
            {
                spawn.Seed = catalog.PhysicsId(collision.sharedMaterial);
                if (collision.sharedMaterial != null && spawn.Seed == 0) OnlineCollisionMaterialState.Capture(collision.sharedMaterial, ref spawn);
                if (collision.isTrigger) spawn.Flags |= 1u << 16;
                if (collision is MeshCollider triangle && triangle.convex) spawn.Flags |= 1u << 17;
            }
            _materials.Clear();
            if (component is MeshRenderer visual) visual.GetSharedMaterials(_materials);
            spawn.Value2 = _materials.Count;
            Enqueue(spawn);
            for (int i = 0; i < _materials.Count; i++)
            {
                uint material = catalog.MaterialId(_materials[i]);
                if (material == 0) throw new InvalidOperationException("Material is missing from the online catalogue: " + _materials[i]?.name);
                Enqueue(new OnlinePacket { Kind = OnlineMessage.BodyMaterial, Id = node.Id, Aux = (uint)i, Seed = material });
            }
            if (component is BoxCollider box) Enqueue(new OnlinePacket { Kind = OnlineMessage.BodyCollider, Id = node.Id, A = box.center, B = box.size });
            else if (component is SphereCollider sphere) Enqueue(new OnlinePacket { Kind = OnlineMessage.BodyCollider, Id = node.Id, A = sphere.center, Value = sphere.radius });
            else if (component is CapsuleCollider capsule) Enqueue(new OnlinePacket { Kind = OnlineMessage.BodyCollider, Id = node.Id,
                A = capsule.center, Value = capsule.radius, Value2 = capsule.height, Flags = (uint)capsule.direction });
            Enqueue(new OnlinePacket { Kind = OnlineMessage.NodeCommit, Id = node.Id });
            node.VisualSent = false; CaptureVisual(node);
        }
        private void CaptureVisual(SourceNode node)
        {
            if (!(node.Source is MeshRenderer renderer)) return;
            OnlinePacket packet = OnlineRendererState.Capture(renderer, _properties);
            int signature = OnlineRendererState.Signature(packet);
            if (node.VisualSent && node.VisualSignature == signature) return;
            packet.Id = node.Id; Enqueue(packet); node.VisualSignature = signature; node.VisualSent = true;
        }
        private static ulong Signature(Component component, Mesh mesh)
        {
            // Runtime reference identity stays local; no Unity instance ID crosses the wire.
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                if (mesh != null) { hash = (hash ^ (uint)mesh.vertexCount) * 1099511628211UL; hash = (hash ^ (uint)mesh.bounds.GetHashCode()) * 1099511628211UL; }
                if (component is BoxCollider box) hash ^= (uint)box.center.GetHashCode() ^ (uint)box.size.GetHashCode();
                if (component is SphereCollider sphere) hash ^= (uint)sphere.center.GetHashCode() ^ (uint)sphere.radius.GetHashCode();
                if (component is CapsuleCollider capsule) hash ^= (uint)capsule.center.GetHashCode() ^ (uint)capsule.radius.GetHashCode() ^ (uint)capsule.height.GetHashCode() ^ (uint)capsule.direction;
                return hash == 0 ? 1 : hash;
            }
        }
        private OnlinePacket Pose(SourceNode node, OnlineMessage kind)
        {
            Matrix4x4 matrix = node.Source.transform.localToWorldMatrix;
            if (node.Wave != null && node.Wave.TryGetInstancedRenderMatrix(out Matrix4x4 instanced)) matrix = instanced;
            return new OnlinePacket { Kind = kind, Id = node.Id, A = matrix.GetColumn(3), Rotation = matrix.rotation, D = matrix.lossyScale };
        }
        private uint EnsureMesh(Mesh mesh)
        {
            if (mesh == null) return 0;
            uint assetId = catalog.MeshId(mesh); if (assetId != 0) return assetId;
            if (_meshIds.TryGetValue(mesh, out uint cached)) return cached;
            byte[] compressed = OnlineMeshCodec.Encode(mesh, out int rawLength, out ulong hash);
            if (_meshHashes.TryGetValue(hash, out uint identical)) { _meshIds.Add(mesh, identical); return identical; }
            if (_meshHashes.Count >= 4096) throw new InvalidOperationException("Online generated mesh budget exhausted; end this round before loading more unique geometry.");
            uint id = _nextMesh++; _meshIds.Add(mesh, id); _meshHashes.Add(hash, id);
            Enqueue(new OnlinePacket { Kind = OnlineMessage.MeshBegin, Id = id, Aux = (uint)rawLength, Seed = (uint)compressed.Length, WorldHash = hash });
            _outbox.Enqueue(new Work { Packet = new OnlinePacket { Kind = OnlineMessage.MeshChunk, Id = id }, Blob = compressed });
            Enqueue(new OnlinePacket { Kind = OnlineMessage.MeshEnd, Id = id }); return id;
        }
        private void Enqueue(OnlinePacket packet)
        {
            if (_outbox.Count >= 32768) throw new InvalidOperationException("Online reliable world queue exceeded its budget.");
            _outbox.Enqueue(new Work { Packet = packet });
        }
        private void Pump()
        {
            float now = Time.unscaledTime;
            _tokens = Mathf.Min(geometryBytesPerSecond, _tokens + (now - _lastPump) * geometryBytesPerSecond); _lastPump = now;
            while (_outbox.Count > 0 && _tokens >= OnlinePacket.MaximumBytes)
            {
                Work work = _outbox.Peek(); OnlinePacket packet = work.Packet;
                if (work.Blob != null)
                {
                    packet.Aux = (uint)work.Offset;
                    int count = Mathf.Min(500, work.Blob.Length - work.Offset);
                    for (int i = 0; i < count; i++) packet.Bytes.Add(work.Blob[work.Offset++]);
                    if (work.Offset == work.Blob.Length) _outbox.Dequeue();
                }
                else _outbox.Dequeue();
                _transport.Publish(packet); _tokens -= OnlinePacket.MaximumBytes;
                if (packet.Kind == OnlineMessage.WorldEnd && packet.Id == _syncRevision) _initialEndSent = true;
            }
        }
        public bool AcceptAcknowledgement(in OnlinePacket packet)
        {
            if (!_authority || packet.Kind != OnlineMessage.WorldAck) return false;
            if (packet.Aux < _syncRevision) return true; // Late earlier ACK must not complete the new checkpoint.
            if (!_initialEndSent || packet.Aux != _syncRevision) return false;
            WorldSynchronized = true; return true;
        }

        public bool Apply(in OnlinePacket packet, out string error)
        {
            error = null;
            if (_authority || !_configured || !_syncStarted) { error = "Replica-world writes require a prepared client."; return false; }
            try
            {
                switch (packet.Kind)
                {
                    case OnlineMessage.WorldBegin:
                        if (packet.WorldHash != catalog.ContentHash || packet.Id < _syncRevision || packet.Id > _syncRevision + 1)
                            throw new InvalidOperationException("Geometry catalogue or checkpoint revision differs.");
                        _syncRevision = packet.Id; _pendingCheckpointAck = false;
                        HideOriginals(); WorldSynchronized = false; return true;
                    case OnlineMessage.WorldEnd:
                        if (packet.Id != _syncRevision || _incomingMeshes.Count != 0 || _replicas.Count != packet.Aux)
                            throw new InvalidOperationException("Geometry checkpoint transfer is incomplete.");
                        replicaRoot.gameObject.SetActive(true); _pendingCheckpointAck = true; return true;
                    case OnlineMessage.MeshBegin:
                        if (packet.Id < 1000000 || packet.Aux == 0 || packet.Aux > OnlineMeshCodec.MaximumRawBytes || packet.Seed == 0 || packet.Seed > OnlineMeshCodec.MaximumRawBytes || _incomingMeshes.Count >= 16)
                            throw new InvalidOperationException("Invalid mesh transfer header.");
                        _incomingMeshes.Add(packet.Id, new IncomingMesh { Bytes = new byte[packet.Seed], RawLength = (int)packet.Aux, Hash = packet.WorldHash }); return true;
                    case OnlineMessage.MeshChunk:
                        if (!_incomingMeshes.TryGetValue(packet.Id, out IncomingMesh incoming) || packet.Aux != incoming.Offset || incoming.Offset + packet.Bytes.Length > incoming.Bytes.Length)
                            throw new InvalidOperationException("Mesh chunks are missing or out of order.");
                        for (int i = 0; i < packet.Bytes.Length; i++) incoming.Bytes[incoming.Offset++] = packet.Bytes[i]; return true;
                    case OnlineMessage.MeshEnd:
                        if (!_incomingMeshes.TryGetValue(packet.Id, out IncomingMesh completed) || completed.Offset != completed.Bytes.Length)
                            throw new InvalidOperationException("Mesh transfer is truncated.");
                        _receivedMeshes.Add(packet.Id, OnlineMeshCodec.Decode(completed.Bytes, completed.RawLength, completed.Hash));
                        _incomingMeshes.Remove(packet.Id); return true;
                    case OnlineMessage.BodySpawn: Spawn(packet); return true;
                    case OnlineMessage.BodyDespawn:
                        if (_replicas.TryGetValue(packet.Id, out ReplicaNode removed)) { DestroyNode(removed); _replicas.Remove(packet.Id); } return true;
                    case OnlineMessage.ArenaPose:
                    case OnlineMessage.BodyState:
                        if (packet.Seed != _syncRevision) return true; // Old pre-reset unreliable poses cannot move restored sources.
                        if (_replicas.TryGetValue(packet.Id, out ReplicaNode posed)) ApplyPose(posed, packet); return true;
                    case OnlineMessage.BodyMaterial:
                        ReplicaNode materialNode = Require(packet.Id);
                        Material material = catalog.ResolveMaterial(packet.Seed);
                        if (material == null || materialNode.Materials == null || packet.Aux >= materialNode.Materials.Length) throw new InvalidOperationException("Invalid replica material slot.");
                        materialNode.Materials[packet.Aux] = material; return true;
                    case OnlineMessage.BodyCollider: ApplyCollider(Require(packet.Id), packet); return true;
                    case OnlineMessage.BodyVisual:
                        if (!OnlineRendererState.Apply(Require(packet.Id).Renderer, packet, _properties)) throw new InvalidOperationException("Invalid Earth shader override state.");
                        return true;
                    case OnlineMessage.NodeCommit:
                        ReplicaNode ready = Require(packet.Id);
                        if (ready.Renderer != null)
                        {
                            for (int i = 0; i < ready.Materials.Length; i++) if (ready.Materials[i] == null) throw new InvalidOperationException("Replica material transaction is incomplete.");
                            ready.Renderer.sharedMaterials = ready.Materials;
                        }
                        ready.Object.SetActive(true); return true;
                    default: error = "Unsupported world packet: " + packet.Kind; return false;
                }
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }
        private static void DestroyNode(ReplicaNode node)
        {
            if (node.Object != null) Destroy(node.Object);
            if (node.OwnedCollisionMaterial != null) Destroy(node.OwnedCollisionMaterial);
        }
        private ReplicaNode Require(uint id) => _replicas.TryGetValue(id, out ReplicaNode node) ? node : throw new InvalidOperationException("Unknown world node.");
        private Mesh ResolveMesh(uint id) => id < 1000000 ? catalog.ResolveMesh(id) : _receivedMeshes.TryGetValue(id, out Mesh mesh) ? mesh : null;
        private void Spawn(in OnlinePacket packet)
        {
            if (packet.Id < 1000 || !_replicas.ContainsKey(packet.Id) && _replicas.Count >= maximumNodes) throw new InvalidOperationException("Invalid world node identity/budget.");
            if (_replicas.TryGetValue(packet.Id, out ReplicaNode previous)) { DestroyNode(previous); _replicas.Remove(packet.Id); }
            uint kind = packet.Flags & 255;
            if (kind < 1 || kind > 5 || packet.Value2 < 0 || packet.Value2 > 16) throw new InvalidOperationException("Invalid world node type.");
            var go = new GameObject("Online Earth " + packet.Id); go.SetActive(false); go.transform.SetParent(replicaRoot, false);
            go.layer = (int)((packet.Flags >> 8) & 31);
            var node = new ReplicaNode { Object = go };
            _replicas.Add(packet.Id, node);
            if (kind == 1)
            {
                Mesh mesh = ResolveMesh(packet.Aux); if (mesh == null) throw new InvalidOperationException("Visual mesh is not available.");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                node.Renderer = go.AddComponent<MeshRenderer>(); node.Materials = new Material[(int)packet.Value2];
                node.Renderer.shadowCastingMode = (UnityEngine.Rendering.ShadowCastingMode)((packet.Flags >> 18) & 3);
                node.Renderer.receiveShadows = (packet.Flags & (1u << 20)) != 0;
            }
            else
            {
                node.Body = go.AddComponent<Rigidbody>(); node.Body.isKinematic = true; node.Body.useGravity = false;
                if (kind == 2) node.Collider = go.AddComponent<BoxCollider>();
                else if (kind == 3) node.Collider = go.AddComponent<SphereCollider>();
                else if (kind == 4) node.Collider = go.AddComponent<CapsuleCollider>();
                else
                {
                    Mesh mesh = ResolveMesh(packet.Aux); if (mesh == null) throw new InvalidOperationException("Collision mesh is not available.");
                    MeshCollider collider = go.AddComponent<MeshCollider>(); collider.convex = (packet.Flags & (1u << 17)) != 0; collider.sharedMesh = mesh; node.Collider = collider;
                }
                node.Collider.isTrigger = (packet.Flags & (1u << 16)) != 0;
                if (OnlineCollisionMaterialState.IsInline(packet))
                    node.Collider.sharedMaterial = node.OwnedCollisionMaterial = OnlineCollisionMaterialState.Create(packet);
                else
                {
                    node.Collider.sharedMaterial = catalog.ResolvePhysics(packet.Seed);
                    if (packet.Seed != 0 && node.Collider.sharedMaterial == null) throw new InvalidOperationException("Collision material is unavailable.");
                }
            }
            ApplyPose(node, packet);
        }
        private static void ApplyPose(ReplicaNode node, in OnlinePacket packet)
        {
            if (Quaternion.Dot(packet.Rotation, packet.Rotation) < .9f || Quaternion.Dot(packet.Rotation, packet.Rotation) > 1.1f)
                throw new InvalidOperationException("Invalid world rotation.");
            if (node.Body != null && node.Object.activeInHierarchy) { node.Body.MovePosition(packet.A); node.Body.MoveRotation(packet.Rotation); }
            else node.Object.transform.SetPositionAndRotation(packet.A, packet.Rotation);
            node.Object.transform.localScale = packet.D;
        }
        private static void ApplyCollider(ReplicaNode node, in OnlinePacket packet)
        {
            if (node.Collider is BoxCollider box)
            {
                if (packet.B.x <= 0 || packet.B.y <= 0 || packet.B.z <= 0) throw new InvalidOperationException("Invalid box dimensions.");
                box.center = packet.A; box.size = packet.B;
            }
            else if (node.Collider is SphereCollider sphere)
            {
                if (packet.Value <= 0) throw new InvalidOperationException("Invalid sphere radius.");
                sphere.center = packet.A; sphere.radius = packet.Value;
            }
            else if (node.Collider is CapsuleCollider capsule)
            {
                if (packet.Value <= 0 || packet.Value2 <= 0 || packet.Flags > 2) throw new InvalidOperationException("Invalid capsule dimensions.");
                capsule.center = packet.A; capsule.radius = packet.Value; capsule.height = packet.Value2; capsule.direction = (int)packet.Flags;
            }
            else throw new InvalidOperationException("Unexpected primitive collider transaction.");
        }
        private void HideOriginals()
        {
            if (_originalsHidden) return;
            _originalsHidden = true; _mutatorEnabled = new bool[clientSuspendedMutators.Length];
            for (int i = 0; i < clientSuspendedMutators.Length; i++)
                if (clientSuspendedMutators[i] != null) _mutatorEnabled[i] = clientSuspendedMutators[i].enabled;
            GatherRoots();
            for (int root = 0; root < _activeRoots.Count; root++)
            {
                _renderers.Clear(); _activeRoots[root].GetComponentsInChildren(true, _renderers);
                for (int i = 0; i < _renderers.Count; i++) _originalRenderers.TryAdd(_renderers[i], _renderers[i].enabled);
                _colliders.Clear(); _activeRoots[root].GetComponentsInChildren(true, _colliders);
                for (int i = 0; i < _colliders.Count; i++) _originalColliders.TryAdd(_colliders[i], _colliders[i].enabled);
                _bodies.Clear(); _activeRoots[root].GetComponentsInChildren(true, _bodies);
                for (int i = 0; i < _bodies.Count; i++) _originalBodies.TryAdd(_bodies[i], _bodies[i].isKinematic);
                _behaviours.Clear(); _activeRoots[root].GetComponentsInChildren(true, _behaviours);
                for (int i = 0; i < _behaviours.Count; i++)
                    if (_behaviours[i] != null) _originalBehaviours.TryAdd(_behaviours[i], _behaviours[i].enabled);
            }
            // Runtime prewarming adds child gravity/fracture scripts after authoring.
            // Suspend every script inside the explicitly owned Earth source roots too.
            foreach (KeyValuePair<MonoBehaviour, bool> item in _originalBehaviours) if (item.Key != null) item.Key.enabled = false;
            for (int i = 0; i < clientSuspendedMutators.Length; i++) if (clientSuspendedMutators[i] != null) clientSuspendedMutators[i].enabled = false;
            foreach (KeyValuePair<Renderer, bool> item in _originalRenderers) if (item.Key != null) item.Key.enabled = false;
            foreach (KeyValuePair<Collider, bool> item in _originalColliders) if (item.Key != null) item.Key.enabled = false;
            foreach (KeyValuePair<Rigidbody, bool> item in _originalBodies) if (item.Key != null) item.Key.isKinematic = true;
        }
        public void Stop()
        {
            _configured = _syncStarted = false; _pendingCheckpointAck = false; WorldSynchronized = false; CheckpointGeometryReady = null;
            foreach (ReplicaNode node in _replicas.Values) DestroyNode(node);
            foreach (Mesh mesh in _receivedMeshes.Values) if (mesh != null) Destroy(mesh);
            _replicas.Clear(); _receivedMeshes.Clear(); _incomingMeshes.Clear(); _meshIds.Clear(); _meshHashes.Clear();
            _sources.Clear(); _ordered.Clear(); _outbox.Clear(); _seen.Clear(); _removed.Clear();
            if (_originalsHidden)
            {
                foreach (KeyValuePair<MonoBehaviour, bool> item in _originalBehaviours) if (item.Key != null) item.Key.enabled = item.Value;
                foreach (KeyValuePair<Renderer, bool> item in _originalRenderers) if (item.Key != null) item.Key.enabled = item.Value;
                foreach (KeyValuePair<Collider, bool> item in _originalColliders) if (item.Key != null) item.Key.enabled = item.Value;
                foreach (KeyValuePair<Rigidbody, bool> item in _originalBodies) if (item.Key != null) item.Key.isKinematic = item.Value;
                for (int i = 0; i < clientSuspendedMutators.Length; i++) if (clientSuspendedMutators[i] != null) clientSuspendedMutators[i].enabled = _mutatorEnabled[i];
            }
            _originalRenderers.Clear(); _originalColliders.Clear(); _originalBodies.Clear(); _originalBehaviours.Clear(); _originalsHidden = false;
            if (replicaRoot != null) replicaRoot.gameObject.SetActive(false);
        }
    }
}
