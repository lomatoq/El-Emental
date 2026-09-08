using System;
using System.IO;
using Elemental.Presentation.Rendering;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
namespace Elemental.Authoring.Editor
{
    public static class ValleyAtmosphereExecutionQa
    {
        private static T Find<T>() where T:Component
        { foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects()){var value=root.GetComponentInChildren<T>(true);if(value!=null)return value;}return null; }
        [MenuItem("Elemental/Graphics/Build Valley Atmosphere 1080 Benchmark")]
        public static void Build()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying || scene.name!="EarthCoreSlice" || scene.isDirty || string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("Save reviewed EarthCoreSlice and stop Play before benchmark build.");
            if(Find<ValleyAtmosphereController>()==null)throw new InvalidOperationException("Install and visually review V2 before performance build.");
            const string folder="Builds/ValleyAtmosphereBench";Directory.CreateDirectory(folder);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene.path},locationPathName=folder+"/ValleyAtmosphereBench.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText(folder+"/build-evidence.txt","utc="+DateTime.UtcNow.ToString("O")+"\nresult="+result.summary.result+"\nframeTimingStats="+PlayerSettings.enableFrameTimingStats+"\nNo PlayerSettings or scene build list modified. Missing FrameTiming samples remain unavailable.");
            if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Benchmark build failed: "+result.summary.result);
        }
        [MenuItem("Elemental/Graphics/Capture Very Far Art On Off")]
        public static void CaptureFarArt()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Ready production Play session required.");
            var owner=Find<ValleyAtmosphereController>();var sky=Find<CelestialSystemBehaviour>();
            if(owner==null||sky==null||sky.TargetCamera==null)throw new InvalidOperationException("Bound atmosphere and camera required.");
            var camera=sky.TargetCamera;var target=camera.targetTexture;var active=RenderTexture.active;
            bool enabled=owner.FarArtEnabled;var rt=new RenderTexture(1920,1080,24);rt.Create();
            var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            const string folder="Logs/VeryFarArt";Directory.CreateDirectory(folder);
            try
            {
                for(int i=0;i<2;i++)
                {
                    owner.FarArtEnabled=i==1;owner.Publish();camera.targetTexture=rt;camera.Render();
                    RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();
                    File.WriteAllBytes(folder+(i==0?"/off.png":"/on.png"),pixels.EncodeToPNG());
                }
                File.WriteAllText(folder+"/evidence.txt","utc="+DateTime.UtcNow.ToString("O")+"\n1920x1080 same camera/time/lighting. Very-far art starts1000m, full1800m. Near arena/UI protection and actual visibility require image review. No GPU budget measured.");
            }
            finally
            {
                camera.targetTexture=target;RenderTexture.active=active;owner.FarArtEnabled=enabled;owner.Publish();
                rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);
            }
        }
        [MenuItem("Elemental/Graphics/Capture Valley Atmosphere 1080 Art First")]
        public static void Capture()=>Capture(false);
        [MenuItem("Elemental/Graphics/Capture Valley Atmosphere No Post Diagnostic")]
        public static void CaptureNoPost()=>Capture(true);
        private static void Capture(bool noPost)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Ready production Play session required.");
            var owner=Find<ValleyAtmosphereController>();var sky=Find<CelestialSystemBehaviour>();
            if(owner==null || sky==null || sky.TargetCamera==null)throw new InvalidOperationException("Bound V2 owner and camera required.");
            var camera=sky.TargetCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;
            var additional=camera.GetComponent<UniversalAdditionalCameraData>();bool originalPost=additional!=null && additional.renderPostProcessing;
            var target=camera.targetTexture;var active=RenderTexture.active;var phase=sky.Snapshot.TimeOfDay01;var authority=sky.LightingAuthority;
            bool fog=owner.FogEnabled,clouds=owner.CloudsEnabled,motion=owner.AnimateClouds;int debug=owner.DebugMode;
            var rt=new RenderTexture(1920,1080,24);rt.Create();var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            string folder=noPost?"Logs/ValleyAtmosphere1080/NoPostDiagnostic":"Logs/ValleyAtmosphere1080";Directory.CreateDirectory(folder);
            string[] names={"gameplay","lookdown","reverse-protected","side-protected","under-clouds"};
            try
            {
                if(noPost && additional!=null)additional.renderPostProcessing=false;
                File.WriteAllText(folder+"/frame.txt","worldCamera="+position+"\nlocalCamera="+owner.transform.InverseTransformPoint(position)+"\nlocalView="+owner.transform.InverseTransformDirection(camera.transform.forward)+"\nframePosition="+owner.transform.position+"\nframeUp="+owner.transform.up+"\nframeForward="+owner.transform.forward);
                owner.AnimateClouds=false;sky.SetLightingAuthorityForQa(Elemental.Simulation.Time.CelestialLightingAuthorityMode.AnimatedEphemeris);
                Vector3 centre=owner.transform.position,up=owner.transform.up,forward=owner.transform.forward,right=owner.transform.right;
                for(int day=0;day<2;day++)for(int view=0;view<5;view++)
                {
                    camera.transform.SetPositionAndRotation(position,rotation);
                    if(view==1){camera.transform.position=centre+up*95+forward*125;camera.transform.rotation=Quaternion.LookRotation(centre-up*160-camera.transform.position,up);}
                    if(view==2){camera.transform.position=centre+up*65-forward*2200;camera.transform.rotation=Quaternion.LookRotation(centre-camera.transform.position,up);}
                    if(view==3){camera.transform.position=centre+up*65+right*2200;camera.transform.rotation=Quaternion.LookRotation(centre-camera.transform.position,up);}
                    if(view==4){camera.transform.position=centre-up*200+forward*250;camera.transform.rotation=Quaternion.LookRotation(centre-camera.transform.position,up);}
                    sky.SetTimeOfDayForQa(day==0?0.25f:0.75f);sky.EvaluatePresentationForQa();
                    for(int mode=0;mode<3;mode++)
                    {
                        owner.FogEnabled=mode!=0;owner.CloudsEnabled=mode==2;owner.DebugMode=0;owner.Publish();
                        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();
                        File.WriteAllBytes(Path.Combine(folder,(day==0?"day-":"night-")+names[view]+"-"+new[]{"off","fog","clouds"}[mode]+".png"),pixels.EncodeToPNG());
                    }
                }
                File.WriteAllText(folder+"/evidence.txt","utc="+DateTime.UtcNow.ToString("O")+"\n1920x1080 production postprocess retained. Off means V2 disabled, original feature retained. Review art before benchmarking. Reverse and side cameras target planet from 2200m; under view tests bank occlusion through fog. No performance claim from captures.");
            }
            finally
            {
                if(additional!=null)additional.renderPostProcessing=originalPost;
                camera.transform.SetPositionAndRotation(position,rotation);camera.targetTexture=target;RenderTexture.active=active;
                owner.FogEnabled=fog;owner.CloudsEnabled=clouds;owner.AnimateClouds=motion;owner.DebugMode=debug;owner.Publish();
                sky.SetTimeOfDayForQa(phase);sky.SetLightingAuthorityForQa(authority);sky.EvaluatePresentationForQa();
                Object.DestroyImmediate(pixels);rt.Release();Object.DestroyImmediate(rt);
            }
        }
    }
}
