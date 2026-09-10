using System.Collections;
using Elemental.Simulation.Fire;
using Elemental.Runtime.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(240000)]public IEnumerator SphereWaveIgnitesAbovePlayerOnceAndRespectsWallAndRecovery()
  {
   float saved=Time.captureDeltaTime;GameObject exposed=null,hidden=null,wall=null;System.IDisposable isolation=null;
   System.Action<FireSurfaceContact> observed=null;FireWorldImpact response=null;
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();isolation=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    Vector3 up=flightMotor.LocalUp,side=Vector3.Cross(up,flightMotor.FacingForward).normalized,center=flightAbility.OwnerRoot.position+up;
    exposed=GameObject.CreatePrimitive(PrimitiveType.Cube);exposed.name="Sphere wave elevated ignition";exposed.transform.position=center+up*3.5f;exposed.transform.localScale=Vector3.one*.4f;
    hidden=GameObject.CreatePrimitive(PrimitiveType.Cube);hidden.name="Sphere wave receiver behind intact wall";hidden.transform.position=center+up*3.5f+side*4;hidden.transform.localScale=Vector3.one*.4f;
    Vector3 blockedDirection=(hidden.transform.position-center).normalized;
    wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=center+blockedDirection*3;wall.transform.rotation=Quaternion.LookRotation(blockedDirection);wall.transform.localScale=new Vector3(1.4f,1.4f,.3f);
    var front=exposed.GetComponent<Collider>();var behind=hidden.GetComponent<Collider>();int visibleIgnitions=0,hiddenIgnitions=0;
    response=binding.PlayerSession.GetComponent<FireWorldImpact>();observed=c=>{if(c.Surface==front)visibleIgnitions++;if(c.Surface==behind)hiddenIgnitions++;};response.SustainedIgnition+=observed;
    Physics.SyncTransforms();flightAbility.SetWeaveHeld(true,FireWeaveForm.Sphere,1);
    Assert.That(flightAbility.TryReleaseSphereWave(),Is.True);Assert.That(flightAbility.WeaveHeld,Is.False);
    Assert.That(flightAbility.TryReleaseSphereWave(),Is.False);
    for(int i=0;i<70;i++)yield return null;
    Assert.That(visibleIgnitions,Is.EqualTo(1),"The wave reaches above the player, not only a ground ring");
    Assert.That(hiddenIgnitions,Is.Zero,"Intact opaque geometry blocks ignition");
    Assert.That(flightAbility.SphereWaveActive,Is.False);Assert.That(flightAbility.SphereWaveContacts,Is.GreaterThan(0));
    flightAbility.SetWeaveHeld(true,FireWeaveForm.Sphere,1);Assert.That(flightAbility.WeaveHeld,Is.False,"Energy is still recovering after the wave");
    for(int i=0;i<25;i++)yield return null;
    flightAbility.SetWeaveHeld(true,FireWeaveForm.Sphere,1);Assert.That(flightAbility.WeaveHeld,Is.True);
    flightAbility.CancelAll();Assert.That(flightAbility.SphereWaveActive,Is.False);
   }
   finally
   {
    if(response!=null&&observed!=null)response.SustainedIgnition-=observed;
    if(exposed!=null)Object.Destroy(exposed);if(hidden!=null)Object.Destroy(hidden);if(wall!=null)Object.Destroy(wall);
    isolation?.Dispose();Time.captureDeltaTime=saved;ReleaseFireAbilityFixture();
   }
  }
 }
}
