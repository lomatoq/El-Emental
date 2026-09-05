using System;
using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    /// <summary>Editor-prepared convex partitions, including recursive child plans.</summary>
    [PreferBinarySerialization]
    public sealed class EarthConvexFractureCacheAsset : ScriptableObject
    {
        public const int CurrentRevision = 3;
        [Serializable] public struct Piece
        {
            public Mesh Collider, Render;
            public Vector3 Center;
            public float Volume;
        }
        [Serializable] public struct Plan
        {
            public Mesh Source;
            public int Count;
            public string SourceSignature;
            public Piece[] Pieces;
        }
        [SerializeField] private int revision;
        [SerializeField] private Plan[] plans = Array.Empty<Plan>();
        public Plan[] Plans => plans;
        public bool Current => revision == CurrentRevision;
        public void Configure(Plan[] value) { revision = CurrentRevision; plans = value; }

        // Only cold source validation uses mesh arrays; gameplay cache hits allocate nothing.
        public static string Signature(Mesh mesh)
        {
            if (mesh == null || !mesh.isReadable) return "unreadable";
            ulong hash = 14695981039346656037UL;
            unchecked
            {
                foreach (Vector3 p in mesh.vertices)
                {
                    hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(p.x)) * 1099511628211UL;
                    hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(p.y)) * 1099511628211UL;
                    hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(p.z)) * 1099511628211UL;
                }
            }
            return hash.ToString("X16");
        }
    }
}
