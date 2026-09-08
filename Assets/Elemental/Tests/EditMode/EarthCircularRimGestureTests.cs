using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthCircularRimGestureTests
    {
        [TestCase(-1f,EarthCircularGestureDirection.Clockwise)]
        [TestCase(1f,EarthCircularGestureDirection.CounterClockwise)]
        public void OneCircleStartingAtItsRimCompletesTheRequestedIntent(float sign,EarthCircularGestureDirection direction)
        {
            float2 center=new(.5f,.5f);float radius=.10f;
            var state=EarthCircularGestureSolver.Begin(center+new float2(radius,0));
            EarthCircularGestureSample sample=default;
            for(int i=1;i<=48;i++)
            {float angle=sign*i*math.PI*2/48;sample=EarthCircularGestureSolver.Step(ref state,center+new float2(math.cos(angle),math.sin(angle))*radius);}
            Assert.That(sample.Direction,Is.EqualTo(direction));
            Assert.That(sample.Phase01,Is.EqualTo(1f));
        }
        [Test] public void StraightPullAndSubpixelNoiseDoNotBecomeRepair()
        {
            var state=EarthCircularGestureSolver.Begin(new float2(.5f));
            EarthCircularGestureSample sample=default;
            for(int i=0;i<100;i++)sample=EarthCircularGestureSolver.Step(ref state,new float2(.5f+i*.002f,.5f+(i%2)*.00001f));
            Assert.That(sample.Recognized,Is.False);
        }
        [TestCase(.008f)]
        [TestCase(.027f)]
        public void RepeatingTinyCirclesInsideSpatialDeadzoneNeverArmsRepair(float radius)
        {
            float2 center=new(.5f,.5f);
            var state=EarthCircularGestureSolver.Begin(center+new float2(radius,0));
            EarthCircularGestureSample sample=default;
            for(int i=1;i<=48*30;i++)
            {
                float angle=-i*math.PI*2/48;
                sample=EarthCircularGestureSolver.Step(ref state,center+new float2(math.cos(angle),math.sin(angle))*radius);
                Assert.That(sample.Recognized,Is.False);
                Assert.That(sample.Phase01,Is.Zero);
            }
            Assert.That(state.AccumulatedDegrees,Is.Zero);
        }
    }
}
