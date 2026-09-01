using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Elemental.Presentation.Rendering
{
    public sealed class CapsuleContactShadowDebugRenderPass : ScriptableRenderPass
    {
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int BlitTextureTexelSizeId =
            Shader.PropertyToID("_BlitTexture_TexelSize");
        private static readonly int CameraDepthTextureId =
            Shader.PropertyToID("_CameraDepthTexture");

        private sealed class PassData
        {
            public TextureHandle Source;
            public TextureHandle Depth;
            public Material Material;
            public Vector4 BlitTextureTexelSize;
        }

        private Material _material;

        public CapsuleContactShadowDebugRenderPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth);
            requiresIntermediateTexture = true;
        }

        public void Setup(Material material)
        {
            _material = material;
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (_material == null)
                return;
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
                return;
            TextureHandle source = resources.activeColorTexture;
            TextureHandle depth = resources.activeDepthTexture;
            if (!source.IsValid() || !depth.IsValid())
                return;

            TextureDesc descriptor = renderGraph.GetTextureDesc(source);
            descriptor.name = "Elemental Capsule Contact Shadow Debug";
            descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            descriptor.format = GraphicsFormat.R16G16B16A16_SFloat;
            descriptor.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(descriptor);

            using IRasterRenderGraphBuilder builder =
                renderGraph.AddRasterRenderPass<PassData>(
                    "Elemental Capsule Contact Shadow-Only Debug",
                    out PassData passData);
            passData.Source = source;
            passData.Depth = depth;
            passData.Material = _material;
            passData.BlitTextureTexelSize = new Vector4(
                1f / Mathf.Max(1, descriptor.width),
                1f / Mathf.Max(1, descriptor.height),
                descriptor.width,
                descriptor.height);
            builder.UseTexture(source, AccessFlags.Read);
            builder.UseTexture(depth, AccessFlags.Read);
            builder.UseAllGlobalTextures(true);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
            builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
            {
                context.cmd.SetGlobalTexture(BlitTextureId, data.Source);
                context.cmd.SetGlobalTexture(CameraDepthTextureId, data.Depth);
                context.cmd.SetGlobalVector(
                    BlitScaleBiasId,
                    new Vector4(1f, 1f, 0f, 0f));
                context.cmd.SetGlobalVector(
                    BlitTextureTexelSizeId,
                    data.BlitTextureTexelSize);
                CoreUtils.DrawFullScreen(context.cmd, data.Material, null, 0);
                CapsuleContactShadowDiagnostics.MarkDebugViewRendered();
            });
            resources.cameraColor = destination;
        }
    }
}
