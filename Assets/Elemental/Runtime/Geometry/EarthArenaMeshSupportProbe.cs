using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    /// <summary>
    /// Selects a small, deterministic and spatially spread set of visible low
    /// mesh vertices. Faceted meshes commonly duplicate one geometric corner;
    /// treating those duplicates as separate contacts creates false support.
    /// </summary>
    public static class EarthArenaMeshSupportProbe
    {
        public const int MaximumSamples = 8;
        private const float DuplicateDistance = 0.004f;

        private readonly struct Candidate
        {
            public Candidate(Vector3 point, float projection)
            {
                Point = point;
                Projection = projection;
            }

            public Vector3 Point { get; }
            public float Projection { get; }
        }

        public static int CollectSpreadLowPoints(
            Transform meshTransform,
            Vector3 projectionOrigin,
            Vector3 up,
            Vector3[] results)
        {
            if (meshTransform == null || results == null || results.Length == 0)
                return 0;
            MeshFilter filter = meshTransform.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.vertexCount == 0) return 0;

            up = up.sqrMagnitude > 0.000001f ? up.normalized : Vector3.up;
            Vector3[] vertices = mesh.vertices;
            var ordered = new List<Candidate>(vertices.Length);
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 point = meshTransform.TransformPoint(vertices[index]);
                float projection = Vector3.Dot(point - projectionOrigin, up);
                if (!float.IsFinite(projection)) continue;
                ordered.Add(new Candidate(point, projection));
                minimum = Mathf.Min(minimum, projection);
                maximum = Mathf.Max(maximum, projection);
            }
            if (ordered.Count == 0) return 0;
            ordered.Sort((a, b) =>
            {
                int projectionOrder = a.Projection.CompareTo(b.Projection);
                if (projectionOrder != 0) return projectionOrder;
                int xOrder = a.Point.x.CompareTo(b.Point.x);
                if (xOrder != 0) return xOrder;
                int yOrder = a.Point.y.CompareTo(b.Point.y);
                return yOrder != 0 ? yOrder : a.Point.z.CompareTo(b.Point.z);
            });

            float lowBand = Mathf.Clamp((maximum - minimum) * 0.14f, 0.025f, 0.14f);
            var candidates = new List<Candidate>(Mathf.Min(48, ordered.Count));
            float duplicateDistanceSq = DuplicateDistance * DuplicateDistance;
            for (int index = 0; index < ordered.Count && candidates.Count < 48; index++)
            {
                Candidate candidate = ordered[index];
                if (candidate.Projection > minimum + lowBand && candidates.Count >= 3) break;
                bool duplicate = false;
                for (int uniqueIndex = 0; uniqueIndex < candidates.Count; uniqueIndex++)
                {
                    if ((candidate.Point - candidates[uniqueIndex].Point).sqrMagnitude >
                        duplicateDistanceSq) continue;
                    duplicate = true;
                    break;
                }
                if (!duplicate) candidates.Add(candidate);
            }
            if (candidates.Count == 0) return 0;

            int capacity = Mathf.Min(MaximumSamples, results.Length);
            var selected = new List<int>(capacity) { 0 };
            results[0] = candidates[0].Point;
            while (selected.Count < capacity && selected.Count < candidates.Count)
            {
                int best = -1;
                float bestScore = float.NegativeInfinity;
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    if (selected.Contains(candidateIndex)) continue;
                    Vector3 tangent = Vector3.ProjectOnPlane(
                        candidates[candidateIndex].Point,
                        up);
                    float nearestTangentDistanceSq = float.PositiveInfinity;
                    for (int selectedIndex = 0; selectedIndex < selected.Count; selectedIndex++)
                    {
                        Vector3 otherTangent = Vector3.ProjectOnPlane(
                            candidates[selected[selectedIndex]].Point,
                            up);
                        nearestTangentDistanceSq = Mathf.Min(
                            nearestTangentDistanceSq,
                            (tangent - otherTangent).sqrMagnitude);
                    }
                    float height = candidates[candidateIndex].Projection - minimum;
                    float score = nearestTangentDistanceSq - height * height * 0.20f;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    best = candidateIndex;
                }
                if (best < 0) break;
                results[selected.Count] = candidates[best].Point;
                selected.Add(best);
            }
            return selected.Count;
        }

        public static void BuildTangentFrame(
            Vector3 up,
            out Vector3 tangentX,
            out Vector3 tangentY)
        {
            up = up.sqrMagnitude > 0.000001f ? up.normalized : Vector3.up;
            tangentX = Vector3.Cross(
                Mathf.Abs(Vector3.Dot(up, Vector3.forward)) < 0.92f
                    ? Vector3.forward
                    : Vector3.right,
                up).normalized;
            tangentY = Vector3.Cross(up, tangentX).normalized;
        }
    }
}
