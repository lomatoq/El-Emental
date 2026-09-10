using System;

using System.Collections;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using Elemental.Input.Gestures;

using Elemental.Presentation.Animation;

using Elemental.Runtime.Characters;

using Elemental.Runtime.Fire;

using Elemental.Simulation.Characters;

using Elemental.Simulation.Combat;

using Elemental.Simulation.Magic;

using NUnit.Framework;

using UnityEngine;

using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode {

 public sealed partial class HardPolishFireStreamBindingRuntimeTests {

  private FireAbilityController flightAbility;private PlanetMotor flightMotor;private HumanoidCharacterPresentation flightActor;private DirectFireInputLease flightLease;

  [Serializable] private sealed class FlightFrame {public float time,altitude,speed,charge;public string pose;public bool ceiling;public FireEffectsFrame effects;}

  private IEnumerator ReadyFireAbilities(){

   yield return EnterCombat();

   double end;

   var input=duel.PlayerTransform.GetComponentInChildren<MagicInputController>(true);Assert.That(input.TrySelectElement(ElementId.Fire),Is.True);

   flightLease=new DirectFireInputLease(input);flightAbility=binding.PlayerAbilities;

   Assert.That(flightAbility,Is.Not.Null);flightAbility.SetSelected(true);

   flightMotor=duel.PlayerTransform.GetComponent<PlanetMotor>();flightActor=duel.PlayerTransform.GetComponentInChildren<HumanoidCharacterPresentation>(true);

   end=Time.realtimeSinceStartupAsDouble+10;while(!flightAbility.IsAvailable&&Time.realtimeSinceStartupAsDouble<end)yield return null;

   Assert.That(flightAbility.IsAvailable,Is.True);Assert.That(Time.timeScale,Is.GreaterThan(0));

  }

  private void ReleaseFireAbilityFixture(){flightAbility?.CancelAll();flightLease?.Dispose();flightLease=null;}

  [UnityTest,Timeout(240000)] public IEnumerator SavedFighterFireLiftGrowsUsesFallPoseAndReleaseFalls(){

   var samples=new List<FlightFrame>();string folder="BuildReports/HardPolish/G05/FireLift-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);

   try{yield return ReadyFireAbilities();Vector3 origin=flightMotor.Body.position,up=flightMotor.LocalUp;float early=0,late=0;bool fallPose=false;

    flightAbility.SetLiftHeld(true);float start=Time.time;double deadline=Time.realtimeSinceStartupAsDouble+25;

    while(Time.time-start<2.8f&&Time.realtimeSinceStartupAsDouble<deadline){yield return new WaitForEndOfFrame();float age=Time.time-start;

     float speed=Vector3.Dot(flightMotor.Body.linearVelocity,up);if(age<.6f)early=speed;if(age>2.5f)late=speed;

     fallPose|=age>.5f&&flightActor.MotionPhase==EarthAnimationPhase.Falling;

     samples.Add(new FlightFrame{time=age,altitude=Vector3.Dot(flightMotor.Body.position-origin,up),speed=speed,charge=flightAbility.LiftCharge,pose=flightActor.MotionPhase.ToString(),ceiling=flightMotor.FireLiftCeilingBlocked,effects=ReadFireEffectsFrame()});

    }

    var ascentImage=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/ascent.png",ascentImage.EncodeToPNG());UnityEngine.Object.Destroy(ascentImage);

    Assert.That(Time.time-start,Is.GreaterThanOrEqualTo(2.8f),"Clock stalled during held thrust.");

    Assert.That(flightMotor.FireLiftCeilingBlocked,Is.False,"Default spawn ascent blocked; inspect actual geometry before judging lift acceleration.");

    Assert.That(Vector3.Dot(flightMotor.Body.position-origin,up),Is.GreaterThan(6));Assert.That(late,Is.GreaterThan(early+2));

    Assert.That(fallPose,Is.True,"Jet ascent must use the existing relaxed fall pose.");

    flightAbility.SetLiftHeld(false);Assert.That(flightMotor.FireLiftActive,Is.False);

    deadline=Time.realtimeSinceStartupAsDouble+8;while(Vector3.Dot(flightMotor.Body.linearVelocity,up)>=-.5f&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;

    Assert.That(Vector3.Dot(flightMotor.Body.linearVelocity,up),Is.LessThan(-.5f));

    Assert.That(flightActor.Animator.GetFloat("VerticalSpeed"),Is.LessThanOrEqualTo(0));

    yield return new WaitForEndOfFrame();var descentImage=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/descent.png",descentImage.EncodeToPNG());UnityEngine.Object.Destroy(descentImage);

   }finally{File.WriteAllText(folder+"/frames.json",JsonUtility.ToJson(new FlightFrames{frames=samples},true));ReleaseFireAbilityFixture();}

  }

  [Serializable] private sealed class FlightFrames {public List<FlightFrame> frames;}

  [UnityTest,Timeout(240000)] public IEnumerator SavedFighterFireLiftStopsAtRealCeilingAndDeselectCancels(){

   GameObject ceiling=null;

   try{yield return ReadyFireAbilities();Vector3 up=flightMotor.LocalUp;Vector3 head=flightMotor.Capsule.bounds.center+up*flightMotor.Capsule.bounds.extents.magnitude;

    Vector3 plane=head+up*2;ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.name="Owned firelift ceiling";

    ceiling.transform.SetPositionAndRotation(plane+up*.15f,Quaternion.FromToRotation(Vector3.up,up));ceiling.transform.localScale=new Vector3(8,.3f,8);Physics.SyncTransforms();

    flightAbility.SetLiftHeld(true);bool blocked=false;float start=Time.time;double deadline=Time.realtimeSinceStartupAsDouble+25;

    while(Time.time-start<3&&Time.realtimeSinceStartupAsDouble<deadline){yield return new WaitForEndOfFrame();blocked|=flightMotor.FireLiftCeilingBlocked;

     Assert.That(Vector3.Dot(flightMotor.Body.position-plane,up),Is.LessThan(0),"Root crossed real opaque ceiling.");}

    Assert.That(blocked,Is.True,"Actual swept body never reported ceiling contact.");

    flightAbility.SetSelected(false);yield return new WaitForFixedUpdate();Assert.That(flightMotor.FireLiftActive,Is.False);Assert.That(flightAbility.IsLifting,Is.False);

   }finally{if(ceiling!=null)UnityEngine.Object.Destroy(ceiling);ReleaseFireAbilityFixture();}

  }

  [UnityTest,Timeout(240000)] public IEnumerator SavedFighterHandAndFootProjectilesTravelAndCancellationClearsEffects(){

   Action<Elemental.Simulation.Fire.FireAbilityCue> listener=null;

   try{yield return ReadyFireAbilities();Vector3 aim=binding.PlayerSession.MuzzlePosition+flightMotor.LocalUp*20;bool handReleased=false,footReleased=false;var groundIds=new List<uint>();

    listener=cue=>{if(cue.Kind==Elemental.Simulation.Fire.FireAbilityEffectKind.HandBolt){handReleased=true;Assert.That(Vector3.Distance((Vector3)cue.Position,binding.PlayerMuzzle.position),Is.LessThan(.01f));}

     if(cue.Kind==Elemental.Simulation.Fire.FireAbilityEffectKind.FootBolt){footReleased=true;var foot=(Transform)typeof(FireAbilityController).GetField("foot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(flightAbility);Assert.That(Vector3.Distance((Vector3)cue.Position,foot.position+flightMotor.LocalUp*.08f),Is.LessThan(.01f));}

     if(cue.Kind==Elemental.Simulation.Fire.FireAbilityEffectKind.GroundFlame){Assert.That(flightAbility.TryGetGroundPose(cue.Id,out _,out _),Is.True,"Every emitted node must start on live support.");groundIds.Add(cue.Id);}};flightAbility.Effect+=listener;

    Assert.That(flightAbility.TryShoot(aim,false),Is.True);var first=Enumerable.Range(0,flightAbility.ProjectileCapacity).Select(flightAbility.GetProjectile).Single(p=>p.Pending);

    Assert.That(first.Active,Is.False);yield return new WaitForSeconds(.3f);Assert.That(handReleased,Is.True);

    yield return new WaitForEndOfFrame();SaveFireAbilityFrame("hand-bolt");

    var moved=Enumerable.Range(0,flightAbility.ProjectileCapacity).Select(flightAbility.GetProjectile).FirstOrDefault(p=>p.Id==first.Id);

    Assert.That(!moved.Active||Vector3.Distance(moved.Position,first.Position)>.1f,Is.True,"Projectile must travel or resolve a physical hit.");

    Assert.That(flightAbility.TryShoot(aim,true),Is.True);yield return new WaitForSeconds(.4f);Assert.That(footReleased,Is.True);Assert.That(flightAbility.FootShots,Is.EqualTo(1));Assert.That(flightAbility.TryRing(),Is.True);yield return new WaitForSeconds(.65f);flightAbility.ReleaseRing();yield return new WaitForSeconds(.13f);yield return new WaitForEndOfFrame();SaveFireAbilityFrame("ring");

    Vector3 origin=flightMotor.Body.position,side=Vector3.Cross(flightMotor.LocalUp,flightMotor.FacingForward).normalized;

    Assert.That(flightAbility.TryGroundLine(origin+side*.8f,origin+side*2),Is.True,"Actual saved ground beside player must support a fire line.");

    yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();SaveFireAbilityFrame("ground-line");

    Assert.That(groundIds.Count,Is.GreaterThanOrEqualTo(2)); // Fire can now fracture its supporting loose stone during these .2s; birth support was checked synchronously above.

    flightAbility.SetSelected(false);yield return new WaitForFixedUpdate();Assert.That(Enumerable.Range(0,flightAbility.ProjectileCapacity).Any(i=>flightAbility.GetProjectile(i).Active||flightAbility.GetProjectile(i).Pending),Is.False);

    foreach(uint id in groundIds)Assert.That(flightAbility.TryGetGroundPose(id,out _,out _),Is.False);

    Assert.That(flightAbility.QuerySaturations,Is.Zero);Assert.That(flightAbility.TryShoot(aim,false),Is.False);

   }finally{if(flightAbility!=null&&listener!=null)flightAbility.Effect-=listener;ReleaseFireAbilityFixture();}

  }

  [Serializable] private sealed class FireEffectsFrame {
   public string scope="Current editor frame, explicit player binding effects; CPU step/upload diagnostic, not GPU timing or GC measurement.";
   public bool available;public int frame,QueryCount,BudgetStops,ActiveEmitters,AliveParticles,RingSectors,SurfaceProbeQueries,BlockedParcels,DroppedTimedCues;public double LastStepMilliseconds;
  }
  private FireEffectsFrame ReadFireEffectsFrame(){
   var effects=binding!=null?binding.GetComponent<Elemental.Presentation.Fire.FireAbilityEffects>():null;
   var sample=new FireEffectsFrame{available=effects!=null,frame=Time.frameCount};
   if(effects!=null){sample.LastStepMilliseconds=effects.LastStepMilliseconds;sample.QueryCount=effects.QueryCount;sample.BudgetStops=effects.BudgetStops;sample.ActiveEmitters=effects.ActiveEmitters;sample.AliveParticles=effects.AliveParticles;sample.RingSectors=effects.RingSectors;sample.SurfaceProbeQueries=effects.SurfaceProbeQueries;sample.BlockedParcels=effects.BlockedParcels;sample.DroppedTimedCues=effects.DroppedTimedCues;}
   return sample;
  }
  private void SaveFireAbilityFrame(string label){
   string folder="BuildReports/HardPolish/G05/FireAbilities";Directory.CreateDirectory(folder);
   string stem=folder+"/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-")+label;
   var sample=ReadFireEffectsFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();
   File.WriteAllBytes(stem+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
   File.WriteAllText(stem+".json",JsonUtility.ToJson(sample,true));
  }


  [UnityTest,Timeout(240000)] public IEnumerator FireLiftDeathCancelsAndRespawnReturnsCanonicalFirstVisibleScale(){
   try{yield return ReadyFireAbilities();var root=duel.PlayerTransform;var renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    Vector3 canonicalScale=root.localScale,canonicalWorldScale=root.lossyScale;
    var gold=All<Elemental.Presentation.VFX.GoldRespawnPresenter>().Single();
    var slots=(Array)typeof(Elemental.Presentation.VFX.GoldRespawnPresenter).GetField("_slots",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(gold);
    object slot=slots.GetValue(0),proxy=slot.GetType().GetField("Proxy").GetValue(slot);
    var proxyRoot=(GameObject)proxy.GetType().GetProperty("Root").GetValue(proxy);
    Assert.That(proxyRoot,Is.Not.Null,"Saved standing proxy must be baked before death.");
    Transform cachedPose=proxyRoot.transform.Find("Cached standing pose");Assert.That(cachedPose,Is.Not.Null);
    flightAbility.SetLiftHeld(true);yield return new WaitForFixedUpdate();
    duel.RequestKnockout(EarthDuelFighterId.Player,RagdollHandoff.Uniform(Vector3.zero));
    double deadline=Time.realtimeSinceStartupAsDouble+10;bool hidden=false,returned=false,proxyObserved=false;
    while(Time.realtimeSinceStartupAsDouble<deadline){yield return new WaitForEndOfFrame();
     bool visible=renderers.Any(r=>r!=null&&r.enabled&&!r.forceRenderingOff&&r.gameObject.activeInHierarchy);
     if(proxyRoot.activeInHierarchy){
      Assert.That(Vector3.Distance(proxyRoot.transform.localScale,Vector3.one),Is.LessThan(.0001f),"Visible gold proxy must use unit presentation scale from its first rendered frame.");
      Assert.That(Vector3.Distance(cachedPose.localScale,canonicalWorldScale),Is.LessThan(.0001f),"Gold cached standing pose must retain actual authored actor scale.");
      if(!proxyObserved){proxyObserved=true;SaveFireAbilityFrame("respawn-first-gold");}
     }
     if(!visible)hidden=true;
     if(hidden&&visible&&!returned){returned=true;Assert.That(root.localScale,Is.EqualTo(canonicalScale),"First visible actual fighter must already have authored canonical scale.");SaveFireAbilityFrame("respawn-first-actor");}
     if(duel.PlayerPhase==EarthDuelFighterPhase.Active)break;
    }
    Assert.That(duel.PlayerPhase,Is.EqualTo(EarthDuelFighterPhase.Active));Assert.That(root.localScale,Is.EqualTo(canonicalScale));
    Assert.That(flightMotor.FireLiftActive,Is.False,"Death/respawn must not resume held thrust.");Assert.That(flightAbility.IsLifting,Is.False);
    Assert.That(proxyObserved,Is.True,"First gold proxy frame was not observed.");
    Assert.That(hidden&&returned,Is.True,"No original-renderer hide/reveal transition was observed; first-visible acceptance is inconclusive.");
   }finally{ReleaseFireAbilityFixture();}
  }
 }
}
