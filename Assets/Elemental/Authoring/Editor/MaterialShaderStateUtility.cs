using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Rebuilds a material's local keyword state whenever authoring code swaps
    /// its shader. LocalKeyword values are tied to one Shader keyword space;
    /// carrying the old state across the assignment can make Unity's renderer
    /// reject the material, even when the serialized keyword names look valid.
    /// </summary>
    internal static class MaterialShaderStateUtility
    {
        public static void RebindShader(Material material, Shader shader)
        {
            if (material == null || shader == null) return;
            string[] requestedKeywords = material.shaderKeywords ?? Array.Empty<string>();
            material.shaderKeywords = Array.Empty<string>();
            if (material.shader != shader) material.shader = shader;
            RestoreCompatibleKeywords(material, requestedKeywords);
        }

        public static void CopyProperties(Material destination, Material source)
        {
            if (destination == null || source == null || source.shader == null) return;
            RebindShader(destination, source.shader);
            destination.CopyPropertiesFromMaterial(source);
            NormalizeKeywords(destination);
        }

        public static void NormalizeKeywords(Material material)
        {
            if (material == null || material.shader == null) return;
            string[] requestedKeywords = material.shaderKeywords ?? Array.Empty<string>();
            material.shaderKeywords = Array.Empty<string>();
            RestoreCompatibleKeywords(material, requestedKeywords);
        }

        private static void RestoreCompatibleKeywords(Material material, string[] requestedKeywords)
        {
            LocalKeywordSpace keywordSpace = material.shader.keywordSpace;
            for (int index = 0; index < requestedKeywords.Length; index++)
            {
                LocalKeyword keyword = keywordSpace.FindKeyword(requestedKeywords[index]);
                if (keyword.isValid) material.SetKeyword(keyword, true);
            }
        }
    }
}
