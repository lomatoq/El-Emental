using Elemental.Runtime.Characters;
using UnityEngine;

namespace Elemental.Tests.PlayMode
{
    internal static class SurfPillarQaLane
    {
        private const int DirectionCount = 32;
        private const float ProbeRadius = 1.15f;
        private const float ProbeDistance = 10f;
        private static readonly RaycastHit[] Hits = new RaycastHit[64];
        private static readonly RaycastHit[] SupportHits = new RaycastHit[32];

        public static Vector3 FindClearDirection(
            Rigidbody rider,
            PlanetMotor motor,
            Vector3 fallback,
            out float clearance)
        {
            Vector3 up = motor != null && motor.LocalUp.sqrMagnitude > 0.5f
                ? motor.LocalUp.normalized
                : rider.transform.up;
            Vector3 seed = Vector3.ProjectOnPlane(fallback, up).normalized;
            if (seed.sqrMagnitude < 0.5f)
                seed = Vector3.ProjectOnPlane(rider.transform.forward, up).normalized;
            Vector3 best = seed;
            clearance = Clearance(rider, up, seed);
            for (int index = 1; index < DirectionCount; index++)
            {
                Vector3 candidate = Quaternion.AngleAxis(
                    index * (360f / DirectionCount),
                    up) * seed;
                float candidateClearance = Clearance(rider, up, candidate);
                if (candidateClearance <= clearance) continue;
                best = candidate;
                clearance = candidateClearance;
            }
            return best.normalized;
        }

        private static float Clearance(Rigidbody rider, Vector3 up, Vector3 direction)
        {
            Vector3 origin = rider.worldCenterOfMass + up * 0.18f;
            int count = Physics.SphereCastNonAlloc(
                origin,
                ProbeRadius,
                direction,
                Hits,
                ProbeDistance,
                ~0,
                QueryTriggerInteraction.Ignore);
            if (count >= Hits.Length) return 0f;
            float nearest = ProbeDistance;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = Hits[index];
                Collider collider = hit.collider;
                if (collider == null || collider.attachedRigidbody == rider ||
                    collider.transform.IsChildOf(rider.transform))
                    continue;
                // Curved ground can enter a broad horizontal sphere cast at its
                // lower rim. It is valid support, not a forward obstruction.
                if (Vector3.Dot(hit.normal, up) > 0.62f) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }
            // A visually open gap is not a valid lane if the arena floor ends
            // underneath it. Verify walkable support along the candidate sweep.
            for (float distance = 1.5f; distance <= Mathf.Min(nearest, 9f); distance += 1.5f)
            {
                Vector3 supportOrigin = rider.worldCenterOfMass +
                                        direction * distance +
                                        up * 0.55f;
                int supportCount = Physics.RaycastNonAlloc(
                    supportOrigin,
                    -up,
                    SupportHits,
                    2.8f,
                    ~0,
                    QueryTriggerInteraction.Ignore);
                bool supported = false;
                for (int supportIndex = 0; supportIndex < supportCount; supportIndex++)
                {
                    RaycastHit support = SupportHits[supportIndex];
                    Collider collider = support.collider;
                    if (collider == null || collider.attachedRigidbody == rider ||
                        collider.transform.IsChildOf(rider.transform))
                        continue;
                    if (Vector3.Dot(support.normal, up) < 0.5f) continue;
                    supported = true;
                    break;
                }
                if (!supported) return Mathf.Min(nearest, Mathf.Max(0f, distance - 1.5f));
            }
            return nearest;
        }
    }
}
