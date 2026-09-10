using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(180000)] public IEnumerator ActualLateRingMediumSameFrameComparison()
  {
   IDisposable safe=null;FireVisualCaptureCamera capture=null;float saved=Time.captureDeltaTime;
   Material[] medium=null;Texture2D candidate=null,legacy=null,without=null;
   string folder="BuildReports/HardPolish/G05/RingMedium-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();safe=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    var camera=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
    capture=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
    capture.Place(flightMotor.Body.position-flightMotor.FacingForward*10+flightMotor.LocalUp*8,flightMotor.Body.position);
    var effects=binding.GetComponent<FireAbilityEffects>();const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
    var flows=(FireFlowVolumeBackend[])typeof(FireAbilityEffects).GetField("flows",flags).GetValue(effects);
    medium=new Material[24];for(int i=0;i<24;i++)medium[i]=(Material)typeof(FireFlowVolumeBackend).GetField("smokeMaterial",flags).GetValue(flows[i+10]);
    Assert.That(flightAbility.TryRing(),Is.True);yield return new WaitForSeconds(.8f);flightAbility.ReleaseRing();
    yield return new WaitForSeconds(.62f);yield return new WaitForEndOfFrame();
    Assert.That(effects.AliveParticles,Is.GreaterThan(150),"Compare a developed real cooling ring, not an empty frame.");
    foreach(var m in medium){Assert.That(m.GetFloat("_MediumAuthoredShape"),Is.GreaterThan(.5f));m.SetFloat("_RingMediumRefinement",1);m.SetFloat("_RingMediumVisibility",1);}
    // All three HDR renders run without a yielded frame, hence identical gas,
    // atlas phase, camera, lighting, opaque background, and exposure inputs.
    candidate=RenderFireDofSameFrame(camera);
    foreach(var m in medium)m.SetFloat("_RingMediumRefinement",0);
    legacy=RenderFireDofSameFrame(camera);
    foreach(var m in medium)m.SetFloat("_RingMediumVisibility",0);
    without=RenderFireDofSameFrame(camera);
    File.WriteAllBytes(Path.Combine(folder,"candidate.png"),candidate.EncodeToPNG());
    File.WriteAllBytes(Path.Combine(folder,"legacy-medium.png"),legacy.EncodeToPNG());
    File.WriteAllBytes(Path.Combine(folder,"medium-disabled.png"),without.EncodeToPNG());
    var a=candidate.GetPixels32();var b=legacy.GetPixels32();var c=without.GetPixels32();
    double changed=0,mediumContribution=0;int warm=0,oldWarm=0,red=0,oldRed=0;
    for(int i=0;i<a.Length;i++)
    {
     changed+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);
     mediumContribution+=Math.Abs(b[i].r-c[i].r)+Math.Abs(b[i].g-c[i].g)+Math.Abs(b[i].b-c[i].b);
     if(a[i].r>180&&a[i].g>95&&a[i].r>a[i].b*1.4f)warm++;
     if(b[i].r>180&&b[i].g>95&&b[i].r>b[i].b*1.4f)oldWarm++;
     if(a[i].r>150&&a[i].g<a[i].r*.42f&&a[i].b<a[i].r*.2f)red++;
     if(b[i].r>150&&b[i].g<b[i].r*.42f&&b[i].b<b[i].r*.2f)oldRed++;
    }
    changed/=a.Length*765.0;mediumContribution/=a.Length*765.0;
    File.WriteAllText(Path.Combine(folder,"metrics.txt"),"candidateDifference="+changed+"\nlegacyMediumContribution="+mediumContribution+"\nwarmPixels="+warm+"\nlegacyWarmPixels="+oldWarm+"\nredPixels="+red+"\nlegacyRedPixels="+oldRed+"\nalive="+effects.AliveParticles+"\nvalidRibbonSpans="+effects.RingRibbonSpans);
    Assert.That(mediumContribution,Is.GreaterThan(.00001),"The isolated medium must measurably contribute to the actual late ring.");
    Assert.That(changed,Is.GreaterThan(.00001),"The candidate must change the actual medium rather than a dormant material.");
    Assert.That(warm,Is.GreaterThan(oldWarm*.65f),"Preserve the broad warm fire band while reducing red extinction lobes.");
   }
   finally
   {
    if(medium!=null)foreach(var m in medium)if(m!=null){m.SetFloat("_RingMediumRefinement",1);m.SetFloat("_RingMediumVisibility",1);}
    if(candidate!=null)UnityEngine.Object.Destroy(candidate);if(legacy!=null)UnityEngine.Object.Destroy(legacy);if(without!=null)UnityEngine.Object.Destroy(without);
    safe?.Dispose();Time.captureDeltaTime=saved;if(capture!=null)UnityEngine.Object.Destroy(capture);ReleaseFireAbilityFixture();
   }
  }
 }
}
