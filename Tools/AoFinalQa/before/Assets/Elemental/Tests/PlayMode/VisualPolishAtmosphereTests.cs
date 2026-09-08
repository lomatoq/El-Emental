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
            }
            finally
            {
                Shader.SetGlobalFloat("_EarthSeismicVision",0);Time.timeScale=1;
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            }
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
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
