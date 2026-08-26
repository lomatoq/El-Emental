using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Keeps the native MVP shadow rescue isolated from the low/Web render path.
    /// Both assets share the same renderer data; only their cost/quality policy differs.
    /// </summary>
    internal static class EarthRenderQualitySetup
    {
        internal const string NativeHighPath = "Assets/Settings/ElEmentalURP.asset";
        internal const string LowWebPath = "Assets/Settings/ElEmentalURPLowWeb.asset";

        internal static void ConfigureProfiles()
        {
            UniversalRenderPipelineAsset high =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(NativeHighPath);
            if (high == null)
                throw new UnityEditor.Build.BuildFailedException(
                    $"Native High URP asset is missing at {NativeHighPath}.");

            ConfigureNativeHigh(high);
            UniversalRenderPipelineAsset low =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(LowWebPath);
            if (low == null)
            {
                low = Object.Instantiate(high);
                low.name = "ElEmental URP Low Web";
                AssetDatabase.CreateAsset(low, LowWebPath);
            }
            ConfigureLowWeb(low);

            int previousQuality = QualitySettings.GetQualityLevel();
            string[] qualityNames = QualitySettings.names;
            for (int index = 0; index < qualityNames.Length; index++)
            {
                QualitySettings.SetQualityLevel(index, false);
                QualitySettings.renderPipeline = index == qualityNames.Length - 1 ? high : low;
            }
            QualitySettings.SetQualityLevel(previousQuality, false);
            EditorUtility.SetDirty(high);
            EditorUtility.SetDirty(low);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureNativeHigh(UniversalRenderPipelineAsset asset)
        {
            asset.supportsCameraDepthTexture = true;
            asset.mainLightShadowmapResolution = 4096;
            asset.shadowDistance = 48f;
            asset.shadowCascadeCount = 4;
            asset.cascade4Split = new Vector3(0.08f, 0.22f, 0.48f);
            asset.shadowDepthBias = 0.90f;
            asset.shadowNormalBias = 0.55f;
            SetShadowSupport(asset, true, true);
        }

        private static void ConfigureLowWeb(UniversalRenderPipelineAsset asset)
        {
            asset.supportsCameraDepthTexture = true;
            asset.mainLightShadowmapResolution = 2048;
            asset.shadowDistance = 40f;
            asset.shadowCascadeCount = 2;
            asset.cascade2Split = 0.25f;
            asset.shadowDepthBias = 0.75f;
            asset.shadowNormalBias = 0.32f;
            SetShadowSupport(asset, true, false);
        }

        private static void SetShadowSupport(
            UniversalRenderPipelineAsset asset,
            bool mainLightShadows,
            bool softShadows)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_MainLightShadowsSupported").boolValue = mainLightShadows;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = softShadows;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
