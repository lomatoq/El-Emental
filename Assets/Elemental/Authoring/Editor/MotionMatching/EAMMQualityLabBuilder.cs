using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor.MotionMatching
{
    public static class EAMMQualityLabBuilder
    {
        private const string ScenePath = "Assets/Elemental/Tests/PlayMode/Scenes/EAMMQualityLab.unity";

        [MenuItem("Elemental/Setup/Create EAMM Quality Lab")]
        public static void Create()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject root = new GameObject("EAMM Quality Lab");
            CreateBlock(root.transform, "Flat", new Vector3(0f, -0.25f, 0f), new Vector3(8f, 0.5f, 10f), Quaternion.identity);
            CreateBlock(root.transform, "Slope 15", new Vector3(-5f, 0.45f, 3f), new Vector3(4f, 0.35f, 6f), Quaternion.Euler(15f, 0f, 0f));
            CreateBlock(root.transform, "Slope 30", new Vector3(5f, 0.9f, 3f), new Vector3(4f, 0.35f, 6f), Quaternion.Euler(30f, 0f, 0f));
            for (int i = 0; i < 5; i++)
                CreateBlock(root.transform, $"Step {i + 1}", new Vector3(-3f + i * 1.2f, i * 0.18f, -5f), new Vector3(1.2f, 0.36f + i * 0.36f, 2f), Quaternion.identity);
            CreateBlock(root.transform, "Narrow Passage Left", new Vector3(-1.15f, 1f, 5.5f), new Vector3(0.4f, 2f, 4f), Quaternion.identity);
            CreateBlock(root.transform, "Narrow Passage Right", new Vector3(1.15f, 1f, 5.5f), new Vector3(0.4f, 2f, 4f), Quaternion.identity);
            CreateBlock(root.transform, "Moving Support Authoring Marker", new Vector3(0f, 0.3f, -8f), new Vector3(3f, 0.5f, 3f), Quaternion.identity);
            GameObject seam = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            seam.name = "Convex Seam";
            seam.transform.SetParent(root.transform);
            seam.transform.SetPositionAndRotation(new Vector3(7f, -2.7f, -4f), Quaternion.identity);
            seam.transform.localScale = Vector3.one * 6f;

            GameObject lightObject = new GameObject("Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            GameObject cameraObject = new GameObject("Quality Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(13f, 11f, -15f), Quaternion.Euler(25f, -38f, 0f));
            camera.farClipPlane = 120f;

            EnsureAssetFolder("Assets/Elemental/Tests/PlayMode/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[EAMM] Generated quality lab at {ScenePath}.");
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static GameObject CreateBlock(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Quaternion rotation)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.SetPositionAndRotation(position, rotation);
            block.transform.localScale = scale;
            return block;
        }
    }
}
