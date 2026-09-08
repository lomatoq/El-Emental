using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Object = UnityEngine.Object;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering.Universal;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthReassemblyRuntimeTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";

        [UnityTest]
        public IEnumerator BakedWallPhysicallyReassemblesAndRestoresIntactProxy()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWall wall = SpawnFracturedWall(scene, 810u);
            yield return new WaitForSeconds(0.75f);
            using var capture = new WallRepairCapture(wall);
            capture.Write("intact");
            Assert.That(wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 6000f), Is.True);
            yield return new WaitForFixedUpdate();
            capture.Write("cracked");
            // One ordinary hit intentionally preserves bonds. Exercise the real
            // full MMB disassembly path, then let actual gravity separate cells
            // before reserving them for return. Never teleport evidence pieces.
            var restPositions = new Vector3[wall.StructureRuntime.PieceCount];
            for (int i = 0; i < restPositions.Length; i++)
                restPositions[i] = wall.StructureRuntime.GetPieceRuntime(i).Body.position;
            Assert.That(wall.SetMagicDisassemblyProgress(1f, wall.transform.position, wall.transform.forward), Is.True);
            yield return new WaitForSeconds(.3f);
            float maximumSeparation = 0f;
            for (int i = 0; i < restPositions.Length; i++)
                maximumSeparation = Mathf.Max(maximumSeparation,
                    Vector3.Distance(restPositions[i], wall.StructureRuntime.GetPieceRuntime(i).Body.position));
            Assert.That(maximumSeparation, Is.GreaterThan(.15f), "Actual gravity must visibly separate pieces before repair capture.");

            EarthReassemblyController repair = wall.Reassembly;
            int repairedBonds = 0;
            int rebuiltEvents = 0;
            int captured = 0;
            int weldedAtCapture = -1;
            repair.PieceCaptured += _ =>
            {
                if (captured > 0) Assert.That(repair.WeldedPieceCount, Is.GreaterThan(weldedAtCapture), "Next wall piece may only fly after previous weld.");
                weldedAtCapture = repair.WeldedPieceCount;
                captured++;
            };
            repair.BondRepaired += _ => repairedBonds++;
            repair.StructureRebuilt += _ => rebuiltEvents++;

            capture.Write("fractured");
            Assert.That(repair.TryBeginRepair(900u), Is.True);
            float deadline = Time.realtimeSinceStartup + 30f;
            float nextCapture = Time.time;
            while (repair.IsRepairing && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
                if (Time.time >= nextCapture)
                {
                    float captureStart = Time.realtimeSinceStartup;
                    capture.Write("repair");
                    // Retain the original 30-second gameplay budget; PNG I/O is
                    // test evidence overhead, not time spent by the repair solver.
                    deadline += Time.realtimeSinceStartup - captureStart;
                    nextCapture = Time.time + .1f;
                }
            }
            capture.Write("complete");

            Assert.That(repair.IsRepairing, Is.False,
                $"Repair stalled at {repair.WeldedPieceCount}/{repair.SelectedPieceCount}; " +
                $"piece {repair.CurrentPieceIndex} phase {repair.CurrentPiecePhase}, " +
                $"error {repair.CurrentPiecePositionError:F4}, speed {repair.CurrentPieceSpeed:F4}, " +
                $"angle {repair.CurrentPieceAngleErrorDegrees:F2}, angular {repair.CurrentPieceAngularSpeed:F3}, " +
                $"retry {repair.CurrentPieceRetryCount}.");
            Assert.That(repair.LastRepairWasPartial, Is.False);
            Assert.That(wall.IsCollapsing, Is.False);
            Assert.That(wall.StructureRuntime.State.Phase, Is.EqualTo(EarthStructurePhase.Rebuilt));
            Assert.That(wall.ActiveFracturePieceCount, Is.Zero);
            Assert.That(wall.VisualEmergenceRoot.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(wall.GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That(repairedBonds, Is.EqualTo(wall.StructureRuntime.BondCount));
            Assert.That(rebuiltEvents, Is.EqualTo(1));
            Assert.That(captured, Is.GreaterThan(1));
            Assert.That(wall.FirstFracturePiece.parent, Is.EqualTo(wall.transform));
            AssertFinite(wall.transform.position);

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator MissingPieceProducesStablePartialRepairWithoutProxySwap()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWall wall = SpawnFracturedWall(scene, 820u);
            yield return new WaitForSeconds(0.75f);
            wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 6000f);
            yield return new WaitForFixedUpdate();

            var targets = new IEarthPhysicalTarget[48];
            int targetCount = wall.CopyActiveTargetsNonAlloc(targets);
            EarthPieceRuntime missing = targets[targetCount - 1] as EarthPieceRuntime;
            missing.Body.detectCollisions = false;
            missing.Body.isKinematic = true;
            missing.gameObject.SetActive(false);

            EarthReassemblyController repair = wall.Reassembly;
            Assert.That(repair.TryBeginRepair(920u), Is.True);
            float deadline = Time.realtimeSinceStartup + 30f;
            while (repair.IsRepairing && Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();

            Assert.That(repair.IsRepairing, Is.False,
                $"Partial repair stalled at {repair.WeldedPieceCount}/{repair.SelectedPieceCount}; " +
                $"piece {repair.CurrentPieceIndex} phase {repair.CurrentPiecePhase}, " +
                $"error {repair.CurrentPiecePositionError:F4}, speed {repair.CurrentPieceSpeed:F4}, " +
                $"angle {repair.CurrentPieceAngleErrorDegrees:F2}, angular {repair.CurrentPieceAngularSpeed:F3}, " +
                $"retry {repair.CurrentPieceRetryCount}.");
            Assert.That(repair.LastRepairWasPartial, Is.True);
            Assert.That(repair.SelectedPieceCount, Is.EqualTo(targetCount - 1));
            Assert.That(repair.WeldedPieceCount, Is.EqualTo(targetCount - 1));
            Assert.That(wall.IsCollapsing, Is.True);
            Assert.That(wall.StructureRuntime.State.Phase, Is.EqualTo(EarthStructurePhase.Fractured));
            Assert.That(wall.VisualEmergenceRoot.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(missing.gameObject.activeSelf, Is.False, "Repair must not invent missing mass.");

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ReleaseInterruptsRepairAndLeavesUnweldedPiecesPhysical()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWall wall = SpawnFracturedWall(scene, 830u);
            yield return new WaitForSeconds(0.75f);
            wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 6000f);
            yield return new WaitForFixedUpdate();

            EarthReassemblyController repair = wall.Reassembly;
            int interruptedEvents = 0;
            repair.RepairInterrupted += value =>
            {
                if (value.Reason == EarthRepairInterruptReason.Released) interruptedEvents++;
            };
            Assert.That(repair.TryBeginRepair(930u), Is.True);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            repair.Interrupt(EarthRepairInterruptReason.Released, 931u);

            Assert.That(repair.IsRepairing, Is.False);
            Assert.That(interruptedEvents, Is.EqualTo(1));
            Assert.That(wall.IsCollapsing, Is.True);
            bool foundDynamic = false;
            for (int index = 0; index < wall.StructureRuntime.PieceCount; index++)
            {
                EarthPieceRuntime piece = wall.StructureRuntime.GetPieceRuntime(index);
                if (piece != null && piece.gameObject.activeSelf && !piece.Body.isKinematic)
                {
                    foundDynamic = true;
                    AssertFinite(piece.Body.position);
                    break;
                }
            }
            Assert.That(foundDynamic, Is.True);

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator CircularPhaseRepairsOnlyRequestedFractionUntilGestureContinues()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWall wall = SpawnFracturedWall(scene, 840u);
            yield return new WaitForSeconds(0.75f);
            wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 6000f);
            yield return new WaitForFixedUpdate();

            EarthReassemblyController repair = wall.Reassembly;
            Assert.That(repair.TryBeginRepair(940u, 0.25f), Is.True);
            Assert.That(repair.TargetPieceCount, Is.GreaterThan(0));
            Assert.That(repair.TargetPieceCount, Is.LessThan(repair.SelectedPieceCount));
            int initialTarget = repair.TargetPieceCount;
            Assert.That(repair.SetTargetProgress(0.50f, 941u), Is.True);
            Assert.That(repair.TargetPieceCount, Is.GreaterThan(initialTarget));
            Assert.That(repair.TargetPieceCount, Is.LessThan(repair.SelectedPieceCount));
            Assert.That(repair.IsRepairing, Is.True);
            Assert.That(wall.IsCollapsing, Is.True);

            repair.Interrupt(EarthRepairInterruptReason.Released, 942u);
            Assert.That(repair.IsRepairing, Is.False);
            Assert.That(wall.IsCollapsing, Is.True);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator CounterClockwisePhaseBreaksDeterministicFractionOfWallBonds()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWall wall = SpawnFracturedWall(scene, 850u);
            yield return new WaitForSeconds(0.75f);
            int totalBonds = wall.StructureRuntime.BondCount;

            Assert.That(wall.SetMagicDisassemblyProgress(
                0.25f, wall.transform.position, wall.transform.forward), Is.True);
            yield return new WaitForFixedUpdate();
            int quarterRemaining = wall.RemainingBondCount;
            Assert.That(quarterRemaining, Is.GreaterThan(0));
            Assert.That(quarterRemaining, Is.LessThan(totalBonds));

            wall.SetMagicDisassemblyProgress(1f, wall.transform.position, wall.transform.forward);
            yield return new WaitForFixedUpdate();
            Assert.That(wall.RemainingBondCount, Is.Zero);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTearDown]
        public IEnumerator UnloadLeakedScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        private sealed class WallRepairCapture : IDisposable
        {
            private const string Folder = "BuildReports/SequentialRepair/Wall";
            private readonly EarthWall _wall;
            private readonly Camera _camera;
            private readonly GameObject _owner, _light;
            private readonly RenderTexture _image;
            private readonly Texture2D _pixels;
            private readonly Dictionary<GameObject, int> _layers = new();
            private readonly StringBuilder _metrics = new("frame,time,stage,welded,total,active,phase,error\n");
            private int _frame;
            private readonly float _started;

            public WallRepairCapture(EarthWall wall)
            {
                _wall = wall;
                _started = Time.time;
                Directory.CreateDirectory(Folder);
                // Only this exact solid visual bounds determines framing. Never
                // include pooled environmental, trigger or debug renderers.
                Bounds bounds = wall.VisualEmergenceRoot.GetComponent<MeshRenderer>().bounds;
                AddRenderers(wall.VisualEmergenceRoot);
                for (int i = 0; i < wall.StructureRuntime.PieceCount; i++)
                    AddRenderers(wall.StructureRuntime.GetPieceRuntime(i).transform);
                _owner = new GameObject("Actual sequential wall repair evidence camera");
                _camera = _owner.AddComponent<Camera>();
                _camera.enabled = false;
                // Preview follows the existing compositing-test convention: it
                // excludes the planet atmosphere fullscreen pass from a cropped
                // isolated asset view while preserving actual rock materials.
                _camera.cameraType = CameraType.Preview;
                _camera.allowHDR = false;
                _camera.allowMSAA = false;
                UniversalAdditionalCameraData data = _camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.renderShadows = false;
                data.volumeLayerMask = 0;
                _camera.orthographic = true;
                _camera.aspect = 800f / 450f;
                _camera.nearClipPlane = .05f;
                _camera.farClipPlane = 50f;
                _camera.cullingMask = 1 << 22;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(.07f, .09f, .12f);
                Vector3 toward = (wall.transform.forward + wall.transform.right * .32f + wall.transform.up * .2f).normalized;
                _owner.transform.SetPositionAndRotation(bounds.center + toward * 15f, Quaternion.LookRotation(-toward, wall.transform.up));
                float halfWidth = 0f, halfHeight = 0f;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 point = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 delta = point - bounds.center;
                    halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(delta, _owner.transform.right)));
                    halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(delta, _owner.transform.up)));
                }
                _camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / _camera.aspect) / .7f;
                Assert.That(halfHeight / _camera.orthographicSize, Is.InRange(.35f, .8f), "Actual wall must occupy a readable frame height.");
                _light = new GameObject("Wall evidence key light");
                Light key = _light.AddComponent<Light>();
                key.type = LightType.Directional;
                key.intensity = .8f;
                key.cullingMask = 1 << 22;
                _light.transform.rotation = Quaternion.LookRotation(-toward, wall.transform.up);
                _image = new RenderTexture(800, 450, 24, RenderTextureFormat.ARGB32);
                _image.Create();
                _camera.targetTexture = _image;
                _pixels = new Texture2D(800, 450, TextureFormat.RGB24, false);
            }

            private void AddRenderers(Transform source)
            {
                foreach (MeshRenderer renderer in source.GetComponentsInChildren<MeshRenderer>(true))
                    if (!_layers.ContainsKey(renderer.gameObject))
                    { _layers.Add(renderer.gameObject, renderer.gameObject.layer); renderer.gameObject.layer = 22; }
            }

            public void Write(string stage)
            {
                RenderTexture previous = RenderTexture.active;
                try
                {
                    _camera.Render();
                    RenderTexture.active = _image;
                    _pixels.ReadPixels(new Rect(0, 0, 800, 450), 0, 0);
                    _pixels.Apply();
                    File.WriteAllBytes(Path.Combine(Folder, $"frame-{_frame:D3}.png"), _pixels.EncodeToPNG());
                    EarthReassemblyController repair = _wall.Reassembly;
                    _metrics.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:F3},{2},{3},{4},{5},{6},{7:F4}",
                        _frame++, Time.time - _started, stage, repair.WeldedPieceCount, repair.SelectedPieceCount,
                        repair.CurrentPieceIndex, repair.CurrentPiecePhase, repair.CurrentPiecePositionError));
                }
                finally { RenderTexture.active = previous; }
            }

            public void Dispose()
            {
                File.WriteAllText(Path.Combine(Folder, "Frames.csv"), _metrics.ToString());
                foreach (var entry in _layers) if (entry.Key != null) entry.Key.layer = entry.Value;
                _camera.targetTexture = null;
                _image.Release();
                Object.Destroy(_pixels); Object.Destroy(_image); Object.Destroy(_light); Object.Destroy(_owner);
            }
        }

        private static EarthWall SpawnFracturedWall(Scene scene, uint tick)
        {
            EarthWallPool pool = FindInScene<EarthWallPool>(scene);
            Assert.That(pool, Is.Not.Null);
            EarthWall wall = pool.Acquire(
                new Vector3(-2.5f, 24f, -8f),
                new Vector3(2.5f, 24f, -8f),
                Vector3.zero,
                3f,
                0.6f,
                tick);
            Assert.That(wall, Is.Not.Null);
            return wall;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T value = root.GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }

        private static void AssertFinite(Vector3 value)
        {
            Assert.That(float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z), Is.True);
        }
    }
}
