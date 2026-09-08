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
