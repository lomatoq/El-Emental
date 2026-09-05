using Elemental.Simulation.Structures;
using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    public enum EarthContainedFitFailure { None, InvalidParent, DegenerateCell, EmptyTemplate, TooSmall, FinalContainment }
    /// <summary>
    /// Fits an already cooked stone inside the real convex parent and one disjoint child cell.
    /// Tests render AND collision vertices after rotation, scale and ancestor transforms.
    /// Convexity then guarantees that every triangle and the cooked child hull also fit.
    /// </summary>
    public static class EarthContainedFractureFit
    {
        public static bool TryGetChord(Collider parent, List<Vector3> scratch,
            out Vector3 first, out Vector3 last)
        {
            first = last = default;
            if (parent == null || !parent.enabled || !parent.gameObject.activeInHierarchy) return false;
            Vector3 center;
            if (parent is MeshCollider mesh)
            {
                if (!mesh.convex || mesh.sharedMesh == null || !mesh.sharedMesh.isReadable) return false;
                mesh.sharedMesh.GetVertices(scratch);
                if (scratch.Count < 4) return false;
                center = Vector3.zero;
                foreach (Vector3 vertex in scratch) center += vertex;
                center = parent.transform.TransformPoint(center / scratch.Count);
            }
            else if (parent is BoxCollider box) center = box.transform.TransformPoint(box.center);
            else if (parent is SphereCollider sphere) center = sphere.transform.TransformPoint(sphere.center);
            else if (parent is CapsuleCollider capsule) center = capsule.transform.TransformPoint(capsule.center);
            else return false;
            GetShapeAxes(parent, out Vector3 x, out Vector3 y, out Vector3 z);
            Vector3 axis = x.sqrMagnitude >= y.sqrMagnitude && x.sqrMagnitude >= z.sqrMagnitude ? x.normalized :
                y.sqrMagnitude >= z.sqrMagnitude ? y.normalized : z.normalized;
            float reach = parent.bounds.size.magnitude * 2f;
            // Intersect a line THROUGH the interior barycenter. ClosestPoint at two
            // distant supports can join two vertices along a tetrahedron edge.
            if (!parent.Raycast(new Ray(center - axis * reach, axis), out var left, reach * 2f) ||
                !parent.Raycast(new Ray(center + axis * reach, -axis), out var right, reach * 2f)) return false;
            first = Vector3.Lerp(left.point, center, .02f);
            last = Vector3.Lerp(right.point, center, .02f);
            return (last - first).sqrMagnitude > 1e-8f;
        }

        public static bool TryFit(Collider parent, Transform child, Vector3[] vertices,
            Vector3 first, Vector3 last, int index, int count, Quaternion rotation, out float radius,
            out EarthContainedFitFailure failure)
        {
            radius = 0f;
            failure = EarthContainedFitFailure.InvalidParent;
            if (parent == null || !parent.enabled || !parent.gameObject.activeInHierarchy ||
                vertices == null || vertices.Length < 4 || child == null ||
                (parent is MeshCollider mesh && !mesh.convex) ||
                !(parent is MeshCollider || parent is BoxCollider || parent is SphereCollider || parent is CapsuleCollider))
                return false;
            Bounds bounds = parent.bounds;
            Vector3 size = bounds.size;
            failure = EarthContainedFitFailure.DegenerateCell;
            if (!EarthContainedFractureLayout.TryGetCell(first, last, index, count, out var cell)) return false;
            Vector3 center = cell.Center;
            Bounds templateBounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Length; i++) templateBounds.Encapsulate(vertices[i]);
            Vector3 chord = cell.Axis;
            Vector3 rollUp = Vector3.ProjectOnPlane(rotation * Vector3.up, chord).normalized;
            if (rollUp.sqrMagnitude < .001f)
                rollUp = Vector3.Cross(chord, Mathf.Abs(chord.y) < .9f ? Vector3.up : Vector3.right).normalized;
            child.SetPositionAndRotation(center, Quaternion.LookRotation(chord, rollUp));
            // Give each child its cell's aspect before the containment search. A long column
            // becomes short chunky stones instead of four tiny inscribed spheres.
            Vector3 templateSize = templateBounds.size;
            Vector3 right = child.right, up = child.up;
            GetShapeAxes(parent, out Vector3 sourceX, out Vector3 sourceY, out Vector3 sourceZ);
            float spanX = Mathf.Abs(Vector3.Dot(right, sourceX)) + Mathf.Abs(Vector3.Dot(right, sourceY)) + Mathf.Abs(Vector3.Dot(right, sourceZ));
            float spanY = Mathf.Abs(Vector3.Dot(up, sourceX)) + Mathf.Abs(Vector3.Dot(up, sourceY)) + Mathf.Abs(Vector3.Dot(up, sourceZ));
            Vector3 initialScale = new Vector3(spanX / Mathf.Max(.00001f, templateSize.x),
                spanY / Mathf.Max(.00001f, templateSize.y),
                cell.HalfWidth * 2f / Mathf.Max(.00001f, templateSize.z));
            child.localScale = initialScale;
            Matrix4x4 basis = child.localToWorldMatrix;
            Vector3 templateCenter = templateBounds.center;
            float extent = 0f;
            for (int i = 0; i < vertices.Length; i++)
                extent = Mathf.Max(extent, basis.MultiplyVector(vertices[i] - templateCenter).magnitude);
            failure = EarthContainedFitFailure.EmptyTemplate;
            if (extent < .00001f) return false;
            float low = 0f, high = size.magnitude / extent;
            for (int step = 0; step < 15; step++)
            {
                float scale = (low + high) * .5f;
                if (Fits(parent, vertices, basis, templateCenter, cell, scale)) low = scale;
                else high = scale;
            }
            float fitted = low * .98f;
            radius = fitted * extent;
            failure = EarthContainedFitFailure.TooSmall;
            if (fitted * extent < .0001f) return false;
            child.localScale = initialScale * fitted;
            child.position = center - basis.MultiplyVector(templateCenter) * fitted;
            bool contained = Fits(parent, vertices, basis, templateCenter, cell, fitted);
            failure = contained ? EarthContainedFitFailure.None : EarthContainedFitFailure.FinalContainment;
            return contained;
        }

        private static void GetShapeAxes(Collider parent, out Vector3 x, out Vector3 y, out Vector3 z)
        {
            Vector3 size = parent is MeshCollider mesh && mesh.sharedMesh != null ? mesh.sharedMesh.bounds.size :
                parent is BoxCollider box ? box.size : Vector3.one;
            if (parent is SphereCollider sphere) size = Vector3.one * (sphere.radius * 2f);
            if (parent is CapsuleCollider capsule)
            {
                size = Vector3.one * (capsule.radius * 2f);
                size[capsule.direction] = capsule.height;
            }
            x = parent.transform.TransformVector(Vector3.right * size.x);
            y = parent.transform.TransformVector(Vector3.up * size.y);
            z = parent.transform.TransformVector(Vector3.forward * size.z);
        }

        private static bool Fits(Collider parent, Vector3[] vertices, Matrix4x4 basis,
            Vector3 templateCenter, EarthContainedFractureCell cell, float scale)
        {
            Vector3 center = cell.Center;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = center + basis.MultiplyVector(vertices[i] - templateCenter) * scale;
                if (!cell.Contains(point) || (parent.ClosestPoint(point) - point).sqrMagnitude > 1e-10f)
                    return false;
            }
            return true;
        }
    }
}
