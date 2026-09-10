using System;
using Elemental.Runtime.Fire;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Fire;
using Elemental.Simulation.Bending;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
namespace Elemental.Presentation.Fire
{
    // Cosmetic typed-cue consumer. All hits, lift and projectile positions belong to runtime.
    [DisallowMultipleComponent,DefaultExecutionOrder(3200)]
    public sealed class FireAbilityEffects:MonoBehaviour
    {
        public const int TimedCapacity=24,SourceCapacity=12,WeaveCapacity=16,LowFlightHandCapacity=2,Capacity=LowFlightHandCapacity+2+FireAbilityTuning.MaximumProjectiles+TimedCapacity+SourceCapacity+WeaveCapacity;
        private const int TimedStart=2+FireAbilityTuning.MaximumProjectiles,TimedEnd=TimedStart+TimedCapacity,SourceStart=TimedEnd,WeaveStart=SourceStart+SourceCapacity,LowFlightHandStart=WeaveStart+WeaveCapacity;
        private static readonly ProfilerMarker Marker=new("Elemental.Fire.AbilityEffects");
        private struct Seat
        {
            public uint Id;public bool Active,Ring,Charge,Supported;public FireAbilityEffectKind Kind;
            public Vector3 Position,Direction,Up,RingUp,Center,Side,LineSpan;public float Age,Life,Power,Angle,SectorHalfWidth;
        }
        private readonly Seat[] seats=new Seat[Capacity];
        private readonly RaycastHit[] surfaceHits=new RaycastHit[16];
        private int surfaceMask;private float lastWeaveRecovery=1;
        public int SimulationSubsteps {get;private set;}
        public int SurfaceProbeQueries {get;private set;}
        private FireBoltTrailRenderer boltTrails;
        public int BoltTrailSpans=>boltTrails!=null?boltTrails.VisibleSpans:0;
        public int BoltTrailQueries=>boltTrails!=null?boltTrails.Queries:0;
        private FireRingRibbonRenderer ringRibbon;private readonly Vector3[] ringSupports=new Vector3[24];private readonly bool[] ringSupportValid=new bool[24];private int ringRibbonCount;
        public int RingRibbonSpans=>ringRibbon!=null?ringRibbon.VisibleSpans:0;
        public int RingRibbonQueries=>ringRibbon!=null?ringRibbon.QueryCount:0;
        private FireMeteorBowRenderer meteorBow;public bool MeteorBowVisible=>meteorBow!=null&&meteorBow.Visible;
        private FireProtectionSphereRenderer protectionSphere;private FireProtectionSphereRenderer[] boltCores;
        private float sphereBlend,morphPower=4f/12,morphVelocity,coverageBlend;
        public float WeaveMorphPower=>morphPower;
        public float WeaveVerticalCoverage=>coverageBlend;
        public float HeldBoltVolumeRadius=>controller!=null&&controller.IsBoltCharging&&protectionSphere.Visible?protectionSphere.Radius:0;
        public float ProjectileVolumeRadius(int index)=>boltCores!=null&&index>=0&&index<boltCores.Length&&boltCores[index].Visible?boltCores[index].Radius:0;
        public bool ProjectileUsesTearDrop(int index)=>boltCores!=null&&index>=0&&index<boltCores.Length&&boltCores[index].Visible&&boltCores[index].IsBoltShape;
        public Vector4 ProjectileShapeParameters(int index)=>boltCores!=null&&index>=0&&index<boltCores.Length?boltCores[index].BoltShapeParameters:Vector4.zero;
        public bool SphereVisible=>protectionSphere!=null&&protectionSphere.Visible;
        public int SphereRaySteps=>SphereVisible?FireProtectionSphereRenderer.RaySteps:0;
        private FireFlowVolumeBackend[] flows;private FirePresentationSnapshot[] snapshots;
        private FireAbilityController controller;private EarthCharacterPoseController pose;
        private Transform leftFoot,rightFoot,rightHand,leftHand;private UnityEngine.Camera camera;private uint poseTick;
        public double LastStepMilliseconds {get;private set;}
        public int QueryCount {get;private set;}
        public int BudgetStops {get;private set;}
        public int BlockedParcels {get;private set;}
        public int RingSectors {get;private set;}
        public int DroppedTimedCues {get;private set;}
        public int RecycledTimedEffects {get;private set;}
        public int AliveParticles {get;private set;}
        public int ActiveEmitters {get;private set;}
        public int PersistentSourceEmitters {get;private set;}
        public int WeaveEmitters {get;private set;}
        public void Configure(FireAbilityController source,Animator humanoid,EarthCharacterPoseController poseController,
            FireVisualProfile profile,UnityEngine.Camera viewCamera,int collisionMask)
        {
            if(source==null||humanoid==null||!humanoid.isHuman||poseController==null||profile==null||viewCamera==null)
                throw new ArgumentException("Fire effects require explicit controller, actual humanoid, pose owner, profile and camera.");
            Texture atlas=profile.CpuMaterial!=null?profile.CpuMaterial.GetTexture("_FlameAtlas"):null;
            if(atlas==null)throw new ArgumentException("Fire ability profile must retain the approved flame atlas.");
            if(controller!=null)controller.Effect-=OnCue;
            DisposeFlows();controller=source;pose=poseController;camera=viewCamera;surfaceMask=collisionMask;
            leftFoot=humanoid.GetBoneTransform(HumanBodyBones.LeftFoot);rightFoot=humanoid.GetBoneTransform(HumanBodyBones.RightFoot);
            rightHand=humanoid.GetBoneTransform(HumanBodyBones.RightHand);leftHand=humanoid.GetBoneTransform(HumanBodyBones.LeftHand);
            if(leftFoot==null||rightFoot==null||rightHand==null||leftHand==null)throw new ArgumentException("Both actual humanoid feet must be bound.");
            ringRibbon=new FireRingRibbonRenderer(transform,source.OwnerRoot,collisionMask);
            boltTrails=new FireBoltTrailRenderer(transform,source.OwnerRoot,collisionMask);
            meteorBow=new FireMeteorBowRenderer(transform,humanoid);protectionSphere=new FireProtectionSphereRenderer(transform);boltCores=new FireProtectionSphereRenderer[FireAbilityTuning.MaximumProjectiles];for(int i=0;i<boltCores.Length;i++)boltCores[i]=new FireProtectionSphereRenderer(transform);
            Shader shader=Resources.Load<Shader>("FireFlowParcel");
            flows=new FireFlowVolumeBackend[Capacity];snapshots=new FirePresentationSnapshot[Capacity];
            for(int i=0;i<Capacity;i++)
            {
                flows[i]=new FireFlowVolumeBackend(transform,shader,collisionMask,atlas,false,i<2?96:i>=LowFlightHandStart?64:i>=SourceStart?32:48);
                if(i<2||i>=LowFlightHandStart)flows[i].ConfigureFootStream();
                else flows[i].ConfigureDetailedAbilityFlame(Resources.Load<Texture2D>("EpicBurnTongues"));
                if(i>=2&&i<TimedStart)flows[i].Solver.SetSimulationRate(60);
                if(i>=WeaveStart)flows[i].ConfigureAbilitySupport(.62f);
                flows[i].Collision.SetEmitter(source.OwnerRoot);
                flows[i].SetTailAgeScale(i<2||i>=LowFlightHandStart?1:.85f);
                snapshots[i]=new FirePresentationSnapshot{Group=new FireGroupHandle(i,1),NodeCount=1,Energy=1};
            }
            if(isActiveAndEnabled)controller.Effect+=OnCue;
        }
        // Explicit caller-owned buffers; sample hot transported gas, not emitter origins.
        // Lighting owners can project these positions onto terrain using their own budget.
        public int CopyHotSamples(Vector3[] positions,Vector3[] up,float[] heat)
        {
            if(positions==null||up==null||heat==null||positions.Length<1||positions.Length>8||up.Length!=positions.Length||heat.Length!=positions.Length)
                throw new ArgumentException("Provide equal preallocated light sample buffers of length 1 to 8.");
            if(flows==null)return 0;
            int available=0;
            for(int i=0;i<flows.Length;i++)if(HottestParticle(flows[i].Solver)>=0)available++;
            int wanted=Math.Min(positions.Length,available),written=0,ordinal=0;
            for(int i=0;i<flows.Length&&written<wanted;i++)
            {
                int best=HottestParticle(flows[i].Solver);if(best<0)continue;
                if(ordinal==written*available/wanted)
                {
                    var particle=flows[i].Solver.Particles[best];positions[written]=particle.Position;up[written]=particle.Up;
                    heat[written]=particle.Temperature*(i<2||i>=LowFlightHandStart?.5f:1f);
                    if(i>=TimedStart&&seats[i].Kind==FireAbilityEffectKind.Impact)heat[written]*=Mathf.Clamp01(1-seats[i].Age/.18f);
                    written++;
                }
                ordinal++;
            }
            return written;
        }
        private static int HottestParticle(FireFlowParticleSolver solver)
        {
            int best=-1;float heat=.45f;
            for(int i=0;i<solver.Count;i++)
            {
                var p=solver.Particles[i];if(!p.Spark&&p.Age<p.HotLifetime&&p.Temperature>heat){heat=p.Temperature;best=i;}
            }
            return best;
        }
        private void OnEnable(){if(controller!=null)controller.Effect+=OnCue;}
        private void OnDisable(){if(controller!=null)controller.Effect-=OnCue;Clear();}
        private void OnDestroy()=>DisposeFlows();
        private void Clear()
        {
            meteorBow?.Clear();
            boltTrails?.Clear();if(boltCores!=null)foreach(var core in boltCores)core.Clear();ringRibbonCount=0;ringRibbon?.Clear();sphereBlend=0;protectionSphere?.Clear();lastWeaveRecovery=1;Array.Clear(seats,0,seats.Length);if(flows!=null)foreach(var flow in flows)flow.Clear();
            AliveParticles=ActiveEmitters=PersistentSourceEmitters=WeaveEmitters=QueryCount=BudgetStops=RingSectors=0;LastStepMilliseconds=0;
        }
        private void DisposeFlows(){meteorBow?.Dispose();meteorBow=null;boltTrails?.Dispose();boltTrails=null;if(boltCores!=null)foreach(var core in boltCores)core.Dispose();boltCores=null;ringRibbon?.Dispose();ringRibbon=null;protectionSphere?.Dispose();protectionSphere=null;if(flows!=null)foreach(var flow in flows)flow.Dispose();flows=null;}
        private void OnCue(FireAbilityCue cue)
        {
            if(flows==null)return;
            if(cue.Kind==FireAbilityEffectKind.HandWindup||cue.Kind==FireAbilityEffectKind.FootWindup)
            {
                poseTick=Math.Max(poseTick+1,pose.LastAuthoritativeTick+1);
                pose.RequestSemanticPresentation(EarthTechniqueKind.Grip,
                    cue.Kind==FireAbilityEffectKind.FootWindup?EarthTechniqueId.QuickStoneRightKick:EarthTechniqueId.QuickStonePunch,
                    poseTick,(Vector3)(cue.Position+cue.Direction*3),10,8,true);
            }
            if(cue.Kind==FireAbilityEffectKind.HandBolt||cue.Kind==FireAbilityEffectKind.FootBolt)return;
            bool ring=cue.Kind==FireAbilityEffectKind.Ring||cue.Kind==FireAbilityEffectKind.RingWindup;
            int count=1;
            if(ring)
            {
                int reservedGround=0;
                for(int i=TimedStart;i<TimedEnd;i++)
                {
                    if(seats[i].Kind==FireAbilityEffectKind.GroundFlame&&seats[i].Active){reservedGround++;continue;}
                    // Release replaces its charging ring deliberately, freeing the existing
                    // seats for complete coverage instead of competing with draining windup gas.
                    if(seats[i].Ring){seats[i]=default;flows[i].Clear();}
                }
                count=TimedCapacity-reservedGround;
                RingSectors=count;if(cue.Kind==FireAbilityEffectKind.Ring)ringRibbonCount=count;
            }
            Vector3 up=math.normalizesafe(cue.Up,new float3(0,1,0));
            Vector3 side=Vector3.Cross(up,Mathf.Abs(up.y)<.9f?Vector3.up:Vector3.right).normalized;
            for(int n=0;n<count;n++)
            {
                int index=cue.Kind==FireAbilityEffectKind.GroundFlame&&cue.Lifetime>1?AllocateSource():AllocateTimed();
                if(index<0){DroppedTimedCues++;continue;}
                seats[index]=new Seat{Id=cue.Id,Kind=cue.Kind,Active=true,Ring=cue.Kind==FireAbilityEffectKind.Ring||cue.Kind==FireAbilityEffectKind.RingWindup,
                    Charge=cue.Kind==FireAbilityEffectKind.HandWindup||cue.Kind==FireAbilityEffectKind.FootWindup||cue.Kind==FireAbilityEffectKind.RingWindup,
                    Center=cue.Position,Position=cue.Position,Direction=cue.Direction,LineSpan=cue.Direction,Up=up,RingUp=up,Side=side,Angle=n*Mathf.PI*2/count,SectorHalfWidth=ring?Mathf.Tan(Mathf.PI/count)*1.35f:0,
                    Power=Mathf.Clamp(cue.Power,.1f,1),Life=cue.Lifetime+(cue.Kind==FireAbilityEffectKind.Ring?.25f:0)};
                flows[index].ConfigureAbilitySupport(ring||cue.Kind==FireAbilityEffectKind.GroundFlame?.62f:.28f);
                flows[index].ConfigureRingMediumDetail(ring); // Reset when pooled ring seats become another cue.
                flows[index].Solver.SetSimulationRate(ring?45:90);
                flows[index].Solver.Injection=new FireFlowInjection(Vector3.zero,4,120,.32f,4,1);
                flows[index].Begin(cue.Id+(uint)n*7919);
            }
        }
        private int AllocateTimed()
        {
            int oldest=-1;float age=-1;
            for(int i=TimedStart;i<TimedEnd;i++)
            {
                if(seats[i].Kind==FireAbilityEffectKind.GroundFlame&&seats[i].Active)continue;
                if(!seats[i].Active&&flows[i].Solver.Count==0)return i;
                if(seats[i].Age>age){age=seats[i].Age;oldest=i;}
            }
            if(oldest<0)return -1;
            RecycledTimedEffects++;flows[oldest].Clear();return oldest;
        }
        private int AllocateSource()
        {
            int oldest=SourceStart;
            for(int i=SourceStart;i<WeaveStart;i++)
            {
                if(!seats[i].Active&&flows[i].Solver.Count==0)return i;
                if(seats[i].Age>seats[oldest].Age)oldest=i;
            }
            RecycledTimedEffects++;flows[oldest].Clear();return oldest;
        }
        private void StepWeaveSeats(Vector3 up,float dt)
        {
            bool wave=controller.SphereWaveActive;
            morphPower=Mathf.SmoothDamp(morphPower,controller.WeavePower01,ref morphVelocity,.2f,float.PositiveInfinity,dt);
            float curl=Mathf.SmoothStep(0,1,Mathf.InverseLerp(5f/12,7f/12,morphPower));
            float sourceOrbitBlend=FireWeaveEmissionOwnership.SourceOrbitBlend(controller.WeaveForm,curl);
            float verticalTarget=Mathf.SmoothStep(0,1,Mathf.InverseLerp(7f/12,10f/12,morphPower));
            coverageBlend=Mathf.MoveTowards(coverageBlend,verticalTarget,dt*2.5f);
            bool active=wave||(controller.WeaveHeld&&curl>.001f);
            float recovery=controller.WeaveRecovery01;bool transferred=active&&recovery<lastWeaveRecovery-.001f;lastWeaveRecovery=recovery;
            float waveEnergy=wave?Mathf.SmoothStep(0,1,Mathf.Clamp01((FireSphereWave.Duration-controller.SphereWaveAge)/.25f)):0;
            Vector3 center=wave?controller.SphereWaveCenter:controller.OwnerRoot.position+up;
            if(wave)up=controller.SphereWaveUp;
            Vector3 side=Vector3.Cross(up,Mathf.Abs(up.y)<.9f?Vector3.up:Vector3.right).normalized,across=Vector3.Cross(up,side);
            float radius=wave?controller.SphereWaveRadius:Mathf.Lerp(2.1f,2.3f,coverageBlend);
            if(transferred)sphereBlend=0;
            sphereBlend=Mathf.MoveTowards(sphereBlend,active?recovery*curl:0,dt*7);
            bool charge=controller.IsBoltCharging&&controller.TryGetChargedBoltPose(out _,out _,out _);
            if(wave)protectionSphere.Step(true,center,up,radius,waveEnergy,Time.time,wave:true);
            else if(active)protectionSphere.Step(true,center,up,radius,sphereBlend*Mathf.Lerp(.75f,1,morphPower),Time.time,coverage:.08f+coverageBlend);
            else if(charge&&controller.TryGetChargedBoltPose(out var chargeCenter,out _,out float chargeRadius))
                protectionSphere.Step(true,chargeCenter,up,chargeRadius,Mathf.Lerp(.6f,1,controller.BoltCharge01),Time.time,filled:true);
            else protectionSphere.Step(false,center,up,radius,0,Time.time);
            for(int n=0;n<WeaveCapacity;n++)
            {
                int i=WeaveStart+n;uint id=wave?42003u:42001u;
                // Form changes preserve the existing gas and stable sources; only actual
                // depletion/new activation starts a fresh emission episode.
                if(active&&(transferred||(seats[i].Id!=0&&seats[i].Id!=id)||(!seats[i].Active&&flows[i].Solver.Count==0))){flows[i].Begin(id+(uint)n*7919);flows[i].Solver.SetSimulationRate(45);}
                ref var seat=ref seats[i];seat.Active=active;seat.Id=id;seat.Up=up;seat.Power=wave?waveEnergy:Mathf.Lerp(.72f,1,morphPower)*recovery*curl;seat.Age+=dt;
                float coverage=wave?1:coverageBlend;
                float latitude=(1-2*(n+.5f)/WeaveCapacity)*coverage,longitude=n*2.3999632f+seat.Age*.35f;
                Vector3 radial=(side*Mathf.Cos(longitude)+across*Mathf.Sin(longitude))*Mathf.Sqrt(1-latitude*latitude)+up*latitude;
                Vector3 tangent=Vector3.Cross(up,radial).normalized;
                if(tangent.sqrMagnitude<.1f)tangent=side;
                Vector3 aim=controller.WeaveAimDirection;
                seat.Position=wave?center+radial*radius:Vector3.Lerp(rightHand.position+aim*.12f,center+radial*radius,sourceOrbitBlend);
                seat.Direction=wave?tangent:Vector3.Slerp(aim,tangent,sourceOrbitBlend).normalized;seat.Ring=false;seat.Charge=false;
                flows[i].ConfigureAbilitySupport(.28f);flows[i].ConfigureSurfaceAccentDetail(coverage>.1f);if(coverage>.1f)flows[i].ConfigureSphereAccentDetail();
                Vector3 velocity=wave?Vector3.zero:controller.OwnerVelocity;
                if(coverage>.01f)flows[i].Solver.ConfigureSphericalGuide(active&&!wave,center,up,radius,5,velocity,curl);
                else flows[i].Solver.ConfigureOrbitalGuide(active,center,up,radius,7,velocity,curl);
                flows[i].Solver.Injection=new FireFlowInjection(velocity+seat.Direction*Mathf.Lerp(10,5,curl),0,64,Mathf.Lerp(.34f,.20f,coverage),12,Mathf.Lerp(.86f,.64f,coverage),
                    developmentScale:2,staggerBirths:true,birthTangent:tangent*.18f,coolingTail:.35f,uniformBirthSpread:true,
                    elongation:.7f,smokeExpansion:1.3f,smokeStride:4,trackEmitterPath:true);
            }
        }
        private bool SeatOnNearbySurface(ref Seat seat)
        {
            // Cosmetic support only. Re-query each active sector rather than caching a pooled
            // support across its generation; gameplay ring hits remain entirely in runtime.
            seat.Supported=false;if(!seat.Active)return true;
            QueryCount++;SurfaceProbeQueries++;
            int count=UnityEngine.Physics.RaycastNonAlloc(seat.Position+seat.Up*.8f,-seat.Up,surfaceHits,2.6f,surfaceMask,QueryTriggerInteraction.Ignore);
            if(count==surfaceHits.Length){BudgetStops++;return false;}
            float nearest=float.MaxValue;int best=-1;
            for(int i=0;i<count;i++)
            {
                var hit=surfaceHits[i];
                if(hit.collider==null||hit.collider.transform.IsChildOf(controller.OwnerRoot)||Vector3.Dot(hit.normal,seat.Up)<.15f)continue;
                if(hit.distance<nearest){nearest=hit.distance;best=i;}
            }
            if(best>=0)
            {
                seat.Supported=true;var hit=surfaceHits[best];
                Vector3 tangent=Vector3.ProjectOnPlane(seat.Direction,hit.normal);
                seat.Position=hit.point+hit.normal*.48f;seat.Up=hit.normal;
                seat.Direction=(tangent-hit.normal*.65f).normalized;
            }
            return true;
        }
        private void LateUpdate()
        {
            LastStepMilliseconds=0;QueryCount=BudgetStops=BlockedParcels=SurfaceProbeQueries=SimulationSubsteps=0;
            if(flows==null||controller==null)return;
            if(!controller.IsAvailable){Clear();return;}
            if(Time.timeScale<=0)return;
            using var marker=Marker.Auto();float dt=Mathf.Min(Time.deltaTime,1f/15);
            Vector3 up=controller.LocalUp;
            Vector3 ownerVelocity=controller.OwnerVelocity;
            StepWeaveSeats(up,dt);
            meteorBow.Step(controller.IsLowFlying,controller.OwnerRoot.position,up,controller.LowFlightDirection,controller.LowFlightSpeed01,Time.time);
            for(int i=0;i<2;i++)
            {
                Transform foot=i==0?leftFoot:rightFoot;
                bool lift=controller.IsLifting||controller.IsLowFlying;
                if(lift&&!seats[i].Active)flows[i].Begin((uint)(123+i));
                float charge=controller.LiftCharge;
                flows[i].Solver.Injection=new FireFlowInjection(ownerVelocity,controller.IsLowFlying?24:Mathf.Lerp(14,20,charge),controller.IsLowFlying?185:Mathf.Lerp(220,280,charge),controller.IsLowFlying?.23f:Mathf.Lerp(.16f,.18f,charge),6,controller.IsLowFlying?1:Mathf.Lerp(.7f,.9f,charge),developmentScale:3f,tailAgeScale:1,staggerBirths:true,coolingTail:controller.IsLowFlying?.32f:.28f,smokeStride:controller.IsLowFlying?2:4,trackEmitterPath:true,surfaceResidence:.12f);
                seats[i]=new Seat{Active=lift,Position=foot.position-up*.045f,Direction=controller.IsLowFlying?(-controller.LowFlightDirection-up*.18f).normalized:-up,Up=up,Power=Mathf.Lerp(.75f,1,controller.LiftCharge)};
            }
            for(int handIndex=0;handIndex<2;handIndex++)
            {
                int index=LowFlightHandStart+handIndex;Transform limb=handIndex==0?leftHand:rightHand;bool flying=controller.IsLowFlying;
                if(flying&&!seats[index].Active)flows[index].Begin((uint)(6311+handIndex));
                Vector3 direction=(-controller.LowFlightDirection-up*.22f).normalized;
                flows[index].Solver.Injection=new FireFlowInjection(ownerVelocity,26,145,.23f,7,.96f,developmentScale:2.2f,staggerBirths:true,coolingTail:.32f,smokeStride:2,trackEmitterPath:true,surfaceResidence:.08f);
                seats[index]=new Seat{Active=flying,Position=limb.position+direction*.04f,Direction=direction,Up=up,Power=.8f};
            }
            for(int i=0;i<FireAbilityTuning.MaximumProjectiles;i++)
            {
                var projectile=controller.GetProjectile(i);int index=i+2;
                var boltProfile=FireChargedBoltProfile.FromPower(projectile.Power);
                boltCores[i].StepBolt(projectile.Active&&!projectile.PosePending,projectile.Position,up,projectile.Direction,projectile.Power,projectile.Age,projectile.Id,Time.time);
                if(projectile.Active&&!projectile.PosePending)
                {
                    if(seats[index].Id!=projectile.Id){flows[index].Begin(projectile.Id);flows[index].ConfigureLeadingBolt(Mathf.InverseLerp(1,3,projectile.Power));}
                    flows[index].Solver.Injection=FireBoltTailProfile.Injection(projectile.Power,projectile.Speed,projectile.Age,projectile.Id,projectile.Direction,up,projectile.Drill,flows[index].Solver.ParticleLimit);
                    seats[index]=new Seat{Id=projectile.Id,Active=true,Position=projectile.Position,Direction=-projectile.Direction,Up=up,Power=Mathf.Clamp01(projectile.Power)};
                }
                else seats[index].Active=false;
            }
            AliveParticles=ActiveEmitters=PersistentSourceEmitters=WeaveEmitters=QueryCount=BudgetStops=0;LastStepMilliseconds=protectionSphere.LastStepMilliseconds+(meteorBow!=null?meteorBow.LastStepMilliseconds:0);foreach(var core in boltCores)LastStepMilliseconds+=core.LastStepMilliseconds;
            boltTrails.BeginFrame();
            Array.Clear(ringSupportValid,0,ringSupportValid.Length);float ribbonEnergy=0;Vector3 ribbonCenter=controller.OwnerRoot.position,ribbonUp=up;
            for(int i=0;i<Capacity;i++)
            {
                ref var seat=ref seats[i];bool surfaceValid=true;
                if(i>=TimedStart&&i<WeaveStart)
                {
                    seat.Age+=dt;
                    bool heldBolt=seat.Charge&&!seat.Ring&&controller.IsBoltCharging&&controller.ChargingBoltId==seat.Id;
                    if(!(seat.Charge&&seat.Ring)&&!heldBolt&&seat.Age>=seat.Life)seat.Active=false;
                    if(seat.Charge&&!seat.Ring)
                    {
                        bool pending=heldBolt;
                        for(int j=0;j<controller.ProjectileCapacity;j++)
                        {var projectile=controller.GetProjectile(j);if(projectile.Id==seat.Id&&projectile.Pending){pending=true;break;}}
                        seat.Active&=pending;
                        float charge=heldBolt?controller.BoltCharge01:Mathf.Clamp01(seat.Age/Mathf.Max(.01f,seat.Life));
                        Transform limb=seat.Kind==FireAbilityEffectKind.FootWindup?rightFoot:rightHand;
                        seat.Position=limb.position+seat.Direction*.08f;seat.Up=up;
                        if(heldBolt&&controller.TryGetChargedBoltPose(out var chargePoint,out var chargeDirection,out _)){seat.Position=chargePoint;seat.Direction=chargeDirection;}
                        var chargeProfile=FireChargedBoltProfile.Evaluate(charge);flows[i].ConfigureChargeAccentDetail(chargeProfile.FlameTongues);
                        Vector3 swirl=Vector3.Cross(up,seat.Direction).normalized;
                        flows[i].Solver.Injection=new FireFlowInjection(ownerVelocity+swirl*2,.3f,chargeProfile.DetailRateForCapacity(48),chargeProfile.TailHotSeconds,3,chargeProfile.DetailSize*.65f,developmentScale:3,tailAgeScale:.85f,staggerBirths:true,birthTangent:swirl*chargeProfile.VisualRadius*.75f,coolingTail:chargeProfile.CoolingSeconds,trackEmitterPath:true,smokeStride:4);
                        seat.Power=Mathf.Lerp(.35f,1,charge);
                    }
                    if(seat.Kind==FireAbilityEffectKind.GroundFlame)
                    {
                        if(controller.TryGetGroundPose(seat.Id,out Vector3 groundPoint,out Vector3 groundUp))
                        {
                            // The hot gas actually strikes the floor: the existing sweep/deflection
                            // then spreads it across relief, instead of spawning an uncollided disc.
                            Vector3 across=Vector3.Cross(groundUp,Mathf.Abs(groundUp.y)<.9f?Vector3.up:Vector3.right).normalized;
                            float phase=seat.Id*.71f+seat.Age*19;
                            Vector3 tangent=across*Mathf.Sin(phase)+Vector3.Cross(groundUp,across)*Mathf.Cos(phase);
                            seat.Position=groundPoint+groundUp*.48f;seat.Up=groundUp;
                            bool surfaceWash=Mathf.Sin(phase*1.7f)>.75f;
                            seat.Direction=(tangent*.2f+groundUp*(surfaceWash?-.8f:1)).normalized;
                            bool persistent=i>=SourceStart;
                            if(persistent)seat.Power=Mathf.SmoothStep(0,1,Mathf.Clamp01((seat.Life-seat.Age)/.45f))*Mathf.SmoothStep(0,1,Mathf.Clamp01(seat.Age/.1f));
                            flows[i].Solver.Injection=new FireFlowInjection(tangent*1.2f,surfaceWash?1.6f:4.2f,persistent?88:136,.25f,4,.85f+.2f*Mathf.Sin(phase*.7f),developmentScale:2,tailAgeScale:.85f,staggerBirths:true,birthTangent:Vector3.ProjectOnPlane(seat.LineSpan,groundUp)*(persistent?.3f:.7f),coolingTail:.28f,uniformBirthSpread:true,elongation:.85f,smokeExpansion:1.4f,smokeStride:4);
                        }
                        else seat.Active=false;
                    }
                    if(seat.Ring)
                    {
                        seat.Up=seat.Charge?up:seat.RingUp;
                        // Sweep births across each existing sector; each parcel then keeps
                        // its own physically swept momentum, never repositioned with the ring.
                        float angle=seat.Angle;
                        Vector3 radial=seat.Side*Mathf.Cos(angle)+Vector3.Cross(seat.Up,seat.Side)*Mathf.Sin(angle);
                        Vector3 tangent=Vector3.Cross(seat.Up,radial);
                        if(seat.Charge)
                        {
                            seat.Active&=controller.IsRingCharging;
                            float charge=controller.RingCharge01;
                            seat.Center=controller.OwnerRoot.position;seat.Up=up;
                            seat.Position=seat.Center+radial*Mathf.Lerp(2.2f,.6f,charge)+up*.14f;
                            seat.Direction=(-radial*.75f-up*.5f+tangent*.2f*Mathf.Sin(seat.Age*23+seat.Angle)).normalized;
                            flows[i].Solver.Injection=new FireFlowInjection(ownerVelocity+tangent*7,1.6f,196,.18f,4,Mathf.Lerp(.85f,1.15f,charge),developmentScale:2,tailAgeScale:.85f,staggerBirths:true,birthTangent:tangent*(Mathf.Lerp(2.2f,.6f,charge)*seat.SectorHalfWidth),coolingTail:.22f,uniformBirthSpread:true,angularSpeed:1.25f,elongation:.85f,trackEmitterPath:true);
                            seat.Power=Mathf.Lerp(.35f,1,charge);
                        }
                        else
                        {
                            float expansion=Mathf.Clamp01(seat.Age/FireAbilityTuning.RingSeconds);
                            flows[i].Solver.Injection=new FireFlowInjection(radial*FireAbilityTuning.RingSpeed(seat.Age)+tangent*15,1.6f,128,Mathf.Lerp(.25f,.23f,expansion),8,
                                1.15f+Mathf.Lerp(.16f,.3f,expansion)*Mathf.Sin(seat.Age*23+seat.Angle*3),developmentScale:2,tailAgeScale:Mathf.Lerp(.85f,1,expansion),staggerBirths:true,
                                birthTangent:tangent*(FireAbilityTuning.RingExtent(seat.Age)*seat.SectorHalfWidth*Mathf.Lerp(1,1.2f,expansion)),coolingTail:Mathf.Lerp(.26f,.38f,expansion),
                                uniformBirthSpread:true,angularSpeed:Mathf.Lerp(1.25f,2.5f,expansion),elongation:Mathf.Lerp(.85f,.5f,expansion),smokeStride:expansion>.55f?4:8,trackEmitterPath:true);
                            seat.Position=seat.Center+radial*FireAbilityTuning.RingExtent(seat.Age)+seat.Up*.14f;
                            seat.Direction=(radial*.6f-seat.Up*.7f+tangent*.25f*Mathf.Sin(seat.Age*19+seat.Angle)).normalized;
                        }
                        surfaceValid=SeatOnNearbySurface(ref seat);
                        if(!seat.Charge&&seat.Active&&ringRibbonCount>0)
                        {
                            int slot=Mathf.Clamp(Mathf.RoundToInt(seat.Angle*ringRibbonCount/(Mathf.PI*2)),0,ringRibbonCount-1);
                            ringSupports[slot]=seat.Position;ringSupportValid[slot]=surfaceValid&&seat.Supported;
                            ribbonEnergy=seat.Power*Mathf.SmoothStep(0,1,Mathf.Clamp01((seat.Life-seat.Age)/.25f));ribbonCenter=seat.Center;ribbonUp=seat.RingUp;
                        }
                    }
                }
                bool emits=seat.Active&&surfaceValid;
                if(seat.Ring&&!seat.Charge&&seat.Age>=seat.Life-.25f)emits=false;
                if(i>=TimedStart&&seat.Kind==FireAbilityEffectKind.Impact)
                {
                    emits&=seat.Age<.05f;
                    float burstPhase=seat.Age*97+seat.Id*.37f;
                    seat.Direction=(seat.Up*.5f+seat.Side*Mathf.Cos(burstPhase)+Vector3.Cross(seat.Up,seat.Side)*Mathf.Sin(burstPhase)).normalized;
                    flows[i].Solver.Injection=new FireFlowInjection(Vector3.zero,7,480,.08f,3,1.1f,developmentScale:3,tailAgeScale:1,staggerBirths:true,coolingTail:.32f,smokeExpansion:2,smokeStride:4);
                }
                if(!emits&&flows[i].Solver.Count==0)continue;
                var snapshot=snapshots[i];snapshot.Lifecycle=emits?FireLifecycle.Active:FireLifecycle.Draining;
                snapshot.Energy=seat.Power;snapshot.FreeUp=seat.Up;snapshot.Origin=seat.Position;
                snapshot.Nodes[0]=FireFieldNode.Stream(seat.Position,seat.Position+seat.Direction*.2f,seat.Direction,seat.Up);
                int stopsBefore=flows[i].Solver.BudgetStops;int blockedBefore=flows[i].Solver.BlockedParcels;
                flows[i].Step(snapshot,dt,camera);
                if(i>=2&&i<TimedStart)
                {
                    var projectile=controller.GetProjectile(i-2);
                    boltTrails.Append(projectile.Active&&!projectile.PosePending,projectile.Position,FireChargedBoltProfile.FromPower(projectile.Power).VisualRadius,up,projectile.Id,flows[i].Solver);
                }
                SimulationSubsteps+=flows[i].Solver.Substeps;
                LastStepMilliseconds+=flows[i].LastStepMilliseconds;QueryCount+=flows[i].Solver.QueryCount;
                BudgetStops+=flows[i].Solver.BudgetStops-stopsBefore;BlockedParcels+=flows[i].Solver.BlockedParcels-blockedBefore;
                AliveParticles+=flows[i].Solver.Count;if(emits){ActiveEmitters++;if(i>=WeaveStart&&i<LowFlightHandStart)WeaveEmitters++;else if(i>=SourceStart&&i<WeaveStart)PersistentSourceEmitters++;}
            }
            boltTrails.EndFrame(Time.time);QueryCount+=boltTrails.Queries;BudgetStops+=boltTrails.Saturations;LastStepMilliseconds+=boltTrails.LastStepMilliseconds;
            ringRibbon.Step(ringSupports,ringSupportValid,ringRibbonCount,ribbonCenter,ribbonUp,ribbonEnergy,Time.time);
            QueryCount+=ringRibbon.QueryCount;BudgetStops+=ringRibbon.Saturations;LastStepMilliseconds+=ringRibbon.LastStepMilliseconds;
        }
    }
}
