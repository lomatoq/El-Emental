using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
    public sealed class FireBoltTrailMathTests
    {
        private sealed class Empty:IFireFlowCollision{public bool Sweep(float3 p,float r,float3 d,out FireFlowHit hit){hit=default;return false;}}
        private sealed class Wall:IFireFlowCollision
        {
            public bool blocked;public float radius;
            public bool Sweep(float3 p,float r,float3 d,out FireFlowHit hit)
            {radius=r;hit=default;if(blocked){hit.Blocked=true;return true;}if(p.x+d.x-r>-1)return false;hit.Fraction=(-1+r-p.x)/d.x;return true;}
        }
        [Test] public void AllThirtyAndSixtyFpsTailsUseContinuousHotGasChainWithinSixSweeps()
        {
            foreach(float power in new[]{.9f,3.6f})foreach(float fps in new[]{30f,60f})
            {
                var profile=FireChargedBoltProfile.FromPower(power);var flow=new FireFlowParticleSolver(false,48);flow.SetSimulationRate(60);flow.Clear(673);var world=new Empty();var spans=new FireBoltTrailSpan[6];
                for(int frame=0;frame<fps*2;frame++)
                {
                    float time=frame/fps;float3 origin=new float3(profile.Speed*time,0,0),up=new float3(0,1,0),forward=new float3(1,0,0);
                    flow.Injection=FireBoltTailProfile.Injection(power,profile.Speed,time,67,forward,up,false,48);flow.Step(1/fps,true,1,origin,-forward,up,world);
                    int count=FireBoltTrailMath.Build(true,origin,profile.VisualRadius,flow.Particles,flow.Count,world,spans,out int queries);
                    if(time<.15f)continue;
                    Assert.That(count,Is.InRange(3,6));Assert.That(queries,Is.LessThanOrEqualTo(6));Assert.That(math.distance(spans[0].A,origin),Is.LessThan(.001f));
                    for(int i=1;i<count;i++){Assert.That(math.distance(spans[i-1].B,spans[i].A),Is.LessThan(.0001f));Assert.That(spans[i].AgeA,Is.GreaterThanOrEqualTo(spans[i-1].AgeA));}
                    Assert.That(spans[count-1].RadiusB,Is.LessThan(spans[0].RadiusA));
                }
                Assert.That(flow.RejectedBirths,Is.Zero);Assert.That(flow.BudgetStops,Is.Zero);
            }
        }
        [Test] public void FullVisibleEnvelopeStopsAtWallAndNeverReconnectsBehindIt()
        {
            var gas=new FireFlowParticle[6];for(int i=0;i<6;i++)gas[i]=new FireFlowParticle{Position=new float3(-.5f*(i+1),0,0),Age=.1f+i*.15f,HotLifetime=1,Temperature=1};
            var spans=new FireBoltTrailSpan[6];var wall=new Wall();int count=FireBoltTrailMath.Build(true,0,.95f,gas,gas.Length,wall,spans,out int queries);
            Assert.That(count,Is.EqualTo(1));Assert.That(queries,Is.EqualTo(1));Assert.That(wall.radius,Is.EqualTo(spans[0].RadiusA));Assert.That(spans[0].B.x-wall.radius,Is.GreaterThan(-1));
            wall.blocked=true;Assert.That(FireBoltTrailMath.Build(true,0,.95f,gas,gas.Length,wall,spans,out _),Is.Zero);
        }
        [Test] public void ColdOrDiscontinuousGasCannotInventAHotTrail()
        {
            var spans=new FireBoltTrailSpan[6];var gas=new[]{new FireFlowParticle{Position=new float3(-10,0,0),Age=.1f,HotLifetime=1,Temperature=1}};
            Assert.That(FireBoltTrailMath.Build(true,0,.95f,gas,1,new Empty(),spans,out _),Is.Zero);
            gas[0].Position=new float3(-.5f,0,0);gas[0].Temperature=.2f;Assert.That(FireBoltTrailMath.Build(true,0,.95f,gas,1,new Empty(),spans,out _),Is.Zero);
            Assert.That(FireBoltTrailMath.Build(false,new float3(100,0,0),.95f,gas,0,new Empty(),spans,out _),Is.Zero);
        }
    }
}
