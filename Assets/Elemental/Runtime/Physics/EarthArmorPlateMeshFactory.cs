using System.Collections.Generic;
using Elemental.Runtime.Geometry;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>
    /// Creates deterministic, convex geological plates for the wearable shell.
    /// Every plate starts from a different chipped outline and crown, while its
    /// normalized bounds stay predictable enough for edge-to-edge body packing.
    /// </summary>
    internal static class EarthArmorPlateMeshFactory
    {
        public static Mesh Create(int pieceIndex)
        {
            Vector2[] footprint = BuildConvexFootprint(pieceIndex);
            int sideCount = footprint.Length;
            var vertices = new List<Vector3>(sideCount * 6 + 2);
            var triangles = new List<int>(sideCount * 18);
            var uv = new List<Vector2>(vertices.Capacity);
            float bottomY = Mathf.Lerp(-0.58f, -0.40f, Hash01((uint)(pieceIndex * 43 + 5)));
            float bottomInset = Mathf.Lerp(0.64f, 0.82f, Hash01((uint)(pieceIndex * 59 + 13)));
            float shoulderInset = Mathf.Lerp(0.80f, 0.97f, Hash01((uint)(pieceIndex * 71 + 23)));

            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, bottomY, 0f));
            uv.Add(new Vector2(0.5f, 0.5f));
            int bottomRing = vertices.Count;
            for (int index = 0; index < sideCount; index++)
            {
                Vector2 point = footprint[index] * bottomInset;
                vertices.Add(new Vector3(point.x, bottomY, point.y));
                uv.Add(point + Vector2.one * 0.5f);
            }

            int topCenter = vertices.Count;
            float crown = Mathf.Lerp(0.40f, 0.64f, Hash01((uint)(pieceIndex * 101 + 43)));
            Vector2 crownOffset = new Vector2(
                Mathf.Lerp(-0.13f, 0.13f, Hash01((uint)(pieceIndex * 107 + 47))),
                Mathf.Lerp(-0.13f, 0.13f, Hash01((uint)(pieceIndex * 109 + 53))));
            vertices.Add(new Vector3(crownOffset.x, crown, crownOffset.y));
            uv.Add(new Vector2(0.5f, 0.5f));
            int topRing = vertices.Count;
            for (int index = 0; index < sideCount; index++)
            {
                Vector2 point = footprint[index] * shoulderInset;
                float shoulder = Mathf.Lerp(0.17f, 0.34f,
                    Hash01((uint)(pieceIndex * 131 + index * 29 + 7)));
                // One or two deeper shoulders give each plate a readable broken edge
                // instead of the same regular bevel repeated ninety-six times.
                if (((pieceIndex * 3 + index * 5) % 11) == 0) shoulder -= 0.10f;
                vertices.Add(new Vector3(point.x, shoulder, point.y));
                uv.Add(point + Vector2.one * 0.5f);
            }

            for (int index = 0; index < sideCount; index++)
            {
                int next = (index + 1) % sideCount;
                // XZ footprint is counter-clockwise: bottom faces down, top faces up.
                triangles.Add(bottomCenter);
                triangles.Add(bottomRing + index);
                triangles.Add(bottomRing + next);
                triangles.Add(topCenter);
                triangles.Add(topRing + next);
                triangles.Add(topRing + index);
            }

            for (int index = 0; index < sideCount; index++)
            {
                int next = (index + 1) % sideCount;
                Vector2 outerA = footprint[index];
                Vector2 outerB = footprint[next];
                Vector2 lowerA = outerA * bottomInset;
                Vector2 lowerB = outerB * bottomInset;
                Vector2 upperA = outerA * shoulderInset;
                Vector2 upperB = outerB * shoulderInset;
                float upperAY = vertices[topRing + index].y;
                float upperBY = vertices[topRing + next].y;
                int start = vertices.Count;
                vertices.Add(new Vector3(lowerA.x, bottomY, lowerA.y));
                vertices.Add(new Vector3(lowerB.x, bottomY, lowerB.y));
                vertices.Add(new Vector3(upperB.x, upperBY, upperB.y));
                vertices.Add(new Vector3(upperA.x, upperAY, upperA.y));
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
                name = $"EarthArmorTile_{pieceIndex + 1:00}",
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
            };
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
            return mesh;
        }

        private static Vector2[] BuildConvexFootprint(int pieceIndex)
        {
            // More proposal points than final sides allow deep chips to become long
            // geological facets after the hull pass. The result varies between broad
            // five-sided wedges and dense ten-sided plates without ever becoming
            // concave or producing an invalid convex MeshCollider.
            int proposalCount = 8 + Mathf.FloorToInt(Hash01((uint)(pieceIndex * 17 + 3)) * 5f);
            float phase = Hash01((uint)(pieceIndex * 31 + 7)) * Mathf.PI * 2f;
            float aspectX = Mathf.Lerp(0.72f, 1.28f, Hash01((uint)(pieceIndex * 37 + 11)));
            float aspectZ = Mathf.Lerp(0.72f, 1.28f, Hash01((uint)(pieceIndex * 41 + 17)));
            float shear = Mathf.Lerp(-0.22f, 0.22f, Hash01((uint)(pieceIndex * 53 + 19)));
            int chipped = Mathf.FloorToInt(Hash01((uint)(pieceIndex * 61 + 29)) * proposalCount) % proposalCount;
            var proposals = new List<Vector2>(proposalCount);
            for (int index = 0; index < proposalCount; index++)
            {
                float step = Mathf.PI * 2f / proposalCount;
                float angleJitter = Mathf.Lerp(-0.22f, 0.22f,
                    Hash01((uint)(pieceIndex * 97 + index * 67 + 37))) * step;
                float angle = phase + index * step + angleJitter;
                float radius = Mathf.Lerp(0.76f, 1.18f,
                    Hash01((uint)(pieceIndex * 127 + index * 83 + 41)));
                if (index == chipped || (proposalCount > 10 && index == (chipped + 4) % proposalCount))
                    radius *= Mathf.Lerp(0.58f, 0.74f,
                        Hash01((uint)(pieceIndex * 149 + index * 43 + 47)));
                float z = Mathf.Sin(angle) * radius * aspectZ;
                float x = Mathf.Cos(angle) * radius * aspectX + z * shear;
                proposals.Add(new Vector2(x, z));
            }

            proposals.Sort((left, right) =>
            {
                int x = left.x.CompareTo(right.x);
                return x != 0 ? x : left.y.CompareTo(right.y);
            });
            var hull = new List<Vector2>(proposalCount * 2);
            for (int index = 0; index < proposals.Count; index++)
                AppendHullPoint(hull, proposals[index], 2);
            int lowerCount = hull.Count;
            for (int index = proposals.Count - 2; index >= 0; index--)
                AppendHullPoint(hull, proposals[index], lowerCount + 1);
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);

            float maxX = 0.001f;
            float maxZ = 0.001f;
            for (int index = 0; index < hull.Count; index++)
            {
                maxX = Mathf.Max(maxX, Mathf.Abs(hull[index].x));
                maxZ = Mathf.Max(maxZ, Mathf.Abs(hull[index].y));
            }
            var normalized = new Vector2[hull.Count];
            for (int index = 0; index < hull.Count; index++)
                normalized[index] = new Vector2(hull[index].x / maxX * 0.5f, hull[index].y / maxZ * 0.5f);
            return normalized;
        }

        private static void AppendHullPoint(List<Vector2> hull, Vector2 point, int popThreshold)
        {
            while (hull.Count >= popThreshold &&
                   Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0f)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(point);
        }

        private static float Cross(Vector2 origin, Vector2 a, Vector2 b) =>
            (a.x - origin.x) * (b.y - origin.y) - (a.y - origin.y) * (b.x - origin.x);

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
