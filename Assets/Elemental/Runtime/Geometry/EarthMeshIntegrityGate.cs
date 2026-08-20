using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    public static class EarthMeshIntegrityGate
    {
        public static Mesh PublishOrFallback(
            Mesh candidate,
            in EarthMeshIntegrityPolicy policy,
            string publicationOwner,
            Bounds fallbackBounds)
        {
            EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(candidate, policy);
            if (report.IsValid) return candidate;

            if (EarthMeshIntegrityValidator.TryRepairFullyInvertedClosedMesh(candidate, out EarthMeshIntegrityReport repaired) &&
                repaired.IsValid)
            {
                Debug.LogWarning($"[EarthGeometry] Safely reversed fully inverted mesh for {publicationOwner}: {repaired}");
                return candidate;
            }

            Debug.LogError($"[EarthGeometry] Blocked invalid runtime mesh for {publicationOwner}: {report}");
            Mesh fallback = EarthSafeMeshFactory.CreateBox($"{publicationOwner}_IntegrityFallback", fallbackBounds);
            EarthMeshIntegrityReport fallbackReport = EarthMeshIntegrityValidator.Validate(fallback, policy);
            if (!fallbackReport.IsValid)
                Debug.LogError($"[EarthGeometry] Deterministic fallback failed validation: {fallbackReport}");
            return fallback;
        }

        public static bool ValidateInPlaceOrUseFallback(
            Mesh candidate,
            in EarthMeshIntegrityPolicy policy,
            string publicationOwner,
            Bounds fallbackBounds)
        {
            EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(candidate, policy);
            if (report.IsValid) return true;
            if (EarthMeshIntegrityValidator.TryRepairFullyInvertedClosedMesh(candidate, out EarthMeshIntegrityReport repaired) &&
                repaired.IsValid)
            {
                Debug.LogWarning($"[EarthGeometry] Safely reversed fully inverted mesh for {publicationOwner}: {repaired}");
                return true;
            }

            Debug.LogError($"[EarthGeometry] Blocked invalid runtime mesh for {publicationOwner}: {report}");
            Mesh fallback = EarthSafeMeshFactory.CreateBox($"{publicationOwner}_IntegrityFallback", fallbackBounds);
            candidate.Clear();
            candidate.name = fallback.name;
            candidate.vertices = fallback.vertices;
            candidate.normals = fallback.normals;
            candidate.tangents = fallback.tangents;
            candidate.uv = fallback.uv;
            candidate.triangles = fallback.triangles;
            candidate.RecalculateBounds();
            if (Application.isPlaying) Object.Destroy(fallback);
            else Object.DestroyImmediate(fallback);
            return false;
        }
    }
}
