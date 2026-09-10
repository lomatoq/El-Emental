using System;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Fire;
using Unity.Profiling;
using UnityEngine;
namespace Elemental.Runtime.Fire
{
    [DefaultExecutionOrder(-90),DisallowMultipleComponent]
    public sealed partial class FireAbilityController:MonoBehaviour
    {
        public struct Projectile {public bool Active,Pending,Kick,Drill,Amplified,PosePending,ChargedMuzzle,OrbitMuzzle;public uint Id;public Vector3 Position,Direction,Aim;public float Age,Power,Speed;}
        private struct GroundNode {public uint Id;public Collider Surface;public Vector3 LocalPoint,LocalNormal;public float Age;public bool Announced;public Vector3 Span;public Elemental.Runtime.Physics.IEarthPhysicalTarget Target;public Elemental.Runtime.Physics.EarthPhysicalTargetHandle Handle;}
        private static readonly ProfilerMarker Marker=new("Elemental.Fire.Abilities.Fixed");
        private FireStreamSession session;private EarthMvpDuelController duel;private EarthDuelFighterId owner,target;
        private PlanetMotor rivalMotor;private PlanetMotor motor;private Transform root,rival,hand,foot,leftFoot;private FireWorldImpact impact;
        private readonly Projectile[] bolts=new Projectile[FireAbilityTuning.MaximumProjectiles];
        private readonly GroundNode[] ground=new GroundNode[FireAbilityTuning.MaximumGroundNodes];
        private readonly Collider[] overlaps=new Collider[128],ringTouched=new Collider[512];
        private readonly RaycastHit[] hits=new RaycastHit[64];
        private int mask,ringCount;private bool selected,liftHeld,ringActive,ringHitFighter;
        private float clock,liftAge,lastShot=-10,lastRing=-10,lastLine=-10,ringAge,ringPrevious;
        private Vector3 ringCenter,ringUp;private uint serial;
        public event Action<FireAbilityCue> Effect;
        public bool IsAvailable=>selected&&session!=null&&session.IsAvailable;
        public Transform OwnerRoot=>root;
        public Vector3 OwnerVelocity=>motor!=null&&motor.Body!=null?motor.Body.linearVelocity:Vector3.zero;
        public Vector3 LocalUp=>motor!=null?motor.LocalUp:Vector3.up;
        public bool IsRingCharging {get;private set;}
        private float ringChargeAge,ringPower=1;
        public float RingCharge01=>Mathf.Clamp01(ringChargeAge/FireAbilityTuning.RingChargeSeconds);
        public bool IsLifting=>IsAvailable&&liftHeld;
        public float LiftCharge=>FireAbilityTuning.LiftCharge(liftAge);
        public int Shots {get;private set;} public int FootShots {get;private set;}public int Rings {get;private set;}public int Lines {get;private set;}
        public int QuerySaturations {get;private set;}
        public int ProjectileCapacity=>bolts.Length;
        public Projectile GetProjectile(int index)=>bolts[index];
        public bool TryGetGroundPose(uint id,out Vector3 point,out Vector3 up)
        {
            for(int i=0;i<ground.Length;i++)if(ground[i].Id==id&&GroundValid(ground[i]))
            {point=ground[i].Surface.transform.TransformPoint(ground[i].LocalPoint);up=ground[i].Surface.transform.TransformDirection(ground[i].LocalNormal).normalized;return true;}
            return TryGetSourcePose(id,out point,out up);
        }
        public void Configure(FireStreamSession stream,EarthMvpDuelController match,EarthDuelFighterId fighter,PlanetMotor actorMotor,
            Transform handMuzzle,Transform footMuzzle,FireWorldImpact worldImpact,int collisionMask,Transform leftFootMuzzle=null)
        {
            session=stream??throw new ArgumentNullException(nameof(stream));duel=match??throw new ArgumentNullException(nameof(match));
            motor=actorMotor??throw new ArgumentNullException(nameof(actorMotor));hand=handMuzzle;foot=footMuzzle;leftFoot=leftFootMuzzle??footMuzzle;
            impact=worldImpact??throw new ArgumentNullException(nameof(worldImpact));owner=fighter;target=owner==EarthDuelFighterId.Player?EarthDuelFighterId.Bot:EarthDuelFighterId.Player;
            root=owner==EarthDuelFighterId.Player?duel.PlayerTransform:duel.BotTransform;rival=owner==EarthDuelFighterId.Player?duel.BotTransform:duel.PlayerTransform;
            rivalMotor=rival.GetComponentInChildren<PlanetMotor>(true);
            mask=collisionMask;ConfigurePoseCommitter();reactionVisibility=ReactionVisible;duel.RoundRestarted+=CancelAll;
        }
        public void SetSelected(bool value){selected=value;if(!value)CancelAll();}
        public void SetLiftHeld(bool held){liftHeld=held&&IsAvailable;if(!liftHeld){liftAge=0;motor?.SetFireLift(0,false);}}
        public void CancelControls(){SetLowFlightHeld(false);liftHeld=false;liftAge=0;motor?.SetFireLift(0,false);}
        public void CancelAll()
        {CancelControls();CancelWeave();Array.Clear(bolts,0,bolts.Length);Array.Clear(ground,0,ground.Length);Array.Clear(ringTouched,0,ringTouched.Length);ringActive=false;IsRingCharging=false;ringChargeAge=0;ringCount=0;}
        private uint NextId(){serial++;if(serial==0)serial=1;return serial;}
        private void Cue(FireAbilityEffectKind kind,Vector3 position,Vector3 direction,Vector3 up,float power,float lifetime,uint id=0)
        {Effect?.Invoke(new FireAbilityCue(id==0?NextId():id,kind,position,direction,up,power,lifetime));}
        public bool TryShoot(Vector3 aim,bool kick)
        {
            if(!IsAvailable||clock-lastShot<.12f||!Finite(aim))return false;
            int slot=-1;for(int i=0;i<bolts.Length;i++)if(!bolts[i].Active&&!bolts[i].Pending){slot=i;break;}if(slot<0)return false;
            Vector3 origin=kick?foot.position+motor.LocalUp*.08f:hand.position;
            Vector3 direction=(aim-origin).normalized;if(direction.sqrMagnitude<.9f)return false;
            // Spawn at the actual limb, sweep every subsequent displacement including the first.
            uint id=NextId();bolts[slot]=new Projectile{Pending=true,Kick=kick,Aim=aim,Id=id,Position=origin,Direction=direction,Power=kick?1.35f:1,Speed=FireAbilityTuning.ProjectileSpeed};
            lastShot=clock;Shots++;if(kick)FootShots++;
            Cue(kick?FireAbilityEffectKind.FootWindup:FireAbilityEffectKind.HandWindup,origin,direction,motor.LocalUp,kick?1.35f:1,kick?FireAbilityTuning.FootChargeSeconds:FireAbilityTuning.HandChargeSeconds,id);
            return true;
        }
        public bool TryRing()
        {
            if(!IsAvailable||ringActive||IsRingCharging||clock-lastRing<1.5f)return false;
            IsRingCharging=true;ringChargeAge=0;ringAge=ringPrevious=0;ringCount=0;ringHitFighter=false;ringCenter=root.position;ringUp=motor.LocalUp;lastRing=clock;Rings++;
            Array.Clear(ringTouched,0,ringTouched.Length);Cue(FireAbilityEffectKind.RingWindup,ringCenter,ringUp,ringUp,1,FireAbilityTuning.RingChargeSeconds);return true;
        }
        public bool ReleaseRing()
        {
            if(!IsAvailable||!IsRingCharging)return false;
            ringPower=1+RingCharge01*.65f;IsRingCharging=false;ringActive=true;ringAge=ringPrevious=0;
            ringCenter=root.position;ringUp=motor.LocalUp;
            Cue(FireAbilityEffectKind.Ring,ringCenter,ringUp,ringUp,ringPower,FireAbilityTuning.RingSeconds);return true;
        }
        public bool TryGroundLine(Vector3 start,Vector3 end)
        {
            if(!IsAvailable||clock-lastLine<.8f||!Finite(start)||!Finite(end))return false;
            Vector3 up=motor.LocalUp;start=root.position+Vector3.ClampMagnitude(start-root.position,12);end=root.position+Vector3.ClampMagnitude(end-root.position,12);
            if(Vector3.Distance(start,end)<.7f)return false;
            // Validate every segment first. A cliff/wall does not become an unsupported fire bridge.
            var nodes=ground;int valid=0;
            for(int i=0;i<nodes.Length;i++)
            {
                Vector3 sample=Vector3.Lerp(start,end,i/(float)(nodes.Length-1));
                if(TryGround(sample,up,out RaycastHit hit))valid++;
            }
            if(valid<2)return false;
            Array.Clear(ground,0,ground.Length);
            for(int i=0;i<ground.Length;i++)
            {
                Vector3 sample=Vector3.Lerp(start,end,i/(float)(ground.Length-1));
                if(!TryGround(sample,up,out RaycastHit hit))continue;
                var support=hit.collider.GetComponentInParent<Elemental.Runtime.Physics.IEarthPhysicalTarget>();
                uint id=NextId();ground[i]=new GroundNode{Id=id,Age=-i*FireAbilityTuning.GroundNodeDelay,Span=(end-start)/(ground.Length-1),Surface=hit.collider,Target=support,Handle=support!=null?support.TargetHandle:default,LocalPoint=hit.collider.transform.InverseTransformPoint(hit.point),LocalNormal=hit.collider.transform.InverseTransformDirection(hit.normal)};

            }
            lastLine=clock;Lines++;return true;
        }
        private bool TryGround(Vector3 sample,Vector3 up,out RaycastHit nearest)
        {
            nearest=default;int count=UnityEngine.Physics.RaycastNonAlloc(sample+up*5,-up,hits,12,mask,QueryTriggerInteraction.Ignore);float distance=float.MaxValue;
            if(count==hits.Length){QuerySaturations++;return false;}
            for(int i=0;i<count;i++)if(!Self(hits[i].collider)&&Vector3.Dot(hits[i].normal,up)>.45f&&hits[i].distance<distance){nearest=hits[i];distance=hits[i].distance;}
            return nearest.collider!=null;
        }
        private void FixedUpdate()
        {
            if(session==null)return;if(!IsAvailable){CancelAll();return;}
            using var marker=Marker.Auto();float dt=Time.fixedDeltaTime;clock+=dt;
            if(liftHeld){liftAge+=dt;motor.SetFireLift(LiftCharge,true);ApplyFootHeat(foot,dt);if(leftFoot!=foot)ApplyFootHeat(leftFoot,dt);}else motor.SetFireLift(0,false);
            StepLowFlight();
            for(int i=0;i<bolts.Length;i++)if(bolts[i].Pending)
            {
                var b=bolts[i];b.Age+=dt;
                if(b.Age>=(b.Kick?FireAbilityTuning.FootChargeSeconds:FireAbilityTuning.HandChargeSeconds))
                {
                    b.Pending=false;b.Active=true;b.PosePending=true;b.Age=0;
                    b.Position=b.Kick?foot.position+motor.LocalUp*.08f:hand.position;
                    b.Direction=(b.Aim-b.Position).normalized;
                    // Final animated limb pose is committed in LateUpdate before first travel or emission.
                }
                bolts[i]=b;
            }
            for(int i=0;i<bolts.Length;i++)if(bolts[i].Active&&!bolts[i].PosePending)
            {
                var b=bolts[i];float travel=b.Speed*dt;Vector3 previous=b.Position;
                int n=UnityEngine.Physics.OverlapSphereNonAlloc(b.Position,.2f*b.Power,overlaps,mask,QueryTriggerInteraction.Ignore);
                Collider touching=null;for(int k=0;k<n;k++)if(!Self(overlaps[k])){touching=overlaps[k];break;}
                if(n==overlaps.Length){QuerySaturations++;b.Active=false;}
                else if(touching!=null){Hit(touching,b.Position,-b.Direction,b.Direction,b.Power);b.Active=b.Drill&&(touching==null||!touching.enabled||!touching.gameObject.activeInHierarchy);}
                else
                {
                    int count=UnityEngine.Physics.SphereCastNonAlloc(b.Position,.2f*b.Power,b.Direction,hits,travel,mask,QueryTriggerInteraction.Ignore);
                    if(count==hits.Length){QuerySaturations++;b.Active=false;}
                    else
                    {
                        int nearest=-1;float distance=travel;for(int k=0;k<count;k++)if(!Self(hits[k].collider)&&hits[k].distance<=distance){distance=hits[k].distance;nearest=k;}
                        if(nearest>=0){var h=hits[nearest];b.Position=h.point;Hit(h.collider,h.point,h.normal,b.Direction,b.Power);b.Active=b.Drill&&(h.collider==null||!h.collider.enabled||!h.collider.gameObject.activeInHierarchy);}
                        else b.Position+=b.Direction*travel;
                    }
                }
                if(!b.Amplified&&TryAmplify(previous,b.Position,b.Power)){b.Amplified=true;b.Power=Mathf.Min(4.9f,b.Power*1.35f);}
                b.Age+=dt;if(b.Age>=FireAbilityTuning.ProjectileLife)b.Active=false;bolts[i]=b;
            }
            if(IsRingCharging)ringChargeAge=Mathf.Min(FireAbilityTuning.RingChargeSeconds,ringChargeAge+dt);
            if(ringActive)StepRing(dt);
            StepGround(dt);StepWeave(dt);StepSphereWave(dt);
        }
        private void ApplyFootHeat(Transform nozzle,float dt)
        {
            if(nozzle==null)return;Vector3 up=motor.LocalUp;
            int count=UnityEngine.Physics.SphereCastNonAlloc(nozzle.position+up*.06f,.16f,-up,hits,2.8f,mask,QueryTriggerInteraction.Ignore);
            if(count==hits.Length){QuerySaturations++;return;}
            int best=-1;float distance=float.PositiveInfinity;
            for(int i=0;i<count;i++)if(!Self(hits[i].collider)&&hits[i].distance<distance){distance=hits[i].distance;best=i;}
            if(best>=0){var h=hits[best];impact.ApplyContact(h.collider,h.point,h.normal,-up,dt,.8f+LiftCharge*.8f);}
        }

