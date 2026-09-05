using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    /// <summary>Limits a wave site's Voronoi territory to the ground actually lifted by that site.</summary>
    public static class EarthWaveFootprintSolver
    {
        public static float Radius(float maximumWidth) => math.max(.25f, maximumWidth * .85f);

        public static float2[] Clip(float2[] vertices, float2 site, float radius,
            out float2 centroid, out float area)
        {
            var polygon = new List<float2>(vertices);
            var output = new List<float2>(vertices.Length + 8);
            // Inscribed octagon: even the corners remain inside the radius.
            float apothem = radius * math.cos(math.PI / 8f);
            for (int edge = 0; edge < 8 && polygon.Count >= 3; edge++)
            {
                float angle = (edge + .5f) * math.PI / 4f;
                float2 normal = new float2(math.cos(angle), math.sin(angle));
                output.Clear();
                float2 previous = polygon[polygon.Count - 1];
                float previousDistance = math.dot(previous - site, normal) - apothem;
                foreach (float2 current in polygon)
                {
                    float distance = math.dot(current - site, normal) - apothem;
                    if ((distance <= 0f) != (previousDistance <= 0f))
                        output.Add(math.lerp(previous, current, previousDistance / (previousDistance - distance)));
                    if (distance <= 0f) output.Add(current);
                    previous = current;
                    previousDistance = distance;
                }
                var swap = polygon; polygon = output; output = swap;
            }
            float twiceArea = 0f;
            float2 weighted = float2.zero;
            for (int i = 0; i < polygon.Count; i++)
            {
                float2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                float cross = a.x * b.y - b.x * a.y;
                twiceArea += cross;
                weighted += (a + b) * cross;
            }
            area = math.abs(twiceArea) * .5f;
            centroid = area > .00001f ? weighted / (3f * twiceArea) : site;
            return polygon.ToArray();
        }
    }
}
