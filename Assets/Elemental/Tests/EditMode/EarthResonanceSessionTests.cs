using Elemental.Simulation.Bending;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthResonanceSessionTests
    {
        [Test]
        public void Charge_ActivatesOnlyAfterThreshold_AndUsesNonlinearRange()
        {
            EarthResonanceProfileData data = EarthResonanceProfileData.Default;
            var session = new EarthResonanceSession(in data);
            Assert.That(session.Begin(10f), Is.True);
            Assert.That(session.Sample(10.54f).Activated, Is.False);
            EarthResonanceChargeSample middle = session.Sample(11.575f);
            Assert.That(middle.Activated, Is.True);
            Assert.That(middle.StoneCount, Is.InRange(8, 28));
            Assert.That(middle.Radius, Is.InRange(1.2f, 6.5f));
            Assert.That(middle.Lifetime, Is.InRange(1.5f, 6f));
        }

        [Test]
        public void ReleasedVolley_ConsumesAndExpiresDeterministically()
        {
            EarthResonanceProfileData data = EarthResonanceProfileData.Default;
            var session = new EarthResonanceSession(in data);
            session.Begin(0f);
            EarthResonanceChargeSample released = session.Release(2.6f);
            Assert.That(released.StoneCount, Is.EqualTo(28));
            Assert.That(session.IsVolleyActive, Is.True);
            for (int index = 0; index < released.StoneCount; index++)
                Assert.That(session.ConsumeStone(), Is.True);
            Assert.That(session.IsVolleyActive, Is.False);

            session.Begin(5f);
            released = session.Release(5.55f);
            Assert.That(session.Expire(5.55f + released.Lifetime - 0.01f), Is.False);
            Assert.That(session.Expire(5.55f + released.Lifetime), Is.True);
        }
    }
}
