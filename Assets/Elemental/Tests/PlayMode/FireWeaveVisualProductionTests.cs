using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Elemental.Simulation.Fire;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [System.Serializable]private sealed class WeaveVisualSample
  {public string label;public int particles,queries,stops,blocked,weaveEmitters,sourceEmitters;public double cpuMilliseconds;}
  private sealed class FireVisualRivalIsolation:System.IDisposable
  {
   private readonly Elemental.Runtime.Characters.PlanetMotor motor;private readonly Rigidbody body;
   private readonly Vector3 position,velocity,angular;private readonly Quaternion rotation;private readonly bool enabled,kinematic;
   public FireVisualRivalIsolation(Transform rival,Elemental.Runtime.Characters.PlanetMotor player)
   {
    motor=rival.GetComponent<Elemental.Runtime.Characters.PlanetMotor>();Assert.That(motor,Is.Not.Null);body=motor.Body;
    position=body.position;rotation=body.rotation;velocity=body.linearVelocity;angular=body.angularVelocity;enabled=motor.enabled;kinematic=body.isKinematic;
    motor.enabled=false;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;
    Vector3 side=Vector3.Cross(player.LocalUp,player.FacingForward).normalized;
    body.position=player.Body.position+player.LocalUp*20+side*20;Physics.SyncTransforms();
   }
   public void Dispose()
   {
    if(body==null)return;body.position=position;body.rotation=rotation;body.isKinematic=kinematic;
    if(!kinematic){body.linearVelocity=velocity;body.angularVelocity=angular;}if(motor!=null)motor.enabled=enabled;Physics.SyncTransforms();
   }
  }
  [System.Serializable]private sealed class WeaveVisualSamples{public List<WeaveVisualSample> frames=new();}
  [UnityTest,Timeout(240000)]public IEnumerator ActualWeaveOrbitSphereChargedBoltAndDrillMotionCorpus()
  {
   System.IDisposable safeRival=null;FireVisualCaptureCamera capture=null;float saved=Time.captureDeltaTime;var samples=new WeaveVisualSamples();
   string folder="BuildReports/HardPolish/G05/WeaveMotion-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();safeRival=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    var view=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
    capture=view.gameObject.AddComponent<FireVisualCaptureCamera>();
    var effects=binding.GetComponent<FireAbilityEffects>();Assert.That(effects,Is.Not.Null);
    Vector3 up=flightMotor.LocalUp,side=Vector3.Cross(up,flightMotor.FacingForward).normalized;
    Vector3 aim=flightMotor.Body.position+flightMotor.FacingForward*40+up*14;
    Directory.CreateDirectory(folder);
    void Frame(string label)
    {
     samples.frames.Add(new WeaveVisualSample{label=label,particles=effects.AliveParticles,queries=effects.QueryCount,stops=effects.BudgetStops,
      blocked=effects.BlockedParcels,weaveEmitters=effects.WeaveEmitters,sourceEmitters=effects.PersistentSourceEmitters,cpuMilliseconds=effects.LastStepMilliseconds});
     var texture=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(folder+"/"+label+".png",texture.EncodeToPNG());}finally{Object.Destroy(texture);}
    }
    foreach(float power in new[]{.38f,2.5f})
    {
     binding.PlayerSession.SetPower(power);Assert.That(binding.PlayerSession.TryBegin(aim),Is.True);
     Vector3 direction=(aim-binding.PlayerSession.MuzzlePosition).normalized;
     Vector3 center=binding.PlayerSession.MuzzlePosition+direction*4;
     capture.Place(center+side*8+up*2,center);
     yield return new WaitForSeconds(.45f);
     for(int frame=0;frame<2;frame++){yield return new WaitForSeconds(.12f);yield return new WaitForEndOfFrame();Frame((power<1?"low-stream-":"high-stream-")+frame);}
     binding.PlayerSession.Stop();yield return new WaitForSeconds(.85f);
    }
    binding.PlayerSession.SetPower(1);
    foreach(var form in new[]{FireWeaveForm.Orbit,FireWeaveForm.Sphere})
    {
     flightAbility.SetWeaveHeld(true,form,form==FireWeaveForm.Orbit?8f/12:1);
     capture.Place(flightMotor.Body.position+side*7+up*3,flightMotor.Body.position+up);
     yield return new WaitForSeconds(.4f);
     for(int frame=0;frame<3;frame++){yield return new WaitForSeconds(.12f);yield return new WaitForEndOfFrame();Frame(form+"-"+frame);Assert.That(effects.WeaveEmitters,Is.EqualTo(16));}
    }
    int before=effects.AliveParticles;Assert.That(flightAbility.TryDrill(aim),Is.True);
    yield return null;yield return new WaitForEndOfFrame();Frame("drill-field-transfer");
    Assert.That(effects.AliveParticles,Is.LessThan(before*.65f),"Drill must visibly empty its orbit instead of spawning an unrelated bolt.");
    yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();Frame("drill-field-rebuilt");Assert.That(effects.WeaveEmitters,Is.EqualTo(16));
    flightAbility.SetWeaveHeld(false,FireWeaveForm.Jet,1);yield return new WaitForSeconds(.65f);
    Assert.That(flightAbility.BeginBoltCharge(aim),Is.True);
    yield return new WaitForSeconds(1.3f);yield return new WaitForEndOfFrame();Frame("held-charged-bolt");
    Assert.That(flightAbility.IsBoltCharging,Is.True);Assert.That(flightAbility.ReleaseBoltCharge(),Is.True);
    capture.Place(flightMotor.Body.position+side*9+up*3,flightMotor.Body.position+flightMotor.FacingForward*4+up*2);
    for(int frame=0;frame<4;frame++){yield return new WaitForSeconds(.08f);yield return new WaitForEndOfFrame();Frame("charged-bolt-motion-"+frame);}
   }
   finally
   {
    Directory.CreateDirectory(folder);File.WriteAllText(folder+"/frames.json",JsonUtility.ToJson(samples,true));
    safeRival?.Dispose();binding?.PlayerSession?.Stop();binding?.PlayerSession?.SetPower(1);if(capture!=null)Object.Destroy(capture);Time.captureDeltaTime=saved;ReleaseFireAbilityFixture();
   }
  }
 }
}
