using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Presentation.Fire;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.Fire;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  private const BindingFlags NightBurnFields=BindingFlags.NonPublic|BindingFlags.Instance;
  private static Color[] NightBurnRender(Camera camera,RenderTexture target,Texture2D image,string path)
  {
   var previous=RenderTexture.active;bool savedSrgb=GL.sRGBWrite;RenderTexture display=null;
   try
   {
    var request=new RenderPipeline.StandardRequest{destination=target};
    Assert.That(RenderPipeline.SupportsRenderRequest(camera,request),Is.True);
    RenderPipeline.SubmitRenderRequest(camera,request);
    // URP uses the external descriptor for its internal color buffer. Preserve
    // HDR until ALL flame blending/postprocessing finishes, then convert once.
    display=RenderTexture.GetTemporary(target.width,target.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
    GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;Graphics.Blit(target,display);RenderTexture.active=display;
    image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply(false,false);
    if(path!=null)File.WriteAllBytes(path,image.EncodeToPNG());return image.GetPixels();
   }
   finally{GL.sRGBWrite=savedSrgb;RenderTexture.active=previous;if(display!=null)RenderTexture.ReleaseTemporary(display);}
  }
  private static int NightBurnChanged(Color[] a,Color[] b,float threshold)
  {
   int changed=0;for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>threshold)changed++;return changed;
  }
  private static string NightBurnOpaqueDelta(Camera view,int width,int height,int mask,Vector3 contact,Vector3 normal,Color[] on,Color[] off,bool flipY=false)
  {
   int count=0,warm=0;Vector3 rgb=Vector3.zero;float peak=0;
   for(int y=4;y<height;y+=4)for(int x=4;x<width;x+=4)
   {
    if(!Physics.Raycast(view.ViewportPointToRay(new Vector3((x+.5f)/width,(y+.5f)/height,0)),out var hit,20,mask,QueryTriggerInteraction.Ignore))continue;
    float distance=Vector3.Distance(hit.point,contact);
    if(distance<.65f||distance>2.2f||Vector3.Dot(hit.normal,(contact+normal*.35f-hit.point).normalized)<.12f)continue;
    int n=(flipY?height-1-y:y)*width+x;Vector3 d=new Vector3(on[n].r-off[n].r,on[n].g-off[n].g,on[n].b-off[n].b);
    count++;rgb+=d;peak=Mathf.Max(peak,d.x);if(d.x>.008f&&d.x>d.z*1.1f)warm++;
   }
   rgb/=Mathf.Max(1,count);return "count="+count+" warm="+warm+" meanRGB="+rgb.ToString("F7")+" peakR="+peak.ToString("F7")+" luminance="+Vector3.Dot(rgb,new Vector3(.2126f,.7152f,.0722f));
  }
  [UnityTest,Timeout(300000)]
  public IEnumerator SavedArenaNightBurnLampsWarmOpaqueReceiversWithVisibleFlamesAndSmoke()
  {
   CelestialSystemBehaviour sky=null;Camera view=null;FireVisualCaptureCamera pose=null;
   RenderTexture target=null;Texture2D image=null;IDisposable rival=null;
   float oldPhase=0,oldScale=Time.timeScale,oldStep=Time.captureDeltaTime,oldAspect=0;
   Vector3 oldPosition=default;Quaternion oldRotation=default;
   UniversalAdditionalCameraData cameraData=null;AntialiasingMode oldAa=default;
   var notes=new List<string>();string folder="BuildReports/HardPolish/G05/NightBurn-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();rival=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    sky=All<CelestialSystemBehaviour>().Single();oldPhase=sky.Snapshot.TimeOfDay01;
    sky.SetTimeOfDayForQa(.75f);sky.EvaluatePresentationForQa();Assert.That(sky.Snapshot.Night01,Is.GreaterThan(.99f));
    view=sky.TargetCamera;oldPosition=view.transform.position;oldRotation=view.transform.rotation;oldAspect=view.aspect;
    cameraData=view.GetUniversalAdditionalCameraData();oldAa=cameraData.antialiasing;
    // Pair renders share one simulation frame; disable only temporal jitter if authored.
    if(oldAa==AntialiasingMode.TemporalAntiAliasing)cameraData.antialiasing=AntialiasingMode.None;
    pose=view.gameObject.AddComponent<FireVisualCaptureCamera>();
    target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf);target.Create();
    image=new Texture2D(1280,720,TextureFormat.RGB24,false);view.aspect=1280f/720;
    Directory.CreateDirectory(folder);
    Assert.That(((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).maxAdditionalLightsCount,Is.EqualTo(8),"Four per-object lights exclude local burn lamps from the arena floor behind seven stronger column lights.");
    var response=binding.PlayerSession.GetComponent<FireWorldImpact>();
    var burning=binding.GetComponent<FireSmolderPresentation>();var lighting=binding.GetComponent<FireAbilityLighting>();
    Assert.That(response,Is.Not.Null);Assert.That(burning,Is.Not.Null);Assert.That(lighting,Is.Not.Null);
    var lamps=(Light[])typeof(FireAbilityLighting).GetField("lamps",NightBurnFields).GetValue(lighting);
    Vector3 up=flightMotor.LocalUp,forward=flightMotor.FacingForward,feet=flightMotor.SupportFeetPoint(up);
    Assert.That(Physics.Raycast(feet+forward*2.2f+up*2,-up,out var ground,5,flightMotor.GroundMask,QueryTriggerInteraction.Ignore),Is.True,"Actual arena ground receiver is required.");
    Collider stoneShape=null;RaycastHit stoneHit=default;float nearest=float.PositiveInfinity;
    foreach(var structure in All<EarthArenaStructure>())
    {
     if(structure.IsFractured)continue;
     var collider=(Collider)typeof(EarthArenaStructure).GetField("intactCollider",NightBurnFields).GetValue(structure);
     if(collider==null||!collider.enabled||collider==ground.collider)continue;
     Vector3 delta=collider.bounds.center-ground.point;float distance=delta.magnitude;
     if(distance>nearest||distance>12)continue;
     Vector3 origin=collider.bounds.center+up*(collider.bounds.extents.magnitude+2)+forward*2;
     if(collider.Raycast(new Ray(origin,(collider.bounds.center-origin).normalized),out var hit,collider.bounds.extents.magnitude*3+8))
     {stoneShape=collider;stoneHit=hit;nearest=distance;}
    }
    Assert.That(stoneShape,Is.Not.Null,"Need an actual authored stone structure near the same saved-arena ground.");
    var contacts=new[]{ground,stoneHit};
    for(int receiver=0;receiver<contacts.Length;receiver++)
    {
     var hit=contacts[receiver];string name=receiver==0?"ground":"stone";
     Assert.That(hit.collider.enabled,Is.True);
     // Thermal shock admits real ignition with tiny mechanical dose, preserving the receiver for lighting QA.
     for(int n=0;n<6;n++)
     {
      Assert.That(response.ApplyContact(hit.collider,hit.point,hit.normal,-hit.normal,.005f,1,n==0),Is.True);
      yield return new WaitForFixedUpdate();
     }
     Vector3 side=Vector3.Cross(up,forward).normalized;
     Vector3 eye=hit.point+hit.normal*3.1f+up*1.7f-forward*2+side*.7f;
     pose.Place(eye,hit.point+up*.3f);yield return new WaitForSeconds(.6f);yield return new WaitForEndOfFrame();
     Assert.That(burning.ActiveIgnitions,Is.GreaterThan(0));Assert.That(burning.SurfaceSamples,Is.GreaterThan(2));
     Assert.That(burning.IgnitionParticles,Is.GreaterThan(8));Assert.That(lighting.ActiveBurnLights,Is.InRange(1,4));
     for(int i=8;i<12;i++)if(lamps[i].enabled)
     {
      Assert.That(lamps[i].range,Is.EqualTo(4.5f).Within(.001f));
      Assert.That(lamps[i].intensity,Is.InRange(.01f,.781f),"Burn radiance must retain .65 gain with at most 20% authored pulse.");
     }
     // Actual game backbuffer reference from the completed frame, before any
     // external-target request changes the camera's render destination.
     var mainScreen=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(folder+"/"+name+"-main-screen.png",mainScreen.EncodeToPNG());}finally{Object.Destroy(mainScreen);}
     float snapshotTime=Time.time;var cameraMatrix=view.transform.localToWorldMatrix;
     var on=NightBurnRender(view,target,image,folder+"/"+name+"-lights-on.png");
     var ldrControl=new RenderTexture(target.width,target.height,24,RenderTextureFormat.ARGB32);
     try{NightBurnRender(view,ldrControl,image,folder+"/"+name+"-ldr-internal-control.png");}finally{ldrControl.Release();Object.Destroy(ldrControl);}
     notes.Add("capture HDR="+view.allowHDR+" target="+target.graphicsFormat+" display=sRGB8; URP external descriptor preserves internalHDR only for HDR target");
     var flameMaterial=(Material)typeof(FireSmolderPresentation).GetField("flameMaterial",NightBurnFields).GetValue(burning);
     if(flameMaterial.HasProperty("_BurnPalette"))
     {
      float palette=flameMaterial.GetFloat("_BurnPalette");
      try{flameMaterial.SetFloat("_BurnPalette",0);NightBurnRender(view,target,image,folder+"/"+name+"-legacy-palette.png");}
      finally{flameMaterial.SetFloat("_BurnPalette",palette);}
     }
     bool post=cameraData.renderPostProcessing;
     try{cameraData.renderPostProcessing=false;NightBurnRender(view,target,image,folder+"/"+name+"-no-post.png");}
     finally{cameraData.renderPostProcessing=post;}
     notes.Add(name+" receiver="+hit.collider.name+" contact="+hit.point.ToString("F5")+" normal="+hit.normal.ToString("F5")+" viewport="+view.WorldToViewportPoint(hit.point).ToString("F5"));
     for(int i=8;i<12;i++)if(lamps[i].enabled)notes.Add("lamp "+i+" position="+lamps[i].transform.position.ToString("F5")+" contactDistance="+Vector3.Distance(lamps[i].transform.position,hit.point)+" intensity="+lamps[i].intensity+" color="+lamps[i].color+" layer="+lamps[i].renderingLayerMask);
     var nearbyLights=All<Light>().Where(l=>l.enabled&&l.gameObject.activeInHierarchy).OrderBy(l=>Vector3.Distance(l.transform.position,hit.point)).Take(16);
     foreach(var lamp in nearbyLights)notes.Add("scene lamp "+lamp.name+" type="+lamp.type+" pos="+lamp.transform.position.ToString("F3")+" intensity="+lamp.intensity+" range="+lamp.range);

     var enabled=new bool[4];Color[] off;
     try
     {
      for(int i=0;i<4;i++){enabled[i]=lamps[8+i].enabled;lamps[8+i].enabled=false;}
      off=NightBurnRender(view,target,image,folder+"/"+name+"-lights-off.png");
     }
     finally{for(int i=0;i<4;i++)lamps[8+i].enabled=enabled[i];}
     Assert.That(Time.time,Is.EqualTo(snapshotTime));Assert.That(view.transform.localToWorldMatrix,Is.EqualTo(cameraMatrix));
     notes.Add("authored "+NightBurnOpaqueDelta(view,target.width,target.height,flightMotor.GroundMask,hit.point,hit.normal,on,off));
     notes.Add("Y-flip diagnostic "+NightBurnOpaqueDelta(view,target.width,target.height,flightMotor.GroundMask,hit.point,hit.normal,on,off,true));
     var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;int lightLimit=pipeline.maxAdditionalLightsCount;
     notes.Add("pipeline="+pipeline.name+" perObjectLightLimit="+lightLimit);
     if(lightLimit<8)
     {
      try
      {
       pipeline.maxAdditionalLightsCount=8;
       var expandedOn=NightBurnRender(view,target,image,folder+"/"+name+"-diagnostic-eight-lights-on.png");
       try
       {
        for(int i=0;i<4;i++)lamps[8+i].enabled=false;
        var expandedOff=NightBurnRender(view,target,image,folder+"/"+name+"-diagnostic-eight-lights-off.png");
        notes.Add("eight-light control "+NightBurnOpaqueDelta(view,target.width,target.height,flightMotor.GroundMask,hit.point,hit.normal,expandedOn,expandedOff));
       }
       finally{for(int i=0;i<4;i++)lamps[8+i].enabled=enabled[i];}
      }
      finally{pipeline.maxAdditionalLightsCount=lightLimit;}
     }
     File.WriteAllLines(folder+"/measurements.txt",notes);
     int samples=0,warmPixels=0,clipped=0;Vector3 deltaSum=Vector3.zero;float luminanceDelta=0;
     for(int y=4;y<target.height;y+=4)for(int x=4;x<target.width;x+=4)
     {
      Ray ray=view.ViewportPointToRay(new Vector3((x+.5f)/target.width,(y+.5f)/target.height,0));
      if(!Physics.Raycast(ray,out var visible,20,flightMotor.GroundMask,QueryTriggerInteraction.Ignore))continue;
      float distance=Vector3.Distance(visible.point,hit.point);
      if(distance<.65f||distance>2.2f||Vector3.Dot(visible.normal,(hit.point+hit.normal*.35f-visible.point).normalized)<.12f)continue;
      int index=y*target.width+x;Vector3 delta=new Vector3(on[index].r-off[index].r,on[index].g-off[index].g,on[index].b-off[index].b);
      deltaSum+=delta;luminanceDelta+=delta.x*.2126f+delta.y*.7152f+delta.z*.0722f;samples++;
      if(delta.x>.008f&&delta.x>delta.z*1.1f)warmPixels++;
      if(on[index].r>.985f&&on[index].g>.985f&&on[index].b>.985f)clipped++;
     }
     notes.Add(name+": samples="+samples+" warmPixels="+warmPixels+" meanRGBDelta="+(deltaSum/Mathf.Max(1,samples))+" meanLuminanceDelta="+(luminanceDelta/Mathf.Max(1,samples))+" clipped="+clipped+" ignitions="+burning.ActiveIgnitions+" cards="+burning.IgnitionParticles);
     File.WriteAllLines(folder+"/measurements.txt",notes);

     var groups=(Array)typeof(FireSmolderPresentation).GetField("flames",NightBurnFields).GetValue(burning);
     var meshes=new List<Mesh>();var ranges=new List<SubMeshDescriptor[]>();
     foreach(var group in groups)
     {
      if(!(bool)group.GetType().GetProperty("Visible").GetValue(group))continue;
      var mesh=(Mesh)group.GetType().GetField("mesh",NightBurnFields).GetValue(group);meshes.Add(mesh);
      ranges.Add(new[]{mesh.GetSubMesh(0),mesh.GetSubMesh(1)});
     }
     for(int hidden=0;hidden<2;hidden++)
     {
      Color[] absent;
      try
      {
       for(int i=0;i<meshes.Count;i++){var descriptors=(SubMeshDescriptor[])ranges[i].Clone();descriptors[hidden]=new SubMeshDescriptor(descriptors[hidden].indexStart,0);meshes[i].SetSubMeshes(descriptors,0,2,MeshUpdateFlags.DontRecalculateBounds);}
       absent=NightBurnRender(view,target,image,folder+"/"+name+(hidden==0?"-no-flame.png":"-no-smoke.png"));
      }
      finally{for(int i=0;i<meshes.Count;i++)meshes[i].SetSubMeshes(ranges[i],0,2,MeshUpdateFlags.DontRecalculateBounds);}
      int changed=NightBurnChanged(on,absent,.012f);notes.Add(name+(hidden==0?" flamePixels=":" smokePixels=")+changed);
      File.WriteAllLines(folder+"/measurements.txt",notes);
      Assert.That(changed,Is.GreaterThan(hidden==0?80:15),"Already emitted "+(hidden==0?"flame":"smoke")+" must contribute visible pixels at night.");
     }
     Assert.That(Time.time,Is.EqualTo(snapshotTime),"Every A/B must use identical simulation/particle age.");
     Assert.That(samples,Is.GreaterThan(30),"Need visible opaque receiver pixels beyond the flame itself.");
     Assert.That(warmPixels,Is.GreaterThan(20),"Pooled burn lamps must visibly warm nearby opaque surfaces.");
     Assert.That(luminanceDelta/samples,Is.GreaterThan(.001f));Assert.That(deltaSum.x,Is.GreaterThan(deltaSum.z*1.1f));
     Assert.That(clipped/(float)samples,Is.LessThan(.02f),"Environment must retain detail rather than becoming a white bloom patch.");
    }
   }
   finally
   {
    Time.timeScale=oldScale;Time.captureDeltaTime=oldStep;
    if(sky!=null){sky.SetTimeOfDayForQa(oldPhase);sky.EvaluatePresentationForQa();}
    if(cameraData!=null)cameraData.antialiasing=oldAa;
    if(pose!=null)Object.Destroy(pose);if(view!=null){view.aspect=oldAspect;view.transform.SetPositionAndRotation(oldPosition,oldRotation);}
    if(target!=null){target.Release();Object.Destroy(target);}if(image!=null)Object.Destroy(image);rival?.Dispose();ReleaseFireAbilityFixture();
   }
  }
 }
}
