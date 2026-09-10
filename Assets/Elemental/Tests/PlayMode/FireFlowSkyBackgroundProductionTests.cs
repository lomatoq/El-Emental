using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.Fire;
using Elemental.Input.Gestures;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [Serializable] private sealed class SkyFireShot {public int shot,skyExpectedPixels,skyVisiblePixels;public float skyNormalEnergy,skyBypassEnergy;public int unstableSkyBaselinePixels,skyBaselinePixels;public int stableSkyExpectedPixels;}
  [Serializable] private sealed class SkyFireReport {public string scope="Actual saved fighter/production flow and post pipeline; frozen gas/cloud motion, diagnostic dithering disabled, settled focus, analysis restricted to repeated-baseline stable sky pixels, GPU atmosphere sky-depth diagnostic mask, normal/depth-bypass radiance and foreground occluder. No energy retuning.";public List<SkyFireShot> shots=new();public float occludedEnergy,bypassEnergy;}
  [UnityTest,Timeout(180000)] public IEnumerator ActualTransportedFireSurvivesSkyVeilAndStillHonorsForegroundDepth()
  {
   string folder="BuildReports/HardPolish/G05/SkyDepth-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
   var camera=System.Linq.Enumerable.Single(All<CelestialSystemBehaviour>()).TargetCamera;
   var valley=System.Linq.Enumerable.Single(All<ValleyAtmosphereController>());
   var cameraData=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
   Assert.That(cameraData,Is.Not.Null);bool dithering=cameraData.dithering;
   var resolution=new ProductionCaptureResolution();var report=new SkyFireReport();
   FireVisualCaptureCamera pose=null;DirectFireInputLease lease=null;FireFlowVolumeBackend gas=null;GameObject blocker=null;
   bool animateClouds=valley.AnimateClouds;float scale=Time.timeScale;int cameraMask=camera.cullingMask,debug=valley.DebugMode;
   Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;
   try
   {
    cameraData.dithering=false;yield return resolution.WaitForRenderedSize(camera);yield return EnterCombat();
    var input=duel.PlayerTransform.GetComponentInChildren<MagicInputController>(true);
    Assert.That(input.TrySelectElement(ElementId.Fire),Is.True);lease=new DirectFireInputLease(input);
    var session=binding.PlayerSession;Vector3 up=duel.PlayerTransform.up;
    Vector3 direction=(FindClearFireDirection(duel.PlayerTransform,session.MuzzlePosition)+up*.25f).normalized;
    Assert.That(session.TryBegin(session.MuzzlePosition+direction*8),Is.True);
    yield return new WaitForSeconds(.65f);
    gas=binding.Presenter(session.Group.Slot).CpuDiagnostics.FlowDiagnostics;Assert.That(gas,Is.Not.Null);
    Assert.That(gas.Solver.Count,Is.GreaterThan(20));
    var parcelShader=Shader.Find("Elemental/Fire/TransportedFlowParcel");
    // Rendering placement is a separate contract from depth-bypass radiance.
    Assert.That(parcelShader,Is.Not.Null);
    Assert.That(parcelShader.FindPassTagValue(0,new UnityEngine.Rendering.ShaderTagId("LightMode")).name,Is.EqualTo("ElementalValleyCloud"));
    Time.timeScale=0;valley.AnimateClouds=false;gas.SetRenderingLayerForQa(31);camera.cullingMask=cameraMask|(1<<31);
    // A timeScale write does not replace this frame's already computed delta.
    // Let pending presentation LateUpdate finish, then complete a zero-delta frame.
    yield return new WaitForEndOfFrame();yield return null;yield return new WaitForEndOfFrame();
    Assert.That(Time.deltaTime,Is.Zero,"Freeze must be effective before snapshotting gas.");
    int count=gas.Solver.Count;Assert.That(count,Is.GreaterThan(20));
    pose=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
    Vector3 subject=session.MuzzlePosition+direction*2.5f,side=Vector3.Cross(up,direction).normalized;
    for(int shot=0;shot<3;shot++)
    {
     Vector3 eye=subject+(Quaternion.AngleAxis((shot-1)*25,up)*side)*6-up*.6f;
     pose.Place(eye,subject);yield return new WaitForSecondsRealtime(2f);
     Texture2D on=null,off=null,mask=null,bypass=null,offRepeat=null;
     try
     {
      valley.DebugMode=0;gas.SetDebugViewForQa(0);camera.cullingMask=cameraMask|(1<<31);
      yield return CaptureSkyFrame(t=>on=t);
      camera.cullingMask=cameraMask&~(1<<31);yield return CaptureSkyFrame(t=>off=t);
      valley.DebugMode=2;yield return CaptureSkyFrame(t=>mask=t);
      valley.DebugMode=0;camera.cullingMask=cameraMask|(1<<31);gas.SetDebugViewForQa(3);
      yield return CaptureSkyFrame(t=>bypass=t);
      camera.cullingMask=cameraMask&~(1<<31);yield return CaptureSkyFrame(t=>offRepeat=t);
      var a=on.GetPixels32();var b=off.GetPixels32();var m=mask.GetPixels32();var c=bypass.GetPixels32();var repeated=offRepeat.GetPixels32();var data=new SkyFireShot{shot=shot};
      for(int i=0;i<a.Length;i++)
      {
       bool sky=m[i].b>64&&m[i].b>m[i].r*1.5f&&m[i].b>m[i].g*1.25f;
       if(sky){data.skyBaselinePixels++;if(Mathf.Abs(SkyLuma(repeated[i])-SkyLuma(b[i]))>1f/255f)data.unstableSkyBaselinePixels++;}
       float expected=SkyLuma(c[i])-SkyLuma(b[i]),actual=SkyLuma(a[i])-SkyLuma(b[i]);
       if(!sky||expected<=1f/255f)continue;
       if(Mathf.Abs(SkyLuma(repeated[i])-SkyLuma(b[i]))>1f/255f)continue;
       data.stableSkyExpectedPixels++;
       data.skyExpectedPixels++;data.skyBypassEnergy+=expected;data.skyNormalEnergy+=Mathf.Max(0,actual);
       if(actual>expected*.5f)data.skyVisiblePixels++;
      }
      report.shots.Add(data);
      File.WriteAllBytes(folder+"/sky-"+shot+"-normal.png",on.EncodeToPNG());File.WriteAllBytes(folder+"/sky-"+shot+"-without-fire.png",off.EncodeToPNG());
      File.WriteAllBytes(folder+"/sky-"+shot+"-depth-mask.png",mask.EncodeToPNG());File.WriteAllBytes(folder+"/sky-"+shot+"-bypass.png",bypass.EncodeToPNG());
      File.WriteAllBytes(folder+"/sky-"+shot+"-without-fire-repeat.png",offRepeat.EncodeToPNG());

     }
     finally{if(on!=null)UnityEngine.Object.Destroy(on);if(off!=null)UnityEngine.Object.Destroy(off);if(mask!=null)UnityEngine.Object.Destroy(mask);if(bypass!=null)UnityEngine.Object.Destroy(bypass);if(offRepeat!=null)UnityEngine.Object.Destroy(offRepeat);}
     Assert.That(gas.Solver.Count,Is.EqualTo(count),"Only rendering may change during paired screenshots.");
    }
    // A real opaque test wall between camera and gas must still occlude the
    // normal ray interval. Depth-bypass mode is diagnostic only.
    blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Owned fire depth occluder";
    Vector3 view=(subject-camera.transform.position).normalized;
    blocker.transform.SetPositionAndRotation(camera.transform.position+view*1f,Quaternion.LookRotation(view,up));blocker.transform.localScale=new Vector3(12,12,.2f);
    Physics.SyncTransforms();
    Texture2D covered=null,baseImage=null,unclipped=null;
    try
    {
     camera.cullingMask=cameraMask|(1<<31);gas.SetDebugViewForQa(0);yield return CaptureSkyFrame(t=>covered=t);
     camera.cullingMask=cameraMask&~(1<<31);yield return CaptureSkyFrame(t=>baseImage=t);
     camera.cullingMask=cameraMask|(1<<31);gas.SetDebugViewForQa(3);yield return CaptureSkyFrame(t=>unclipped=t);
     var a=covered.GetPixels32();var b=baseImage.GetPixels32();var c=unclipped.GetPixels32();
     for(int i=0;i<a.Length;i++){report.occludedEnergy+=Mathf.Max(0,SkyLuma(a[i])-SkyLuma(b[i]));report.bypassEnergy+=Mathf.Max(0,SkyLuma(c[i])-SkyLuma(b[i]));}
     File.WriteAllBytes(folder+"/foreground-normal.png",covered.EncodeToPNG());File.WriteAllBytes(folder+"/foreground-bypass.png",unclipped.EncodeToPNG());

    }
    finally{if(covered!=null)UnityEngine.Object.Destroy(covered);if(baseImage!=null)UnityEngine.Object.Destroy(baseImage);if(unclipped!=null)UnityEngine.Object.Destroy(unclipped);}
    // Finish every view and foreground capture before reporting metric failures.
    var failures=new List<string>();
    if(!report.shots.Exists(x=>x.skyExpectedPixels>500))failures.Add("No meaningful stable sky/fire overlap (>500 pixels).");
    foreach(var data in report.shots)if(data.skyExpectedPixels>500){
     float visibility=data.skyVisiblePixels/(float)data.skyExpectedPixels,energy=data.skyNormalEnergy/data.skyBypassEnergy;
     if(!(visibility>.9f))failures.Add("Sky visibility shot "+data.shot+": "+visibility);
     if(!(energy>=.9f&&energy<=1.1f))failures.Add("Empty-depth radiance shot "+data.shot+": "+energy);}
    if(!(report.bypassEnergy>5f))failures.Add("Insufficient foreground bypass radiance.");
    if(!(report.occludedEnergy<report.bypassEnergy*.1f))failures.Add("Real foreground depth did not occlude fire.");
    Assert.That(failures,Is.Empty,string.Join("; ",failures));
   }
   finally
   {
    if(gas!=null){gas.SetDebugViewForQa(0);gas.SetRenderingLayerForQa(0);}
    cameraData.dithering=dithering;valley.AnimateClouds=animateClouds;valley.DebugMode=debug;camera.cullingMask=cameraMask;Time.timeScale=scale;binding.PlayerSession.Stop();lease?.Dispose();
    if(blocker!=null)UnityEngine.Object.Destroy(blocker);if(pose!=null)UnityEngine.Object.Destroy(pose);
    camera.transform.SetPositionAndRotation(oldPosition,oldRotation);resolution.Dispose();
    File.WriteAllText(folder+"/report.json",JsonUtility.ToJson(report,true));
   }
  }
  private static IEnumerator CaptureSkyFrame(Action<Texture2D> receive)
  {for(int i=0;i<3;i++)yield return new WaitForEndOfFrame();receive(ScreenCapture.CaptureScreenshotAsTexture());}
  private static float SkyLuma(Color32 c)=>(.2126f*c.r+.7152f*c.g+.0722f*c.b)/255f;
 }
}

