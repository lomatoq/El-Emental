using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
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
  [UnityTest,Timeout(240000)] public IEnumerator SavedProductionDustHaloSeparatesLightingBloomAndRendererSources()
  {
   yield return ReadyFireAbilities();
   string folder="BuildReports/HardPolish/G05/DustHalo-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
   var notes=new List<string>();var sky=All<CelestialSystemBehaviour>().Single();var view=sky.TargetCamera;
   float phase=sky.Snapshot.TimeOfDay01,scale=Time.timeScale,aspect=view.aspect;
   var cameraData=view.GetUniversalAdditionalCameraData();bool post=cameraData.renderPostProcessing;var aa=cameraData.antialiasing;bool dither=cameraData.dithering;cameraData.antialiasing=AntialiasingMode.None;cameraData.dithering=false;
   float legacyRadiance=Shader.GetGlobalFloat("_ElementalDustLegacyRadiance");Shader.SetGlobalFloat("_ElementalDustLegacyRadiance",0);
   var pose=view.gameObject.AddComponent<FireVisualCaptureCamera>();Vector3 position=view.transform.position;Quaternion rotation=view.transform.rotation;
   var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf);target.Create();var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
   var dust=All<Renderer>().Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name.Contains("Dust")).ToArray();
   var enabled=dust.Select(r=>r.enabled).ToArray();
   var lights=All<Light>().Where(l=>l.isActiveAndEnabled&&l.type==LightType.Point).ToArray();
   var blooms=All<Volume>().Select(v=>v.HasInstantiatedProfile()?v.profile:v.sharedProfile).Where(p=>p!=null).SelectMany(p=>p.components).OfType<Bloom>().Distinct().ToArray();
   var bloomActive=blooms.Select(b=>b.active).ToArray();
   var dofs=All<EarthCinematicDepthOfFieldController>();var dofWeights=dofs.Select(d=>d.PresentationWeight).ToArray();
   try
   {
    sky.SetTimeOfDayForQa(.25f);sky.EvaluatePresentationForQa();view.aspect=1280f/720;
    Vector3 up=flightMotor.LocalUp;
    foreach(var source in lights)notes.Add("available point="+source.name+" intensity="+source.intensity+" position="+source.transform.position);
    var lamp=lights.Where(l=>l.name.StartsWith("Column Flame ")).OrderBy(l=>Vector3.Distance(l.transform.position,flightMotor.transform.position)).FirstOrDefault();
    Assert.That(lamp,Is.Not.Null,"Need an active authored column light; daytime intensity is deliberately only .3.");
    Vector3 toward=Vector3.ProjectOnPlane(flightMotor.transform.position-lamp.transform.position,up).normalized;
    Vector3 point=lamp.transform.position+toward*.8f-up*.7f;
    pose.Place(point+toward*7+up*.4f,point);
    var hub=All<EarthMaterialFeedbackHub>().First(h=>h.isActiveAndEnabled);
    // The actual production feedback layers, beside an existing authored torch:
    // no cloned material, synthetic particle card or extra diagnostic light.
    hub.Emit(EarthMaterialFeedbackKind.Impact,point,up,2,1.2f,dustCount:48,chipCount:0);
    yield return new WaitForSeconds(.18f);yield return new WaitForEndOfFrame();Time.timeScale=0;
    notes.Add("torch="+lamp.name+" intensity="+lamp.intensity+" color="+lamp.color+" range="+lamp.range+" at="+lamp.transform.position);
    var native=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(folder+"/00-native-screen.png",native.EncodeToPNG());}finally{Object.Destroy(native);}
    float time=Time.time;Matrix4x4 matrix=view.transform.localToWorldMatrix;
    var normal=NightBurnRender(view,target,image,folder+"/01-all.png");
    var stable=NightBurnRender(view,target,image,folder+"/01-all-repeat-control.png");
    float repeatDelta=0;for(int n=0;n<normal.Length;n++)repeatDelta+=Mathf.Abs(normal[n].r-stable[n].r)+Mathf.Abs(normal[n].g-stable[n].g)+Mathf.Abs(normal[n].b-stable[n].b);
    repeatDelta/=normal.Length;notes.Add("same-frame repeat mean="+repeatDelta);
    Assert.That(repeatDelta,Is.LessThan(.001f),"The on/on control must be stable before attributing lighting differences.");
    Shader.SetGlobalFloat("_ElementalDustLegacyRadiance",1);
    var legacy=NightBurnRender(view,target,image,folder+"/01-legacy-unbounded.png");
    Shader.SetGlobalFloat("_ElementalDustLegacyRadiance",0);
    for(int i=0;i<dust.Length;i++)
    {
     var r=dust[i];var ps=r.GetComponent<ParticleSystem>();var m=r.sharedMaterial;
     notes.Add("renderer="+r.name+" active="+r.gameObject.activeInHierarchy+" enabled="+r.enabled+" particles="+(ps!=null?ps.particleCount:-1)+" shader="+m.shader.name+" material="+m.name+" brightness="+(m.HasProperty("_Brightness")?m.GetFloat("_Brightness"):0)+" tint="+(m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):Color.clear)+" bounds="+r.bounds);
     r.enabled=false;
    }
    var absent=NightBurnRender(view,target,image,folder+"/02-no-dust.png");
    float oldAdded=0,newAdded=0;int contributionPixels=0;
    for(int n=0;n<normal.Length;n++)
    {
     float oldLight=(legacy[n].r-absent[n].r)*.2126f+(legacy[n].g-absent[n].g)*.7152f+(legacy[n].b-absent[n].b)*.0722f;
     if(oldLight<.015f)continue;
     contributionPixels++;oldAdded+=oldLight;
     newAdded+=Mathf.Max(0,(normal[n].r-absent[n].r)*.2126f+(normal[n].g-absent[n].g)*.7152f+(normal[n].b-absent[n].b)*.0722f);
    }
    notes.Add("positive dust radiance old="+oldAdded+" bounded="+newAdded+" ratio="+(newAdded/Mathf.Max(.001f,oldAdded))+" pixels="+contributionPixels);
    Assert.That(contributionPixels,Is.GreaterThan(150),"Legacy halo must actually be reproduced.");
    Assert.That(newAdded,Is.LessThan(oldAdded*.65f),"Non-emissive dust must stop reproducing the legacy radiance halo.");
    for(int i=0;i<dust.Length;i++)dust[i].enabled=enabled[i];
    foreach(var l in lights)l.enabled=false;
    var noPoint=NightBurnRender(view,target,image,folder+"/03-no-point-lights.png");
    foreach(var l in lights)l.enabled=true;
    foreach(var b in blooms)b.active=false;
    var noBloom=NightBurnRender(view,target,image,folder+"/04-no-bloom.png");
    for(int i=0;i<blooms.Length;i++)blooms[i].active=bloomActive[i];
    foreach(var d in dofs)d.SetPresentationWeight(0);
    NightBurnRender(view,target,image,folder+"/05-no-cinematic-dof.png");
    for(int i=0;i<dofs.Length;i++)dofs[i].SetPresentationWeight(dofWeights[i]);
    cameraData.renderPostProcessing=false;
    NightBurnRender(view,target,image,folder+"/06-no-post.png");
    var raw=new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;
    try
    {
     RenderTexture.active=target;raw.ReadPixels(new Rect(0,0,target.width,target.height),0,0);raw.Apply();
     var pixels=raw.GetPixels();int aboveOne=0,aboveFour=0,dustPixels=0,greenPixels=0;float peak=0;
     for(int n=0;n<pixels.Length;n++)
     {
      if(Mathf.Abs(normal[n].r-absent[n].r)+Mathf.Abs(normal[n].g-absent[n].g)+Mathf.Abs(normal[n].b-absent[n].b)<.015f)continue;
      dustPixels++;var c=pixels[n];float largest=Mathf.Max(c.r,c.g,c.b);peak=Mathf.Max(peak,largest);if(largest>1.12f)aboveOne++;if(largest>4)aboveFour++;
      if(normal[n].g>normal[n].r+.02f&&normal[n].g>normal[n].b+.02f)greenPixels++;
     }
     notes.Add("RAW HDR within actual dust footprint: peak="+peak+" pixels="+dustPixels+" pixelsAboveBloomThreshold="+aboveOne+" pixelsAbove4="+aboveFour+" postGreenPixels="+greenPixels);
    }
    finally{RenderTexture.active=previous;Object.Destroy(raw);cameraData.renderPostProcessing=post;}
    int isolated=0;
    for(int i=0;i<dust.Length&&isolated<10;i++)
    {
     var r=dust[i];var ps=r.GetComponent<ParticleSystem>();
     if(!r.gameObject.activeInHierarchy||!enabled[i]||(ps!=null&&ps.particleCount==0))continue;
     r.enabled=false;
     var without=NightBurnRender(view,target,image,folder+"/source-off-"+isolated+".png");r.enabled=enabled[i];
     notes.Add("source-off-"+isolated+" renderer="+r.name+" changed="+NightBurnChanged(normal,without,.015f));isolated++;
    }
    notes.Add("dustChanged="+NightBurnChanged(normal,absent,.015f)+" pointLightChanged="+NightBurnChanged(normal,noPoint,.015f)+" bloomChanged="+NightBurnChanged(normal,noBloom,.015f));
    Assert.That(Time.time,Is.EqualTo(time));Assert.That(view.transform.localToWorldMatrix,Is.EqualTo(matrix));
    Assert.That(NightBurnChanged(normal,absent,.015f),Is.GreaterThan(150),"Real production dust must visibly contribute to the diagnostic.");
   }
   finally
   {
    File.WriteAllLines(folder+"/sources.txt",notes);
    for(int i=0;i<dust.Length;i++)if(dust[i]!=null)dust[i].enabled=enabled[i];
    foreach(var l in lights)if(l!=null)l.enabled=true;
    for(int i=0;i<blooms.Length;i++)blooms[i].active=bloomActive[i];
    for(int i=0;i<dofs.Length;i++)dofs[i].SetPresentationWeight(dofWeights[i]);
    cameraData.renderPostProcessing=post;cameraData.antialiasing=aa;cameraData.dithering=dither;Shader.SetGlobalFloat("_ElementalDustLegacyRadiance",legacyRadiance);Time.timeScale=scale;sky.SetTimeOfDayForQa(phase);sky.EvaluatePresentationForQa();
    Object.DestroyImmediate(pose);view.aspect=aspect;view.transform.SetPositionAndRotation(position,rotation);
    target.Release();Object.Destroy(target);Object.Destroy(image);ReleaseFireAbilityFixture();
   }
  }
 }
}
