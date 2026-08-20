using System.Collections.Generic;
using Elemental.Runtime.Geometry;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>
    /// Builds the six deterministic geological cell families used by the pooled web wave.
    /// Meshes are created once when the pool wakes; casts only move and scale them.
    /// </summary>
    public static class EarthWebWaveCellMeshFactory
    {
        private const int TopSubdivisions = 2;

        public static Mesh Create(int poolIndex)
        {
            int family = Mathf.Abs(poolIndex) % 6;
            int sides = 3 + ((poolIndex * 5 + family * 3) % 6);
            float phase = Hash01((uint)(poolIndex + 1) * 0x9E3779B9u) * Mathf.PI * 2f;
            var footprint = new Vector2[sides];
            for (int index = 0; index < sides; index++)
            {
                float angle = phase + (index * Mathf.PI * 2f / sides);
                float radialNoise = Mathf.Lerp(
                    0.72f,
                    1.04f,
                    Hash01((uint)(poolIndex * 97 + index * 31 + 17)));
                float xBias = Mathf.Lerp(0.82f, 1.08f, Hash01((uint)(family * 41 + 7)));
                footprint[index] = new Vector2(
                    Mathf.Cos(angle) * 0.5f * radialNoise * xBias,
                    Mathf.Sin(angle) * 0.5f * radialNoise);
            }

            var mesh = new Mesh
            {
                name = $"EarthWebCell_{poolIndex:00}",
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
            };
            var geologicalFootprint = new float2[footprint.Length];
            for (int index = 0; index < footprint.Length; index++)
                geologicalFootprint[index] = new float2(footprint[index].x, footprint[index].y);
            Vector3[] vertices = ConfigureSharedBoundaryCell(
                mesh,
                geologicalFootprint,
                (uint)(poolIndex + 701),
                1f);
            // Shared wave cells are authored from bottom=-thickness to top=0. Generic
            // armor/fragment stones need their pivot through the centre so a plate can
            // hug a body collider instead of floating one full thickness away.
            for (int index = 0; index < vertices.Length; index++) vertices[index].y += 0.5f;
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.name = $"EarthFacetedCell_{poolIndex:00}";
            mesh.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            EarthMeshIntegrityGate.ValidateInPlaceOrUseFallback(
                mesh,
                EarthMeshIntegrityPolicy.ConvexCollider,
                mesh.name,
                mesh.bounds);
            return mesh;
        }

        public static Vector3[] ConfigureSharedBoundaryCell(
            Mesh mesh,
            float2[] footprint,
            uint seed,
            float thickness)
        {
            if (mesh == null || footprint == null || footprint.Length < 3)
                return System.Array.Empty<Vector3>();
            int sides = math.clamp(footprint.Length, 3, 8);
            float safeThickness = Mathf.Max(0.18f, thickness);
            const int ringCount = 5;
            var ringVertices = new Vector3[ringCount, sides];
            // The visible volume is a geological shard, not a vertical extrusion.
            // The belly breaks the silhouette, the shoulder bevels the top, and the
            // 0.992 top inset leaves a narrow readable web crack between neighbours.
            float[] ringY =
            {
                -safeThickness,
                -safeThickness + Mathf.Min(0.18f, safeThickness * 0.12f),
                -safeThickness * 0.44f,
                -safeThickness * 0.13f,
                0f
            };
            for (int ring = 0; ring < ringCount; ring++)
            {
                for (int index = 0; index < sides; index++)
                {
                    float2 p = footprint[index];
                    uint vertexSeed = seed + (uint)(ring * 613 + index * 47 + 11);
                    float scale;
                    switch (ring)
                    {
                        case 0:
                            scale = Mathf.Lerp(0.58f, 0.70f, Hash01(vertexSeed));
                            break;
                        case 1:
                            scale = Mathf.Lerp(0.78f, 0.88f, Hash01(vertexSeed));
                            break;
                        case 2:
                            scale = Mathf.Lerp(1.00f, 1.075f, Hash01(vertexSeed));
                            break;
                        case 3:
                            scale = Mathf.Lerp(0.94f, 0.985f, Hash01(vertexSeed));
                            break;
                        default:
                            scale = 0.992f;
                            break;
                    }
                    float chip = ring == ringCount - 1
                        ? Mathf.Lerp(-0.055f, 0.032f, Hash01(vertexSeed ^ 0xA511u))
                        : Mathf.Lerp(-0.018f, 0.018f, Hash01(vertexSeed ^ 0x71C3u));
                    ringVertices[ring, index] = new Vector3(p.x * scale, ringY[ring] + chip, p.y * scale);
                }
            }

            var vertices = new List<Vector3>(sides * (ringCount - 1) * 6 + sides + 2);
            var triangles = new List<int>(sides * (ringCount - 1) * 6 + sides * 6);
            var uv = new List<Vector2>(vertices.Capacity);

            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -safeThickness, 0f));
            uv.Add(new Vector2(0.5f, 0.5f));
            int bottomRing = vertices.Count;
            for (int index = 0; index < sides; index++)
            {
                Vector3 p = ringVertices[0, index];
                vertices.Add(p);
                uv.Add(new Vector2(p.x, p.z));
            }

            int topCenter = vertices.Count;
            float centerChip = Mathf.Lerp(-0.075f, 0.045f, Hash01(seed ^ 0x51A7u));
            vertices.Add(new Vector3(0f, centerChip, 0f));
            uv.Add(new Vector2(0.5f, 0.5f));
            int topRing = vertices.Count;
            for (int index = 0; index < sides; index++)
            {
                Vector3 p = ringVertices[ringCount - 1, index];
                vertices.Add(p);
                uv.Add(new Vector2(p.x, p.z));
            }

            for (int index = 0; index < sides; index++)
            {
                int next = (index + 1) % sides;
                // Footprints are counter-clockwise in XZ. Bottom faces point down;
                // top faces point up. Reversing these two windings produced inward
                // cap normals on every armor stone after RecalculateNormals.
                triangles.Add(bottomCenter);
                triangles.Add(bottomRing + index);
                triangles.Add(bottomRing + next);
                triangles.Add(topCenter);
                triangles.Add(topRing + next);
                triangles.Add(topRing + index);
            }

            for (int ring = 0; ring < ringCount - 1; ring++)
                AddFacetedBand(vertices, triangles, uv, ringVertices, ring, sides, seed);

            // The geological rings are assembled in the convenient footprint order,
            // which is inward-facing under Unity's mesh winding convention. Publish
            // the intended outward orientation directly instead of making the runtime
            // integrity gate repair every pooled wave/armor mesh on first use.
            if (SignedArea(footprint, sides) < 0f)
                ReverseWinding(triangles);

            mesh.Clear();
            mesh.name = $"EarthWebVoronoi_{seed:X8}";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.SetUVs(0, uv);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EarthMeshIntegrityGate.ValidateInPlaceOrUseFallback(
                mesh,
                EarthMeshIntegrityPolicy.ConvexCollider,
                mesh.name,
                mesh.bounds);
            return mesh.vertices;
        }

        private static void AddFacetedBand(
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector2> uv,
            Vector3[,] rings,
            int ring,
            int sides,
            uint seed)
        {
            for (int index = 0; index < sides; index++)
            {
                int next = (index + 1) % sides;
                Vector3 lowerA = rings[ring, index];
                Vector3 lowerB = rings[ring, next];
                Vector3 upperA = rings[ring + 1, index];
                Vector3 upperB = rings[ring + 1, next];
                int start = vertices.Count;
                vertices.Add(lowerA);
                vertices.Add(lowerB);
                vertices.Add(upperB);
                vertices.Add(upperA);
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(1f, 0f));
                uv.Add(new Vector2(1f, 1f));
                uv.Add(new Vector2(0f, 1f));
                bool alternateDiagonal = Hash01(seed + (uint)(ring * 193 + index * 29)) > 0.5f;
                if (alternateDiagonal)
                {
                    triangles.Add(start);
                    triangles.Add(start + 2);
                    triangles.Add(start + 1);
                    triangles.Add(start);
                    triangles.Add(start + 3);
                    triangles.Add(start + 2);
                }
                else
                {
                    triangles.Add(start);
                    triangles.Add(start + 3);
                    triangles.Add(start + 1);
                    triangles.Add(start + 1);
                    triangles.Add(start + 3);
                    triangles.Add(start + 2);
                }
            }
        }

        private static Mesh BuildPrism(Vector2[] footprint, int poolIndex)
        {
            int sides = footprint.Length;
            var vertices = new List<Vector3>(sides * (TopSubdivisions + 2) + 2);
            var triangles = new List<int>(sides * 18);
            var normals = new List<Vector3>(sides * (TopSubdivisions + 2) + 2);
            var uv = new List<Vector2>(sides * (TopSubdivisions + 2) + 2);

            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -0.5f, 0f));
            normals.Add(Vector3.down);
            uv.Add(new Vector2(0.5f, 0.5f));

            int bottomRing = vertices.Count;
            for (int index = 0; index < sides; index++)
            {
                Vector2 p = footprint[index];
                vertices.Add(new Vector3(p.x, -0.5f, p.y));
                normals.Add(Vector3.down);
                uv.Add(p + Vector2.one * 0.5f);
            }

            int topCenter = vertices.Count;
            float centerChip = Mathf.Lerp(-0.035f, 0.055f, Hash01((uint)(poolIndex * 131 + 29)));
            vertices.Add(new Vector3(0f, 0.5f + centerChip, 0f));
            normals.Add(Vector3.up);
            uv.Add(new Vector2(0.5f, 0.5f));

            int topRing = vertices.Count;
            for (int index = 0; index < sides; index++)
            {
                Vector2 p = footprint[index];
                float chip = Mathf.Lerp(-0.07f, 0.035f, Hash01((uint)(poolIndex * 193 + index * 47 + 11)));
                vertices.Add(new Vector3(p.x, 0.5f + chip, p.y));
                normals.Add(Vector3.up);
                uv.Add(p + Vector2.one * 0.5f);
            }

            for (int index = 0; index < sides; index++)
            {
                int next = (index + 1) % sides;
                triangles.Add(bottomCenter);
                triangles.Add(bottomRing + index);
                triangles.Add(bottomRing + next);

                triangles.Add(topCenter);
                triangles.Add(topRing + next);
                triangles.Add(topRing + index);
            }

            // Duplicate side vertices so the chipped top remains crisp and the walls
            // receive their own geological normals instead of looking inflated.
            for (int index = 0; index < sides; index++)
            {
                int next = (index + 1) % sides;
                Vector3 bottomA = vertices[bottomRing + index];
                Vector3 bottomB = vertices[bottomRing + next];
                Vector3 topA = vertices[topRing + index];
                Vector3 topB = vertices[topRing + next];
                Vector3 sideNormal = Vector3.Cross(topA - bottomA, bottomB - bottomA).normalized;
                int start = vertices.Count;
                vertices.Add(bottomA);
                vertices.Add(bottomB);
                vertices.Add(topB);
                vertices.Add(topA);
                for (int vertex = 0; vertex < 4; vertex++) normals.Add(sideNormal);
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(1f, 0f));
                uv.Add(new Vector2(1f, 1f));
                uv.Add(new Vector2(0f, 1f));
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start);
                triangles.Add(start + 3);
                triangles.Add(start + 2);
            }

            var mesh = new Mesh
            {
                name = $"EarthWebCell_{poolIndex:00}",
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ReverseWinding(List<int> triangles)
        {
            for (int index = 0; index + 2 < triangles.Count; index += 3)
                (triangles[index + 1], triangles[index + 2]) =
                    (triangles[index + 2], triangles[index + 1]);
        }

        private static float SignedArea(float2[] footprint, int count)
        {
            float twiceArea = 0f;
            for (int index = 0; index < count; index++)
            {
                float2 current = footprint[index];
                float2 next = footprint[(index + 1) % count];
                twiceArea += current.x * next.y - next.x * current.y;
            }
            return twiceArea * 0.5f;
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
