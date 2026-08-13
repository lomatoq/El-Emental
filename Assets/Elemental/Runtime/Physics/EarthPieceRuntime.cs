using Elemental.Simulation.Structures;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public class EarthPieceRuntime : MonoBehaviour, IEarthPhysicalTarget
    {
        private int _lastImpactFrame = -100;
        private EarthStructureRuntime _structure;
        private EarthPieceId _pieceId;
        private EarthPieceFaceMetadata _faceMetadata;
        private EarthStructureId _structureId;
        private uint _generation;
        private Rigidbody _body;
        private bool _hasMagicOwner;
        private EarthMagicGripKind _magicOwner;

        public EarthWall Owner { get; private set; }
        public EarthStructureRuntime Structure => _structure;
        public EarthStructureId StructureId => _structureId;
        public uint Generation => _generation;
        public int PieceIndex { get; private set; }
        public EarthPieceId PieceId => _pieceId;
        public EarthPieceFaceMetadata FaceMetadata => _faceMetadata;
        public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());
        public uint StableEarthId => Owner != null
            ? (Owner.WallId * 100u) + (uint)Mathf.Max(0, PieceIndex) + 1u
            : 0u;
        public EarthPhysicalTargetHandle TargetHandle => Owner != null
            ? new EarthPhysicalTargetHandle(StableEarthId, Owner.TargetHandle.Generation)
            : default;
        public float EarthMass => Body != null ? Body.mass : 0f;
        public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.WallPiece;
        public bool IsEarthTargetValid => Owner != null && Owner.IsCollapsing &&
                                          gameObject.activeSelf && Body != null;
        public bool HasMagicOwner => _hasMagicOwner;
        public EarthMagicGripKind MagicOwner => _magicOwner;

        public void Configure(EarthWall owner, int pieceIndex)
        {
            Owner = owner;
            PieceIndex = pieceIndex;
            _body = GetComponent<Rigidbody>();
        }

        public void ConfigureCanonical(
            EarthWall owner,
            EarthStructureRuntime structure,
            int pieceIndex,
            EarthPieceId pieceId,
            EarthPieceFaceMetadata faceMetadata)
        {
            Configure(owner, pieceIndex);
            _structure = structure;
            _pieceId = pieceId;
            _faceMetadata = faceMetadata;
        }

        public void ResetExact(
            in EarthPieceDefinition definition,
            EarthStructureId structureId,
            uint generation)
        {
            _structureId = structureId;
            _generation = generation;
            _hasMagicOwner = false;
            transform.SetParent(Owner != null ? Owner.transform : transform.parent, false);
            transform.localPosition = ToVector3(definition.RestLocalPosition);
            quaternion rotation = definition.RestLocalRotation;
            transform.localRotation = new Quaternion(
                rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w);
            transform.localScale = ToVector3(definition.RestLocalScale);
            Rigidbody body = Body;
            if (body != null)
            {
                body.detectCollisions = false;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
            }
            gameObject.SetActive(false);
        }

        public void OnEarthMagicGrabbed(EarthMagicGripKind grip)
        {
            if (_hasMagicOwner) return;
            bool acquired = grip == EarthMagicGripKind.Repair
                ? Owner != null && Owner.AcquirePieceForRepair(PieceIndex)
                : Owner != null && Owner.AcquirePieceForMagic(PieceIndex);
            if (!acquired) return;
            _hasMagicOwner = true;
            _magicOwner = grip;
        }

        public void OnEarthMagicReleased(EarthMagicGripKind grip)
        {
            if (!_hasMagicOwner || _magicOwner != grip) return;
            if (grip == EarthMagicGripKind.Repair)
                Owner?.ReleasePieceFromRepair(PieceIndex);
            else
                Owner?.ReleasePieceFromMagic(PieceIndex);
            _hasMagicOwner = false;
        }

        public bool TryAcquireForRepair()
        {
            if (_hasMagicOwner || Owner == null || !Owner.AcquirePieceForRepair(PieceIndex)) return false;
            _hasMagicOwner = true;
            _magicOwner = EarthMagicGripKind.Repair;
            return true;
        }

        public void ReleaseFromRepair()
        {
            OnEarthMagicReleased(EarthMagicGripKind.Repair);
        }

        protected virtual void OnCollisionEnter(Collision collision)
        {
            if (Owner == null || Time.frameCount - _lastImpactFrame < 2) return;
            _lastImpactFrame = Time.frameCount;
            Owner.HandlePieceCollision(PieceIndex, collision);
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
