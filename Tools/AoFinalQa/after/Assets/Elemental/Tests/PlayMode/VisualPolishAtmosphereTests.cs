#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class VisualPolishAtmosphereTests
    {
        private const string Folder="BuildReports/VisualPolishFollowup";
        [UnityTest]
        public IEnumerator ProductionCloudsFadeOutOfSonarAndSunsetWarmsFog()
        {
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            var previous=SceneManager.GetActiveScene();Scene scene=default;
            try
            {
                yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);
                scene=SceneManager.GetSceneByPath(path);SceneManager.SetActiveScene(scene);
                var gate=Find<EarthSceneReadinessGate>(scene);
                double deadline=Time.realtimeSinceStartupAsDouble+130;
                while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(gate.IsReady,Is.True,gate.Status);
                yield return ProductionCombatTestFlow.BeginBotAfterReadiness(scene);
                var sky=Find<CelestialSystemBehaviour>(scene);var atmosphere=Find<ValleyAtmosphereController>(scene);
                var camera=sky.TargetCamera;Assert.That(camera,Is.Not.Null);
                var cameraData=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                bool post=cameraData.renderPostProcessing;
                cameraData.renderPostProcessing=false; // Exclude independent film-grain/dither from exact same-frame shader proof.
                var seismic=Find<EarthSeismicVision>(scene);if(seismic!=null)seismic.enabled=false;
                sky.SetTimeOfDayForQa(.25f);sky.EvaluatePresentationForQa();atmosphere.Publish();
                Shader.SetGlobalFloat("_EarthSeismicVision",0);
                var views=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Renderer>(true))
                    .Where(r=>r.enabled&&r.sharedMaterial!=null&&(r.sharedMaterial.shader.name=="Elemental/Procedural Cloud Banks"||r.sharedMaterial.shader.name=="Elemental/Valley Image Cloud Particles")).ToArray();
                Assert.That(views.Length,Is.GreaterThan(0));
                var noon=Capture(camera,"noon-clouds");
                var rendererData=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/Settings/ElEmentalRenderer.asset");
                var ao=rendererData.rendererFeatures.First(f=>f.name=="Elemental Contact SSAO");
                bool aoWasActive=ao.isActive;
                try { ao.SetActive(false);var noAo=Capture(camera,"noon-ao-off");ao.SetActive(true);var withAo=Capture(camera,"noon-ao-on");Assert.That(Difference(noAo,withAo),Is.GreaterThan(100),"Contact AO must affect the rendered production surfaces."); }
                finally { ao.SetActive(aoWasActive); }
                var stone=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/GraphicsV5/Materials/RumbleArenaSandstone.mat");
                float form=stone.GetFloat("_FormLightStrength");
                try { stone.SetFloat("_FormLightStrength",0); Capture(camera,"noon-form-off"); }
                finally { stone.SetFloat("_FormLightStrength",form); }
                Capture(camera,"noon-form-on");
                foreach(var v in views)v.enabled=false;
                var noClouds=Capture(camera,"noon-no-clouds");
                foreach(var v in views)v.enabled=true;
                Assert.That(Difference(noon,noClouds),Is.GreaterThan(100),"Production view must actually contain visible clouds.");
                Shader.SetGlobalFloat("_EarthSeismicVision",1);
                var sonarOn=Capture(camera,"sonar-cloud-renderers-on");
                foreach(var v in views)v.enabled=false;
                var sonarOff=Capture(camera,"sonar-cloud-renderers-off");
                foreach(var v in views)v.enabled=true;
                Assert.That(Difference(sonarOn,sonarOff),Is.EqualTo(0),"Cloud renderer contribution must be exactly zero at full sonar.");
                Shader.SetGlobalFloat("_EarthSeismicVision",0);
                float cloudClock=Shader.GetGlobalFloat("_ElementalCloudMotionTime");
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(Shader.GetGlobalFloat("_ElementalCloudMotionTime"),Is.GreaterThan(cloudClock));
                atmosphere.AnimateClouds=false;
                cloudClock=Shader.GetGlobalFloat("_ElementalCloudMotionTime");
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(Shader.GetGlobalFloat("_ElementalCloudMotionTime"),Is.EqualTo(cloudClock));
                atmosphere.AnimateClouds=true;
                var shaft=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/Materials/AtmosphereFullscreen.mat");
                float shaftStrength=shaft.GetFloat("_SunDustStrength");
                long dayShafts=0,nightShafts=0;
                try
                {
                    shaft.SetFloat("_SunDustStrength",0);var off=Capture(camera,"day-rays-off");
                    shaft.SetFloat("_SunDustStrength",shaftStrength);var on=Capture(camera,"day-rays-on");
                    dayShafts=Difference(off,on);Assert.That(dayShafts,Is.GreaterThan(100));
                    sky.SetTimeOfDayForQa(.75f);sky.EvaluatePresentationForQa();atmosphere.Publish();
                    shaft.SetFloat("_SunDustStrength",0);off=Capture(camera,"night-rays-off");
                    shaft.SetFloat("_SunDustStrength",shaftStrength);on=Capture(camera,"night-rays-on");
                    nightShafts=Difference(off,on);Assert.That(nightShafts,Is.EqualTo(0));
                }
                finally { shaft.SetFloat("_SunDustStrength",shaftStrength); }
                cameraData.renderPostProcessing=post;
                sky.SetTimeOfDayForQa(.5f);sky.EvaluatePresentationForQa();atmosphere.Publish();
                var fog=Shader.GetGlobalVector("_ElementalValleyTimeFogTop");
                Assert.That(fog.x,Is.GreaterThan(fog.z*1.5f),"Sunset fog must visibly warm.");
                var finalSunset=Capture(camera,"sunset");
                var rockMaterials=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&m.HasProperty("_TwilightFill")&&m.GetFloat("_TwilightFill")>0).Distinct().ToArray();
                var savedFills=rockMaterials.Select(m=>m.GetFloat("_TwilightFill")).ToArray();
                try { foreach(var m in rockMaterials)m.SetFloat("_TwilightFill",.24f);var baseline=Capture(camera,"sunset-previous-fill"); Assert.That(Difference(finalSunset,baseline),Is.GreaterThan(100),"Saved rock family fill must change actual sunset geometry."); }
                finally { for(int i=0;i<rockMaterials.Length;i++)rockMaterials[i].SetFloat("_TwilightFill",savedFills[i]); }
                cameraData.renderPostProcessing=false;Capture(camera,"sunset-no-post");cameraData.renderPostProcessing=post;
                File.WriteAllText(Folder+"/sunset-lighting.txt","solarAltitude="+Shader.GetGlobalFloat("_ElementalSolarAltitude")+"; night="+Shader.GetGlobalFloat("_ElementalNight01")+"; materialFill="+stone.GetFloat("_TwilightFill")+"\n"+string.Join("\n",scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&m.HasProperty("_TwilightFill")).Distinct().Select(m=>m.name+" fill="+m.GetFloat("_TwilightFill"))));
                sky.SetTimeOfDayForQa(.75f);sky.EvaluatePresentationForQa();atmosphere.Publish();
                Capture(camera,"night-dust");
                File.WriteAllText(Folder+"/atmosphere-evidence.txt","Normal cloud pixel difference="+Difference(noon,noClouds)+"\nFull sonar cloud difference="+Difference(sonarOn,sonarOff)+"\nSunset fog="+fog+"\nDay shaft difference="+dayShafts+"\nNight shaft difference="+nightShafts);
                // Run timing last so the bounded sampling window cannot disturb the earlier same-frame proofs.
                sky.SetTimeOfDayForQa(.25f);sky.EvaluatePresentationForQa();atmosphere.Publish();
                CaptureContactAo(scene,camera,ao);
                yield return MeasureAoGpu(camera,ao,post);
            }
            finally
            {
                Shader.SetGlobalFloat("_EarthSeismicVision",0);Time.timeScale=1;
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            }
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
        private static void CaptureContactAo(Scene scene,Camera camera,UnityEngine.Rendering.Universal.ScriptableRendererFeature ao)
        {
            var materials=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Renderer>(true))
                .SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&m.shader.name=="Elemental/Graphics V5/Rumble Rock Lit"&&m.HasProperty("_DebugMode")).Distinct().ToArray();
            Assert.That(materials.Length,Is.GreaterThan(0),"Contact AO debug capture requires actual production rock materials.");
            var modes=materials.Select(m=>m.GetFloat("_DebugMode")).ToArray();
            var data=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            bool post=data.renderPostProcessing,active=ao.isActive;
            try
            {
                ao.SetActive(true);data.renderPostProcessing=false;
                foreach(var material in materials)material.SetFloat("_DebugMode",6);
                Capture(camera,"noon-contact-ao-debug");
            }
            finally
            {
                for(int i=0;i<materials.Length;i++)if(materials[i]!=null)materials[i].SetFloat("_DebugMode",modes[i]);
                data.renderPostProcessing=post;ao.SetActive(active);
            }
        }
        private static IEnumerator MeasureAoGpu(Camera camera,UnityEngine.Rendering.Universal.ScriptableRendererFeature ao,bool productionPost)
        {
            const int warmup=15,attempts=60;
            string path=Folder+"/ao-gpu-timing.txt";
            Directory.CreateDirectory(Folder);
            var report=new System.Text.StringBuilder("FrameTimingManager whole-frame GPU comparison; actual production camera at 1280x720.\n");
            report.AppendLine("Editor="+Application.isEditor+"; device="+SystemInfo.graphicsDeviceName+"; API="+SystemInfo.graphicsDeviceType);
            report.AppendLine("Scope includes other rendering/editor overhead; this is not an isolated SSAO GPU marker. No CPU/wall-clock substitution.");
            report.AppendLine("Order full/half/full/half; 15 warmup + 60 attempts per run. Scaled simulation frozen; production postprocessing restored for measurement.");
            if(!FrameTimingManager.IsFeatureEnabled())
            {
                report.AppendLine("UNAVAILABLE: FrameTimingManager statistics are disabled on this runtime. Enable Frame Timing Stats in a supported development Player for GPU evidence.");
                File.WriteAllText(path,report.ToString());yield break;
            }
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            var settingsField=ao.GetType().GetField("m_Settings",flags);
            Assert.That(settingsField,Is.Not.Null,"Installed URP SSAO settings field changed; update the QA adapter.");
            var settings=settingsField.GetValue(ao);
            var downsample=settings.GetType().GetField("Downsample",flags);
            Assert.That(downsample,Is.Not.Null,"Installed URP SSAO Downsample field changed; update the QA adapter.");
            bool oldDownsample=(bool)downsample.GetValue(settings),oldActive=ao.isActive,oldEnabled=camera.enabled;
            var data=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            bool oldPost=data.renderPostProcessing;float oldScale=Time.timeScale;
            var oldTarget=camera.targetTexture;
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.DefaultHDR){name="AO bounded GPU comparison"};
            var timings=new FrameTiming[1];var full=new double[attempts*2];var half=new double[attempts*2];
            int fullCount=0,halfCount=0;ulong latest=0;
            try
            {
                target.Create();camera.targetTexture=target;camera.enabled=true;
                data.renderPostProcessing=productionPost;Time.timeScale=0;ao.SetActive(true);
                for(int run=0;run<4;run++)
                {
                    bool halfResolution=(run&1)!=0;downsample.SetValue(settings,halfResolution);
                    for(int i=0;i<warmup;i++){FrameTimingManager.CaptureFrameTimings();yield return null;}
                    // Discard the most recent warmup timestamp so delayed data cannot be counted twice.
                    if(FrameTimingManager.GetLatestTimings(1,timings)>0)latest=timings[0].frameStartTimestamp;
                    int valid=0;
                    for(int i=0;i<attempts;i++)
                    {
                        FrameTimingManager.CaptureFrameTimings();yield return null;
                        if(FrameTimingManager.GetLatestTimings(1,timings)==0)continue;
                        var timing=timings[0];
                        if(timing.frameStartTimestamp==0||timing.frameStartTimestamp<=latest)continue;
                        latest=timing.frameStartTimestamp;
                        if(!double.IsFinite(timing.gpuFrameTime)||timing.gpuFrameTime<=0)continue;
                        if(halfResolution)half[halfCount++]=timing.gpuFrameTime;else full[fullCount++]=timing.gpuFrameTime;
                        valid++;
                    }
                    report.AppendLine("run="+(run+1)+";mode="+(halfResolution?"half":"full")+";valid="+valid+"/"+attempts);
                }
                System.Array.Sort(full,0,fullCount);System.Array.Sort(half,0,halfCount);
                report.AppendLine(GpuStatistics("full",full,fullCount));report.AppendLine(GpuStatistics("half",half,halfCount));
                if(fullCount<30||halfCount<30)
                    report.AppendLine("UNAVAILABLE/INSUFFICIENT: fewer than 30 unique positive GPU timings in one mode. No cost or acceptance conclusion.");
                else
                    report.AppendLine("full-minus-half median GPUms="+Number(Median(full,fullCount)-Median(half,halfCount))+"; diagnostic comparison only, not an isolated pass cost.");
                File.WriteAllText(path,report.ToString());
            }
            finally
            {
                downsample.SetValue(settings,oldDownsample);ao.SetActive(oldActive);
                camera.targetTexture=oldTarget;camera.enabled=oldEnabled;data.renderPostProcessing=oldPost;Time.timeScale=oldScale;
                target.Release();Object.DestroyImmediate(target);
            }
        }
        private static string GpuStatistics(string label,double[] samples,int count)
        {
            if(count==0)return label+": GPU timing unavailable (no positive unique samples).";
            int p95=Mathf.Clamp(Mathf.CeilToInt(count*.95f)-1,0,count-1);
            return label+":n="+count+";medianGPUms="+Number(Median(samples,count))+";p95GPUms="+Number(samples[p95]);
        }
        private static double Median(double[] samples,int count)=>(count&1)==0?(samples[count/2-1]+samples[count/2])*.5:samples[count/2];
        private static string Number(double value)=>value.ToString("F4",System.Globalization.CultureInfo.InvariantCulture);
        private static T Find<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).FirstOrDefault();
        private static long Difference(Color32[] a,Color32[] b)
        {long sum=0;for(int i=0;i<a.Length;i++)sum+=System.Math.Abs(a[i].r-b[i].r)+System.Math.Abs(a[i].g-b[i].g)+System.Math.Abs(a[i].b-b[i].b);return sum;}
        private static Color32[] Capture(Camera camera,string name)
        {
            Directory.CreateDirectory(Folder);var old=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{rt.Create();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());return image.GetPixels32();}
            finally{camera.targetTexture=old;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}

#endif
