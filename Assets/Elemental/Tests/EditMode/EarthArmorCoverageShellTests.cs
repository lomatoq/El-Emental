using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthArmorCoverageShellTests
    {
        [Test]
        public void JunctionFillersCoverCollarShouldersAndBothTorsoSeams()
        {
            int collar = 0, leftShoulder = 0, rightShoulder = 0, upper = 0, lower = 0;
            for (int index = 0; index < EarthArmorCoverageShell.FillerCount; index++)
            {
                EarthArmorCoverageFiller filler = EarthArmorCoverageShell.Filler(index);
                Assert.That(math.all(math.isfinite(filler.Direction)), Is.True);
                Assert.That(math.abs(math.length(filler.Direction) - 1f), Is.LessThan(1e-5f));
                Assert.That(math.cmin(filler.Scale), Is.GreaterThan(.035f));
                Assert.That(math.cmax(filler.Scale), Is.LessThan(.22f));
                switch (filler.Zone)
                {
                    case EarthArmorCoverageZone.NeckCollar: collar++; break;
                    case EarthArmorCoverageZone.LeftShoulder: leftShoulder++; break;
                    case EarthArmorCoverageZone.RightShoulder: rightShoulder++; break;
                    case EarthArmorCoverageZone.UpperTorsoSeam: upper++; break;
                    case EarthArmorCoverageZone.LowerTorsoSeam: lower++; break;
                }
            }

            Assert.That(collar, Is.EqualTo(8));
            Assert.That(leftShoulder, Is.EqualTo(4));
            Assert.That(rightShoulder, Is.EqualTo(4));
            Assert.That(upper, Is.EqualTo(6));
            Assert.That(lower, Is.EqualTo(6));
        }

        [Test]
        public void CollarHasNoUpwardFacePlateAndNoAngularHoleOverFortyFiveDegrees()
        {
            var angles = new float[8];
            int count = 0;
            for (int index = 0; index < EarthArmorCoverageShell.FillerCount; index++)
            {
                EarthArmorCoverageFiller filler = EarthArmorCoverageShell.Filler(index);
                if (filler.Zone != EarthArmorCoverageZone.NeckCollar) continue;
                Assert.That(filler.Direction.y, Is.LessThan(0f),
                    "Collar stones must bias below the jaw rather than into the face.");
                float angle = math.degrees(math.atan2(filler.Direction.x, filler.Direction.z));
                angles[count++] = angle < 0f ? angle + 360f : angle;
            }
            System.Array.Sort(angles);
            float largestGap = 0f;
            for (int index = 0; index < angles.Length; index++)
            {
                float next = index + 1 < angles.Length ? angles[index + 1] : angles[0] + 360f;
                largestGap = math.max(largestGap, next - angles[index]);
            }
            Assert.That(largestGap, Is.LessThanOrEqualTo(45.01f));
        }

        [Test]
        public void ShoulderCapsAreMirroredAndIncludeOneClavicleBridge()
        {
            for (int index = 0; index < 4; index++)
            {
                EarthArmorCoverageFiller left = EarthArmorCoverageShell.Filler(8 + index);
                EarthArmorCoverageFiller right = EarthArmorCoverageShell.Filler(12 + index);
                Assert.That(math.distance(
                    new float3(-left.Direction.x, left.Direction.y, left.Direction.z),
                    right.Direction), Is.LessThan(1e-5f));
                if (index < 3)
                {
                    Assert.That(left.Direction.x, Is.LessThan(-.7f));
                    Assert.That(right.Direction.x, Is.GreaterThan(.7f));
                }
                else
                {
                    Assert.That(left.Direction.x, Is.GreaterThan(.5f),
                        "The left clavicle stone must bridge inward toward UpperChest.");
                    Assert.That(right.Direction.x, Is.LessThan(-.5f));
                    Assert.That(left.Direction.y, Is.GreaterThan(.7f));
                }
            }
        }
    }
}
