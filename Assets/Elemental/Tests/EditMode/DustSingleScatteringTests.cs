using Elemental.Simulation.Rendering;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class DustSingleScatteringTests
    {
        [Test] public void DayAndNightDiffuseValuesStayUnchangedBelowTheShoulder()
        {
            foreach(float value in new[]{0f,.02f,.09f,.3f,.6f,.75f})
            {
                Assert.That(DustSingleScattering.BoundedPeak(value), Is.EqualTo(value));
                Assert.That(DustSingleScattering.HuePreservingGain(value), Is.EqualTo(1));
            }
        }
        [Test] public void ExcessRadianceHasContinuousShoulderAndPreservesHue()
        {
            float previous = .75f;
            foreach(float value in new[]{.751f,.8f,1f,2f,4f,14f})
            {
                float bounded=DustSingleScattering.BoundedPeak(value), gain=DustSingleScattering.HuePreservingGain(value);
                Assert.That(bounded, Is.GreaterThanOrEqualTo(previous).And.LessThanOrEqualTo(1));
                Assert.That(bounded, Is.LessThan(value));
                Assert.That((value*.42f*gain)/(value*gain), Is.EqualTo(.42f).Within(.000001f));
                previous=bounded;
            }
            Assert.That(DustSingleScattering.BoundedPeak(.7501f), Is.EqualTo(.7501f).Within(.000001f));
        }
    }
}
