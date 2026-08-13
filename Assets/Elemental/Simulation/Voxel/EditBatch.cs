using System;

namespace Elemental.Simulation.Voxel
{
    public sealed class EditBatch
    {
        private readonly SdfEdit[] _edits;

        public EditBatch(params SdfEdit[] edits)
        {
            if (edits == null)
            {
                throw new ArgumentNullException(nameof(edits));
            }

            _edits = new SdfEdit[edits.Length];
            Array.Copy(edits, _edits, edits.Length);
        }

        public int Count => _edits.Length;
        public SdfEdit this[int index] => _edits[index];
    }
}
