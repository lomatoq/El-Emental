using System;
using System.IO;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Matter;
using Elemental.Runtime.World;
using Elemental.Simulation.Diagnostics;
using Elemental.Simulation.Matter;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Diagnostics
{
    [Serializable]
    public sealed class EarthPerformanceReport
    {
        public string unityVersion;
        public string utc;
        public string operatingSystem;
        public string processor;
        public string graphicsDevice;
        public string qualityLevel;
        public int samples;
        public double elapsedSeconds;
        public EarthPercentileReport cpu;
        public EarthPercentileReport gpu;
        public EarthPercentileReport mainThread;
        public EarthPercentileReport renderThread;
        public long maximumGcBytesInFrame;
        public double steadyStateWarmupSeconds;
        public int steadyStateSamples;
        public int steadyStateGcFramesOverZero;
        public long steadyStateMaximumGcBytesInFrame;
        public int rigidbodyCount;
        public int heroMatterCount;
        public int secondaryMatterCount;
        public int visualMatterCount;
        public int visualDebrisCount;
        public int voxelRuntimeChunkCount;
        public int voxelPendingRenderCount;
        public int voxelPendingColliderCount;
        public double voxelPeakRenderQueueMilliseconds;
        public double voxelPeakColliderQueueMilliseconds;
        public int botSpellCount;
        public int botLandedSpellCount;
        public int playerKnockoutCount;
        public int botKnockoutCount;
    }

    [Serializable]
    public struct EarthPercentileReport
    {
        public double p50;
        public double p95;
        public double p99;
        public double maximum;
    }

    /// <summary>
    /// Standalone-safe fixed-ring capture for Gate 8 evidence. Sampling and tier
    /// counters allocate no managed memory after enable; sorting/JSON happen only
    /// when an explicit snapshot is requested.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthPerformanceTelemetry : MonoBehaviour
    {
        private const int SampleCapacity = 36000;
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private readonly double[] _cpu = new double[SampleCapacity];
        private readonly double[] _gpu = new double[SampleCapacity];
        private readonly double[] _main = new double[SampleCapacity];
        private readonly double[] _render = new double[SampleCapacity];
        private readonly double[] _scratch = new double[SampleCapacity];

        [SerializeField] private EarthMatterKernelBehaviour matterKernel;
        [SerializeField] private EarthIndirectDebrisRenderer indirectDebris;
        [SerializeField] private bool writePlayerSnapshotOnQuit = true;
        [SerializeField, Min(0f)] private float steadyStateWarmupSeconds = 5f;

        private ProfilerRecorder _gcRecorder;
        private int _writeIndex;
        private int _sampleCount;
        private double _elapsed;
        private long _maximumGcBytesInFrame;
        private long _steadyStateMaximumGcBytesInFrame;
        private int _steadyStateSamples;
        private int _steadyStateGcFramesOverZero;

        public int SampleCount => _sampleCount;

        public void Configure(
            EarthMatterKernelBehaviour configuredKernel,
            EarthIndirectDebrisRenderer configuredDebris)
        {
            matterKernel = configuredKernel;
            indirectDebris = configuredDebris;
        }

        private void OnEnable()
        {
            _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        }

        private void OnDisable()
        {
            if (Application.isEditor && _sampleCount > 0)
            {
                WriteSnapshot(Path.Combine(
                    Application.dataPath,
                    "..",
                    "BuildReports",
                    "Mvp01PerformanceLatest.json"));
            }
            if (_gcRecorder.Valid) _gcRecorder.Dispose();
        }

        private void LateUpdate()
        {
            FrameTimingManager.CaptureFrameTimings();
            uint count = FrameTimingManager.GetLatestTimings(1, _timings);
            FrameTiming timing = count > 0 ? _timings[0] : default;
            double fallback = Time.unscaledDeltaTime * 1000.0;
            _cpu[_writeIndex] = timing.cpuFrameTime > 0.0 ? timing.cpuFrameTime : fallback;
            _gpu[_writeIndex] = timing.gpuFrameTime > 0.0 ? timing.gpuFrameTime : 0.0;
            _main[_writeIndex] = timing.cpuMainThreadFrameTime > 0.0 ? timing.cpuMainThreadFrameTime : fallback;
            _render[_writeIndex] = timing.cpuRenderThreadFrameTime > 0.0 ? timing.cpuRenderThreadFrameTime : 0.0;
            if (_gcRecorder.Valid)
            {
                long allocatedBytes = _gcRecorder.LastValue;
                _maximumGcBytesInFrame = Math.Max(_maximumGcBytesInFrame, allocatedBytes);
                if (_elapsed >= steadyStateWarmupSeconds)
                {
                    _steadyStateSamples++;
                    if (allocatedBytes > 0) _steadyStateGcFramesOverZero++;
                    _steadyStateMaximumGcBytesInFrame =
                        Math.Max(_steadyStateMaximumGcBytesInFrame, allocatedBytes);
                }
            }
            _writeIndex = (_writeIndex + 1) % SampleCapacity;
            _sampleCount = Math.Min(_sampleCount + 1, SampleCapacity);
            _elapsed += Time.unscaledDeltaTime;
        }

        public EarthPerformanceReport CaptureReport()
        {
            EarthPercentiles cpu = EarthPerformanceStatistics.Compute(
                _cpu, _sampleCount, _writeIndex, _scratch);
            EarthPercentiles gpu = EarthPerformanceStatistics.Compute(
                _gpu, _sampleCount, _writeIndex, _scratch);
            EarthPercentiles main = EarthPerformanceStatistics.Compute(
                _main, _sampleCount, _writeIndex, _scratch);
            EarthPercentiles render = EarthPerformanceStatistics.Compute(
                _render, _sampleCount, _writeIndex, _scratch);
            EarthMatterRegistry registry = matterKernel != null ? matterKernel.Registry : null;
            VoxelPlanetBehaviour voxelPlanet = FindAnyObjectByType<VoxelPlanetBehaviour>(FindObjectsInactive.Exclude);
            EarthMvpBotController bot = FindAnyObjectByType<EarthMvpBotController>(FindObjectsInactive.Exclude);
            EarthMvpDuelController duel = FindAnyObjectByType<EarthMvpDuelController>(FindObjectsInactive.Exclude);
            return new EarthPerformanceReport
            {
                unityVersion = Application.unityVersion,
                utc = DateTime.UtcNow.ToString("O"),
                operatingSystem = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                qualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()],
                samples = _sampleCount,
                elapsedSeconds = _elapsed,
                cpu = Convert(in cpu),
                gpu = Convert(in gpu),
                mainThread = Convert(in main),
                renderThread = Convert(in render),
                maximumGcBytesInFrame = _maximumGcBytesInFrame,
                steadyStateWarmupSeconds = steadyStateWarmupSeconds,
                steadyStateSamples = _steadyStateSamples,
                steadyStateGcFramesOverZero = _steadyStateGcFramesOverZero,
                steadyStateMaximumGcBytesInFrame = _steadyStateMaximumGcBytesInFrame,
                rigidbodyCount = FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude).Length,
                heroMatterCount = registry?.CountByRepresentation(EarthRepresentationTier.HeroPhysical) ?? 0,
                secondaryMatterCount = registry?.CountByRepresentation(EarthRepresentationTier.SecondaryPhysical) ?? 0,
                visualMatterCount = registry?.CountByRepresentation(EarthRepresentationTier.VisualOnlyGpu) ?? 0,
                visualDebrisCount = indirectDebris != null ? indirectDebris.ActiveVisualCount : 0,
                voxelRuntimeChunkCount = voxelPlanet != null ? voxelPlanet.RuntimeChunkCount : 0,
                voxelPendingRenderCount = voxelPlanet != null ? voxelPlanet.PendingRenderCount : 0,
                voxelPendingColliderCount = voxelPlanet != null ? voxelPlanet.PendingColliderCount : 0,
                voxelPeakRenderQueueMilliseconds = voxelPlanet != null ? voxelPlanet.PeakRenderQueueMilliseconds : 0.0,
                voxelPeakColliderQueueMilliseconds = voxelPlanet != null ? voxelPlanet.PeakColliderQueueMilliseconds : 0.0,
                botSpellCount = bot != null ? bot.StrikeCount : 0,
                botLandedSpellCount = bot != null ? bot.LandedStrikeCount : 0,
                playerKnockoutCount = duel != null ? duel.PlayerKnockoutCount : 0,
                botKnockoutCount = duel != null ? duel.BotKnockoutCount : 0
            };
        }

        public string WriteSnapshot(string path)
        {
            EarthPerformanceReport report = CaptureReport();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            return path;
        }

        private void OnApplicationQuit()
        {
            if (!writePlayerSnapshotOnQuit || _sampleCount == 0) return;
            WriteSnapshot(Path.Combine(Application.persistentDataPath, "EarthPerformanceLatest.json"));
        }

        private static EarthPercentileReport Convert(in EarthPercentiles value) => new EarthPercentileReport
        {
            p50 = value.P50,
            p95 = value.P95,
            p99 = value.P99,
            maximum = value.Maximum
        };
    }
}
