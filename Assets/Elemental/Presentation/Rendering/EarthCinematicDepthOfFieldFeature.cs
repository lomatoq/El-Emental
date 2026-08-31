using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Elemental.Presentation.Rendering
{
    /// <summary>
    /// Native-high cinematic DOF for URP 17 RenderGraph. It consumes the real
    /// camera depth, creates a signed CoC at half resolution, gathers near/far
    /// independently, then composites foreground last to stop background bleed.
    /// </summary>
    public sealed class EarthCinematicDepthOfFieldFeature : ScriptableRendererFeature
    {
        [SerializeField] private Material material;
        private EarthCinematicDepthOfFieldPass _pass;

        public void Configure(Material configuredMaterial)
        {
            material = configuredMaterial;
            Create();
        }

        public override void Create()
        {
            _pass = new EarthCinematicDepthOfFieldPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (material == null ||
                renderingData.cameraData.cameraType != CameraType.Game ||
                renderingData.cameraData.renderType != CameraRenderType.Base)
                return;

            UnityEngine.Camera camera = renderingData.cameraData.camera;
            if (camera == null ||
                !camera.TryGetComponent(out EarthCinematicDepthOfFieldController controller) ||
                !controller.TryGetRenderSettings(out EarthCinematicDepthOfFieldSettings settings))
                return;

            _pass.Setup(material, settings);
            renderer.EnqueuePass(_pass);
        }

        private sealed class EarthCinematicDepthOfFieldPass : ScriptableRenderPass
        {
            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly int BlitTextureTexelSizeId =
                Shader.PropertyToID("_BlitTexture_TexelSize");
            private static readonly int CameraDepthTextureId =
                Shader.PropertyToID("_CameraDepthTexture");
            private static readonly int PackedTextureId =
                Shader.PropertyToID("_EarthDofPackedTexture");
            private static readonly int NearTextureId =
                Shader.PropertyToID("_EarthDofNearTexture");
            private static readonly int FarTextureId =
                Shader.PropertyToID("_EarthDofFarTexture");
            private static readonly int DofParamsId =
                Shader.PropertyToID("_EarthDofParams");
            private static readonly int GatherParamsId =
                Shader.PropertyToID("_EarthDofGatherParams");
            private static readonly int DebugModeId =
                Shader.PropertyToID("_EarthDofDebugMode");

            private Material _material;
            private EarthCinematicDepthOfFieldSettings _settings;

            public EarthCinematicDepthOfFieldPass()
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public void Setup(
                Material configuredMaterial,
                in EarthCinematicDepthOfFieldSettings configuredSettings)
            {
                _material = configuredMaterial;
                _settings = configuredSettings;
            }

            public override void RecordRenderGraph(
                RenderGraph renderGraph,
                ContextContainer frameData)
            {
                if (_material == null) return;
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;
                // At this injection point activeDepthTexture is the populated
                // opaque depth attachment. cameraDepthTexture can still be the
                // uninitialized copy on manual camera.Render/A-B capture paths.
                TextureHandle depth = resources.activeDepthTexture;
                if (!source.IsValid() || !depth.IsValid()) return;

                TextureDesc fullDescriptor = renderGraph.GetTextureDesc(source);
                TextureDesc halfDescriptor = fullDescriptor;
                halfDescriptor.width = Mathf.Max(1, fullDescriptor.width / 2);
                halfDescriptor.height = Mathf.Max(1, fullDescriptor.height / 2);
                halfDescriptor.depthBufferBits = DepthBits.None;
                halfDescriptor.msaaSamples = MSAASamples.None;
                halfDescriptor.format = GraphicsFormat.R16G16B16A16_SFloat;
                halfDescriptor.filterMode = FilterMode.Bilinear;
                halfDescriptor.wrapMode = TextureWrapMode.Clamp;
                halfDescriptor.clearBuffer = false;

                halfDescriptor.name = "Elemental DOF Signed CoC";
                TextureHandle packed = renderGraph.CreateTexture(halfDescriptor);
                halfDescriptor.name = "Elemental DOF Near Gather";
                TextureHandle near = renderGraph.CreateTexture(halfDescriptor);
                halfDescriptor.name = "Elemental DOF Far Gather";
                TextureHandle far = renderGraph.CreateTexture(halfDescriptor);

                TextureDesc compositeDescriptor = fullDescriptor;
                compositeDescriptor.name = "Elemental Cinematic DOF Color";
                compositeDescriptor.clearBuffer = false;
                compositeDescriptor.depthBufferBits = DepthBits.None;
                compositeDescriptor.msaaSamples = MSAASamples.None;
                TextureHandle composite = renderGraph.CreateTexture(compositeDescriptor);

                Vector4 dofParams = new Vector4(
                    _settings.SharpNearDistance,
                    _settings.SharpFarDistance,
                    _settings.NearTransition,
                    _settings.FarTransition);
                // Gather happens at half resolution, so a full-resolution pixel
                // radius is halved before conversion through the source texel size.
                Vector4 gatherParams = new Vector4(
                    _settings.MaxRadiusPixels * 0.5f,
                    0f,
                    0f,
                    0f);

                RecordFullscreenPass(
                    renderGraph,
                    "Elemental DOF Signed CoC",
                    source,
                    depth,
                    TextureHandle.nullHandle,
                    TextureHandle.nullHandle,
                    TextureHandle.nullHandle,
                    packed,
                    fullDescriptor.width,
                    fullDescriptor.height,
                    0,
                    dofParams,
                    gatherParams,
                    0);
                RecordFullscreenPass(
                    renderGraph,
                    "Elemental DOF Near Gather",
                    packed,
                    TextureHandle.nullHandle,
                    TextureHandle.nullHandle,
                    TextureHandle.nullHandle,
                    TextureHandle.nullHandle,
                    near,
                    halfDescriptor.width,
                    halfDescriptor.height,
                    1,
                    dofParams,
                    gatherParams,
                    0);
                RecordFullscreenPass(
                    renderGraph,
                    "Elemental DOF Far Gather",
                    packed,
                    TextureHandle.nullHandle,
                    TextureHandle.nullHandle,
                    TextureHandle.nullHandle,
                    TextureHandle.nullHandle,
                    far,
                    halfDescriptor.width,
                    halfDescriptor.height,
                    2,
                    dofParams,
                    gatherParams,
                    0);
                RecordFullscreenPass(
                    renderGraph,
                    "Elemental DOF Foreground-Safe Composite",
                    source,
                    depth,
                    packed,
                    near,
                    far,
                    composite,
                    fullDescriptor.width,
                    fullDescriptor.height,
                    3,
                    dofParams,
                    gatherParams,
                    (int)_settings.DebugView);

                resources.cameraColor = composite;
            }

            private void RecordFullscreenPass(
                RenderGraph renderGraph,
                string passName,
                TextureHandle source,
                TextureHandle depth,
                TextureHandle packed,
                TextureHandle near,
                TextureHandle far,
                TextureHandle destination,
                int sourceWidth,
                int sourceHeight,
                int shaderPass,
                Vector4 dofParams,
                Vector4 gatherParams,
                int debugMode)
            {
                using IRasterRenderGraphBuilder builder =
                    renderGraph.AddRasterRenderPass<FullscreenPassData>(
                        passName,
                        out FullscreenPassData passData);
                passData.source = source;
                passData.depth = depth;
                passData.packed = packed;
                passData.near = near;
                passData.far = far;
                passData.material = _material;
                passData.shaderPass = shaderPass;
                passData.blitTexelSize = new Vector4(
                    1f / Mathf.Max(1, sourceWidth),
                    1f / Mathf.Max(1, sourceHeight),
                    sourceWidth,
                    sourceHeight);
                passData.dofParams = dofParams;
                passData.gatherParams = gatherParams;
                passData.debugMode = debugMode;

                builder.UseTexture(source, AccessFlags.Read);
                if (depth.IsValid()) builder.UseTexture(depth, AccessFlags.Read);
                if (packed.IsValid()) builder.UseTexture(packed, AccessFlags.Read);
                if (near.IsValid()) builder.UseTexture(near, AccessFlags.Read);
                if (far.IsValid()) builder.UseTexture(far, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (
                    FullscreenPassData data,
                    RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(BlitTextureId, data.source);
                    context.cmd.SetGlobalVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                    context.cmd.SetGlobalVector(BlitTextureTexelSizeId, data.blitTexelSize);
                    if (data.depth.IsValid())
                        context.cmd.SetGlobalTexture(CameraDepthTextureId, data.depth);
                    if (data.packed.IsValid())
                        context.cmd.SetGlobalTexture(PackedTextureId, data.packed);
                    if (data.near.IsValid())
                        context.cmd.SetGlobalTexture(NearTextureId, data.near);
                    if (data.far.IsValid())
                        context.cmd.SetGlobalTexture(FarTextureId, data.far);
                    context.cmd.SetGlobalVector(DofParamsId, data.dofParams);
                    context.cmd.SetGlobalVector(GatherParamsId, data.gatherParams);
                    context.cmd.SetGlobalFloat(DebugModeId, data.debugMode);
                    CoreUtils.DrawFullScreen(
                        context.cmd,
                        data.material,
                        null,
                        data.shaderPass);
                });
            }

            private sealed class FullscreenPassData
            {
                public TextureHandle source;
                public TextureHandle depth;
                public TextureHandle packed;
                public TextureHandle near;
                public TextureHandle far;
                public Material material;
                public int shaderPass;
                public int debugMode;
                public Vector4 blitTexelSize;
                public Vector4 dofParams;
                public Vector4 gatherParams;
            }
        }
    }
}
