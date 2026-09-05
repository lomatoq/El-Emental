using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public sealed partial class PlanetMotor
    {
        private static readonly ProfilerMarker MantleMarker = new("Elemental.PlanetMotor.AutoMantle");
        private readonly Collider[] _mantleOverlaps = new Collider[16];
        private readonly RaycastHit[] _mantleHits = new RaycastHit[16];
        private Collider _mantleSupport;
        private uint _mantleSurfaceId, _mantleGeneration;
        private Vector3 _mantleStartLocal, _mantleEndLocal, _mantleUpLocal, _mantleLedgeLocal, _mantleTopLocal, _mantleForwardLocal;
        private float _mantleElapsed, _mantleDuration, _mantleNextAttempt, _mantleIntent;
        private Vector3 _mantleLastGoal;
        public bool IsMantling { get; private set; }
        public float MantleProgress => IsMantling ? Mathf.Clamp01(_mantleElapsed/_mantleDuration) : 0f;
        public EarthMantlePhase MantlePhase => IsMantling ? EarthMantleMotion.Phase(MantleProgress) : EarthMantlePhase.None;
        public Vector3 MantleLedgePoint => _mantleSupport != null ? _mantleSupport.transform.TransformPoint(_mantleLedgeLocal) : Vector3.zero;
        public uint MantleSequence { get; private set; }
        public bool MantleStartedAirborne { get; private set; }
        public string MantleLastRejection { get; private set; }

        private bool StepAutoMantle()
        {
            using (MantleMarker.Auto())
            {
                bool physical = _puppet != null && _puppet.CurrentState.Mode != CharacterPhysicalMode.AnimatedMotor &&
                    _puppet.CurrentState.Mode != CharacterPhysicalMode.PhysicalAssist;
                if (IsMantling)
                {
                    if (physical || _mantleSupport == null || !_mantleSupport.enabled || !_mantleSupport.gameObject.activeInHierarchy)
                        return AbortMantle("Support lost or physical interruption");
                    var support = CharacterSupportRuntimeAdapter.Classify(_mantleSupport, 0, 1);
                    if (!support.IsWalkable || support.SurfaceId != _mantleSurfaceId || support.Generation != _mantleGeneration)
                        return AbortMantle("Support generation changed");
                    if (_mantleElapsed > .15f && Vector3.Distance(targetBody.position, _mantleLastGoal) > .35f)
                        return AbortMantle("Traversal displaced by collision");
                    _mantleElapsed += Time.fixedDeltaTime;
                    Transform frame = _mantleSupport.transform;
                    Vector3 start = frame.TransformPoint(_mantleStartLocal);
                    Vector3 end = frame.TransformPoint(_mantleEndLocal);
                    Vector3 up = frame.TransformDirection(_mantleUpLocal).normalized;
                    Vector3 landing = frame.TransformPoint(_mantleTopLocal);
                    Vector3 forward = Vector3.ProjectOnPlane(frame.TransformDirection(_mantleForwardLocal), _localUp).normalized;
                    float radius = capsule.radius*Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.z));
                    float minUp = Mathf.Cos(Mathf.Min(maxSlopeAngle,45f)*Mathf.Deg2Rad);
                    // A live collider can keep its handle while its mesh is carved,
                    // resized or tilted. Revalidate contact, not just object lifetime.
                    if (!MantleFootprintSupported(landing,forward,radius,_localUp,minUp,_mantleSurfaceId,_mantleGeneration))
                        return AbortMantle("Support footprint lost");
                    Vector3 goal = ToVector3(EarthMantleMotion.Evaluate(ToFloat3(start), ToFloat3(end), ToFloat3(up), MantleProgress));
                    if (!MantleSegmentClear(targetBody.position, goal, _localUp)) return AbortMantle("Traversal obstructed");
                    _mantleLastGoal = goal;
                    targetBody.linearVelocity = Vector3.ClampMagnitude((goal-targetBody.position)/Time.fixedDeltaTime, 7f);
                    // GravityBody remains the sole gravity provider; neutralize it
                    // only while the motor follows this explicit collision-checked path.
                    targetBody.AddForce(-_lastGravityAcceleration, ForceMode.Acceleration);
                    IsGrounded = false;
                    _movingSupportTicks = 0;
                    _jumpWindow = default;
                    if (_mantleElapsed >= _mantleDuration)
                    {
                        IsMantling = false;
                        _mantleNextAttempt = Time.fixedTime+.35f;
                        _ignoreGroundTicks = 0;
                        SuppressLandingRoll(.3f);
                    }
                    return true;
                }
                if (feelProfile != null && !feelProfile.AutoMantle) return false;
                bool supportedApproach = HasStableSupport;
                bool airborneApproach = !supportedApproach && !IsGrounded;
                bool intent = LastCommand.Move.y >= .6f && !LastCommand.JumpPressed &&
                    (supportedApproach || airborneApproach) &&
                    !physical && !_landingRoll.Active && _castBrace01 < .05f;
                _mantleIntent = intent ? _mantleIntent+Time.fixedDeltaTime : 0;
                float admissionHold = airborneApproach ? .04f : .12f;
                if (_mantleIntent < admissionHold || Time.fixedTime < _mantleNextAttempt) return false;
                _mantleNextAttempt = Time.fixedTime+.10f;
                return TryBeginMantle();
            }
        }

        private bool TryBeginMantle()
        {
            bool airborneCatch = !HasStableSupport;
            Vector3 up = _localUp;
            Vector3 forward = Vector3.ProjectOnPlane(tankSteering ? FacingForward :
                cameraFrame != null ? cameraFrame.forward : FacingForward, up).normalized;
            if (Vector3.Dot(forward, FacingForward) < .65f) return false;
            float radius = capsule.radius*Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.z));
            float minHeight = feelProfile != null ? feelProfile.MantleMinimumHeight : .35f;
            float maxHeight = feelProfile != null ? feelProfile.MantleMaximumHeight : 1.35f;
            float reach = feelProfile != null ? feelProfile.MantleReach : .60f;
            Vector3 feet = FeetPoint(up);
            if (!MantleRay(feet+up*(minHeight*.65f), forward, radius+reach, out var wall)) return false;
            if (Mathf.Abs(Vector3.Dot(wall.normal,up)) > .45f || Vector3.Dot(wall.normal,forward) > -.55f) return false;
            // Land the feet far enough onto the top that a trailing idle foot is
            // still supported after the body settles. Keep the separately stored
            // ledge point at the physical lip for hand placement.
            Vector3 topOrigin = wall.point+forward*(radius+.25f);
            topOrigin += up*(Vector3.Dot(feet-topOrigin,up)+maxHeight+.25f);
            if (!MantleRay(topOrigin,-up,maxHeight+.3f,out var top)) return false;
            float height = Vector3.Dot(top.point-feet,up);
            var support = CharacterSupportRuntimeAdapter.Classify(top.collider,0,Vector3.Dot(top.normal,up));
            Vector3 end = targetBody.position + (top.point-feet) + up*.025f;
            float minUp = Mathf.Cos(Mathf.Min(maxSlopeAngle,45f)*Mathf.Deg2Rad);
            bool destinationClear = support.IsWalkable && MantleCapsuleClear(end,up);
            bool admitted;
            if (airborneCatch)
            {
                Vector3 supportVelocity = Vector3.zero;
                IMovingSurface movingSurface = top.collider.GetComponentInParent(
                    typeof(IMovingSurface)) as IMovingSurface;
                if (movingSurface != null)
                {
                    SupportFrameSnapshot supportFrame = movingSurface.SupportFrame;
                    if (supportFrame.IsValid)
                        supportVelocity = ToVector3(supportFrame.VelocityAt(ToFloat3(top.point)));
                }
                else if (top.rigidbody != null)
                    supportVelocity = top.rigidbody.GetPointVelocity(top.point);
                float relativeUpSpeed = Vector3.Dot(targetBody.linearVelocity-supportVelocity,up);
                float airborneMinimumHeight = Mathf.Max(.12f,minHeight*.45f);
                admitted = EarthMantleMotion.CanCatchAirborne(
                    LastCommand.Move.y,false,relativeUpSpeed,height,airborneMinimumHeight,maxHeight,
                    Vector3.Dot(top.normal,up),minUp,destinationClear);
            }
            else
            {
                admitted = EarthMantleMotion.CanStart(
                    LastCommand.Move.y,true,false,height,minHeight,maxHeight,
                    Vector3.Dot(top.normal,up),minUp,destinationClear);
            }
            if (!admitted) return false;
            // Require support across the destination footprint, not a single
            // ray perched on a narrow ledge or bridging a hole.
            if (!MantleFootprintSupported(top.point,forward,radius,up,minUp,support.SurfaceId,support.Generation)) return false;
            Vector3 lift = targetBody.position+up*(height+.085f);
            if (!MantleSegmentClear(targetBody.position,lift,up) || !MantleSegmentClear(lift,end+up*.06f,up)) return false;
            _mantleSupport = top.collider;
            _mantleSurfaceId = support.SurfaceId; _mantleGeneration = support.Generation;
            Transform frame = top.collider.transform;
            _mantleStartLocal = frame.InverseTransformPoint(targetBody.position);
            _mantleEndLocal = frame.InverseTransformPoint(end);
            _mantleUpLocal = frame.InverseTransformDirection(up);
            _mantleLedgeLocal = frame.InverseTransformPoint(top.point-forward*(radius+.22f));
            _mantleTopLocal = frame.InverseTransformPoint(top.point);
            _mantleForwardLocal = frame.InverseTransformDirection(forward);
            _mantleDuration = feelProfile != null ? feelProfile.MantleDuration : 1.2f;
            _mantleElapsed = 0; _mantleLastGoal = targetBody.position;
            IsMantling = true; MantleSequence++; MantleStartedAirborne = airborneCatch; MantleLastRejection = null;
            targetBody.linearVelocity = Vector3.zero;
            _landingRoll.Cancel(); _jumpWindow = default;
            return true;
        }

        private bool MantleRay(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            int count = UnityEngine.Physics.RaycastNonAlloc(origin,direction,_mantleHits,distance,groundMask,QueryTriggerInteraction.Ignore);
            hit = default; float nearest = float.PositiveInfinity;
            if(count == _mantleHits.Length) return false;
            for(int i=0;i<count;i++)
            {
                var candidate=_mantleHits[i];
                if(MantleSelf(candidate.collider) || candidate.distance>=nearest) continue;
                nearest=candidate.distance;hit=candidate;
            }
            return hit.collider!=null;
        }

        private bool MantleFootprintSupported(Vector3 top,Vector3 forward,float radius,Vector3 up,float minUp,uint surfaceId,uint generation)
        {
            if (forward.sqrMagnitude < .5f) return false;
            Vector3 right=Vector3.Cross(up,forward);
            // A downhill footprint sample can be radius*tan(slope) below the
            // center contact. A fixed .15m downward budget rejected valid slopes.
            float slopeRadians=Mathf.Acos(Mathf.Clamp(minUp,.05f,1f));
            float footprintHeight=radius*.85f*Mathf.Tan(slopeRadians)+.05f;
            float probeAbove=Mathf.Max(.25f,footprintHeight+.05f);
            float probeDistance=probeAbove+footprintHeight;
            for(int i=0;i<5;i++)
            {
                Vector3 offset=(i==0 ? Vector3.zero : i==1 ? -right : i==2 ? right : i==3 ? forward : -forward)*radius*.85f;
                if(!MantleRay(top+offset+up*probeAbove,-up,probeDistance,out var hit)) return false;
                var candidate=CharacterSupportRuntimeAdapter.Classify(hit.collider,0,Vector3.Dot(hit.normal,up));
                if(!candidate.IsWalkable || candidate.SurfaceId!=surfaceId || candidate.Generation!=generation || Vector3.Dot(hit.normal,up)<minUp) return false;
                if(i==0 && Mathf.Abs(Vector3.Dot(hit.point-top,up))>.045f) return false;
            }
            return true;
        }

        private void MantleCapsule(Vector3 bodyPosition, Vector3 up, out Vector3 a,out Vector3 b,out float radius)
        {
            const float skin = .015f;
            radius = Mathf.Max(.01f,capsule.radius*Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.z))-skin);
            float half = Mathf.Max(radius,capsule.height*Mathf.Abs(transform.lossyScale.y)*.5f-skin);
            Vector3 center = bodyPosition + transform.TransformVector(capsule.center);
            a=center+up*(half-radius); b=center-up*(half-radius);
        }
        private bool MantleCapsuleClear(Vector3 position,Vector3 up)
        {
            MantleCapsule(position,up,out var a,out var b,out var r);
            int count=UnityEngine.Physics.OverlapCapsuleNonAlloc(a,b,r,_mantleOverlaps,groundMask,QueryTriggerInteraction.Ignore);
            if(count==_mantleOverlaps.Length) return false;
            for(int i=0;i<count;i++) if(!MantleSelf(_mantleOverlaps[i])) return false;
            return true;
        }
        private bool MantleSegmentClear(Vector3 start,Vector3 end,Vector3 up)
        {
            Vector3 delta=end-start; float distance=delta.magnitude;
            // Sweeps miss pre-existing overlaps, including an obstacle inserted
            // during the stationary reach phase. Always validate the endpoint.
            if(!MantleCapsuleClear(end,up)) return false;
            if(distance<.00001f) return true;
            MantleCapsule(start,up,out var a,out var b,out var r);
            int count=UnityEngine.Physics.CapsuleCastNonAlloc(a,b,r,delta/distance,_mantleHits,distance,groundMask,QueryTriggerInteraction.Ignore);
            if(count==_mantleHits.Length) return false;
            for(int i=0;i<count;i++) if(!MantleSelf(_mantleHits[i].collider) && _mantleHits[i].distance<distance-.008f) return false;
            return true;
        }
        private bool MantleSelf(Collider c) => c==null || c==capsule || c.attachedRigidbody==targetBody ||
            c.transform.IsChildOf(transform) || (_puppet!=null && _puppet.OwnsCollider(c));
        private bool AbortMantle(string reason) { MantleLastRejection=reason; CancelMantle(); return false; }
        public void CancelMantle() { IsMantling=false; _mantleSupport=null; _mantleIntent=0; _mantleNextAttempt=Time.fixedTime+.4f; }
    }
}
