using Elemental.Simulation.Rendering;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class MenuOcclusionFadeTests
    {
        [Test] public void VisibilityCannotPopAcrossThePortraitBoundary()
        {
            float value = MenuOcclusionFade.Step(1, 0, 1f / 60);
            Assert.That(value, Is.InRange(.9f, .95f));
            float restored = MenuOcclusionFade.Step(value, 1, 1f / 60);
            Assert.That(restored, Is.GreaterThan(value).And.LessThan(1));
            for (int i = 0; i < 30; i++) value = MenuOcclusionFade.Step(value, 0, 1f / 60);
            Assert.That(value, Is.Zero);
            Assert.That(MenuOcclusionFade.Step(value, 1, .12f), Is.EqualTo(.5f).Within(.0001f));
            Assert.That(MenuOcclusionFade.Step(value, 1, .24f), Is.EqualTo(1));
        }
        [Test] public void FrozenWorldUsesExplicitRenderClockWithoutOvershoot()
        {
            Assert.That(MenuOcclusionFade.Step(.3f, 0, 0), Is.EqualTo(.3f));
            Assert.That(MenuOcclusionFade.Step(.3f, 1, -1), Is.EqualTo(.3f));
            Assert.That(MenuOcclusionFade.Step(.3f, 1, 2), Is.EqualTo(1));
            Assert.That(MenuOcclusionFade.Step(.3f, 0, 2), Is.Zero);
        }
    }
}
