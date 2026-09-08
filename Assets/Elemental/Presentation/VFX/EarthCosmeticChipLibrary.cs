using Elemental.Presentation.Rendering;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    /// <summary>Four native-renderer-compatible, cold-created cosmetic silhouettes. Never used for repairable matter or colliders.</summary>
    internal static class EarthCosmeticChipLibrary
    {
        public static Mesh[] Build(Mesh authoredChip = null)
        {
            var meshes = new Mesh[4];
            for (int i = 0; i < meshes.Length; i++)
            {
                RumbleRockFamily family = i == 0 ? RumbleRockFamily.Slab :
                    i < 3 ? RumbleRockFamily.Wedge : RumbleRockFamily.Pebble;
                Mesh mesh = i == 0 && authoredChip != null ? Object.Instantiate(authoredChip) : RumbleRockMeshFactory.Build(
                    RumbleRockMeshFactory.CreateDefaultRecipe(1847 + i * 997, family), "Cosmetic Chip " + i);
                // Center the particle pivot and normalize only the longest axis; preserve aspect ratios.
                Vector3 center = mesh.bounds.center, size = mesh.bounds.size;
                float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                Vector3[] vertices = mesh.vertices;
                for (int v = 0; v < vertices.Length; v++) vertices[v] = (vertices[v] - center) / longest;
                mesh.vertices = vertices; mesh.RecalculateBounds(); meshes[i] = mesh;
            }
            return meshes;
        }
    }
}
