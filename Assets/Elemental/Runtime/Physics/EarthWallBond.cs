using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public readonly struct EarthWallBond
    {
        public EarthWallBond(
            ConfigurableJoint joint,
            int pieceA,
            int pieceB,
            float normalizedContactArea,
            bool foundation)
        {
            Joint = joint;
            PieceA = pieceA;
            PieceB = pieceB;
            NormalizedContactArea = Mathf.Max(0.0001f, normalizedContactArea);
            Foundation = foundation;
        }

        public ConfigurableJoint Joint { get; }
        public int PieceA { get; }
        public int PieceB { get; }
        public float NormalizedContactArea { get; }
        public bool Foundation { get; }
    }
}
