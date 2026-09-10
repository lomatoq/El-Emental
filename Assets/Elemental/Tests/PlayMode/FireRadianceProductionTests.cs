using System.Collections;
using System.Linq;
using Elemental.Runtime.Fire;
using Elemental.Runtime.Physics;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(240000)] public IEnumerator FireChargeActuallyIlluminatesSurroundingsAndClears()
  {
   float scale=Time.timeScale;FireVisualCaptureCamera capture=null;
   try
   {
    yield return ReadyFireAbilities();var lighting=binding.GetComponent<FireAbilityLighting>();Assert.That(lighting,Is.Not.Null);
    Assert.That(flightAbility.TryRing(),Is.True);yield return new WaitForSeconds(.45f);
    Assert.That(lighting.ActiveLights,Is.GreaterThan(2));
    var camera=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
    capture=camera.gameObject.AddComponent<FireVisualCaptureCamera>();capture.Place(flightMotor.Body.position-flightMotor.FacingForward*7+flightMotor.LocalUp*5,flightMotor.Body.position);
    Time.timeScale=0;yield return new WaitForEndOfFrame();SaveFireAbilityFrame("ring-light-on");
    var on=ScreenCapture.CaptureScreenshotAsTexture();lighting.enabled=false;yield return new WaitForEndOfFrame();var off=ScreenCapture.CaptureScreenshotAsTexture();
    try{var a=on.GetPixels32();var b=off.GetPixels32();int changed=0;for(int i=0;i<a.Length;i++)if(a[i].r-b[i].r>4)changed++;
     Assert.That(changed,Is.GreaterThan(150),"Actual camera image must show surrounding light beyond emissive flames.");}
    finally{Object.Destroy(on);Object.Destroy(off);lighting.enabled=true;Time.timeScale=scale;}
    flightAbility.CancelAll();yield return new WaitForSeconds(1);Assert.That(lighting.ActiveLights,Is.Zero);
   }
   finally{Time.timeScale=scale;if(capture!=null)Object.Destroy(capture);ReleaseFireAbilityFixture();}
  }
  [UnityTest,Timeout(240000)] public IEnumerator FlyingFireballRetainsHotTrailAndCoolingSmoke()
  {
   FireVisualCaptureCamera capture=null;float step=Time.captureDeltaTime;
   try
   {
    Time.captureDeltaTime=1f/60f;yield return ReadyFireAbilities();
    var camera=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
    Vector3 origin=binding.PlayerSession.MuzzlePosition;
    capture=camera.gameObject.AddComponent<FireVisualCaptureCamera>();capture.Place(origin-flightMotor.FacingForward*12+flightMotor.LocalUp*6,origin+flightMotor.LocalUp*5);
    Assert.That(flightAbility.TryShoot(origin+flightMotor.LocalUp*30,false),Is.True);yield return new WaitForSeconds(.62f);yield return new WaitForEndOfFrame();
    var shot=Enumerable.Range(0,flightAbility.ProjectileCapacity).Select(flightAbility.GetProjectile).First(x=>x.Active);
    var effects=binding.GetComponent<FireAbilityEffects>();
    var flows=(FireFlowVolumeBackend[])typeof(FireAbilityEffects).GetField("flows",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(effects);
    int hot=0,cool=0;float tail=0;
    for(int j=2;j<2+flightAbility.ProjectileCapacity;j++)for(int i=0;i<flows[j].Solver.Count;i++)
    {var parcel=flows[j].Solver.Particles[i];if(parcel.Spark)continue;if(parcel.Age>parcel.HotLifetime)cool++;else{hot++;tail=Mathf.Max(tail,Vector3.Dot((Vector3)shot.Position-(Vector3)parcel.Position,(Vector3)shot.Direction));}}
    Assert.That(hot,Is.GreaterThan(10));Assert.That(cool,Is.GreaterThan(0));Assert.That(tail,Is.GreaterThan(1));
    SaveFireAbilityFrame("developed-fireball-trail-smoke");
    Time.captureDeltaTime=1f/100000f;
    Vector3 shotPoint=shot.Position,shotDirection=shot.Direction;
    Vector3 side=Vector3.Cross(shotDirection,flightMotor.FacingForward).normalized;
    if(side.sqrMagnitude<.1f)side=flightMotor.transform.right;
    capture.Place(shotPoint+side*4-shotDirection,shotPoint-shotDirection*1.2f);
    yield return new WaitForEndOfFrame();SaveFireAbilityFrame("fireball-atlas-close");
   }
   finally{Time.captureDeltaTime=step;if(capture!=null)Object.Destroy(capture);ReleaseFireAbilityFixture();}
  }
  [UnityTest,Timeout(240000)] public IEnumerator ReleasedRingDamagesActualOpponentAndIgnitesReceivers()
  {
   int ignitions=0;System.Action<FireSurfaceContact> onIgnite=null;FireWorldImpact response=null;
   try
   {
    yield return ReadyFireAbilities();response=binding.PlayerSession.GetComponent<FireWorldImpact>();
    onIgnite=_=>ignitions++;response.SustainedIgnition+=onIgnite;
    Assert.That(flightAbility.TryRing(),Is.True);yield return new WaitForSeconds(.65f);
    var opponent=duel.BotTransform.GetComponent<Elemental.Runtime.Characters.PlanetMotor>();
    opponent.Body.position=flightMotor.Body.position+flightMotor.FacingForward*3+flightMotor.LocalUp*.15f;
    opponent.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();float health=duel.BotHealth;
    Assert.That(flightAbility.ReleaseRing(),Is.True);
    yield return new WaitForSeconds(.32f);yield return new WaitForEndOfFrame();
    Assert.That(duel.BotHealth,Is.LessThan(health-20),"Released ring must hurt the actual saved opponent.");
    Assert.That(ignitions,Is.GreaterThan(0),"Strong ring contact must ignite receivers without repeated stream heating.");
    Assert.That(binding.GetComponent<FireAbilityLighting>().ActiveLights,Is.GreaterThan(0));
    SaveFireAbilityFrame("ring-damage-ignition");
   }
   finally{if(response!=null&&onIgnite!=null)response.SustainedIgnition-=onIgnite;ReleaseFireAbilityFixture();}
  }
  [UnityTest,Timeout(240000)] public IEnumerator NeighboringArenaCachesShareIrregularBurnFootprint()
  {
   GameObject left=null,right=null;
   try
   {
    yield return ReadyFireAbilities();left=GameObject.CreatePrimitive(PrimitiveType.Cube);right=GameObject.CreatePrimitive(PrimitiveType.Cube);
    Vector3 center=flightMotor.Body.position+flightMotor.LocalUp*8;
    left.transform.position=center-Vector3.right*.5f;right.transform.position=center+Vector3.right*.5f;
    left.AddComponent<EarthArenaPiece>();right.AddComponent<EarthArenaPiece>();
    left.GetComponent<Rigidbody>().isKinematic=true;right.GetComponent<Rigidbody>().isKinematic=true;
    var response=binding.PlayerSession.GetComponent<FireWorldImpact>();var collider=left.GetComponent<Collider>();
    Vector3 point=center-Vector3.right*.04f+Vector3.forward*.5f;
    for(int i=0;i<15;i++){Physics.SyncTransforms();Assert.That(response.ApplyContact(collider,point,Vector3.forward,Vector3.back,.02f),Is.True);yield return new WaitForFixedUpdate();}
    yield return new WaitForEndOfFrame();var a=new MaterialPropertyBlock();var b=new MaterialPropertyBlock();
    left.GetComponent<Renderer>().GetPropertyBlock(a);right.GetComponent<Renderer>().GetPropertyBlock(b);
    Assert.That(a.GetFloat("_FireChar"),Is.GreaterThan(.01f));Assert.That(b.GetFloat("_FireChar"),Is.EqualTo(a.GetFloat("_FireChar")).Within(.025f));
    Assert.That(Vector3.Distance(a.GetVector("_FireBurnPoint"),b.GetVector("_FireBurnPoint")),Is.LessThan(.001f));
    Assert.That(a.GetFloat("_FireBurnRadius"),Is.LessThanOrEqualTo(1.1f));
   }
   finally{if(left!=null)Object.Destroy(left);if(right!=null)Object.Destroy(right);ReleaseFireAbilityFixture();}
  }
 }
}
