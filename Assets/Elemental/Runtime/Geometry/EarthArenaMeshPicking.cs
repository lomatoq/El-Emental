using System;
using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    /// <summary>Immutable CPU query for dormant closed authored cells; independent of PhysX activation.</summary>
    public sealed class EarthArenaMeshPicking
    {
        private readonly Vector3[] _vertices;
        private readonly int[] _triangles;
        private readonly Bounds _bounds;
        private readonly Vector3 _center;

        public EarthArenaMeshPicking(Mesh source)
        {
            if (source == null || !source.isReadable)
                throw new ArgumentException("Dormant arena picking needs a readable authored collider mesh.", nameof(source));
            _vertices = source.vertices;
            _triangles = source.triangles;
            _bounds = source.bounds;
            if (_vertices.Length == 0 || _triangles.Length == 0 || _triangles.Length % 3 != 0)
                throw new ArgumentException("Dormant arena picking needs a nonempty triangulated mesh.", nameof(source));
            foreach (Vector3 vertex in _vertices) _center += vertex;
            _center /= _vertices.Length;
        }

        public float SquaredDistance(Vector3 point, Matrix4x4 localToWorld, Matrix4x4 worldToLocal,
            out float centerSquaredDistance)
        {
            centerSquaredDistance = (localToWorld.MultiplyPoint3x4(_center) - point).sqrMagnitude;
            bool mayContain = _bounds.Contains(worldToLocal.MultiplyPoint3x4(point));
            double solidAngle = 0;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < _triangles.Length; i += 3)
            {
                Vector3 a = localToWorld.MultiplyPoint3x4(_vertices[_triangles[i]]);
                Vector3 b = localToWorld.MultiplyPoint3x4(_vertices[_triangles[i + 1]]);
                Vector3 c = localToWorld.MultiplyPoint3x4(_vertices[_triangles[i + 2]]);
                nearest = Mathf.Min(nearest, (ClosestPointOnTriangle(point, a, b, c) - point).sqrMagnitude);
                if (nearest <= 1e-12f) return 0;
                if (!mayContain) continue;
                // Signed solid angle is independent of ray/edge coincidences and
                // supports closed concave cells as well as their convex proxies.
                Vector3 pa = a - point, pb = b - point, pc = c - point;
                double la = pa.magnitude, lb = pb.magnitude, lc = pc.magnitude;
                double denominator = la * lb * lc + Vector3.Dot(pa, pb) * lc +
                    Vector3.Dot(pb, pc) * la + Vector3.Dot(pc, pa) * lb;
                solidAngle += 2 * Math.Atan2(Vector3.Dot(pa, Vector3.Cross(pb, pc)), denominator);
            }
            return mayContain && Math.Abs(solidAngle) > 2 * Math.PI ? 0 : nearest;
        }

        private static Vector3 ClosestPointOnTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a;
            if (Vector3.Cross(ab, ac).sqrMagnitude <= 1e-20f)
            {
                Vector3 first = ClosestPointOnSegment(point, a, b);
                Vector3 second = ClosestPointOnSegment(point, a, c);
                Vector3 third = ClosestPointOnSegment(point, b, c);
                Vector3 best = (first - point).sqrMagnitude < (second - point).sqrMagnitude ? first : second;
                return (best - point).sqrMagnitude < (third - point).sqrMagnitude ? best : third;
            }
            Vector3 ap = point - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return a;
            Vector3 bp = point - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = point - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
                return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float inverse = 1f / (va + vb + vc);
            return a + ab * (vb * inverse) + ac * (vc * inverse);
        }

        private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 edge = b - a;
            return a + edge * Mathf.Clamp01(Vector3.Dot(point - a, edge) / Mathf.Max(1e-20f, edge.sqrMagnitude));
        }
    }
}
