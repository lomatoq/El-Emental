using Elemental.Presentation.Animation;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class SecondaryBoneSpringSolverTests
    {
        [Test]
        public void SpringMovesTowardTargetWithoutExceedingLimit()
        {
            var state = new SecondaryBoneSpringState();
            for (int index = 0; index < 120; index++)
                state = SecondaryBoneSpringSolver.Step(state, new Vector2(12f, -8f), 5.6f, 0.72f, 18f, 1f / 60f);

            Assert.That(state.AngleDegrees.x, Is.EqualTo(12f).Within(0.12f));
            Assert.That(state.AngleDegrees.y, Is.EqualTo(-8f).Within(0.12f));
            Assert.That(state.AngleDegrees.magnitude, Is.LessThanOrEqualTo(18.001f));
        }

        [Test]
        public void SpringRecoversToBindPose()
        {
            var state = new SecondaryBoneSpringState
            {
                AngleDegrees = new Vector2(16f, -5f),
                AngularVelocity = new Vector2(4f, 2f)
            };
            for (int index = 0; index < 180; index++)
                state = SecondaryBoneSpringSolver.Step(state, Vector2.zero, 5.6f, 0.72f, 22f, 1f / 60f);

            Assert.That(state.AngleDegrees.magnitude, Is.LessThan(0.05f));
            Assert.That(state.AngularVelocity.magnitude, Is.LessThan(0.1f));
        }

        [Test]
        public void InvalidFrameDeltaCannotExplodeTheChain()
        {
            var state = new SecondaryBoneSpringState();
            state = SecondaryBoneSpringSolver.Step(state, new Vector2(100f, 100f), 8f, 0.3f, 14f, 1f);

            Assert.That(float.IsFinite(state.AngleDegrees.x), Is.True);
            Assert.That(float.IsFinite(state.AngleDegrees.y), Is.True);
            Assert.That(state.AngleDegrees.magnitude, Is.LessThanOrEqualTo(14.001f));
        }

        [Test]
        public void NonFiniteStateAndInputsResetToABoundedFinitePose()
        {
            var state = new SecondaryBoneSpringState
            {
                AngleDegrees = new Vector2(float.NaN, float.PositiveInfinity),
                AngularVelocity = new Vector2(float.NegativeInfinity, float.NaN)
            };

            state = SecondaryBoneSpringSolver.Step(
                state,
                new Vector2(float.NaN, float.PositiveInfinity),
                float.NaN,
                float.NaN,
                float.NaN,
                float.NaN);

            Assert.That(float.IsFinite(state.AngleDegrees.x), Is.True);
            Assert.That(float.IsFinite(state.AngleDegrees.y), Is.True);
            Assert.That(state.AngleDegrees, Is.EqualTo(Vector2.zero));
            Assert.That(state.AngularVelocity, Is.EqualTo(Vector2.zero));
        }
    }
}
