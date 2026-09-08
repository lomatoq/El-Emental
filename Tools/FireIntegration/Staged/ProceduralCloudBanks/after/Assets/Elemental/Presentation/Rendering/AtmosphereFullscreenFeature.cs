using UnityEngine;
using Elemental.Presentation.VFX;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RendererUtils;

namespace Elemental.Presentation.Rendering
{
    /// <summary>URP depth-aware atmosphere pass. The limb shell remains the outside-planet companion.</summary>
    public sealed class AtmosphereFullscreenFeature : ScriptableRendererFeature
    {
        [SerializeField] private Material material;
        private AtmospherePass _pass;

        public void Configure(Material configuredMaterial)
        {
            material = configuredMaterial;
            Create();
        }

        public override void Create()
        {
            _pass = new AtmospherePass(material)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material != null && renderingData.cameraData.cameraType == CameraType.Game)
                renderer.EnqueuePass(_pass);
        }

        private sealed class AtmospherePass : ScriptableRenderPass
        {
            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly int BlitTextureTexelSizeId =
                Shader.PropertyToID("_BlitTexture_TexelSize");
            private static readonly int CameraDepthTextureId =
                Shader.PropertyToID("_CameraDepthTexture");
            private readonly Material _material;

            public AtmospherePass(Material material)
            {
                _material = material;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null) return;
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                var targets = frameData.Get<UniversalCameraData>().camera.GetComponent<EarthSeismicCameraTargets>();
                if (resources.isActiveTargetBackBuffer) return;
                TextureHandle source = resources.activeColorTexture;
                TextureHandle depth = resources.activeDepthTexture;
                if (!source.IsValid() || !depth.IsValid()) return;
                TextureDesc destinationDescriptor = renderGraph.GetTextureDesc(source);
                destinationDescriptor.name = "Elemental Atmosphere Color";
                destinationDescriptor.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDescriptor);
                bool drawClouds=ValleyCloudParticles.HasActive || ProceduralCloudBanks.HasActive;
                RendererListHandle cloudList=default;
                if(drawClouds)
                {
                    var desc=new RendererListDesc(new ShaderTagId("ElementalValleyCloud"),frameData.Get<UniversalRenderingData>().cullResults,frameData.Get<UniversalCameraData>().camera)
                    {renderQueueRange=RenderQueueRange.transparent,sortingCriteria=SortingCriteria.CommonTransparent};
                    cloudList=renderGraph.CreateRendererList(desc);
                }
                using (IRasterRenderGraphBuilder builder =
                       renderGraph.AddRasterRenderPass<AtmospherePassData>(
                           "Elemental Atmosphere Fullscreen",
                           out AtmospherePassData passData))
                {
                    passData.clouds=cloudList;passData.drawClouds=drawClouds;
                    if(drawClouds)builder.UseRendererList(cloudList);
                    passData.source = source;
                    passData.depth = depth;
                    passData.material = _material;
                    passData.targets = targets != null && targets.isActiveAndEnabled && targets.Blend > 0f
                        ? targets.Targets : null;
                    passData.blitTexelSize = new Vector4(
                        1f / Mathf.Max(1, destinationDescriptor.width),
                        1f / Mathf.Max(1, destinationDescriptor.height),
                        destinationDescriptor.width,
                        destinationDescriptor.height);
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(depth, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (
                        AtmospherePassData data,
                        RasterGraphContext context) =>
                    {
                        // Bind the exact RG handles consumed by this pass. The old
                        // material blit relied on an implicit global depth owner and
                        // read sky depth across opaque arena geometry.
                        context.cmd.SetGlobalTexture(BlitTextureId, data.source);
                        context.cmd.SetGlobalTexture(CameraDepthTextureId, data.depth);
                        context.cmd.SetGlobalVector(
                            BlitScaleBiasId,
                            new Vector4(1f, 1f, 0f, 0f));
                        context.cmd.SetGlobalVector(
                            BlitTextureTexelSizeId,
                            data.blitTexelSize);
                        CoreUtils.DrawFullScreen(context.cmd, data.material, null, 0);
                        // Dedicated sprite geometry draws after the opaque sky veil, in
                        // this same raster pass. Its custom LightMode excludes normal URP
                        // transparent drawing; exact scene depth is sampled in its shader.
                        if(data.drawClouds)context.cmd.DrawRendererList(data.clouds);
                        // Draw hostile meshes after the world recolour. Ignore wall depth
                        // and ordinary occlusion culling; never include the local player.
                        if (data.targets != null)
                            foreach (EarthSeismicCameraTargets.Target target in data.targets)
                            {
                                Renderer renderer = target.Renderer;
                                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                                for (int submesh = 0; submesh < target.SubmeshCount; submesh++)
                                    context.cmd.DrawRenderer(renderer, data.material, submesh, 1);
                            }
                    });
                }
                resources.cameraColor = destination;
            }

            private sealed class AtmospherePassData
            {
                public RendererListHandle clouds;
                public bool drawClouds;
                public TextureHandle source;
                public TextureHandle depth;
                public Material material;
                public EarthSeismicCameraTargets.Target[] targets;
                public Vector4 blitTexelSize;
            }
        }
    }
}
