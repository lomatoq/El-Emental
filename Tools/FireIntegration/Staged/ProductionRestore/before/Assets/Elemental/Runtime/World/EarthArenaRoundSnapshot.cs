using System;
using System.Collections.Generic;
using Elemental.Runtime.Physics;
using Elemental.Runtime.Matter;
using Elemental.Simulation.Matter;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Runtime.World
{
    /// <summary>Runtime baseline of this arena, not a replacement scene or a new authored profile.</summary>
    public sealed class EarthArenaRoundSnapshot
    {
        private readonly Scene _scene;
        private readonly VoxelPlanetBehaviour _planet;
        private readonly byte[] _terrain;
        private readonly List<Node> _nodes = new();
        private readonly List<Matter> _matter = new();
        private readonly EarthArenaStructure[] _structures;
        private readonly EarthDestructibleDecorRock[] _decor;
        private readonly float[] _integrity;
        private sealed class Matter { public EarthMatterIdentity Identity; public EarthMatterKernelBehaviour Kernel; public EarthMatterRecord Record; public Rigidbody Body; }
        private sealed class Node
        {
            public Transform Transform, Parent; public Vector3 Position, Scale; public Quaternion Rotation; public bool Active;
            public Renderer[] Renderers; public bool[] Rendered; public Collider[] Colliders; public bool[] Colliding;
            public Rigidbody Body; public bool Kinematic, Detect; public RigidbodyConstraints Constraints;
            public GravityBody Gravity; public bool GravityEnabled;
            public Node(Transform value)
            {
                Transform = value; Parent = value.parent; Position = value.localPosition; Rotation = value.localRotation; Scale = value.localScale; Active = value.gameObject.activeSelf;
                Renderers = value.GetComponents<Renderer>(); Rendered = Array.ConvertAll(Renderers, r => r.enabled);
                Colliders = value.GetComponents<Collider>(); Colliding = Array.ConvertAll(Colliders, c => c.enabled);
                Body = value.GetComponent<Rigidbody>(); if (Body != null) { Kinematic = Body.isKinematic; Detect = Body.detectCollisions; Constraints = Body.constraints; }
                Gravity = value.GetComponent<GravityBody>(); GravityEnabled = Gravity != null && Gravity.enabled;
            }
            public void Restore()
            {
                if (Transform == null) throw new InvalidOperationException("An authored arena source was destroyed; cannot silently replace its identity.");
                if (Body != null && !Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
                if (Transform.parent != Parent) Transform.SetParent(Parent, false);
                Transform.localPosition = Position; Transform.localRotation = Rotation; Transform.localScale = Scale;
                Transform.gameObject.SetActive(Active);
                for (int i = 0; i < Renderers.Length; i++) if (Renderers[i] != null) Renderers[i].enabled = Rendered[i];
                for (int i = 0; i < Colliders.Length; i++) if (Colliders[i] != null) Colliders[i].enabled = Colliding[i];
                if (Body != null) { Body.position = Transform.position; Body.rotation = Transform.rotation; Body.isKinematic = Kinematic; Body.detectCollisions = Detect; Body.constraints = Constraints; }
                if (Gravity != null) Gravity.enabled = GravityEnabled;
            }
        }
        public bool Ready => _planet != null && _planet.GeometryReady;
        public ulong TerrainHash => VoxelPlanetBehaviour.ArenaSnapshotHash(_terrain);
        public EarthArenaRoundSnapshot(VoxelPlanetBehaviour planet)
        {
            _planet = planet; _scene = planet.gameObject.scene; _terrain = planet.CaptureArenaSnapshot();
            _structures = Find<EarthArenaStructure>(); _decor = Find<EarthDestructibleDecorRock>(); _integrity = new float[_decor.Length];
            var seen = new HashSet<Transform>();
            foreach (EarthArenaStructure structure in _structures) CaptureNodes(structure.transform, seen);
            for (int i = 0; i < _decor.Length; i++) { _integrity[i] = _decor[i].CaptureArenaIntegrity(); CaptureNodes(_decor[i].transform, seen); }
            foreach (EarthMatterIdentity identity in Find<EarthMatterIdentity>())
                if (identity.TryRead(out EarthMatterRecord record)) _matter.Add(new Matter { Identity = identity, Kernel = identity.Kernel, Record = record, Body = identity.Body });
        }
        private void CaptureNodes(Transform root, HashSet<Transform> seen)
        { foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (seen.Add(child)) _nodes.Add(new Node(child)); }
        public static T[] SceneComponents<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
            return found.ToArray();
        }
        private T[] Find<T>() where T : Component => SceneComponents<T>(_scene);
        public void Restore()
        {
            // Stop ongoing operations before retiring records or cancelling receipts.
            foreach (MagicExecutor executor in Find<MagicExecutor>()) executor.CancelForArenaRestore();
            foreach (EarthMatterReturnController controller in Find<EarthMatterReturnController>()) controller.CancelForArenaRestore();
            foreach (EarthSurfController surf in Find<EarthSurfController>()) surf.Cancel();
            foreach (EarthResonanceController resonance in Find<EarthResonanceController>()) resonance.Cancel();
            foreach (EarthArmorController armor in Find<EarthArmorController>()) armor.ResetForArenaRestore();
            foreach (EarthMatterKernelBehaviour kernel in Find<EarthMatterKernelBehaviour>()) kernel.Registry.RetireForArenaRestore();
            foreach (EarthMatterIdentity identity in Find<EarthMatterIdentity>()) identity.ReleaseRetiredRepresentation();
            foreach (EarthFragmentPool pool in Find<EarthFragmentPool>()) pool.ResetForArenaRestore();
            foreach (EarthRockDebris debris in Find<EarthRockDebris>()) debris.ResetPiece();
            foreach (EarthWall wall in Find<EarthWall>()) wall.ReturnToPoolAsTransientProxy();
            foreach (EarthPlatform platform in Find<EarthPlatform>()) platform.PrepareForPool();
            foreach (EarthPillarWaveColumn column in Find<EarthPillarWaveColumn>()) column.ResetColumn();
            foreach (Node node in _nodes) node.Restore();
            foreach (EarthArenaStructure structure in _structures) structure.RestoreArenaStructure();
            for (int i = 0; i < _decor.Length; i++) _decor[i].RestoreArenaIntegrity(_integrity[i]);
            foreach (Matter matter in _matter)
                if (matter.Identity != null && !matter.Identity.Configure(matter.Kernel, matter.Record, matter.Body))
                    throw new InvalidOperationException("Could not restore canonical authored arena matter.");
            _planet.RestoreArenaSnapshot(_terrain);
            UnityEngine.Physics.SyncTransforms();
        }
    }
}
