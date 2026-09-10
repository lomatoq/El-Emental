using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthChannelPresentationSessionTests
    {
        [Test] public void RepeatedBeginAndFocusUpdatesKeepOneGeneration()
        {
            var session=new EarthChannelPresentationSession();
            Assert.That(session.Begin(3,new float3(1,2,3)),Is.True);
            Assert.That(session.Begin(3,new float3(4,5,6)),Is.True);
            Assert.That(session.Generation,Is.EqualTo(3u));
            Assert.That(session.Focus,Is.EqualTo(new float3(4,5,6)));
            Assert.That(session.Update(2,float3.zero),Is.False);
            Assert.That(session.End(2),Is.False);
            Assert.That(session.Active,Is.True);
        }
        [Test] public void CancelledAndPriorLifeMessagesCannotRestartAChannel()
        {
            var session=new EarthChannelPresentationSession();
            session.Begin(7,float3.zero); session.Cancel();
            Assert.That(session.Begin(7,float3.zero),Is.False);
            Assert.That(session.Update(7,float3.zero),Is.False);
            Assert.That(session.Begin(6,float3.zero),Is.False);
            Assert.That(session.Begin(8,float3.zero),Is.True);
            Assert.That(session.End(7),Is.False);
            Assert.That(session.End(8),Is.True);
            Assert.That(session.End(8),Is.False);
        }
        [Test] public void InvalidFocusDoesNotDamageAnAcceptedSession()
        {
            var session=new EarthChannelPresentationSession();
            Assert.That(session.Begin(0,float3.zero),Is.False);
            session.Begin(1,new float3(1));
            Assert.That(session.Update(1,new float3(float.NaN)),Is.False);
            Assert.That(session.Begin(2,new float3(float.PositiveInfinity)),Is.False);
            Assert.That(session.Generation,Is.EqualTo(1u));
            Assert.That(session.Focus,Is.EqualTo(new float3(1)));
        }
        [Test] public void GenerationMayWrapWithoutAdmittingLatePackets()
        {
            var session=new EarthChannelPresentationSession();
            Assert.That(session.Begin(uint.MaxValue,float3.zero),Is.True);
            Assert.That(session.Begin(1,float3.zero),Is.True);
            Assert.That(session.Begin(uint.MaxValue,float3.zero),Is.False);
        }
    }
}
