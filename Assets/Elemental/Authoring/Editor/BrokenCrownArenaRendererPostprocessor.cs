using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Makes the arena's renderer policy an import-time asset contract. Scene
    /// instances then remain deterministic after any forced FBX reimport.
    /// </summary>
    public sealed class BrokenCrownArenaRendererPostprocessor : AssetPostprocessor
    {
        public const int ContractVersion = 2;

        private void OnPostprocessModel(GameObject importedRoot)
        {
            if (!string.Equals(
                    assetPath,
                    BrokenCrownArenaImporter.ModelPath,
                    StringComparison.Ordinal) || importedRoot == null) return;

            Renderer[] renderers = importedRoot.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            }
        }
    }
}
