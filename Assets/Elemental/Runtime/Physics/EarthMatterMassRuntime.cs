using Elemental.Simulation.Structures;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>
    /// Unity-side geometry adapter for the pure earth-matter mass policy.
    /// It is intentionally the only place that converts authored Collider
    /// geometry into gameplay mass.
    /// </summary>
    public static class EarthMatterMassRuntime
    {
        public static float ResolveFromAuthoredPhysicalMass(float physicalMassKilograms)
        {
            EarthMatterMassProfile profile = EarthMatterMassProfile.ArenaStone;
            return EarthMatterMassPolicy.ResolveGameplayMassFromPhysicalMass(
                physicalMassKilograms,
                in profile);
        }

        public static float ResolveFromCollider(Collider shape, float fallbackRadius = 0.5f)
        {
            EarthMatterMassProfile profile = EarthMatterMassProfile.ArenaStone;
            float volume = EstimateColliderVolume(shape, fallbackRadius);
            return EarthMatterMassPolicy.ResolveGameplayMass(volume, in profile);
        }

        public static float EstimateColliderVolume(Collider shape, float fallbackRadius = 0.5f)
        {
            if (shape == null) return SphereVolume(Mathf.Max(0.01f, fallbackRadius));

            Vector3 scale = Abs(shape.transform.lossyScale);
            switch (shape)
            {
                case BoxCollider box:
                {
                    Vector3 size = Vector3.Scale(box.size, scale);
                    return EarthMatterMassPolicy.EstimateBoxVolume(ToFloat3(size), 1f);
                }
                case SphereCollider sphere:
                {
                    float radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                    return SphereVolume(radius);
                }
                case CapsuleCollider capsule:
                    return CapsuleVolume(capsule, scale);
                case MeshCollider mesh when mesh.sharedMesh != null:
                {
                    Vector3 size = Vector3.Scale(mesh.sharedMesh.bounds.size, scale);
                    // Fracture and decor meshes are irregular rocks. Their AABB is
                    // deliberately reduced by one shared solid-fill ratio rather
                    // than giving every content family a different fake density.
                    return EarthMatterMassPolicy.EstimateBoxVolume(ToFloat3(size), 0.62f);
                }
                default:
                {
                    Bounds bounds = shape.bounds;
                    if (bounds.size.sqrMagnitude > 0.000001f)
                        return EarthMatterMassPolicy.EstimateBoxVolume(
                            ToFloat3(bounds.size),
                            0.62f);
                    return SphereVolume(Mathf.Max(0.01f, fallbackRadius));
                }
            }
        }

        private static float CapsuleVolume(CapsuleCollider capsule, Vector3 scale)
        {
            float axisScale;
            float radiusScale;
            switch (capsule.direction)
            {
                case 0:
                    axisScale = scale.x;
                    radiusScale = Mathf.Max(scale.y, scale.z);
                    break;
                case 2:
                    axisScale = scale.z;
                    radiusScale = Mathf.Max(scale.x, scale.y);
                    break;
                default:
                    axisScale = scale.y;
                    radiusScale = Mathf.Max(scale.x, scale.z);
                    break;
            }

            float radius = Mathf.Max(0.001f, capsule.radius * radiusScale);
            float totalHeight = Mathf.Max(radius * 2f, capsule.height * axisScale);
            float cylinderHeight = Mathf.Max(0f, totalHeight - radius * 2f);
            return Mathf.PI * radius * radius * cylinderHeight + SphereVolume(radius);
        }

        private static float SphereVolume(float radius)
        {
            float safeRadius = Mathf.Max(0.001f, radius);
            return 4f / 3f * Mathf.PI * safeRadius * safeRadius * safeRadius;
        }

        private static Vector3 Abs(Vector3 value) => new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));

        private static float3 ToFloat3(Vector3 value) =>
            new float3(value.x, value.y, value.z);
    }
}
