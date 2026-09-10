using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Fire
{
    // Explicit cosmetic birth preset. Default(struct) preserves every existing emitter.
    public readonly struct FireFlowInjection
    {
        public readonly bool Enabled,StaggerBirths,UniformBirthSpread,TrackEmitterPath;
        public readonly float3 Velocity,BirthTangent;
        public readonly float Speed,Rate,Lifetime,Path,Size,Temperature,DevelopmentScale,TailAgeScale,CoolingTail,AngularSpeed,Elongation,SmokeExpansion,SurfaceResidence,BirthDiscRadius,NozzleRadius;
        public readonly int SmokeStride;
        public FireFlowInjection(float3 velocity,float speed,float rate,float lifetime,float path,float size,float temperature=1f,float developmentScale=1f,float tailAgeScale=1f,bool staggerBirths=false,float3 birthTangent=default,float coolingTail=0,bool uniformBirthSpread=false,float angularSpeed=0,float elongation=0,float smokeExpansion=1,int smokeStride=4,bool trackEmitterPath=false,float surfaceResidence=0,float birthDiscRadius=0,float nozzleRadius=.09f)
        {
            if(!math.all(math.isfinite(velocity))||!math.isfinite(speed)||!math.isfinite(rate)||!math.isfinite(lifetime)||!math.isfinite(path)||!math.isfinite(size)||!math.isfinite(temperature)||!math.isfinite(developmentScale)||!math.isfinite(tailAgeScale)||!math.all(math.isfinite(birthTangent))||!math.isfinite(coolingTail)||!math.isfinite(angularSpeed)||!math.isfinite(elongation)||!math.isfinite(smokeExpansion)||!math.isfinite(surfaceResidence)||!math.isfinite(birthDiscRadius)||!math.isfinite(nozzleRadius))
                throw new ArgumentException("Injection must be finite.");
            Enabled=true;BirthDiscRadius=math.clamp(birthDiscRadius,0,.5f);NozzleRadius=math.clamp(nozzleRadius,.06f,.24f);TrackEmitterPath=trackEmitterPath;SurfaceResidence=math.clamp(surfaceResidence,0,.16f);SmokeExpansion=math.clamp(smokeExpansion,1,2.5f);SmokeStride=math.clamp(smokeStride,2,16);UniformBirthSpread=uniformBirthSpread;AngularSpeed=math.clamp(angularSpeed,-3,3);Elongation=math.saturate(elongation);CoolingTail=math.clamp(coolingTail,0,.5f);BirthTangent=birthTangent*math.min(1,3/math.max(.001f,math.length(birthTangent)));StaggerBirths=staggerBirths;DevelopmentScale=math.clamp(developmentScale,1,4);TailAgeScale=math.clamp(tailAgeScale,.5f,1);Velocity=velocity*math.min(1,40/math.max(.001f,math.length(velocity)));
            Speed=math.clamp(speed,0,32);Rate=math.clamp(rate,8,480);Lifetime=math.clamp(lifetime,.06f,.8f);Path=math.clamp(path,.5f,12);Size=math.clamp(size,.25f,1.5f);Temperature=math.clamp(temperature,.2f,1);
        }
    }
    public struct FireFlowHit
    {
        public float Fraction;
        public bool Blocked;
        public float3 Point, Normal, SurfaceVelocity;
    }
    // Cosmetic world query; cannot mutate FireWorld, health, materials or rigid bodies.
    public interface IFireFlowCollision
    {
        bool Sweep(float3 position, float radius, float3 displacement, out FireFlowHit hit);
    }
    public struct FireFlowParticle
    {
        public uint Id;
        public float3 Position, Velocity, Up;
        public float Age, Lifetime, Radius, Distance, Phase, Size, Spin, Aspect;
        public float FirstStep,DevelopmentScale,TailAgeScale,HotLifetime,AngularSpeed,SmokeExpansion,SurfaceResidence,NozzleRadius;
        // Normalized transported thermal/soot state; visual gas only, never gameplay damage.
        public float Temperature, Soot;
        public bool Spark;
        public float4 ClipPlaneA, ClipPlaneB;
        public int Contacts;
        public float Heat => Temperature;
    }
    // Bounded Lagrangian hot-gas transport, not a pressure-grid/Navier-Stokes solver.
    // Birth direction is sampled once. Existing gas retains momentum when the actor aims elsewhere.
    public sealed class FireFlowParticleSolver
    {
        public const int Capacity = 192;
        public const int MaximumQueriesPerStep = 768;
        public const float MaximumPath = 8;
        public readonly FireFlowParticle[] Particles;
        public readonly bool Stationary;
        private readonly bool customCapacity;
        public FireFlowInjection Injection {get;set;}
        private bool orbitGuided,sphereGuided;private float guideStrength=1;private float3 orbitCenter,orbitAxis,orbitVelocity;private float orbitRadius,orbitSpeed;
        // Cosmetic curved transport only. No position snapping; contact releases the guide permanently.
        public void ConfigureOrbitalGuide(bool enabled,float3 center,float3 axis,float radius,float speed,float3 velocity,float strength=1)
        {
            if(!math.all(math.isfinite(center))||!math.all(math.isfinite(axis))||!math.all(math.isfinite(velocity))||!math.isfinite(radius)||!math.isfinite(speed)||!math.isfinite(strength))throw new ArgumentException("Orbital guide must be finite.");
            orbitGuided=enabled;sphereGuided=false;guideStrength=math.saturate(strength);orbitCenter=center;orbitAxis=math.normalizesafe(axis,new float3(0,1,0));orbitRadius=math.clamp(radius,.5f,4);orbitSpeed=math.clamp(speed,0,12);orbitVelocity=velocity;
        }
        public void ConfigureSphericalGuide(bool enabled,float3 center,float3 axis,float radius,float speed,float3 velocity,float strength=1)
        {
            ConfigureOrbitalGuide(false,center,axis,radius,speed,velocity,strength);sphereGuided=enabled;orbitRadius=math.clamp(radius,.5f,12);
        }
        public static float3 SphericalVelocity(float3 position,float3 center,float3 up,float radius,float speed,float3 velocity)
        {
            float3 delta=position-center,radial=math.normalizesafe(delta,FireContactMath.Tangent(up));
            float3 upward=up-radial*math.dot(up,radial);
            return velocity+math.cross(up,radial)*speed+upward*2-radial*math.clamp((math.length(delta)-radius)*12,-4,4);
        }
        public static float3 OrbitalVelocity(float3 position,float3 center,float3 axis,float radius,float speed,float3 ownerVelocity)
        {
            float3 offset=position-center;float axial=math.dot(offset,axis);float3 planar=offset-axis*axial;
            float3 radial=math.normalizesafe(planar,FireContactMath.Tangent(axis));
            return ownerVelocity+math.cross(axis,radial)*speed-radial*math.clamp((math.length(planar)-radius)*12,-4,4)-axis*math.clamp(axial*12,-4,4);
        }
        public int ParticleLimit => Particles.Length;
        public float HandPower {get;private set;}=1;
        public void ConfigureHandPower(float power)
        {if(!math.isfinite(power))throw new ArgumentException("Hand power must be finite.");HandPower=math.clamp(power,.38f,2.5f);}
        public float PathLimit => Injection.Enabled ? Injection.Path : Stationary ? 3f : MaximumPath*math.sqrt(HandPower);
        public int QueryLimit => customCapacity ? math.min(Stationary ? 224 : MaximumQueriesPerStep,ParticleLimit*6) : Stationary ? 224 : MaximumQueriesPerStep;
        // Explicit cosmetic preset. Default construction preserves the hand stream.
        public FireFlowParticleSolver(bool stationary = false,int particleCapacity=0)
        {
            if(particleCapacity<0||particleCapacity>Capacity)throw new ArgumentOutOfRangeException(nameof(particleCapacity));
            customCapacity=particleCapacity>0;Stationary=stationary;Particles=new FireFlowParticle[particleCapacity>0?math.max(8,particleCapacity):stationary?32:Capacity];
        }
        public int SimulationHz {get;private set;}=90;
        public int Substeps {get;private set;}
        public void SetSimulationRate(int hz)
        {
            if(hz<30||hz>90)throw new ArgumentOutOfRangeException(nameof(hz));
            SimulationHz=hz;
        }
        public int Count { get; private set; }
        public int Collisions { get; private set; }
        public int QueryCount { get; private set; }
        public int BudgetStops { get; private set; }
        public int RejectedBirths { get; private set; }
        public int BlockedParcels {get;private set;}
        public int InvalidContacts { get; private set; }
        public FireFlowHit LastInvalidContact {get;private set;}
        public float3 LastInvalidCenter {get;private set;}
        public float LastInvalidRadius {get;private set;}
        public float DroppedSeconds { get; private set; }
        private float remainder, time,distributionPhase;
        private uint serial, random = 1;
        private float3 previousOrigin;private bool previousEmitting;
        public void Clear(uint seed = 1)
        { Count=Collisions=QueryCount=BudgetStops=RejectedBirths=InvalidContacts=BlockedParcels=0; remainder=time=DroppedSeconds=0;serial=0;random=seed==0?1:seed;distributionPhase=(seed%997)/997f;previousEmitting=false;previousOrigin=0; }
        private float Random01()
        { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return (random & 0x00ffffff) / 16777216f; }
        public void Step(float delta, bool emits, float energy, float3 origin, float3 direction, float3 up, IFireFlowCollision world)
        {
            if(world==null || !math.isfinite(delta) || !math.isfinite(energy) || delta<0 || !math.all(math.isfinite(origin)) || !math.all(math.isfinite(direction)) || !math.all(math.isfinite(up)))
                throw new ArgumentException("Flow requires finite frame data and an explicit collision owner.");
            QueryCount=0;
            float dt=math.min(delta,1f/15); DroppedSeconds+=delta-dt;
            int steps=math.max(1,(int)math.ceil(dt*SimulationHz));Substeps=steps;float h=dt/steps;
            direction=math.normalizesafe(direction,new float3(0,0,1));up=math.normalizesafe(up,new float3(0,1,0));
            // Only opted-in moving abilities sweep their sampled emitter trajectory. A
            // discontinuity starts a fresh nozzle, never a trail across a respawn/teleport.
            bool track=Injection.Enabled&&Injection.TrackEmitterPath&&emits&&previousEmitting&&
                math.distance(previousOrigin,origin)<=math.max(1,delta*80);
            float3 emitterFrom=track?previousOrigin:origin;
            previousOrigin=origin;previousEmitting=emits;
            float3 side=math.normalizesafe(math.cross(up,direction),FireContactMath.Tangent(direction));
            float3 across=math.normalizesafe(math.cross(direction,side),up);
            for(int step=0;step<steps;step++)
            {
                time+=h;
                // Injection into a finite nozzle, rather than arbitrary births down a clipped tube.
                if(emits)
                {
                    float birthRate=(Injection.Enabled?Injection.Rate:Stationary?24:240)*math.saturate(energy);
                    float beforeRemainder=remainder;
                    remainder+=birthRate*h;
                    int births=(int)remainder;remainder-=births;
                    for(int n=0;n<births;n++)
                    {
                        if(Count==ParticleLimit){RejectedBirths++;continue;}
                        float phase=Random01()*6.2831853f,rad=math.sqrt(Random01())*(Injection.Enabled&&Injection.BirthDiscRadius>0?Injection.BirthDiscRadius:Stationary?.19f:.055f);
                        float3 spread=side*math.cos(phase)+across*math.sin(phase);
                        uint id=++serial;bool spark=id%8==0;
                        // Persistent small tongues, medium rolls and a few large body parcels.
                        float size=id%5<2?.36f+Random01()*.22f:id%5<4?.68f+Random01()*.28f:1.12f+Random01()*.3f;
                        if(Injection.Enabled)size*=Injection.Size;else if(!Stationary)size*=math.sqrt(HandPower);
                        float nozzleRadius=Injection.Enabled?Injection.NozzleRadius:.09f;float birthRadius=(spark?.025f:nozzleRadius)*size;
                        // Reconstruct each emission time, not a batch at the newest frame origin.
                        // Newborns advance only for the remainder of their own substep.
                        float localBirth=math.clamp((n+1-beforeRemainder)/math.max(.00001f,birthRate),0,h);
                        float3 birthOrigin=math.lerp(emitterFrom,origin,dt>0?(step*h+localBirth)/dt:1);
                        if(track&&math.distancesq(emitterFrom,birthOrigin)>.00000001f)
                        {
                            if(QueryCount>=QueryLimit){RejectedBirths++;BudgetStops++;continue;}
                            QueryCount++;
                            if(world.Sweep(emitterFrom,birthRadius,birthOrigin-emitterFrom,out _))
                            {RejectedBirths++;continue;} // Do not stamp a trail through an opaque wall.
                        }
                        float3 birthPosition=birthOrigin+spread*rad;
                        if(Injection.Enabled&&(math.lengthsq(Injection.BirthTangent)>.000001f||Injection.BirthDiscRadius>0))
                        {
                            // Fill an annular sector per birth, with an actual sphere sweep from
                            // its safe nozzle. A wall can clip this spread; gas never jumps through it.
                            float sample=Random01();
                            if(Injection.UniformBirthSpread)sample=math.frac(id*.61803398875f+distributionPhase);
                            float3 offset=spread*rad+Injection.BirthTangent*(sample*2-1);
                            if(QueryCount>=QueryLimit){RejectedBirths++;BudgetStops++;continue;}
                            QueryCount++;
                            if(world.Sweep(birthOrigin,birthRadius,offset,out var birthHit))
                            {
                                if(birthHit.Blocked){BlockedParcels++;RejectedBirths++;continue;}
                                birthPosition=birthOrigin+offset*math.max(0,math.saturate(birthHit.Fraction)-.01f);
                            }
                            else birthPosition=birthOrigin+offset;
                        }
                        Particles[Count++]=new FireFlowParticle{Id=id,Spark=spark,Size=size,FirstStep=Injection.Enabled&&Injection.TrackEmitterPath?math.max(.000001f,h-localBirth):Injection.Enabled&&Injection.StaggerBirths?h*(n+.5f)/births:0,DevelopmentScale=Injection.Enabled?Injection.DevelopmentScale:1,TailAgeScale=Injection.Enabled?Injection.TailAgeScale:1,Aspect=spark?3.8f:1.1f+Random01()*.95f,Spin=(Random01()-.5f)*7,Position=birthPosition,
                            Velocity=(Injection.Enabled?Injection.Velocity:float3.zero)+direction*(Injection.Enabled?Injection.Speed*(.95f+Random01()*.1f):(Stationary?1.05f:13.5f*math.sqrt(HandPower))+Random01()*(Stationary?.35f:1.5f*math.sqrt(HandPower)))+spread*((Stationary?.12f:.5f)+Random01()*(Stationary?.18f:.8f)),Up=up,
                            NozzleRadius=nozzleRadius,Radius=birthRadius,Lifetime=Injection.Enabled?Injection.Lifetime*(.9f+Random01()*.2f):(spark?.45f:Stationary?1.25f:.82f)+Random01()*.08f,Phase=phase,Temperature=Injection.Enabled?Injection.Temperature:1};
                        // Preserve hot lifetime/appearance. A bounded one-in-eight sample
                        // survives as cooled, physically transported smoke after the flame ends.
                        ref var born=ref Particles[Count-1];born.HotLifetime=born.Lifetime;
                        if(Injection.Enabled){born.SurfaceResidence=Injection.SurfaceResidence;born.AngularSpeed=Injection.AngularSpeed;born.SmokeExpansion=Injection.SmokeExpansion;if(!spark)born.Aspect=math.lerp(born.Aspect,2.05f,Injection.Elongation);}
                        if(Injection.Enabled&&Injection.CoolingTail>0&&!spark&&id%Injection.SmokeStride==1)born.Lifetime+=Injection.CoolingTail;
                    }
                }
                for(int i=Count-1;i>=0;i--)
                {
                    var p=Particles[i];float particleStep=p.FirstStep>0?p.FirstStep:h;p.FirstStep=0;p.Age+=particleStep;
                    if(p.Age>=p.Lifetime || p.Distance>=PathLimit){Particles[i]=Particles[--Count];continue;}
                    // Cooling is transported with each parcel; wall contact enhances mixing without
                    // spawning uncollided smoke or extending damage beyond the gameplay stream.
                    float coolingVariation=math.lerp(.82f,1.18f,math.frac(p.Phase*.731f));
                    float lostHeat=p.Temperature*(1-math.exp(-(p.Spark?1.8f:(Stationary?1.7f:2.35f)+(p.Contacts>0?.65f:0))*coolingVariation*particleStep));
                    if(p.Age>p.HotLifetime)lostHeat=p.Temperature*(1-math.exp(-10f*particleStep));
                    p.Temperature=math.max(0,p.Temperature-lostHeat);
                    p.Soot=p.Spark?0:math.saturate(p.Soot+lostHeat*1.25f-p.Soot*.45f*particleStep);
                    // Analytic curl forcing, NOT a pressure-projected Navier-Stokes solve.
                    float3 q=p.Position*.9f;float cool=1-math.smoothstep(.2f,.65f,p.Temperature);
                    float3 curl=new float3(math.sin(q.y+time*3.1f)-math.cos(q.z-time*2.4f),
                        math.sin(q.z+time*2.7f)-math.cos(q.x-time*3.1f),math.sin(q.x+time*2.4f)-math.cos(q.y-time*2.7f));
                    // Spatial curl and a coherent helical jet roll-up, not random camera-facing rotations.
                    float3 radial=p.Velocity-p.Up*math.dot(p.Velocity,p.Up);
                    float3 roll=math.normalizesafe(math.cross(p.Up,radial),new float3(1,0,0));
                    float3 fineCurl=new float3(math.sin(q.y*3.4f-time*4),math.sin(q.z*3.4f-time*4),math.sin(q.x*3.4f-time*4));
                    float3 wallRoll=0;
                    if(p.Contacts>0)wallRoll=math.cross(p.ClipPlaneA.xyz,p.Velocity)*(.5f+.4f*math.sin(p.Phase+time*4));
                    p.Velocity+=(p.Up*(1.6f+3*p.Temperature)+curl*(2.9f+cool*1.6f)+fineCurl*(1.8f+cool*.8f)+wallRoll+roll*math.sin(p.Phase+time*5.4f)*1.3f)*particleStep*(Stationary?.35f:1);
                    // Optional circulation rotates transported velocity, never parcel position.
                    // This avoids Euler energy gain; every resulting displacement is still swept.
                    if(p.AngularSpeed!=0)
                    {
                        float angle=p.AngularSpeed*particleStep;float3 vertical=p.Up*math.dot(p.Velocity,p.Up),planar=p.Velocity-vertical;
                        p.Velocity=vertical+planar*math.cos(angle)+math.cross(p.Up,planar)*math.sin(angle);
                    }
                    if((orbitGuided||sphereGuided)&&!p.Spark&&p.Contacts==0&&p.Age<p.HotLifetime)
                    {
                        float3 desired=sphereGuided?SphericalVelocity(p.Position,orbitCenter,orbitAxis,orbitRadius,orbitSpeed,orbitVelocity):OrbitalVelocity(p.Position,orbitCenter,orbitAxis,orbitRadius,orbitSpeed,orbitVelocity);
                        p.Velocity=math.lerp(p.Velocity,desired,1-math.exp(-18*guideStrength*particleStep));
                    }
                    // Cooler entrained gas loses jet momentum and becomes drifting smoke.
                    p.Velocity*=math.exp(-(.32f+1.9f*(1-math.smoothstep(.2f,.55f,p.Temperature)))*particleStep);
                    p.Radius=p.Spark?.025f*p.Size:(math.lerp(p.NozzleRadius,.09f,math.smoothstep(0,.16f,p.Age))+.25f*math.smoothstep(0,.2f,p.Age*p.DevelopmentScale)+.05f*math.smoothstep(.2f,.5f,p.Age*p.DevelopmentScale))*p.Size;
                    if (!Stationary && !p.Spark)
                        p.Radius *= math.lerp(TailRadiusScale(p.Age/p.HotLifetime*p.TailAgeScale,p.Distance/PathLimit),1,
                            math.smoothstep(0,.12f,math.max(0,p.Age-p.HotLifetime)));
                    if(!p.Spark&&p.Lifetime>p.HotLifetime&&p.SmokeExpansion>1)
                        p.Radius*=math.lerp(1,p.SmokeExpansion,math.smoothstep(0,1,(p.Age-p.HotLifetime)/(p.Lifetime-p.HotLifetime)));
                    float left=particleStep;
                    for(int collision=0;collision<3 && left>.00001f;collision++)
                    {
                        if(QueryCount>=QueryLimit){BudgetStops++;break;} // fail closed, never advance unqueried gas
                        float3 displacement=p.Velocity*left;
                        float length=math.length(displacement);
                        if(length<.00001f)break;
                        if(p.Distance+length>PathLimit)displacement*=math.max(0,PathLimit-p.Distance)/length;
                        QueryCount++;
                        if(!world.Sweep(p.Position,p.Radius,displacement,out var hit))
                        {p.Position+=displacement;p.Distance+=math.length(displacement);break;}
                        if(hit.Blocked){BlockedParcels++;p.Lifetime=0;break;}
                        float fraction=math.saturate(hit.Fraction);
                        float3 contactCenter=p.Position+displacement*fraction;
                        if(!math.all(math.isfinite(hit.Point))||!math.all(math.isfinite(hit.Normal))||math.lengthsq(hit.Normal)<.5f||
                            math.distance(contactCenter,hit.Point)>p.Radius+.035f)
                        {InvalidContacts++;LastInvalidContact=hit;LastInvalidCenter=contactCenter;LastInvalidRadius=p.Radius;p.Lifetime=0;break;} // A zero/invalid sweep point must never teleport gas.
                        p.Distance+=math.length(displacement)*fraction;
                        p.Position=contactCenter;
                        float3 normal=math.normalizesafe(hit.Normal,p.Up);
                        // Surface correction includes gradual hot-gas expansion and numerical skin.
                        float separation=math.dot(p.Position-hit.Point,normal);
                        p.Position+=normal*math.max(0,p.Radius+.004f-separation);
                        float4 plane=new float4(normal,-math.dot(normal,hit.Point)-.002f);
                        if(math.dot(p.ClipPlaneA.xyz,normal)<.96f)p.ClipPlaneB=p.ClipPlaneA;
                        p.ClipPlaneA=plane;
                        p.Velocity=Deflect(p.Velocity,hit.SurfaceVelocity,normal,p.Up,p.Phase);
                        // A short down-jet needs a bounded hot wall-jet phase after arrival.
                        // Extend once, never per collision; smoke still follows the same parcel.
                        if(p.Contacts==0&&!p.Spark&&p.SurfaceResidence>0)
                        {p.HotLifetime+=p.SurfaceResidence;p.Lifetime+=p.SurfaceResidence;}
                        p.Contacts++;Collisions++;
                        left*=1-fraction;
                    }
                    if(p.Lifetime<=0||(sphereGuided&&!FireTongueEvolution.OutsideActor(p.Position,p.Radius,p.Aspect,orbitCenter,orbitRadius)))
                    {Particles[i]=Particles[--Count];continue;}
                    Particles[i]=p;
                }
            }
        }
        // The same physical radius drives sweeps and the uploaded density support.
        // Preserve the established middle, then dissipate large tail parcels.
        public static float TailFraction(float ageFraction, float pathFraction) =>
            math.smoothstep(.48f,.90f,math.max(ageFraction,math.smoothstep(.75f,1f,pathFraction)));
        public static float TailRadiusScale(float ageFraction,float pathFraction) =>
            math.lerp(1f,.42f,TailFraction(ageFraction,pathFraction));
        public static float3 Deflect(float3 velocity,float3 surfaceVelocity,float3 normal,float3 up,float phase)
        {
            normal=math.normalizesafe(normal,new float3(0,1,0));
            float3 relative=velocity-surfaceVelocity;
            float inward=math.max(0,-math.dot(relative,normal));
            float3 tangent=relative-normal*math.dot(relative,normal);
            float3 a=math.normalizesafe(up-normal*math.dot(up,normal),FireContactMath.Tangent(normal));
            float3 b=math.cross(normal,a);
            // A head-on jet redistributes stopped momentum into a fan. Oblique flow preserves its tangent.
            float3 fan=a*math.cos(phase)+b*math.sin(phase);
            float3 along=math.normalizesafe(tangent+fan*inward*.55f,fan);
            float speed=math.sqrt(math.lengthsq(tangent)+inward*inward*.55f)*.92f;
            return surfaceVelocity+along*speed+normal*math.min(.25f,inward*.025f);
        }
    }
}
