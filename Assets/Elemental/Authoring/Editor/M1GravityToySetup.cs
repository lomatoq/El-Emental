using System;
using Elemental.Input.Actions;
using Elemental.Presentation.Camera;
using Elemental.Runtime.Bootstrap;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Gravity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class M1GravityToySetup
    {
        public const string GravityToyScenePath = "Assets/Elemental/Content/Scenes/GravityToy.unity";
        private const string GameplayActionsPath = "Assets/Elemental/Input/Actions/Gameplay.inputactions";

        [MenuItem("Elemental/Setup/Create M1 Gravity Toy")]
        public static void Configure()
        {
            Configure(M2VoxelPlanetSetup.CreateOrLoadWorldProfile());
        }

        internal static void Configure(PlanetWorldProfile worldProfile)
        {
            if (worldProfile == null)
                throw new UnityEditor.Build.BuildFailedException("PlanetWorldProfile is required to build a world scene.");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float radius = worldProfile.Radius;
            float gravity = worldProfile.SurfaceGravity;

            GameObject worldObject = new GameObject("Gravity World");
            worldObject.SetActive(false);
            worldObject.AddComponent<WorldBootstrap>();
            PointPlanetGravitySource source = worldObject.AddComponent<PointPlanetGravitySource>();
            source.Configure(new GravityFieldId(1u), radius, gravity, Mathf.Max(1f, radius / 12f), radius * 4f, 2f, Mathf.Max(40f, gravity * 3f));
            GravityWorldBehaviour gravityWorld = worldObject.AddComponent<GravityWorldBehaviour>();
            gravityWorld.Configure(new[] { source });

            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Primitive Planet";
            planet.transform.localScale = Vector3.one * radius * 2f;
            planet.isStatic = true;

            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Top Ramp";
            ramp.transform.position = new Vector3(0f, radius, Mathf.Min(5f, radius * 0.21f));
            ramp.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            ramp.transform.localScale = new Vector3(6f, 0.5f, 4f);
            ramp.isStatic = true;

            const int bodyCount = 32;
            GameObject[] bodyObjects = new GameObject[bodyCount];
            float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (int index = 0; index < bodyCount; index++)
            {
                float y = 1f - (2f * (index + 0.5f) / bodyCount);
                float radial = Mathf.Sqrt(1f - (y * y));
                float theta = goldenAngle * index;
                Vector3 direction = new Vector3(
                    Mathf.Cos(theta) * radial,
                    y,
                    Mathf.Sin(theta) * radial);

                GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bodyObject.name = $"Gravity Body {index + 1:00}";
                bodyObject.SetActive(false);
                bodyObject.transform.position = direction * (radius + 10f);
                bodyObject.transform.localScale = Vector3.one * 0.9f;
                Rigidbody body = bodyObject.AddComponent<Rigidbody>();
                body.mass = 2f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                GravityBody gravityBody = bodyObject.AddComponent<GravityBody>();
                gravityBody.Configure(gravityWorld, body);
                bodyObjects[index] = bodyObject;
            }

            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(GameplayActionsPath);
            if (actions == null)
            {
                throw new InvalidOperationException($"Gameplay actions asset was not found at {GameplayActionsPath}.");
            }

            GameObject character = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            character.name = "Planet Character";
            character.SetActive(false);
            character.transform.position = new Vector3(0f, radius + 1.3f, 0f);
            character.transform.localScale = Vector3.one * 1.2f;
            Rigidbody characterBody = character.AddComponent<Rigidbody>();
            characterBody.mass = 30f;
            characterBody.interpolation = RigidbodyInterpolation.Interpolate;
            characterBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            CapsuleCollider characterCapsule = character.GetComponent<CapsuleCollider>();
            GravityBody characterGravity = character.AddComponent<GravityBody>();
            characterGravity.Configure(gravityWorld, characterBody);
            PlayerInput playerInput = character.AddComponent<PlayerInput>();
            playerInput.actions = actions;
            playerInput.defaultActionMap = "Gameplay";
            PlanetInputReader inputReader = character.AddComponent<PlanetInputReader>();
            inputReader.Configure(playerInput);
            PlanetMotor planetMotor = character.AddComponent<PlanetMotor>();

            GameObject cameraObject = new GameObject("Gravity Toy Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            camera.transform.position = new Vector3(0f, radius + 4f, -8f);
            camera.transform.LookAt(character.transform.position, Vector3.up);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.028f, 0.05f);
            PlanetCameraRig cameraRig = cameraObject.AddComponent<PlanetCameraRig>();
            cameraRig.Configure(character.transform, characterBody, gravityWorld);
            planetMotor.Configure(
                gravityWorld,
                characterBody,
                characterCapsule,
                inputReader,
                cameraObject.transform);

            GameObject lightObject = new GameObject("Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

            worldObject.SetActive(true);
            character.SetActive(true);
            for (int index = 0; index < bodyObjects.Length; index++)
            {
                bodyObjects[index].SetActive(true);
            }

            EditorSceneManager.SaveScene(scene, GravityToyScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Elemental] M1 Gravity Toy scene configured with 32 dynamic bodies.");
        }
    }
}
