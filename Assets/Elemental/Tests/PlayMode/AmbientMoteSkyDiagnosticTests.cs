using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(180000)] public IEnumerator SavedAmbientMoteAgainstSkyDiagnostic()
  {
   yield return ReadyFireAbilities();
   var sky=All<CelestialSystemBehaviour>().Single();var camera=sky.TargetCamera;
   using var output=new ProductionCaptureResolution();yield return output.WaitForRenderedSize(camera);
   var pose=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
   Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;
   float oldTime=Time.timeScale,oldPhase=sky.Snapshot.TimeOfDay01;
   var go=new GameObject("Production dust against actual sky");var particles=go.AddComponent<ParticleSystem>();
   particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var main=particles.main;main.loop=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=100;main.startSpeed=0;main.startSize=3;
   var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
   var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;
   Material material=null;
   string folder="BuildReports/HardPolish/G05/MotesSky-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
   var report=new System.Text.StringBuilder("phase,R,G,B,shader\n");
   try
   {
#if UNITY_EDITOR
    material=new Material(UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/Materials/LightDustMote.mat"));
#endif
    Assert.That(material,Is.Not.Null);renderer.sharedMaterial=material;
    Elemental.Runtime.World.EarthParticleSystemTuningApplier.ConfigureDustFlipbook(particles);
    Vector3 up=flightMotor.LocalUp,origin=flightMotor.transform.position+up*14;
    Vector3 forward=(flightMotor.FacingForward+up*.6f).normalized;
    pose.Place(origin,origin+forward*10);
    particles.Emit(new ParticleSystem.EmitParams{position=origin+forward*4,startSize=3,startLifetime=2,startColor=Color.white,velocity=Vector3.zero},1);
    particles.Simulate(.5f,true,false);particles.Pause();Time.timeScale=0;
    var originalShader=material.shader;
    foreach(float phase in new[]{.25f,.52f})
    {
     sky.SetTimeOfDayForQa(phase);sky.EvaluatePresentationForQa();
     for(int n=0;n<5;n++)yield return new WaitForEndOfFrame();
     Assert.That(material.shader,Is.SameAs(originalShader));
     var frame=ScreenCapture.CaptureScreenshotAsTexture();
     renderer.enabled=false;yield return new WaitForEndOfFrame();var withoutDust=ScreenCapture.CaptureScreenshotAsTexture();renderer.enabled=true;
     try
     {
      var colors=frame.GetPixels(frame.width/2-60,frame.height/2-60,120,120);Color sum=Color.clear;
      var background=withoutDust.GetPixels(frame.width/2-60,frame.height/2-60,120,120);float difference=0;
      for(int pixel=0;pixel<colors.Length;pixel++)difference+=Mathf.Abs(colors[pixel].r-background[pixel].r)+Mathf.Abs(colors[pixel].g-background[pixel].g)+Mathf.Abs(colors[pixel].b-background[pixel].b);
      Assert.That(difference/colors.Length,Is.GreaterThan(.005f),"Dust must contribute actual visible pixels; a bare-sky capture is not dust evidence.");
      foreach(var color in colors)sum+=color;sum/=colors.Length;

      report.AppendLine(phase+","+sum.r+","+sum.g+","+sum.b+","+material.shader.name);
      File.WriteAllBytes(Path.Combine(folder,"sky-dust-"+phase.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+".png"),frame.EncodeToPNG());
     }
     finally{UnityEngine.Object.Destroy(frame);UnityEngine.Object.Destroy(withoutDust);}
    }
   }
   finally
   {
    File.WriteAllText(Path.Combine(folder,"colors.csv"),report.ToString());
    Time.timeScale=oldTime;sky.SetTimeOfDayForQa(oldPhase);sky.EvaluatePresentationForQa();
    UnityEngine.Object.Destroy(go);if(material!=null)UnityEngine.Object.Destroy(material);UnityEngine.Object.DestroyImmediate(pose);
    camera.transform.SetPositionAndRotation(oldPosition,oldRotation);ReleaseFireAbilityFixture();
   }
  }
 }
}
