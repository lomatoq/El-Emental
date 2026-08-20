using Elemental.Runtime.Matter;
using Elemental.Simulation.Matter;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class EarthArmorPiece : MonoBehaviour, IEarthPhysicalTarget
    {
        private const int ControlledMagicLayer = 2;
        private EarthArmorController _owner;
        private int _pieceIndex;
        private uint _generation;
        private float _releasedElapsed;
        private float _restSeconds;
        private float _shrinkSeconds;
        private bool _released;
        private int _gripCount;
        private Vector3 _fullScale;
        private MaterialPropertyBlock _visualProperties;
        private EarthMatterIdentity _matterIdentity;

        public Rigidbody Body { get; private set; }
        public Mesh OwnedMesh { get; private set; }
        public Vector3 SourcePosition { get; private set; }
        public uint StableEarthId => 0xA0000000u + (uint)(_pieceIndex + 1);
        public EarthPhysicalTargetHandle TargetHandle => new EarthPhysicalTargetHandle(StableEarthId, _generation);
        public float EarthMass => Body != null ? Body.mass : 0f;
        public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.ArmorPiece;
        public bool IsEarthTargetValid => gameObject.activeInHierarchy && Body != null;
        public Collider PieceCollider { get; private set; }
        public bool IsReleased => _released;
        public Renderer VisualRenderer { get; private set; }
        public bool CameraSuppressed => VisualRenderer != null && VisualRenderer.forceRenderingOff;
        public EarthMatterIdentity MatterIdentity =>
            _matterIdentity != null ? _matterIdentity : (_matterIdentity = GetComponent<EarthMatterIdentity>());

        public void Configure(
            EarthArmorController owner,
            int pieceIndex,
            Rigidbody body,
            Collider pieceCollider,
            Mesh ownedMesh)
        {
            _owner = owner;
            _pieceIndex = pieceIndex;
            Body = body;
            PieceCollider = pieceCollider;
            OwnedMesh = ownedMesh;
            VisualRenderer = GetComponent<Renderer>();
            ApplyVisualVariation();
        }

        public void Activate(uint generation, Vector3 source, Quaternion rotation)
        {
            _generation = generation;
            SourcePosition = source;
            _released = false;
            _releasedElapsed = 0f;
            _gripCount = 0;
            SetCameraSuppressed(false);
            gameObject.layer = ControlledMagicLayer;
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            Body.isKinematic = true;
            Body.detectCollisions = true;
            _owner?.ReapplyCasterCollisionIgnores(PieceCollider);
            transform.SetPositionAndRotation(source, rotation);
            float width = Mathf.Lerp(0.12f, 0.22f, (_pieceIndex % 5) / 4f);
            float height = Mathf.Lerp(0.14f, 0.27f, ((_pieceIndex * 3) % 7) / 6f);
            float thickness = Mathf.Lerp(0.045f, 0.080f, (_pieceIndex % 3) / 2f);
            _fullScale = new Vector3(width, thickness, height);
            transform.localScale = _fullScale;
            gameObject.SetActive(true);
        }

        public void SetFormationScale(float multiplier)
        {
            if (_released) return;
            transform.localScale = _fullScale * Mathf.Clamp(multiplier, 0.65f, 2.2f);
        }

        public void SetBaseScale(Vector3 scale)
        {
            if (_released) return;
            _fullScale = new Vector3(
                Mathf.Clamp(scale.x, 0.14f, 1.15f),
                Mathf.Clamp(scale.y, 0.055f, 0.32f),
                Mathf.Clamp(scale.z, 0.16f, 1.15f));
            transform.localScale = _fullScale;
            if (Body != null)
                Body.mass = Mathf.Max(2f, _fullScale.x * _fullScale.y * _fullScale.z * 180f);
        }

        public void RegisterMatter(
            EarthMatterKernelBehaviour kernel,
            Vector3 sourceLocalPoint,
            uint sourceRevision,
            EarthOwnerId owner)
        {
            if (kernel == null || Body == null) return;
            float volume = Mathf.Max(0.000001f, _fullScale.x * _fullScale.y * _fullScale.z * 0.62f);
            var source = new EarthSourceProvenance(
                EarthSourceKind.TerrainEdit,
                1u,
                1,
                _pieceIndex,
                sourceRevision,
                new float3(sourceLocalPoint.x, sourceLocalPoint.y, sourceLocalPoint.z),
                volume,
                EarthProvenanceFlags.VolumeReserved);
            _matterIdentity = EarthMatterRuntimeBridge.EnsureIdentity(
                this,
                kernel,
                Body,
                EarthMatterPhase.Forming,
                EarthRepresentationTier.HeroPhysical,
                EarthMaterialKind.Stone,
                EarthShapeSemantic.ArmorPlate,
                volume,
                Body.mass,
                source,
                owner);
        }

        public void Move(Vector3 position, Quaternion rotation)
        {
            if (!gameObject.activeSelf || _released) return;
            Body.MovePosition(position);
            Body.MoveRotation(rotation);
        }

        public void Release(Vector3 velocity, float restSeconds, float shrinkSeconds)
        {
            if (!gameObject.activeSelf) return;
            _released = true;
            _releasedElapsed = 0f;
            _restSeconds = Mathf.Max(0f, restSeconds);
            _shrinkSeconds = Mathf.Max(0.05f, shrinkSeconds);
            // Released plates remain valid physical targets, but they never become
            // camera occluders. Layer 2 still collides with gameplay and is included
            // by the explicit earth target queries (~0).
            gameObject.layer = ControlledMagicLayer;
            Body.isKinematic = false;
            _owner?.ReapplyCasterCollisionIgnores(PieceCollider);
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.mass = Mathf.Max(2f, _fullScale.x * _fullScale.y * _fullScale.z * 180f);
            Body.linearVelocity = velocity;
            Body.angularVelocity = Vector3.zero;
            Body.WakeUp();
            if (MatterIdentity != null && MatterIdentity.TryRead(out EarthMatterRecord record) &&
                (record.Phase == EarthMatterPhase.Forming || record.Phase == EarthMatterPhase.Controlled))
                MatterIdentity.TryTransition(EarthMatterPhase.FreeDynamic);
        }

        public bool TryBeginRecall()
        {
            if (!gameObject.activeSelf || !_released || _gripCount > 0 || Body == null)
                return false;
            _released = false;
            _releasedElapsed = 0f;
            SetCameraSuppressed(false);
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.isKinematic = true;
            Body.detectCollisions = true;
            transform.localScale = _fullScale;
            _owner?.ReapplyCasterCollisionIgnores(PieceCollider);
            if (MatterIdentity != null && MatterIdentity.TryRead(out EarthMatterRecord record) &&
                record.Phase == EarthMatterPhase.FreeDynamic)
                MatterIdentity.TryTransition(EarthMatterPhase.Controlled);
            return true;
        }

        public void ResetToPool()
        {
            MatterIdentity?.RetireTransientRepresentation();
            SetCameraSuppressed(false);
            if (Body != null)
            {
                Body.detectCollisions = false;
                if (!Body.isKinematic)
                {
                    Body.linearVelocity = Vector3.zero;
                    Body.angularVelocity = Vector3.zero;
                }
                Body.isKinematic = true;
            }
            gameObject.layer = 0;
            gameObject.SetActive(false);
        }

        public void SetCameraSuppressed(bool suppressed)
        {
            if (VisualRenderer != null) VisualRenderer.forceRenderingOff = suppressed;
        }

        private void ApplyVisualVariation()
        {
            if (VisualRenderer == null || VisualRenderer.sharedMaterial == null) return;
            _visualProperties ??= new MaterialPropertyBlock();
            VisualRenderer.GetPropertyBlock(_visualProperties);
            Material shared = VisualRenderer.sharedMaterial;
            Color exterior = shared.HasProperty("_ExteriorColor")
                ? shared.GetColor("_ExteriorColor")
                : new Color(0.38f, 0.245f, 0.155f, 1f);
            float brightness = Mathf.Lerp(0.76f, 1.24f, Hash01((uint)(_pieceIndex * 113 + 17)));
            float warmth = Mathf.Lerp(-0.055f, 0.055f, Hash01((uint)(_pieceIndex * 151 + 29)));
            Color varied = new Color(
                Mathf.Clamp01(exterior.r * brightness + warmth),
                Mathf.Clamp01(exterior.g * brightness + warmth * 0.42f),
                Mathf.Clamp01(exterior.b * brightness - warmth * 0.18f),
                exterior.a);
            _visualProperties.SetColor("_ExteriorColor", varied);
            _visualProperties.SetFloat("_MacroFrequency",
                Mathf.Lerp(0.052f, 0.112f, Hash01((uint)(_pieceIndex * 173 + 37))));
            _visualProperties.SetFloat("_MineralAmount",
                Mathf.Lerp(0.018f, 0.075f, Hash01((uint)(_pieceIndex * 197 + 43))));
            VisualRenderer.SetPropertyBlock(_visualProperties);
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private void Update()
        {
            if (!_released || _gripCount > 0) return;
            _releasedElapsed += Time.deltaTime;
            if (_releasedElapsed <= _restSeconds) return;
            float shrink01 = Mathf.Clamp01((_releasedElapsed - _restSeconds) / _shrinkSeconds);
            transform.localScale = Vector3.Lerp(_fullScale, Vector3.zero, shrink01);
            Body.WakeUp();
            if (shrink01 >= 1f) ResetToPool();
        }

        public void OnEarthMagicGrabbed(EarthMagicGripKind grip)
        {
            _gripCount++;
            transform.localScale = _fullScale;
            Body.WakeUp();
            MatterIdentity?.TryTransition(EarthMatterPhase.Controlled);
        }

        public void OnEarthMagicReleased(EarthMagicGripKind grip)
        {
            _gripCount = Mathf.Max(0, _gripCount - 1);
            Body.WakeUp();
            if (_gripCount == 0 && MatterIdentity != null &&
                MatterIdentity.TryRead(out EarthMatterRecord record) &&
                record.Phase == EarthMatterPhase.Controlled)
                MatterIdentity.TryTransition(EarthMatterPhase.FreeDynamic);
        }
    }
}
