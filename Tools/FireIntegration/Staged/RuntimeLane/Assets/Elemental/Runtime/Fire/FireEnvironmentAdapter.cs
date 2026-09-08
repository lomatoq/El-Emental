using System;
using Elemental.Simulation.Fire;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityPhysics = UnityEngine.Physics;

namespace Elemental.Runtime.Fire
{
    public sealed class FireEnvironmentAdapter
    {
        private static readonly ProfilerMarker ProbeMarker = new ProfilerMarker("Elemental.Fire.Environment");
        private const int MaximumProbes = 6, MaximumQueries = 12;
        private readonly Collider[] _overlaps = new Collider[16];
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly SphereCollider _querySphere;
        private readonly Transform _ignoredRoot;
        private readonly int _mask;
        public FireSurfaceResolver Resolver { get; }
        public int QueryCount { get; private set; }
        public int ProbeCount { get; private set; }
        public int BudgetSaturations { get; private set; }
        public int BufferSaturations { get; private set; }
        public int UnresolvedContacts { get; private set; }
        public int InitialOverlaps { get; private set; }

        public FireEnvironmentAdapter(FireSurfaceResolver resolver, SphereCollider querySphere,
            int layerMask, Transform ignoredRoot = null)
        {
            Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _querySphere = querySphere != null ? querySphere : throw new ArgumentNullException(nameof(querySphere));
            _mask = layerMask; _ignoredRoot = ignoredRoot;
        }

        // Six wide-domain sweeps at most. Overlap, penetration and sweep calls all share 12 calls.
        // Analytic box normal / finite edge refinement consumes no extra physics query.
        public void Collect(FireFieldNode[] nodes, int count, float dt, float time, FireContactCache cache)
        {
            using (ProbeMarker.Auto())
            {
                QueryCount = ProbeCount = 0;
                for (int probe = 0; probe < MaximumProbes; probe++)
                {
                    int index = probe % count;
                    FireFieldNode node = nodes[index];
                    if (!node.Active) continue;
                    int ring = probe / count;
                    float3 direction = FireContactMath.SafeNormal(node.B - node.A, node.Flow);
                    float3 tangent = FireContactMath.Tangent(direction);
                    float angle = (ring - 1) * 1.25663706f;
                    float3 offset = ring == 0 ? float3.zero : node.Radius * 0.65f *
                        (tangent * math.cos(angle) + math.cross(direction, tangent) * math.sin(angle));
                    Vector3 origin = FireSurfaceResolver.ToVector(node.A + offset);
                    Vector3 target = FireSurfaceResolver.ToVector(node.B + node.Flow * dt + offset);
                    float radius = math.max(0.035f, node.Radius * 0.3f);
                    ProbeCount++;
                    if (!Probe(origin, target, radius, node.Radius, time, cache, out Vector3 permitted))
                    {
                        // No authority is granted to unknown geometry or an exhausted query buffer.
                        node.B = node.A; nodes[index] = node;
                    }
                    else if (ring == 0)
                    {
                        float originalLength = math.length(node.B - node.A);
                        float allowedLength = Vector3.Distance(origin, permitted);
                        if (allowedLength < originalLength)
                        { node.B = node.A + direction * allowedLength; nodes[index] = node; }
                    }
                    if (QueryCount >= MaximumQueries)
                    {
                        if (probe + 1 < MaximumProbes)
                        {
                            BudgetSaturations++;
                            // Nodes never visited this tick cannot extend into unchecked space.
                            for (int unvisited = probe + 1; unvisited < count; unvisited++)
                            { FireFieldNode blocked = nodes[unvisited]; blocked.B = blocked.A; nodes[unvisited] = blocked; }
                        }
                        break;
                    }
                }
            }
        }

        private bool Spend()
        {
            if (QueryCount >= MaximumQueries) { BudgetSaturations++; return false; }
            QueryCount++; return true;
        }
        private bool Ignore(Collider collider) => collider == null || collider == _querySphere ||
            (_ignoredRoot != null && collider.transform.IsChildOf(_ignoredRoot));

        private bool Probe(Vector3 origin, Vector3 target, float radius, float footprint, float time,
            FireContactCache cache, out Vector3 permitted)
        {
            permitted = origin;
            if (!Spend()) return false;
            int overlaps = UnityPhysics.OverlapSphereNonAlloc(origin, radius, _overlaps, _mask, QueryTriggerInteraction.Ignore);
            if (overlaps >= _overlaps.Length) { BufferSaturations++; return false; }
            bool unresolved = false;
            for (int i = 0; i < overlaps; i++)
            {
                Collider collider = _overlaps[i];
                if (Ignore(collider)) continue;
                InitialOverlaps++;
                if (!Spend()) return false;
                _querySphere.radius = radius;
                if (!UnityPhysics.ComputePenetration(_querySphere, origin, Quaternion.identity, collider,
                    collider.transform.position, collider.transform.rotation, out Vector3 direction, out float distance))
                { unresolved = true; continue; }
                Vector3 point = origin + direction * (distance - radius);
                if (!Resolver.TryResolve(collider, point, footprint, out FireSurfaceAnchor anchor, out _))
                { unresolved = true; continue; }
                if (!cache.Add(in anchor, time)) unresolved = true;
                // A deeply embedded domain must be rejected; do not teleport it across geometry.
                if (distance > 0.08f) unresolved = true;
                else origin += direction * (distance + 0.015f);
            }
            if (unresolved) { UnresolvedContacts++; return false; }
            Vector3 delta = target - origin;
            float length = delta.magnitude;
            if (length < 0.00001f) { permitted = origin; return true; }
            if (!Spend()) return false;
            int hits = UnityPhysics.SphereCastNonAlloc(origin, radius, delta / length, _hits, length, _mask, QueryTriggerInteraction.Ignore);
            if (hits >= _hits.Length) { BufferSaturations++; return false; }
            int nearest = -1;
            for (int i = 0; i < hits; i++)
                if (!Ignore(_hits[i].collider) && (nearest < 0 || _hits[i].distance < _hits[nearest].distance)) nearest = i;
            if (nearest < 0) { permitted = target; return true; }
            RaycastHit hit = _hits[nearest];
            permitted = origin + delta / length * math.max(hit.distance - 0.015f, 0);
            if (!Resolver.TryResolve(hit.collider, hit.point, footprint, out FireSurfaceAnchor surface, out _))
            { UnresolvedContacts++; return false; }
            if (!cache.Add(in surface, time)) { UnresolvedContacts++; return false; }
            return true;
        }
    }
}
