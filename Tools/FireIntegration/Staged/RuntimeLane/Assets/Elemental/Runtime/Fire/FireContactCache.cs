using Elemental.Simulation.Fire;

namespace Elemental.Runtime.Fire
{
    public sealed class FireContactCache
    {
        private readonly FireSurfaceAnchor[] _anchors = new FireSurfaceAnchor[FireWorld.MaximumContacts];
        private readonly float[] _confirmed = new float[FireWorld.MaximumContacts];
        private int _count;
        public int Saturations { get; private set; }
        public void Clear() { System.Array.Clear(_anchors, 0, _anchors.Length); _count = 0; }
        public bool Add(in FireSurfaceAnchor anchor, float time)
        {
            // Spatially distinct discs on the same face are not merged across an unknown edge.
            for (int i = 0; i < _count; i++)
                if (_anchors[i].Handle.Equals(anchor.Handle) &&
                    (_anchors[i].LocalPoint - anchor.LocalPoint).sqrMagnitude < 0.01f)
                { _anchors[i] = anchor; _confirmed[i] = time; return true; }
            if (_count == _anchors.Length) { Saturations++; return false; }
            _anchors[_count] = anchor; _confirmed[_count++] = time; return true;
        }
        public int CopyCurrent(FireSurfaceResolver resolver, float time, FireContactPatch[] output)
        {
            int write = 0;
            for (int i = 0; i < _count; i++)
            {
                if (time - _confirmed[i] > 0.05f || !resolver.TryRefresh(in _anchors[i], out FireContactPatch patch)) continue;
                _anchors[write] = _anchors[i]; _confirmed[write] = _confirmed[i]; output[write++] = patch;
            }
            for (int i = write; i < _count; i++) _anchors[i] = default;
            _count = write;
            for (int i = write; i < output.Length; i++) output[i] = default;
            return write;
        }
    }
}
