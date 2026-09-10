using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Simulation.Fire;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(240000)]public IEnumerator ActualWheelMorphDrawFadeAndSphericalWaveReviewCorpus()
  {
   float saved=Time.captureDeltaTime;FireVisualCaptureCamera capture=null;System.IDisposable isolation=null;
   string folder="BuildReports/HardPolish/G05/FireReview-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();isolation=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    var view=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
    capture=view.gameObject.AddComponent<FireVisualCaptureCamera>();Directory.CreateDirectory(folder);
    Vector3 up=flightMotor.LocalUp,forward=flightMotor.FacingForward,side=Vector3.Cross(up,forward).normalized;
    Vector3 center=flightAbility.OwnerRoot.position+up,aim=center+up*20+forward*30;
    capture.Place(center+side*8+up*2-forward*2,center+forward*1.6f);
    void Save(string label)
    {
     var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(folder+"/"+label+".png",image.EncodeToPNG());}finally{Object.Destroy(image);}
     File.WriteAllText(folder+"/"+label+".json",JsonUtility.ToJson(ReadFireEffectsFrame(),true));
    }
    foreach(int level in new[]{0,4,5,6,7,8,9,10,12})
    {
     float power=level/12f;var form=level<3?FireWeaveForm.Jet:level<7?FireWeaveForm.Flood:level<10?FireWeaveForm.Orbit:FireWeaveForm.Sphere;
     flightAbility.SetChargedAim(aim);flightAbility.SetWeaveHeld(true,form,power);
     if(level<7){binding.PlayerSession.SetPower(FireWeaveTuning.StreamPower(power));if(!binding.PlayerSession.IsActive)Assert.That(binding.PlayerSession.TryBegin(aim),Is.True);else binding.PlayerSession.SetAim(aim);}
     else binding.PlayerSession.Stop();
     yield return new WaitForSeconds(.22f);yield return new WaitForEndOfFrame();Save("wheel-"+level.ToString("D2")+"-transition");
     yield return new WaitForSeconds(.22f);yield return new WaitForEndOfFrame();Save("wheel-"+level.ToString("D2")+"-settled");
    }
    flightAbility.SetWeaveHeld(false,FireWeaveForm.Jet,0);binding.PlayerSession.Stop();yield return new WaitForSeconds(.8f);
    Vector3 origin=flightMotor.SupportFeetPoint(up)+side*1.2f;
    capture.Place(center+side*6+up*5-forward*4,origin+forward);flightAbility.BeginContour();int drawn=0;
    for(int i=0;i<5;i++){if(flightAbility.TraceContour(origin+forward*i*.68f+side*Mathf.Sin(i*.7f)*.2f))drawn++;yield return new WaitForSeconds(.08f);}
    Assert.That(drawn,Is.GreaterThanOrEqualTo(2));yield return new WaitForEndOfFrame();Save("draw-fresh");
    Assert.That(ReadFireEffectsFrame().AliveParticles,Is.GreaterThan(8),"Live contour sources must actually emit visible flame gas, not just retain runtime anchors.");
    yield return new WaitForSeconds(.7f);yield return new WaitForEndOfFrame();Save("draw-old-tail-cooling");
    yield return new WaitForSeconds(.65f);yield return new WaitForEndOfFrame();Save("draw-expired");Assert.That(flightAbility.SourceCount,Is.Zero);
    capture.Place(center+side*11+up*6-forward*3,center);flightAbility.SetWeaveHeld(true,FireWeaveForm.Sphere,1);
    yield return new WaitForSeconds(.45f);yield return new WaitForEndOfFrame();Save("sphere-before-wave");
    Assert.That(flightAbility.TryReleaseSphereWave(),Is.True);
    for(int i=0;i<7;i++){yield return new WaitForSeconds(.10f);yield return new WaitForEndOfFrame();Save("sphere-wave-"+i.ToString("D2"));}
    yield return new WaitForSeconds(.6f);yield return new WaitForEndOfFrame();Save("sphere-wave-cooling");
    Assert.That(flightAbility.SphereWaveActive,Is.False);
   }
   finally{if(capture!=null)Object.Destroy(capture);isolation?.Dispose();Time.captureDeltaTime=saved;binding?.PlayerSession?.Stop();binding?.PlayerSession?.SetPower(1);ReleaseFireAbilityFixture();}
  }
 }
}
