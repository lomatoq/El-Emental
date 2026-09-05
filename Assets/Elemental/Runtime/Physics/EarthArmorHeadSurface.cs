using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>Cast-time skin sampling only. Formation follows cached head-local anchors.</summary>
    public static class EarthArmorHeadSurface
    {
        public static bool TryMeasure(Animator animator, out float3[] points, out Vector3 center)
        {
            points = null;
            center = default;
            if (animator == null || !animator.isHuman) return false;
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return false;
            var samples = new List<float3>();
            Bounds bounds = default;
            foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (!renderer.enabled || renderer.sharedMesh == null) continue;
                Mesh mesh = renderer.sharedMesh;
                // Imported player meshes are not CPU-readable in a player build.
                // Bake a cast-time pose, with scale accounted for, then transform
                // its local vertices exactly once. Skin weights remain available.
                var posedMesh = new Mesh();
                Vector3[] vertices;
                try { renderer.BakeMesh(posedMesh, true); vertices = posedMesh.vertices; }
                finally
                {
                    if (Application.isPlaying) Object.Destroy(posedMesh);
                    else Object.DestroyImmediate(posedMesh);
                }
                var weights = mesh.boneWeights;
                var bones = renderer.bones;
                if (weights.Length != vertices.Length || bones.Length == 0) continue;
                var headBones = new bool[bones.Length];
                for (int b = 0; b < bones.Length; b++)
                {
                    if (bones[b] == null) continue;
                    headBones[b] = bones[b] == head || bones[b].IsChildOf(head);
                }
                for (int i = 0; i < vertices.Length; i++)
                {
                    BoneWeight w = weights[i];
                    float headWeight = (headBones[w.boneIndex0] ? w.weight0 : 0f) +
                        (headBones[w.boneIndex1] ? w.weight1 : 0f) +
                        (headBones[w.boneIndex2] ? w.weight2 : 0f) +
                        (headBones[w.boneIndex3] ? w.weight3 : 0f);
                    if (headWeight < .5f) continue;
                    Vector3 p = renderer.transform.TransformPoint(vertices[i]);
                    if (samples.Count == 0) bounds = new Bounds(p, Vector3.zero);
                    else bounds.Encapsulate(p);
                    samples.Add(p);
                }
            }
            if (samples.Count == 0) return false;
            points = samples.ToArray();
            center = bounds.center;
            return true;
        }
    }
}
