using Elemental.Simulation.Rendering;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthChargeVignetteTests
    {
        [Test] public void ChargeDarkensEdgesAndReleaseReturnsToBaseline()
        {
            Assert.That(EarthChargeVignette.Solve(.1f, 0f, .43f, .1f, .85f, 0f, false), Is.EqualTo(.1f));
            Assert.That(EarthChargeVignette.Solve(.1f, 1f, .43f, .1f, .85f, 0f, false), Is.InRange(.39f, .43f));
        }
        [Test] public void ReducedMotionKeepsDarknessWithoutPulsation()
        {
            float first = EarthChargeVignette.Solve(.1f, .75f, .43f, .1f, .85f, 0f, true);
            Assert.That(EarthChargeVignette.Solve(.1f, .75f, .43f, .1f, .85f, 5f, true), Is.EqualTo(first));
        }
        [Test] public void PulseIsBoundedAndNonfiniteInputsCannotContaminateVolume()
        {
            for (int i = 0; i < 200; i++)
                Assert.That(EarthChargeVignette.Solve(.1f, 1f, .43f, .1f, .85f, i * .05f, false), Is.InRange(.396f, .43001f));
            Assert.That(float.IsFinite(EarthChargeVignette.Solve(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, false)), Is.True);
        }
    }
}