        private void Hit(Collider collider,Vector3 point,Vector3 normal,Vector3 direction,float power)
        {
            impact.ApplyContact(collider,point,normal,direction,.1f,power*4);
            bool hurt=rival!=null&&collider.transform.IsChildOf(rival);
            if(hurt)Damage(20*power,direction*power*7);
            Vector3 center=point+normal*.22f;float radius=2.2f*Mathf.Sqrt(power);
            int count=UnityEngine.Physics.OverlapSphereNonAlloc(center,radius,overlaps,mask,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)QuerySaturations++;
            else for(int i=0;i<count;i++)
            {
                var c=overlaps[i];if(Self(c)||c==collider)continue;if(!FireColliderSurface.TryPoint(c,center,-normal,radius,out Vector3 contact))continue;
                if(!Unobstructed(center,contact,c))continue;
                Vector3 push=(contact-center);if(push.sqrMagnitude<.0001f)push=normal;push.Normalize();
                impact.ApplyContact(c,contact,-push,push,.1f,power*4);
                if(!hurt&&rival!=null&&c.transform.IsChildOf(rival)){hurt=true;Damage(16*power,push*8*power);}
            }
            Cue(FireAbilityEffectKind.Impact,point+normal*.25f,normal,motor.LocalUp,power,.8f);
        }
        private void Damage(float value,Vector3 velocity)
        {if(duel.CanReceiveDamage(target)){rivalMotor?.ApplyFireImpulse(velocity);var handoff=RagdollHandoff.Uniform(velocity);duel.ApplyDamage(target,value,in handoff);}}
        private void StepRing(float dt)
        {
            ringAge+=dt;float radius=FireAbilityTuning.RingExtent(ringAge);
            // Query only the swept front. A filled sphere overflows on authored arena rubble.
            Vector3 side=Vector3.Cross(ringUp,Mathf.Abs(ringUp.y)<.9f?Vector3.up:Vector3.right).normalized;
            for(int sector=0;sector<24;sector++)
            {
                float angle=sector*Mathf.PI*2/24;
                Vector3 radial=side*Mathf.Cos(angle)+Vector3.Cross(ringUp,side)*Mathf.Sin(angle);
                Vector3 from=ringCenter+radial*ringPrevious+ringUp*.6f,to=ringCenter+radial*radius+ringUp*.6f;
                float width=Mathf.Max(.7f,radius*Mathf.Sin(Mathf.PI/24)+.35f);
                int count=UnityEngine.Physics.OverlapCapsuleNonAlloc(from,to,width,overlaps,mask,QueryTriggerInteraction.Ignore);
                if(count==overlaps.Length){QuerySaturations++;continue;}
                for(int i=0;i<count;i++)
                {
                    var c=overlaps[i];if(Self(c))continue;if(!FireColliderSurface.TryPoint(c,to,-ringUp,width+Vector3.Distance(from,to),out Vector3 point))continue;Vector3 offset=point-ringCenter;
                    float distance=Vector3.ProjectOnPlane(offset,ringUp).magnitude;
                    if(Mathf.Abs(Vector3.Dot(offset,ringUp))>2.2f||distance<ringPrevious-.7f||distance>radius+.7f)continue;
                    bool seen=false;for(int j=0;j<ringCount;j++)if(ringTouched[j]==c||(c.attachedRigidbody!=null&&ringTouched[j]!=null&&ringTouched[j].attachedRigidbody==c.attachedRigidbody)){seen=true;break;}
                    if(seen||ringCount>=ringTouched.Length||!Unobstructed(from,point,c))continue;
                    Vector3 direction=(radial+ringUp*.2f).normalized;
                    if(!impact.ApplyContact(c,point,-direction,direction,.1f,4*ringPower,thermalShock:true))continue;
                    ringTouched[ringCount++]=c;
                    if(!ringHitFighter&&rival!=null&&c.transform.IsChildOf(rival)){ringHitFighter=true;Damage(26*ringPower,direction*(9*ringPower));}
                }
            }
            ringPrevious=radius;if(ringAge>=FireAbilityTuning.RingSeconds)ringActive=false;
        }
        private void StepGround(float dt)
        {
            bool hurt=false;
            for(int i=0;i<ground.Length;i++)
            {
                var node=ground[i];if(!GroundValid(node)){ground[i]=default;continue;}node.Age+=dt;
                if(node.Age>FireAbilityTuning.GroundPulseSeconds){ground[i]=default;continue;}
                ground[i]=node;if(node.Age<0)continue;
                Vector3 point=node.Surface.transform.TransformPoint(node.LocalPoint),up=node.Surface.transform.TransformDirection(node.LocalNormal).normalized;
                if(!node.Announced){node.Announced=true;ground[i]=node;Cue(FireAbilityEffectKind.GroundFlame,point,node.Span,up,1,FireAbilityTuning.GroundPulseSeconds,node.Id);}
                if(node.Age>dt&&Mathf.FloorToInt(node.Age/.1f)==Mathf.FloorToInt((node.Age-dt)/.1f))continue;
                float damageStep=Mathf.Min(.1f,Mathf.Max(dt,node.Age));
                impact.ApplyContact(node.Surface,point,up,up,damageStep,4);
                int count=UnityEngine.Physics.OverlapCapsuleNonAlloc(point+up*.1f,point+up*1.4f,.55f,overlaps,mask,QueryTriggerInteraction.Ignore);
                if(count==overlaps.Length)QuerySaturations++;
                else for(int j=0;j<count;j++)
                {
                    var c=overlaps[j];if(Self(c)||c==node.Surface||!FireColliderSurface.TryPoint(c,point+up*.5f,up,1.5f,out Vector3 contact)||!Unobstructed(point+up*.15f,contact,c))continue;
                    impact.ApplyContact(c,contact,up,up,damageStep,4);
                    if(!hurt&&rival!=null&&c.transform.IsChildOf(rival)){Damage(48*damageStep,up*(node.Age<=dt+.001f?6f:0f));hurt=true;}
                }
                ground[i]=node;
            }
        }
        private static bool GroundValid(GroundNode node)
        {
            if(node.Surface==null||!node.Surface.enabled||!node.Surface.gameObject.activeInHierarchy)return false;
            if(node.Target is UnityEngine.Object unityTarget&&unityTarget==null)return false;
            return node.Target==null||(node.Target.TargetHandle.StableId==node.Handle.StableId&&node.Target.TargetHandle.Generation==node.Handle.Generation);
        }
        private bool Unobstructed(Vector3 from,Vector3 to,Collider targetCollider)
        {
            Vector3 delta=to-from;float length=delta.magnitude;if(length<.02f)return true;
            int count=UnityEngine.Physics.RaycastNonAlloc(from,delta/length,hits,length,mask,QueryTriggerInteraction.Ignore);
            if(count==hits.Length){QuerySaturations++;return false;}
            for(int i=0;i<count;i++)
            {
                var c=hits[i].collider;
                if(!Self(c)&&c!=targetCollider&&!(c.attachedRigidbody!=null&&c.attachedRigidbody==targetCollider.attachedRigidbody)&&hits[i].distance<length-.03f)return false;
            }
            return true;
        }
        private bool Self(Collider c)=>c==null||c.transform.IsChildOf(root);
        private static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
        private void OnDisable()=>CancelAll();
        private void OnDestroy(){if(duel!=null)duel.RoundRestarted-=CancelAll;}
    }
}
