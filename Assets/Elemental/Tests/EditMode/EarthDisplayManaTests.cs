using Elemental.Simulation.Combat;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthDisplayManaTests
    {
        [Test] public void ArbitrarilyExpensiveMagicCannotEmptyDisplay()
        {
            var mana = new EarthDisplayMana(); mana.Reset();
            for (int i = 0; i < 100; i++) mana.Spend(20f);
            Assert.That(mana.Value, Is.EqualTo(15f));
            mana.Step(4f);
            Assert.That(mana.Value, Is.EqualTo(100f));
        }
        [Test] public void RecoveryIsIndependentOfFrameSubdivision()
        {
            var a = new EarthDisplayMana(); a.Reset(); a.Spend(40f);
            var b = a; a.Step(1f);
            for (int i = 0; i < 120; i++) b.Step(1f / 120f);
            Assert.That(a.Value, Is.EqualTo(b.Value).Within(.001f));
            Assert.That(a.Value, Is.EqualTo(76.25f).Within(.001f));
        }
    }
}
