using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Elemental.Presentation.Rendering
{
    public sealed class CapsuleContactShadowRenderPass : ScriptableRenderPass
    {
        public static readonly int StartRadiusId =
            Shader.PropertyToID("_ElementalCapsuleShadowStartRadius");
        public static readonly int EndSoftnessId =
            Shader.PropertyToID("_ElementalCapsuleShadowEndSoftness");
        public static readonly int ShadowParamsId =
            Shader.PropertyToID("_ElementalCapsuleShadowParams");
        public static readonly int BiasDebugParamsId =
            Shader.PropertyToID("_ElementalCapsuleShadowBiasDebugParams");

        private sealed class PassData
        {
            public Vector4[] StartRadius;
            public Vector4[] EndSoftness;
            public Vector4 ShadowParams;
            public Vector4 BiasDebugParams;
        }

        private readonly Vector4[] _startRadius =
            new Vector4[CapsuleShadowBuffer.MaximumProxyCount];
        private readonly Vector4[] _endSoftness =
            new Vector4[CapsuleShadowBuffer.MaximumProxyCount];
        private readonly ProfilingSampler _profilingSampler =
            new ProfilingSampler("Elemental Capsule Contact Shadows");
        private Vector4 _shadowParams;
        private Vector4 _biasDebugParams;
        private int _proxyCount;

        public CapsuleContactShadowRenderPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        }

        public bool Setup(in CapsuleContactShadowRuntimeSettings settings)
        {
            CapsuleShadowBuffer buffer = CapsuleShadowBuffer.Shared;
            _proxyCount = buffer.CopyActiveProxies(
                _startRadius,
                _endSoftness,
                settings,
                out int activeCasterCount,
                out int rejectedCasterCount,
                out int rejectedProxyCount);
            _shadowParams = new Vector4(
                _proxyCount > 0 ? 1f : 0f,
                settings.ShadowStrength,
                settings.MaximumContactDistance,
                _proxyCount);
            _biasDebugParams = new Vector4(
                settings.SurfaceBias,
                settings.NormalBias,
                settings.DebugView == CapsuleContactShadowDebugView.ShadowOnly ? 1f : 0f,
                0f);
            CapsuleContactShadowDiagnostics.Publish(
                new CapsuleContactShadowDiagnosticsSnapshot(
                    true,
                    false,
                    false,
                    Time.frameCount,
                    buffer.Count,
                    activeCasterCount,
                    _proxyCount,
                    rejectedCasterCount,
                    rejectedProxyCount,
                    buffer.CapacityRejectCount,
                    buffer.GenerationRejectCount,
                    settings.MaximumContactDistance));
            return _proxyCount > 0;
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (_proxyCount <= 0)
                return;
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            TextureHandle color = resources.activeColorTexture;
            if (!color.IsValid())
                return;

            using IRasterRenderGraphBuilder builder =
                renderGraph.AddRasterRenderPass<PassData>(
                    "Elemental Capsule Contact Shadow Upload",
                    out PassData passData,
                    _profilingSampler);
            passData.StartRadius = _startRadius;
            passData.EndSoftness = _endSoftness;
            passData.ShadowParams = _shadowParams;
            passData.BiasDebugParams = _biasDebugParams;
            builder.SetRenderAttachment(color, 0, AccessFlags.ReadWrite);
            builder.AllowGlobalStateModification(true);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
            {
                context.cmd.SetGlobalVectorArray(StartRadiusId, data.StartRadius);
                context.cmd.SetGlobalVectorArray(EndSoftnessId, data.EndSoftness);
                context.cmd.SetGlobalVector(ShadowParamsId, data.ShadowParams);
                context.cmd.SetGlobalVector(BiasDebugParamsId, data.BiasDebugParams);
                CapsuleContactShadowDiagnostics.MarkBufferUploaded();
            });
        }
    }
}
