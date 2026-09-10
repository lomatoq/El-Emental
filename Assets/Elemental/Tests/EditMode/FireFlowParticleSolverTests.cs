using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class FireFlowParticleSolverTests
    {
        [Test] public void FiveGuidedSourcesCoverGreatCircleWithoutAngularHoles()
        {
            var flows=new FireFlowParticleSolver[5];var world=new PlaneWorld();var up=new float3(0,1,0);
            for(int n=0;n<5;n++){flows[n]=new FireFlowParticleSolver(false,32);flows[n].SetSimulationRate(45);flows[n].Clear((uint)(1+n*7919));flows[n].ConfigureOrbitalGuide(true,0,up,2.3f,8,0);}
            for(int frame=1;frame<=90;frame++)
            {
                for(int n=0;n<5;n++)
                {
                    float angle=n*math.PI*2/5-frame/60f*.7f;var radial=new float3(math.cos(angle),0,-math.sin(angle));var tangent=math.cross(up,radial);
                    flows[n].Injection=new FireFlowInjection(tangent*8,0,44,.55f,8,.86f,developmentScale:2,staggerBirths:true,birthTangent:tangent*.18f,coolingTail:.23f,uniformBirthSpread:true,smokeStride:4,trackEmitterPath:true);
                    flows[n].Step(1f/60,true,1,radial*2.3f,tangent,up,world);
                }
                if(frame<30||frame%15!=0)continue;
                var angles=new System.Collections.Generic.List<float>();foreach(var solver in flows)for(int index=0;index<solver.Count;index++){var p=solver.Particles[index];if(p.Spark||p.Age<=.02f||p.Age>=p.HotLifetime||p.Temperature<.45f)continue;angles.Add(math.atan2(p.Position.z,p.Position.x));}
                angles.Sort();float gap=0;for(int i=1;i<angles.Count;i++)gap=math.max(gap,angles[i]-angles[i-1]);gap=math.max(gap,angles[0]+math.PI*2-angles[angles.Count-1]);
                Assert.That(gap,Is.LessThan(.65f),"Hot angular gap at frame "+frame);
            }
            foreach(var solver in flows){Assert.That(solver.RejectedBirths,Is.Zero);Assert.That(solver.BudgetStops,Is.Zero);}
        }
        [Test] public void OrbitalGuidePreservesCircleAndStopsSteeringAfterContact()
        {
            var solver=new FireFlowParticleSolver(false,32);solver.SetSimulationRate(45);
            solver.Injection=new FireFlowInjection(new float3(0,0,-8),0,44,.55f,8,.86f,coolingTail:.23f,smokeStride:4);
            var center=new float3(0,2,0);solver.ConfigureOrbitalGuide(true,center,new float3(0,1,0),2.3f,8,0);
            var world=new PlaneWorld();
            for(int frame=0;frame<120;frame++)solver.Step(1f/60,true,1,center+new float3(2.3f,0,0),new float3(0,0,-1),new float3(0,1,0),world);
            Assert.That(solver.RejectedBirths,Is.Zero);Assert.That(solver.BudgetStops,Is.Zero);
            int following=0;
            for(int i=0;i<solver.Count;i++){var p=solver.Particles[i];if(p.Spark||p.Age>.45f)continue;var offset=p.Position-center;Assert.That(math.abs(math.length(offset.xz)-2.3f),Is.LessThan(.4f));following++;}
            Assert.That(following,Is.GreaterThan(10));
            // Force two identical already-contacted parcels; changing guide must have no effect.
            var a=new FireFlowParticleSolver(false,32);var b=new FireFlowParticleSolver(false,32);
            a.Step(1f/60,true,1,0,new float3(0,0,1),new float3(0,1,0),world);b.Step(1f/60,true,1,0,new float3(0,0,1),new float3(0,1,0),world);
            for(int i=0;i<a.Count;i++){a.Particles[i].Contacts=1;b.Particles[i]=a.Particles[i];}
            a.ConfigureOrbitalGuide(true,center,new float3(0,1,0),2.3f,8,0);
            a.Step(1f/60,false,1,0,new float3(0,0,1),new float3(0,1,0),world);b.Step(1f/60,false,1,0,new float3(0,0,1),new float3(0,1,0),world);
            for(int i=0;i<a.Count;i++)Assert.That(math.distance(a.Particles[i].Position,b.Particles[i].Position),Is.LessThan(.00001f));
        }
        [Test] public void OrbitalGuideStillHonorsSolidWallAndQueryBudget()
        {
            var solver=new FireFlowParticleSolver(false,32);solver.SetSimulationRate(45);solver.Injection=new FireFlowInjection(new float3(0,0,8),0,44,.55f,8,.86f,coolingTail:.23f,smokeStride:4);
            solver.ConfigureOrbitalGuide(true,new float3(0,0,2.3f),new float3(0,1,0),2.3f,8,0);var wall=new PlaneWorld{Wall=true};
            for(int frame=0;frame<90;frame++){solver.Step(1f/60,true,1,new float3(-2.3f,0,2.3f),new float3(0,0,1),new float3(0,1,0),wall);for(int i=0;i<solver.Count;i++)Assert.That(solver.Particles[i].Position.z,Is.LessThan(3));}
            Assert.That(solver.Collisions,Is.GreaterThan(0));Assert.That(solver.InvalidContacts,Is.Zero);Assert.That(solver.BudgetStops,Is.Zero);
        }
        private static FireFlowParticleSolver MovingTrail(int hz)
        {
            var solver=new FireFlowParticleSolver(false,48);solver.SetSimulationRate(60);var world=new PlaneWorld();
            solver.Injection=new FireFlowInjection(new float3(0,0,6),1.2f,128,.26f,8,.85f,
                developmentScale:3,tailAgeScale:1,staggerBirths:true,coolingTail:.32f,
                smokeExpansion:1.4f,smokeStride:4,trackEmitterPath:true);
            solver.Step(0,true,1,0,new float3(0,0,-1),new float3(0,1,0),world);
            for(int i=1;i<=hz;i++)solver.Step(1f/hz,true,1,new float3(0,0,24f*i/hz),new float3(0,0,-1),new float3(0,1,0),world);
            Assert.That(solver.BudgetStops,Is.Zero);Assert.That(solver.RejectedBirths,Is.Zero);
            return solver;
        }
        [Test] public void VariableHandPowerChangesWidthVelocityAndRangeWithoutIncreasingCapacityOrBirthRate()
        {
            var weak=new FireFlowParticleSolver();var normal=new FireFlowParticleSolver();var strong=new FireFlowParticleSolver();var world=new PlaneWorld();
            weak.ConfigureHandPower(.38f);normal.ConfigureHandPower(1);strong.ConfigureHandPower(2.5f);
            foreach(var solver in new[]{weak,normal,strong})solver.Step(1f/60,true,1,0,new float3(0,0,1),new float3(0,1,0),world);
            Assert.That(normal.PathLimit,Is.EqualTo(8));Assert.That(strong.PathLimit,Is.EqualTo(8*math.sqrt(2.5f)).Within(.0001f));
            Assert.That(weak.Count,Is.EqualTo(normal.Count));Assert.That(strong.Count,Is.EqualTo(normal.Count));
            Assert.That(strong.Particles[0].Radius,Is.GreaterThan(normal.Particles[0].Radius));
            Assert.That(weak.Particles[0].Radius,Is.LessThan(normal.Particles[0].Radius));
            Assert.That(strong.Particles[0].Velocity.z,Is.GreaterThan(normal.Particles[0].Velocity.z));
            Assert.That(strong.ParticleLimit,Is.EqualTo(192));Assert.That(strong.QueryLimit,Is.EqualTo(768));
        }
        [Test] public void OrbitalSourceAndTowerPresetsKeepThirtyTwoCapacityWithCooling()
        {
            var presets=new[]{
                new FireFlowInjection(new float3(7,0,0),.5f,100,.22f,5,.9f,developmentScale:2,staggerBirths:true,birthTangent:new float3(.48f,0,0),coolingTail:.23f,uniformBirthSpread:true,angularSpeed:2.5f,elongation:.9f,smokeExpansion:1.3f,smokeStride:4,trackEmitterPath:true),
                new FireFlowInjection(0,3.8f,76,.32f,3,1.1f,developmentScale:2,tailAgeScale:.9f,staggerBirths:true,coolingTail:.18f,elongation:.8f,smokeExpansion:1.3f,smokeStride:4),
                new FireFlowInjection(new float3(1.2f,0,0),4.2f,88,.25f,4,.85f,developmentScale:2,coolingTail:.28f,smokeStride:4)};
            foreach(var preset in presets)
            {
                var solver=new FireFlowParticleSolver(false,32);solver.SetSimulationRate(preset.Speed>.6f?90:45);solver.Injection=preset;var world=new FloorWorld();bool cooled=false;
                for(int frame=0;frame<180;frame++)
                {
                    solver.Step(1f/60,true,1,new float3(0,.48f,0),new float3(0,1,0),new float3(0,1,0),world);
                    for(int i=0;i<solver.Count;i++)cooled|=solver.Particles[i].Age>solver.Particles[i].HotLifetime;
                }
                Assert.That(cooled,Is.True);Assert.That(solver.RejectedBirths,Is.Zero,"births rate="+preset.Rate);Assert.That(solver.BudgetStops,Is.Zero,"queries rate="+preset.Rate);Assert.That(solver.InvalidContacts,Is.Zero,"contacts rate="+preset.Rate);
            }
        }
        [Test] public void RingAndPillarLifecyclesLeaveRoomForTransportedSmoke()
        {
            var presets=new[]{
                new FireFlowInjection(new float3(0,0,7),1.6f,196,.18f,4,1,developmentScale:2,coolingTail:.22f,smokeStride:8,trackEmitterPath:true),
                new FireFlowInjection(new float3(0,0,15),1.6f,180,.19f,8,1.15f,developmentScale:2,coolingTail:.22f,smokeStride:8,trackEmitterPath:true),
                new FireFlowInjection(new float3(0,0,15),1.6f,180,.15f,8,1.15f,developmentScale:2,coolingTail:.3f,smokeStride:4,trackEmitterPath:true),
                new FireFlowInjection(new float3(1.2f,0,0),4.2f,136,.25f,4,.85f,developmentScale:2,coolingTail:.28f,smokeStride:4)};
            foreach(var injection in presets)
            {
                var solver=new FireFlowParticleSolver(false,48);solver.SetSimulationRate(45);solver.Injection=injection;
                var world=new FloorWorld();bool smoke=false;
                for(int frame=0;frame<120;frame++)
                {
                    solver.Step(1f/60,true,1,new float3(0,.48f,frame*.02f),new float3(0,1,0),new float3(0,1,0),world);
                    for(int i=0;i<solver.Count;i++)smoke|=!solver.Particles[i].Spark&&solver.Particles[i].Age>solver.Particles[i].HotLifetime;
                }
                Assert.That(smoke,Is.True);Assert.That(solver.RejectedBirths,Is.Zero);
                Assert.That(solver.BudgetStops,Is.Zero);Assert.That(solver.InvalidContacts,Is.Zero);
            }
        }
        [Test] public void MovingEmitterHasComparableContinuousTrailAt15And120Frames()
        {
            var low=MovingTrail(15);var high=MovingTrail(120);int matched=0;
            float minHot=24,maxHot=0;
            for(int i=0;i<low.Count;i++)
            {
                var a=low.Particles[i];
                if(!a.Spark&&a.Age<a.HotLifetime){minHot=math.min(minHot,a.Position.z);maxHot=math.max(maxHot,a.Position.z);}
                for(int j=0;j<high.Count;j++)if(a.Id==high.Particles[j].Id)
                {Assert.That(math.distance(a.Position,high.Particles[j].Position),Is.LessThan(.12f));matched++;break;}
            }
            Assert.That(matched,Is.GreaterThan(30));
            Assert.That(maxHot-minHot,Is.GreaterThan(3.5f));
            // Every .45m interval of the developed hot trail contains actual transported gas.
            for(float z=minHot+.2f;z<maxHot-.2f;z+=.2f)
            {
                float nearest=100;
                for(int i=0;i<low.Count;i++)if(!low.Particles[i].Spark&&low.Particles[i].Age<low.Particles[i].HotLifetime)
                    nearest=math.min(nearest,math.abs(low.Particles[i].Position.z-z));
                Assert.That(nearest,Is.LessThan(.225f));
            }
        }
        [Test] public void EmitterTrajectoryCannotStampBirthsAcrossWall()
        {
            var s=new FireFlowParticleSolver(false,48);var w=new PlaneWorld{Wall=true};
            s.Injection=new FireFlowInjection(0,0,128,.26f,8,.8f,trackEmitterPath:true);
            s.Step(0,true,1,new float3(0,0,2),new float3(0,0,-1),new float3(0,1,0),w);
            s.Step(.05f,true,1,new float3(0,0,4),new float3(0,0,-1),new float3(0,1,0),w);
            Assert.That(s.RejectedBirths,Is.GreaterThan(0));Assert.That(s.Count,Is.GreaterThan(0));
            for(int i=0;i<s.Count;i++)Assert.That(s.Particles[i].Position.z+s.Particles[i].Radius,Is.LessThan(3.006f));
            Assert.That(s.InvalidContacts,Is.Zero);
        }
        private sealed class FloorWorld:IFireFlowCollision
        {
            public bool Sweep(float3 p,float r,float3 d,out FireFlowHit hit)
            {
                hit=default;float f=p.y<r?0:d.y<0?(r-p.y)/d.y:2;
                if(f<0||f>1)return false;
                float3 center=p+d*f;hit=new FireFlowHit{Fraction=f,Normal=new float3(0,1,0),Point=new float3(center.x,0,center.z)};return true;
            }
        }
        [Test] public void FootContactKeepsOneBoundedHotSurfacePhaseAndThenCools()
        {
            var s=new FireFlowParticleSolver(false,96);var w=new FloorWorld();
            s.Injection=new FireFlowInjection(0,16,240,.18f,5,.8f,developmentScale:3,staggerBirths:true,
                coolingTail:.28f,trackEmitterPath:true,surfaceResidence:.12f);
            bool contactHot=false,smoke=false;float spread=0;
            for(int step=0;step<90;step++)
            {
                s.Step(1f/90,step<45,1,new float3(0,1.2f,0),new float3(0,-1,0),new float3(0,1,0),w);
                for(int i=0;i<s.Count;i++)
                {
                    var p=s.Particles[i];Assert.That(p.Position.y-p.Radius,Is.GreaterThanOrEqualTo(-.001f));
                    Assert.That(p.HotLifetime,Is.LessThanOrEqualTo(.18f*1.1f+.12001f));
                    if(p.Contacts>0&&!p.Spark)
                    {spread=math.max(spread,math.length(p.Position.xz));contactHot|=p.Age>.20f&&p.Age<p.HotLifetime;smoke|=p.Age>p.HotLifetime&&p.Temperature<.4f;}
                }
            }
            Assert.That(contactHot,Is.True);Assert.That(smoke,Is.True);Assert.That(spread,Is.GreaterThan(1.2f));
            Assert.That(s.InvalidContacts,Is.Zero);Assert.That(s.BudgetStops,Is.Zero);
        }
        [Test] public void TailRadiusKeepsMiddleAndDissipatesMonotonically()
        {
            Assert.That(FireFlowParticleSolver.TailRadiusScale(.40f,.5f),Is.EqualTo(1f));
            float previous=1;
            for(int i=48;i<=100;i++)
            {
                float scale=FireFlowParticleSolver.TailRadiusScale(i*.01f,.5f);
                Assert.That(scale,Is.InRange(.42f-.00001f,previous+.00001f)); previous=scale;
            }
            Assert.That(previous,Is.EqualTo(.42f).Within(.00001f));
            Assert.That(FireFlowParticleSolver.TailRadiusScale(.2f,.98f),Is.LessThan(.5f));
        }
        [Test] public void MovingTailUsesReducedRadiusInActualQueries()
        {
            var solver=new FireFlowParticleSolver(); var world=new PlaneWorld();
            solver.Step(1f/90,true,1,0,new float3(0,0,1),new float3(0,1,0),world);
            bool sawTail=false;
            for(int step=0;step<70;step++)
            {
                solver.Step(1f/90,false,0,0,new float3(0,0,1),new float3(0,1,0),world);
                for(int i=0;i<solver.Count;i++)
                {
                    var p=solver.Particles[i];
                    if(p.Spark || p.Age/p.Lifetime<.75f)continue;
                    sawTail=true;
                    Assert.That(p.Radius/p.Size,Is.LessThan(.27f));
                    Assert.That(math.all(math.isfinite(p.Position)),Is.True);
                }
            }
            Assert.That(sawTail,Is.True);
            Assert.That(solver.InvalidContacts,Is.Zero);
        }
        [Test] public void InjectionInheritsMotionWithoutMovingExistingParcelsOrChangingDefaultBudget()
        {
            var solver=new FireFlowParticleSolver(true);var world=new PlaneWorld();
            Assert.That(solver.PathLimit,Is.EqualTo(3));
            solver.Injection=new FireFlowInjection(new float3(0,0,24),.6f,48,.35f,12,.6f);
            for(int step=0;step<4;step++)solver.Step(1f/90,true,1,0,new float3(0,0,-1),new float3(0,1,0),world);
            Assert.That(solver.Count,Is.GreaterThan(0));
            float3 before=solver.Particles[0].Position;
            Assert.That(solver.Particles[0].Velocity.z,Is.GreaterThan(20));
            solver.Injection=new FireFlowInjection(new float3(0,8,0),16,72,.32f,8,.8f);
            Assert.That(math.distance(before,solver.Particles[0].Position),Is.Zero);
            Assert.That(solver.ParticleLimit,Is.EqualTo(32));Assert.That(solver.QueryLimit,Is.EqualTo(224));
            for(int step=0;step<60;step++)solver.Step(1f/90,false,0,0,new float3(0,-1,0),new float3(0,1,0),world);
            Assert.That(solver.Count,Is.Zero);
        }
        [Test] public void DownJetOvercomesUpwardOwnerMotionAndStillRespectsContactPlane()
        {
            var solver=new FireFlowParticleSolver(true);var world=new PlaneWorld{Wall=true};
            solver.Injection=new FireFlowInjection(new float3(0,8,0),16,72,.32f,8,.8f);
            for(int step=0;step<4;step++)solver.Step(1f/90,true,1,0,new float3(0,-1,0),new float3(0,1,0),world);
            Assert.That(solver.Particles[0].Velocity.y,Is.LessThan(-6));
            solver.Clear();solver.Injection=new FireFlowInjection(new float3(0,0,24),.6f,48,.35f,12,.6f);
            for(int step=0;step<90;step++)
            {
                solver.Step(1f/90,true,1,0,new float3(0,0,-1),new float3(0,1,0),world);
                for(int i=0;i<solver.Count;i++)Assert.That(solver.Particles[i].Position.z+solver.Particles[i].Radius,Is.LessThanOrEqualTo(3.006f));
            }
            Assert.That(solver.Collisions,Is.GreaterThan(0));Assert.That(solver.InvalidContacts,Is.Zero);
        }
        private sealed class PlaneWorld:IFireFlowCollision
        {
            public bool Wall,Corner,Blocked;
            public bool Sweep(float3 p,float r,float3 d,out FireFlowHit hit)
            {
                hit=default;if(Blocked){hit.Blocked=true;return true;}
                if(Wall&&p.z+r>3){hit=new FireFlowHit{Normal=new float3(0,0,-1),Point=new float3(p.x,p.y,3)};return true;}
                if(Corner&&p.x+r>1){hit=new FireFlowHit{Normal=new float3(-1,0,0),Point=new float3(1,p.y,p.z)};return true;}
                float nearest=2;float3 normal=default;
                if(Wall&&d.z>0){float f=(3-r-p.z)/d.z;if(f>=0&&f<=1&&f<nearest){nearest=f;normal=new float3(0,0,-1);}}
                if(Corner&&d.x>0){float f=(1-r-p.x)/d.x;if(f>=0&&f<=1&&f<nearest){nearest=f;normal=new float3(-1,0,0);}}
                if(nearest>1)return false;
                hit=new FireFlowHit{Fraction=nearest,Normal=normal,Point=p+d*nearest-normal*r};return true;
            }
        }
        private sealed class BadContactWorld:IFireFlowCollision
        {
            public bool Sweep(float3 p,float r,float3 d,out FireFlowHit hit){hit=new FireFlowHit{Normal=new float3(0,0,-1),Point=0};return true;}
        }
        [Test] public void TransportedHeatCoolsIntoSootBeforeRetirementAndRemainsBounded()
        {
            var solver=new FireFlowParticleSolver();var world=new PlaneWorld();
            solver.Step(1f/90,true,1,0,new float3(0,0,1),new float3(0,1,0),world);
            uint id=solver.Particles[0].Id;float previous=solver.Particles[0].Temperature;
            bool sawCoolingSmoke=false;
            for(int step=0;step<100;step++)
            {
                solver.Step(1f/90,false,0,0,new float3(0,0,1),new float3(0,1,0),world);
                for(int i=0;i<solver.Count;i++)if(solver.Particles[i].Id==id)
                {
                    var p=solver.Particles[i];
                    Assert.That(p.Temperature,Is.InRange(0,previous));
                    Assert.That(p.Soot,Is.InRange(0,1));
                    Assert.That(math.all(math.isfinite(p.Position)),Is.True);
                    if(p.Temperature<.35f && p.Soot>.25f)sawCoolingSmoke=true;
                    previous=p.Temperature;
                }
            }
            Assert.That(sawCoolingSmoke,Is.True,"The transported parcel must have a visible cooling phase before removal.");
            Assert.That(solver.Count,Is.Zero);
        }
        [Test] public void WallFlowRetainsSmallTonguesAndLargerBodyScales()
        {
            var solver=new FireFlowParticleSolver();var world=new PlaneWorld{Wall=true};float small=100,large=0;
            for(int step=0;step<90;step++)
            {
                solver.Step(1f/90,true,1,0,new float3(0,0,1),new float3(0,1,0),world);
                for(int i=0;i<solver.Count;i++)if(!solver.Particles[i].Spark && solver.Particles[i].Contacts>0)
                {small=math.min(small,solver.Particles[i].Radius);large=math.max(large,solver.Particles[i].Radius);}
            }
            Assert.That(large/small,Is.GreaterThan(2.3f),"Wall impact must retain multiple physical/render scales.");
            Assert.That(solver.InvalidContacts,Is.Zero);
            Assert.That(solver.Count,Is.LessThanOrEqualTo(FireFlowParticleSolver.Capacity));
        }
        [Test] public void DefaultWorldOriginContactCannotTeleportDistantGas()
        {
            var solver=new FireFlowParticleSolver();solver.Step(1f/30,true,1,new float3(0,100,0),new float3(0,0,1),new float3(0,1,0),new BadContactWorld());
            Assert.That(solver.InvalidContacts,Is.GreaterThan(0));Assert.That(solver.Count,Is.Zero);
        }
        [Test] public void HeadOnMomentumSpreadsAlongWallWithoutEnergyAmplification()
        {
            float3 normal=new float3(0,0,-1),up=new float3(0,1,0),incoming=new float3(0,0,14);
            float3 a=FireFlowParticleSolver.Deflect(incoming,0,normal,up,0);
            float3 b=FireFlowParticleSolver.Deflect(incoming,0,normal,up,math.PI);
            Assert.That(a.y,Is.GreaterThan(8));Assert.That(b.y,Is.LessThan(-8));
            Assert.That(a.z,Is.LessThanOrEqualTo(0));Assert.That(math.length(a),Is.LessThan(math.length(incoming)));
        }
        [Test] public void ObliqueImpactKeepsTangentialDirectionAndMovingWallVelocity()
        {
            float3 result=FireFlowParticleSolver.Deflect(new float3(8,0,10),new float3(0,2,0),new float3(0,0,-1),new float3(0,1,0),1);
            Assert.That(result.x,Is.GreaterThan(4));Assert.That(result.z,Is.LessThanOrEqualTo(0));Assert.That(math.all(math.isfinite(result)),Is.True);
        }
        [Test] public void ActualTransportHitsHeadOnPlaneAndFansOutWithoutCrossing()
        {
            var world=new PlaneWorld{Wall=true};var solver=new FireFlowParticleSolver();float spread=0;
            for(int i=0;i<90;i++)
            {
                solver.Step(1f/90,true,1,0,new float3(0,0,1),new float3(0,1,0),world);
                Assert.That(solver.QueryCount,Is.LessThanOrEqualTo(FireFlowParticleSolver.MaximumQueriesPerStep));
                for(int p=0;p<solver.Count;p++){Assert.That(solver.Particles[p].Position.z,Is.LessThan(3));if(solver.Particles[p].Contacts>0)spread=math.max(spread,math.length(solver.Particles[p].Position.xy));}
            }
            Assert.That(solver.Collisions,Is.GreaterThan(20));Assert.That(spread,Is.GreaterThan(.7f));Assert.That(solver.Count,Is.InRange(20,192));
        }
        [Test] public void OldParticlesKeepMomentumWhenAimTurnsAndReleaseDrains()
        {
            var world=new PlaneWorld();var solver=new FireFlowParticleSolver();
            for(int i=0;i<20;i++)solver.Step(1f/90,true,1,0,new float3(0,0,1),new float3(0,1,0),world);
            uint id=solver.Particles[0].Id;float3 before=solver.Particles[0].Position;
            solver.Step(1f/90,true,1,0,new float3(1,0,0),new float3(0,1,0),world);
            bool found=false;for(int i=0;i<solver.Count;i++)if(solver.Particles[i].Id==id){found=true;Assert.That(solver.Particles[i].Position.z,Is.GreaterThan(before.z));Assert.That(solver.Particles[i].Velocity.z,Is.GreaterThan(10));}
            Assert.That(found,Is.True);
            for(int i=0;i<100;i++)solver.Step(1f/90,false,0,0,new float3(1,0,0),new float3(0,1,0),world);
            Assert.That(solver.Count,Is.Zero);
        }
        [Test] public void CornerKeepsAllCentersOutsideBothWallsAndBounded()
        {
            var solver=new FireFlowParticleSolver();var world=new PlaneWorld{Wall=true,Corner=true};
            for(int i=0;i<120;i++)
            {
                solver.Step(1f/90,true,1,0,math.normalize(new float3(.2f,0,1)),new float3(0,1,0),world);
                for(int p=0;p<solver.Count;p++){Assert.That(solver.Particles[p].Position.x,Is.LessThan(1.001f));Assert.That(solver.Particles[p].Position.z,Is.LessThan(3.001f));}
            }
            Assert.That(solver.Collisions,Is.GreaterThan(20));
        }
        [Test] public void InsideSolidOrSaturatedQueryRetiresGasWithoutUnqueriedMotion()
        {
            var solver=new FireFlowParticleSolver();solver.Step(1f/30,true,1,0,new float3(0,0,1),new float3(0,1,0),new PlaneWorld{Blocked=true});
            Assert.That(solver.Count,Is.Zero);
        }
        [Test] public void DenseHandModeAbilityPresetsKeepExplicitCapacityAndSweepBudget()
        {
            var solver=new FireFlowParticleSolver(false,96);
            solver.Injection=new FireFlowInjection(new float3(0,8,0),26,360,.26f,8,.72f);
            Assert.That(new FireFlowParticleSolver().QueryLimit,Is.EqualTo(768));
            Assert.That(new FireFlowParticleSolver(true).QueryLimit,Is.EqualTo(224));
            Assert.That(solver.ParticleLimit,Is.EqualTo(96));
            var world=new PlaneWorld();int peak=0;
            for(int i=0;i<90;i++)
            {
                solver.Step(1f/90,true,1,0,new float3(0,-1,0),new float3(0,1,0),world);
                peak=math.max(peak,solver.Count);
                Assert.That(solver.QueryCount,Is.LessThanOrEqualTo(576));
                Assert.That(solver.Count,Is.LessThanOrEqualTo(96));
            }
            Assert.That(peak,Is.GreaterThan(60),"A foot jet needs a coherent dense body rather than a few stationary puffs.");
            Assert.That(solver.InvalidContacts,Is.Zero);
        }
        [Test] public void CoolTorchInjectionStartsInTransportedCoolingState()
        {
            var solver=new FireFlowParticleSolver(false,32);
            solver.Injection=new FireFlowInjection(0,1.7f,32,.8f,3,.8f,.58f);
            for(int i=0;i<5;i++)solver.Step(1f/90,true,1,0,new float3(0,1,0),new float3(0,1,0),new PlaneWorld());
            Assert.That(solver.Count,Is.GreaterThan(0));
            for(int i=0;i<solver.Count;i++)Assert.That(solver.Particles[i].Temperature,Is.InRange(0,.58f));
            Assert.That(solver.Stationary,Is.False,"Torches use the same transported tail refinement as hand fire.");
        }
        [Test] public void ShortJetStaggersSweptBirthsAndDevelopsBodyWithoutExtraCapacity()
        {
            var solver=new FireFlowParticleSolver(false,96);
            solver.Injection=new FireFlowInjection(0,20,360,.26f,8,.9f,developmentScale:3,tailAgeScale:.65f,staggerBirths:true);
            solver.Step(1f/90,true,1,0,new float3(0,-1,0),new float3(0,1,0),new PlaneWorld());
            Assert.That(solver.Count,Is.EqualTo(4));
            float closest=100,furthest=0;
            for(int i=0;i<solver.Count;i++){float distance=math.abs(solver.Particles[i].Position.y);closest=math.min(closest,distance);furthest=math.max(furthest,distance);}
            Assert.That(closest,Is.LessThan(.05f),"A physically swept young parcel remains connected to the nozzle.");
            Assert.That(furthest-closest,Is.GreaterThan(.12f),"One birth batch fills the stream instead of a co-located bead.");
            Assert.That(solver.QueryCount,Is.EqualTo(4));
            Assert.That(solver.ParticleLimit,Is.EqualTo(96));
            var wall=new PlaneWorld{Wall=true};
            for(int i=0;i<90;i++)
            {
                solver.Step(1f/90,true,1,0,new float3(0,0,1),new float3(0,1,0),wall);
                for(int j=0;j<solver.Count;j++)Assert.That(solver.Particles[j].Position.z,Is.LessThan(3.001f));
            }
            Assert.That(solver.InvalidContacts,Is.Zero);
        }
        [Test] public void CompactSurfaceFireReallyDeflectsAndDrainsWithinItsShortLifetime()
        {
            var solver=new FireFlowParticleSolver(false,48);
            solver.Injection=new FireFlowInjection(new float3(1.8f,0,0),3.6f,220,.20f,4,1,developmentScale:3,tailAgeScale:.85f,staggerBirths:true);
            var world=new PlaneWorld{Wall=true};
            float maximumSpread=0;int maximumCount=0;
            for(int step=0;step<90;step++)
            {
                solver.Step(1f/90,true,1,new float3(0,0,2.86f),new float3(0,0,1),new float3(0,0,-1),world);
                maximumCount=math.max(maximumCount,solver.Count);
                for(int i=0;i<solver.Count;i++)
                {
                    Assert.That(solver.Particles[i].Position.z,Is.LessThan(3.001f));
                    if(solver.Particles[i].Contacts>0)maximumSpread=math.max(maximumSpread,math.length(solver.Particles[i].Position.xy));
                }
                Assert.That(solver.QueryCount,Is.LessThanOrEqualTo(288));
            }
            Assert.That(solver.Collisions,Is.GreaterThan(20));
            Assert.That(maximumSpread,Is.GreaterThan(.2f));
            Assert.That(maximumCount,Is.LessThanOrEqualTo(48));
            Assert.That(solver.InvalidContacts,Is.Zero);
            for(int step=0;step<23;step++)solver.Step(1f/90,false,0,0,new float3(0,0,1),new float3(0,0,-1),world);
            Assert.That(solver.Count,Is.Zero);
        }
        [Test] public void CompactHotFlameLeavesBoundedTransportedCoolingSmokeAfterRelease()
        {
            var solver=new FireFlowParticleSolver(false,96);
            solver.Injection=new FireFlowInjection(0,20,360,.18f,8,.9f,developmentScale:3,tailAgeScale:.85f,staggerBirths:true,coolingTail:.32f);
            var world=new PlaneWorld();
            for(int step=0;step<30;step++)solver.Step(1f/90,true,1,0,new float3(0,-1,0),new float3(0,1,0),world);
            for(int step=0;step<20;step++)solver.Step(1f/90,false,0,0,new float3(0,-1,0),new float3(0,1,0),world);
            Assert.That(solver.Count,Is.GreaterThan(0),"Short flame lifetime must leave real transported cooling gas.");
            for(int i=0;i<solver.Count;i++)
            {
                var p=solver.Particles[i];Assert.That(p.Age,Is.GreaterThan(p.HotLifetime));
                Assert.That(p.Temperature,Is.LessThan(.6f));Assert.That(p.Soot,Is.GreaterThan(.3f));
                Assert.That(math.all(math.isfinite(p.Position)),Is.True);
            }
            Assert.That(solver.Count,Is.LessThanOrEqualTo(96));
            for(int step=0;step<50;step++)solver.Step(1f/90,false,0,0,new float3(0,-1,0),new float3(0,1,0),world);
            Assert.That(solver.Count,Is.Zero);
        }
        [Test] public void RingRateReducesSweepsWithoutSkippingElapsedTimeOrCornerContact()
        {
            var fast=new FireFlowParticleSolver(false,48);var ring=new FireFlowParticleSolver(false,48);ring.SetSimulationRate(45);
            var injection=new FireFlowInjection(new float3(1,0,0),13,200,.19f,8,1.15f,developmentScale:2,tailAgeScale:.85f,staggerBirths:true,birthTangent:new float3(.4f,0,0),coolingTail:.22f);
            fast.Injection=ring.Injection=injection;var world=new PlaneWorld{Wall=true,Corner=true};int fastQueries=0,ringQueries=0;
            for(int frame=0;frame<30;frame++)
            {
                fast.Step(.05f,true,1,0,new float3(0,0,1),new float3(0,1,0),world);
                ring.Step(.05f,true,1,0,new float3(0,0,1),new float3(0,1,0),world);
                fastQueries+=fast.QueryCount;ringQueries+=ring.QueryCount;
                Assert.That(ring.Substeps,Is.EqualTo(3));Assert.That(fast.Substeps,Is.EqualTo(5));
                for(int i=0;i<ring.Count;i++){Assert.That(ring.Particles[i].Position.z,Is.LessThan(3.001f));Assert.That(ring.Particles[i].Position.x,Is.LessThan(1.001f));}
            }
            Assert.That(ring.Count,Is.GreaterThan(24));Assert.That(ring.Collisions,Is.GreaterThan(0));
            Assert.That(ring.InvalidContacts,Is.Zero);Assert.That(ring.DroppedSeconds,Is.Zero);
            Assert.That(ringQueries,Is.LessThan(fastQueries*.8f),"Reducing cosmetic integration frequency must save real sweeps.");
            for(int i=0;i<12;i++)ring.Step(.05f,false,0,0,new float3(0,0,1),new float3(0,1,0),world);
            Assert.That(ring.Count,Is.Zero);
        }
        [Test] public void UniformSectorBirthsCoverRibbonWithoutUnqueriedWallCrossing()
        {
            var solver=new FireFlowParticleSolver(false,48);solver.SetSimulationRate(45);
            solver.Injection=new FireFlowInjection(0,0,480,.19f,8,1,birthTangent:new float3(2,0,0),uniformBirthSpread:true);
            solver.Step(1f/15,true,1,0,new float3(0,0,1),new float3(0,1,0),new PlaneWorld());
            float[] positions=new float[solver.Count];for(int i=0;i<solver.Count;i++)positions[i]=solver.Particles[i].Position.x;
            System.Array.Sort(positions);float gap=0;for(int i=1;i<positions.Length;i++)gap=math.max(gap,positions[i]-positions[i-1]);
            Assert.That(positions.Length,Is.GreaterThan(28));Assert.That(gap,Is.LessThan(.35f));
            solver.Clear();var corner=new PlaneWorld{Wall=true,Corner=true};
            for(int i=0;i<30;i++){solver.Step(.05f,true,1,0,new float3(0,0,1),new float3(0,1,0),corner);for(int j=0;j<solver.Count;j++)Assert.That(solver.Particles[j].Position.x,Is.LessThan(1.001f));}
            Assert.That(solver.InvalidContacts,Is.Zero);
        }
        [Test] public void SlowerBoltGasLeavesHotTrailThenCoolingSmokeBehindProjectile()
        {
            var solver=new FireFlowParticleSolver(false,48);solver.Injection=new FireFlowInjection(new float3(0,0,13.2f),1.2f,240,.17f,8,1.15f,staggerBirths:true,coolingTail:.32f);
            var world=new PlaneWorld();float time=0;
            for(int i=0;i<30;i++){time+=1f/90;solver.Step(1f/90,true,1,new float3(0,0,time*24),new float3(0,0,-1),new float3(0,1,0),world);}
            float hotTrail=0;bool smoke=false;
            for(int i=0;i<solver.Count;i++){var p=solver.Particles[i];if(p.Age<p.HotLifetime)hotTrail=math.max(hotTrail,time*24-p.Position.z);else if(p.Temperature<.6f)smoke=true;}
            Assert.That(hotTrail,Is.GreaterThan(1.3f));Assert.That(smoke,Is.True);Assert.That(solver.Count,Is.LessThanOrEqualTo(48));
        }
        [Test] public void RingCirculationTurnsVelocityWithoutTeleportingOrAmplifyingSpeed()
        {
            var straight=new FireFlowParticleSolver(false,48);var swirl=new FireFlowParticleSolver(false,48);
            straight.Injection=new FireFlowInjection(new float3(10,0,0),0,240,.3f,8,1);
            swirl.Injection=new FireFlowInjection(new float3(10,0,0),0,240,.3f,8,1,angularSpeed:1.25f,elongation:.85f);
            var world=new PlaneWorld();
            for(int i=0;i<11;i++)
            {
                straight.Step(1f/90,i==0,1,0,new float3(1,0,0),new float3(0,1,0),world);
                swirl.Step(1f/90,i==0,1,0,new float3(1,0,0),new float3(0,1,0),world);
            }
            var a=straight.Particles[0];var b=swirl.Particles[0];
            Assert.That(b.Velocity.z,Is.LessThan(a.Velocity.z-.8f));
            Assert.That(math.length(b.Velocity),Is.LessThan(math.length(a.Velocity)*1.05f));
            Assert.That(b.Position.z,Is.LessThan(a.Position.z));Assert.That(b.Aspect,Is.InRange(1.9f,2.05f));
        }
        [Test] public void ShortImpactFlashEndsHotEmissionThenKeepsExpandingSweptSmoke()
        {
            var solver=new FireFlowParticleSolver(false,48);
            solver.Injection=new FireFlowInjection(0,7,480,.08f,3,1.1f,developmentScale:3,staggerBirths:true,coolingTail:.32f,smokeExpansion:2,smokeStride:4);
            var world=new PlaneWorld();
            for(int frame=0;frame<18;frame++)solver.Step(1f/90,frame<4,1,0,new float3(0,1,0),new float3(0,1,0),world);
            Assert.That(solver.Count,Is.GreaterThan(2));
            for(int i=0;i<solver.Count;i++)
            {
                var p=solver.Particles[i];Assert.That(p.Age,Is.GreaterThan(p.HotLifetime));
                Assert.That(p.Temperature,Is.LessThan(.45f));Assert.That(p.SmokeExpansion,Is.EqualTo(2));
                Assert.That(math.all(math.isfinite(p.Position)),Is.True);
            }
            Assert.That(solver.Count,Is.LessThanOrEqualTo(48));Assert.That(solver.InvalidContacts,Is.Zero);
            for(int i=0;i<40;i++)solver.Step(1f/90,false,0,0,new float3(0,1,0),new float3(0,1,0),world);
            Assert.That(solver.Count,Is.Zero);
        }
    }
}
