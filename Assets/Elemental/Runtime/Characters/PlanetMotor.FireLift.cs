using Elemental.Simulation.Characters;
using UnityEngine;
namespace Elemental.Runtime.Characters {
 public sealed partial class PlanetMotor {
  public bool FireLiftActive {get;private set;}
  public float FireLiftCharge {get;private set;}
  public bool FireLiftCeilingBlocked {get;private set;}
  public void SetFireLift(float normalizedCharge,bool active) {
   FireLiftActive=active&&isActiveAndEnabled&&float.IsFinite(normalizedCharge);
   FireLiftCharge=FireLiftActive?Mathf.Clamp01(normalizedCharge):0;
   if(!FireLiftActive)FireLiftCeilingBlocked=false;
  }
  private void PrepareFireLift() {
   if(!FireLiftActive)return;
   if(IsImpactStunned||IsMantling||HasDirectedExternalMotion||targetBody==null||targetBody.isKinematic||
      (_puppet!=null&&_puppet.CurrentState.Mode==CharacterPhysicalMode.FullRagdoll)) {SetFireLift(0,false);return;}
   _ignoreGroundTicks=Mathf.Max(_ignoreGroundTicks,2);IsGrounded=false;_movingSupportTicks=0;
   _lastCarrySurfaceId=_lastCarryGeneration=0;_lastCarrySurfaceVelocity=Vector3.zero;
   _jumpWindow=default;_landingRoll.Cancel();
  }
  private void ApplyFireLift() {
   if(!FireLiftActive)return;
   float delta=Time.fixedDeltaTime,speed=Vector3.Dot(targetBody.linearVelocity,_localUp);
   float probe=Mathf.Max(0,speed,FireLiftMotion.TargetSpeed(FireLiftCharge))*delta+.04f;
   float clearance=float.PositiveInfinity;FireLiftCeilingBlocked=false;
   // Sweep the actual motor body's attached shapes. Never reposition through a ceiling.
   if(targetBody.SweepTest(_localUp,out var hit,probe,QueryTriggerInteraction.Ignore)) {
    clearance=Mathf.Max(0,hit.distance-.02f);FireLiftCeilingBlocked=true;
   }
   float change=FireLiftMotion.VelocityChange(speed,Vector3.Dot(_lastGravityAcceleration,_localUp),FireLiftCharge,delta,clearance);
   if(_puppet!=null)_puppet.ApplyUniformVelocityChange(_localUp*change);
   else targetBody.AddForce(_localUp*change,ForceMode.VelocityChange);
  }
 }
}
