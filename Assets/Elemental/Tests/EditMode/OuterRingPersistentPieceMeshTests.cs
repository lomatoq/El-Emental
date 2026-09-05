using Elemental.Authoring.Fracture;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Tests.EditMode
{
    public sealed class OuterRingPersistentPieceMeshTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";

        [Test]
        public void SavedOuterRingPiecesReferencePersistentSourceMeshes()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject ring = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.name == "Outer Stone Ring") { ring = root; break; }
                Assert.That(ring, Is.Not.Null);
                int count = 0;
                foreach (EarthArenaStructure structure in ring.GetComponentsInChildren<EarthArenaStructure>(true))
                {
                    var serialized = new SerializedObject(structure);
                    var asset = serialized.FindProperty("fractureAssetObject").objectReferenceValue as EarthFractureAsset;
                    SerializedProperty pieces = serialized.FindProperty("pieces");
                    Assert.That(asset, Is.Not.Null, structure.name);
                    Assert.That(pieces.arraySize, Is.EqualTo(asset.PieceCount), structure.name);
                    for (int index = 0; index < pieces.arraySize; index++)
                    {
                        var piece = pieces.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                        Mesh actual = piece.GetComponent<MeshFilter>().sharedMesh;
                        Assert.That(actual, Is.SameAs(asset.GetPieceRenderMesh(index)), piece.name);
                        Assert.That(AssetDatabase.GetAssetPath(actual), Is.Not.Empty, piece.name);
                        count++;
                    }
                }
                Assert.That(count, Is.EqualTo(85));
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
