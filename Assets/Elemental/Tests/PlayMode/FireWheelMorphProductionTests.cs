using System.Collections;
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
  [UnityTest,Timeout(120000)]public IEnumerator ActualWheelMorphRetainsGasAcrossModesAndOpensVerticalCoverage()
  {
   System.IDisposable safeRival=null;FireVisualCaptureCamera capture=null;float saved=Time.captureDeltaTime;
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();safeRival=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    var effects=binding.GetComponent<FireAbilityEffects>();var view=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
    capture=view.gameObject.AddComponent<FireVisualCaptureCamera>();Vector3 up=flightMotor.LocalUp,side=Vector3.Cross(up,flightMotor.FacingForward).normalized;
    capture.Place(flightMotor.Body.position+side*6+up*2,flightMotor.Body.position+up);
    flightAbility.SetWeaveHeld(true,FireWeaveForm.Flood,6f/12);yield return new WaitForSeconds(.4f);yield return new WaitForEndOfFrame();SaveFireAbilityFrame("wheel-hand-to-orbit-bridge");
    var flows=(FireFlowVolumeBackend[])typeof(FireAbilityEffects).GetField("flows",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(effects);
    var solver=flows[46].Solver;Assert.That(solver.Count,Is.GreaterThan(0));uint id=solver.Particles.Take(solver.Count).OrderBy(x=>x.Age).First().Id;
    flightAbility.SetWeaveHeld(true,FireWeaveForm.Orbit,8f/12);yield return null;yield return new WaitForEndOfFrame();
    Assert.That(solver.Particles.Take(solver.Count).Any(x=>x.Id==id),Is.True,"Changing form must not clear previously emitted gas.");
    yield return new WaitForSeconds(.35f);yield return new WaitForEndOfFrame();SaveFireAbilityFrame("wheel-opening-orbital-volume");float before=effects.WeaveVerticalCoverage;
    flightAbility.SetWeaveHeld(true,FireWeaveForm.Sphere,1);yield return null;yield return new WaitForEndOfFrame();
    Assert.That(effects.WeaveVerticalCoverage,Is.LessThan(.98f),"A wheel step must not replace the whole visual instantly.");
    yield return new WaitForSeconds(.65f);yield return new WaitForEndOfFrame();Assert.That(effects.WeaveVerticalCoverage,Is.GreaterThan(before));Assert.That(effects.WeaveVerticalCoverage,Is.GreaterThan(.95f));SaveFireAbilityFrame("wheel-full-approved-helical-sphere");
   }
   finally{safeRival?.Dispose();if(capture!=null)Object.Destroy(capture);Time.captureDeltaTime=saved;ReleaseFireAbilityFixture();}
  }
 }
}
