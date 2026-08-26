using Elemental.Simulation.Bending;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthArmorWorkBudgetTests
    {
        [Test]
        public void NinetySixPiecesArePreparedInEightBoundedSlices()
        {
            int completed = 0;
            int slices = 0;
            while (completed < 96)
            {
                EarthArmorWorkSlice slice = EarthArmorWorkBudget.Next(completed, 96, 12);
                Assert.That(slice.Start, Is.EqualTo(completed));
                Assert.That(slice.Count, Is.InRange(1, 12));
                completed += slice.Count;
                slices++;
            }

            Assert.That(completed, Is.EqualTo(96));
            Assert.That(slices, Is.EqualTo(8));
        }

        [Test]
        public void FinalSliceNeverRunsBeyondRequestedPieceCount()
        {
            EarthArmorWorkSlice slice = EarthArmorWorkBudget.Next(60, 64, 12);

            Assert.That(slice.Start, Is.EqualTo(60));
            Assert.That(slice.Count, Is.EqualTo(4));
            Assert.That(slice.IsComplete, Is.True);
        }
    }
}
