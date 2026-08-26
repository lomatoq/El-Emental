using Elemental.Simulation.Bending;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthPlatformPreparationBudgetTests
    {
        [Test]
        public void OrdinarySliceCooksOnlyOneCell()
        {
            EarthPlatformPreparationSlice slice = EarthPlatformPreparationBudget.Next(7, 36);
            Assert.That(slice.StartIndex, Is.EqualTo(7));
            Assert.That(slice.Count, Is.EqualTo(1));
            Assert.That(slice.Complete, Is.False);
        }

        [Test]
        public void FinalSliceReportsCompletionWithoutOvershoot()
        {
            EarthPlatformPreparationSlice slice = EarthPlatformPreparationBudget.Next(35, 36);
            Assert.That(slice.Count, Is.EqualTo(1));
            Assert.That(slice.Complete, Is.True);
            EarthPlatformPreparationSlice empty = EarthPlatformPreparationBudget.Next(36, 36);
            Assert.That(empty.Count, Is.EqualTo(0));
            Assert.That(empty.Complete, Is.True);
        }
    }
}
