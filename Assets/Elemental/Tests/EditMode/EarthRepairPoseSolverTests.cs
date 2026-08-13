using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthRepairPoseSolverTests
    {
        [Test]
        public void MassAwarePdIsBoundedAndHeavyPieceAcceleratesLess()
        {
            EarthReassemblyTuning tuning = Tuning();
            EarthRepairPoseInput light = Input(1f);
            EarthRepairPoseInput heavy = Input(20f);

            EarthRepairPoseControlSample lightSample = EarthRepairPoseSolver.Solve(in light, in tuning, false);
            EarthRepairPoseControlSample heavySample = EarthRepairPoseSolver.Solve(in heavy, in tuning, false);

            Assert.That(lightSample.IsFinite, Is.True);
            Assert.That(heavySample.IsFinite, Is.True);
            Assert.That(math.length(lightSample.Acceleration), Is.LessThanOrEqualTo(tuning.MaximumAcceleration + 0.001f));
            Assert.That(math.length(heavySample.Acceleration), Is.LessThan(math.length(lightSample.Acceleration)));
            Assert.That(math.length(heavySample.Acceleration), Is.LessThanOrEqualTo(tuning.MaximumForce / 20f + 0.001f));
        }

        [Test]
        public void SettleGateRequiresContinuousPoseAndVelocityAgreement()
        {
            EarthReassemblyTuning tuning = Tuning();
            var state = new EarthRepairSettleState();
            var good = new EarthRepairPoseControlSample(
                float3.zero, float3.zero, 0.01f, math.radians(1f), false, false, true);

            Assert.That(EarthRepairPoseSolver.UpdateSettle(
                in good, 0.05f, 0.04f, 0.06f, in tuning, ref state), Is.False);
            Assert.That(EarthRepairPoseSolver.UpdateSettle(
                in good, 0.05f, 0.04f, 0.06f, in tuning, ref state), Is.True);
            Assert.That(state.StableSeconds, Is.GreaterThanOrEqualTo(tuning.SettleDuration));

            var bad = new EarthRepairPoseControlSample(
                float3.zero, float3.zero, 0.04f, 0f, false, false, true);
            Assert.That(EarthRepairPoseSolver.UpdateSettle(
                in bad, 0f, 0f, 0.1f, in tuning, ref state), Is.False);
            Assert.That(state.StableSeconds, Is.Zero);
        }

        [Test]
        public void JamDetectionRequestsBoundedDeterministicRetry()
        {
            EarthReassemblyTuning tuning = Tuning();
            var state = new EarthRepairProgressState { BestError = float.MaxValue };
            EarthRepairProgressSample sample = default;
            for (int index = 0; index < 8; index++)
            {
                sample = EarthRepairPoseSolver.UpdateProgress(2f, 0.1f, in tuning, ref state);
                if (sample.RetryRequested) break;
            }

            Assert.That(sample.RetryRequested, Is.True);
            Assert.That(state.RetryCount, Is.EqualTo(1));
            Assert.That(state.RetryDelayRemaining, Is.EqualTo(tuning.RetryDelay).Within(0.0001f));
        }

        [Test]
        public void OneHundredSolveCyclesStayFiniteDriftFreeAndAllocationFree()
        {
            EarthReassemblyTuning tuning = Tuning();
            EarthRepairPoseInput input = Input(4f);
            for (int warmup = 0; warmup < 32; warmup++)
                EarthRepairPoseSolver.Solve(in input, in tuning, false);

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            float3 expectedTarget = input.TargetPosition;
            bool allFinite = true;
            float maximumFinalError = 0f;
            bool targetDrifted = false;
            for (int cycle = 0; cycle < 100; cycle++)
            {
                float3 position = new float3(4f, -2f, cycle * 0.001f);
                float3 velocity = float3.zero;
                quaternion rotation = quaternion.AxisAngle(math.normalize(new float3(1f, 2f, 3f)), 0.8f);
                float3 angularVelocity = float3.zero;
                for (int step = 0; step < 480; step++)
                {
                    var stepInput = new EarthRepairPoseInput(
                        position, rotation, velocity, angularVelocity,
                        expectedTarget, quaternion.identity, float3.zero, float3.zero, 4f);
                    EarthRepairPoseControlSample sample = EarthRepairPoseSolver.Solve(
                        in stepInput, in tuning, false);
                    allFinite &= sample.IsFinite;
                    const float dt = 1f / 120f;
                    velocity += sample.Acceleration * dt;
                    position += velocity * dt;
                    angularVelocity += sample.AngularAcceleration * dt;
                    float angularSpeed = math.length(angularVelocity);
                    if (angularSpeed > 0.000001f)
                    {
                        quaternion delta = quaternion.AxisAngle(angularVelocity / angularSpeed, angularSpeed * dt);
                        rotation = math.normalize(math.mul(delta, rotation));
                    }
                }
                maximumFinalError = math.max(maximumFinalError, math.distance(position, expectedTarget));
                allFinite &= math.all(math.isfinite(position));
                targetDrifted |= !input.TargetPosition.Equals(expectedTarget);
            }
            long after = System.GC.GetAllocatedBytesForCurrentThread();
            Assert.That(after - before, Is.Zero, "The hot solver must not allocate after warmup.");
            Assert.That(allFinite, Is.True);
            Assert.That(maximumFinalError, Is.LessThan(0.03f));
            Assert.That(targetDrifted, Is.False, "The immutable rest target must never drift.");
        }

        private static EarthRepairPoseInput Input(float mass)
        {
            return new EarthRepairPoseInput(
                new float3(3f, 2f, -1f),
                quaternion.AxisAngle(new float3(0f, 1f, 0f), 0.7f),
                new float3(1f, -0.5f, 0.25f),
                new float3(0.4f, -0.2f, 0.1f),
                float3.zero,
                quaternion.identity,
                float3.zero,
                float3.zero,
                mass);
        }

        private static EarthReassemblyTuning Tuning()
        {
            return new EarthReassemblyTuning
            {
                CaptureSettleTime = 0.42f,
                AlignmentSettleTime = 0.28f,
                DampingRatio = 1f,
                MaximumAcceleration = 55f,
                MaximumForce = 180f,
                MaximumAngularAcceleration = 90f,
                RotationStiffness = 42f,
                RotationDamping = 13f,
                PositionTolerance = 0.025f,
                AngleToleranceRadians = math.radians(2.5f),
                MaximumRelativeSpeed = 0.12f,
                MaximumRelativeAngularSpeed = 0.12f,
                SettleDuration = 0.12f,
                JamDuration = 0.6f,
                JamProgressEpsilon = 0.003f,
                RetryDelay = 0.18f
            };
        }
    }
}
