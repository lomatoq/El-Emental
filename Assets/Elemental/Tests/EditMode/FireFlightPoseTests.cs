using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
    public sealed class FireFlightPoseTests
    {
        [TestCase(60)] [TestCase(120)]
        public void FlightLeanFollowsTravelAndStaysInsideEighteenDegrees(int fps)
        {
            float2 direction=math.normalize(new float2(1,-1)),lean=0;
            for(int i=0;i<fps;i++){lean=FireFlightPose.StepLean(lean,direction*8,true,1f/fps);Assert.That(math.length(lean),Is.LessThanOrEqualTo(18.0001f));}
            Assert.That(math.length(lean),Is.InRange(17.9f,18.0001f));
            Assert.That(math.dot(math.normalize(lean),direction),Is.GreaterThan(.9999f));
            Assert.That(math.distance(lean,FireFlightPose.StepLean(lean,direction*8,true,0)),Is.Zero);
            for(int i=0;i<fps;i++)lean=FireFlightPose.StepLean(lean,direction*8,false,1f/fps);
            Assert.That(math.length(lean),Is.LessThan(.02f));
        }
    }
}
