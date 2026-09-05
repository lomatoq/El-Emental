using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthArmorCoverageRuntimeTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const int NeckFirstPiece = EarthArmorProfile.DefaultPieceCount + EarthArmorHeadShell.FillerCount;
        private const int NeckPieceCount = 8;
        private const int LeftShoulderFirstPiece = NeckFirstPiece + NeckPieceCount;
        private const int ShoulderPieceCount = 4;
        private const int RightShoulderFirstPiece = LeftShoulderFirstPiece + ShoulderPieceCount;
        private const int UpperTorsoFirstPiece = RightShoulderFirstPiece + ShoulderPieceCount;
        private const int TorsoRingPieceCount = 6;
        private const int LowerTorsoFirstPiece = UpperTorsoFirstPiece + TorsoRingPieceCount;

        [UnityTest]
        public IEnumerator CompactArmorKeepsNeckShouldersAndTorsoClosedWhileWalkingAndTurning()
        {
            Scene existing = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(existing.isLoaded, Is.False, "Run from the focused armor-coverage launcher.");
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            AsyncOperation unload = null;
            try
            {
                EarthSceneReadinessGate gate = All<EarthSceneReadinessGate>(scene).First();
                double deadline = Time.realtimeSinceStartupAsDouble + 130d;
                while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.That(gate.IsReady, Is.True, gate.Status);

                foreach (EarthMvpBotController bot in All<EarthMvpBotController>(scene)) bot.enabled = false;
                PlanetMotor motor = All<PlanetMotor>(scene).First(value => value.name == "Planet Character");
                Animator animator = motor.GetComponentInChildren<Animator>(true);
                EarthArmorController armor = motor.GetComponent<EarthArmorController>();
                Assert.That(animator != null && animator.isHuman, Is.True);
                Assert.That(armor, Is.Not.Null);

                var driver = motor.gameObject.AddComponent<CoverageMotorInput>();
                motor.ConfigureInputSource(driver);
                motor.SettleTangentialMotion();
                Assert.That(armor.Begin(), Is.True);
                for (int tick = 0; tick < 18; tick++) yield return new WaitForFixedUpdate();
                Assert.That(armor.ActivePieceCount, Is.EqualTo(EarthArmorProfile.MaximumPieceCount));

                var report = new CoverageReport();
                try
                {
                    Measure(animator, motor, armor, "idle", report);
                    yield return DriveAndMeasure(animator, motor, armor, driver,
                        new float2(0f, .58f), 28, "walk", report);
                    yield return DriveAndMeasure(animator, motor, armor, driver,
                        new float2(-.62f, 0f), 22, "turn-left", report);
                    yield return DriveAndMeasure(animator, motor, armor, driver,
                        new float2(.62f, 0f), 22, "turn-right", report);
                    driver.Move = float2.zero;
                    for (int tick = 0; tick < 12; tick++) yield return new WaitForFixedUpdate();
                    Measure(animator, motor, armor, "settled", report);
                }
                finally
                {
                    // Persist geometric evidence even when a coverage assertion fails.
                    // In particular, compare nearest-any with nearest-expected-zone:
                    // helmet plates can otherwise make a healthy collar look sparse.
                    report.WriteToDisk();
                }

                Assert.That(report.MaximumNeckGap, Is.LessThanOrEqualTo(.24f),
                    $"Neck collar opened while animated: {report.WorstSample}; diagnostics={report.ReportPath}");
                Assert.That(report.MaximumShoulderGap, Is.LessThanOrEqualTo(.25f),
                    $"Shoulder shell opened while animated: {report.WorstSample}; diagnostics={report.ReportPath}");
                Assert.That(report.MaximumTorsoGap, Is.LessThanOrEqualTo(.30f),
                    $"Torso seams opened while animated: {report.WorstSample}; diagnostics={report.ReportPath}");
                Assert.That(report.MinimumDistinctNeckPlates, Is.GreaterThanOrEqualTo(4),
                    $"Nearest-any collar diversity was low; compare ExpectedZone in {report.ReportPath}");
                Assert.That(report.MinimumDistinctLeftShoulderPlates, Is.GreaterThanOrEqualTo(2),
                    $"Left shoulder diversity was low; diagnostics={report.ReportPath}");
                Assert.That(report.MinimumDistinctRightShoulderPlates, Is.GreaterThanOrEqualTo(2),
                    $"Right shoulder diversity was low; diagnostics={report.ReportPath}");
                Assert.That(report.MinimumDistinctTorsoPlates, Is.GreaterThanOrEqualTo(6),
                    $"Torso diversity was low; diagnostics={report.ReportPath}");
                Assert.That(motor.ArmorEncumbrance01, Is.GreaterThan(.4f));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) while (!unload.isDone) yield return null;
        }

        private static IEnumerator DriveAndMeasure(
            Animator animator, PlanetMotor motor, EarthArmorController armor,
            CoverageMotorInput input, float2 move, int ticks, string label, CoverageReport report)
        {
            input.Move = move;
            for (int tick = 0; tick < ticks; tick++)
            {
                yield return new WaitForFixedUpdate();
                if ((tick % 4) == 3) Measure(animator, motor, armor, label, report);
            }
        }

        private static void Measure(
            Animator animator, PlanetMotor motor, EarthArmorController armor,
            string pose, CoverageReport report)
        {
            var pieces = new EarthArmorPiece[EarthArmorProfile.MaximumPieceCount];
            int count = armor.CopyActivePiecesNonAlloc(pieces);
            Assert.That(count, Is.EqualTo(EarthArmorProfile.MaximumPieceCount));
            Vector3 up = motor.LocalUp.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;

            Transform neck = Bone(animator, HumanBodyBones.Neck, HumanBodyBones.UpperChest);
            Transform leftShoulder = Bone(animator, HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm);
            Transform rightShoulder = Bone(animator, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm);
            Transform upperChest = Bone(animator, HumanBodyBones.UpperChest, HumanBodyBones.Chest);
            Transform chest = Bone(animator, HumanBodyBones.Chest, HumanBodyBones.Spine);

            var distinct = new HashSet<int>();
            var expectedDistinct = new HashSet<int>();
            float neckGap = SampleRing(
                pose, "neck", neck, neck.position, .085f, 6, up, forward, right,
                pieces, count, NeckFirstPiece, NeckPieceCount, distinct, expectedDistinct, report);
            report.MinimumDistinctNeckPlates = Mathf.Min(report.MinimumDistinctNeckPlates, distinct.Count);
            report.MinimumDistinctExpectedNeckPlates = Mathf.Min(
                report.MinimumDistinctExpectedNeckPlates, expectedDistinct.Count);
            distinct.Clear();
            expectedDistinct.Clear();
            float leftGap = SampleShoulder(
                pose, "left-shoulder", leftShoulder, leftShoulder.position, -right, up, forward,
                pieces, count, LeftShoulderFirstPiece, ShoulderPieceCount, distinct, expectedDistinct, report);
            report.MinimumDistinctLeftShoulderPlates = Mathf.Min(report.MinimumDistinctLeftShoulderPlates, distinct.Count);
            report.MinimumDistinctExpectedLeftShoulderPlates = Mathf.Min(
                report.MinimumDistinctExpectedLeftShoulderPlates, expectedDistinct.Count);
            distinct.Clear();
            expectedDistinct.Clear();
            float rightGap = SampleShoulder(
                pose, "right-shoulder", rightShoulder, rightShoulder.position, right, up, forward,
                pieces, count, RightShoulderFirstPiece, ShoulderPieceCount, distinct, expectedDistinct, report);
            report.MinimumDistinctRightShoulderPlates = Mathf.Min(report.MinimumDistinctRightShoulderPlates, distinct.Count);
            report.MinimumDistinctExpectedRightShoulderPlates = Mathf.Min(
                report.MinimumDistinctExpectedRightShoulderPlates, expectedDistinct.Count);
            distinct.Clear();
            expectedDistinct.Clear();
            float torsoGap = Mathf.Max(
                SampleRing(
                    pose, "upper-torso", upperChest, upperChest.position, .16f, 8, up, forward, right,
                    pieces, count, UpperTorsoFirstPiece, TorsoRingPieceCount, distinct, expectedDistinct, report),
                SampleRing(
                    pose, "lower-torso", chest, chest.position, .16f, 8, up, forward, right,
                    pieces, count, LowerTorsoFirstPiece, TorsoRingPieceCount, distinct, expectedDistinct, report));
            report.MinimumDistinctTorsoPlates = Mathf.Min(report.MinimumDistinctTorsoPlates, distinct.Count);
            report.MinimumDistinctExpectedTorsoPlates = Mathf.Min(
                report.MinimumDistinctExpectedTorsoPlates, expectedDistinct.Count);

            report.Record(pose, neckGap, Mathf.Max(leftGap, rightGap), torsoGap);
        }

        private static float SampleShoulder(
            string pose, string zone, Transform bone, Vector3 center,
            Vector3 outward, Vector3 up, Vector3 forward,
            EarthArmorPiece[] pieces, int count, int expectedFirst, int expectedCount,
            HashSet<int> distinct, HashSet<int> expectedDistinct, CoverageReport report)
        {
            return Mathf.Max(
                MeasureProbe(pose, zone, 0, bone, center + outward * .09f + up * .06f,
                    pieces, count, expectedFirst, expectedCount, distinct, expectedDistinct, report),
                MeasureProbe(pose, zone, 1, bone, center + outward * .08f + forward * .07f,
                    pieces, count, expectedFirst, expectedCount, distinct, expectedDistinct, report),
                MeasureProbe(pose, zone, 2, bone, center + outward * .08f - forward * .07f,
                    pieces, count, expectedFirst, expectedCount, distinct, expectedDistinct, report));
        }

        private static float SampleRing(
            string pose, string zone, Transform bone, Vector3 center, float radius, int samples,
            Vector3 up, Vector3 forward, Vector3 right, EarthArmorPiece[] pieces, int count,
            int expectedFirst, int expectedCount, HashSet<int> distinct,
            HashSet<int> expectedDistinct, CoverageReport report)
        {
            float maximum = 0f;
            for (int index = 0; index < samples; index++)
            {
                float angle = index * Mathf.PI * 2f / samples;
                Vector3 point = center + (right * Mathf.Sin(angle) + forward * Mathf.Cos(angle)) * radius;
                maximum = Mathf.Max(maximum, MeasureProbe(
                    pose, zone, index, bone, point, pieces, count,
                    expectedFirst, expectedCount, distinct, expectedDistinct, report));
            }
            return maximum;
        }

        private static float MeasureProbe(
            string pose, string zone, int sampleIndex, Transform bone, Vector3 point,
            EarthArmorPiece[] pieces, int count, int expectedFirst, int expectedCount,
            HashSet<int> distinct, HashSet<int> expectedDistinct, CoverageReport report)
        {
            NearestHit any = FindNearest(point, pieces, count, 0, int.MaxValue);
            NearestHit expected = FindNearest(
                point, pieces, count, expectedFirst, expectedFirst + expectedCount);
            if (any.PieceIndex >= 0) distinct.Add(any.PieceIndex);
            if (expected.PieceIndex >= 0) expectedDistinct.Add(expected.PieceIndex);
            report.AddProbe(pose, zone, sampleIndex, bone, point, expectedFirst, expectedCount, any, expected);
            return any.SurfaceDistance;
        }

        private static NearestHit FindNearest(
            Vector3 point, EarthArmorPiece[] pieces, int count, int firstInclusive, int lastExclusive)
        {
            var nearest = NearestHit.Missing;
            for (int index = 0; index < count; index++)
            {
                EarthArmorPiece piece = pieces[index];
                if (piece == null || piece.PieceIndex < firstInclusive || piece.PieceIndex >= lastExclusive)
                    continue;
                Collider collider = piece.PieceCollider;
                bool usedCollider = collider != null && collider.enabled;
                Vector3 surface = usedCollider
                    ? collider.ClosestPoint(point)
                    : piece.transform.position;
                float distance = Vector3.Distance(point, surface);
                if (distance >= nearest.SurfaceDistance) continue;
                nearest = new NearestHit(
                    piece.PieceIndex,
                    distance,
                    piece.transform.position,
                    piece.transform.lossyScale,
                    surface,
                    usedCollider,
                    collider != null ? collider.bounds.center : piece.transform.position,
                    collider != null ? collider.bounds.extents : Vector3.zero);
            }
            return nearest;
        }

        private static Transform Bone(Animator animator, HumanBodyBones primary, HumanBodyBones fallback) =>
            animator.GetBoneTransform(primary) ?? animator.GetBoneTransform(fallback);

        private static T[] All<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private sealed class CoverageMotorInput : MonoBehaviour, IPlanetMotorInputSource
        {
            public float2 Move;
            public PlanetMotorCommand SampleCommand(uint tick) => new(tick, Move, false);
        }

        private readonly struct NearestHit
        {
            public static readonly NearestHit Missing = new NearestHit(
                -1, float.MaxValue, Vector3.zero, Vector3.zero, Vector3.zero,
                false, Vector3.zero, Vector3.zero);

            public readonly int PieceIndex;
            public readonly float SurfaceDistance;
            public readonly Vector3 WorldPosition;
            public readonly Vector3 LossyScale;
            public readonly Vector3 ClosestPoint;
            public readonly bool UsedCollider;
            public readonly Vector3 ColliderBoundsCenter;
            public readonly Vector3 ColliderBoundsExtents;

            public NearestHit(
                int pieceIndex, float surfaceDistance, Vector3 worldPosition, Vector3 lossyScale,
                Vector3 closestPoint, bool usedCollider, Vector3 colliderBoundsCenter,
                Vector3 colliderBoundsExtents)
            {
                PieceIndex = pieceIndex;
                SurfaceDistance = surfaceDistance;
                WorldPosition = worldPosition;
                LossyScale = lossyScale;
                ClosestPoint = closestPoint;
                UsedCollider = usedCollider;
                ColliderBoundsCenter = colliderBoundsCenter;
                ColliderBoundsExtents = colliderBoundsExtents;
            }
        }

        [Serializable]
        private sealed class CoverageProbe
        {
            public string Pose;
            public string Zone;
            public int SampleIndex;
            public Vector3 SampleWorldPosition;
            public string BoneName;
            public Vector3 BoneWorldPosition;
            public Vector3 BoneWorldEuler;
            public int ExpectedFirstPiece;
            public int ExpectedPieceCount;
            public int NearestAnyPiece;
            public float NearestAnySurfaceDistance;
            public Vector3 NearestAnyWorldPosition;
            public Vector3 NearestAnyLossyScale;
            public Vector3 NearestAnyClosestPoint;
            public bool NearestAnyUsedCollider;
            public Vector3 NearestAnyBoundsCenter;
            public Vector3 NearestAnyBoundsExtents;
            public int NearestExpectedPiece;
            public float NearestExpectedSurfaceDistance;
            public Vector3 NearestExpectedWorldPosition;
            public Vector3 NearestExpectedLossyScale;
            public Vector3 NearestExpectedClosestPoint;
            public bool NearestExpectedUsedCollider;
            public Vector3 NearestExpectedBoundsCenter;
            public Vector3 NearestExpectedBoundsExtents;
        }

        [Serializable]
        private sealed class CoverageReport
        {
            public string RunUtc = DateTime.UtcNow.ToString("O");
            public string ReportPath;
            public float MaximumNeckGap;
            public float MaximumShoulderGap;
            public float MaximumTorsoGap;
            public float MaximumExpectedNeckGap;
            public float MaximumExpectedShoulderGap;
            public float MaximumExpectedTorsoGap;
            public int MinimumDistinctNeckPlates = int.MaxValue;
            public int MinimumDistinctLeftShoulderPlates = int.MaxValue;
            public int MinimumDistinctRightShoulderPlates = int.MaxValue;
            public int MinimumDistinctTorsoPlates = int.MaxValue;
            public int MinimumDistinctExpectedNeckPlates = int.MaxValue;
            public int MinimumDistinctExpectedLeftShoulderPlates = int.MaxValue;
            public int MinimumDistinctExpectedRightShoulderPlates = int.MaxValue;
            public int MinimumDistinctExpectedTorsoPlates = int.MaxValue;
            public string WorstSample;
            public List<CoverageProbe> Probes = new List<CoverageProbe>(512);

            public void AddProbe(
                string pose, string zone, int sampleIndex, Transform bone, Vector3 sample,
                int expectedFirst, int expectedCount, NearestHit any, NearestHit expected)
            {
                Probes.Add(new CoverageProbe
                {
                    Pose = pose,
                    Zone = zone,
                    SampleIndex = sampleIndex,
                    SampleWorldPosition = sample,
                    BoneName = bone != null ? bone.name : "<missing>",
                    BoneWorldPosition = bone != null ? bone.position : Vector3.zero,
                    BoneWorldEuler = bone != null ? bone.rotation.eulerAngles : Vector3.zero,
                    ExpectedFirstPiece = expectedFirst,
                    ExpectedPieceCount = expectedCount,
                    NearestAnyPiece = any.PieceIndex,
                    NearestAnySurfaceDistance = any.SurfaceDistance,
                    NearestAnyWorldPosition = any.WorldPosition,
                    NearestAnyLossyScale = any.LossyScale,
                    NearestAnyClosestPoint = any.ClosestPoint,
                    NearestAnyUsedCollider = any.UsedCollider,
                    NearestAnyBoundsCenter = any.ColliderBoundsCenter,
                    NearestAnyBoundsExtents = any.ColliderBoundsExtents,
                    NearestExpectedPiece = expected.PieceIndex,
                    NearestExpectedSurfaceDistance = expected.SurfaceDistance,
                    NearestExpectedWorldPosition = expected.WorldPosition,
                    NearestExpectedLossyScale = expected.LossyScale,
                    NearestExpectedClosestPoint = expected.ClosestPoint,
                    NearestExpectedUsedCollider = expected.UsedCollider,
                    NearestExpectedBoundsCenter = expected.ColliderBoundsCenter,
                    NearestExpectedBoundsExtents = expected.ColliderBoundsExtents
                });

                switch (zone)
                {
                    case "neck":
                        MaximumExpectedNeckGap = Mathf.Max(MaximumExpectedNeckGap, expected.SurfaceDistance);
                        break;
                    case "left-shoulder":
                    case "right-shoulder":
                        MaximumExpectedShoulderGap = Mathf.Max(MaximumExpectedShoulderGap, expected.SurfaceDistance);
                        break;
                    default:
                        MaximumExpectedTorsoGap = Mathf.Max(MaximumExpectedTorsoGap, expected.SurfaceDistance);
                        break;
                }
            }

            public void Record(string pose, float neck, float shoulder, float torso)
            {
                if (neck > MaximumNeckGap) { MaximumNeckGap = neck; WorstSample = $"{pose}/neck={neck:F3}"; }
                if (shoulder > MaximumShoulderGap) { MaximumShoulderGap = shoulder; WorstSample = $"{pose}/shoulder={shoulder:F3}"; }
                if (torso > MaximumTorsoGap) { MaximumTorsoGap = torso; WorstSample = $"{pose}/torso={torso:F3}"; }
            }

            public void WriteToDisk()
            {
                string stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
                string directory = Path.GetFullPath(Path.Combine(
                    Application.dataPath, "..", "BuildReports", "EnvironmentAnimationRescue",
                    "ArmorCoverageDiagnostics", stamp));
                Directory.CreateDirectory(directory);
                ReportPath = Path.Combine(directory, "CoverageDiagnostics.json");
                string json = JsonUtility.ToJson(this, true);
                File.WriteAllText(ReportPath, json);
                string latest = Path.GetFullPath(Path.Combine(
                    Application.dataPath, "..", "BuildReports", "ArmorCoverageDiagnosticsLatest.json"));
                File.WriteAllText(latest, json);
                Debug.Log(
                    $"ARMOR_COVERAGE_DIAGNOSTICS path={ReportPath} " +
                    $"neckAnyDistinct={MinimumDistinctNeckPlates} " +
                    $"neckExpectedDistinct={MinimumDistinctExpectedNeckPlates} " +
                    $"neckAnyGap={MaximumNeckGap:F4} neckExpectedGap={MaximumExpectedNeckGap:F4}");
            }
        }
    }
}
