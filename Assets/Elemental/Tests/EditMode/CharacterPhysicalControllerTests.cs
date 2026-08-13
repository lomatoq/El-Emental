using System;
using Elemental.Core.IDs;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class CharacterPhysicalControllerTests
    {
        [Test]
        public void RecoverySelection_IsRelativeToLocalGravityOnEveryPlanetSide()
        {
            float3[] ups =
            {
                new float3(0f, 1f, 0f),
                new float3(0f, -1f, 0f),
                new float3(1f, 0f, 0f),
                new float3(-1f, 0f, 0f),
                new float3(0f, 0f, 1f),
                new float3(0f, 0f, -1f)
            };

            foreach (float3 up in ups)
            {
                Assert.That(
                    CharacterPhysicalController.SelectRecovery(up, up, new float3(1f, 0f, 0f)),
                    Is.EqualTo(RecoveryCandidate.FaceUp));
                Assert.That(
                    CharacterPhysicalController.SelectRecovery(up, -up, new float3(1f, 0f, 0f)),
                    Is.EqualTo(RecoveryCandidate.FaceDown));
            }
        }

        [Test]
        public void BalanceTorque_IsFiniteAndBounded()
        {
            float3 torque = BalanceControllerMath.ComputeCorrectiveTorque(
                new float3(100f, 2f, -80f),
                float3.zero,
                new float3(0f, 1f, 0f),
                50f,
                220f);

            Assert.That(math.all(math.isfinite(torque)), Is.True);
            Assert.That(math.length(torque), Is.LessThanOrEqualTo(220.001f));
            Assert.That(math.abs(math.dot(torque, new float3(0f, 1f, 0f))), Is.LessThan(0.001f));
        }

        [Test]
        public void TwoHundredImpacts_StayFiniteAndBounded()
        {
            var controller = CreateController();
            CharacterPhysicalState state = default;
            for (int index = 0; index < 200; index++)
            {
                controller.ApplyImpact(5f + (index % 25), 10f);
                state = controller.Step(StableFrame(1f / 60f));
                Assert.That(float.IsFinite(state.StaggerDebt), Is.True);
                Assert.That(state.StaggerDebt, Is.InRange(0f, 20f));
                Assert.That(state.MuscleStrength, Is.InRange(0f, 1f));
            }

            Assert.That(state.Mode, Is.EqualTo(CharacterPhysicalMode.FullRagdoll));
        }

        [Test]
        public void HundredRecoveryCycles_ReturnControlWithoutStuckState()
        {
            var controller = CreateController();
            for (int cycle = 0; cycle < 100; cycle++)
            {
                controller.ApplyImpact(80f, 10f);
                Assert.That(controller.Mode, Is.EqualTo(CharacterPhysicalMode.FullRagdoll));

                CharacterPhysicalState state = default;
                for (int tick = 0; tick < 80; tick++)
                {
                    state = controller.Step(StableFrame(1f / 60f));
                }

                Assert.That(state.Mode, Is.EqualTo(CharacterPhysicalMode.AnimatedMotor));
                Assert.That(state.Recovery, Is.EqualTo(RecoveryCandidate.None));
                Assert.That(state.MuscleStrength, Is.EqualTo(1f).Within(0.001f));
            }
        }

        [Test]
        public void SteadyStateControllerStep_AllocatesZeroManagedBytes()
        {
            var controller = CreateController();
            CharacterPhysicalFrame frame = StableFrame(1f / 60f);
            for (int index = 0; index < 32; index++)
            {
                controller.Step(in frame);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1000; index++)
            {
                controller.Step(in frame);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(after - before, Is.EqualTo(0L));
        }

        private static CharacterPhysicalController CreateController()
        {
            return new CharacterPhysicalController(new ActorId(1u), CharacterPhysicalTuning.Default);
        }

        private static CharacterPhysicalFrame StableFrame(float deltaTime)
        {
            return new CharacterPhysicalFrame(
                deltaTime,
                new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 0f),
                2,
                float3.zero,
                float3.zero,
                new float3(0f, 1f, 0f),
                new float3(1f, 0f, 0f));
        }
    }
}
