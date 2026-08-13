using System.Collections;
using System.Collections.Generic;
using Elemental.Input.Gestures;
using Elemental.Input.Actions;
using Elemental.Runtime.Characters;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using Unity.Mathematics;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthCoreVisualRuntimeTests
    {
        [UnityTest]
        public IEnumerator EarthCoreLoadsAsReadableDioramaWithHudAndFeedback()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool loaded = scene.IsValid() && scene.isLoaded;
            bool hasHud = FindInScene<EarthCoreHud>(scene) != null;
            bool hasFeedback = FindInScene<EarthMagicFeedback>(scene) != null;
            bool hasFootprintPreview = FindInScene<EarthFootprintPreview>(scene) != null;
            bool hasAbilityPreview = FindInScene<EarthAbilityPreview>(scene) != null;
            bool hasPoseDriver = FindInScene<EarthMagicPoseDriver>(scene) != null;
            bool hasWallPool = FindInScene<Elemental.Runtime.Physics.EarthWallPool>(scene) != null;
            bool hasShaper = FindByName(scene, "Earth Shaper Visual") != null;
            bool hasLandmarks = FindByName(scene, "Earth Diorama Landmarks") != null;
            bool hasStars = FindByName(scene, "Diorama Star Field") != null;
            bool hasSun = FindByName(scene, "Visible Sun") != null;
            bool hasRingedPlanet = FindByName(scene, "Ringed Ember Planet") != null;
            GameObject moonObject = FindByName(scene, "Distant Moon");
            GameObject atmosphereObject = FindByName(scene, "Planet Atmosphere Limb");
            CelestialSystemBehaviour celestialSystem = FindInScene<CelestialSystemBehaviour>(scene);
            MeteorShowerBehaviour meteorSystem = FindInScene<MeteorShowerBehaviour>(scene);
            HumanoidCharacterPresentation humanoid = FindInScene<HumanoidCharacterPresentation>(scene);
            Animator humanoidAnimator = humanoid != null ? humanoid.Animator : null;
            bool hasCelestialSystem = celestialSystem != null;
            bool hasMeteorSystem = meteorSystem != null;
            Material configuredSky = celestialSystem != null ? celestialSystem.StarSkybox : null;
            bool hasProceduralSky = configuredSky != null && configuredSky.shader != null &&
                                    configuredSky.shader.name == "Elemental/Procedural Stars";
            bool moonHasNoCollider = moonObject != null && moonObject.GetComponent<Collider>() == null;
            bool atmosphereHasNoCollider = atmosphereObject != null && atmosphereObject.GetComponent<Collider>() == null;
            bool hasValidHumanoid = humanoidAnimator != null && humanoidAnimator.avatar != null &&
                                    humanoidAnimator.avatar.isValid && humanoidAnimator.avatar.isHuman &&
                                    !humanoidAnimator.applyRootMotion;
            GameObject celestialBackdrop = FindByName(scene, "Celestial Diorama Backdrop");
            Camera playableCamera = FindInScene<Camera>(scene);
            bool celestialBackdropIsWorldSpace = celestialBackdrop != null &&
                                                 celestialBackdrop.transform.parent == null;
            float playableFieldOfView = playableCamera != null ? playableCamera.fieldOfView : 0f;
            GameObject lightBoulder = FindByName(scene, "Light Push Boulder");
            GameObject heavyBoulder = FindByName(scene, "Heavy Push Boulder");
            Rigidbody lightBoulderBody = lightBoulder != null ? lightBoulder.GetComponent<Rigidbody>() : null;
            Rigidbody heavyBoulderBody = heavyBoulder != null ? heavyBoulder.GetComponent<Rigidbody>() : null;
            bool hasPushBoulders = lightBoulderBody != null && heavyBoulderBody != null &&
                                   lightBoulderBody.mass < heavyBoulderBody.mass;
            GameObject ramp = FindByName(scene, "Top Ramp");
            GameObject gravityBody = FindByName(scene, "Gravity Body 01");
            bool technicalPropsHidden = ramp != null && !ramp.activeSelf &&
                                        gravityBody != null && !gravityBody.activeSelf;
            bool hasVolume = FindInScene<Volume>(scene) != null;
            UIDocument document = FindInScene<UIDocument>(scene);
            bool hasAbilityLabel = document != null && document.rootVisualElement.Q<Label>("ability-value") != null;
            bool hasLiftMeter = document != null && document.rootVisualElement.Q<VisualElement>("lift-fill") != null;
            EarthPillarMobility pillarMobility = FindInScene<EarthPillarMobility>(scene);
            PlanetInputReader planetInput = FindInScene<PlanetInputReader>(scene);
            bool hasPillarMobility = pillarMobility != null;
            bool spaceRoutesToPillar = planetInput != null && planetInput.UsesEarthPillarMobility;

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;

            Assert.That(loaded, Is.True);
            Assert.That(hasHud, Is.True);
            Assert.That(hasFeedback, Is.True);
            Assert.That(hasFootprintPreview, Is.True);
            Assert.That(hasAbilityPreview, Is.True);
            Assert.That(hasPoseDriver, Is.True);
            Assert.That(hasWallPool, Is.True);
            Assert.That(hasShaper, Is.True);
            Assert.That(hasLandmarks, Is.True);
            Assert.That(hasStars, Is.True);
            Assert.That(hasSun, Is.True);
            Assert.That(hasRingedPlanet, Is.True);
            Assert.That(hasCelestialSystem, Is.True);
            Assert.That(hasMeteorSystem, Is.True);
            Assert.That(hasProceduralSky, Is.True);
            Assert.That(moonHasNoCollider, Is.True);
            Assert.That(atmosphereHasNoCollider, Is.True);
            Assert.That(hasValidHumanoid, Is.True);
            Assert.That(celestialBackdrop, Is.Not.Null);
            Assert.That(celestialBackdropIsWorldSpace, Is.True,
                "Celestial bodies must stay in world space instead of following the player camera.");
            Assert.That(playableFieldOfView, Is.GreaterThanOrEqualTo(62f));
            Assert.That(hasPushBoulders, Is.True);
            Assert.That(technicalPropsHidden, Is.True);
            Assert.That(hasVolume, Is.True);
            Assert.That(hasAbilityLabel, Is.True);
            Assert.That(hasLiftMeter, Is.True);
            Assert.That(hasPillarMobility, Is.True);
            Assert.That(planetInput, Is.Not.Null);
            Assert.That(spaceRoutesToPillar, Is.True,
                "The Space input must route into charged Earth mobility in the playable scene.");
        }

        [UnityTest]
        public IEnumerator PullThenFlickWorksFromScreenInputWithoutProjectingThrowOntoPlanet()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            MagicInputController input = FindInScene<MagicInputController>(scene);
            MagicExecutor executor = FindInScene<MagicExecutor>(scene);
            Camera camera = FindInScene<Camera>(scene);
            GameObject proxyObject = FindByName(scene, "Planet Collision Proxy");
            Collider proxy = proxyObject != null ? proxyObject.GetComponent<Collider>() : null;
            Transform heldAnchor = FindByName(scene, "Held Earth Anchor")?.transform;
            Physics.SyncTransforms();

            List<float2> surfaceLine = FindHorizontalPlanetStroke(camera, proxy);
            Assert.That(surfaceLine, Is.Not.Null);
            float2 pullStart = surfaceLine[0];
            var pullStroke = new List<float2>
            {
                pullStart
            };
            Assert.That(input.SelectEarthAbility(EarthAbilityIds.PullRock), Is.True);
            int commandsBefore = executor.SuccessfulCommandCount;
            bool pulled = input.TryCommitScreenPath(pullStroke, 0.8f);
            float heldMass = executor.HeldMass;
            EarthFragment initiallyHeld = executor.HeldFragment;
            float initialHeldDistance = initiallyHeld != null && heldAnchor != null
                ? Vector3.Distance(initiallyHeld.transform.position, heldAnchor.position)
                : float.PositiveInfinity;
            for (int frame = 0; frame < 45; frame++) yield return null;
            EarthFragment held = executor.HeldFragment;
            float heldDistance = held != null && heldAnchor != null
                ? Vector3.Distance(held.transform.position, heldAnchor.position)
                : float.PositiveInfinity;
            bool heldWasDynamic = held != null && !held.Body.isKinematic;
            float heldControlForce = held != null ? held.LastAppliedControlForce.sqrMagnitude : 0f;

            Assert.That(input.SelectEarthAbility(EarthAbilityIds.FlickThrow), Is.True);
            var flickStroke = new List<float2>
            {
                new float2(720f, 430f),
                new float2(880f, 455f)
            };
            bool thrown = input.TryCommitScreenPath(flickStroke, 0.18f);
            yield return new WaitForFixedUpdate();
            float launchedSpeed = held != null ? held.Body.linearVelocity.magnitude : 0f;
            float launchVelocityChange = executor.LastLaunchVelocityChange;
            int commandsAfter = executor.SuccessfulCommandCount;

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;

            Assert.That(pulled, Is.True);
            Assert.That(heldMass, Is.GreaterThan(100f));
            Assert.That(heldDistance, Is.LessThan(initialHeldDistance),
                "The dynamic mass should converge toward its target without being teleported onto it.");
            Assert.That(heldWasDynamic, Is.True);
            Assert.That(heldControlForce, Is.GreaterThan(1f));
            Assert.That(thrown, Is.True);
            Assert.That(launchVelocityChange, Is.InRange(6f, 18f));
            Assert.That(launchedSpeed, Is.GreaterThan(4f));
            Assert.That(commandsAfter, Is.EqualTo(commandsBefore + 2));
        }

        [UnityTest]
        public IEnumerator ScreenStrokeProjectsToPlanetAndCommitsEarthMagic()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            MagicInputController input = FindInScene<MagicInputController>(scene);
            MagicExecutor executor = FindInScene<MagicExecutor>(scene);
            Camera camera = FindInScene<Camera>(scene);
            EarthWallPool wallPool = FindInScene<EarthWallPool>(scene);
            Elemental.Presentation.Camera.PlanetCameraRig cameraRig =
                FindInScene<Elemental.Presentation.Camera.PlanetCameraRig>(scene);
            GameObject proxyObject = FindByName(scene, "Planet Collision Proxy");
            Collider proxy = proxyObject != null ? proxyObject.GetComponent<Collider>() : null;
            bool hasProxy = proxy != null;
            Physics.SyncTransforms();

            List<float2> stroke = FindWidestPlanetStroke(camera, proxy);
            int commandsBefore = executor != null ? executor.SuccessfulCommandCount : -1;
            bool committed = input != null && stroke != null && input.TryCommitScreenPath(stroke, 0.8f);
            int commandsAfter = executor != null ? executor.SuccessfulCommandCount : -1;
            float wallSpan = wallPool != null && wallPool.LastAcquired != null
                ? Vector3.Distance(wallPool.LastAcquired.Start, wallPool.LastAcquired.End)
                : 0f;
            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;

            Assert.That(hasProxy, Is.True);
            Assert.That(stroke, Is.Not.Null, "No screen-space line across the planet proxy could be found.");
            Assert.That(committed, Is.True);
            Assert.That(commandsAfter, Is.EqualTo(commandsBefore + 1));
            Assert.That(wallSpan, Is.GreaterThan(7.5f),
                "A wide screen stroke must no longer be clipped by the former 7.5 m wall limit.");
            Assert.That(cameraRig, Is.Not.Null);
            Assert.That(cameraRig.PeakRequestedImpulseAmplitude, Is.GreaterThan(0.08f),
                "A raised wall should request a noticeable, bounded presentation impulse.");
        }

        private static List<float2> FindHorizontalPlanetStroke(Camera camera, Collider proxy)
        {
            if (camera == null || proxy == null) return null;
            int width = Mathf.Max(320, Screen.width);
            int height = Mathf.Max(200, Screen.height);
            for (int y = 16; y < height - 16; y += 8)
            {
                int first = -1;
                int last = -1;
                for (int x = 16; x < width - 16; x += 8)
                {
                    if (!proxy.Raycast(camera.ScreenPointToRay(new Vector2(x, y)), out _, 200f)) continue;
                    if (first < 0) first = x;
                    last = x;
                }

                if (last - first < 80) continue;
                int inset = Mathf.Max(16, (last - first) / 5);
                return new List<float2>
                {
                    new float2(first + inset, y),
                    new float2(last - inset, y)
                };
            }

            return null;
        }

        private static List<float2> FindWidestPlanetStroke(Camera camera, Collider proxy)
        {
            if (camera == null || proxy == null) return null;
            int width = Mathf.Max(320, Screen.width);
            int height = Mathf.Max(200, Screen.height);
            int bestFirst = -1;
            int bestLast = -1;
            int bestY = -1;
            for (int y = 16; y < height - 16; y += 8)
            {
                int first = -1;
                int last = -1;
                for (int x = 16; x < width - 16; x += 8)
                {
                    if (!proxy.Raycast(camera.ScreenPointToRay(new Vector2(x, y)), out _, 200f)) continue;
                    if (first < 0) first = x;
                    last = x;
                }
                if (last - first <= bestLast - bestFirst) continue;
                bestFirst = first;
                bestLast = last;
                bestY = y;
            }

            if (bestLast - bestFirst < 80) return null;
            int inset = 8;
            return new List<float2>
            {
                new float2(bestFirst + inset, bestY),
                new float2(bestLast - inset, bestY)
            };
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T found = roots[index].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindByName(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Transform[] transforms = roots[index].GetComponentsInChildren<Transform>(true);
                for (int child = 0; child < transforms.Length; child++)
                    if (transforms[child].name == name) return transforms[child].gameObject;
            }
            return null;
        }
    }
}
