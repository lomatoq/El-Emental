using Elemental.Simulation.Characters;
using Unity.Profiling;
using UnityEngine;
namespace Elemental.Runtime.Characters
{
    public sealed partial class PlanetMotor
    {
        private static readonly ProfilerMarker FireLowFlightMarker=new("Elemental.Fire.LowFlight.Fixed");
        private readonly RaycastHit[] fireLowFlightGroundHits=new RaycastHit[48];
        public bool FireLowFlightActive {get;private set;}
        public bool FireFlightPoseActive=>FireLiftActive||FireLowFlightActive;
        public Vector3 FireLowFlightDirection {get;private set;}
        public float FireLowFlightClearance {get;private set;}
        public bool FireLowFlightBlocked {get;private set;}
        public int FireLowFlightQuerySaturations {get;private set;}
        public void SetFireLowFlight(bool active)
        {
            FireLowFlightActive=active&&isActiveAndEnabled&&!FireLiftActive&&!IsImpactStunned&&!IsMantling&&
                !HasDirectedExternalMotion&&targetBody!=null&&!targetBody.isKinematic;
            if(!FireLowFlightActive){FireLowFlightBlocked=false;FireLowFlightClearance=0;}
        }
        private void PrepareFireLowFlight()
        {
            if(!FireLowFlightActive)return;
            if(FireLiftActive||IsImpactStunned||IsMantling||HasDirectedExternalMotion||
               (_puppet!=null&&_puppet.CurrentState.Mode==CharacterPhysicalMode.FullRagdoll))
            {SetFireLowFlight(false);return;}
            if(!TryFireLowFlightSupport(SupportFeetPoint(_localUp),out float height))
            {SetFireLowFlight(false);return;}
            FireLowFlightClearance=height;
            _ignoreGroundTicks=Mathf.Max(_ignoreGroundTicks,2);IsGrounded=false;_movingSupportTicks=0;
            _lastCarrySurfaceId=_lastCarryGeneration=0;_lastCarrySurfaceVelocity=Vector3.zero;
            _jumpWindow=default;_landingRoll.Cancel();
        }
        private bool TryFireLowFlightSupport(Vector3 feet,out float height)
        {
            height=float.PositiveInfinity;
            const float above=.65f;
            int count=UnityEngine.Physics.RaycastNonAlloc(feet+_localUp*above,-_localUp,fireLowFlightGroundHits,
                FireLowFlightMotion.MaximumSupportDistance+above,groundMask,QueryTriggerInteraction.Ignore);
            if(count==fireLowFlightGroundHits.Length){FireLowFlightQuerySaturations++;return false;}
            for(int i=0;i<count;i++)
            {
                var h=fireLowFlightGroundHits[i];
                if(h.collider==null||h.rigidbody==targetBody||h.collider.transform.IsChildOf(transform)||
                   h.collider.GetComponentInParent<PlanetMotor>()!=null||Vector3.Dot(h.normal,_localUp)<.55f)continue;
                height=Mathf.Min(height,h.distance-above);
            }
            return float.IsFinite(height)&&height>=-.2f&&height<=FireLowFlightMotion.MaximumSupportDistance;
        }
        private void ApplyFireLowFlight(Vector3 desiredDirection)
        {
            using var marker=FireLowFlightMarker.Auto();float dt=Time.fixedDeltaTime;
            Vector3 tangent=Vector3.ProjectOnPlane(targetBody.linearVelocity,_localUp);
            Vector3 desired=Vector3.ProjectOnPlane(desiredDirection,_localUp);
            Vector3 projected=Vector3.MoveTowards(tangent,Vector3.ClampMagnitude(desired,1)*FireLowFlightMotion.MaximumSpeed,
                FireLowFlightMotion.Acceleration*dt);
            FireLowFlightDirection=projected.sqrMagnitude>.01f?projected.normalized:FacingForward;
            float clearance=float.PositiveInfinity,ceiling=float.PositiveInfinity;FireLowFlightBlocked=false;
            if(projected.sqrMagnitude>.0001f&&targetBody.SweepTest(projected.normalized,out RaycastHit obstacle,
                projected.magnitude*dt+.07f,QueryTriggerInteraction.Ignore))
            {clearance=Mathf.Max(0,obstacle.distance-.04f);FireLowFlightBlocked=true;}
            float upwardProbe=Mathf.Max(FireLowFlightMotion.MaximumVerticalSpeed,Vector3.Dot(targetBody.linearVelocity,_localUp))*dt+.06f;
            if(targetBody.SweepTest(_localUp,out RaycastHit ceilingHit,upwardProbe,QueryTriggerInteraction.Ignore))
                ceiling=Mathf.Max(0,ceilingHit.distance-.025f);
            Vector3 delta=ToVector3(FireLowFlightMotion.VelocityChange(ToFloat3(targetBody.linearVelocity),ToFloat3(_localUp),
                ToFloat3(desired),FireLowFlightClearance,Vector3.Dot(_lastGravityAcceleration,_localUp),dt,clearance,ceiling));
            if(_puppet!=null)_puppet.ApplyUniformVelocityChange(delta);else targetBody.AddForce(delta,ForceMode.VelocityChange);
            PublishLocomotion(tangent,projected,false);
        }
    }
}
