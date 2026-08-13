using System.Collections.Generic;
using Elemental.Authoring.Assets;
using Elemental.Authoring.Bakers;
using Elemental.Input.Gestures;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Fields;
using Elemental.Simulation.Magic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class M5WindLabSetup
    {
        public const string WindLabScenePath = "Assets/Elemental/Content/Scenes/WindLab.unity";
        private const string AbilityFolder = "Assets/Elemental/Content/Abilities/";
        private const string WindMaterialPath = "Assets/Elemental/Content/Materials/WindDebug.mat";

        [MenuItem("Elemental/Setup/Create M5 Wind Lab")]
        public static void Configure()
        {
            M4CharacterFeelSetup.Configure();
            Scene scene = SceneManager.GetActiveScene();
            GameObject planet = GameObject.Find("Primitive Planet");
            GameObject character = GameObject.Find("Planet Character");
            Camera camera = Object.FindAnyObjectByType<Camera>();
            GravityWorldBehaviour gravityWorld = Object.FindAnyObjectByType<GravityWorldBehaviour>();
            if (planet == null || character == null || camera == null || gravityWorld == null)
            {
                throw new UnityEditor.Build.BuildFailedException("M4 dependencies are missing for Wind Lab.");
            }

            GameObject runtimeRoot = new GameObject("Air Field Runtime");
            runtimeRoot.SetActive(false);
            FieldWorldBehaviour fieldWorld = runtimeRoot.AddComponent<FieldWorldBehaviour>();
            fieldWorld.Configure(64, 24, 20f, 24);
            AirMagicExecutor executor = runtimeRoot.AddComponent<AirMagicExecutor>();
            executor.Configure(fieldWorld);
            AirAbilityRegistryBootstrap registry = runtimeRoot.AddComponent<AirAbilityRegistryBootstrap>();
            registry.Configure(executor, CreateOrLoadRecipes());
            runtimeRoot.SetActive(true);

            character.SetActive(false);
            PlayerInput playerInput = character.GetComponent<PlayerInput>();
            LineRenderer preview = character.GetComponent<LineRenderer>();
            if (preview == null)
            {
                preview = character.AddComponent<LineRenderer>();
            }
            preview.useWorldSpace = true;
            preview.loop = false;
            preview.widthMultiplier = 0.09f;
            preview.sharedMaterial = CreateOrLoadWindMaterial();
            preview.positionCount = 0;
            MagicInputController input = character.GetComponent<MagicInputController>();
            if (input == null)
            {
                input = character.AddComponent<MagicInputController>();
            }
            input.ConfigureAir(playerInput, camera, executor, planet.GetComponent<Collider>(), preview);
            character.SetActive(true);

            AddAerodynamicsToExistingBodies(fieldWorld, gravityWorld);
            Rigidbody[] projectiles = CreateProjectiles(fieldWorld, gravityWorld);
            CreateLightDebris(fieldWorld, gravityWorld);
            CreateOccluders();
            CreateVisualization(fieldWorld);

            GameObject driverObject = new GameObject("Wind Lab Driver");
            WindLabDriver driver = driverObject.AddComponent<WindLabDriver>();
            driver.Configure(executor, projectiles, planet.transform);

            EditorSceneManager.SaveScene(scene, WindLabScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Elemental] M5 Wind Lab configured. Keys 1-4 select Gust, Vortex, Lift, and Air Brake.");
        }

        private static AbilityRecipeAsset[] CreateOrLoadRecipes()
        {
            return new[]
            {
                CreateOrLoadRecipe("AirGustCorridor.asset", AirAbilityIds.GustCorridor, MagicGeometryKind.Direction, 2.5f, 16f),
                CreateOrLoadRecipe("AirVortex.asset", AirAbilityIds.Vortex, MagicGeometryKind.AnchorSphere, 5f, 14f),
                CreateOrLoadRecipe("AirLiftColumn.asset", AirAbilityIds.LiftColumn, MagicGeometryKind.Direction, 3f, 18f),
                CreateOrLoadRecipe("AirBrake.asset", AirAbilityIds.AirBrake, MagicGeometryKind.AnchorSphere, 5f, 7f)
            };
        }

        private static AbilityRecipeAsset CreateOrLoadRecipe(
            string fileName,
            AbilityId id,
            MagicGeometryKind geometry,
            float radius,
            float strength)
        {
            string path = AbilityFolder + fileName;
            AbilityRecipeAsset asset = AssetDatabase.LoadAssetAtPath<AbilityRecipeAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AbilityRecipeAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Configure(
                id,
                MagicSelectorKind.PlanetSurface,
                geometry,
                new[] { MagicOperatorKind.SpawnField },
                radius,
                strength);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void AddAerodynamicsToExistingBodies(FieldWorldBehaviour fieldWorld, GravityWorldBehaviour gravityWorld)
        {
            Rigidbody[] bodies = Object.FindObjectsByType<Rigidbody>();
            for (int index = 0; index < bodies.Length; index++)
            {
                Rigidbody body = bodies[index];
                if (body == null || body.isKinematic)
                {
                    continue;
                }
                AirFieldBody airBody = body.GetComponent<AirFieldBody>();
                if (airBody == null)
                {
                    airBody = body.gameObject.AddComponent<AirFieldBody>();
                }
                float area = body.GetComponent<ActiveRagdollJoint>() != null ? 0.45f : 0.8f;
                airBody.Configure(fieldWorld, body, gravityWorld, area, 0.85f, 0.12f, 40f);
            }
        }

        private static Rigidbody[] CreateProjectiles(FieldWorldBehaviour fieldWorld, GravityWorldBehaviour gravityWorld)
        {
            const int count = 12;
            var bodies = new Rigidbody[count];
            for (int index = 0; index < count; index++)
            {
                GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectile.name = $"Wind Projectile {index + 1:00}";
                projectile.transform.position = new Vector3(-10f, 24f + (index % 4), -3f + ((index / 4) * 2f));
                projectile.transform.localScale = Vector3.one * 0.38f;
                Rigidbody body = projectile.AddComponent<Rigidbody>();
                body.mass = 0.8f;
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                projectile.AddComponent<GravityBody>().Configure(gravityWorld, body);
                projectile.AddComponent<AirFieldBody>().Configure(fieldWorld, body, gravityWorld, 0.35f, 1.1f, 0.25f, 48f);
                bodies[index] = body;
            }
            return bodies;
        }

        private static void CreateLightDebris(FieldWorldBehaviour fieldWorld, GravityWorldBehaviour gravityWorld)
        {
            const int count = 48;
            for (int index = 0; index < count; index++)
            {
                GameObject debris = GameObject.CreatePrimitive(index % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                debris.name = $"Light Debris {index + 1:00}";
                float angle = index * Mathf.PI * 2f / count;
                debris.transform.position = new Vector3(Mathf.Cos(angle) * 9f, 25f + (index % 6), Mathf.Sin(angle) * 9f);
                debris.transform.localScale = Vector3.one * Mathf.Lerp(0.18f, 0.45f, (index % 7) / 6f);
                Rigidbody body = debris.AddComponent<Rigidbody>();
                body.mass = Mathf.Lerp(0.12f, 0.6f, (index % 5) / 4f);
                body.useGravity = false;
                debris.AddComponent<GravityBody>().Configure(gravityWorld, body);
                debris.AddComponent<AirFieldBody>().Configure(fieldWorld, body, gravityWorld, 0.55f, 1.35f, 0.1f, 55f);
            }
        }

        private static void CreateOccluders()
        {
            for (int index = 0; index < 4; index++)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wind Occluder {index + 1:00}";
                wall.transform.position = new Vector3(-6f + (index * 4f), 25f, 3.5f);
                wall.transform.localScale = new Vector3(1.2f, 4f, 0.8f);
                wall.isStatic = true;
            }
        }

        private static void CreateVisualization(FieldWorldBehaviour fieldWorld)
        {
            GameObject root = new GameObject("Air Field Presentation");
            Material material = CreateOrLoadWindMaterial();
            var traces = new LineRenderer[12];
            for (int index = 0; index < traces.Length; index++)
            {
                GameObject traceObject = new GameObject($"Field Trace {index + 1:00}");
                traceObject.transform.SetParent(root.transform, false);
                LineRenderer trace = traceObject.AddComponent<LineRenderer>();
                trace.useWorldSpace = true;
                trace.widthMultiplier = 0.1f;
                trace.sharedMaterial = material;
                trace.positionCount = 0;
                traces[index] = trace;
            }

            GameObject smokeObject = new GameObject("Wind Smoke Tracers");
            smokeObject.transform.SetParent(root.transform, false);
            smokeObject.transform.position = new Vector3(0f, 26f, 0f);
            ParticleSystem smoke = smokeObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = smoke.main;
            main.loop = true;
            main.startLifetime = 2.5f;
            main.startSpeed = 1.5f;
            main.startSize = 0.12f;
            main.maxParticles = 256;
            ParticleSystem.ShapeModule shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 9f;
            smokeObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            AirFieldVisualizer visualizer = root.AddComponent<AirFieldVisualizer>();
            visualizer.Configure(fieldWorld, traces, smoke);
        }

        private static Material CreateOrLoadWindMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(WindMaterialPath);
            if (material != null)
            {
                return material;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                throw new UnityEditor.Build.BuildFailedException("URP Unlit shader was not found.");
            }
            material = new Material(shader)
            {
                name = "Wind Debug",
                color = new Color(0.25f, 0.85f, 1f, 0.72f)
            };
            AssetDatabase.CreateAsset(material, WindMaterialPath);
            return material;
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(item => item.path == WindLabScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(WindLabScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
