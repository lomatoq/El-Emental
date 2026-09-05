using Elemental.Simulation.Structures;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class EarthArenaPiece : MonoBehaviour, IEarthPhysicalTarget, IEarthDamageableStructure
    {
        [SerializeField] private EarthArenaStructure owner;
        [SerializeField] private Rigidbody body;
        [SerializeField] private Collider shape;
        [SerializeField] private GravityBody gravityBody;
        [SerializeField] private int pieceIndex;
        [SerializeField] private ushort pieceId;

        private bool _hasMagicOwner;
        private EarthMagicGripKind _magicOwner;

        public EarthArenaStructure Owner => owner;
        public uint StructureId => StableEarthId;
        public bool ApplyEarthImpact(in EarthStructureImpact impact) =>
            owner != null && owner.ApplyReleasedPieceImpact(pieceIndex, in impact);
        public int PieceIndex => pieceIndex;
        public Rigidbody Body => body;
        public uint StableEarthId => owner != null
            ? unchecked(owner.StructureId * 131u + (uint)Mathf.Max(1, pieceId))
            : 0u;
        public EarthPhysicalTargetHandle TargetHandle => owner != null
            ? new EarthPhysicalTargetHandle(StableEarthId, owner.Generation)
            : default;
        public float EarthMass => body != null ? Mathf.Max(0.1f, body.mass) : 0f;
        public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.WallPiece;
        public bool IsEarthTargetValid => owner != null && owner.IsPieceReleased(pieceIndex) &&
                                          gameObject.activeInHierarchy && body != null &&
                                          shape != null && shape.enabled;

        public void Configure(
            EarthArenaStructure configuredOwner,
            int configuredIndex,
            EarthPieceId configuredId,
            Rigidbody configuredBody,
            Collider configuredShape,
            GravityBody configuredGravity)
        {
            owner = configuredOwner;
            pieceIndex = configuredIndex;
            pieceId = configuredId.Value;
            body = configuredBody;
            shape = configuredShape;
            gravityBody = configuredGravity;
            _hasMagicOwner = false;

            // EarthArenaStructure receives the exact physical mass baked from the
            // fracture volume. Convert it once into the same gameplay scale used by
            // loose rocks instead of retaining a separate arena-only density scale.
            if (body != null)
                body.mass = EarthMatterMassRuntime.ResolveFromAuthoredPhysicalMass(body.mass);
        }

        public void OnEarthMagicGrabbed(EarthMagicGripKind grip)
        {
            if (_hasMagicOwner || owner == null || !owner.TryAcquirePiece(pieceIndex)) return;
            _hasMagicOwner = true;
            _magicOwner = grip;
            body?.WakeUp();
        }

        public void OnEarthMagicReleased(EarthMagicGripKind grip)
        {
            if (!_hasMagicOwner || _magicOwner != grip) return;
            _hasMagicOwner = false;
            owner?.NotifyPieceMagicReleased(pieceIndex);
            body?.WakeUp();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (owner != null && IsEarthTargetValid)
                owner.HandlePieceCollision(pieceIndex, collision);
        }

        private void OnCollisionStay(Collision collision) => owner?.ReportPieceFriction(pieceIndex, collision);
    }
}
