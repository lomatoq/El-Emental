using Elemental.Simulation.Rendering;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthSurfaceWindPolicyTests
    {
        [Test] public void StoneWakeCurlsLocallyAndStaysTangentAndBounded()
        {
            var up=math.normalize(new float3(1,2,3));
            var velocity=EarthSurfaceWindPolicy.TangentVelocity(new float3(1,0,0),up,1.4f);
            var point=velocity*.5f;
            var curl=EarthSurfaceWindPolicy.StoneWakeVelocity(velocity,up,point,2.4f,1.4f,2f);
            Assert.That(math.distance(curl,velocity),Is.GreaterThan(.3f));
            Assert.That(math.abs(math.dot(curl,up)),Is.LessThan(.00001f));
            Assert.That(math.length(curl),Is.LessThanOrEqualTo(4));
            Assert.That(math.distance(EarthSurfaceWindPolicy.StoneWakeVelocity(velocity,up,point*10,2.4f,1.4f,2),velocity),Is.LessThan(.00001f));
        }
        [Test] public void WindRemainsTangentOnDifferentPlanetNormals()
        {
            foreach (var normal in new[] { new float3(0,1,0), new float3(1,0,0), math.normalize(new float3(1,1,1)) })
            {
                float3 velocity = EarthSurfaceWindPolicy.TangentVelocity(new float3(.9f,.08f,.4f),normal,1.45f);
                Assert.That(math.abs(math.dot(velocity,normal)),Is.LessThan(.00001f));
                Assert.That(math.length(velocity),Is.EqualTo(1.45f).Within(.00001f));
            }
        }
        [Test] public void ParallelWindHasStableTangentFallback()
        {
            float3 velocity = EarthSurfaceWindPolicy.TangentVelocity(new float3(0,1,0),new float3(0,1,0),1f);
            Assert.That(math.length(velocity),Is.EqualTo(1f).Within(.00001f));
            Assert.That(velocity.y,Is.Zero);
        }
        private static float3 Gust(float3 position, float time, float3 normal) =>
            EarthSurfaceWindPolicy.GustVelocity(new float3(1,0,0),normal,position,time,.65f,.18f,10f,24f,.075f);
        [Test] public void GustRemainsTangentAndBoundedAcrossCurvedGround()
        {
            foreach(var normal in new[] { new float3(0,1,0), new float3(1,0,0), math.normalize(new float3(1,1,1)) })
                for(int i=0;i<240;i++)
                {
                    float3 velocity=Gust(new float3(i*.1f,0,2),i*.1f,normal);
                    Assert.That(math.abs(math.dot(velocity,normal)),Is.LessThan(.00001f));
                    Assert.That(math.length(velocity),Is.InRange(.65f*.82f-.00001f,.65f*1.18f+.00001f));
                }
        }
        [Test] public void NearbyWispsShareContinuousMotion()
        {
            var up=new float3(0,1,0);var point=new float3(2,0,3);
            for(int i=0;i<100;i++)
            {
                var velocity=Gust(point,i*.1f,up);
                Assert.That(math.distance(velocity,Gust(point+new float3(.1f,0,0),i*.1f,up)),Is.LessThan(.005f));
                Assert.That(math.distance(velocity,Gust(point,i*.1f+1f/60f,up)),Is.LessThan(.005f));
            }
        }
        [Test] public void GustFrontTravelsDownwindInsteadOfFollowingParticleSlots()
        {
            var up=new float3(0,1,0);
            // One quarter wavelength travels in one quarter period.
            Assert.That(math.length(Gust(new float3(0),1f,up)),
                Is.EqualTo(math.length(Gust(new float3(6,0,0),3.5f,up))).Within(.00001f));
        }
        [Test] public void ZeroStrengthRestoresSteadyBaseline()
        {
            var wind=new float3(.9f,.08f,.4f);var up=new float3(0,1,0);
            var value=EarthSurfaceWindPolicy.GustVelocity(wind,up,new float3(9,0,4),7,.65f,0,10,24,0);
            Assert.That(math.distance(value,EarthSurfaceWindPolicy.TangentVelocity(wind,up,.65f)),Is.LessThan(.00001f));
        }
    }
}
