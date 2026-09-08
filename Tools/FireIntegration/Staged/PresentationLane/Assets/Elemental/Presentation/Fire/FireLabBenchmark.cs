using System;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Fire
{
    // Standalone-only opt-in harness. It preserves the same camera and Bloom for
    // baseline vs 8 High groups; captures final render timings when available.
    [DefaultExecutionOrder(10000)]
    public sealed class FireLabBenchmark : MonoBehaviour
    {
        private const int SampleCount=360;
        private readonly double[] baselineCpu=new double[SampleCount],baselineGpu=new double[SampleCount];
        private readonly double[] highCpu=new double[SampleCount],highGpu=new double[SampleCount],presentation=new double[SampleCount];
        private readonly long[] gc=new long[SampleCount];
        private readonly FrameTiming[] timing=new FrameTiming[1];
        private FireLabDriver lab;
        private ProfilerRecorder allocations;
        private int frame,stage,index,gpuBaselineSamples,gpuHighSamples;
        private int minParticles=int.MaxValue,maxParticles;
        public void Configure(FireLabDriver driver){lab=driver;}
        private void Start()
        {
            if(lab==null){enabled=false;return;}
            QualitySettings.vSyncCount=0;Application.targetFrameRate=120;
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            allocations=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
            lab.SetScenario(FireLabScenario.EightGroups);lab.StopEmission();
        }
        private void LateUpdate()
        {
            if(lab==null)return;
            frame++;
            FrameTimingManager.CaptureFrameTimings();
            uint available=FrameTimingManager.GetLatestTimings(1,timing);
            double cpu=available>0?timing[0].cpuFrameTime:Time.unscaledDeltaTime*1000;
            double gpu=available>0?timing[0].gpuFrameTime:0;
            if(stage==0){if(frame>=240){stage=1;frame=0;}return;}
            if(stage==1)
            {
                baselineCpu[index]=cpu;baselineGpu[index]=gpu;if(gpu>0)gpuBaselineSamples++;index++;
                if(index==SampleCount){index=0;stage=2;frame=0;lab.SetScenario(FireLabScenario.EightGroups);}return;
            }
            if(stage==2){if(frame>=240){stage=3;frame=0;}return;}
            if(stage==3)
            {
                highCpu[index]=cpu;highGpu[index]=gpu;if(gpu>0)gpuHighSamples++;
                presentation[index]=lab.CpuPresentationMilliseconds;gc[index]=allocations.Valid?allocations.LastValue:-1;
                minParticles=Math.Min(minParticles,lab.TotalAliveParticles);maxParticles=Math.Max(maxParticles,lab.TotalAliveParticles);index++;
                if(index==SampleCount){stage=4;WriteReport();Application.Quit();}
            }
        }
        private void WriteReport()
        {
            string path=Path.Combine(Application.persistentDataPath,"FireLabStandalone.json");
            foreach(var argument in Environment.GetCommandLineArgs())if(argument.StartsWith("--fire-report=",StringComparison.Ordinal))path=argument.Substring("--fire-report=".Length);
            Array.Sort(baselineCpu);Array.Sort(baselineGpu);Array.Sort(highCpu);Array.Sort(highGpu);Array.Sort(presentation);Array.Sort(gc);
            var report=new Report
            {
                utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,hardware=SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName,
                graphicsApi=SystemInfo.graphicsDeviceType.ToString(),colorSpace=QualitySettings.activeColorSpace.ToString(),
                width=Screen.width,height=Screen.height,groups=8,samples=SampleCount,minAlive=minParticles,maxAlive=maxParticles,
                presentationCpuP50=Percentile(presentation,0.5),presentationCpuP95=Percentile(presentation,0.95),
                baselineFrameMillisecondsP95=Percentile(baselineCpu,0.95),highFrameMillisecondsP95=Percentile(highCpu,0.95),
                baselineFrameGpuP95=Percentile(baselineGpu,0.95),highFrameGpuP95=Percentile(highGpu,0.95),
                validBaselineGpuSamples=gpuBaselineSamples,validHighGpuSamples=gpuHighSamples,
                wholeFrameGcP95=gc[(int)(SampleCount*0.95)],wholeFrameGcMax=gc[SampleCount-1],
                scope="CPU presentation stopwatch spans all eight mesh backends. Frame timings include world/render/Bloom and are not isolated Fire GPU timings. When FrameTiming is unavailable, frame milliseconds use unscaledDeltaTime, not measured CPU execution time. Zero GPU samples mean unsupported/unavailable, not zero cost. Development standalone, fixed camera, vSync off, 120 target."
            };
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));
        }
        private static double Percentile(double[] sorted,double p)=>sorted[Math.Min(sorted.Length-1,(int)(sorted.Length*p))];
        private void OnDestroy(){allocations.Dispose();}
        [Serializable] private sealed class Report
        {
            public string utc,unity,hardware,graphicsApi,colorSpace,scope;
            public int width,height,groups,samples,minAlive,maxAlive,validBaselineGpuSamples,validHighGpuSamples;
            public double presentationCpuP50,presentationCpuP95,baselineFrameMillisecondsP95,highFrameMillisecondsP95,baselineFrameGpuP95,highFrameGpuP95;
            public long wholeFrameGcP95,wholeFrameGcMax;
        }
    }
}

