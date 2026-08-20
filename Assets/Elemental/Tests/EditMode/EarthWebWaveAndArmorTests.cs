using System.Linq;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthWebWaveAndArmorTests
    {
        [Test]
        public void WaveSemanticFamilies_AntiRepeatAndProduceDifferentRhythms()
        {
            EarthWaveSemanticFamily previous = EarthWaveSemanticFamily.RollingTerraces;
            var seen = new bool[6];
            for (int cast = 0; cast < 30; cast++)
            {
                float sector = (cast % 7) / 6f;
                float power = ((cast * 3) % 11) / 10f;
                EarthWaveSemanticFamily family = EarthWaveFamilySelector.Select(sector, power, previous, cast);
                Assert.That(family, Is.Not.EqualTo(previous));
                seen[(int)family] = true;
                EarthPillarWaveTuning tuning = EarthPillarWaveTuning.Default;
                EarthWebWaveTopology topology = EarthPillarWaveSolver.BuildTopology(
                    sector, power, in tuning, cast % 6, family);
                Assert.That(topology.Family, Is.EqualTo(family));
                Assert.That(topology.Cells.Length, Is.GreaterThan(8).And.LessThanOrEqualTo(96));
                previous = family;
            }
            int distinct = 0;
            for (int index = 0; index < seen.Length; index++) if (seen[index]) distinct++;
            Assert.That(distinct, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void DefaultArmorShellCoversEveryAnatomicalRegionIncludingHead()
        {
            EarthArmorShellSegment[] segments = EarthArmorShellDefinition.CreateDefaultSegments();
            Assert.That(segments, Has.Length.EqualTo(EarthArmorShellDefinition.RequiredSegmentCount));
            Assert.That(System.Array.FindAll(segments, segment => segment.Region == EarthArmorShellRegion.Head),
                Has.Length.EqualTo(12));
            Assert.That(System.Array.FindAll(segments, segment => segment.Region == EarthArmorShellRegion.Torso),
                Has.Length.EqualTo(12));
            Assert.That(System.Array.FindAll(segments, segment => segment.Region == EarthArmorShellRegion.Pelvis),
                Has.Length.EqualTo(6));
            Assert.That(System.Array.FindAll(segments, segment => segment.Region == EarthArmorShellRegion.Arm),
                Has.Length.EqualTo(16));
            Assert.That(System.Array.FindAll(segments, segment => segment.Region == EarthArmorShellRegion.Leg),
                Has.Length.EqualTo(18));

            int headFront = 0;
            int headRear = 0;
            int headTop = 0;
            for (int index = 0; index < segments.Length; index++)
            {
                EarthArmorShellSegment segment = segments[index];
                Assert.That(segment.CharacterDirection.sqrMagnitude, Is.EqualTo(1f).Within(0.001f));
                Assert.That(segment.Scale.x, Is.InRange(0.20f, 0.72f));
                Assert.That(segment.Scale.z, Is.InRange(0.30f, 0.62f));
                if (segment.Region != EarthArmorShellRegion.Head) continue;
                if (segment.CharacterDirection.z > 0.45f) headFront++;
                if (segment.CharacterDirection.z < -0.45f) headRear++;
                if (segment.CharacterDirection.y > 0.45f) headTop++;
            }
            Assert.That(headFront, Is.GreaterThanOrEqualTo(2));
            Assert.That(headRear, Is.GreaterThanOrEqualTo(2));
            Assert.That(headTop, Is.GreaterThanOrEqualTo(1), "The crown must not remain exposed.");
        }

        [Test]
        public void WebWaveBuildsSixDistinctMultiScalePolygonTopologies()
        {
            EarthPillarWaveTuning tuning = EarthPillarWaveTuning.Default;
            ulong previousDigest = 0ul;
            for (int seed = 0; seed < 6; seed++)
            {
                EarthWebWaveTopology topology = EarthPillarWaveSolver.BuildTopology(1f, 1f, in tuning, seed);
                Assert.That(topology.RadialThreadCount, Is.InRange(12, 18));
                Assert.That(topology.Cells.Length, Is.EqualTo(96));
                float[] areas = topology.Cells.Select(cell => cell.Sample.ShapeAreaScale).OrderBy(value => value).ToArray();
                Assert.That(areas[(int)(areas.Length * 0.9f)] / areas[(int)(areas.Length * 0.1f)], Is.GreaterThanOrEqualTo(3f));
                Assert.That(topology.Cells.All(cell =>
                    cell.Sample.ShapeSides >= 3 && cell.Sample.ShapeSides <= 8), Is.True);
                Assert.That(topology.Cells.All(cell =>
                    cell.Footprint.Length >= 3 && cell.Footprint.Length <= 8 && cell.Area > 0f), Is.True);
                int quadrilaterals = topology.Cells.Count(cell => cell.Footprint.Length == 4);
                Assert.That(quadrilaterals, Is.LessThan(topology.Cells.Length * 0.72f),
                    "A web topology must not collapse back into a field of rectangular prisms.");
                float[] geometricAreas = topology.Cells.Select(cell => cell.Area).OrderBy(value => value).ToArray();
                Assert.That(
                    geometricAreas[(int)(geometricAreas.Length * 0.9f)] /
                    geometricAreas[(int)(geometricAreas.Length * 0.1f)],
                    Is.GreaterThanOrEqualTo(3f));
                ulong digest = 1469598103934665603ul;
                for (int index = 0; index < topology.Cells.Length; index++)
                {
                    EarthPillarWaveSample sample = topology.Cells[index].Sample;
                    digest = (digest ^ (uint)math.round(sample.AngleDegrees * 100f)) * 1099511628211ul;
                    digest = (digest ^ (uint)sample.ShapeSides) * 1099511628211ul;
                }
                if (seed > 0) Assert.That(digest, Is.Not.EqualTo(previousDigest));
                previousDigest = digest;
            }
        }

        [Test]
        public void SharedBoundaryCellPublishesOutwardForEitherFootprintOrder()
        {
            float2[] counterClockwise =
            {
                new(-1.0f, -0.7f), new(0.9f, -0.8f),
                new(1.2f, 0.4f), new(0.1f, 1.0f), new(-1.1f, 0.5f)
            };
            float2[] clockwise = counterClockwise.Reverse().ToArray();
            Mesh first = new Mesh();
            Mesh second = new Mesh();
            try
            {
                EarthWebWaveCellMeshFactory.ConfigureSharedBoundaryCell(first, counterClockwise, 81u, 0.8f);
                EarthWebWaveCellMeshFactory.ConfigureSharedBoundaryCell(second, clockwise, 82u, 0.8f);
                Assert.That(EarthMeshIntegrityValidator.Validate(first, EarthMeshIntegrityPolicy.ConvexCollider).IsValid,
                    Is.True);
                Assert.That(EarthMeshIntegrityValidator.Validate(second, EarthMeshIntegrityPolicy.ConvexCollider).IsValid,
                    Is.True);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void PlatformFracturePlanStaysInsideAuthoredConvexBoundary()
        {
            float2[] boundary =
            {
                new float2(-3.5f, -1.2f), new float2(2.8f, -1.6f),
                new float2(4.1f, 0.4f), new float2(1.8f, 2.3f),
                new float2(-2.7f, 1.9f), new float2(-4.2f, 0.2f)
            };
            EarthStructureFracturePlan plan = VoronoiFractureSolver.BuildHierarchicalClipped(
                0xBEEFu, boundary, 24);
            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Cells.Length, Is.EqualTo(24));
            Assert.That(plan.Cells.All(cell => cell.Vertices.Length >= 3 && cell.Area > 0f), Is.True);
            float totalArea = plan.Cells.Sum(cell => cell.Area);
            Assert.That(totalArea, Is.EqualTo(24.08f).Within(0.2f));
        }

        [Test]
        public void QuickStoneSecondClickFiresInsideWindowAndExpiresOutsideIt()
        {
            EarthQuickCastProfileData data = EarthQuickCastProfileData.Default;
            var session = new EarthQuickStoneSession(in data);
            Assert.That(session.TryPrime(10f, 42u), Is.True);
            Assert.That(session.TryFire(10.18f, out float speed), Is.True);
            Assert.That(speed, Is.InRange(30f, 38f));
            session.Reset();
            session.TryPrime(20f, 43u);
            Assert.That(session.ExpireIfNeeded(20.421f), Is.True);
            Assert.That(session.IsPrimed, Is.False);
        }

        [Test]
        public void QuickStoneBuffersSecondClickUntilExtractionCompletes()
        {
            EarthQuickCastProfileData data = EarthQuickCastProfileData.Default;
            var session = new EarthQuickStoneSession(in data);
            Assert.That(session.TryPrime(2f, 77u), Is.True);
            Assert.That(session.IsExtracting, Is.True);
            Assert.That(session.TryFire(2.04f, out float earlySpeed), Is.True,
                "The second click belongs to quick-stone even while its mesh is emerging.");
            Assert.That(earlySpeed, Is.Zero);
            Assert.That(session.HasBufferedFire, Is.True);
            Assert.That(session.TryConsumeBufferedFire(2.149f, out _), Is.False);
            Assert.That(session.TryConsumeBufferedFire(2.151f, out float launchSpeed), Is.True);
            Assert.That(launchSpeed, Is.InRange(30f, 38f));
            Assert.That(session.State, Is.EqualTo(EarthQuickStoneState.Fired));
        }

        [Test]
        public void ArmorWheelCompressesAndRequiresTwoConfirmedOverscrollSteps()
        {
            EarthArmorProfileData data = EarthArmorProfileData.Default;
            var session = new EarthArmorSession(in data);
            session.Begin(1f);
            Assert.That(session.ApplyWheelSteps(1f, 1f), Is.EqualTo(EarthArmorInputResult.OverscrollArmed));
            Assert.That(session.Active, Is.True);
            Assert.That(session.ApplyWheelSteps(1f, 1.2f), Is.EqualTo(EarthArmorInputResult.RadialRelease));
            Assert.That(session.Active, Is.False);
            session.Begin(0.7f);
            session.ApplyWheelSteps(-1f, 2f);
            Assert.That(session.Phase01, Is.LessThan(0.7f));
        }

        [Test]
        public void CircularGestureUsesViewportScale()
        {
            EarthCircularGestureState state = EarthCircularGestureSolver.Begin(new float2(0.5f, 0.5f));
            EarthCircularGestureSample sample = default;
            for (int index = 0; index <= 20; index++)
            {
                float angle = -index * math.PI * 2f / 20f;
                float2 point = new float2(0.5f, 0.5f) + new float2(math.cos(angle), math.sin(angle)) * 0.18f;
                sample = EarthCircularGestureSolver.Step(ref state, point);
            }
            Assert.That(sample.Recognized, Is.True);
            Assert.That(sample.Direction, Is.EqualTo(EarthCircularGestureDirection.Clockwise));
            Assert.That(sample.Phase01, Is.GreaterThan(0.95f));
        }
    }
}
