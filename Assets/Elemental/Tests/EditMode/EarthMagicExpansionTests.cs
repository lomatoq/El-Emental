using System.Collections.Generic;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMagicExpansionTests
    {
        [Test]
        public void VectorFieldContinuouslyAcceleratesAndRespectsMassAndSpeedCap()
        {
            EarthVectorFieldSample light = EarthVectorFieldSolver.Solve(
                float3.zero, 50f, new float3(0f, 0f, 1f), 1f, 4200f, 32f, 0.02f);
            EarthVectorFieldSample heavy = EarthVectorFieldSolver.Solve(
                float3.zero, 500f, new float3(0f, 0f, 1f), 1f, 4200f, 32f, 0.02f);
            EarthVectorFieldSample capped = EarthVectorFieldSolver.Solve(
                new float3(0f, 0f, 31.95f), 50f, new float3(0f, 0f, 1f), 1f, 4200f, 32f, 0.02f);

            Assert.That(light.VelocityChange.z, Is.GreaterThan(heavy.VelocityChange.z * 9f));
            Assert.That(capped.ResultingForwardSpeed, Is.EqualTo(32f).Within(0.0001f));
            Assert.That(capped.SpeedLimited, Is.True);
            Assert.That(EarthVectorFieldSolver.FinalImpulse(1f, 260f, 2400f), Is.EqualTo(2400f).Within(0.01f));
        }

        [Test]
        public void VectorReleaseSeparatesControlledHoldQuickPulseAndProjectileFlick()
        {
            EarthVectorGestureSample controlled = EarthVectorGestureSolver.Classify(
                0.8f, 0.01f, new float2(0.02f, 0.01f));
            EarthVectorGestureSample tap = EarthVectorGestureSolver.Classify(
                0.12f, 0.005f, float2.zero);
            EarthVectorGestureSample flick = EarthVectorGestureSolver.Classify(
                0.32f, 0.12f, new float2(2.4f, 0.35f));

            Assert.That(controlled.Intent, Is.EqualTo(EarthVectorReleaseIntent.Controlled));
            Assert.That(tap.Intent, Is.EqualTo(EarthVectorReleaseIntent.QuickPulse));
            Assert.That(flick.Intent, Is.EqualTo(EarthVectorReleaseIntent.ProjectileFlick));
            Assert.That(flick.Strength01, Is.GreaterThan(0.5f));
            Assert.That(math.length(flick.ScreenDirection), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CircularGestureMapsClockwiseToRepairAndCounterClockwiseToDisassembly()
        {
            EarthCircularGestureState clockwise = EarthCircularGestureSolver.Begin(float2.zero);
            EarthCircularGestureSample clockwiseSample = default;
            for (int index = 0; index <= 12; index++)
            {
                float radians = -index * math.PI * 2f / 12f;
                clockwiseSample = EarthCircularGestureSolver.Step(
                    ref clockwise, new float2(math.cos(radians), math.sin(radians)) * 100f);
            }

            EarthCircularGestureState counter = EarthCircularGestureSolver.Begin(float2.zero);
            EarthCircularGestureSample counterSample = default;
            for (int index = 0; index <= 7; index++)
            {
                float radians = index * math.PI / 7f;
                counterSample = EarthCircularGestureSolver.Step(
                    ref counter, new float2(math.cos(radians), math.sin(radians)) * 100f);
            }

            Assert.That(clockwiseSample.Direction, Is.EqualTo(EarthCircularGestureDirection.Clockwise));
            Assert.That(clockwiseSample.Phase01, Is.GreaterThan(0.95f));
            Assert.That(counterSample.Direction, Is.EqualTo(EarthCircularGestureDirection.CounterClockwise));
            Assert.That(counterSample.Phase01, Is.InRange(0.45f, 0.65f));
        }

        [Test]
        public void GravityWellPullsAndOrbitsWithBoundedSpeed()
        {
            EarthGravityWellSample sample = EarthGravityWellSolver.Solve(
                new float3(4f, 0f, 0f),
                new float3(0f, 0f, 0f),
                float3.zero,
                new float3(0f, 1f, 0f),
                8f, 0.8f, 38f, 6f, 1.8f, 16f, 0.02f, 1f);
            EarthGravityWellSample capped = EarthGravityWellSolver.Solve(
                new float3(4f, 0f, 0f),
                new float3(-15.95f, 0f, 0f),
                float3.zero,
                new float3(0f, 1f, 0f),
                8f, 0.8f, 80f, 6f, 0f, 16f, 0.1f, 1f);

            Assert.That(sample.Acceleration.x, Is.LessThan(0f));
            Assert.That(math.abs(sample.Acceleration.z), Is.GreaterThan(0.1f));
            Assert.That(sample.Weight, Is.InRange(0.45f, 0.55f));
            Assert.That(capped.SpeedLimited, Is.True);
            Assert.That(capped.PredictedSpeed, Is.EqualTo(16f).Within(0.001f));
        }

        [Test]
        public void EveryOpenStrokeBuildsWallWhileOnlyClosedAreaBuildsPlatform()
        {
            var line = new List<float2>
            {
                new float2(0.10f, 0.10f), new float2(0.40f, 0.11f), new float2(0.80f, 0.12f)
            };
            var arc = new List<float2>
            {
                new float2(0.05f, 0.05f), new float2(0.25f, 0.42f), new float2(0.50f, 0.58f),
                new float2(0.75f, 0.39f), new float2(0.95f, 0.05f)
            };
            var pi = new List<float2>
            {
                new float2(0.20f, 0.20f), new float2(0.20f, 0.65f), new float2(0.70f, 0.65f),
                new float2(0.70f, 0.20f)
            };
            var closed = new List<float2>
            {
                new float2(0.20f, 0.20f), new float2(0.20f, 0.65f), new float2(0.70f, 0.65f),
                new float2(0.70f, 0.20f), new float2(0.205f, 0.205f)
            };

            Assert.That(EarthStructureGestureSolver.Classify(line).Kind, Is.EqualTo(EarthStructureGestureKind.Wall));
            Assert.That(EarthStructureGestureSolver.Classify(arc).Kind, Is.EqualTo(EarthStructureGestureKind.Wall));
            Assert.That(EarthStructureGestureSolver.Classify(pi).Kind, Is.EqualTo(EarthStructureGestureKind.Wall));
            Assert.That(EarthStructureGestureSolver.Classify(closed).Kind, Is.EqualTo(EarthStructureGestureKind.Platform));
        }

        [Test]
        public void PlatformGeometryAutoClosesSelfCrossingStrokeIntoSimpleOuterHull()
        {
            var bowTie = new List<float3>
            {
                new float3(-2f, 24f, -2f), new float3(2f, 24f, 2f),
                new float3(-2f, 24f, 2f), new float3(2f, 24f, -2f)
            };
            EarthPlatformGeometry geometry = EarthPlatformGeometrySolver.Build(bowTie, float3.zero);

            Assert.That(geometry.IsValid, Is.True);
            Assert.That(geometry.Polygon.Length, Is.InRange(3, 32));
            Assert.That(geometry.Area, Is.EqualTo(16f).Within(0.1f));
            Assert.That(math.length(geometry.Center), Is.EqualTo(geometry.SurfaceRadius).Within(0.001f));
            Assert.That(EarthPlatformGeometrySolver.RequiredChordEmbedDepth(
                in geometry, 0.24f, 0.45f),
                Is.GreaterThan(0.55f));
        }

        [Test]
        public void DynamicDebrisShrinksWithoutOwningOrStoppingItsPhysics()
        {
            DynamicDebrisLifecycleSample falling = DynamicDebrisLifecycle.Evaluate(0.8f, 1.1f, 0.9f);
            DynamicDebrisLifecycleSample shrinking = DynamicDebrisLifecycle.Evaluate(1.55f, 1.1f, 0.9f);
            DynamicDebrisLifecycleSample gone = DynamicDebrisLifecycle.Evaluate(2.1f, 1.1f, 0.9f);

            Assert.That(falling.Shrinking, Is.False);
            Assert.That(falling.Scale01, Is.EqualTo(1f));
            Assert.That(shrinking.Shrinking, Is.True);
            Assert.That(shrinking.Scale01, Is.InRange(0.45f, 0.55f));
            Assert.That(shrinking.Complete, Is.False);
            Assert.That(gone.Complete, Is.True);
            Assert.That(gone.Scale01, Is.Zero);
        }

        [Test]
        public void LandingPredictionTargetsSphereAndCapsDownwardSpeed()
        {
            EarthLandingPrediction prediction = EarthLandingCushionSolver.Predict(
                new float3(0f, 32f, 0f),
                new float3(4f, -8f, 0f),
                float3.zero,
                24f,
                14f,
                4f);

            Assert.That(prediction.Valid, Is.True);
            Assert.That(math.length(prediction.SurfacePoint), Is.EqualTo(24f).Within(0.001f));
            Assert.That(prediction.ImpactSpeed, Is.GreaterThan(8f));
            Assert.That(EarthLandingCushionSolver.RequiredUpwardVelocityChange(-18f, 4f),
                Is.EqualTo(14f).Within(0.001f));
        }

        [Test]
        public void FullWaveFormsContiguousVoronoiCellsAndACompactSmoothCrest()
        {
            EarthPillarWaveSample[] samples = EarthPillarWaveSolver.Build(1f, 1f);
            int maximumRow = 0;
            for (int index = 0; index < samples.Length; index++)
            {
                EarthPillarWaveSample sample = samples[index];
                maximumRow = math.max(maximumRow, sample.Row);
                int countInRow = 0;
                for (int candidate = 0; candidate < samples.Length; candidate++)
                    if (samples[candidate].Row == sample.Row) countInRow++;
                float gap = (math.PI * 2f * sample.ArcDistance) / countInRow;
                Assert.That(sample.Width, Is.GreaterThan(gap * 1.01f),
                    "Angular neighbours must overlap slightly instead of leaving lanes.");
                Assert.That(sample.Width, Is.LessThan(gap * 1.3f));
                Assert.That(sample.Depth, Is.GreaterThan(0.5f));
            }

            var rowHeights = new float[maximumRow + 1];
            var rowCounts = new int[maximumRow + 1];
            for (int index = 0; index < samples.Length; index++)
            {
                rowHeights[samples[index].Row] += samples[index].Height;
                rowCounts[samples[index].Row]++;
            }
            float maximumAverage = 0f;
            int crestRow = 0;
            for (int row = 0; row < rowHeights.Length; row++)
            {
                rowHeights[row] /= math.max(1, rowCounts[row]);
                if (rowHeights[row] <= maximumAverage) continue;
                maximumAverage = rowHeights[row];
                crestRow = row;
            }
            Assert.That(crestRow, Is.InRange(2, maximumRow - 1));
            Assert.That(rowHeights[0], Is.LessThan(maximumAverage * 0.25f));
            Assert.That(rowHeights[maximumRow], Is.LessThan(maximumAverage * 0.30f));
        }

        [Test]
        public void WaveColumnMotionEasesUpSettlesAndSmoothlyReturnsIntoGround()
        {
            EarthPillarWaveMotionSample start = EarthPillarWaveSolver.EvaluateMotion(0f, 0.36f, 0.08f, 0.46f);
            EarthPillarWaveMotionSample peak = EarthPillarWaveSolver.EvaluateMotion(0.30f, 0.36f, 0.08f, 0.46f);
            EarthPillarWaveMotionSample settled = EarthPillarWaveSolver.EvaluateMotion(0.40f, 0.36f, 0.08f, 0.46f);
            EarthPillarWaveMotionSample retreat = EarthPillarWaveSolver.EvaluateMotion(0.67f, 0.36f, 0.08f, 0.46f);
            EarthPillarWaveMotionSample complete = EarthPillarWaveSolver.EvaluateMotion(1f, 0.36f, 0.08f, 0.46f);

            Assert.That(start.Height01, Is.InRange(0.02f, 0.03f));
            Assert.That(peak.Height01, Is.GreaterThan(1f));
            Assert.That(settled.Height01, Is.EqualTo(1f).Within(0.001f));
            Assert.That(retreat.Height01, Is.InRange(0.25f, 0.75f));
            Assert.That(retreat.Sink01, Is.GreaterThan(0f));
            Assert.That(complete.Complete, Is.True);
        }

        [Test]
        public void LegacyVisualWaveSampleMatchesThePhysicsMotionExactly()
        {
            EarthPillarWaveVisualTuning tuning = EarthPillarWaveVisualTuning.PremiumDefault;
            float[] times = { -0.04f, 0f, 0.12f, 0.30f, 0.37f, 0.58f, 0.90f };
            for (int index = 0; index < times.Length; index++)
            {
                EarthPillarWaveMotionSample physics = EarthPillarWaveSolver.EvaluateMotion(
                    times[index], 0.30f, 0.05f, 0.32f);
                EarthPillarWaveVisualSample visual = EarthPillarWaveSolver.EvaluateVisualMotion(
                    times[index],
                    0.30f,
                    0.05f,
                    0.32f,
                    WaveMotionMode.Legacy,
                    in tuning,
                    19u);

                Assert.That(visual.Height01, Is.EqualTo(physics.Height01).Within(0.000001f));
                Assert.That(visual.Width01, Is.EqualTo(physics.Width01).Within(0.000001f));
                Assert.That(visual.TiltDegrees, Is.Zero);
                Assert.That(visual.Tremor01, Is.Zero);
            }
        }

        [Test]
        public void PremiumVisualWaveHasPrecompressionOvershootSettleAndBuriedRetreat()
        {
            EarthPillarWaveVisualTuning tuning = EarthPillarWaveVisualTuning.PremiumDefault;
            EarthPillarWaveVisualSample pre = EarthPillarWaveSolver.EvaluateVisualMotion(
                -0.015f, 0.30f, 0.05f, 0.32f,
                WaveMotionMode.PremiumVisual, in tuning, 91u);
            EarthPillarWaveVisualSample peak = EarthPillarWaveSolver.EvaluateVisualMotion(
                tuning.RiseSeconds, 0.30f, 0.05f, 0.32f,
                WaveMotionMode.PremiumVisual, in tuning, 91u);
            EarthPillarWaveVisualSample settled = EarthPillarWaveSolver.EvaluateVisualMotion(
                tuning.RiseSeconds + tuning.SettleSeconds, 0.30f, 0.05f, 0.32f,
                WaveMotionMode.PremiumVisual, in tuning, 91u);
            EarthPillarWaveVisualSample retreat = EarthPillarWaveSolver.EvaluateVisualMotion(
                tuning.Duration - 0.01f, 0.30f, 0.05f, 0.32f,
                WaveMotionMode.PremiumVisual, in tuning, 91u);

            Assert.That(pre.Height01, Is.InRange(0.02f, 0.03f));
            Assert.That(pre.Width01, Is.LessThan(1f));
            Assert.That(pre.Tremor01, Is.GreaterThan(0f));
            Assert.That(peak.Height01, Is.InRange(1.035f, 1.05f));
            Assert.That(settled.Height01, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(retreat.Height01, Is.LessThan(0.01f));
            Assert.That(EarthPillarWaveSolver.IsVisualMotionComplete(
                tuning.Duration,
                WaveMotionMode.PremiumVisual,
                in tuning), Is.True);
        }

        [Test]
        public void PremiumVisualWaveSeedVariationStaysInsideSevenPercent()
        {
            EarthPillarWaveVisualTuning tuning = EarthPillarWaveVisualTuning.PremiumDefault;
            for (uint seed = 1u; seed <= 64u; seed++)
            {
                EarthPillarWaveVisualSample sample = EarthPillarWaveSolver.EvaluateVisualMotion(
                    tuning.RiseSeconds * 0.5f,
                    0.30f,
                    0.05f,
                    0.32f,
                    WaveMotionMode.PremiumVisual,
                    in tuning,
                    seed);
                Assert.That(Mathf.Abs(sample.TiltDegrees), Is.InRange(5.58f, 6.42f));
            }
        }
    }
}
