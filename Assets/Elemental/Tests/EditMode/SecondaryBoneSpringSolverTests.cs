using Elemental.Presentation.Animation;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class SecondaryBoneSpringSolverTests
    {
        [Test]
        public void CapsuleProjectionPreservesExteriorAndClearsInterior()
        {
            Vector3 a = Vector3.zero;
            Vector3 b = Vector3.up;
            Vector3 outside = new Vector3(0f, .4f, .5f);
            Assert.That(SecondaryBoneCollisionSolver.OutsideCapsule(outside, a, b, .2f, Vector3.forward), Is.EqualTo(outside));
            Vector3 inside = new Vector3(0f, .4f, .05f);
            Vector3 projected = SecondaryBoneCollisionSolver.OutsideCapsule(inside, a, b, .2f, Vector3.forward);
            Assert.That(projected.y, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(projected.z, Is.EqualTo(.2f).Within(.0001f));
        }

        [Test]
        public void DegenerateHelmetCapsuleUsesStableOutwardFallback()
        {
            Vector3 center = new Vector3(1f, 2f, 3f);
            Vector3 projected = SecondaryBoneCollisionSolver.OutsideCapsule(center, center, center, .25f, Vector3.back);
            Assert.That(projected, Is.EqualTo(center + Vector3.back * .25f));
        }

        [Test]
        public void SkirtPlaneClearsPenetrationWithoutChangingHeightOrSidewaysMotion()
        {
            Vector3 point = new Vector3(.13f, -.2f, -.1f);
            Vector3 projected = SecondaryBoneCollisionSolver.OutsidePlane(point, Vector3.forward * .2f, Vector3.forward);
            Assert.That(projected.x, Is.EqualTo(point.x));
            Assert.That(projected.y, Is.EqualTo(point.y));
            Assert.That(projected.z, Is.EqualTo(.2f).Within(.0001f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void WalkingOscillationProducesBoundedVisibleSecondaryMotion(int fps)
        {
            var state = new SecondaryBoneSpringState();
            float peak = 0f;
            for (int i = 0; i < fps * 3; i++)
            {
                Vector2 target = new Vector2(Mathf.Sin(i * 2f * Mathf.PI * 2f / fps) * 10f, 0f);
                state = SecondaryBoneSpringSolver.Step(state, target, 5.6f, .72f, 18f, 1f / fps);
                peak = Mathf.Max(peak, state.AngleDegrees.magnitude);
            }
            Assert.That(peak, Is.InRange(6f, 18.001f));
        }

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
