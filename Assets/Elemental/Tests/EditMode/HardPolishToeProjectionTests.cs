using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
    public sealed class HardPolishToeProjectionTests
    {
        [TestCase(0f)][TestCase(.25f)][TestCase(.96865344f)][TestCase(1f)]
        public void ToeProbePreservesArticulationAndMatchesWeightedGoalDelta(float weight)
        {
            float3 toe=new float3(.02f,-.017f,.08f);
            quaternion authored=quaternion.EulerXYZ(.3f,.7f,-.2f);
            quaternion slope=quaternion.AxisAngle(new float3(1,0,0),-.12f);
            float3 projected=EarthPelvisCompensation.ProjectToeOffset(toe,authored,math.mul(slope,authored),weight);
            float3 expected=math.rotate(quaternion.AxisAngle(new float3(1,0,0),-.12f*weight),toe);
            Assert.That(math.distance(projected,expected),Is.LessThan(.000001f));
            Assert.That(math.length(projected),Is.EqualTo(math.length(toe)).Within(.000001f));
            if(weight==0)Assert.That(math.distance(projected,toe),Is.LessThan(.000001f));
        }
        [Test] public void ToeProjectionCommutesWithPlanetOrientation()
        {
            float3 toe=new float3(.02f,-.017f,.08f);quaternion authored=quaternion.EulerXYZ(.3f,.7f,-.2f);
            quaternion target=math.mul(quaternion.RotateX(-.12f),authored),planet=quaternion.EulerXYZ(1.1f,-.7f,.9f);
            float3 local=EarthPelvisCompensation.ProjectToeOffset(toe,authored,target,.96865344f);
            float3 world=EarthPelvisCompensation.ProjectToeOffset(math.rotate(planet,toe),math.mul(planet,authored),math.mul(planet,target),.96865344f);
            Assert.That(math.distance(world,math.rotate(planet,local)),Is.LessThan(.000001f));
        }
    }
}
