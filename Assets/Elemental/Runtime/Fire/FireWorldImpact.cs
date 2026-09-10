using System;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Runtime.Matter;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Fire;
using Elemental.Simulation.Structures;
using Elemental.Simulation.Matter;
using Unity.Profiling;
using UnityEngine;
namespace Elemental.Runtime.Fire
{
    public readonly struct FireSurfaceContact
    {
        public readonly Collider Surface; public readonly Vector3 Point,Normal;
        public readonly float Radius,Energy; public readonly EarthPhysicalTargetHandle Target;
        public FireSurfaceContact(Collider surface,Vector3 point,Vector3 normal,float radius,float energy,EarthPhysicalTargetHandle target)
        {Surface=surface;Point=point;Normal=normal;Radius=radius;Energy=energy;Target=target;}
    }
    [DisallowMultipleComponent]
    public sealed class FireWorldImpact:MonoBehaviour
    {
        private static readonly ProfilerMarker Marker=new("Elemental.Fire.WorldImpact");
        private readonly FireStoneExposure exposure=new();
        private readonly FireStoneExposure structureExposure=new();
        private struct Burning {public Collider Surface;public IEarthPhysicalTarget Target;public EarthPhysicalTargetHandle Handle;public Vector3 Point,Normal;public float Remaining,Dose,LastHeatTick,LastReaction;public bool Announced;}
        private readonly Burning[] burning=new Burning[32];private bool residual;private float burnCadence;
        private void Ignite(Collider surface,IEarthPhysicalTarget target,EarthPhysicalTargetHandle handle,Vector3 point,Vector3 normal,float deltaSeconds,float energy,bool thermalShock)
        {
            int index=-1;for(int i=0;i<burning.Length;i++)if(burning[i].Surface==surface){index=i;break;}
            if(index<0)for(int i=0;i<burning.Length;i++)if(burning[i].Surface==null){index=i;break;}
            if(index<0)return;
            var burn=burning[index];
            if(burn.Surface!=surface||burn.Handle.StableId!=handle.StableId||burn.Handle.Generation!=handle.Generation)
                burn=new Burning{LastHeatTick=float.NegativeInfinity,LastReaction=-10};
            if(Time.fixedTime-burn.LastHeatTick>.25f&&!burn.Announced)burn.Dose=0;
            if(burn.LastHeatTick!=Time.fixedTime)
                burn.Dose+=Mathf.Min(deltaSeconds,.1f)*Mathf.Clamp(energy,0,2);
            burn.LastHeatTick=Time.fixedTime;
            burn.Surface=surface;burn.Target=target;burn.Handle=handle;
            burn.Point=surface.transform.InverseTransformPoint(point);burn.Normal=surface.transform.InverseTransformDirection(normal);burn.Remaining=3;
            if(thermalShock)burn.Dose=Mathf.Max(burn.Dose,.6f);
            bool announce=!burn.Announced&&burn.Dose>=.6f;
            if(announce)burn.Announced=true;
            burning[index]=burn;
            if(announce)SustainedIgnition?.Invoke(new FireSurfaceContact(surface,point,normal,.4f,Mathf.Clamp(energy,0,4),handle));
        }
        // An accepted burning receiver is a catalyst, never a recursive projectile source.
        public bool TryConsumeIgnitionReaction(Vector3 from,Vector3 to,Func<Collider,Vector3,bool> visible,out FireSurfaceContact contact)
        {
            contact=default;if(residual)return false;
            for(int i=0;i<burning.Length;i++)
            {
                var b=burning[i];
                if(!b.Announced||b.Remaining<=0||b.Surface==null||!b.Surface.enabled||!b.Surface.gameObject.activeInHierarchy)continue;
                if(b.Target is UnityEngine.Object obj&&obj==null)continue;
                if(b.Target!=null&&(b.Target.TargetHandle.StableId!=b.Handle.StableId||b.Target.TargetHandle.Generation!=b.Handle.Generation))continue;
                Vector3 point=b.Surface.transform.TransformPoint(b.Point),normal=b.Surface.transform.TransformDirection(b.Normal).normalized;
                if(!FireWeaveTuning.CanReact(Time.fixedTime,b.LastReaction,point+normal*.2f,from,to,.65f))continue;
                if(visible==null||!visible(b.Surface,point))continue;
                b.LastReaction=Time.fixedTime;burning[i]=b;
                contact=new FireSurfaceContact(b.Surface,point,normal,.65f,1,b.Handle);return true;
            }
            return false;
        }
        private void FixedUpdate()
        {
            burnCadence+=Time.fixedDeltaTime;if(burnCadence<.1f)return;float dt=burnCadence;burnCadence=0;
            residual=true;bool hurtFighter=false;
            try{for(int i=0;i<burning.Length;i++)
            {
                var burn=burning[i];if(burn.Surface==null)continue;
                if(!burn.Surface.enabled||!burn.Surface.gameObject.activeInHierarchy||(burn.Target!=null&&(burn.Target.TargetHandle.StableId!=burn.Handle.StableId||burn.Target.TargetHandle.Generation!=burn.Handle.Generation))){burning[i]=default;continue;}
                burn.Remaining-=dt;if(burn.Remaining<=0){burning[i]=default;continue;}
                Vector3 normal=burn.Surface.transform.TransformDirection(burn.Normal);bool accepted=ApplyContact(burn.Surface,burn.Surface.transform.TransformPoint(burn.Point),normal,normal,dt,.3f);
                if(accepted&&!hurtFighter&&rivalRoot!=null&&burn.Surface.transform.IsChildOf(rivalRoot)&&duel.CanReceiveDamage(rivalId))
                {var handoff=RagdollHandoff.Uniform(Vector3.zero);duel.ApplyDamage(rivalId,5*dt,in handoff);hurtFighter=true;}
                burning[i]=burn;
            }}finally{residual=false;}
        }
        private EarthMvpDuelController duel; private EarthDuelFighterId owner,rivalId;private Transform rivalRoot;
        private EarthRockDebrisPool pool; private EarthMaterialFeedbackHub feedback; private Transform ownerRoot;
        public int Fractures {get;private set;} public int RejectedPartitions {get;private set;}
        public event Action<FireSurfaceContact> ContactAccepted;
        public event Action<FireSurfaceContact> SustainedIgnition;
        public event Action Cleared;
        public void Configure(EarthMvpDuelController match,EarthDuelFighterId fighter,EarthRockDebrisPool debrisPool,EarthMaterialFeedbackHub materialFeedback)
        {
            if(duel!=null)duel.RoundRestarted-=ResetExposure;
            duel=match!=null?match:throw new ArgumentNullException(nameof(match));
            pool=debrisPool!=null?debrisPool:throw new ArgumentNullException(nameof(debrisPool));
            feedback=materialFeedback!=null?materialFeedback:throw new ArgumentNullException(nameof(materialFeedback));owner=fighter;
            ownerRoot=owner==EarthDuelFighterId.Player?duel.PlayerTransform:duel.BotTransform;
            rivalId=owner==EarthDuelFighterId.Player?EarthDuelFighterId.Bot:EarthDuelFighterId.Player;rivalRoot=owner==EarthDuelFighterId.Player?duel.BotTransform:duel.PlayerTransform;
            if(ownerRoot==null)throw new ArgumentException("Bind actual fighter root before fire world response.");
            exposure.Clear();structureExposure.Clear();duel.RoundRestarted+=ResetExposure;
        }
        public bool ApplyContact(Collider surface,Vector3 point,Vector3 normal,Vector3 direction,float deltaSeconds,float energy=1f,bool thermalShock=false,bool allowIgnition=true)
        {
            if(!isActiveAndEnabled||duel==null||!duel.HasSimulationAuthority||!duel.CombatAllowed||
               !duel.CanReceiveDamage(owner)||duel.IsRecoverablyKnockedDown(owner)||Time.timeScale<=0||
               surface==null||!surface.enabled||surface.isTrigger||!surface.gameObject.activeInHierarchy||surface.transform.IsChildOf(ownerRoot)||
               !Finite(point)||!Finite(normal)||!Finite(direction)||normal.sqrMagnitude<.5f||direction.sqrMagnitude<.0001f||
               !float.IsFinite(deltaSeconds)||!float.IsFinite(energy)||deltaSeconds<=0||energy<=0)return false;
            // Caller must provide an authority query hit; reject detached/fabricated remote points.
            if(!FireColliderSurface.ContainsSurfacePoint(surface,point,normal))return false;
            using var marker=Marker.Auto(); normal.Normalize();direction.Normalize();
            var target=surface.GetComponentInParent<IEarthPhysicalTarget>();
            var handle=target!=null?target.TargetHandle:default;
            if(!residual){ContactAccepted?.Invoke(new FireSurfaceContact(surface,point,normal,.4f,Mathf.Clamp(energy,0,4),handle));if(allowIgnition)Ignite(surface,target,handle,point,normal,deltaSeconds,energy,thermalShock);}
            // Intact structures have no loose physical target yet. Route accumulated
            // heat through their existing structural owner instead of dropping contact.
            var arena=target as EarthArenaPiece;
            var structure=arena!=null?arena.Owner:surface.GetComponentInParent<EarthArenaStructure>();
            if(structure!=null&&(target==null||!target.IsEarthTargetValid))
            {
                if(!structure.OrdinaryDamageEnabled||(arena!=null&&(arena.HasMagicOwner||structure.IsPieceReservedForRepair(arena.PieceIndex))))return true;
                if(structureExposure.Step(structure.StructureId,structure.Generation,Time.fixedTime,deltaSeconds,energy,
                    Mathf.Max(pool.LargeStoneRadius+.01f,surface.bounds.extents.magnitude),pool.SmallStoneRadius,pool.LargeStoneRadius))
                {
                    var structural=new EarthStructureImpact(point,direction,240f,EarthStructureImpactKind.Projectile,(uint)owner+1);
                    EarthStructureImpactRouter.Apply(surface,in structural);
                }
                return true;
            }
            if(target==null||!target.IsEarthTargetValid||!handle.IsValid)return true;
            if(arena!=null&&arena.HasMagicOwner)return true;
            Rigidbody body=target.Body;if(body==null)return true;
            float radius=target is EarthFragment fragment?fragment.Radius:target is EarthRockDebris debris?debris.BreakRadius:surface.bounds.extents.magnitude;
            bool loose=target.TargetKind==EarthPhysicalTargetKind.Rock||target.TargetKind==EarthPhysicalTargetKind.ResonanceProjectile;
            // Existing material transaction rejects controlled/returning/repaired matter.
            var identity=body.GetComponent<EarthMatterIdentity>();
            if(identity!=null&&identity.TryRead(out EarthMatterRecord record))
            {
                if(record.Phase!=EarthMatterPhase.FreeDynamic&&record.Phase!=EarthMatterPhase.Sleeping)return true;
                if(record.Volume>0&&!(target is EarthFragment)&&!(target is EarthRockDebris))radius=Mathf.Pow(record.Volume*.2387324f,1f/3f);
            }
            if(target is EarthFragment held&&held.IsHeld)return true;
            bool burns=exposure.Step(handle.StableId,handle.Generation,Time.fixedTime,deltaSeconds,energy,radius,pool.SmallStoneRadius,pool.LargeStoneRadius);
            if(!residual&&exposure.LastStepAccepted&&!body.isKinematic)body.AddForceAtPosition(direction*FireStoneExposure.PushImpulse(body.mass,deltaSeconds,energy),point,ForceMode.Impulse);
            if(!burns)return true;
            if(!loose)
            {
                var impact=new EarthStructureImpact(point,direction,Mathf.Max(90,target.EarthMass*8),EarthStructureImpactKind.Projectile,(uint)owner+1);
                EarthStructureImpactRouter.Apply(surface,in impact);return true;
            }
            if(identity==null){RejectedPartitions++;return true;}
            var decision=new EarthRockBreakDecision(true,radius<=pool.SmallStoneRadius?0:radius<=pool.LargeStoneRadius?2:3, radius<=pool.SmallStoneRadius?48:96, radius<=pool.SmallStoneRadius?12:24);
            Vector3 velocity=body.isKinematic?Vector3.zero:body.linearVelocity;
            if(!pool.TryEmitBreak(point,normal,velocity,radius,body.mass,handle.StableId,decision,0,identity))
            {RejectedPartitions++;return true;}
            if(target is EarthFragment retired)retired.RetireAfterCounterSplit();
            else if(target is EarthRockDebris retiredDebris)retiredDebris.ResetPiece();
            else body.gameObject.SetActive(false);
            Fractures++;return true;
        }
        private static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);
        private void ResetExposure(){exposure.Clear();structureExposure.Clear();Array.Clear(burning,0,burning.Length);burnCadence=0;Cleared?.Invoke();}
        private void OnDisable()=>ResetExposure();
        private void OnDestroy(){if(duel!=null)duel.RoundRestarted-=ResetExposure;}
    }
}
