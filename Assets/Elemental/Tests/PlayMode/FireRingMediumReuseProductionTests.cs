using System.Collections;
using System.Reflection;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(180000)]public IEnumerator ActualRingChargeSeatsResetMediumDetailWhenReusedForGroundFire()
  {
   System.IDisposable safe=null;float saved=Time.captureDeltaTime;
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();safe=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    var effects=binding.GetComponent<FireAbilityEffects>();var flags=BindingFlags.NonPublic|BindingFlags.Instance;
    var flows=(FireFlowVolumeBackend[])typeof(FireAbilityEffects).GetField("flows",flags).GetValue(effects);
    var materials=new Material[24];for(int i=0;i<24;i++)materials[i]=(Material)typeof(FireFlowVolumeBackend).GetField("smokeMaterial",flags).GetValue(flows[i+10]);
    Assert.That(flightAbility.TryRing(),Is.True);yield return new WaitForSeconds(.12f);int charged=0;
    foreach(var material in materials)if(material.GetFloat("_MediumAuthoredShape")>.5f)charged++;
    Assert.That(charged,Is.EqualTo(24));flightAbility.CancelAll();yield return new WaitForSeconds(.05f);
    Vector3 origin=flightMotor.Body.position,side=Vector3.Cross(flightMotor.LocalUp,flightMotor.FacingForward).normalized;
    Assert.That(flightAbility.TryGroundLine(origin+side*.3f,origin+side*2),Is.True);yield return new WaitForSeconds(.13f);
    int reused=0;for(int i=0;i<24;i++)if(flows[i+10].Solver.Count>0&&materials[i].GetFloat("_MediumAuthoredShape")<.5f)reused++;
    Assert.That(reused,Is.GreaterThan(0),"Real ground cues must reset the ring-only visual contract on pooled seats.");
   }
   finally{safe?.Dispose();Time.captureDeltaTime=saved;ReleaseFireAbilityFixture();}
  }
 }
}
