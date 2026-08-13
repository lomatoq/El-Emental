using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

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
                if (resources.isActiveTargetBackBuffer) return;
                TextureHandle source = resources.activeColorTexture;
                TextureDesc destinationDescriptor = renderGraph.GetTextureDesc(source);
                destinationDescriptor.name = "Elemental Atmosphere Color";
                destinationDescriptor.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDescriptor);
                RenderGraphUtils.BlitMaterialParameters parameters =
                    new(source, destination, _material, 0);
                renderGraph.AddBlitPass(parameters, "Elemental Atmosphere Fullscreen");
                resources.cameraColor = destination;
            }
        }
    }
}
