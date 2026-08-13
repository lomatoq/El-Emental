using System.Collections.Generic;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Presentation.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class M4CharacterFeelSetup
    {
        public const string CharacterFeelScenePath = "Assets/Elemental/Content/Scenes/CharacterFeelLab.unity";

        [MenuItem("Elemental/Setup/Create M4 Character Feel Lab")]
        public static void Configure()
        {
            M1GravityToySetup.Configure();
            Scene scene = SceneManager.GetActiveScene();
            GameObject character = GameObject.Find("Planet Character");
            GravityWorldBehaviour gravityWorld = Object.FindAnyObjectByType<GravityWorldBehaviour>();
            GameObject planet = GameObject.Find("Primitive Planet");
            if (character == null || gravityWorld == null || planet == null)
            {
                throw new UnityEditor.Build.BuildFailedException("M1 dependencies are missing for Character Feel Lab.");
            }

            character.SetActive(false);
            Rigidbody rootBody = character.GetComponent<Rigidbody>();
            PlanetMotor motor = character.GetComponent<PlanetMotor>();
            PhysicalImpactTarget impactTarget = character.GetComponent<PhysicalImpactTarget>();
            if (impactTarget == null)
            {
                impactTarget = character.AddComponent<PhysicalImpactTarget>();
            }
            impactTarget.Configure(rootBody, 0.3f);

            MeshRenderer rootRenderer = character.GetComponent<MeshRenderer>();
            if (rootRenderer != null)
            {
                rootRenderer.enabled = false;
            }

            Transform targetsRoot = new GameObject("Puppet Pose Targets").transform;
            targetsRoot.SetParent(character.transform, false);
            var joints = new List<ActiveRagdollJoint>(6);
            var colliders = new List<Collider>(8) { character.GetComponent<Collider>() };

            PuppetPart chest = CreatePart(
                "Puppet Chest", PrimitiveType.Cube, character.transform, targetsRoot,
                new Vector3(0f, 0.55f, 0f), new Vector3(0.95f, 0.65f, 0.55f),
                7f, rootBody, gravityWorld, 35f);
            PuppetPart head = CreatePart(
                "Puppet Head", PrimitiveType.Sphere, character.transform, targetsRoot,
                new Vector3(0f, 1.4f, 0f), Vector3.one * 0.78f,
                3f, chest.Body, gravityWorld, 38f);
            PuppetPart leftArm = CreatePart(
                "Puppet Arm L", PrimitiveType.Capsule, character.transform, targetsRoot,
                new Vector3(-0.82f, 0.5f, 0f), new Vector3(0.32f, 0.58f, 0.32f),
                2f, chest.Body, gravityWorld, 55f);
            PuppetPart rightArm = CreatePart(
                "Puppet Arm R", PrimitiveType.Capsule, character.transform, targetsRoot,
                new Vector3(0.82f, 0.5f, 0f), new Vector3(0.32f, 0.58f, 0.32f),
                2f, chest.Body, gravityWorld, 55f);
            PuppetPart leftLeg = CreatePart(
                "Puppet Leg L", PrimitiveType.Capsule, character.transform, targetsRoot,
                new Vector3(-0.34f, -0.82f, 0f), new Vector3(0.38f, 0.68f, 0.38f),
                4f, rootBody, gravityWorld, 42f);
            PuppetPart rightLeg = CreatePart(
                "Puppet Leg R", PrimitiveType.Capsule, character.transform, targetsRoot,
                new Vector3(0.34f, -0.82f, 0f), new Vector3(0.38f, 0.68f, 0.38f),
                4f, rootBody, gravityWorld, 42f);

            PuppetPart[] parts = { chest, head, leftArm, rightArm, leftLeg, rightLeg };
            for (int index = 0; index < parts.Length; index++)
            {
                joints.Add(parts[index].JointDriver);
                colliders.Add(parts[index].Collider);
            }

            ActiveRagdollPuppet puppet = character.AddComponent<ActiveRagdollPuppet>();
            puppet.Configure(
                1u,
                gravityWorld,
                rootBody,
                motor,
                impactTarget,
                chest.Transform,
                joints.ToArray(),
                colliders.ToArray());
            character.SetActive(true);

            Rigidbody[] rocks = CreateFallingRocks(gravityWorld);
            GameObject driverObject = new GameObject("Character Feel Lab Driver");
            CharacterFeelLabDriver driver = driverObject.AddComponent<CharacterFeelLabDriver>();
            driver.Configure(puppet, rootBody, rocks, planet.transform);

            Camera camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                GameObject feedbackObject = new GameObject("Character Impact Feedback");
                feedbackObject.transform.SetParent(character.transform, false);
                ParticleSystem particles = feedbackObject.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particles.main;
                main.playOnAwake = false;
                main.duration = 0.35f;
                main.startLifetime = 0.35f;
                main.startSpeed = 4f;
                main.startSize = 0.18f;
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.enabled = false;
                AudioSource audio = feedbackObject.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                CharacterFeelFeedbackRouter feedback = camera.gameObject.AddComponent<CharacterFeelFeedbackRouter>();
                feedback.Configure(puppet, particles, audio, camera.transform);
            }

            CreateRecoverySlope();
            EditorSceneManager.SaveScene(scene, CharacterFeelScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Elemental] M4 Character Feel Lab configured.");
        }

        private static PuppetPart CreatePart(
            string name,
            PrimitiveType primitive,
            Transform physicalParent,
            Transform targetsRoot,
            Vector3 localPosition,
            Vector3 localScale,
            float mass,
            Rigidbody connectedBody,
            GravityWorldBehaviour gravityWorld,
            float angularLimit)
        {
            GameObject targetObject = new GameObject(name + " Target");
            targetObject.transform.SetParent(targetsRoot, false);
            targetObject.transform.localPosition = localPosition;
            targetObject.transform.localRotation = Quaternion.identity;

            GameObject partObject = GameObject.CreatePrimitive(primitive);
            partObject.name = name;
            partObject.transform.SetParent(physicalParent, false);
            partObject.transform.localPosition = localPosition;
            partObject.transform.localRotation = Quaternion.identity;
            partObject.transform.localScale = localScale;
            Rigidbody body = partObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = 20f;
            GravityBody gravityBody = partObject.AddComponent<GravityBody>();
            gravityBody.Configure(gravityWorld, body);
            ConfigurableJoint joint = partObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = connectedBody;
            joint.autoConfigureConnectedAnchor = true;
            ActiveRagdollJoint driver = partObject.AddComponent<ActiveRagdollJoint>();
            driver.Configure(body, joint, targetObject.transform, 900f, 65f, 1400f, angularLimit);
            return new PuppetPart(partObject.transform, body, partObject.GetComponent<Collider>(), driver);
        }

        private static Rigidbody[] CreateFallingRocks(GravityWorldBehaviour gravityWorld)
        {
            const int count = 10;
            var rocks = new Rigidbody[count];
            for (int index = 0; index < count; index++)
            {
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"Feel Rock {index + 1:00}";
                rock.transform.position = new Vector3((index - 5) * 0.8f, 31f + (index % 3), 2f + (index % 4));
                rock.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.2f, index / 9f);
                Rigidbody body = rock.AddComponent<Rigidbody>();
                body.mass = Mathf.Lerp(4f, 24f, index / 9f);
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                GravityBody gravityBody = rock.AddComponent<GravityBody>();
                gravityBody.Configure(gravityWorld, body);
                rocks[index] = body;
            }

            return rocks;
        }

        private static void CreateRecoverySlope()
        {
            GameObject slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slope.name = "Recovery Slope";
            slope.transform.position = new Vector3(8f, 22.5f, 0f);
            slope.transform.rotation = Quaternion.Euler(0f, 0f, -25f);
            slope.transform.localScale = new Vector3(8f, 0.6f, 5f);
            slope.isStatic = true;
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(item => item.path == CharacterFeelScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(CharacterFeelScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private readonly struct PuppetPart
        {
            public PuppetPart(Transform transform, Rigidbody body, Collider collider, ActiveRagdollJoint jointDriver)
            {
                Transform = transform;
                Body = body;
                Collider = collider;
                JointDriver = jointDriver;
            }

            public Transform Transform { get; }
            public Rigidbody Body { get; }
            public Collider Collider { get; }
            public ActiveRagdollJoint JointDriver { get; }
        }
    }
}
