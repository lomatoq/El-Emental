using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Elemental.Presentation.Rendering
{
    public sealed class DuelShadowRenderPass : ScriptableRenderPass
    {
        public static readonly int ShadowMapId = Shader.PropertyToID("_ElementalDuelShadowMap");
        public static readonly int WorldToShadowId =
            Shader.PropertyToID("_ElementalDuelWorldToShadow");
        public static readonly int ShadowParamsId =
            Shader.PropertyToID("_ElementalDuelShadowParams");

        private sealed class PassData
        {
            public DuelShadowDrawCommand[] Commands;
            public int CommandCount;
            public Material CasterMaterial;
            public DuelShadowFrame Frame;
            public DuelShadowRuntimeSettings Settings;
        }

        private readonly DuelShadowDrawCommand[] _commands =
            new DuelShadowDrawCommand[DuelShadowCasterRegistry.MaximumCapacity];
        private readonly ProfilingSampler _profilingSampler =
            new ProfilingSampler("Elemental Duel Shadow Map");
        private DuelShadowBoundsState _boundsState;
        private DuelShadowFrame _frame;
        private DuelShadowRuntimeSettings _settings;
        private Material _casterMaterial;
        private int _commandCount;

        public DuelShadowRenderPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingShadows;
        }

        public bool Setup(
            Material casterMaterial,
            in DuelShadowRuntimeSettings settings,
            Bounds baseCoverage,
            Vector3 lightDirection,
            Vector3 referenceUp)
        {
            _casterMaterial = casterMaterial;
            _settings = settings;
            DuelShadowCasterRegistry registry = DuelShadowCasterRegistry.Shared;
            _commandCount = registry.CopyActiveDrawCommands(
                _commands,
                settings.Classification,
                settings.MaximumCasterCount,
                out Bounds casterBounds,
                out int rejectedCount);

            Bounds completeCoverage = baseCoverage;
            if (_commandCount > 0)
                completeCoverage.Encapsulate(casterBounds);
            bool frameValid = _commandCount > 0 && DuelShadowMath.TryBuildFrame(
                completeCoverage,
                lightDirection,
                referenceUp,
                settings.Stabilization,
                settings.Quality.Resolution,
                ref _boundsState,
                out _frame);

            DuelShadowDiagnostics.Publish(new DuelShadowDiagnosticsSnapshot(
                true,
                frameValid,
                false,
                Time.frameCount,
                settings.Quality.Resolution,
                settings.Quality.PcfKernelWidth,
                registry.Count,
                _commandCount,
                rejectedCount,
                registry.CapacityRejectCount,
                registry.GenerationRejectCount,
                frameValid ? _frame.TexelWorldSize : 0f,
                completeCoverage,
                frameValid ? _frame.WorldToShadowMatrix : Matrix4x4.identity));
            return frameValid && _casterMaterial != null;
        }

        public void ResetStabilization()
        {
            _boundsState.Reset();
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (_casterMaterial == null || _commandCount <= 0)
                return;

            int resolution = _settings.Quality.Resolution;
            TextureDesc descriptor = new TextureDesc(resolution, resolution)
            {
                name = "Elemental Duel Shadow Map",
                depthBufferBits = DepthBits.Depth16,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                isShadowMap = true,
                clearBuffer = true,
                clearColor = Color.white
            };
            TextureHandle shadowMap = renderGraph.CreateTexture(descriptor);

            using IRasterRenderGraphBuilder builder =
                renderGraph.AddRasterRenderPass<PassData>(
                    "Elemental Duel Shadow Map",
                    out PassData passData,
                    _profilingSampler);
            passData.Commands = _commands;
            passData.CommandCount = _commandCount;
            passData.CasterMaterial = _casterMaterial;
            passData.Frame = _frame;
            passData.Settings = _settings;
            builder.SetRenderAttachmentDepth(shadowMap, AccessFlags.Write);
            builder.SetGlobalTextureAfterPass(shadowMap, ShadowMapId);
            builder.AllowGlobalStateModification(true);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
            {
                context.cmd.SetViewport(new Rect(
                    0f,
                    0f,
                    data.Settings.Quality.Resolution,
                    data.Settings.Quality.Resolution));
                context.cmd.SetViewProjectionMatrices(
                    data.Frame.ViewMatrix,
                    data.Frame.ProjectionMatrix);
                context.cmd.SetGlobalDepthBias(
                    data.Settings.ConstantDepthBias,
                    data.Settings.SlopeDepthBias);
                for (int commandIndex = 0;
                     commandIndex < data.CommandCount;
                     commandIndex++)
                {
                    DuelShadowDrawCommand command = data.Commands[commandIndex];
                    if (command.Renderer == null ||
                        !command.Renderer.enabled ||
                        !command.Renderer.gameObject.activeInHierarchy)
                        continue;
                    for (int submeshIndex = 0;
                         submeshIndex < command.SubmeshCount;
                         submeshIndex++)
                    {
                        context.cmd.DrawRenderer(
                            command.Renderer,
                            data.CasterMaterial,
                            submeshIndex,
                            0);
                    }
                }

                context.cmd.SetGlobalDepthBias(0f, 0f);
                context.cmd.SetGlobalMatrix(
                    WorldToShadowId,
                    data.Frame.WorldToShadowMatrix);
                context.cmd.SetGlobalVector(
                    ShadowParamsId,
                    new Vector4(
                        1f,
                        data.Settings.ShadowStrength,
                        1f / data.Settings.Quality.Resolution,
                        (data.Settings.Quality.PcfKernelWidth - 1) * 0.5f));
                DuelShadowDiagnostics.MarkMapRendered();
            });
        }
    }
}
