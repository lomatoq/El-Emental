using Elemental.Runtime.World;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthCombatMobilityFixTests
    {
        [Test]
        public void PlatformAccumulatesWeakHitsWhileFracturePreparationIsPending()
        {
            var host = new GameObject("Platform fatigue regression");
            host.SetActive(false);
            try
            {
                var platform = host.AddComponent<EarthPlatform>();
                float hit = platform.FractureThreshold / 4f;
                for (int i = 0; i < 3; i++)
                    Assert.That(platform.ApplyStructureImpact(Vector3.zero, Vector3.right, hit), Is.False);
                Assert.That(platform.AccumulatedImpactImpulse, Is.EqualTo(hit * 3f));
                Assert.That(platform.ApplyStructureImpact(Vector3.zero, Vector3.right, hit), Is.True);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void WeakImpactsAccumulateAndInvalidContactsCannotPoisonDamage()
        {
            EarthImpactDamage damage = default;
            for (int i = 0; i < 10; i++) Assert.That(damage.Add(12f), Is.True);
            Assert.That(damage.Impulse, Is.EqualTo(120f));
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -12f, .5f })
                Assert.That(damage.Add(invalid), Is.False);
            Assert.That(damage.Impulse, Is.EqualTo(120f));
            damage.Consume(95f);
            Assert.That(damage.Impulse, Is.EqualTo(25f));
        }

        [Test]
        public void PremiumWaveHasContinuousPoseAtEveryPhaseBoundary()
        {
            var tuning = new EarthPillarWaveVisualTuning(.4f, .07f, .8f, .1f, .6f, .5f, 1.2f, 12f, 2f, .8f, .2f);
            Assert.That(tuning.PrecompressionSeconds, Is.EqualTo(.4f));
            Assert.That(tuning.RiseSeconds, Is.EqualTo(.8f));
            float[] boundaries = { -.4f, 0f, .8f, 1.4f, 1.9f, 3.1f };
            foreach (uint seed in new uint[] { 1, 17, 131 })
            foreach (float time in boundaries)
            {
                var a = EarthPillarWaveSolver.EvaluateVisualMotion(time - .00001f, .36f, .1f, .46f,
                    WaveMotionMode.PremiumVisual, in tuning, seed);
                var b = EarthPillarWaveSolver.EvaluateVisualMotion(time + .00001f, .36f, .1f, .46f,
                    WaveMotionMode.PremiumVisual, in tuning, seed);
                Assert.That(b.Height01, Is.EqualTo(a.Height01).Within(.001f), "height at " + time);
                Assert.That(b.Width01, Is.EqualTo(a.Width01).Within(.001f), "width at " + time);
                Assert.That(b.TiltDegrees, Is.EqualTo(a.TiltDegrees).Within(.002f), "tilt at " + time);
                Assert.That(b.Tremor01, Is.EqualTo(a.Tremor01).Within(.001f), "tremor at " + time);
            }
        }

        [Test]
        public void MeshChipsHaveIndependentSpawnAnglesAndSpinOnAllAxes()
        {
            var host = new GameObject("Chip rotation regression");
            try
            {
                var system = host.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                EarthParticleSystemTuningApplier.ApplyChipRotation(system, new Vector2(-240f, 300f));
                Assert.That(system.main.startRotation3D, Is.True);
                Assert.That(system.main.startRotationX.constantMax, Is.GreaterThan(6f));
                var spin = system.rotationOverLifetime;
                Assert.That(spin.enabled && spin.separateAxes, Is.True);
                Assert.That(spin.x.constantMin, Is.LessThan(0f));
                Assert.That(spin.y.constantMax, Is.GreaterThan(0f));
                Assert.That(spin.z.constantMax, Is.GreaterThan(0f));
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
