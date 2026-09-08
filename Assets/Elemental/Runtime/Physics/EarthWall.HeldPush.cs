using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public sealed partial class EarthWall
    {
        private static readonly ProfilerMarker HeldPushMarker=new("Elemental.Earth.Wall.HeldPush");
        private EarthWallPushMotion _heldPush;
        private Vector3 _heldPushDirection;
        private bool _heldPushCoasting;
        private bool _heldPushWasKinematic;
        private Vector3 _heldPushSavedDirection;
        private Vector3 _heldPushSavedVelocity, _heldPushSavedAngularVelocity, _heldPushChargePosition;
        private Quaternion _heldPushChargeRotation;
        private CollisionDetectionMode _heldPushChargeCollisionMode;
        private float _heldPushReleasedSpeedCap=14f;
        private float _heldPushDustClock;
        private readonly RaycastHit[] _heldPushSupportHits=new RaycastHit[16];
        private float _heldPushSupportGap=float.PositiveInfinity;
        private CollisionDetectionMode _heldPushPreviousCollisionMode;
        private bool _heldPushOwnsCollisionMode;
        public bool IsHeldPushActive=>_heldPush.Active;
        public Vector3 HeldPushDirection=>_heldPushDirection;
        public float HeldPushSupportGap=>_heldPushSupportGap;
        public bool TryBeginHeldPush(Vector3 direction)
        {
            if(_heldPush.Active)return true;
            if(!isActiveAndEnabled||!IsEmergenceComplete||_body==null)return false;
            var wallNormal=Vector3.ProjectOnPlane(_forward,_up).normalized;
            if(wallNormal.sqrMagnitude<.5f)return false;
            _heldPushSavedDirection=_heldPushDirection;
            _heldPushDirection=Vector3.Dot(direction,wallNormal)<0?-wallNormal:wallNormal;
            if(_fractured)
            {
                if(!BeginFracturedHeldPush(_heldPushDirection))return false;
                _heldPush.Begin(FracturedHeldPushMass);
                return true;
            }
            RevealCracks();
            _heldPushWasKinematic=_body.isKinematic;
            _heldPushSavedVelocity=_heldPushWasKinematic?Vector3.zero:_body.linearVelocity;
            _heldPushSavedAngularVelocity=_heldPushWasKinematic?Vector3.zero:_body.angularVelocity;
            _heldPushChargePosition=_body.position;_heldPushChargeRotation=_body.rotation;
            _heldPushChargeCollisionMode=_body.collisionDetectionMode;
            _body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            _body.isKinematic=true;
            _heldPush.Begin(EstimatedMass);
            return true;
        }
        public float HeldPushCharge01=>_heldPush.Charge01;
        public void UpdateHeldPush(Vector3 direction)
        {
            // Ownership refresh only; charge never steers the latched wall normal.
        }
        public bool ReleaseHeldPush()
        {
            if(!_heldPush.Active)return false;
            if(IsFracturedHeldPushActive)
            {
                if(!isActiveAndEnabled){CancelHeldPush();return false;}
                return ReleaseFracturedHeldPush(_heldPush.Release());
            }
            if(_fractured||_body==null||!isActiveAndEnabled){CancelHeldPush();return false;}
            float charge=_heldPush.Charge01;
            float impulse=_heldPush.Release();
            RestoreHeldPushChargeBody();
            OnEarthMagicGrabbed(EarthMagicGripKind.VectorField);
            if(_body.isKinematic)return false;
            if(!_heldPushOwnsCollisionMode){_heldPushPreviousCollisionMode=_body.collisionDetectionMode;_heldPushOwnsCollisionMode=true;}
            _body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            _heldPushReleasedSpeedCap=Mathf.Lerp(18f,45f,charge);
            _heldPushCoasting=true;_heldPushDustClock=0;
            _body.AddForce(_heldPushDirection*impulse,ForceMode.Impulse);
            OnEarthMagicReleased(EarthMagicGripKind.VectorField);
            return true;
        }
        private void RestoreHeldPushChargeBody()
        {
            if(_body==null)return;
            _body.isKinematic=_heldPushWasKinematic;
            _body.collisionDetectionMode=_heldPushChargeCollisionMode;
            if(!_heldPushWasKinematic)
            {
                _body.linearVelocity=_heldPushSavedVelocity;
                _body.angularVelocity=_heldPushSavedAngularVelocity;
            }
        }
        public void CancelHeldPush()
        {
            if(!_heldPush.Active)return;
            bool fracturedCharge=IsFracturedHeldPushActive;
            _heldPush.Cancel();CancelFracturedHeldPush();if(!fracturedCharge&&!_fractured)RestoreHeldPushChargeBody();_heldPushDirection=_heldPushSavedDirection;
        }
        private void ResetHeldPushState()
        {
            CancelHeldPush();StopHeldPushCoasting();_heldPushDirection=Vector3.zero;_heldPushDustClock=0;
            _heldPushSupportGap=float.PositiveInfinity;
        }
        private void StopHeldPushCoasting()
        {
            _heldPushCoasting=false;
            if(_heldPushOwnsCollisionMode&&_body!=null)_body.collisionDetectionMode=_heldPushPreviousCollisionMode;
            _heldPushOwnsCollisionMode=false;
        }
        private float RootSlideDrag=>_heldPushCoasting?EarthWallPushMotion.Drag(EstimatedMass):_magicFieldActive?MagicFieldSlideDrag:WallSlideDrag;
        private void TickHeldPush()
        {
            if(!_heldPush.Active&&!_heldPushCoasting)return;
            using var marker=HeldPushMarker.Auto();
            if(_heldPush.Active)
            {
                if(IsFracturedHeldPushActive)
                {
                    TickFracturedHeldPush();
                    if(!IsFracturedHeldPushActive){CancelHeldPush();return;}
                    _heldPush.Step(Time.fixedDeltaTime);
                    return;
                }
                if(_fractured||_body==null){CancelHeldPush();StopHeldPushCoasting();return;}
                _body.position=_heldPushChargePosition;_body.rotation=_heldPushChargeRotation;
                _heldPush.Step(Time.fixedDeltaTime);
                return;
            }
            if(!_heldPushCoasting)return;
            if(_fractured||_body==null||_body.isKinematic){CancelHeldPush();StopHeldPushCoasting();return;}
            float speed=Vector3.ProjectOnPlane(_body.linearVelocity,_up).magnitude;
            if(!_heldPush.Active&&speed<.15f&&float.IsFinite(_heldPushSupportGap)&&_heldPushSupportGap<.1f&&Mathf.Abs(Vector3.Dot(_body.linearVelocity,_up))<.15f){StopHeldPushCoasting();return;}
            _heldPushDustClock-=Time.fixedDeltaTime;
            if(materialFeedback==null||speed<.4f||_heldPushDustClock>0||!float.IsFinite(_heldPushSupportGap)||_heldPushSupportGap>.2f)return;
            _heldPushDustClock=.075f;
            int stations=Mathf.Clamp(Mathf.CeilToInt(_finalScale.x/1.4f),3,8);
            float radius=Mathf.Clamp(_finalScale.x/stations*.6f,.4f,1.4f);
            var foot=_body.position-_up*(Height*.5f-_foundationEmbed);
            float strength=Mathf.Lerp(.8f,1.7f,Mathf.Clamp01(speed/9));
            for(int i=0;i<stations;i++)
            {
                var contact=foot+_tangent*((i+.5f)/stations-.5f)*_finalScale.x;
                var normal=_up;
                if(_orientationMode==ConstructionOrientationMode.FollowPlanetGravity)
                {
                    normal=(contact-_planetCenter).normalized;
                    contact=_planetCenter+normal*(foot-_planetCenter).magnitude;
                }
                materialFeedback.Emit(EarthMaterialFeedbackKind.Friction,contact,normal,strength,radius,
                    WallId,_generation,18,3);
            }
        }
        private void StabilizeHeldPushBody()
        {
            using var marker=HeldPushMarker.Auto();
            Vector3 nextUp=_up;
            if(_orientationMode==ConstructionOrientationMode.FollowPlanetGravity)
            {
                Vector3 radial=_body.position-_planetCenter;
                if(radial.sqrMagnitude>.01f)nextUp=radial.normalized;
            }
            var transport=Quaternion.FromToRotation(_up,nextUp);
            _heldPushDirection=Vector3.ProjectOnPlane(transport*_heldPushDirection,nextUp).normalized;
            _tangent=Vector3.ProjectOnPlane(transport*_tangent,nextUp).normalized;
            _forward=Vector3.Cross(_tangent,nextUp).normalized;
            _up=nextUp;_surfaceRotation=Quaternion.LookRotation(_forward,_up);
            _body.angularVelocity=Vector3.zero;_body.rotation=_surfaceRotation;
            // PhysX owns translation. Never advance a predicted position here and
            // then let the physics solver integrate the same velocity a second time.
            _surfacePosition=_body.position;_surfaceRootRadius=(_body.position-_planetCenter).magnitude;
            var baseCenter=_surfacePosition-_up*(Height*.5f);
            _embeddedStart=baseCenter-_tangent*(_finalScale.x*.5f);
            _embeddedEnd=baseCenter+_tangent*(_finalScale.x*.5f);
            Vector3 velocity=_body.linearVelocity;
            Vector3 tangentVelocity=Vector3.ProjectOnPlane(velocity,_up);
            Vector3 lateral=tangentVelocity-_heldPushDirection*Vector3.Dot(tangentVelocity,_heldPushDirection);
            _body.AddForce(-tangentVelocity*RootSlideDrag-lateral*8f,ForceMode.Acceleration);
            if(tangentVelocity.magnitude>_heldPushReleasedSpeedCap)
                _body.linearVelocity=Vector3.ClampMagnitude(tangentVelocity,_heldPushReleasedSpeedCap)+Vector3.Project(velocity,_up);
            var scale=transform.lossyScale;
            var colliderCenter=_body.position+_body.rotation*Vector3.Scale(_collider.center,scale);
            float halfHeight=Mathf.Abs(_collider.size.y*scale.y)*.5f;
            float halfWidth=Mathf.Abs(_collider.size.x*scale.x)*.4f;
            var foot=colliderCenter-_up*halfHeight;
            float nearestGap=float.PositiveInfinity;
            Vector3 supportNormal=_up;
            float ahead=Mathf.Abs(_collider.size.z*scale.z)*.5f+
                Mathf.Max(0,Vector3.Dot(tangentVelocity,_heldPushDirection))*Time.fixedDeltaTime;
            for(int row=0;row<2;row++)
            for(int station=-1;station<=1;station++)
            {
                Vector3 point=foot+_tangent*(station*halfWidth)+_heldPushDirection*(row*ahead);
                int count=UnityEngine.Physics.RaycastNonAlloc(point+_up*.35f,-_up,_heldPushSupportHits,1.4f,~0,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)
                {
                    var hit=_heldPushSupportHits[i];var other=hit.collider;
                    if(other==null||other==_collider||other.transform.IsChildOf(transform))continue;
                    if(other.attachedRigidbody!=null&&!other.attachedRigidbody.isKinematic)continue;
                    if(Vector3.Dot(hit.normal,_up)<.7f)continue;
                    float gap=Vector3.Dot(point-hit.point,_up);
                    if(gap<-.18f)continue; // A tall obstacle remains a blocking collision, never a step/teleport.
                    if(gap<nearestGap){nearestGap=gap;supportNormal=hit.normal;}
                }
            }
            _heldPushSupportGap=nearestGap;
            float supportAcceleration=float.IsFinite(nearestGap)?
                nearestGap<-.005f?Mathf.Clamp(-nearestGap*240-Vector3.Dot(velocity,_up)*14,-8,80): -Mathf.Min(8,2+Mathf.Max(0,nearestGap)*30):-8;
            if(float.IsFinite(nearestGap)&&nearestGap<.12f)
            {
                // Follow a rising support plane before the leading bottom edge hits it.
                // This supplies vertical velocity through force, without moving the body pose.
                float climbSpeed=Mathf.Max(0,-Vector3.Dot(tangentVelocity,supportNormal)/Mathf.Max(.7f,Vector3.Dot(_up,supportNormal)));
                if(climbSpeed>.05f)
                    supportAcceleration=Mathf.Max(supportAcceleration,Mathf.Clamp((climbSpeed-Vector3.Dot(velocity,_up))/Time.fixedDeltaTime,0,250));
            }
            _body.AddForce(_up*supportAcceleration,ForceMode.Acceleration);
        }
    }
}
