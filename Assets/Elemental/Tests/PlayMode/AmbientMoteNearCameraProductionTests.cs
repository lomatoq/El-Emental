using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(240000)] public IEnumerator SavedAmbientMotesKeepTheirRealSizeAndFadeNearCameraWithBloomComparison()
  {
   yield return ReadyFireAbilities();
   var sky=All<CelestialSystemBehaviour>().Single();var camera=sky.TargetCamera;
   using var resolution=new ProductionCaptureResolution();yield return resolution.WaitForRenderedSize(camera);
   var look=All<EarthChargeCameraLookdevV2>().Single(x=>x.gameObject==camera.gameObject);
   var actualSources=All<ParticleSystem>().Where(p=>p.name=="Sunlit Air Motes").ToArray();
   Assert.That(actualSources.Length,Is.GreaterThanOrEqualTo(1),"Saved camera-local ambient source must exist");
   foreach(var source in actualSources)
   {
    var owner=source.transform.parent.GetComponent<EarthChargeCameraLookdevV2>();
    Assert.That(owner,Is.Not.Null,source.name+" must belong to its explicit production camera");
    Assert.That(owner.LightMotes,Is.SameAs(source),owner.name+" has a stale/null serialized ambient binding");
    Assert.That(source.GetComponent<ParticleSystemRenderer>().maxParticleSize,Is.EqualTo(.015f).Within(.00001),"Every saved ambient renderer must have the screen-size bound, even before its camera activates");
   }
   var motes=actualSources.Single(p=>p.transform.parent==camera.transform);
   Assert.That(look.LightMotes,Is.SameAs(motes));
   var renderer=motes.GetComponent<ParticleSystemRenderer>();var material=renderer.sharedMaterial;
   Assert.That(renderer.maxParticleSize,Is.EqualTo(.015f).Within(.00001));
   Assert.That(material.GetFloat("_Brightness"),Is.EqualTo(.9f).Within(.001));
   Assert.That(material.GetFloat("_CameraNearFadeStart"),Is.EqualTo(.3f).Within(.001));
   Assert.That(material.GetFloat("_CameraNearFadeEnd"),Is.EqualTo(1.2f).Within(.001));
   var saved=new ParticleSystem.Particle[motes.main.maxParticles];int count=motes.GetParticles(saved);
   bool wasPlaying=motes.isPlaying,wasPaused=motes.isPaused;var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
   float oldTime=Time.timeScale;var pose=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
   var otherMotes=All<ParticleSystem>().Where(p=>p!=motes&&p.name=="Sunlit Air Motes").Select(p=>p.GetComponent<ParticleSystemRenderer>()).ToArray();
   bool[] oldOther=otherMotes.Select(r=>r.enabled).ToArray();bool oldRenderer=renderer.enabled;
   var blooms=new List<Bloom>();var oldBloom=new List<bool>();
   foreach(var volume in All<Volume>())
   {
    var profile=volume.HasInstantiatedProfile()?volume.profile:volume.sharedProfile;
    if(profile!=null&&profile.TryGet<Bloom>(out var bloom)&&!blooms.Contains(bloom)){blooms.Add(bloom);oldBloom.Add(bloom.active);}
   }
   string folder="BuildReports/HardPolish/G05/AmbientMoteNear-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
   var report=new System.Text.StringBuilder("distance,bloom,meanRGBDifference,peakRGBDifference,size,actualDepth,controlMean,frame\n");
   try
   {
    foreach(var r in otherMotes)r.enabled=false;
    Vector3 up=flightMotor.LocalUp,origin=flightMotor.transform.position+up*14,forward=(flightMotor.FacingForward+up*.6f).normalized;
    pose.Place(origin,origin+forward*10);motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);Time.timeScale=0;
    float authoredSize=motes.main.startSize.constantMax;Assert.That(authoredSize,Is.InRange(.03f,.085f),"Use real saved mote art, not a synthetic metre-wide cloud");
    Color authoredColor=motes.main.startColor.colorMax;
    foreach(bool bloomOn in new[]{false,true})
    {
     for(int j=0;j<blooms.Count;j++)blooms[j].active=bloomOn&&oldBloom[j];
     foreach(float distance in new[]{.2f,.6f,2f,5f})
     {
      var particle=new ParticleSystem.Particle{position=origin+forward*distance,startSize=authoredSize,startColor=authoredColor,startLifetime=2,remainingLifetime=1.5f,randomSeed=17};
      motes.SetParticles(new[]{particle},1);motes.Pause();renderer.enabled=true;
      for(int f=0;f<3;f++)yield return new WaitForEndOfFrame();
      // Freeze the actual particle/camera frame. timeScale=0 alone does not stop
      // unscaled atmosphere/lens updates between ordinary screen captures.
      var actual=new ParticleSystem.Particle[1];Assert.That(motes.GetParticles(actual),Is.EqualTo(1));
      float actualDepth=Vector3.Dot(actual[0].position-camera.transform.position,camera.transform.forward);
      int captureFrame=Time.frameCount;Texture2D control=null,with=null,without=null;
      try
      {
       renderer.enabled=false;control=RenderFireDofSameFrame(camera);
       renderer.enabled=false;without=RenderFireDofSameFrame(camera);
       renderer.enabled=true;with=RenderFireDofSameFrame(camera);
       int span=64;var a=with.GetPixels(with.width/2-span/2,with.height/2-span/2,span,span);var b=without.GetPixels(without.width/2-span/2,without.height/2-span/2,span,span);
       var c=control.GetPixels(control.width/2-span/2,control.height/2-span/2,span,span);
       float mean=0,peak=0,controlMean=0;for(int i=0;i<a.Length;i++){float difference=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b);mean+=difference;peak=Mathf.Max(peak,difference);controlMean+=Mathf.Abs(c[i].r-b[i].r)+Mathf.Abs(c[i].g-b[i].g)+Mathf.Abs(c[i].b-b[i].b);}mean/=a.Length;controlMean/=a.Length;
       report.AppendLine(distance+","+bloomOn+","+mean+","+peak+","+authoredSize+","+actualDepth+","+controlMean+","+captureFrame);
       string label=distance.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+(bloomOn?"-bloom":"-no-bloom");
       // Failure must retain the actual images, not only an assertion and an empty CSV.
       File.WriteAllBytes(folder+"/"+label+"-on.png",with.EncodeToPNG());File.WriteAllBytes(folder+"/"+label+"-off.png",without.EncodeToPNG());File.WriteAllBytes(folder+"/"+label+"-off-control.png",control.EncodeToPNG());
       File.WriteAllText(folder+"/metrics.csv",report.ToString());
       Assert.That(Time.frameCount,Is.EqualTo(captureFrame),"All differential renders must share one simulation/render-pose frame");
       Assert.That(actualDepth,Is.EqualTo(distance).Within(.002f),"Measure the real camera-space particle position");
       Assert.That(controlMean,Is.LessThan(.001f),"Same-frame OFF/OFF control must be stable before attributing pixels to the mote");
       if(distance<.3f)Assert.That(mean,Is.LessThan(.001f),"Camera-near mote must fade, including bloom energy");
       if(distance>=2&&!bloomOn)Assert.That(peak,Is.GreaterThan(.004f),"Visible actual authored mote must survive beyond near fade");
      }
      finally{renderer.enabled=true;if(with!=null)UnityEngine.Object.Destroy(with);if(without!=null)UnityEngine.Object.Destroy(without);if(control!=null)UnityEngine.Object.Destroy(control);}
     }
    }
   }
   finally
   {
    File.WriteAllText(folder+"/metrics.csv",report.ToString());
    for(int i=0;i<blooms.Count;i++)blooms[i].active=oldBloom[i];for(int i=0;i<otherMotes.Length;i++)otherMotes[i].enabled=oldOther[i];
    renderer.enabled=oldRenderer;motes.SetParticles(saved,count);if(wasPlaying)motes.Play();else if(wasPaused)motes.Pause();Time.timeScale=oldTime;
    UnityEngine.Object.DestroyImmediate(pose);camera.transform.SetPositionAndRotation(oldPosition,oldRotation);ReleaseFireAbilityFixture();
   }
  }
 }
}
