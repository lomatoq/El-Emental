using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Presentation.Fire;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(240000)]public IEnumerator ActualRingShowsDistributedCoolingAndSphereKeepsActorInteriorClear()
  {
   System.IDisposable safe=null;FireVisualCaptureCamera capture=null;float saved=Time.captureDeltaTime;
   string folder="BuildReports/HardPolish/G05/LivingFlame-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();safe=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    var effects=binding.GetComponent<FireAbilityEffects>();
    var flows=(FireFlowVolumeBackend[])typeof(FireAbilityEffects).GetField("flows",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(effects);
    int start=(int)typeof(FireAbilityEffects).GetField("WeaveStart",BindingFlags.NonPublic|BindingFlags.Static).GetRawConstantValue();
    int end=(int)typeof(FireAbilityEffects).GetField("LowFlightHandStart",BindingFlags.NonPublic|BindingFlags.Static).GetRawConstantValue();
    Vector3 up=flightMotor.LocalUp,side=Vector3.Cross(up,flightMotor.FacingForward).normalized,center=flightMotor.Body.position;
    var view=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
    capture=view.gameObject.AddComponent<FireVisualCaptureCamera>();capture.Place(center+side*12+up*7,center);
    Directory.CreateDirectory(folder);
    void Frame(string name){var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());}finally{Object.Destroy(image);}}
    Assert.That(flightAbility.TryRing(),Is.True);yield return new WaitForSeconds(.8f);Assert.That(flightAbility.ReleaseRing(),Is.True);
    int peakCooling=0,peakSmokeCards=0;double cpu=0;
    for(int frame=0;frame<13;frame++)
    {
     yield return new WaitForSeconds(.05f);yield return new WaitForEndOfFrame();
     int cooling=0;foreach(var flow in flows)for(int p=0;p<flow.Solver.Count;p++)if(!flow.Solver.Particles[p].Spark&&flow.Solver.Particles[p].Age>flow.Solver.Particles[p].HotLifetime)cooling++;
     int smokeCards=0;
     foreach(var renderer in effects.GetComponentsInChildren<MeshRenderer>())
      if(renderer.enabled&&renderer.name=="Transported flame and smoke atlas accents")smokeCards+=(int)renderer.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(1)/6;
     peakCooling=Mathf.Max(peakCooling,cooling);peakSmokeCards=Mathf.Max(peakSmokeCards,smokeCards);cpu=System.Math.Max(cpu,effects.LastStepMilliseconds);
     if(frame==3||frame==7||frame==11)Frame("ring-"+frame);
    }
    Assert.That(peakCooling,Is.GreaterThan(12));Assert.That(peakSmokeCards,Is.GreaterThan(12),"Cooling ring must submit distributed shared dust wisps.");
    flightAbility.SetWeaveHeld(true,FireWeaveForm.Sphere,1);capture.Place(center+side*6+up*2,center+up);
    yield return new WaitForSeconds(.9f);yield return new WaitForEndOfFrame();
    Assert.That(effects.SphereVisible,Is.True);int tongues=0;
    var sphere=(FireProtectionSphereRenderer)typeof(FireAbilityEffects).GetField("protectionSphere",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(effects);
    for(int i=start;i<end;i++)for(int p=0;p<flows[i].Solver.Count;p++)
    {
     var parcel=flows[i].Solver.Particles[p];tongues++;
     Assert.That(FireTongueEvolution.OutsideActor(parcel.Position,parcel.Radius,parcel.Aspect,sphere.Center,sphere.Radius),Is.True,"Cosmetic sphere detail intrudes into actor/head interior.");
    }
    Assert.That(tongues,Is.GreaterThan(15));Frame("sphere-clear-interior");
    File.WriteAllText(folder+"/measurements.txt",$"peakCooling={peakCooling}\npeakSmokeCards={peakSmokeCards}\npeakCpuMs={cpu}\nsphereDetailParcels={tongues}\n");
   }
   finally{safe?.Dispose();if(capture!=null)Object.Destroy(capture);Time.captureDeltaTime=saved;ReleaseFireAbilityFixture();}
  }
 }
}
