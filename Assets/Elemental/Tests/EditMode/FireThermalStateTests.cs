using Elemental.Simulation.Fire;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class FireThermalStateTests
    {
        [Test] public void SustainedFireCharsThenHeatCoolsBeforeSootDisappears()
        {
            var state=new FireThermalState();
            for(int i=0;i<100;i++){state.Ignite(1);state.Step(.05f);}
            Assert.That(state.Char,Is.GreaterThan(.95f));Assert.That(state.Heat,Is.GreaterThan(.9f));
            float previous=state.Char;
            for(int i=0;i<110;i++){state.Step(.05f);if(i>24){Assert.That(state.Char,Is.LessThanOrEqualTo(previous));Assert.That(previous-state.Char,Is.LessThan(.014f));}previous=state.Char;}
            Assert.That(state.Heat,Is.Zero);
            Assert.That(state.Char,Is.Zero);
        }
        [Test] public void BriefHitLeavesSootThatFadesSmoothly()
        {
            var state=new FireThermalState();state.Ignite(1);state.Step(.2f);
            Assert.That(state.Char,Is.GreaterThan(.1f));
            for(int i=0;i<120;i++)state.Step(.05f);
            Assert.That(state.Char,Is.Zero);Assert.That(state.Heat,Is.Zero);
        }
        [Test] public void InvalidHeatDoesNotPoisonState()
        {var state=new FireThermalState();state.Ignite(float.NaN);state.Step(float.PositiveInfinity);Assert.That(state.Heat,Is.Zero);Assert.That(state.Char,Is.Zero);}
    }
}
