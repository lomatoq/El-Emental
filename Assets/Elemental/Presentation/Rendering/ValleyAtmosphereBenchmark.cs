#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Globalization;
using System.Text;
using Elemental.Simulation.Time;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Elemental.Presentation.Rendering
{
    // Development-only standalone opt-in. No scene component or production Update added.
    [DefaultExecutionOrder(32000)]
    public sealed class ValleyAtmosphereBenchmark:MonoBehaviour
    {
        private const int WarmFrames=240,SampleFrames=600,BlocksPerView=6,Views=2;
        private static readonly int[] Order={0,1,2,2,1,0};
        private static string requestedPath;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartFromArguments()
        {
            if(Application.isEditor)return;
            bool requested=false;requestedPath=null;
            foreach(string arg in Environment.GetCommandLineArgs())
            {if(arg=="--valley-benchmark")requested=true;if(arg.StartsWith("--valley-report=",StringComparison.Ordinal))requestedPath=arg.Substring(16);}
            if(requested)new GameObject("Valley atmosphere development benchmark").AddComponent<ValleyAtmosphereBenchmark>();
        }
        [Serializable] private struct Row
        {
            public int block,view,mode,index,width,height;
            public double wallMs,cpuFrameMs,gpuFrameMs,publisherCpuMs,passCpuMs,cameraPositionError,cameraAngleError;
            public long gcBytes;
            public ulong timingStamp;
        }
        [Serializable] private sealed class Metric
        {public int valid,positive;public double p50=-1,p95=-1,max=-1;}
        [Serializable] private sealed class Block
        {
            public int block,view,mode,samples,warmFrames;
            public Metric wall,cpuFrame,gpuWholeFrame,publisherCpu,passCpu,gc;
            public double maxCameraPositionError,maxCameraAngleError;
        }
        [Serializable] private sealed class Report
        {
            public string utc,status,reason,unity,hardware,api,colorSpace,scope,csv;
            public int width,height,warmFrames,sampleFrames,blocks,rows,targetFrameRate,vSync;
            public bool publisherRecorderValid,passRecorderValid,gcRecorderValid;
            public Block[] results;
        }
        private readonly Row[] rows=new Row[SampleFrames*BlocksPerView*Views];
        private readonly FrameTiming[] timing=new FrameTiming[1];
        private ValleyAtmosphereController owner;
        private CelestialSystemBehaviour sky;
        private UnityEngine.Camera camera;
        private ProfilerRecorder publisher,pass,allocations;
        private Vector3 originalPosition,expectedPosition;
        private Quaternion originalRotation,expectedRotation;
        private float originalScale,originalPhase,startTime;
        private int originalVSync,originalTarget,block,frame,index,count;
        private bool originalFog,originalClouds,originalMotion,prepared,finished,restored;
        private int originalDebug;
        private CelestialLightingAuthorityMode originalAuthority;
        private ulong lastTimingStamp;
        private void Awake(){startTime=Time.realtimeSinceStartup;}
        private void LateUpdate()
        {
            if(finished)return;
            if(!prepared)
            {
                if(Time.realtimeSinceStartup-startTime<5)return;
                if(!Bind())
                {if(Time.realtimeSinceStartup-startTime>60)Finish("Failed","No ready authored valley/sky/camera after60seconds.");return;}
                if(Screen.width!=1920 || Screen.height!=1080){Finish("Failed","Require explicit1920x1080 launch flags; actualresolution differs.");return;}
                Prepare();return;
            }
            // Advance only after the prior frame rendered, so final pose evidence is retained.
            if(index==SampleFrames)
            {
                block++;
                if(block>=BlocksPerView*Views){Finish("Completed","All 12 blocks collected; review sample availability and invariants.");return;}
                BeginBlock();
            }
            // Scaled simulation is paused for stable A/B/C scene state. This is a
            // fixed-scene rendering benchmark, not a full gameplay frame-budget claim.
            camera.transform.SetPositionAndRotation(expectedPosition,expectedRotation);
            FrameTimingManager.CaptureFrameTimings();
            frame++;
            if(frame<=WarmFrames)return;
            if(index>=SampleFrames)return;
            uint available=FrameTimingManager.GetLatestTimings(1,timing);
            bool fresh=available>0 && timing[0].frameStartTimestamp!=0 && timing[0].frameStartTimestamp!=lastTimingStamp;
            if(fresh)lastTimingStamp=timing[0].frameStartTimestamp;
            var row=new Row
            {
                block=block,view=block/BlocksPerView,mode=Order[block%BlocksPerView],index=index,width=Screen.width,height=Screen.height,
                wallMs=Time.unscaledDeltaTime*1000,
                cpuFrameMs=fresh && timing[0].cpuFrameTime>0?timing[0].cpuFrameTime:-1,
                gpuFrameMs=fresh && timing[0].gpuFrameTime>0?timing[0].gpuFrameTime:-1,
                timingStamp=fresh?timing[0].frameStartTimestamp:0,
                publisherCpuMs=publisher.Valid && publisher.Count>0?publisher.LastValue/1000000.0:-1,
                passCpuMs=pass.Valid && pass.Count>0?pass.LastValue/1000000.0:-1,
                gcBytes=allocations.Valid && allocations.Count>0?allocations.LastValue:-1,
                cameraPositionError=Vector3.Distance(camera.transform.position,expectedPosition),
                cameraAngleError=Quaternion.Angle(camera.transform.rotation,expectedRotation)
            };
            rows[count++]=row;index++;
        }
        private bool Bind()
        {
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {owner=root.GetComponentInChildren<ValleyAtmosphereController>(true)??owner;sky=root.GetComponentInChildren<CelestialSystemBehaviour>(true)??sky;}
            camera=sky!=null?sky.TargetCamera:null;
            return owner!=null && owner.isActiveAndEnabled && camera!=null && sky.HasRequiredBindings;
        }
        private void Prepare()
        {
            originalPosition=camera.transform.position;originalRotation=camera.transform.rotation;
            originalScale=Time.timeScale;originalVSync=QualitySettings.vSyncCount;originalTarget=Application.targetFrameRate;
            originalFog=owner.FogEnabled;originalClouds=owner.CloudsEnabled;originalMotion=owner.AnimateClouds;originalDebug=owner.DebugMode;
            originalPhase=sky.Snapshot.TimeOfDay01;originalAuthority=sky.LightingAuthority;
            Time.timeScale=0;QualitySettings.vSyncCount=0;Application.targetFrameRate=120;
            owner.AnimateClouds=false;owner.DebugMode=0;
            sky.SetLightingAuthorityForQa(CelestialLightingAuthorityMode.AnimatedEphemeris);
            publisher=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Elemental.ValleyAtmosphere.Publish",1);
            pass=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Elemental Atmosphere Fullscreen",1);
            allocations=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
            sky.SetTimeOfDayForQa(originalPhase);sky.EvaluatePresentationForQa();
            prepared=true;RenderPipelineManager.endCameraRendering+=EndCamera;BeginBlock();
        }
        private void BeginBlock()
        {
            int mode=Order[block%BlocksPerView];owner.FogEnabled=mode!=0;owner.CloudsEnabled=mode==2;
            expectedPosition=originalPosition;expectedRotation=originalRotation;
            if(block/BlocksPerView==1)
            {
                var centre=owner.transform.position;var up=owner.transform.up;var forward=owner.transform.forward;
                expectedPosition=centre+up*95+forward*125;expectedRotation=Quaternion.LookRotation(centre-up*160-expectedPosition,up);
            }
            frame=0;index=0;
        }
        private void EndCamera(ScriptableRenderContext context,UnityEngine.Camera rendered)
        {
            if(rendered!=camera || !prepared || finished || frame<=WarmFrames || count==0)return;
            // Check final rendered pose too, not only the pose written in LateUpdate.
            int i=count-1;var row=rows[i];row.cameraPositionError=Math.Max(row.cameraPositionError,Vector3.Distance(rendered.transform.position,expectedPosition));
            row.cameraAngleError=Math.Max(row.cameraAngleError,Quaternion.Angle(rendered.transform.rotation,expectedRotation));rows[i]=row;
        }
        private void Finish(string status,string reason)
        {
            if(finished)return;finished=true;
            try{Write(status,reason);}finally{Restore();Application.Quit(status=="Completed"?0:2);}
        }
        private void Write(string status,string reason)
        {
            string path=string.IsNullOrEmpty(requestedPath)?Path.Combine(Application.persistentDataPath,"ValleyAtmosphere1080.json"):requestedPath;
            path=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(path));string csv=Path.ChangeExtension(path,"csv");
            var text=new StringBuilder("block,view,mode,index,width,height,wallMs,cpuFrameMs,gpuWholeFrameMs,publisherCpuMs,passCpuMs,gcBytes,timingStamp,cameraPositionError,cameraAngleError\n");
            for(int i=0;i<count;i++)
            {
                var r=rows[i];text.Append(r.block).Append(',').Append(r.view).Append(',').Append(r.mode).Append(',').Append(r.index).Append(',').Append(r.width).Append(',').Append(r.height).Append(',');
                text.Append(r.wallMs.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(r.cpuFrameMs.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(r.gpuFrameMs.ToString("R",CultureInfo.InvariantCulture)).Append(',');
                text.Append(r.publisherCpuMs.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(r.passCpuMs.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(r.gcBytes).Append(',').Append(r.timingStamp).Append(',');
                text.Append(r.cameraPositionError.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(r.cameraAngleError.ToString("R",CultureInfo.InvariantCulture)).Append('\n');
            }
            File.WriteAllText(csv,text.ToString());var blocks=new Block[(count+SampleFrames-1)/SampleFrames];
            for(int b=0;b<blocks.Length;b++)
            {
                int start=b*SampleFrames,n=Math.Min(SampleFrames,count-start);var item=new Block{block=b,view=b/BlocksPerView,mode=Order[b%BlocksPerView],samples=n,warmFrames=WarmFrames};
                item.wall=Measure(start,n,0);item.cpuFrame=Measure(start,n,1);item.gpuWholeFrame=Measure(start,n,2);item.publisherCpu=Measure(start,n,3);item.passCpu=Measure(start,n,4);item.gc=Measure(start,n,5);
                for(int i=start;i<start+n;i++){item.maxCameraPositionError=Math.Max(item.maxCameraPositionError,rows[i].cameraPositionError);item.maxCameraAngleError=Math.Max(item.maxCameraAngleError,rows[i].cameraAngleError);}
                blocks[b]=item;
            }
            var report=new Report{utc=DateTime.UtcNow.ToString("O"),status=status,reason=reason,unity=Application.unityVersion,hardware=SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName,
                api=SystemInfo.graphicsDeviceType.ToString(),colorSpace=QualitySettings.activeColorSpace.ToString(),width=Screen.width,height=Screen.height,warmFrames=WarmFrames,sampleFrames=SampleFrames,
                blocks=blocks.Length,rows=count,targetFrameRate=Application.targetFrameRate,vSync=QualitySettings.vSyncCount,publisherRecorderValid=publisher.Valid,passRecorderValid=pass.Valid,gcRecorderValid=allocations.Valid,results=blocks,csv=csv,
                scope="Development standalone1920x1080, paused scaled simulation, fixed camera/time, A(original)-B(veil)-C(clouds)-C-B-A in gameplay and lookdown. FrameTiming GPU is WHOLE FRAME, never isolated atmosphere GPU. Missing=-1, zero validGPU samples=unavailable. Pass recorder is CPU Render-category timing only, not GPU. Publisher timing is absolute1call/frame. No GPU budget inferred. CSV timestamps reject duplicate FrameTiming entries. Wall time is frame interval, not CPU execution. Screenshots/write allocations excluded from sample windows."};
            File.WriteAllText(path,JsonUtility.ToJson(report,true));
        }
        private Metric Measure(int start,int count,int field)
        {
            var values=new double[count];int n=0,positive=0;
            for(int i=start;i<start+count;i++)
            {var r=rows[i];double value=field==0?r.wallMs:field==1?r.cpuFrameMs:field==2?r.gpuFrameMs:field==3?r.publisherCpuMs:field==4?r.passCpuMs:r.gcBytes;if(value<0 || double.IsNaN(value)||double.IsInfinity(value))continue;values[n++]=value;if(value>0)positive++;}
            var m=new Metric{valid=n,positive=positive};if(n==0)return m;Array.Sort(values,0,n);m.p50=values[(n-1)/2];m.p95=values[Math.Min(n-1,(int)(n*0.95))];m.max=values[n-1];return m;
        }
        private void Restore()
        {
            if(restored)return;restored=true;RenderPipelineManager.endCameraRendering-=EndCamera;
            if(prepared)
            {
                Time.timeScale=originalScale;QualitySettings.vSyncCount=originalVSync;Application.targetFrameRate=originalTarget;
                if(camera!=null)camera.transform.SetPositionAndRotation(originalPosition,originalRotation);
                if(owner!=null){owner.FogEnabled=originalFog;owner.CloudsEnabled=originalClouds;owner.AnimateClouds=originalMotion;owner.DebugMode=originalDebug;owner.Publish();}
                if(sky!=null){sky.SetTimeOfDayForQa(originalPhase);sky.SetLightingAuthorityForQa(originalAuthority);sky.EvaluatePresentationForQa();}
            }
            publisher.Dispose();pass.Dispose();allocations.Dispose();
        }
        private void OnDestroy()=>Restore();
    }
}
#endif
