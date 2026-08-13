using System.Collections.Generic;
using Elemental.Presentation.UI;
using Elemental.Runtime.Missions;
using Elemental.Runtime.World;
using Elemental.Simulation.Missions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Elemental.Authoring.Editor
{
    public static class M7VolcanoVillageSetup
    {
        public const string ScenePath = "Assets/Elemental/Content/Scenes/VolcanoVillage.unity";
        private const string PanelPath = "Assets/Elemental/Content/UI/MissionPanelSettings.asset";

        [MenuItem("Elemental/Setup/Create M7 Volcano Village")]
        public static void Configure()
        {
            M6ElementLabSetup.Configure();
            Scene scene = SceneManager.GetActiveScene();
            GameObject planet = GameObject.Find("Primitive Planet");
            if (planet == null) throw new UnityEditor.Build.BuildFailedException("M6 dependencies are missing.");

            Transform routeAStart = Marker("Route A Start", new Vector3(-8f, 24f, -5f));
            Transform routeAEnd = Marker("Route A Safe Zone", new Vector3(-3f, 29f, 8f));
            Transform routeBStart = Marker("Route B Start", new Vector3(8f, 24f, -5f));
            Transform routeBEnd = Marker("Route B Safe Zone", new Vector3(3f, 29f, 8f));
            CreateSettlement();
            CreateLavaFront();
            CivilianProxyBehaviour[] civilians = CreateCivilians(routeAStart, routeAEnd, routeBStart, routeBEnd);
            CrisisPresentationPool presentation = CreateCrisisPool();
            GameObject directorObject = new GameObject("Volcano Village Mission Director");
            MissionDirectorBehaviour director = directorObject.AddComponent<MissionDirectorBehaviour>();
            director.Configure(0xC0FFEEu, MissionStrategyKind.EarthFortify, civilians, presentation);
            CreateDestructibleRouteBlocks(director);
            CreateHud(director);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Elemental] M7 Volcano Village configured with two routes and three strategy controls.");
        }

        private static Transform Marker(string name, Vector3 position)
        {
            GameObject marker = new GameObject(name); marker.transform.position = position; return marker.transform;
        }

        private static void CreateSettlement()
        {
            for (int index = 0; index < 14; index++)
            {
                float angle = index * Mathf.PI * 2f / 14f;
                GameObject house = GameObject.CreatePrimitive(PrimitiveType.Cube);
                house.name = $"Village Structure {index + 1:00}";
                house.transform.position = new Vector3(Mathf.Cos(angle) * 8f, 25f + (index % 3), Mathf.Sin(angle) * 8f);
                house.transform.localScale = new Vector3(1.3f, 1.8f + (index % 2), 1.3f);
                Rigidbody body = house.AddComponent<Rigidbody>(); body.mass = 40f; body.isKinematic = true;
            }
            for (int index = 0; index < 2; index++)
            {
                GameObject route = GameObject.CreatePrimitive(PrimitiveType.Cube);
                route.name = $"Viable Evacuation Route {index + 1}";
                route.transform.position = new Vector3(index == 0 ? -5f : 5f, 25.5f, 2f);
                route.transform.rotation = Quaternion.Euler(0f, index == 0 ? -25f : 25f, 0f);
                route.transform.localScale = new Vector3(2f, 0.25f, 14f);
                route.isStatic = true;
            }
        }

        private static void CreateLavaFront()
        {
            GameObject lava = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lava.name = "Bounded Lava Advance Primitive";
            lava.transform.position = new Vector3(0f, 21f, -10f);
            lava.transform.localScale = new Vector3(9f, 1f, 4f);
            Material lavaMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            { name = "Lava Debug", color = new Color(1f, 0.08f, 0.01f) };
            lava.GetComponent<MeshRenderer>().sharedMaterial = lavaMaterial;
            Object.DestroyImmediate(lava.GetComponent<Collider>());
        }

        private static void CreateDestructibleRouteBlocks(MissionDirectorBehaviour director)
        {
            for (int index = 0; index < 6; index++)
            {
                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Destructible Route Block {index + 1:00}";
                block.transform.position = new Vector3(-2.5f + index, 26f, 1.5f + (index % 2));
                block.transform.localScale = Vector3.one * 0.9f;
                Rigidbody body = block.AddComponent<Rigidbody>(); body.mass = 6f;
                MissionTerrainLever lever = block.AddComponent<MissionTerrainLever>();
                lever.Configure(director, body, index < 3, index >= 3, 5f);
            }
        }

        private static CivilianProxyBehaviour[] CreateCivilians(Transform aStart, Transform aEnd, Transform bStart, Transform bEnd)
        {
            var civilians = new CivilianProxyBehaviour[12];
            for (int index = 0; index < civilians.Length; index++)
            {
                GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                proxy.name = $"Civilian Proxy {index + 1:00}";
                proxy.transform.position = index % 2 == 0 ? aStart.position : bStart.position;
                proxy.transform.localScale = new Vector3(0.45f, 0.65f, 0.45f);
                Object.DestroyImmediate(proxy.GetComponent<Collider>());
                CivilianProxyBehaviour view = proxy.AddComponent<CivilianProxyBehaviour>();
                view.Configure(aStart, aEnd, bStart, bEnd, proxy.GetComponent<MeshRenderer>());
                civilians[index] = view;
            }
            return civilians;
        }

        private static CrisisPresentationPool CreateCrisisPool()
        {
            GameObject root = new GameObject("Pooled Crisis Presentation");
            var effects = new ParticleSystem[12];
            for (int index = 0; index < effects.Length; index++)
            {
                GameObject effectObject = new GameObject($"Crisis FX {index + 1:00}");
                effectObject.transform.SetParent(root.transform, false);
                ParticleSystem effect = effectObject.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = effect.main;
                main.playOnAwake = false; main.startLifetime = 2f; main.startSpeed = 2.5f; main.startSize = 0.35f;
                effects[index] = effect;
            }
            CrisisPresentationPool pool = root.AddComponent<CrisisPresentationPool>();
            pool.Configure(effects); return pool;
        }

        private static void CreateHud(MissionDirectorBehaviour director)
        {
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>(); panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1920, 1080); AssetDatabase.CreateAsset(panel, PanelPath);
            }
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Elemental/Content/UI/MissionHud.uxml");
            GameObject hudObject = new GameObject("Volcano Village UI Toolkit HUD"); hudObject.SetActive(false);
            UIDocument document = hudObject.AddComponent<UIDocument>(); document.panelSettings = panel; document.visualTreeAsset = tree;
            MissionHud hud = hudObject.AddComponent<MissionHud>(); hud.Configure(director); hudObject.SetActive(true);
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(item => item.path == ScenePath))
            { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
        }
    }
}
