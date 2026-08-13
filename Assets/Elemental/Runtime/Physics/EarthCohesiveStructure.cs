using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthCohesiveStructure : MonoBehaviour
    {
        private bool[] _pieceHeld = System.Array.Empty<bool>();

        public bool IsFractured { get; private set; }
        public int PieceCount => _pieceHeld.Length;

        public void Configure(int pieceCount)
        {
            int count = Mathf.Max(0, pieceCount);
            if (_pieceHeld.Length != count) _pieceHeld = new bool[count];
            else System.Array.Clear(_pieceHeld, 0, _pieceHeld.Length);
            IsFractured = false;
        }

        public void BeginFracture() => IsFractured = true;

        public void ResetCohesion()
        {
            IsFractured = false;
            System.Array.Clear(_pieceHeld, 0, _pieceHeld.Length);
        }

        public bool AcquirePiece(int index)
        {
            if (!IsFractured || index < 0 || index >= _pieceHeld.Length) return false;
            _pieceHeld[index] = true;
            return true;
        }

        public void ReleasePiece(int index)
        {
            if (index >= 0 && index < _pieceHeld.Length) _pieceHeld[index] = false;
        }

        public bool IsPieceHeld(int index) =>
            index >= 0 && index < _pieceHeld.Length && _pieceHeld[index];
    }
}
