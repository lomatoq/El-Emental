using System.Collections;
using System.Reflection;
using Elemental.Runtime.Fire;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 [DefaultExecutionOrder(2990)] public sealed class FireFinalPoseTestWriter:MonoBehaviour
 {
  public Transform Muzzle;public Vector3 Shift;public bool Armed;public Vector3 Written;
  void LateUpdate(){if(!Armed)return;Muzzle.position+=Shift;Written=Muzzle.position;Armed=false;}
 }
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(240000)] public IEnumerator FlyingQuickAndHeldBoltsCommitFinalHandPoseAndRejectBlockedMuzzle()
  {
   FireFinalPoseTestWriter writer=null;Transform muzzle=null;Vector3 savedLocal=default;GameObject blocker=null;System.IDisposable rival=null;System.Action<FireAbilityCue> observed=null;
   try
   {
    yield return ReadyFireAbilities();rival=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    muzzle=(Transform)typeof(FireAbilityController).GetField("hand",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(flightAbility);savedLocal=muzzle.localPosition;
    writer=flightAbility.gameObject.AddComponent<FireFinalPoseTestWriter>();writer.Muzzle=muzzle;
    flightAbility.SetLiftHeld(true);yield return new WaitForSeconds(.7f);
    Assert.That(flightAbility.IsLifting,Is.True);
    int emitted=0;observed=cue=>{if(cue.Kind==FireAbilityEffectKind.HandBolt)emitted++;};flightAbility.Effect+=observed;
    for(int sample=0;sample<3;sample++)
    {
     yield return new WaitForSeconds(.16f);Vector3 up=flightMotor.LocalUp;
     Vector3 aim=flightAbility.CurrentHandMuzzlePosition+up*35+flightMotor.FacingForward*35;
     Assert.That(flightAbility.BeginBoltCharge(aim),Is.True);
     if(sample==1)yield return new WaitForSeconds(.6f);
     uint id=flightAbility.ChargingBoltId;int before=emitted,commits=flightAbility.CommittedLaunches,blocked=flightAbility.BlockedMuzzleCommits;
     Vector3 old=flightAbility.CurrentHandMuzzlePosition;
     Assert.That(flightAbility.ReleaseBoltCharge(),Is.True);Assert.That(emitted,Is.EqualTo(before),"Admission cannot publish a stale flight cue");
     writer.Shift=up*2;writer.Armed=true;
     if(sample==2){blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=old+writer.Shift;blocker.transform.localScale=Vector3.one*2;Physics.SyncTransforms();}
     yield return new WaitForEndOfFrame();
     if(sample<2)
     {
      Assert.That(flightAbility.CommittedLaunches,Is.EqualTo(commits+1));Assert.That(emitted,Is.EqualTo(before+1));
      Assert.That(Vector3.Distance(flightAbility.LastCommittedHand,writer.Written),Is.LessThan(.001f));
      Assert.That(Vector3.Distance(flightAbility.LastCommittedHand,old),Is.GreaterThan(1.5f),"Final pose must replace old hand location");
      Assert.That(flightAbility.LastLaunchFrame,Is.EqualTo(Time.frameCount));
      for(int i=0;i<flightAbility.ProjectileCapacity;i++){var b=flightAbility.GetProjectile(i);if(b.Id==id){Assert.That(b.PosePending,Is.False);Assert.That(b.Active,Is.True);Assert.That(Vector3.Distance(b.Position,flightAbility.LastLaunchPosition),Is.LessThan(.001));}}
     }
     else
     {
      Assert.That(flightAbility.BlockedMuzzleCommits,Is.EqualTo(blocked+1));Assert.That(emitted,Is.EqualTo(before));
      for(int i=0;i<flightAbility.ProjectileCapacity;i++){var b=flightAbility.GetProjectile(i);if(b.Id==id)Assert.That(b.Active,Is.False);}
     }
     muzzle.localPosition=savedLocal;
    }
   }
   finally{if(observed!=null&&flightAbility!=null)flightAbility.Effect-=observed;if(muzzle!=null)muzzle.localPosition=savedLocal;if(writer!=null)Object.Destroy(writer);if(blocker!=null)Object.Destroy(blocker);rival?.Dispose();ReleaseFireAbilityFixture();}
  }
 }
}
