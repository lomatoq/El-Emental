using Elemental.Online.Editor;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Online.Tests
{
    public sealed class OnlineArenaIdentityTests
    {
        [Test]
        public void SceneEmbeddedMeshesHashByGeometryRatherThanTransientIdentity()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            Mesh first = null, second = null;
            try
            {
                var planet = new GameObject("Editable Voxel Planet"); SceneManager.MoveGameObjectToScene(planet, scene);
                planet.AddComponent<VoxelPlanetBehaviour>();
                var rock = new GameObject("Authored Platform"); SceneManager.MoveGameObjectToScene(rock, scene);
                var filter = rock.AddComponent<MeshFilter>();
                first = new Mesh { name = "Runtime Earth Platform", vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
                first.RecalculateNormals(); first.RecalculateBounds(); filter.sharedMesh = first;
                ulong initial = OnlineArenaIdentityAuthoring.WorldHash(scene);
                second = Object.Instantiate(first); second.name = "Equivalent separately allocated platform";
                OnlineMeshCodec.Encode(first, out int firstLength, out ulong firstGeometry);
                OnlineMeshCodec.Encode(second, out int secondLength, out ulong secondGeometry);
                Assert.That(secondLength, Is.EqualTo(firstLength));
                Assert.That(secondGeometry, Is.EqualTo(firstGeometry), "Cloning and renaming preserves the transmitted geometry.");
                filter.sharedMesh = second;
                Assert.That(OnlineArenaIdentityAuthoring.WorldHash(scene), Is.EqualTo(initial));
                second.vertices = new[] { Vector3.zero, Vector3.right * 2f, Vector3.up }; second.RecalculateBounds();
                Assert.That(OnlineArenaIdentityAuthoring.WorldHash(scene), Is.Not.EqualTo(initial));
                second.vertices = first.vertices; second.RecalculateBounds();
                second.colors32 = new[] { new Color32(1, 2, 3, 255), new Color32(4, 5, 6, 255), new Color32(7, 8, 9, 255) };
                Assert.That(OnlineArenaIdentityAuthoring.WorldHash(scene), Is.Not.EqualTo(initial), "Visible vertex surface attributes are part of compatibility.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (first != null) Object.DestroyImmediate(first);
                if (second != null) Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void WorldIdentityTracksSeedAndActualGeometryPoseButNotItsOwnStamp()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var planet = new GameObject("Editable Voxel Planet"); SceneManager.MoveGameObjectToScene(planet, scene);
                planet.AddComponent<VoxelPlanetBehaviour>();
                var rock = new GameObject("Authored Push Boulder"); SceneManager.MoveGameObjectToScene(rock, scene);
                rock.AddComponent<BoxCollider>();
                ulong initial = OnlineArenaIdentityAuthoring.WorldHash(scene);
                var owner = new GameObject("Earth Online Runtime"); SceneManager.MoveGameObjectToScene(owner, scene);
                var binding = owner.AddComponent<EarthOnlineGameplayBinding>();
                var serialized = new SerializedObject(binding); serialized.FindProperty("initialWorldHash").ulongValue = initial;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(OnlineArenaIdentityAuthoring.WorldHash(scene), Is.EqualTo(initial), "The stored hash and new online root must not hash themselves.");
                rock.transform.position = new Vector3(.1f, 3, 4);
                ulong moved = OnlineArenaIdentityAuthoring.WorldHash(scene);
                Assert.That(moved, Is.Not.EqualTo(initial));
                var planetFields = new SerializedObject(planet.GetComponent<VoxelPlanetBehaviour>());
                planetFields.FindProperty("seed").uintValue++; planetFields.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(OnlineArenaIdentityAuthoring.WorldHash(scene), Is.Not.EqualTo(moved));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
