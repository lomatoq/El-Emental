using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Elemental.Presentation.Rendering
{
    public sealed class CapsuleContactShadowFeature : ScriptableRendererFeature
    {
        [SerializeField] private DuelRenderingProfile profile = null;
        [SerializeField] private Shader debugShader = null;

        private CapsuleContactShadowRenderPass _pass;
        private CapsuleContactShadowDebugRenderPass _debugPass;
        private Material _debugMaterial;
        private bool _reportedInvalidSetup;
        private bool _reportedMissingDebugShader;

        public override void Create()
        {
            ClearGlobalState();
            CoreUtils.Destroy(_debugMaterial);
            _debugMaterial = debugShader != null
                ? CoreUtils.CreateEngineMaterial(debugShader)
                : null;
            _pass = new CapsuleContactShadowRenderPass();
            _debugPass = new CapsuleContactShadowDebugRenderPass();
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            bool hasCaptureOverride = CapsuleContactShadowCaptureOverride.TryGet(
                out CapsuleContactShadowRuntimeSettings captureSettings);
            bool requested = hasCaptureOverride ||
                (profile != null && profile.UseCapsuleContactShadows);
            bool supportedCamera =
                renderingData.cameraData.cameraType == CameraType.Game &&
                renderingData.cameraData.renderType == CameraRenderType.Base;
            if (!requested)
            {
                ClearGlobalState();
                return;
            }
            if (!supportedCamera || _pass == null)
            {
                DisableGlobalVectors();
                CapsuleContactShadowDiagnostics.PublishDisabled(true);
                return;
            }

            CapsuleContactShadowRuntimeSettings settings = hasCaptureOverride
                ? captureSettings
                : profile.CapsuleContactShadows.CreateRuntimeSettings();
            if (!_pass.Setup(settings))
            {
                DisableGlobalVectors();
                ReportInvalidSetupOnce();
                return;
            }

            _reportedInvalidSetup = false;
            renderer.EnqueuePass(_pass);
            if (settings.DebugView != CapsuleContactShadowDebugView.ShadowOnly)
                return;
            if (_debugPass != null && _debugMaterial != null)
            {
                _reportedMissingDebugShader = false;
                _debugPass.Setup(_debugMaterial);
                renderer.EnqueuePass(_debugPass);
                return;
            }
            if (!_reportedMissingDebugShader)
            {
                _reportedMissingDebugShader = true;
                Debug.LogError(
                    "Capsule contact-shadow ShadowOnly diagnostics were requested, " +
                    "but the renderer feature has no valid debug shader.");
            }
        }

        private void ReportInvalidSetupOnce()
        {
            if (_reportedInvalidSetup)
                return;
            _reportedInvalidSetup = true;
            Debug.LogError(
                "Capsule contact shadows were enabled without an admitted active proxy. " +
                "Bind a character or hero-rock caster with a current uint generation.");
        }

        /// <summary>
        /// Clears both shader controls and diagnostic strength. Capture and test
        /// owners may call this when restoring the shipping OFF state.
        /// </summary>
        public static void ClearGlobalState()
        {
            DisableGlobalVectors();
            CapsuleContactShadowDiagnostics.PublishDisabled(false);
        }

        private static void DisableGlobalVectors()
        {
            Shader.SetGlobalVector(
                CapsuleContactShadowRenderPass.ShadowParamsId,
                Vector4.zero);
            Shader.SetGlobalVector(
                CapsuleContactShadowRenderPass.BiasDebugParamsId,
                Vector4.zero);
        }

        private void OnDisable()
        {
            ClearGlobalState();
        }

        protected override void Dispose(bool disposing)
        {
            ClearGlobalState();
            CoreUtils.Destroy(_debugMaterial);
            _debugMaterial = null;
        }
    }
}
