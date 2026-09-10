using System;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
    public sealed class FireBoltTailProfileTests
    {
        private sealed class EmptySpace:IFireFlowCollision
        {public bool Sweep(float3 p,float radius,float3 d,out FireFlowHit hit){hit=default;return false;}}
        [Test] public void MovingTailConnectsToHotCoreAndRetainsCoolingWithinExistingPool()
        {
            foreach(float power in new[]{.9f,3.6f})foreach(float fps in new[]{30f,60f})
            {
                var profile=FireChargedBoltProfile.FromPower(power);var flow=new FireFlowParticleSolver(false,48);flow.SetSimulationRate(60);flow.Clear(673);
                var world=new EmptySpace();float worstFront=float.MinValue;int samples=0,smoke=0;
                for(int frame=0;frame<fps*2;frame++)
                {
                    float time=frame/fps;float3 origin=new float3(profile.Speed*time,0,0);
                    flow.Injection=FireBoltTailProfile.Injection(power,profile.Speed,time,67,new float3(1,0,0),new float3(0,1,0),false,48);
                    flow.Step(1/fps,true,1,origin,new float3(-1,0,0),new float3(0,1,0),world);
                    if(time<.5f)continue;float nearest=float.MaxValue;
                    for(int i=0;i<flow.Count;i++)
                    {
                        var p=flow.Particles[i];if(p.Spark)continue;
                        if(p.Age/p.HotLifetime>=FireBoltTailProfile.DetailStartAge&&p.Age<p.HotLifetime*.72f)
                            nearest=math.min(nearest,origin.x-p.Position.x-p.Radius*2.4f*.86f);
                        if(p.Age>p.HotLifetime+.04f&&p.Temperature<.55f)smoke++;
                    }
                    worstFront=math.max(worstFront,nearest);samples++;
                }
                Console.WriteLine($"power={power} fps={fps} worstFront={worstFront} core={profile.VisualRadius} smokeMean={smoke/(float)samples} birthsRejected={flow.RejectedBirths}");
                Assert.That(worstFront,Is.LessThan(profile.VisualRadius+.25f),"Fine transported flame must reach the hot core instead of beginning meters behind it");
                Assert.That(smoke/(float)samples,Is.GreaterThan(2),"The tail must retain actual cooling gas, not only hot parcels");
                Assert.That(flow.RejectedBirths,Is.Zero);Assert.That(flow.BudgetStops,Is.Zero);
            }
        }
        [Test] public void StoppingEmissionRetainsThenCompletelyDrainsCoolingTail()
        {
            var world=new EmptySpace();var flow=new FireFlowParticleSolver(false,48);flow.SetSimulationRate(60);flow.Clear(673);
            float3 forward=new float3(1,0,0),up=new float3(0,1,0),origin=default;
            for(int frame=0;frame<90;frame++)
            {
                float time=frame/60f;origin=forward*(35*time);
                flow.Injection=FireBoltTailProfile.Injection(3.6f,35,time,67,forward,up,false,48);
                flow.Step(1f/60,true,1,origin,-forward,up,world);
            }
            for(int frame=0;frame<12;frame++)flow.Step(1f/60,false,0,origin,-forward,up,world);
            int cooled=0;for(int i=0;i<flow.Count;i++)if(!flow.Particles[i].Spark&&flow.Particles[i].Age>flow.Particles[i].HotLifetime&&flow.Particles[i].Temperature<.55f)cooled++;
            Assert.That(cooled,Is.GreaterThan(1),"Emitter release must leave advected cooling smoke instead of clearing it");
            for(int frame=0;frame<60;frame++)flow.Step(1f/60,false,0,origin,-forward,up,world);
            Assert.That(flow.Count,Is.Zero,"Tail must retire fully within its bounded lifetime");Assert.That(flow.RejectedBirths,Is.Zero);Assert.That(flow.BudgetStops,Is.Zero);
        }
        [Test] public void FlyingTailKeepsHeldGrowthSeparateAndPreservesSweptBirths()
        {
            var full=FireChargedBoltProfile.Evaluate(1);
            var tail=FireBoltTailProfile.Injection(full.Power,full.Speed,0,1,new float3(0,0,1),new float3(0,1,0),false,48);
            Assert.That(tail.Lifetime,Is.LessThan(full.TailHotSeconds));Assert.That(tail.TrackEmitterPath,Is.True);
            Assert.That(tail.Velocity.z,Is.GreaterThan(full.Speed*.5f));Assert.That(tail.CoolingTail,Is.GreaterThan(.4f));
        }
    }
}
