using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthStoneSweepResponseTests
    {
        [TestCase(10f,10f)] [TestCase(10f,100f)] [TestCase(100f,10f)]
        public void DynamicPairConservesMomentumWithoutEnergyGrowth(float a,float b)
        {
            float3 va=new float3(22,3,0),vb=new float3(-8,-2,0);
            var result=EarthStoneSweepResponse.Resolve(va,vb,a,b,true,new float3(-1,0,0),.06f);
            Assert.That(result.Accepted,Is.True);
            Assert.That(math.distance(va*a+vb*b,result.SourceVelocity*a+result.TargetVelocity*b),Is.LessThan(.001f));
            float before=a*math.lengthsq(va)+b*math.lengthsq(vb);
            float after=a*math.lengthsq(result.SourceVelocity)+b*math.lengthsq(result.TargetVelocity);
            Assert.That(after,Is.LessThanOrEqualTo(before+.001f));
            Assert.That(result.SourceVelocity.y,Is.EqualTo(va.y));
            Assert.That(result.TargetVelocity.y,Is.EqualTo(vb.y));
        }
        [Test]
        public void SeparatingAndInvalidPairsDoNotGetAnotherKick()
        {
            Assert.That(EarthStoneSweepResponse.Resolve(new float3(-2,0,0),float3.zero,10,10,true,
                new float3(-1,0,0),.06f).Accepted,Is.False);
            Assert.That(EarthStoneSweepResponse.Resolve(new float3(2,0,0),float3.zero,10,float.NaN,true,
                new float3(-1,0,0),.06f).Accepted,Is.False);
        }
    }
}
