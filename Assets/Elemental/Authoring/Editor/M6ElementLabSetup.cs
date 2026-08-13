using System.Collections.Generic;
using Elemental.Authoring.Assets;
using Elemental.Authoring.Bakers;
using Elemental.Input.Gestures;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Materials;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Elemental.Authoring.Editor
{
    public static class M6ElementLabSetup
    {
        public const string ElementLabScenePath = "Assets/Elemental/Content/Scenes/ElementLab.unity";
        private const string AbilityFolder = "Assets/Elemental/Content/Abilities/";
        private const string MaterialFolder = "Assets/Elemental/Content/Materials/";
        private const string PanelSettingsPath = "Assets/Elemental/Content/UI/ElementLabPanelSettings.asset";

        [MenuItem("Elemental/Setup/Create M6 Element Lab")]
        public static void Configure()
        {
            M5WindLabSetup.Configure();
            Scene scene = SceneManager.GetActiveScene();
            GameObject planet = GameObject.Find("Primitive Planet");
            GameObject character = GameObject.Find("Planet Character");
            Camera camera = Object.FindAnyObjectByType<Camera>();
            GravityWorldBehaviour gravity = Object.FindAnyObjectByType<GravityWorldBehaviour>();
            if (planet == null || character == null || camera == null || gravity == null)
            {
                throw new UnityEditor.Build.BuildFailedException("M5 dependencies are missing for Element Lab.");
            }

            GameObject root = new GameObject("Thermal Water Runtime");
            root.SetActive(false);
            ThermalWaterWorldBehaviour world = root.AddComponent<ThermalWaterWorldBehaviour>();
            world.Configure(64, 64, 16, 10f, 16);
            ThermalWaterMagicExecutor executor = root.AddComponent<ThermalWaterMagicExecutor>();
            executor.Configure(world);
            ThermalWaterAbilityRegistryBootstrap registry = root.AddComponent<ThermalWaterAbilityRegistryBootstrap>();
            registry.Configure(executor, CreateOrLoadRecipes());
            WaterVolume[] volumes = RegisterInitialWater(world);
            WaterVolumeBootstrap waterBootstrap = root.AddComponent<WaterVolumeBootstrap>();
            waterBootstrap.Configure(world, volumes);
            root.SetActive(true);

            Material liquid = CreateOrLoadMaterial("WaterLiquid.mat", new Color(0.08f, 0.45f, 0.9f, 0.82f));
            Material ice = CreateOrLoadMaterial("WaterIce.mat", new Color(0.55f, 0.92f, 1f, 0.9f));
            Material steam = CreateOrLoadMaterial("WaterSteam.mat", new Color(0.86f, 0.92f, 0.96f, 0.45f));
            for (int index = 0; index < volumes.Length; index++)
            {
                CreateWaterPresentation(world, volumes[index], liquid, ice, steam);
                CreateIceCollision(world, volumes[index]);
            }

            character.SetActive(false);
            PlayerInput playerInput = character.GetComponent<PlayerInput>();
            LineRenderer preview = character.GetComponent<LineRenderer>();
            if (preview == null) preview = character.AddComponent<LineRenderer>();
            preview.useWorldSpace = true;
            preview.widthMultiplier = 0.1f;
            preview.sharedMaterial = liquid;
            preview.positionCount = 0;
            MagicInputController input = character.GetComponent<MagicInputController>();
            if (input == null) input = character.AddComponent<MagicInputController>();
            input.ConfigureThermalWater(playerInput, camera, executor, planet.GetComponent<Collider>(), preview, ElementId.Water);
            character.SetActive(true);

            Transform reactionTarget = CreateReactionTarget(executor, gravity);
            CreateReactionFeedback(executor, steam);
            CreateHud(world);
            GameObject driverObject = new GameObject("Element Lab Driver");
            ElementLabDriver driver = driverObject.AddComponent<ElementLabDriver>();
            driver.Configure(executor, world, reactionTarget);

            EditorSceneManager.SaveScene(scene, ElementLabScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Elemental] M6 Element Lab configured. R/F choose Water/Fire; digits 1-4 choose abilities.");
        }

        private static AbilityRecipeAsset[] CreateOrLoadRecipes()
        {
            return new[]
            {
                Recipe("HeatJet.asset", FireAbilityIds.HeatJet, MagicOperatorKind.AddHeat, 2f, 90f),
                Recipe("ThermalFocus.asset", FireAbilityIds.ThermalFocus, MagicOperatorKind.AddHeat, 3f, 240f),
                Recipe("GatherWater.asset", WaterAbilityIds.GatherWater, MagicOperatorKind.TransferMass, 4f, 8f),
                Recipe("WaterJet.asset", WaterAbilityIds.WaterJet, MagicOperatorKind.ApplyPressureImpulse, 2f, 25f),
                Recipe("FreezeBridge.asset", WaterAbilityIds.FreezeBridge, MagicOperatorKind.Freeze, 4f, 450f),
                Recipe("SteamBurst.asset", WaterAbilityIds.SteamBurst, MagicOperatorKind.Vaporize, 5f, 2800f)
            };
        }

        private static AbilityRecipeAsset Recipe(string file, AbilityId id, MagicOperatorKind operation, float radius, float strength)
        {
            string path = AbilityFolder + file;
            AbilityRecipeAsset asset = AssetDatabase.LoadAssetAtPath<AbilityRecipeAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AbilityRecipeAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Configure(id, MagicSelectorKind.PlanetSurface, MagicGeometryKind.Direction, new[] { operation }, radius, strength);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static WaterVolume[] RegisterInitialWater(ThermalWaterWorldBehaviour world)
        {
            Elemental.Simulation.Materials.MaterialDefinition water =
                Elemental.Simulation.Materials.MaterialDefinition.Water;
            var volumes = new[]
            {
                new WaterVolume(new WaterVolumeId(1), 91u, new float3(0f, 27f, 6f), float3.zero, 1.25f,
                    new PhaseState(water.Id, PhaseKind.Liquid, 20f, 4f)),
                new WaterVolume(new WaterVolumeId(2), 91u, new float3(-6f, 25f, 8f), float3.zero, 1.5f,
                    new PhaseState(water.Id, PhaseKind.Liquid, 12f, 7f)),
                new WaterVolume(new WaterVolumeId(3), 91u, new float3(6f, 25f, 8f), float3.zero, 1f,
                    new PhaseState(water.Id, PhaseKind.Liquid, 65f, 3f))
            };
            for (int index = 0; index < volumes.Length; index++) world.Water.Register(in volumes[index]);
            return volumes;
        }

        private static void CreateWaterPresentation(
            ThermalWaterWorldBehaviour world,
            WaterVolume volume,
            Material liquid,
            Material ice,
            Material steam)
        {
            GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proxy.name = $"Water Volume {volume.Id.Value:00} Visual";
            Object.DestroyImmediate(proxy.GetComponent<Collider>());
            MeshRenderer renderer = proxy.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = liquid;
            GameObject particlesObject = new GameObject("Fluid Ribbon Spray Steam Proxy");
            particlesObject.transform.SetParent(proxy.transform, false);
            ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true; main.startLifetime = 1.8f; main.startSpeed = 2.2f; main.startSize = 0.12f; main.maxParticles = 180;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 18f; shape.radius = 0.25f;
            particlesObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = steam;
            WaterVolumeVisualProxy visual = proxy.AddComponent<WaterVolumeVisualProxy>();
            visual.Configure(world, volume.Id, renderer, particles, liquid, ice, steam);
        }

        private static void CreateIceCollision(ThermalWaterWorldBehaviour world, WaterVolume volume)
        {
            GameObject collision = new GameObject($"Ice Bridge {volume.Id.Value:00} Collision");
            collision.transform.position = new Vector3(volume.Center.x, volume.Center.y, volume.Center.z);
            BoxCollider box = collision.AddComponent<BoxCollider>();
            box.enabled = false;
            WaterPhaseCollider phaseCollider = collision.AddComponent<WaterPhaseCollider>();
            phaseCollider.Configure(world, volume.Id, box);
        }

        private static Transform CreateReactionTarget(ThermalWaterMagicExecutor executor, GravityWorldBehaviour gravity)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Hot Brittle Thermal Shock Target";
            target.transform.position = new Vector3(3f, 28f, 6f);
            target.transform.localScale = Vector3.one * 1.4f;
            Rigidbody body = target.AddComponent<Rigidbody>();
            body.mass = 15f; body.useGravity = false;
            target.AddComponent<GravityBody>().Configure(gravity, body);
            ReactionImpulseBody reaction = target.AddComponent<ReactionImpulseBody>();
            target.SetActive(false);
            reaction.Configure(executor, body, 30f);
            target.SetActive(true);
            return target.transform;
        }

        private static void CreateReactionFeedback(ThermalWaterMagicExecutor executor, Material steam)
        {
            GameObject feedbackObject = new GameObject("Thermal Reaction Presentation");
            feedbackObject.SetActive(false);
            ParticleSystem particles = feedbackObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false; main.startLifetime = 1.2f; main.startSpeed = 5f; main.startSize = 0.25f;
            feedbackObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = steam;
            GameObject lightObject = new GameObject("Heat Distortion Light Proxy");
            lightObject.transform.SetParent(feedbackObject.transform, false);
            Light heatLight = lightObject.AddComponent<Light>();
            heatLight.color = new Color(1f, 0.35f, 0.08f); heatLight.range = 8f; heatLight.intensity = 0.5f;
            ThermalReactionFeedback feedback = feedbackObject.AddComponent<ThermalReactionFeedback>();
            feedback.Configure(executor, particles, heatLight);
            feedbackObject.SetActive(true);
        }

        private static void CreateHud(ThermalWaterWorldBehaviour world)
        {
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.name = "Element Lab Panel Settings";
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1920, 1080);
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Elemental/Content/UI/ElementLabHud.uxml");
            if (tree == null) throw new UnityEditor.Build.BuildFailedException("ElementLabHud.uxml was not imported.");
            GameObject hudObject = new GameObject("Element Lab UI Toolkit HUD");
            hudObject.SetActive(false);
            UIDocument document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = panel; document.visualTreeAsset = tree; document.sortingOrder = 20;
            ElementLabHud hud = hudObject.AddComponent<ElementLabHud>();
            hud.Configure(world);
            hudObject.SetActive(true);
        }

        private static Material CreateOrLoadMaterial(string fileName, Color color)
        {
            string path = MaterialFolder + fileName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new UnityEditor.Build.BuildFailedException("URP Unlit shader was not found.");
            material = new Material(shader) { name = fileName.Replace(".mat", string.Empty), color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(item => item.path == ElementLabScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ElementLabScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
