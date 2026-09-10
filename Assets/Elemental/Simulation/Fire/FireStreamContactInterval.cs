using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Fire
{
    /// <summary>
    /// Contact time for a translating capsule against a straight stream whose
    /// length changes linearly over one authority step. The nine closest-feature
    /// pairs cover segment interiors and endpoints, including instantaneous
    /// crossings between samples. No particles, render clocks or collider IDs.
    /// </summary>
    public static class FireStreamContactInterval
    {
        public static float ContactSeconds(float3 beamStart, float3 beamDirection,
            float startLength, float endLength, float3 capsuleStart, float3 capsuleEnd,
            float3 relativeTranslation, float combinedRadius, float deltaTime)
        {
            return math.isfinite(deltaTime) && deltaTime > 0 &&
                TryGetInterval(beamStart, beamDirection, startLength, endLength, capsuleStart,
                    capsuleEnd, relativeTranslation, combinedRadius, out float first, out float last)
                ? deltaTime * (last - first) : 0;
        }

        public static bool TryGetInterval(float3 beamStart, float3 beamDirection,
            float startLength, float endLength, float3 capsuleStart, float3 capsuleEnd,
            float3 relativeTranslation, float combinedRadius, out float entry, out float exit)
        {
            entry = exit = 0;
            if (!math.all(math.isfinite(beamStart)) || !math.all(math.isfinite(beamDirection)) ||
                !math.all(math.isfinite(capsuleStart)) || !math.all(math.isfinite(capsuleEnd)) ||
                !math.all(math.isfinite(relativeTranslation)) ||
                !math.all(math.isfinite(new float3(startLength, endLength, combinedRadius))) ||
                startLength < 0 || endLength < 0 || combinedRadius <= 0 ||
                math.lengthsq(beamDirection) < 1e-12f) return false;

            float3 u = math.normalize(beamDirection), v = capsuleEnd - capsuleStart;
            float3 w = capsuleStart - beamStart;
            double uu = Dot(u, u), vv = Dot(v, v), uv = Dot(u, v);
            double uw = Dot(u, w), vw = Dot(v, w);
            double ud = Dot(u, relativeTranslation), vd = Dot(v, relativeTranslation);
            double first = 1, last = 0, lengthDelta = endLength - startLength;
            bool found = false;
            for (int a = 0; a < 3; a++)
            for (int b = 0; b < 3; b++)
            {
                // 0/1: first/last endpoint. 2: interior of that segment.
                double p = a == 1 ? startLength : 0, pt = a == 1 ? lengthDelta : 0;
                double q = b == 1 ? 1 : 0, qt = 0;
                if (a == 2 && b == 2)
                {
                    double determinant = uu * vv - uv * uv;
                    if (determinant <= 1e-12 * Math.Max(1, uu * vv)) continue;
                    p = (vv * uw - uv * vw) / determinant;
                    pt = (vv * ud - uv * vd) / determinant;
                    q = (uv * uw - uu * vw) / determinant;
                    qt = (uv * ud - uu * vd) / determinant;
                }
                else if (a == 2)
                {
                    p = (uw + uv * q) / uu;
                    pt = (ud + uv * qt) / uu;
                }
                else if (b == 2)
                {
                    if (vv < 1e-12) continue;
                    q = (uv * p - vw) / vv;
                    qt = (uv * pt - vd) / vv;
                }

                double lo = 0, hi = 1;
                if (a == 2 && (!ClipPositive(p, pt, ref lo, ref hi) ||
                    !ClipPositive(startLength - p, lengthDelta - pt, ref lo, ref hi))) continue;
                if (b == 2 && (!ClipPositive(q, qt, ref lo, ref hi) ||
                    !ClipPositive(1 - q, -qt, ref lo, ref hi))) continue;

                double x = w.x + v.x * q - u.x * p;
                double y = w.y + v.y * q - u.y * p;
                double z = w.z + v.z * q - u.z * p;
                double dx = relativeTranslation.x + v.x * qt - u.x * pt;
                double dy = relativeTranslation.y + v.y * qt - u.y * pt;
                double dz = relativeTranslation.z + v.z * qt - u.z * pt;
                double aa = dx * dx + dy * dy + dz * dz;
                double bb = 2 * (x * dx + y * dy + z * dz);
                double cc = x * x + y * y + z * z - (double)combinedRadius * combinedRadius;
                if (aa < 1e-24)
                {
                    if (cc > 0) continue;
                }
                else
                {
                    double discriminant = bb * bb - 4 * aa * cc;
                    if (discriminant < 0) continue;
                    double root = Math.Sqrt(discriminant);
                    lo = Math.Max(lo, (-bb - root) / (2 * aa));
                    hi = Math.Min(hi, (-bb + root) / (2 * aa));
                    if (hi < lo) continue;
                }
                first = Math.Min(first, lo); last = Math.Max(last, hi); found = true;
            }
            // A linearly moving capsule and linearly growing segment describe
            // convex sets in space-time, so their contact interval is contiguous.
            if (!found || last <= first) return false;
            entry = (float)first; exit = (float)last; return true;
        }

        private static bool ClipPositive(double value, double slope, ref double lo, ref double hi)
        {
            if (Math.Abs(slope) < 1e-15) return value >= 0;
            double crossing = -value / slope;
            if (slope > 0) lo = Math.Max(lo, crossing); else hi = Math.Min(hi, crossing);
            return lo <= hi;
        }

        private static double Dot(float3 a, float3 b) =>
            (double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z;
    }
}
