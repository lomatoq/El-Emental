using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Elemental.Presentation.Rendering
{
    public sealed class DuelShadowRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private DuelRenderingProfile profile = null;
        [SerializeField] private Shader casterShader = null;
        [SerializeField] private Shader debugShader = null;

        private DuelShadowRenderPass _pass;
        private DuelShadowDebugRenderPass _debugPass;
        private Material _casterMaterial;
        private Material _debugMaterial;
        private bool _reportedInvalidSetup = false;
        private bool _reportedMissingDebugShader = false;

        public override void Create()
        {
            CoreUtils.Destroy(_casterMaterial);
            CoreUtils.Destroy(_debugMaterial);
            _casterMaterial = casterShader != null
                ? CoreUtils.CreateEngineMaterial(casterShader)
                : null;
            _debugMaterial = debugShader != null
                ? CoreUtils.CreateEngineMaterial(debugShader)
                : null;
            _pass = new DuelShadowRenderPass();
            _debugPass = new DuelShadowDebugRenderPass();
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            bool requested = profile != null && profile.UseDuelShadowMap;
            bool supportedCamera =
                renderingData.cameraData.cameraType == CameraType.Game &&
                renderingData.cameraData.renderType == CameraRenderType.Base;
            DuelShadowBoundsProvider boundsProvider = DuelShadowBoundsProvider.Active;
            if (!requested ||
                !supportedCamera ||
                _pass == null ||
                _casterMaterial == null ||
                boundsProvider == null ||
                !boundsProvider.TryGetCoverage(
                    out Bounds coverage,
                    out Vector3 lightDirection,
                    out Vector3 referenceUp))
            {
                Shader.SetGlobalVector(
                    DuelShadowRenderPass.ShadowParamsId,
                    Vector4.zero);
                if (requested && supportedCamera)
                {
                    _pass?.ResetStabilization();
                    ReportInvalidSetupOnce();
                }
                DuelShadowDiagnostics.PublishDisabled(requested);
                return;
            }

            DuelShadowRuntimeSettings settings =
                profile.DuelShadows.CreateRuntimeSettings();
            if (_pass.Setup(
                    _casterMaterial,
                    settings,
                    coverage,
                    lightDirection,
                    referenceUp))
            {
                _reportedInvalidSetup = false;
                renderer.EnqueuePass(_pass);
                if (settings.DebugView == DuelShadowDebugView.ShadowOnly &&
                    _debugPass != null &&
                    _debugMaterial != null)
                {
                    _reportedMissingDebugShader = false;
                    _debugPass.Setup(_debugMaterial);
                    renderer.EnqueuePass(_debugPass);
                }
                else if (settings.DebugView == DuelShadowDebugView.ShadowOnly &&
                         !_reportedMissingDebugShader)
                {
                    _reportedMissingDebugShader = true;
                    Debug.LogError(
                        "Duel shadow ShadowOnly diagnostics were requested, but the " +
                        "renderer feature has no valid debug shader.");
                }
            }
            else
            {
                Shader.SetGlobalVector(
                    DuelShadowRenderPass.ShadowParamsId,
                    Vector4.zero);
                ReportInvalidSetupOnce();
            }
        }

        private void ReportInvalidSetupOnce()
        {
            if (_reportedInvalidSetup)
                return;
            _reportedInvalidSetup = true;
            Debug.LogError(
                "Duel shadows were enabled but no valid pass could be enqueued. " +
                "Assign the profile/caster shader and one complete bounds provider, " +
                "then register at least one admitted opaque caster.");
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_casterMaterial);
            CoreUtils.Destroy(_debugMaterial);
            _casterMaterial = null;
            _debugMaterial = null;
        }
    }
}
