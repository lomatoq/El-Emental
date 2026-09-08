using System;
using UnityEngine;

namespace Elemental.Runtime.Fire
{
    // Explicit owner for lab / non-Earth surfaces. Destruction adapters call InvalidateGeometry
    // synchronously before changing shape, and BeginGeneration before pooled reuse.
    [DisallowMultipleComponent]
    public sealed class FireSurfaceBinding : MonoBehaviour
    {
        [SerializeField] private uint stableId;
        [SerializeField] private uint generation = 1;
        private uint _revision = 1;
        public uint StableId => stableId;
        public uint Generation => generation;
        public uint Revision => _revision;
        public bool IsCurrent => isActiveAndEnabled && stableId != 0 && generation != 0 && _revision != 0;
        public void Configure(uint id, uint incarnation = 1)
        {
            if (id == 0 || incarnation == 0) throw new ArgumentOutOfRangeException(nameof(id));
            stableId = id; generation = incarnation; InvalidateGeometry();
        }
        public void InvalidateGeometry()
        {
            if (_revision == uint.MaxValue) throw new InvalidOperationException("Fire surface revision exhausted; create a new canonical identity.");
            _revision++;
        }
        public void BeginGeneration()
        {
            if (generation == uint.MaxValue) throw new InvalidOperationException("Fire surface generation exhausted; assign a new stable identity.");
            generation++; InvalidateGeometry();
        }
        private void OnDisable() => InvalidateGeometry();
    }
}
