using Elemental.Simulation.Structures;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>
    /// Thin renderer adapter for the pure fracture mapping frame. The property
    /// block is merged rather than cleared so unrelated presentation overrides
    /// survive intact/fractured proxy changes.
    /// </summary>
    public static class EarthArenaFractureShading
    {
        public static readonly int MappingEnabledId =
            Shader.PropertyToID("_FractureMappingEnabled");
        public static readonly int LocalToStructureId =
            Shader.PropertyToID("_FractureLocalToStructure");

        public static bool Apply(
            Renderer renderer,
            in EarthFractureMappingFrame frame,
            MaterialPropertyBlock properties)
        {
            if (renderer == null || !frame.IsValid || properties == null) return false;
            Material shared = renderer.sharedMaterial;
            if (shared == null || !shared.HasProperty(MappingEnabledId)) return false;

            renderer.GetPropertyBlock(properties);
            properties.SetFloat(MappingEnabledId, 1f);
            properties.SetMatrix(LocalToStructureId, ToMatrix(frame.LocalToStructure));
            renderer.SetPropertyBlock(properties);
            return true;
        }

        public static float4x4 ToFloat4x4(Matrix4x4 value) => new(
            new float4(value.m00, value.m10, value.m20, value.m30),
            new float4(value.m01, value.m11, value.m21, value.m31),
            new float4(value.m02, value.m12, value.m22, value.m32),
            new float4(value.m03, value.m13, value.m23, value.m33));

        public static Matrix4x4 ToMatrix(float4x4 value)
        {
            var result = new Matrix4x4();
            result.SetColumn(0, ToVector4(value.c0));
            result.SetColumn(1, ToVector4(value.c1));
            result.SetColumn(2, ToVector4(value.c2));
            result.SetColumn(3, ToVector4(value.c3));
            return result;
        }

        private static Vector4 ToVector4(float4 value) =>
            new(value.x, value.y, value.z, value.w);
    }
}
