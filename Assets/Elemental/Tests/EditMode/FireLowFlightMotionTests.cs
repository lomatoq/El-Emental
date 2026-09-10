using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
    public sealed class FireLowFlightMotionTests
    {
        [Test]public void SurfChordYieldsToAllFireCastingAndSpace()
        {
            Assert.That(FireLowFlightMotion.WantsFlight(true,1,false,false,false,false),Is.True);
            Assert.That(FireLowFlightMotion.WantsFlight(true,1,true,false,false,false),Is.False);
            Assert.That(FireLowFlightMotion.WantsFlight(true,1,false,true,false,false),Is.False);
            Assert.That(FireLowFlightMotion.WantsFlight(true,1,false,false,true,false),Is.False);
            Assert.That(FireLowFlightMotion.WantsFlight(true,1,false,false,false,true),Is.False);
            Assert.That(FireLowFlightMotion.WantsFlight(true,-1,false,false,false,false),Is.False);
        }
        [Test]public void ServoConvergesWithoutTeleportingAndIgnoresWorldAxis()
        {
            foreach(var up in new[]{new float3(0,1,0),new float3(1,0,0),math.normalize(new float3(1,2,3))})
            {
                var forward=math.normalizesafe(math.cross(up,new float3(0,0,1)));var velocity=float3.zero;float height=0,dt=1f/60;
                for(int i=0;i<180;i++)
                {
                    var delta=FireLowFlightMotion.VelocityChange(velocity,up,forward,height,-14,dt,float.PositiveInfinity,float.PositiveInfinity);
                    Assert.That(math.length(delta-up*math.dot(delta,up)),Is.LessThanOrEqualTo(FireLowFlightMotion.Acceleration*dt+.001f));
                    velocity+=delta-up*(14*dt);height+=math.dot(velocity,up)*dt;
                }
                Assert.That(height,Is.EqualTo(FireLowFlightMotion.Height).Within(.02f));
                Assert.That(math.dot(velocity,forward),Is.EqualTo(FireLowFlightMotion.MaximumSpeed).Within(.01f));
            }
        }
        [Test]public void SweptClearanceStopsFullSpeedBeforeWallAndCeiling()
        {
            var up=new float3(0,1,0);var velocity=new float3(0,2,15);float dt=1f/60;
            var next=velocity+FireLowFlightMotion.VelocityChange(velocity,up,new float3(0,0,1),0,-14,dt,.02f,0)-up*(14*dt);
            Assert.That(next.z*dt,Is.LessThanOrEqualTo(.02001f));Assert.That(next.y,Is.LessThanOrEqualTo(.0001f));
        }
        [Test]public void LostSupportAndNonFiniteSamplesNeverGenerateThrust()
        {
            Assert.That(FireLowFlightMotion.VelocityChange(float3.zero,new float3(0,1,0),new float3(0,0,1),2,-14,.02f,1,1),Is.EqualTo(float3.zero));
            Assert.That(FireLowFlightMotion.VelocityChange(float3.zero,new float3(0,1,0),new float3(0,0,1),float.NaN,-14,.02f,1,1),Is.EqualTo(float3.zero));
        }
    }
}
