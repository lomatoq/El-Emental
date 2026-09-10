using System;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Fire;
using UnityEngine;
namespace Elemental.Runtime.Fire
{
    public sealed partial class FireAbilityController
    {
        private struct Source
        {
            public uint Id;public Collider Surface;public IEarthPhysicalTarget Target;public EarthPhysicalTargetHandle Handle;
            public Vector3 Point,Normal;public float Expires,LastReaction;
        }
        private readonly Source[] sources=new Source[FireWeaveTuning.SourceCapacity];
        private readonly Rigidbody[] guardedBodies=new Rigidbody[512];
        private readonly Collider[] guardOverlaps=new Collider[512];
        private float boltChargeAge,nextReaction,nextSourceTick,nextPaintTick,nextGuardTick,lastDrill=-10;
        private Vector3 chargedAim,contourLast;private bool contourValid;
        private Vector3 reactionFrom;private Func<Collider,Vector3,bool> reactionVisibility;
        private bool ReactionVisible(Collider surface,Vector3 point)=>Unobstructed(reactionFrom,point,surface);
        public bool WeaveHeld {get;private set;}
        public Vector3 WeaveAimDirection {get;private set;}=Vector3.forward;
        public FireWeaveForm WeaveForm {get;private set;}
        public float WeavePower01 {get;private set;}=1f/3;
        public float WeaveRecovery01=>Mathf.Clamp01((clock-lastDrill)/.45f);
        public bool IsBoltCharging {get;private set;}
        public uint ChargingBoltId {get;private set;}
        public float BoltCharge01=>Mathf.Clamp01(boltChargeAge/FireWeaveTuning.ChargeSeconds);
        public int SourceCount {get {int n=0;for(int i=0;i<sources.Length;i++)if(SourceValid(sources[i]))n++;return n;}}
        public int Amplifications {get;private set;}
        public int GuardDeflections {get;private set;}
        public int RecycledSources {get;private set;}
        public void SetWeaveHeld(bool held,FireWeaveForm form,float power01)
        {WeaveHeld=held&&IsAvailable&&clock>=waveRecoveryUntil;WeaveForm=form;WeavePower01=float.IsFinite(power01)?Mathf.Clamp01(power01):1f/3;}
        public void SetChargedAim(Vector3 aim){if(Finite(aim)){chargedAim=aim;Vector3 delta=aim-hand.position;if(delta.sqrMagnitude>.001f)WeaveAimDirection=delta.normalized;}}
        public bool BeginBoltCharge(Vector3 aim)
        {
            if(!IsAvailable||IsBoltCharging||!Finite(aim)||clock-lastShot<.12f)return false;
            bool free=false;for(int i=0;i<bolts.Length;i++)free|=!bolts[i].Active&&!bolts[i].Pending;
            if(!free)return false;
            IsBoltCharging=true;boltChargeAge=0;chargedAim=aim;ChargingBoltId=NextId();
            Cue(FireAbilityEffectKind.HandWindup,hand.position,(aim-hand.position).normalized,LocalUp,1,FireWeaveTuning.ChargeSeconds,ChargingBoltId);return true;
        }
        public void CancelBoltCharge(){IsBoltCharging=false;boltChargeAge=0;ChargingBoltId=0;}
        public bool ReleaseBoltCharge()
        {
            if(!IsAvailable||!IsBoltCharging)return false;
            float charge=BoltCharge01;uint id=ChargingBoltId;Vector3 aim=chargedAim;CancelBoltCharge();
            var profile=FireChargedBoltProfile.Evaluate(charge);
            return Launch(aim,profile.Power,profile.Speed,false,id);
        }
        public bool TryRapidShot(Vector3 aim)=>WeaveHeld&&Launch(aim,.55f,38,false);
        public bool TryDrill(Vector3 aim)
        {if(!WeaveHeld||!Launch(aim,2.3f+WeavePower01,48,true))return false;lastDrill=clock;return true;}
        private bool Launch(Vector3 aim,float power,float speed,bool drill,uint id=0)
        {
            if(!IsAvailable||!Finite(aim)||clock-lastShot<.12f)return false;
            int slot=-1;for(int i=0;i<bolts.Length;i++)if(!bolts[i].Active&&!bolts[i].Pending){slot=i;break;}
            if(slot<0)return false;
            Vector3 origin=hand.position;
            if(id!=0)origin=ChargedBoltMuzzle(aim,FireChargedBoltProfile.FromPower(power),out _);
            if(WeaveHeld&&WeaveForm>=FireWeaveForm.Orbit)
            {
                Vector3 direction=Vector3.ProjectOnPlane(aim-root.position,LocalUp).normalized;
                if(direction.sqrMagnitude>.5f)
                {
                    Vector3 candidate=root.position+LocalUp+direction*1.8f;
                    // The ring muzzle may not teleport a shot beyond a wall.
                    if(ClearMuzzle(origin,candidate,.2f*power))origin=candidate;
                }
            }
            Vector3 forward=(aim-origin).normalized;if(forward.sqrMagnitude<.9f)return false;
            bool immediate=id==0;if(immediate)id=NextId();
            bolts[slot]=new Projectile{Id=id,Active=true,PosePending=true,ChargedMuzzle=!immediate,OrbitMuzzle=WeaveHeld&&WeaveForm>=FireWeaveForm.Orbit,Position=origin,Direction=forward,Aim=aim,Power=power,Speed=speed,Drill=drill};
            lastShot=clock;Shots++;
            if(immediate)Cue(FireAbilityEffectKind.HandWindup,origin,forward,LocalUp,power,.08f,id);
            // The fresh HandBolt cue is published only after final-pose muzzle validation.
            return true;
        }
        private bool ClearMuzzle(Vector3 from,Vector3 to,float radius)
        {
            int n=UnityEngine.Physics.OverlapSphereNonAlloc(from,radius,overlaps,mask,QueryTriggerInteraction.Ignore);
            if(n==overlaps.Length){QuerySaturations++;return false;}
            for(int i=0;i<n;i++)if(!Self(overlaps[i]))return false;
            Vector3 delta=to-from;if(delta.sqrMagnitude<.000001f)return true;n=UnityEngine.Physics.SphereCastNonAlloc(from,radius,delta.normalized,hits,delta.magnitude,mask,QueryTriggerInteraction.Ignore);
            if(n==hits.Length){QuerySaturations++;return false;}
            for(int i=0;i<n;i++)if(!Self(hits[i].collider))return false;return true;
        }
        public void BeginContour(){contourValid=false;}
        public bool TraceContour(Vector3 point)
        {
            if(!IsAvailable||!Finite(point)||Vector3.Distance(point,root.position)>12)return false;
            float distance=contourValid?Vector3.Distance(point,contourLast):0;
            if(contourValid&&distance<FireWeaveTuning.SourceSpacing)return false;
            // A pointer jump starts a new stroke; never fill a hidden bridge across a cliff.
            int steps=contourValid&&distance<4?Mathf.Clamp(Mathf.CeilToInt(distance/FireWeaveTuning.SourceSpacing),1,6):1;
            Vector3 start=contourValid?contourLast:point;bool added=false;
            for(int i=1;i<=steps;i++)
            {
                Vector3 sample=steps==1?point:Vector3.Lerp(start,point,i/(float)steps);
                if(!TryGround(sample,LocalUp,out RaycastHit h)){contourValid=false;continue;}
                if(!Unobstructed(hand.position,h.point+h.normal*.03f,h.collider))continue;
                added|=AddSource(h,(point-start)/steps);
            }
            contourLast=point;contourValid=added;return added;
        }
        public void PaintContact(RaycastHit hit)
        {
            if(!IsAvailable||clock<nextPaintTick||Self(hit.collider)||Vector3.Distance(hit.point,hand.position)>12)return;
            nextPaintTick=clock+.08f;
            if(!Unobstructed(hand.position,hit.point,hit.collider))return;
            impact.ApplyContact(hit.collider,hit.point,hit.normal,(hit.point-hand.position).normalized,.08f,1.5f);
        }
        private bool AddSource(RaycastHit hit,Vector3 span)
        {
            int slot=-1;float oldest=float.PositiveInfinity;
            for(int i=0;i<sources.Length;i++)if(SourceValid(sources[i])&&Vector3.Distance(SourcePoint(sources[i]),hit.point)<.45f)return false;
            for(int i=0;i<sources.Length;i++)
            {
                if(!SourceValid(sources[i])){slot=i;break;}
                if(sources[i].Expires<oldest){oldest=sources[i].Expires;slot=i;}
            }
            if(slot<0)return false;if(SourceValid(sources[slot]))RecycledSources++;
            var support=hit.collider.GetComponentInParent<IEarthPhysicalTarget>();uint id=NextId();
            sources[slot]=new Source{Id=id,Surface=hit.collider,Target=support,Handle=support!=null?support.TargetHandle:default,
                Point=hit.collider.transform.InverseTransformPoint(hit.point),Normal=hit.collider.transform.InverseTransformDirection(hit.normal),Expires=clock+FireWeaveTuning.SourceLife,LastReaction=-10};
            Cue(FireAbilityEffectKind.GroundFlame,hit.point,Vector3.ClampMagnitude(span,1),hit.normal,1,FireWeaveTuning.SourceLife,id);return true;
        }
        private bool SourceValid(Source s)=>s.Id!=0&&s.Expires>clock&&s.Surface!=null&&s.Surface.enabled&&s.Surface.gameObject.activeInHierarchy&&
            (!(s.Target is UnityEngine.Object obj)||obj!=null)&&(s.Target==null||(s.Target.TargetHandle.StableId==s.Handle.StableId&&s.Target.TargetHandle.Generation==s.Handle.Generation));
        private static Vector3 SourcePoint(Source s)=>s.Surface.transform.TransformPoint(s.Point);
        private bool TryGetSourcePose(uint id,out Vector3 point,out Vector3 up)
        {
            for(int i=0;i<sources.Length;i++)if(sources[i].Id==id&&SourceValid(sources[i]))
            {point=SourcePoint(sources[i]);up=sources[i].Surface.transform.TransformDirection(sources[i].Normal).normalized;return true;}
            point=default;up=LocalUp;return false;
        }
        private void CancelWeave(){WeaveHeld=false;CancelBoltCharge();ClearSphereWave();Array.Clear(sources,0,sources.Length);contourValid=false;}
        private void StepWeave(float dt)
        {
            if(IsBoltCharging)boltChargeAge=Mathf.Min(FireWeaveTuning.ChargeSeconds,boltChargeAge+dt);
            if(clock>=nextSourceTick)
            {
                nextSourceTick=clock+.1f;bool hurt=false;
                for(int i=0;i<sources.Length;i++)
                {
                    if(!SourceValid(sources[i])){sources[i]=default;continue;}
                    var s=sources[i];Vector3 p=SourcePoint(s),up=s.Surface.transform.TransformDirection(s.Normal).normalized;
                    // A drawn contour supplies flame/catalyst energy. It must not stamp
                    // soot and remesh its supporting floor ten times per second per node.
                    int n=UnityEngine.Physics.OverlapCapsuleNonAlloc(p+up*.15f,p+up*1.1f,.55f,overlaps,mask,QueryTriggerInteraction.Ignore);
                    if(n==overlaps.Length){QuerySaturations++;continue;}
                    for(int j=0;j<n;j++)
                    {
                        var c=overlaps[j];if(Self(c)||c==s.Surface)continue;if(!FireColliderSurface.TryPoint(c,p+up*.4f,up,1.4f,out Vector3 contact))continue;
                        if(Vector3.Dot(contact-p,up)<.12f)continue;
                        if(!Unobstructed(p+up*.1f,contact,c))continue;
                        impact.ApplyContact(c,contact,up,up,.1f,1.2f);
                        if(!hurt&&c.transform.IsChildOf(rival)){Damage(2,up*.12f);hurt=true;}
                    }
                }
            }
            if(session.IsActive)TryAmplify(session.MuzzlePosition,session.MuzzlePosition+(session.AimPoint-session.MuzzlePosition).normalized*session.CurrentLength,session.Power);
            if(WeaveHeld&&WeaveForm>=FireWeaveForm.Orbit&&WeaveRecovery01>.2f)StepGuard(dt);
        }
        private void StepGuard(float dt)
        {
            Vector3 center=root.position+LocalUp;float radius=WeaveForm==FireWeaveForm.Sphere?2.3f:2.1f;
            float lookAhead=Mathf.Min(4,120*dt);
            int n=UnityEngine.Physics.OverlapSphereNonAlloc(center,radius+.6f+lookAhead,guardOverlaps,mask,QueryTriggerInteraction.Ignore);
            if(n==guardOverlaps.Length){QuerySaturations++;return;}
            bool heat=clock>=nextGuardTick,hurt=false;int bodies=0;
            if(heat)nextGuardTick=clock+.1f;
            for(int i=0;i<n;i++)
            {
                var c=guardOverlaps[i];if(Self(c))continue;if(!heat&&!FireColliderSurface.SupportsClosestPoint(c))continue;
                if(!FireColliderSurface.TryPoint(c,center,-LocalUp,radius+.6f+lookAhead,out Vector3 point))continue;Vector3 offset=point-center;
                if(WeaveForm==FireWeaveForm.Orbit&&Mathf.Abs(Vector3.Dot(offset,LocalUp))>.75f)continue;
                var body=c.attachedRigidbody;
                bool reaches=body!=null&&!body.isKinematic&&FireWeaveTuning.SegmentDistanceSquared(center,point,point+(body.linearVelocity-OwnerVelocity)*dt)<(radius+.2f)*(radius+.2f);
                if(offset.magnitude>=radius+.2f&&!reaches)continue;
                if(!Unobstructed(center,point,c))continue;
                Vector3 radial=offset.sqrMagnitude>.001f?offset.normalized:LocalUp;
                if(body!=null&&!body.isKinematic)
                {
                    bool seen=false;for(int j=0;j<bodies;j++)seen|=guardedBodies[j]==body;
                    if(!seen)
                    {
                        guardedBodies[bodies++]=body;float incoming=Vector3.Dot(body.linearVelocity-OwnerVelocity,radial);
                        if(incoming<-.2f&&reaches)
                        {body.AddForce(radial*Mathf.Min(140,8-incoming),ForceMode.VelocityChange);GuardDeflections++;}
                    }
                }
                if(heat&&offset.magnitude<radius+.2f)
                {
                    impact.ApplyContact(c,point,-radial,radial,.1f,2,thermalShock:true);
                    if(!hurt&&c.transform.IsChildOf(rival)){Damage(3,radial*1.4f);hurt=true;}
                }
            }
            Array.Clear(guardedBodies,0,bodies);
        }
        private bool TryAmplify(Vector3 from,Vector3 to,float power)
        {
            if(clock<nextReaction)return false;
            for(int i=0;i<sources.Length;i++)
            {
                var s=sources[i];if(!SourceValid(s))continue;Vector3 p=SourcePoint(s),up=s.Surface.transform.TransformDirection(s.Normal).normalized;
                if(!FireWeaveTuning.CanReact(clock,s.LastReaction,p+up*.45f,from,to,.9f)||!Unobstructed(from,p+up*.1f,s.Surface))continue;
                s.LastReaction=clock;sources[i]=s;nextReaction=clock+FireWeaveTuning.ReactionCooldown;Amplifications++;
                Hit(s.Surface,p,up,(to-from).sqrMagnitude>.001f?(to-from).normalized:up,Mathf.Clamp(power*1.15f,.8f,2.5f));return true;
            }
            reactionFrom=from;
            if(impact.TryConsumeIgnitionReaction(from,to,reactionVisibility,out FireSurfaceContact contact))
            {
                nextReaction=clock+FireWeaveTuning.ReactionCooldown;Amplifications++;
                Hit(contact.Surface,contact.Point,contact.Normal,(to-from).sqrMagnitude>.001f?(to-from).normalized:contact.Normal,Mathf.Clamp(power,.8f,2.5f));return true;
            }
            return false;
        }
    }
}
