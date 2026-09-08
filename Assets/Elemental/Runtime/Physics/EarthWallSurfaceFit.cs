using Elemental.Simulation.Bending;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>Fits a drawn wall's complete base strip onto its original finite face.</summary>
    public static class EarthWallSurfaceFit
    {
        private static readonly ProfilerMarker FitMarker = new ProfilerMarker("Elemental.Earth.Wall.SurfaceFit");
        private const float EdgeInset = 0.012f;
        private const float MinimumLength = 0.25f;
        private const float MinimumThickness = 0.05f;

        public static bool TryResolveEmbed(EarthSurfaceQueryService surfaces, in EarthSurfaceSample surface,
            Vector3 start, Vector3 end, float thickness, out float embed)
        {
            embed = 0f;
            Vector3 normal = (Vector3)surface.Normal;
            Vector3 across = Vector3.Cross((end - start).normalized, normal);
            float minimumEmbed = 0.001f, maximumEmbed = float.PositiveInfinity;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 point = (corner < 2 ? start : end) +
                    across * (corner % 2 == 0 ? -0.5f : 0.5f) * thickness;
                if (!TrySupportPoint(surfaces, in surface, point, out EarthSurfaceSample top)) return false;
                EarthSurfaceHandle handle = top.Handle;
                Collider shape = surfaces.GetConstructionCollider(in handle);
                if (shape == null) return false;
                Bounds bounds = shape.bounds;
                float extent = Vector3.Dot(new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z)), bounds.extents);
                Vector3 outside = point - normal * (Vector3.Dot(point - bounds.center, normal) + extent + 0.05f);
                float distance = Vector3.Dot(point - outside, normal) + 1f;
                if (!shape.Raycast(new Ray(outside, normal), out RaycastHit bottom, distance)) return false;
                // Intersect the four actual solid depth intervals. A bumpy arena
                // needs a common plane below its lowest corner; thin slabs need a
                // shallower seat. Both limits come from the original colliders.
                float frontDepth = Vector3.Dot(point - (Vector3)top.Point, normal);
                float backDepth = Vector3.Dot(point - bottom.point, normal);
                minimumEmbed = Mathf.Max(minimumEmbed, frontDepth + 0.001f);
                maximumEmbed = Mathf.Min(maximumEmbed, backDepth - 0.001f);
            }
            if (maximumEmbed < minimumEmbed) return false;
            embed = Mathf.Clamp(Mathf.Max(0.06f, minimumEmbed + 0.01f), minimumEmbed, maximumEmbed);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 point = (corner < 2 ? start : end) + across * (corner % 2 == 0 ? -0.5f : 0.5f) * thickness;
                if (!TrySupportPoint(surfaces, in surface, point, out EarthSurfaceSample top)) return false;
                EarthSurfaceHandle handle = top.Handle;
                Collider shape = surfaces.GetConstructionCollider(in handle);
                float travel = Vector3.Dot((Vector3)top.Point - (point - normal * embed), normal);
                if (shape == null || shape.Raycast(new Ray(point - normal * embed, normal), out _, travel + 0.0001f)) return false;
            }
            return true;
        }

        public static bool TryFit(EarthSurfaceQueryService surfaces, in EarthSurfaceSample surface,
            ref Vector3 start, ref Vector3 end, ref float thickness)
        {
            using (FitMarker.Auto())
            {
                if (surfaces == null || !surface.IsValid || thickness < MinimumThickness) return false;
                EarthSurfaceHandle handle = surface.Handle;
                if (!surfaces.IsCurrent(in handle)) return false;
                Vector3 normal = (Vector3)surface.Normal;
                Vector3 planePoint = (Vector3)surface.Point;
                Vector3 from = start - normal * Vector3.Dot(start - planePoint, normal);
                Vector3 to = end - normal * Vector3.Dot(end - planePoint, normal);
                Vector3 chord = to - from;
                float length = chord.magnitude;
                if (length < MinimumLength) return false;
                Vector3 direction = chord / length;
                Vector3 across = Vector3.Cross(direction, normal).normalized;
                // The fixed cap is construction-time work, never a per-frame query.
                int steps = Mathf.Clamp(Mathf.CeilToInt(length / 0.04f), 16, 512);
                int anchorIndex = Mathf.Clamp(Mathf.RoundToInt(Vector3.Dot(planePoint - from, direction) / length * steps), 0, steps);
                for (float width = thickness;; width = Mathf.Max(MinimumThickness, width * 0.7f))
                {
                    int runStart = -1, bestStart = -1, bestEnd = -1;
                    int bestAnchorDistance = int.MaxValue;
                    for (int index = 0; index <= steps; index++)
                    {
                        Vector3 center = from + direction * (length * index / steps);
                        bool supported = SupportsStrip(surfaces, in surface, center, across, width);
                        if (supported && runStart < 0) runStart = index;
                        if (runStart >= 0 && (!supported || index == steps))
                        {
                            int runEnd = supported ? index : index - 1;
                            int anchorDistance = Mathf.Max(Mathf.Max(runStart - anchorIndex, anchorIndex - runEnd), 0);
                            if (anchorDistance < bestAnchorDistance ||
                                (anchorDistance == bestAnchorDistance && runEnd - runStart > bestEnd - bestStart))
                            { bestStart = runStart; bestEnd = runEnd; bestAnchorDistance = anchorDistance; }
                            runStart = -1;
                        }
                    }
                    if (bestStart >= 0 && (bestEnd - bestStart) * length / steps >= MinimumLength + 2f * EdgeInset)
                    {
                        float first = RefineEdge(surfaces, in surface, from, direction, across, width,
                            length * bestStart / steps, length * Mathf.Max(0, bestStart - 1) / steps);
                        float last = RefineEdge(surfaces, in surface, from, direction, across, width,
                            length * bestEnd / steps, length * Mathf.Min(steps, bestEnd + 1) / steps);
                        start = from + direction * (first + EdgeInset);
                        end = from + direction * (last - EdgeInset);
                        thickness = width;
                        return SupportsStrip(surfaces, in surface, start, across, width) &&
                               SupportsStrip(surfaces, in surface, end, across, width);
                    }
                    if (width <= MinimumThickness) return false;
                }
            }
        }

        private static float RefineEdge(EarthSurfaceQueryService surfaces, in EarthSurfaceSample surface,
            Vector3 from, Vector3 direction, Vector3 across, float width, float inside, float outside)
        {
            for (int iteration = 0; iteration < 10; iteration++)
            {
                float midpoint = (inside + outside) * 0.5f;
                if (SupportsStrip(surfaces, in surface, from + direction * midpoint, across, width)) inside = midpoint;
                else outside = midpoint;
            }
            return inside;
        }

        private static bool SupportsStrip(EarthSurfaceQueryService surfaces, in EarthSurfaceSample surface,
            Vector3 center, Vector3 across, float width)
        {
            // Check center and both thickness edges. The extra margin keeps all
            // four base corners inside the support silhouette after seating.
            return SupportsPoint(surfaces, in surface, center) &&
                   SupportsPoint(surfaces, in surface, center + across * (width * 0.5f + EdgeInset)) &&
                   SupportsPoint(surfaces, in surface, center - across * (width * 0.5f + EdgeInset));
        }

        private static bool SupportsPoint(EarthSurfaceQueryService surfaces, in EarthSurfaceSample surface, Vector3 point)
            => TrySupportPoint(surfaces, in surface, point, out _);

        private static bool TrySupportPoint(EarthSurfaceQueryService surfaces, in EarthSurfaceSample surface,
            Vector3 point, out EarthSurfaceSample hit)
        {
            float3 normal = surface.Normal;
            bool arenaTop = surfaces.AllowsTopConstructionContinuation(in surface);
            float probeDepth = arenaTop ? 1f : 0.08f;
            var query = new EarthSurfaceQuery((float3)point + normal * probeDepth, -normal,
                probeDepth * 2f, EarthSurfaceCapabilities.Draw);
            return surfaces.TrySampleConstructionSupport(in surface, in query, out hit) &&
                   math.dot(hit.Normal, normal) >= 0.995f &&
                   math.abs(math.dot(hit.Point - (float3)point, normal)) <= (arenaTop ? 0.9f : 0.025f);
        }
    }
}
