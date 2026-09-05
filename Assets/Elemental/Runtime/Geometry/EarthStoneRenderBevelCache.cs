using System.Collections.Generic;
using Elemental.Runtime.World;
using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    /// <summary>Owner-local prepared render meshes. Source/collider geometry and materials remain authoritative.</summary>
    public sealed class EarthStoneRenderBevelCache
    {
        private readonly Dictionary<Mesh, Mesh> _copies = new();

        private static readonly string[] ApprovedPrefixes =
        {
            "V5_Boulder_", "V5_Slab_", "V5_Wedge_", "V5_Pebble_", "V5_Pillar_",
            "V5_Physics_Boulder_", "V5_Physics_Slab_", "V5_Physics_Wedge_",
            "V5_Physics_Pebble_", "V5_Physics_Pillar_"
        };

        public static bool HasApprovedAuthoredBevel(Mesh source)
        {
            if (source == null) return false;
            string name = source.name;
            for (int index = 0; index < ApprovedPrefixes.Length; index++)
                if (name.StartsWith(ApprovedPrefixes[index], System.StringComparison.Ordinal)) return true;
            return false;
        }

        public Mesh Get(Mesh source, EarthStoneBevelProfile profile)
        {
            if (source == null || HasApprovedAuthoredBevel(source)) return source;
            if (_copies.ContainsValue(source)) return source; // Repeated OnEnable sees its own prior render copy.
            if (profile != null && (profile.Width <= 0f || profile.MaxLocalEdgeFraction <= 0f)) return source;
            if (_copies.TryGetValue(source, out Mesh cached)) return cached;
            Mesh copy = EarthFractureBevelMeshBuilder.Create(source, profile);
            if (copy != null && copy != source)
                copy.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            _copies[source] = copy;
            return copy;
        }

        /// <summary>Use only when cast preparation rebuilt source vertices, never on impact or in steady-state.</summary>
        public Mesh Rebuild(Mesh source, EarthStoneBevelProfile profile)
        {
            if (source != null && _copies.TryGetValue(source, out Mesh old))
            {
                _copies.Remove(source);
                if (old != source) DestroyOwned(old);
            }
            return Get(source, profile);
        }

        public void Clear()
        {
            foreach (KeyValuePair<Mesh, Mesh> pair in _copies)
                if (pair.Value != pair.Key) DestroyOwned(pair.Value);
            _copies.Clear();
        }

        private static void DestroyOwned(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);
        }
    }
}
