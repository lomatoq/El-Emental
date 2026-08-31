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
        internal const string RendererPath = "Assets/Settings/ElEmentalRenderer.asset";

        internal static void ConfigureProfiles()
        {
            UniversalRenderPipelineAsset high =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(NativeHighPath);
            if (high == null)
                throw new UnityEditor.Build.BuildFailedException(
                    $"Native High URP asset is missing at {NativeHighPath}.");

            ConfigureNativeHigh(high);
            ConfigureContactSsao();
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
            asset.shadowDistance = 90f;
            asset.shadowCascadeCount = 4;
            asset.cascade4Split = new Vector3(0.08f, 0.22f, 0.48f);
            asset.shadowDepthBias = 0.50f;
            asset.shadowNormalBias = 0.30f;
            SetShadowSupport(asset, true, true, 3);
        }

        private static void ConfigureContactSsao()
        {
            UniversalRendererData renderer =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
                throw new UnityEditor.Build.BuildFailedException(
                    $"Native renderer asset is missing at {RendererPath}.");
            ScriptableRendererFeature contactAo = null;
            for (int index = 0; index < renderer.rendererFeatures.Count; index++)
            {
                ScriptableRendererFeature feature = renderer.rendererFeatures[index];
                if (feature != null && feature.name == "Elemental Contact SSAO")
                {
                    contactAo = feature;
                    break;
                }
            }
            if (contactAo == null)
                throw new UnityEditor.Build.BuildFailedException(
                    "Elemental Contact SSAO renderer feature is missing.");

            var serialized = new SerializedObject(contactAo);
            // Authored DepthNormals are required for the stylized arena shader.
            // Full-resolution bilateral AO keeps contact/crease depth visible on
            // the Game camera without turning broad surfaces into a dirty wash.
            SetBool(serialized, "m_Settings.Downsample", false);
            SetBool(serialized, "m_Settings.AfterOpaque", false);
            SetInt(serialized, "m_Settings.Source", 1);
            SetFloat(serialized, "m_Settings.Intensity", 0.82f);
            SetFloat(serialized, "m_Settings.DirectLightingStrength", 0.08f);
            SetFloat(serialized, "m_Settings.Radius", 0.065f);
            SetInt(serialized, "m_Settings.NormalSamples", 2);
            SetInt(serialized, "m_Settings.Samples", 0);
            SetInt(serialized, "m_Settings.BlurQuality", 0);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(contactAo);
            EditorUtility.SetDirty(renderer);
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
            SetShadowSupport(asset, true, false, 2);
        }

        private static void SetShadowSupport(
            UniversalRenderPipelineAsset asset,
            bool mainLightShadows,
            bool softShadows,
            int softShadowQuality)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_MainLightShadowsSupported").boolValue = mainLightShadows;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = softShadows;
            SerializedProperty quality = serialized.FindProperty("m_SoftShadowQuality");
            if (quality != null) quality.intValue = Mathf.Clamp(softShadowQuality, 1, 3);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(SerializedObject serialized, string path, float value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property == null)
                throw new UnityEditor.Build.BuildFailedException(
                    $"Renderer property '{path}' is missing.");
            property.floatValue = value;
        }

        private static void SetInt(SerializedObject serialized, string path, int value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property == null)
                throw new UnityEditor.Build.BuildFailedException(
                    $"Renderer property '{path}' is missing.");
            property.intValue = value;
        }

        private static void SetBool(SerializedObject serialized, string path, bool value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property == null)
                throw new UnityEditor.Build.BuildFailedException(
                    $"Renderer property '{path}' is missing.");
            property.boolValue = value;
        }
    }
}
