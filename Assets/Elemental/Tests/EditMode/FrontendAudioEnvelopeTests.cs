using System;
using Elemental.Presentation.UI;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class FrontendAudioEnvelopeTests
    {
        [Test]
        public void LoopOverlapKeepsEqualPowerThroughTheEntireSeam()
        {
            const double duration = 12, overlap = 2;
            for (int i = 0; i <= 100; i++)
            {
                double t = overlap * i / 100;
                float outgoing = FrontendAudioEnvelope.LoopGain(duration - overlap + t, duration, overlap);
                float incoming = FrontendAudioEnvelope.LoopGain(t, duration, overlap);
                Assert.That(outgoing * outgoing + incoming * incoming, Is.EqualTo(1f).Within(.00001f));
            }
            Assert.That(FrontendAudioEnvelope.LoopGain(-.01, duration, overlap), Is.Zero);
            Assert.That(FrontendAudioEnvelope.LoopGain(duration, duration, overlap), Is.Zero);
        }

        [Test]
        public void SmoothFadeHasQuietEndpointsAndNoOvershoot()
        {
            Assert.That(FrontendAudioEnvelope.Smooth(1, 0, 0, 1), Is.EqualTo(1));
            Assert.That(FrontendAudioEnvelope.Smooth(1, 0, 1, 1), Is.Zero);
            Assert.That(FrontendAudioEnvelope.Smooth(1, 0, 2, 1), Is.Zero);
            float firstStep = 1f - FrontendAudioEnvelope.Smooth(1, 0, .01, 1);
            float middleStep = FrontendAudioEnvelope.Smooth(1, 0, .5, 1) - FrontendAudioEnvelope.Smooth(1, 0, .51, 1);
            float lastStep = FrontendAudioEnvelope.Smooth(1, 0, .99, 1);
            Assert.That(firstStep, Is.LessThan(middleStep * .1f));
            Assert.That(lastStep, Is.LessThan(middleStep * .1f));
        }

        [Test]
        public void PanelOneShotAttacksAndReleasesToExactSilence()
        {
            Assert.That(FrontendAudioEnvelope.OneShotGain(0, 1, .05, .2), Is.Zero);
            Assert.That(FrontendAudioEnvelope.OneShotGain(.3, 1, .05, .2), Is.EqualTo(1));
            Assert.That(FrontendAudioEnvelope.OneShotGain(.99, 1, .05, .2), Is.LessThan(.01f));
            Assert.That(FrontendAudioEnvelope.OneShotGain(1, 1, .05, .2), Is.Zero);
            Assert.That(FrontendAudioEnvelope.OneShotGain(2, 1, .05, .2), Is.Zero);
        }

        [TestCase(.4, 3, .1)]
        [TestCase(12, 2, 2)]
        [TestCase(12, -1, .01)]
        public void OverlapIsBoundedForShortClips(double duration, double requested, double expected)
        {
            Assert.That(FrontendAudioEnvelope.Overlap(duration, requested), Is.EqualTo(expected).Within(.000001));
        }
    }
}
