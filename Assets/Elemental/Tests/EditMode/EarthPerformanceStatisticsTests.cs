using Elemental.Simulation.Diagnostics;
using Elemental.Presentation.Rendering;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthPerformanceStatisticsTests
    {
        [Test]
        public void RingPercentilesUseChronologicalBoundedSamples()
        {
            double[] ring = { 9, 10, 3, 4, 5, 6, 7, 8 };
            double[] scratch = new double[8];
            EarthPercentiles result = EarthPerformanceStatistics.Compute(ring, 8, 2, scratch);
            Assert.That(result.P50, Is.EqualTo(6.5).Within(0.0001));
            Assert.That(result.P95, Is.EqualTo(9.65).Within(0.0001));
            Assert.That(result.P99, Is.EqualTo(9.93).Within(0.0001));
            Assert.That(result.Maximum, Is.EqualTo(10));
        }

        [Test]
        public void EmptyCaptureReturnsZeroes()
        {
            EarthPercentiles result = EarthPerformanceStatistics.Compute(
                new double[4], 0, 0, new double[4]);
            Assert.That(result.P99, Is.Zero);
            Assert.That(result.Maximum, Is.Zero);
        }

        [Test]
        public void StandaloneEvidenceRequiresEveryPerformanceGate()
        {
            VisualQaCaptureBehaviour.Mvp01ProfilerEvidence evidence = PassingEvidence();
            evidence.EvaluateGates();

            Assert.That(evidence.resolutionGatePassed, Is.True);
            Assert.That(evidence.sampleCoverageGatePassed, Is.True);
            Assert.That(evidence.cpuGpuBudgetGatePassed, Is.True);
            Assert.That(evidence.zeroGcGatePassed, Is.True);
            Assert.That(evidence.footContactGatePassed, Is.True);
            Assert.That(evidence.runtimeRenderAuditPassed, Is.True);
            Assert.That(evidence.authoritativePassed, Is.True);
            Assert.That(evidence.passed, Is.True);
        }

        [Test]
        public void EditorDiagnosticIsLabelledAndCanNeverBeAuthoritative()
        {
            VisualQaCaptureBehaviour.Mvp01ProfilerEvidence evidence = PassingEvidence();
            evidence.isEditor = true;
            evidence.mode = "editor-diagnostic-camera-rt";
            evidence.steadyStateGcFramesOverZero = 720;
            evidence.steadyStateMaximumGcBytesInFrame = 4096;
            evidence.EvaluateGates();

            Assert.That(evidence.zeroGcGatePassed, Is.False);
            Assert.That(evidence.editorDiagnosticPassed, Is.True);
            Assert.That(evidence.authoritativePassed, Is.False);
            Assert.That(evidence.passed, Is.False);
        }

        [Test]
        public void MissingFootMarkerCoverageFailsEvenWhenReportedDurationIsZero()
        {
            VisualQaCaptureBehaviour.Mvp01ProfilerEvidence evidence = PassingEvidence();
            evidence.footContactFrameSamples = 0;
            evidence.footContactMissingFrames = 720;
            evidence.footContactMinimumInvocations = 0;
            evidence.footContactP95Milliseconds = 0.0;
            evidence.EvaluateGates();

            Assert.That(evidence.footContactGatePassed, Is.False);
            Assert.That(evidence.authoritativePassed, Is.False);
        }

        [Test]
        public void RuntimeVolumeMismatchCannotPassAsGameRenderEvidence()
        {
            VisualQaCaptureBehaviour.Mvp01ProfilerEvidence evidence = PassingEvidence();
            evidence.runtimeRenderAudit.resolvedPostExposure = 0.24f;
            evidence.runtimeRenderAudit.resolvedContrast = 12f;
            evidence.runtimeRenderAudit.resolvedSaturation = 3f;
            evidence.runtimeRenderAudit.resolvedTemperature = 6f;
            evidence.runtimeRenderAudit.resolvedTint = 0f;
            evidence.runtimeRenderAudit.Evaluate();
            evidence.EvaluateGates();

            Assert.That(evidence.runtimeRenderAudit.authoredLookContractPassed, Is.False);
            Assert.That(evidence.runtimeRenderAuditPassed, Is.False);
            Assert.That(evidence.authoritativePassed, Is.False);
            StringAssert.Contains("runtime-volume", evidence.runtimeRenderAudit.mismatchSummary);
        }

        [Test]
        public void D3D11MissingGpuTimingIsExplicitlyWaivedWithoutFabricatingSamples()
        {
            VisualQaCaptureBehaviour.Mvp01ProfilerEvidence evidence = PassingEvidence();
            evidence.gpuTimingAvailable = false;
            evidence.gpuFrameP95Milliseconds = 0.0;
            evidence.gpuFrameMaximumMilliseconds = 0.0;
            evidence.runtimeRenderAudit.graphicsDeviceType = "Direct3D11";
            evidence.EvaluateGates();

            Assert.That(evidence.cpuBudgetGatePassed, Is.True);
            Assert.That(evidence.gpuBudgetGatePassed, Is.False);
            Assert.That(evidence.gpuTimingWaived, Is.True);
            Assert.That(evidence.cpuGpuBudgetGatePassed, Is.True);
            Assert.That(evidence.authoritativePassed, Is.True);
        }

        private static VisualQaCaptureBehaviour.Mvp01ProfilerEvidence PassingEvidence() =>
            new VisualQaCaptureBehaviour.Mvp01ProfilerEvidence
            {
                schema = "mvp-performance-evidence-v2",
                mode = "standalone-game-backbuffer",
                isEditor = false,
                isBatchMode = false,
                requestedFrameSamples = 720,
                totalFrameSamples = 720,
                frameTimingSamples = 720,
                renderWidth = 1920,
                renderHeight = 1080,
                cpuFrameP95Milliseconds = 12.0,
                gpuFrameP95Milliseconds = 8.0,
                gpuFrameMaximumMilliseconds = 9.0,
                gpuTimingAvailable = true,
                gcSampleFrames = 720,
                steadyStateGcFramesOverZero = 0,
                steadyStateMaximumGcBytesInFrame = 0,
                activeFootControllerCount = 2,
                footContactFrameSamples = 720,
                footContactMissingFrames = 0,
                footContactTotalInvocations = 1440,
                footContactMinimumInvocations = 2,
                footContactP95Milliseconds = 0.25,
                runtimeRenderAudit = PassingRenderAudit()
            };

        private static VisualQaCaptureBehaviour.Mvp01RuntimeRenderAudit PassingRenderAudit()
        {
            var audit = new VisualQaCaptureBehaviour.Mvp01RuntimeRenderAudit
            {
                universalPipelineAsset = "ElEmentalURP",
                activePipelineAsset = "ElEmentalURP",
                activeRendererIndex = 0,
                loadedRendererDataAssets = "ElEmentalRenderer",
                pipelineSupportsHdr = true,
                pipelineMsaaSamples = 1,
                pipelineRenderScale = 1f,
                pipelineDepthTexture = true,
                pipelineShadowDistance = 90f,
                pipelineShadowCascades = 4,
                pipelineMainLightShadowAtlas = 4096,
                qualityShadows = UnityEngine.ShadowQuality.All.ToString(),
                qualityShadowCascades = 4,
                qualityShadowDistance = 90f,
                cameraAllowHdr = true,
                cameraPostProcessing = true,
                cameraRequiresDepthTexture = true,
                cameraRendersShadows = true,
                cameraStopNaN = true,
                cameraDithering = true,
                cameraAntialiasing = "SubpixelMorphologicalAntiAliasing",
                cameraAntialiasingQuality = "High",
                ssaoFeatureFound = true,
                ssaoFeatureActive = true,
                resolvedColorAdjustments = true,
                resolvedPostExposure = 0f,
                resolvedContrast = 7f,
                resolvedSaturation = -8f,
                resolvedWhiteBalance = true,
                resolvedTemperature = 2f,
                resolvedTint = -1f
            };
            audit.Evaluate();
            return audit;
        }
    }
}
