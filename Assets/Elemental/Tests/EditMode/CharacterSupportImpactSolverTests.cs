using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class CharacterSupportImpactSolverTests
    {
        [Test]
        public void StaticContactBelowFeetIsSupportNotDamage()
        {
            Assert.That(CharacterSupportImpactSolver.IsSupportContact(
                new float3(0f, 1f, 0f),
                new float3(0f, 1.2f, 0f),
                new float3(0.15f, 0f, 0.1f),
                new float3(0f, -1f, 0f),
                false), Is.True);
        }

        [Test]
        public void SideHitAndDynamicBodyRemainRealImpacts()
        {
            Assert.That(CharacterSupportImpactSolver.IsSupportContact(
                new float3(0f, 1f, 0f),
                new float3(0f, 1.2f, 0f),
                new float3(0.5f, 1.1f, 0f),
                new float3(-1f, 0f, 0f),
                false), Is.False);
            Assert.That(CharacterSupportImpactSolver.IsSupportContact(
                new float3(0f, 1f, 0f),
                new float3(0f, 1.2f, 0f),
                new float3(0f, 0f, 0f),
                new float3(0f, 1f, 0f),
                true), Is.False);
        }

        [Test]
        public void ClassificationIsLocalUpInvariant()
        {
            Assert.That(CharacterSupportImpactSolver.IsSupportContact(
                new float3(1f, 0f, 0f),
                new float3(25.2f, 0f, 0f),
                new float3(24f, 0.1f, 0f),
                new float3(1f, 0f, 0f),
                false), Is.True);
        }
    }
}
