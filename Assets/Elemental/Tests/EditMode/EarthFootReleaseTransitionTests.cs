using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthFootReleaseTransitionTests
    {
        [Test] public void ReleaseBeginsAtRenderedEndpointAndFollowsNewAuthoredMotion()
        {
            var state=new EarthFootReleaseTransition();
            float3 previous=new float3(.2f,-1.1f,.3f),authored=new float3(-.25f,-1f,.15f);
            state.Begin(previous,authored);
            Assert.That(math.distance(state.Resolve(authored),previous),Is.LessThan(1e-6f));
            float3 step=new float3(.03f,.06f,-.01f);
            Assert.That(math.distance(state.Resolve(authored+step),previous+step),Is.LessThan(1e-6f),
                "A release offset must follow authored motion, not pin its old world endpoint.");
        }
        [Test] public void RotationReleaseBeginsAtRenderedOrientationInEitherQuaternionHemisphere()
        {
            quaternion authored=quaternion.EulerXYZ(.3f,-.7f,.2f),previous=quaternion.EulerXYZ(-.2f,.6f,.8f);
            foreach(float sign in new[]{1f,-1f})
            {
                var state=new EarthFootReleaseTransition();
                state.Begin(float3.zero,float3.zero,new quaternion(previous.value*sign),authored);
                Assert.That(state.Active,Is.True,"Pure angular mismatch must activate without positional error.");
                Assert.That(math.abs(math.dot(state.ResolveRotation(authored).value,previous.value)),Is.GreaterThan(.99999f));
                quaternion root=quaternion.EulerXYZ(.1f,1.4f,-.2f);
                Assert.That(math.abs(math.dot(math.mul(root,state.ResolveRotation(authored)).value,math.mul(root,previous).value)),Is.GreaterThan(.99999f));
            }
        }
        [TestCase(60)] [TestCase(120)]
        public void RotationOffsetTakesShortestArcAndExpiresAtAuthoredPose(int fps)
        {
            quaternion authored=quaternion.RotateY(math.radians(170f)),previous=quaternion.RotateY(math.radians(-170f));
            var state=new EarthFootReleaseTransition();state.Begin(float3.zero,float3.zero,previous,authored);
            float previousError=20.01f;
            for(int i=0;i<fps;i++)
            {
                state.Advance(1f/fps);quaternion actual=state.ResolveRotation(authored);
                Assert.That(math.all(math.isfinite(actual.value)),Is.True);
                Assert.That(math.length(actual.value),Is.EqualTo(1f).Within(.0001f));
                float error=2f*math.degrees(math.acos(math.clamp(math.abs(math.dot(actual.value,authored.value)),0f,1f)));
                Assert.That(error,Is.LessThanOrEqualTo(previousError+.001f));previousError=error;
            }
            Assert.That(state.Active,Is.False);
            Assert.That(math.abs(math.dot(state.ResolveRotation(authored).value,authored.value)),Is.GreaterThan(.99999f));
        }
        [TestCase(60)] [TestCase(120)]
        public void FiniteTransitionConvergesWithoutOvershootAndStopsOwningSwing(int fps)
        {
            var state=new EarthFootReleaseTransition();float3 authored=new float3(.3f,-1f,.2f);
            state.Begin(authored+new float3(.45f,0f,0f),authored);
            float last=.45f;
            for(int frame=0;frame<fps;frame++)
            {
                state.Advance(1f/fps);float residual=math.distance(state.Resolve(authored),authored);
                Assert.That(residual,Is.InRange(0f,last+1e-6f));last=residual;
                if((frame+1f)/fps>=EarthFootReleaseTransition.Duration+.0001f)
                    Assert.That(state.Active,Is.False,"Release cannot accumulate a stale target backlog.");
            }
            Assert.That(math.distance(state.Resolve(authored),authored),Is.Zero);
        }
    }
}
