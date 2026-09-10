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
        private AtmospherePass _backgroundPass;
        private Material _fireDofMaterial;
        public static bool FireDofActive {get;private set;}
        public static bool DisableFireDofForQa {get;set;}

        public void Configure(Material configuredMaterial)
        {
            material = configuredMaterial;
            Create();
        }

        public override void Create()
        {
            CoreUtils.Destroy(_fireDofMaterial);var fireShader=Resources.Load<Shader>("FireDepthBokeh");
            _fireDofMaterial=fireShader!=null?CoreUtils.CreateEngineMaterial(fireShader):null;
            _backgroundPass = new AtmospherePass(material,true,null)
            {
                // Depth-defined fog and clouds must be included in the background
                // bokeh. Applying them later repaints a sharp island silhouette.
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents-1)
            };
            _pass = new AtmospherePass(material,false,_fireDofMaterial)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        protected override void Dispose(bool disposing){CoreUtils.Destroy(_fireDofMaterial);FireDofActive=false;}

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material != null && renderingData.cameraData.cameraType == CameraType.Game)
            {
                renderer.EnqueuePass(_backgroundPass);
                renderer.EnqueuePass(_pass);
            }
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
            private readonly bool _background;
            private readonly Material _fireDof;
            private static readonly int FireDofParamsId=Shader.PropertyToID("_ElementalFireDofParams");
            private static readonly int HeatSourceId = Shader.PropertyToID("_ElementalHeatSource");
            private static readonly int HeatTexelSizeId = Shader.PropertyToID("_ElementalHeatSource_TexelSize");

            public AtmospherePass(Material material,bool background,Material fireDof)
            {
                _material = material;
                _background = background;_fireDof=fireDof;
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
                destinationDescriptor.name = _background?"Elemental Atmosphere Before Bokeh":"Elemental Fire After Bokeh";
                destinationDescriptor.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDescriptor);
                bool drawClouds=_background?(ValleyCloudParticles.HasActive || ProceduralCloudBanks.HasActive):(Elemental.Presentation.Fire.ArenaColumnFires.HasActive ||
                    Elemental.Presentation.Fire.FireCpuMeshBackend.HasVisibleGroups || Elemental.Presentation.Fire.FireFlowVolumeBackend.HasVisibleGroups || Elemental.Presentation.Fire.FireSurfaceFlameRenderer.HasVisibleGroups || Elemental.Presentation.Fire.FireProtectionSphereRenderer.HasVisibleGroups || Elemental.Presentation.Fire.FireRingRibbonRenderer.HasVisibleGroups);
                EarthCinematicDepthOfFieldSettings fireSettings=default;
                var camera=frameData.Get<UniversalCameraData>().camera;
                bool fireDof=!DisableFireDofForQa&&!_background&&drawClouds&&_fireDof!=null&&camera.TryGetComponent<EarthCinematicDepthOfFieldController>(out var focus)&&focus.TryGetRenderSettings(out fireSettings);
                if(!_background)FireDofActive=fireDof;
                Vector4 fireParams=fireDof?new Vector4(fireSettings.SharpNearDistance,fireSettings.SharpFarDistance,fireSettings.NearTransition,fireSettings.FarTransition):new Vector4(0,100000,1,1);
                TextureHandle fireMoments=default;
                if(fireDof){var momentDesc=destinationDescriptor;momentDesc.name="Coverage weighted transported fire depth";momentDesc.colorFormat=UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;momentDesc.clearBuffer=true;momentDesc.clearColor=Color.clear;fireMoments=renderGraph.CreateTexture(momentDesc);}
                RendererListHandle cloudList=default;
                if(drawClouds)
                {
                    var desc=new RendererListDesc(new ShaderTagId(_background?"ElementalAtmosphereCloud":"ElementalValleyCloud"),frameData.Get<UniversalRenderingData>().cullResults,frameData.Get<UniversalCameraData>().camera)
                    {renderQueueRange=RenderQueueRange.transparent,sortingCriteria=SortingCriteria.CommonTransparent};
                    cloudList=renderGraph.CreateRendererList(desc);
                }
                using (IRasterRenderGraphBuilder builder =
                       renderGraph.AddRasterRenderPass<AtmospherePassData>(
                           _background?"Elemental Atmosphere Before Bokeh":"Elemental Fire After Bokeh",
                           out AtmospherePassData passData))
                {
                    passData.fireParams=fireParams;
                    passData.clouds=cloudList;passData.drawClouds=drawClouds;
                    if(drawClouds)builder.UseRendererList(cloudList);
                    passData.source = source;
                    passData.depth = depth;
                    passData.material = _material;
                    passData.applyAtmosphere = _background;
                    passData.targets = !_background && targets != null && targets.isActiveAndEnabled && targets.Blend > 0f
                        ? targets.Targets : null;
                    passData.blitTexelSize = new Vector4(
                        1f / Mathf.Max(1, destinationDescriptor.width),
                        1f / Mathf.Max(1, destinationDescriptor.height),
                        destinationDescriptor.width,
                        destinationDescriptor.height);
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(depth, AccessFlags.Read);
                    if(resources.mainShadowsTexture.IsValid())builder.UseTexture(resources.mainShadowsTexture,AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    if(fireDof)builder.SetRenderAttachment(fireMoments,1,AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (
                        AtmospherePassData data,
                        RasterGraphContext context) =>
                    {
                        // Bind the exact RG handles consumed by this pass. The old
                        // material blit relied on an implicit global depth owner and
                        // read sky depth across opaque arena geometry.
                        context.cmd.SetGlobalVector(FireDofParamsId,data.fireParams);
                        context.cmd.SetGlobalTexture(BlitTextureId, data.source);
                        context.cmd.SetGlobalTexture(CameraDepthTextureId, data.depth);
                        context.cmd.SetGlobalVector(
                            BlitScaleBiasId,
                            new Vector4(1f, 1f, 0f, 0f));
                        context.cmd.SetGlobalVector(
                            BlitTextureTexelSizeId,
                            data.blitTexelSize);
                        if(data.applyAtmosphere)CoreUtils.DrawFullScreen(context.cmd, data.material, null, 0);
                        else Blitter.BlitTexture(context.cmd,data.source,new Vector4(1,1,0,0),0,false);
                        // Clouds, columns and transported flame/smoke draw once after the
                        // background-depth veil: applying sky fog to already-composited
                        // near fire erased it specifically over sky gaps.
                        // Dedicated geometry draws after the opaque sky veil, in
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
                if(fireDof)destination=FireDepthOfFieldComposite.Record(renderGraph,_fireDof,source,destination,fireMoments,depth,fireParams,fireSettings.MaxRadiusPixels);
                resources.cameraColor = destination;
                if (!_background && (Elemental.Presentation.Fire.FireCpuMeshBackend.HasVisibleGroups || Elemental.Presentation.Fire.FireFlowVolumeBackend.HasVisibleGroups || Elemental.Presentation.Fire.FireSurfaceFlameRenderer.HasVisibleGroups || Elemental.Presentation.Fire.FireProtectionSphereRenderer.HasVisibleGroups || Elemental.Presentation.Fire.FireRingRibbonRenderer.HasVisibleGroups))
                {
                    // Read the completed atmosphere/cloud/flame image into a
                    // separate target: never sample the current attachment.
                    var heatDescription = destinationDescriptor;
                    heatDescription.name = "Elemental Heat Haze Color";
                    var heatOutput = renderGraph.CreateTexture(heatDescription);
                    var heatListDescription = new RendererListDesc(new ShaderTagId("ElementalFireHeat"),
                        frameData.Get<UniversalRenderingData>().cullResults,
                        frameData.Get<UniversalCameraData>().camera)
                    { renderQueueRange = RenderQueueRange.transparent, sortingCriteria = SortingCriteria.CommonTransparent };
                    var heatList = renderGraph.CreateRendererList(heatListDescription);
                    using (var builder = renderGraph.AddRasterRenderPass<HeatPassData>(
                        "Elemental Fire Heat Haze", out var data))
                    {
                        data.source = destination; data.depth = depth; data.renderers = heatList;
                        data.texelSize = new Vector4(1f / destinationDescriptor.width,
                            1f / destinationDescriptor.height, destinationDescriptor.width, destinationDescriptor.height);
                        builder.UseTexture(destination, AccessFlags.Read);
                        builder.UseTexture(depth, AccessFlags.Read);
                        builder.UseRendererList(heatList);
                        builder.SetRenderAttachment(heatOutput, 0, AccessFlags.Write);
                        builder.AllowGlobalStateModification(true);
                        builder.SetRenderFunc(static (HeatPassData pass, RasterGraphContext context) =>
                        {
                            Blitter.BlitTexture(context.cmd, pass.source, new Vector4(1, 1, 0, 0), 0, false);
                            context.cmd.SetGlobalTexture(HeatSourceId, pass.source);
                            context.cmd.SetGlobalVector(HeatTexelSizeId, pass.texelSize);
                            context.cmd.SetGlobalTexture(CameraDepthTextureId, pass.depth);
                            context.cmd.DrawRendererList(pass.renderers);
                        });
                    }
                    resources.cameraColor = heatOutput;
                }
            }

            private sealed class HeatPassData
            {
                public TextureHandle source, depth;
                public RendererListHandle renderers;
                public Vector4 texelSize;
            }

            private sealed class AtmospherePassData
            {
                public Vector4 fireParams;
                public RendererListHandle clouds;
                public bool drawClouds;
                public bool applyAtmosphere;
                public TextureHandle source;
                public TextureHandle depth;
                public Material material;
                public EarthSeismicCameraTargets.Target[] targets;
                public Vector4 blitTexelSize;
            }
        }
    }
}
