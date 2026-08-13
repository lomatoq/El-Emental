using Elemental.Input.Gestures;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class BendSessionTests
    {
        [Test]
        public void AmountAndChargeRemainIndependentAcrossLifecycle()
        {
            var session = new BendSessionState(BendTuning.Default);

            Assert.That(session.BeginAcquire(BendOriginMode.Aim), Is.True);
            Assert.That(session.SourceAcquired(), Is.True);
            Assert.That(session.SetAmount(0.72f), Is.True);
            Assert.That(session.Phase, Is.EqualTo(BendPhase.Holding));
            Assert.That(session.BeginCharge(), Is.True);
            session.Tick(0.45f);

            Assert.That(session.Amount01, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(session.Charge01, Is.GreaterThan(0.45f));
            Assert.That(session.Charge01, Is.LessThan(0.55f));
        }

        [Test]
        public void ReleasedChargeIsRememberedThenDecays()
        {
            BendTuning tuning = BendTuning.Default;
            var session = CreateHoldingSession(tuning);
            session.BeginCharge();
            session.Tick(0.9f);
            session.EndCharge();
            float full = session.Charge01;

            session.Tick(0.34f);
            Assert.That(session.Charge01, Is.EqualTo(full).Within(0.0001f));
            session.Tick(0.2f);
            Assert.That(session.Charge01, Is.LessThan(full));
        }

        [Test]
        public void ShiftChangesOriginModeInsteadOfCreatingAnotherAbility()
        {
            var session = new BendSessionState(BendTuning.Default);

            Assert.That(session.BeginAcquire(BendOriginMode.Self), Is.True);

            Assert.That(session.OriginMode, Is.EqualTo(BendOriginMode.Self));
            Assert.That(session.Phase, Is.EqualTo(BendPhase.Acquiring));
        }

        [Test]
        public void PdForceClampsSoHeavyMassLagsMore()
        {
            BendTuning tuning = new BendTuning(maximumControlForce: 1000f);
            BendForceResult light = BendForceSolver.SolvePdForce(
                float3.zero, float3.zero, new float3(10f, 0f, 0f), float3.zero, 10f, 0f, tuning);
            BendForceResult heavy = BendForceSolver.SolvePdForce(
                float3.zero, float3.zero, new float3(10f, 0f, 0f), float3.zero, 100f, 0f, tuning);

            float lightAcceleration = math.length(light.AppliedForce) / 10f;
            float heavyAcceleration = math.length(heavy.AppliedForce) / 100f;
            Assert.That(light.WasClamped, Is.True);
            Assert.That(heavy.WasClamped, Is.True);
            Assert.That(lightAcceleration, Is.GreaterThan(heavyAcceleration * 9f));
        }

        [Test]
        public void ReleasePreservesPhysicalAndGestureVelocity()
        {
            BendTuning tuning = new BendTuning(
                minimumReleaseSpeed: 0f,
                maximumReleaseSpeed: 30f,
                gestureVelocityTransfer: 0.5f);
            float3 released = BendForceSolver.SolveReleaseVelocity(
                new float3(2f, 0f, 0f),
                new float3(0f, 0f, 1f),
                new float3(4f, 2f, 0f),
                0.5f,
                tuning);

            Assert.That(released.x, Is.EqualTo(4f).Within(0.001f));
            Assert.That(released.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(released.z, Is.EqualTo(15f).Within(0.001f));
        }

        [Test]
        public void AmountChangesExtractedVolumeWithoutChangingCharge()
        {
            float smallRadius = EarthGeometryBuilder.ExtractionRadius(1.2f, 0.2f);
            float largeRadius = EarthGeometryBuilder.ExtractionRadius(1.2f, 0.9f);

            Assert.That(largeRadius, Is.GreaterThan(smallRadius * 1.45f));
            Assert.That(BendSessionState.ChargeFromSeconds(0.45f, BendTuning.Default),
                Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void HoldingStillFormsAContinuouslyLargerRock()
        {
            float tapAmount = MagicInputController.FormAmountFromSeconds(0.05f);
            float mediumAmount = MagicInputController.FormAmountFromSeconds(0.55f);
            float fullAmount = MagicInputController.FormAmountFromSeconds(1.15f);

            Assert.That(tapAmount, Is.InRange(0.18f, 0.2f));
            Assert.That(mediumAmount, Is.GreaterThan(tapAmount + 0.25f));
            Assert.That(fullAmount, Is.EqualTo(1f).Within(0.001f));
        }

        private static BendSessionState CreateHoldingSession(BendTuning tuning)
        {
            var session = new BendSessionState(tuning);
            session.BeginAcquire(BendOriginMode.Aim);
            session.SourceAcquired();
            session.SetAmount(0.5f);
            return session;
        }
    }
}
