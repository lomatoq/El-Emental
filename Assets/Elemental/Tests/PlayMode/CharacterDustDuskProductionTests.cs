using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [Serializable] private sealed class DuskFrame
        {
            public int index,dustParticles,dustAffectedPixels;
            public float phase,solarAltitude,night,sunIntensity,moonIntensity,characterRoiLuminance,dustContrast,postExposure;
            public Vector3 ambientProbeL0;public Color ambientSky,ambientEquator;
        }
        [Serializable] private sealed class DuskReport
        {
            public string scope="Actual saved Linebreaker and frozen production surface-wind dust. Same camera/pose/particles, controlled sunset. Character ROI is a fixed screen region, not a segmentation mask; dust contrast is measured by renderer on/off difference. Visual inspection and shadow-filter GPU timing remain required.";
            public string[] materials;public List<DuskFrame> frames=new List<DuskFrame>();
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator ActualSavedCharacterDuskSeparatesAmbientOcclusionAlbedoAndNormals()
        {
            Actor actor=ShortPlayer();var motor=actor.Presentation.GetComponentInParent<PlanetMotor>();
            var sky=_scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CelestialSystemBehaviour>(true)).Single();
            var renderers=actor.Presentation.GetComponentsInChildren<Renderer>(false)
                .Where(r=>r.sharedMaterials.Any(m=>m!=null&&m.HasProperty("_SurfaceMode")&&m.GetFloat("_SurfaceMode")>.5f)).ToArray();
            Assert.That(renderers.Length,Is.GreaterThan(0),"Need saved Linebreaker character renderers.");
            var original=new Material[renderers.Length][];var temporary=new List<Material>();
            string folder="BuildReports/HardPolish/G02/CharacterDuskDiagnostic-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(folder);
            var camera=sky.TargetCamera;var output=new ProductionCaptureResolution();
            Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;
            var pose=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
            float oldScale=Time.timeScale,oldPhase=sky.Snapshot.TimeOfDay01;
            try
            {
                yield return output.WaitForRenderedSize(camera);
                actor.Input.Move=Unity.Mathematics.float2.zero;
                pose.Place(motor.transform.position+motor.FacingForward*5.4f+Vector3.Cross(motor.LocalUp,motor.FacingForward)*3f+motor.LocalUp*.8f,motor.transform.position);
                yield return new WaitForSeconds(7f);
                Time.timeScale=0f;
                for(int i=0;i<renderers.Length;i++)
                {
                    original[i]=renderers[i].sharedMaterials;
                    var copies=(Material[])original[i].Clone();
                    for(int j=0;j<copies.Length;j++)
                    {
                        Material source=copies[j];
                        if(source==null||!source.HasProperty("_SurfaceMode")||source.GetFloat("_SurfaceMode")<.5f)continue;
                        Assert.That(source.HasProperty("_DebugMode"),Is.True,source.name+" lacks diagnostic mode.");
                        copies[j]=new Material(source);temporary.Add(copies[j]);
                    }
                    renderers[i].sharedMaterials=copies;
                }
                Assert.That(temporary.Count,Is.GreaterThan(0));
                sky.SetTimeOfDayForQa(.505f);sky.EvaluatePresentationForQa();
                string[] names={"baseline","ssao","albedo","normals"};float[] modes={0f,5.5f,4.5f,1.5f};
                for(int mode=0;mode<modes.Length;mode++)
                {
                    foreach(var material in temporary)material.SetFloat("_DebugMode",modes[mode]);
                    for(int warm=0;warm<4;warm++)yield return _frame;
                    ProductionCaptureResolution.SaveScreen(Path.Combine(folder,names[mode]+".png"));
                }
                File.WriteAllText(Path.Combine(folder,"scope.txt"),
                    "Actual saved ShortPlayer Linebreaker, fixed pose/camera, phase .505. Only character materials use temporary diagnostic clones. " +
                    "baseline is ordinary final lighting; ssao is raw screen-space indirect occlusion (white=unoccluded); albedo bypasses lighting; normals encode world normal. " +
                    "Debug outputs still pass through production postprocessing, so colors are visual diagnostics, not raw buffer values. " +
                    "Other world objects/fire remain normally rendered and may vary between captures. Materials restored in finally.\n"+
                    string.Join("\n",temporary.Select(m=>m.name+"/"+m.shader.name+" ambient="+m.GetFloat("_AmbientStrength")+" occlusion="+m.GetFloat("_OcclusionStrength")+" texture="+m.GetFloat("_TextureStrength"))));
            }
            finally
            {
                for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null&&original[i]!=null)renderers[i].sharedMaterials=original[i];
                foreach(var material in temporary)UnityEngine.Object.Destroy(material);
                Time.timeScale=oldScale;sky.SetTimeOfDayForQa(oldPhase);sky.EvaluatePresentationForQa();
                if(pose!=null)UnityEngine.Object.DestroyImmediate(pose);
                camera.transform.SetPositionAndRotation(oldPosition,oldRotation);output.Dispose();
            }
        }

        [UnityTest,Timeout(300000)]
        public IEnumerator ActualSavedCharacterAndDustCrossSunsetContinuously()
        {
            Actor actor=ShortPlayer();var motor=actor.Presentation.GetComponentInParent<PlanetMotor>();
            var sky=_scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CelestialSystemBehaviour>(true)).Single();
            var dust=_scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EarthSurfaceWindDust>(true)).Single();
            var dustRenderer=dust.GetComponent<ParticleSystemRenderer>();
            var character=actor.Presentation.GetComponentsInChildren<Renderer>(false)
                .Where(r=>r.sharedMaterials.Any(m=>m!=null&&m.HasProperty("_SurfaceMode")&&m.GetFloat("_SurfaceMode")>.5f)).ToArray();
            Assert.That(character.Length,Is.GreaterThan(0),"Actual authored Linebreaker materials are required.");
            var report=new DuskReport {materials=character.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(m=>m.name+"/"+m.shader.name).Distinct().ToArray()};
            string folder="BuildReports/HardPolish/G02/CharacterDustDusk-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
            var camera=sky.TargetCamera;var output=new ProductionCaptureResolution();
            Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;
            var pose=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
            float oldScale=Time.timeScale,oldPhase=sky.Snapshot.TimeOfDay01;bool oldDustEnabled=dustRenderer.enabled;
            Material originalDust=dustRenderer.sharedMaterial,comparisonDust=null;
            try
            {
                yield return output.WaitForRenderedSize(camera);
                actor.Input.Move=Unity.Mathematics.float2.zero;
                pose.Place(motor.transform.position+motor.FacingForward*5.4f+Vector3.Cross(motor.LocalUp,motor.FacingForward)*3f+motor.LocalUp*.8f,motor.transform.position);
                yield return new WaitForSeconds(7f);
                Assert.That(dust.LiveParticles,Is.GreaterThan(0),"Need existing production dust, not a synthetic replacement.");
                Time.timeScale=0f;
                yield return _frame;
                Bounds bounds=character[0].bounds;foreach(var renderer in character)bounds.Encapsulate(renderer.bounds);
                RectInt characterRect=DuskScreenRect(camera,bounds);
                int frozenCount=dust.LiveParticles;
                for(int i=0;i<=50;i++)
                {
                    float phase=.475f+i*.001f;sky.SetTimeOfDayForQa(phase);sky.EvaluatePresentationForQa();
                    for(int warm=0;warm<4;warm++)yield return _frame;
                    Texture2D on=ScreenCapture.CaptureScreenshotAsTexture();
                    dustRenderer.enabled=false;yield return _frame;
                    Texture2D off=ScreenCapture.CaptureScreenshotAsTexture();
                    dustRenderer.enabled=oldDustEnabled;
                    try
                    {
                        Assert.That(on.width,Is.EqualTo(1920));Assert.That(on.height,Is.EqualTo(1080));
                        var a=on.GetPixels32();var b=off.GetPixels32();double sum=0;int count=0;double dustDifference=0;int affected=0;
                        for(int y=characterRect.yMin;y<characterRect.yMax;y++)for(int x=characterRect.xMin;x<characterRect.xMax;x++)
                        {sum+=DuskLuma(a[y*on.width+x]);count++;}
                        for(int pixel=0;pixel<a.Length;pixel++)
                        {
                            float difference=Mathf.Abs(DuskLuma(a[pixel])-DuskLuma(b[pixel]));
                            if(difference>1f/255f){dustDifference+=difference;affected++;}
                        }
                        var probe=RenderSettings.ambientProbe;
                        var color=VolumeManager.instance.stack.GetComponent<ColorAdjustments>();
                        report.frames.Add(new DuskFrame {index=i,phase=phase,solarAltitude=Shader.GetGlobalFloat("_ElementalSolarAltitude"),
                            night=sky.Snapshot.Night01,sunIntensity=sky.SunLight.intensity,moonIntensity=sky.MoonLight!=null?sky.MoonLight.intensity:0,
                            dustParticles=dust.LiveParticles,dustAffectedPixels=affected,characterRoiLuminance=(float)(sum/Math.Max(1,count)),
                            dustContrast=(float)(dustDifference/Math.Max(1,affected)),postExposure=color!=null?color.postExposure.value:0,
                            ambientProbeL0=new Vector3(probe[0,0],probe[1,0],probe[2,0]),ambientSky=RenderSettings.ambientSkyColor,
                            ambientEquator=RenderSettings.ambientEquatorColor});
                        File.WriteAllBytes(Path.Combine(folder,"dusk-"+i.ToString("D3")+".png"),on.EncodeToPNG());
                    }
                    finally{UnityEngine.Object.Destroy(on);UnityEngine.Object.Destroy(off);}
                    Assert.That(dust.LiveParticles,Is.EqualTo(frozenCount),"Density/lifetime must remain constant while lighting is sampled.");
                }
                // Same-phase spatial-filter comparison uses the actual saved dust
                // renderer/material, retaining all its texture/tint/opacity settings.
                comparisonDust=new Material(originalDust);dustRenderer.sharedMaterial=comparisonDust;
                foreach(float phase in new[]{.48f,.495f,.51f})
                foreach(float width in new[]{0f,.35f})
                {
                    Assert.That(comparisonDust.HasProperty("_ShadowSoftnessMeters"),Is.True);
                    comparisonDust.SetFloat("_ShadowSoftnessMeters",width);
                    sky.SetTimeOfDayForQa(phase);sky.EvaluatePresentationForQa();
                    for(int warm=0;warm<4;warm++)yield return _frame;
                    ProductionCaptureResolution.SaveScreen(Path.Combine(folder,"shadow-"+phase.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"-"+width.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"));
                }
                Assert.That(report.frames.Max(f=>f.dustAffectedPixels),Is.GreaterThan(50),"Dust never contributed visible pixels in this framing.");
                Assert.That(report.frames.First().solarAltitude,Is.GreaterThan(.1f));
                Assert.That(report.frames.Last().solarAltitude,Is.LessThan(-.1f));
                Assert.That(report.frames.All(f=>float.IsFinite(f.characterRoiLuminance)&&float.IsFinite(f.dustContrast)),Is.True);
            }
            finally
            {
                dustRenderer.enabled=oldDustEnabled;dustRenderer.sharedMaterial=originalDust;
                if(comparisonDust!=null)UnityEngine.Object.Destroy(comparisonDust);
                Time.timeScale=oldScale;sky.SetTimeOfDayForQa(oldPhase);sky.EvaluatePresentationForQa();
                if(pose!=null)UnityEngine.Object.DestroyImmediate(pose);
                camera.transform.SetPositionAndRotation(oldPosition,oldRotation);output.Dispose();
                File.WriteAllText(Path.Combine(folder,"frames.json"),JsonUtility.ToJson(report,true));
            }
        }
        private static float DuskLuma(Color32 c)=>(.2126f*c.r+.7152f*c.g+.0722f*c.b)/255f;
        private static RectInt DuskScreenRect(Camera camera,Bounds bounds)
        {
            Vector2 lo=new Vector2(float.PositiveInfinity,float.PositiveInfinity),hi=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                Vector3 screen=camera.WorldToScreenPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)));
                Assert.That(screen.z,Is.GreaterThan(0));lo=Vector2.Min(lo,screen);hi=Vector2.Max(hi,screen);
            }
            return new RectInt(Mathf.Clamp(Mathf.FloorToInt(lo.x),0,Screen.width-1),Mathf.Clamp(Mathf.FloorToInt(lo.y),0,Screen.height-1),
                Mathf.Clamp(Mathf.CeilToInt(hi.x-lo.x),1,Screen.width-Mathf.Clamp(Mathf.FloorToInt(lo.x),0,Screen.width-1)),
                Mathf.Clamp(Mathf.CeilToInt(hi.y-lo.y),1,Screen.height-Mathf.Clamp(Mathf.FloorToInt(lo.y),0,Screen.height-1)));
        }
    }
}
