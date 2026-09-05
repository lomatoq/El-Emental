using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public class EarthArmorHeadRuntimeTests
    {
        [System.Serializable] private class Report
        {
            public bool passed;
            public int measuredVertices, coveredViews, headPieces, expandedFillers, launchedFillers;
            public float maximumFollowError;
            public double maximumCompactFollowMilliseconds;
        }

        [UnityTest]
        public IEnumerator ProductionArmorCoversHeadAndFollowsItsRotation()
        {
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(path);
            var report = new Report();
            var probes = new List<GameObject>();
            AsyncOperation unload = null;
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Armor.CompactFollow", 64);
            try
            {
                foreach (var bot in All<EarthMvpBotController>(scene)) bot.enabled = false;
                yield return null;
                var armor = All<EarthArmorController>(scene).First(x => x.name == "Planet Character");
                var motor = armor.GetComponent<PlanetMotor>();
                var animator = armor.GetComponentInChildren<Animator>();
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                Assert.That(armor.Begin(), Is.True);
                yield return new WaitForSeconds(.8f);
                yield return new WaitForEndOfFrame();
                Assert.That(EarthArmorHeadSurface.TryMeasure(animator, out var vertices, out var center), Is.True);
                report.measuredVertices = vertices.Length;
                Assert.That(vertices.Length, Is.GreaterThan(100));
                Assert.That(Vector3.Distance(center,head.position), Is.InRange(.2f,.9f), "Head skin was transformed with the imported scale twice.");
                var pieces = new EarthArmorPiece[EarthArmorProfile.MaximumPieceCount];
                Assert.That(armor.CopyActivePiecesNonAlloc(pieces), Is.EqualTo(EarthArmorProfile.MaximumPieceCount));
                var headIndices = Enumerable.Range(0,18).Concat(Enumerable.Range(EarthArmorProfile.DefaultPieceCount,EarthArmorHeadShell.FillerCount)).ToArray();
                var colliders = new List<MeshCollider>();
                // The production definition allocates its first eighteen stones to the head.
                foreach (int i in headIndices)
                {
                    var piece = pieces[i];
                    Assert.That(piece.GetComponent<MeshRenderer>().enabled, Is.True);
                    Assert.That(piece.IsReleased, Is.False);
                    Assert.That(piece.Body.isKinematic, Is.True);
                    Assert.That(piece.CameraSuppressed, Is.False);
                    var probe = new GameObject("Head render mesh raycast probe");
                    probes.Add(probe);
                    probe.transform.SetPositionAndRotation(piece.transform.position, piece.transform.rotation);
                    probe.transform.localScale = piece.transform.lossyScale;
                    var collider = probe.AddComponent<MeshCollider>();
                    collider.sharedMesh = piece.GetComponent<MeshFilter>().sharedMesh;
                    colliders.Add(collider);
                    report.headPieces++;
                }
                Vector3 up = motor.LocalUp, forward = motor.FacingForward;
                Vector3 right = Vector3.Cross(up, forward).normalized;
                Capture(center, forward, up, "Front");
                Capture(center, -forward, up, "Back");
                foreach (var direction in new[] {up,forward,-forward,right,-right})
                {
                    // Require a visible stone outside the real head, not merely a
                    // nearby anchor/collider hidden inside the skull.
                    var ray = new Ray(center + direction * 2f, -direction);
                    float nearest = float.MaxValue;
                    foreach (var collider in colliders)
                        if (collider.Raycast(ray, out var hit, 2f)) nearest = Mathf.Min(nearest, hit.distance);
                    float support = 0f;
                    foreach (var vertex in vertices) support = Mathf.Max(support,Vector3.Dot((Vector3)vertex-center,direction));
                    Assert.That(nearest, Is.LessThan(2f-support+.04f), "Head exposed from " + direction);
                    report.coveredViews++;
                }
                // Freeze pose evaluation only for this controlled head-motion check.
                foreach (var collider in colliders) collider.enabled = false;
                animator.enabled = false;
                var localPoints = headIndices.Select(i => head.InverseTransformPoint(pieces[i].transform.position)).ToArray();
                head.rotation = Quaternion.AngleAxis(40f, up) * Quaternion.AngleAxis(15f, right) * head.rotation;
                yield return null;
                yield return new WaitForEndOfFrame();
                for (int i = 0; i < headIndices.Length; i++)
                    report.maximumFollowError = Mathf.Max(report.maximumFollowError,
                        Vector3.Distance(head.TransformPoint(localPoints[i]), pieces[headIndices[i]].transform.position));
                Assert.That(report.maximumFollowError, Is.LessThan(.003f));
                if (recorder.Valid) report.maximumCompactFollowMilliseconds = recorder.LastValue / 1000000d;
                var fillerPositions = pieces.Skip(EarthArmorProfile.DefaultPieceCount).Select(x => x.transform.position).ToArray();
                for (int step = 0; step < 8; step++) armor.ApplyWheel(120f,Time.unscaledTime);
                yield return new WaitForSeconds(.8f);
                for (int i = 0; i < EarthArmorHeadShell.FillerCount; i++)
                {
                    var piece = pieces[EarthArmorProfile.DefaultPieceCount+i];
                    Assert.That(piece.IsReleased, Is.False);
                    Assert.That(Vector3.Distance(piece.transform.position,fillerPositions[i]),Is.GreaterThan(.7f));
                    Assert.That(piece.transform.localScale.x, Is.LessThan(.5f));
                    report.expandedFillers++;
                }
                Assert.That(armor.FireAll(up), Is.EqualTo(EarthArmorProfile.MaximumPieceCount));
                for (int tick = 0; tick < 10; tick++) yield return new WaitForFixedUpdate();
                foreach (var piece in pieces.Skip(EarthArmorProfile.DefaultPieceCount))
                {
                    Assert.That(piece.IsReleased, Is.True);
                    Assert.That(piece.IsPhysical, Is.True);
                    report.launchedFillers++;
                }
                report.passed = true;
            }
            finally
            {
                foreach (var probe in probes) Object.Destroy(probe);
                Directory.CreateDirectory("BuildReports/HeadArmor");
                File.WriteAllText("BuildReports/HeadArmor/Latest.json",JsonUtility.ToJson(report,true));
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }

        private static void Capture(Vector3 center, Vector3 direction, Vector3 up, string name)
        {
            var go = new GameObject("Head armor evidence camera");
            var camera = go.AddComponent<Camera>();
            var target = new RenderTexture(768,768,24);
            var image = new Texture2D(768,768,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            try
            {
                if (Camera.main != null) camera.CopyFrom(Camera.main);
                camera.enabled = false;
                camera.targetTexture = target;
                camera.orthographic = true;
                camera.orthographicSize = 1f;
                camera.nearClipPlane = .01f;
                go.transform.SetPositionAndRotation(center + direction * 2.8f,Quaternion.LookRotation(-direction,up));
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,768,768),0,0);
                image.Apply();
                Directory.CreateDirectory("BuildReports/HeadArmor");
                File.WriteAllBytes("BuildReports/HeadArmor/" + name + ".png",image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.Destroy(image); target.Release(); Object.Destroy(target); Object.Destroy(go);
            }
        }

        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();
    }
}
