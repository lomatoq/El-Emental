using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthBakedFractureRuntimeTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";

        [UnityTest]
        public IEnumerator NarrowAndWideWallsKeepTheSameBeveledSilhouetteWhenCracked()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWallPool pool = FindInScene<EarthWallPool>(scene);
            string folder = Path.GetFullPath("BuildReports/WallSilhouette");
            Directory.CreateDirectory(folder);
            GameObject cameraObject = new GameObject("Wall silhouette proof camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << 22;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.045f, .055f, .07f, 1f);
            camera.fieldOfView = 38f;
            var target = new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            try
            {
                foreach (float width in new[] { 1.8f, 8f })
                {
                    EarthWall wall = pool.Acquire(new Vector3(-width * .5f, 120f, 0f),
                        new Vector3(width * .5f, 120f, 0f), Vector3.zero, 3f, .8f, 1200u, Vector3.up);
                    foreach (Transform child in wall.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 22;
                    yield return new WaitForSeconds(1.5f);
                    camera.transform.position = wall.transform.position + wall.transform.forward * Mathf.Max(7f, width * 1.25f)
                        + wall.transform.right * width * .18f + wall.transform.up * 1.3f;
                    camera.transform.LookAt(wall.transform.position, wall.SurfaceUp);
                    Mesh intact = wall.VisualEmergenceRoot.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(intact.name, Is.EqualTo("Earth Wall Exact Cell Assembly"));
                    var expected = new HashSet<Vector3Int>();
                    foreach (Vector3 vertex in intact.vertices) expected.Add(QuantizeWallVertex(vertex));
                    string size = width < 3f ? "Narrow" : "Wide";
                    Color32[] before = CaptureWall(camera, target, Path.Combine(folder, size + "Intact.png"));
                    wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 50000f);
                    var actual = new HashSet<Vector3Int>();
                    for (int index = 0; index < wall.StructureRuntime.PieceCount; index++)
                    {
                        EarthPieceRuntime piece = wall.StructureRuntime.GetPieceRuntime(index);
                        Matrix4x4 local = wall.transform.worldToLocalMatrix * piece.transform.localToWorldMatrix;
                        foreach (Vector3 vertex in piece.GetComponent<MeshFilter>().sharedMesh.vertices)
                            actual.Add(QuantizeWallVertex(local.MultiplyPoint3x4(vertex)));
                    }
                    foreach (Vector3Int vertex in expected)
                        Assert.That(HasNearbyWallVertex(actual, vertex), Is.True,
                            "Fracture lost a visible vertex from the crest, chips or bevels.");
                    foreach (Vector3Int vertex in actual)
                        Assert.That(HasNearbyWallVertex(expected, vertex), Is.True,
                            "Fracture introduced a different visible wall surface.");
                    Color32[] after = CaptureWall(camera, target, Path.Combine(folder, size + "Cracked.png"));
                    int changed = 0;
                    for (int index = 0; index < before.Length; index++)
                    {
                        Color32 a = before[index], b = after[index];
                        if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 24) changed++;
                    }
                    Assert.That(changed / (float)before.Length, Is.LessThan(.02f),
                        "Initial cracks must not replace the visible wall with a simpler silhouette.");
                    pool.ReleaseTransient(wall);
                }
            }
            finally
            {
                camera.targetTexture = null;
                Object.Destroy(cameraObject); target.Release(); Object.Destroy(target);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static Vector3Int QuantizeWallVertex(Vector3 vertex) => new Vector3Int(
            Mathf.RoundToInt(vertex.x * 10000f), Mathf.RoundToInt(vertex.y * 10000f), Mathf.RoundToInt(vertex.z * 10000f));

        private static bool HasNearbyWallVertex(HashSet<Vector3Int> points, Vector3Int vertex)
        {
            // World -> local cancellation at a 120 m fixture can cross a
            // quantization boundary. Allow one 0.1 mm normalized bucket.
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
                if (points.Contains(vertex + new Vector3Int(x, y, z))) return true;
            return false;
        }

        private static Color32[] CaptureWall(Camera camera, RenderTexture target, string path)
        {
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.Destroy(image); }
        }

        [UnityTest]
        public IEnumerator CrackedWallKeepsFullCellsAndNeedsRepeatedHitsToDetach()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWallPool pool = FindInScene<EarthWallPool>(scene);
            EarthWall wall = pool.Acquire(new Vector3(-3f, 120f, 0f), new Vector3(3f, 120f, 0f),
                Vector3.zero, 3f, 0.8f, 900u, Vector3.up);
            yield return new WaitForSeconds(1.5f);
            Assert.That(wall.UsesBakedFracture, Is.True);
            int originalBonds = wall.RemainingBondCount;
            Assert.That(wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 50000f), Is.True);
            Assert.That(wall.RemainingBondCount, Is.EqualTo(originalBonds),
                "The initial blow should expose cracks while retaining every bond.");
            EarthStructureRuntime runtime = wall.StructureRuntime;
            float canonicalVolume = 0f, collisionVolume = 0f, renderVolume = 0f;
            var restPositions = new Vector3[runtime.PieceCount];
            for (int index = 0; index < runtime.PieceCount; index++)
            {
                EarthPieceRuntime piece = runtime.GetPieceRuntime(index);
                canonicalVolume += runtime.GetPieceDefinition(index).Volume;
                collisionVolume += MeshVolume(piece.GetComponent<MeshCollider>().sharedMesh);
                renderVolume += MeshVolume(piece.GetComponent<MeshFilter>().sharedMesh);
                restPositions[index] = wall.transform.InverseTransformPoint(piece.transform.position);
                Assert.That(wall.IsPieceStructurallySupported(index), Is.True);
                Assert.That(piece.Body.isKinematic, Is.True,
                    "Supported cells must share a rigid frame instead of a stretching joint chain.");
            }
            Assert.That(collisionVolume / canonicalVolume, Is.GreaterThanOrEqualTo(0.95f),
                "Cracking must preserve the wall's physical volume.");
            Assert.That(renderVolume / canonicalVolume, Is.GreaterThanOrEqualTo(0.85f),
                "Visible cells must fill the cracked wall, with only narrow bevel seams.");
            wall.Body.isKinematic = false;
            wall.Body.linearVelocity = wall.transform.right * 2f;
            yield return new WaitForSeconds(1f);
            Assert.That(wall.RemainingBondCount, Is.EqualTo(originalBonds),
                "Internal/seating physics must not impersonate repeated incoming blows.");
            for (int index = 0; index < runtime.PieceCount; index++)
                Assert.That(Vector3.Distance(restPositions[index], wall.transform.InverseTransformPoint(
                    runtime.GetPieceRuntime(index).transform.position)), Is.LessThan(0.05f),
                    $"Supported cell {index} fell out of its matching wall seat.");
            wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 50000f);
            Assert.That(wall.RemainingBondCount, Is.EqualTo(originalBonds));
            wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 50000f);
            Assert.That(wall.RemainingBondCount, Is.LessThan(originalBonds),
                "Repeated blows must eventually sever weakened bonds.");
            bool detached = false;
            for (int index = 0; index < runtime.PieceCount; index++)
                detached |= !wall.IsPieceStructurallySupported(index);
            Assert.That(detached, Is.True, "Broken bonds must release an unsupported island.");
            for (int index = 0; index < runtime.BondCount; index++)
                if (runtime.GetBondDefinition(index).PieceB == EarthBondGraph.WorldPieceIndex)
                    Assert.That(runtime.GetBondState(index).Phase, Is.Not.EqualTo(EarthBondPhase.Broken),
                        "Source contacts must outlast ordinary piece bonds.");
            wall.SetMagicDisassemblyProgress(1f, wall.transform.position, wall.transform.forward);
            Assert.That(wall.RemainingBondCount, Is.Zero);
            for (int index = 0; index < runtime.PieceCount; index++)
            {
                Assert.That(wall.IsPieceStructurallySupported(index), Is.False,
                    "Severed explicit source anchors must not leave immortal Foundation support.");
                Assert.That(runtime.GetPieceRuntime(index).Body.isKinematic, Is.False);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static float MeshVolume(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            double volume = 0d;
            for (int index = 0; index < triangles.Length; index += 3)
                volume += Vector3.Dot(vertices[triangles[index]], Vector3.Cross(
                    vertices[triangles[index + 1]], vertices[triangles[index + 2]])) / 6d;
            return (float)System.Math.Abs(volume);
        }

        [UnityTest]
        public IEnumerator ProductionWallUsesBakedGraphAndDoesNotDecayBondsOnTimer()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWallPool pool = FindInScene<EarthWallPool>(scene);
            Assert.That(pool, Is.Not.Null);
            Assert.That(pool.UsingBakedFractureAsset, Is.True);
            Assert.That(pool.RuntimeFallbackUsed, Is.False);

            EarthWall wall = pool.Acquire(
                new Vector3(-3f, 24f, 0f),
                new Vector3(3f, 24f, 0f),
                Vector3.zero,
                3f,
                0.6f,
                100u);
            yield return new WaitForSeconds(0.8f);
            Assert.That(wall.UsesBakedFracture, Is.True);
            Assert.That(wall.IsCollapsing, Is.False);
            MeshRenderer bakedRenderer = wall.FirstFracturePiece.GetComponent<MeshRenderer>();
            Assert.That(bakedRenderer.sharedMaterials, Has.Length.EqualTo(2));
            Assert.That(bakedRenderer.sharedMaterials[0], Is.Not.SameAs(bakedRenderer.sharedMaterials[1]));
            Assert.That(bakedRenderer.sharedMaterials[0].shader.name, Is.EqualTo("Elemental/SG Earth Master"));
            Assert.That(wall.FirstFracturePiece.GetComponent<MeshFilter>().sharedMesh.colors32,
                Has.Length.EqualTo(wall.FirstFracturePiece.GetComponent<MeshFilter>().sharedMesh.vertexCount));

            Assert.That(wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 100f), Is.True);
            var targets = new IEarthPhysicalTarget[48];
            int targetCount = wall.CopyActiveTargetsNonAlloc(targets);
            Assert.That(targetCount, Is.EqualTo(40));
            var depthBands = new System.Collections.Generic.HashSet<int>();
            for (int index = 0; index < targetCount; index++)
            {
                EarthPieceRuntime piece = targets[index] as EarthPieceRuntime;
                MeshCollider pieceCollider = piece?.GetComponent<MeshCollider>();
                Assert.That(pieceCollider, Is.Not.Null);
                Assert.That(pieceCollider.sharedMesh.bounds.size.z, Is.GreaterThan(0.035f),
                    "Every fracture cell must retain real volume through wall depth.");
                depthBands.Add(Mathf.RoundToInt(piece.transform.localPosition.z * 20f));
            }
            Assert.That(depthBands.Count, Is.GreaterThanOrEqualTo(3),
                "The wall must fracture through at least three depth layers, not use full-depth prisms.");
            for (int index = 0; index < targetCount; index++)
            {
                EarthPieceRuntime piece = targets[index] as EarthPieceRuntime;
                Assert.That(piece, Is.Not.Null);
                piece.enabled = false;
                piece.Body.detectCollisions = false;
                piece.Body.isKinematic = true;
            }
            // Flush contacts already queued by the initial proxy swap before the
            // timer-only observation window begins.
            yield return new WaitForSeconds(0.5f);
            Assert.That(wall.StructureRuntime.State.Phase,
                Is.EqualTo(EarthStructurePhase.Fractured).Or.EqualTo(EarthStructurePhase.Damaged));
            Assert.That(wall.ActiveFracturePieceCount, Is.EqualTo(40));
            int bondsAfterImpact = wall.RemainingBondCount;
            Assert.That(bondsAfterImpact, Is.GreaterThan(0));

            yield return new WaitForSeconds(3.2f);
            Assert.That(wall.RemainingBondCount, Is.EqualTo(bondsAfterImpact),
                "Baked structural bonds may change only from impacts/repair, never a decay timer.");
            Assert.That(wall.ActiveFracturePieceCount, Is.EqualTo(40));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTearDown]
        public IEnumerator UnloadLeakedEarthCoreScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator PoolReuseRestoresExactBakedPiecePoseAndProxyState()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            EarthWallPool pool = FindInScene<EarthWallPool>(scene);
            EarthWall first = pool.Acquire(
                new Vector3(-2f, 24f, -9f), new Vector3(2f, 24f, -9f),
                Vector3.zero, 2.5f, 0.55f, 200u);
            Vector3 restPosition = first.FirstFracturePiece.localPosition;
            Quaternion restRotation = first.FirstFracturePiece.localRotation;
            Vector3 restScale = first.FirstFracturePiece.localScale;
            yield return new WaitForSeconds(0.7f);
            first.ApplyRockImpact(first.transform.position, first.transform.forward, 5000f);
            yield return new WaitForFixedUpdate();
            Assert.That(first.IsCollapsing, Is.True);

            Assert.That(pool.ReleaseTransient(first), Is.True);
            EarthWall recycled = pool.Acquire(
                new Vector3(-2f, 24f, -5f), new Vector3(2f, 24f, -5f),
                Vector3.zero, 2.5f, 0.55f, 201u);

            Assert.That(recycled, Is.SameAs(first));
            Assert.That(recycled.IsCollapsing, Is.False);
            Assert.That(recycled.ActiveFracturePieceCount, Is.Zero);
            Assert.That(recycled.VisualEmergenceRoot.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(recycled.GetComponent<BoxCollider>().enabled, Is.False);
            Assert.That(recycled.StructureRuntime.State.Phase, Is.EqualTo(EarthStructurePhase.Intact));
            Assert.That(recycled.FirstFracturePiece.parent, Is.EqualTo(recycled.transform));
            Assert.That(recycled.FirstFracturePiece.localPosition, Is.EqualTo(restPosition));
            Assert.That(Quaternion.Angle(recycled.FirstFracturePiece.localRotation, restRotation), Is.LessThan(0.001f));
            Assert.That(recycled.FirstFracturePiece.localScale, Is.EqualTo(restScale));
            Rigidbody pieceBody = recycled.FirstFracturePiece.GetComponent<Rigidbody>();
            Assert.That(pieceBody.isKinematic, Is.True);
            Assert.That(pieceBody.detectCollisions, Is.False);
            Assert.That(pieceBody.linearVelocity.sqrMagnitude, Is.Zero);
            Assert.That(pieceBody.angularVelocity.sqrMagnitude, Is.Zero);

            yield return SceneManager.UnloadSceneAsync(scene);
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
    }
}
