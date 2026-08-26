using Elemental.Runtime.Matter;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Matter;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>
    /// Prewarmed physical shell for one fracture cell. Kept in its own file so the
    /// scene can serialize all pooled shells without creating missing-script entries.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class EarthPlatformPiece : MonoBehaviour, IEarthPhysicalTarget
    {
        [SerializeField] private EarthPlatform owner;
        [SerializeField] private int pieceIndex;
        [SerializeField] private Rigidbody body;

        public EarthPlatform Owner => owner;
        public int PieceIndex => pieceIndex;
        public Rigidbody Body => body;
        public uint StableEarthId => Owner != null
            ? (Owner.PlatformId * 100u) + (uint)Mathf.Max(0, PieceIndex) + 1u
            : 0u;
        public EarthPhysicalTargetHandle TargetHandle => Owner != null
            ? new EarthPhysicalTargetHandle(StableEarthId, Owner.Generation)
            : default;
        public float EarthMass => Body != null ? Body.mass : 0f;
        public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.PlatformPiece;
        public bool IsEarthTargetValid => Owner != null && Owner.IsFractured &&
                                          gameObject.activeSelf && Body != null;

        public void Configure(EarthPlatform configuredOwner, int configuredPieceIndex)
        {
            owner = configuredOwner;
            pieceIndex = configuredPieceIndex;
            body = GetComponent<Rigidbody>();
        }

        public void OnEarthMagicGrabbed(EarthMagicGripKind grip)
        {
            Owner?.AcquirePiece(PieceIndex);
            GetComponent<EarthMatterIdentity>()?.TryTransition(EarthMatterPhase.Controlled);
        }

        public void OnEarthMagicReleased(EarthMagicGripKind grip)
        {
            Owner?.ReleasePiece(PieceIndex);
            EarthMatterIdentity identity = GetComponent<EarthMatterIdentity>();
            if (identity != null && identity.TryRead(out EarthMatterRecord record) &&
                record.Phase == EarthMatterPhase.Controlled)
                identity.TryTransition(EarthMatterPhase.FreeDynamic);
        }

        private void OnCollisionEnter(Collision collision) =>
            Owner?.ReportPieceImpact(PieceIndex, collision);
    }
}
