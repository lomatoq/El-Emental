using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthWaveRepairTests
    {
        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
        public void EveryFamilyHasBoundedCellsAcrossSeedsAndCharges(int family)
        {
            var tuning = EarthPillarWaveTuning.Default;
            float limit = EarthWaveFootprintSolver.Radius(tuning.MaximumWidth) * 2f + .002f;
            for (int seed = 0; seed < 6; seed++)
            foreach (float charge in new[] { .15f, .55f, 1f })
            {
                var topology = EarthPillarWaveSolver.BuildTopology(charge, charge, in tuning, seed,
                    (EarthWaveSemanticFamily)family);
                Assert.That(topology.Cells.Length, Is.InRange(1, 96));
                foreach (var cell in topology.Cells)
                {
                    Assert.That(cell.Area, Is.GreaterThan(0f));
                    Assert.That(cell.Footprint.Length, Is.InRange(3, 8));
                    foreach (var a in cell.Footprint)
                    foreach (var b in cell.Footprint)
                    {
                        Assert.That(math.all(math.isfinite(a)), Is.True);
                        Assert.That(math.distance(a, b), Is.LessThanOrEqualTo(limit),
                            $"Family {family} seed {seed} charge {charge}: oversized fracture cell");
                    }
                }
            }
        }

        [Test]
        public void OverlappingCastsCannotReopenPreviouslyClaimedFracture()
        {
            var host = new GameObject("Wave claim test"); host.SetActive(false);
            try
            {
                var pool = host.AddComponent<EarthPillarWavePool>();
                for (uint cast = 1; cast <= 70; cast++) Assert.That(pool.TryClaimFaultLineTarget(cast, 100u), Is.True);
                for (uint cast = 1; cast <= 70; cast++) Assert.That(pool.TryClaimFaultLineTarget(cast, 200u), Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void CurvesDriveMotionAndLongTimingsWithoutPhaseJumps()
        {
            var profile = ScriptableObject.CreateInstance<EarthPillarWaveProfile>();
            try
            {
                profile.ConfigureMotionMode(WaveMotionMode.PremiumVisual);
                var data = new SerializedObject(profile);
                data.FindProperty("premiumRiseSeconds").floatValue = 2.4f;
                data.FindProperty("premiumHoldSeconds").floatValue = 1.4f;
                data.ApplyModifiedPropertiesWithoutUndo();
                float original = profile.EvaluateVisualMotion(1.2f, 1u).Height01;
                data.FindProperty("riseCurve").animationCurveValue = new AnimationCurve(
                    new Keyframe(0f, .025f), new Keyframe(.5f, .2f), new Keyframe(1f, 1.045f));
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(profile.ColumnRiseSeconds, Is.EqualTo(2.4f));
                Assert.That(profile.EvaluateVisualMotion(1.2f, 1u).Height01, Is.LessThan(original - .1f));
                var timing = profile.AnimationTiming;
                foreach (float boundary in new[] { -timing.Anticipation, 0f, timing.Rise,
                    timing.Rise + timing.Settle, timing.Rise + timing.Settle + timing.Hold, timing.Duration })
                {
                    var left = profile.EvaluateVisualMotion(boundary - .00001f, 1u);
                    var right = profile.EvaluateVisualMotion(boundary + .00001f, 1u);
                    Assert.That(right.Height01, Is.EqualTo(left.Height01).Within(.001f));
                    Assert.That(right.TiltDegrees, Is.EqualTo(left.TiltDegrees).Within(.01f));
                }
                UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(profile);
                Assert.That(editor.GetType().Name, Is.EqualTo("EarthPillarWaveProfileEditor"));
                Object.DestroyImmediate(editor);
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
