from pathlib import Path
base=Path('Tools/AoFinalQa');rel='Assets/Elemental/Tests/PlayMode/VisualPolishAtmosphereTests.cs'
for part in ['before','after']:
 p=base/part/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(Path(rel).read_bytes())
p=base/'after'/rel;s=p.read_text()
anchor='''            }
            finally
            {
                Shader.SetGlobalFloat("_EarthSeismicVision",0);Time.timeScale=1;'''
insert='''                // Run timing last so the bounded sampling window cannot disturb the earlier same-frame proofs.
                sky.SetTimeOfDayForQa(.25f);sky.EvaluatePresentationForQa();atmosphere.Publish();
                CaptureContactAo(scene,camera,ao);
                yield return MeasureAoGpu(camera,ao,post);
            }
            finally
            {
                Shader.SetGlobalFloat("_EarthSeismicVision",0);Time.timeScale=1;'''
assert anchor in s;s=s.replace(anchor,insert)
marker='        private static T Find<T>'
helper='''        private static void CaptureContactAo(Scene scene,Camera camera,UnityEngine.Rendering.Universal.ScriptableRendererFeature ao)
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
            var report=new System.Text.StringBuilder("FrameTimingManager whole-frame GPU comparison; actual production camera at1280x720.\\n");
            report.AppendLine("Editor="+Application.isEditor+"; device="+SystemInfo.graphicsDeviceName+"; API="+SystemInfo.graphicsDeviceType);
            report.AppendLine("Scope includes other rendering/editor overhead; this is not an isolated SSAO GPU marker. No CPU/wall-clock substitution.");
            report.AppendLine("Order full/half/full/half;15warmup+60attempts per run. Scaled simulation frozen; production postprocessing restored for measurement.");
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
                    report.AppendLine("UNAVAILABLE/INSUFFICIENT: fewer than30unique positive GPU timings in one mode. No cost or acceptance conclusion.");
                else
                    report.AppendLine("full-minus-half median GPUms="+Number(full[fullCount/2]-half[halfCount/2])+"; diagnostic comparison only, not an isolated pass cost.");
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
            return label+":n="+count+";medianGPUms="+Number(samples[count/2])+";p95GPUms="+Number(samples[p95]);
        }
        private static string Number(double value)=>value.ToString("F4",System.Globalization.CultureInfo.InvariantCulture);
'''
assert marker in s;s=s.replace(marker,helper+marker);p.write_text(s)
print('Staged existing VisualPolishAtmosphereTests: debug capture + bounded4x75frame GPU comparison; no additional UnityTest cases.')
