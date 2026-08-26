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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

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
            EarthSkyController skyController = FindInScene<EarthSkyController>(scene);
            MeteorShowerBehaviour meteorSystem = FindInScene<MeteorShowerBehaviour>(scene);
            HumanoidCharacterPresentation humanoid = FindInScene<HumanoidCharacterPresentation>(scene);
            EarthCharacterPoseController characterPose = FindInScene<EarthCharacterPoseController>(scene);
            Elemental.Presentation.Camera.EarthCameraDirector cameraDirector =
                FindInScene<Elemental.Presentation.Camera.EarthCameraDirector>(scene);
            Animator humanoidAnimator = humanoid != null ? humanoid.Animator : null;
            bool hasCelestialSystem = celestialSystem != null;
            bool hasReadableDaySky = skyController != null && skyController.LastStarVisibility <= 0.001f &&
                                     skyController.LastZenithColor.b > skyController.LastZenithColor.r * 1.8f &&
                                     skyController.LastHorizonColor.maxColorComponent > 0.5f;
            bool hasMeteorSystem = meteorSystem != null;
            Material configuredSky = celestialSystem != null ? celestialSystem.StarSkybox : null;
            bool hasProceduralSky = configuredSky != null && configuredSky.shader != null &&
                                    configuredSky.shader.name == "Elemental/Procedural Stars";
            bool moonHasNoCollider = moonObject != null && moonObject.GetComponent<Collider>() == null;
            bool atmosphereHasNoCollider = atmosphereObject != null && atmosphereObject.GetComponent<Collider>() == null;
            bool hasValidHumanoid = humanoidAnimator != null && humanoidAnimator.avatar != null &&
                                    humanoidAnimator.avatar.isValid && humanoidAnimator.avatar.isHuman &&
                                    !humanoidAnimator.applyRootMotion;
            bool hasEmbodiedPose = characterPose != null;
            bool hasAuthoredCameraDirector = cameraDirector != null && cameraDirector.Profile != null;
            float exploreDistance = 0f;
            Elemental.Presentation.Camera.EarthCameraStateProfile exploreCamera = default;
            bool hasExploreProfile = cameraDirector != null && cameraDirector.Profile != null &&
                                     cameraDirector.Profile.TryGet(
                                         Elemental.Simulation.Characters.EarthCameraState.Explore,
                                         out exploreCamera);
            if (hasExploreProfile) exploreDistance = exploreCamera.Distance;
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
            Assert.That(hasReadableDaySky, Is.True,
                "Daytime must render a blue gradient and suppress stars instead of falling back to black space.");
            Assert.That(hasMeteorSystem, Is.True);
            Assert.That(hasProceduralSky, Is.True);
            Assert.That(moonHasNoCollider, Is.True);
            Assert.That(atmosphereHasNoCollider, Is.True);
            Assert.That(hasValidHumanoid, Is.True);
            Assert.That(hasEmbodiedPose, Is.True);
            Assert.That(hasAuthoredCameraDirector, Is.True);
            Assert.That(hasExploreProfile, Is.True);
            Assert.That(exploreDistance, Is.InRange(7f, 7.8f));
            Assert.That(celestialBackdrop, Is.Not.Null);
            Assert.That(celestialBackdropIsWorldSpace, Is.True,
                "Celestial bodies must stay in world space instead of following the player camera.");
            Assert.That(playableFieldOfView, Is.InRange(57f, 64f));
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
            bool transactionWasPending = executor.HasPendingExtraction;
            for (int frame = 0; frame < 240 && executor.HeldFragment == null; frame++)
                yield return null;
            EarthFragment held = executor.HeldFragment;
            float heldMass = executor.HeldMass;
            for (int frame = 0; frame < 90; frame++) yield return new WaitForFixedUpdate();
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
            Assert.That(transactionWasPending || held != null, Is.True,
                "Extraction must either stage a transaction or commit its visible rock immediately.");
            Assert.That(held, Is.Not.Null,
                "Committed terrain extraction must wake its reserved visible fragment.");
            Assert.That(heldMass, Is.GreaterThan(100f));
            Assert.That(heldDistance, Is.LessThan(4f),
                "The committed dynamic mass should converge toward the held anchor.");
            Assert.That(heldWasDynamic, Is.True);
            Assert.That(heldControlForce, Is.GreaterThan(1f));
            Assert.That(thrown, Is.True);
            Assert.That(launchVelocityChange, Is.InRange(6f, 18f));
            Assert.That(launchedSpeed, Is.GreaterThan(4f));
            Assert.That(commandsAfter, Is.EqualTo(commandsBefore + 2));
        }

        [UnityTest]
        public IEnumerator QuickStoneSurvivesBudgetedTerrainCommitAndLaunchesItsReservedRock()
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
            Collider proxy = FindByName(scene, "Planet Collision Proxy")?.GetComponent<Collider>();
            Physics.SyncTransforms();
            List<float2> surfaceLine = FindHorizontalPlanetStroke(camera, proxy);
            Assert.That(surfaceLine, Is.Not.Null);
            float2 pointer = surfaceLine[0];

            bool primed = input.TryQuickStoneTapAtScreenPoint(pointer);
            EarthFragment reserved = executor.ReservedOrHeldFragment;
            bool secondClickAccepted = input.TryQuickStoneTapAtScreenPoint(pointer);
            bool wasPending = executor.HasPendingExtraction;
            for (int frame = 0; frame < 300 &&
                 (executor.HasPendingExtraction || input.IsQuickStonePrimed); frame++)
                yield return null;
            yield return new WaitForFixedUpdate();
            float launchedSpeed = reserved != null ? reserved.Body.linearVelocity.magnitude : 0f;
            float launchVelocityChange = executor.LastLaunchVelocityChange;
            bool stillOwned = executor.HeldBody != null;

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;

            Assert.That(primed, Is.True);
            Assert.That(reserved, Is.Not.Null,
                "The fragment must be reserved before the SDF edit can create a cavity.");
            Assert.That(secondClickAccepted, Is.True,
                "A second click during terrain staging must buffer instead of losing the stone.");
            Assert.That(wasPending, Is.True,
                "The shipping scene must exercise the budgeted terrain transaction path.");
            Assert.That(launchVelocityChange, Is.GreaterThan(20f),
                "The buffered shot must reach the authored launch contract before contact response.");
            Assert.That(launchedSpeed, Is.GreaterThan(4f),
                "The committed stone must remain a live physical projectile after its first physics step.");
            Assert.That(stillOwned, Is.False);
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
            EarthWallPool wallPool = executor != null ? executor.WallPool : null;
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

        [UnityTest]
        public IEnumerator WallsCommitFromBothNearAndFarPlanetStrokes()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            MagicInputController input = FindInScene<MagicInputController>(scene);
            MagicExecutor executor = FindInScene<MagicExecutor>(scene);
            Camera camera = input.CastCamera;
            EarthWallPool wallPool = FindInScene<EarthWallPool>(scene);
            GameObject player = FindByName(scene, "Planet Character");
            Collider proxy = input.PlanetCollider;
            Assert.That(input, Is.Not.Null);
            Assert.That(executor, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Assert.That(wallPool, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(proxy, Is.Not.Null);
            Physics.SyncTransforms();

            Assert.That(TryFindNearAndFarPlanetStrokes(
                camera,
                proxy,
                player.transform.position,
                out List<float2> nearStroke,
                out List<float2> farStroke), Is.True);
            int commandsBefore = executor.SuccessfulCommandCount;
            Assert.That(input.SelectEarthAbility(EarthAbilityIds.LineWall), Is.True);
            Vector2[] nearPath = ToVector2Array(nearStroke);
            input.ReplayBufferedPrimaryPath(nearPath, nearPath.Length);
            input.ReplayBufferedPrimaryRelease(nearPath[nearPath.Length - 1]);
            bool nearCommitted = executor.SuccessfulCommandCount == commandsBefore + 1;
            float nearDistance = wallPool.LastAcquired != null
                ? Vector3.Distance(player.transform.position, wallPool.LastAcquired.Start)
                : float.PositiveInfinity;
            Assert.That(input.SelectEarthAbility(EarthAbilityIds.LineWall), Is.True);
            Vector2[] farPath = ToVector2Array(farStroke);
            input.ReplayBufferedPrimaryPath(farPath, farPath.Length);
            input.ReplayBufferedPrimaryRelease(farPath[farPath.Length - 1]);
            bool farCommitted = executor.SuccessfulCommandCount == commandsBefore + 2;
            float farDistance = wallPool.LastAcquired != null
                ? Vector3.Distance(player.transform.position, wallPool.LastAcquired.Start)
                : 0f;
            int commandsAfter = executor.SuccessfulCommandCount;
            yield return null;

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;

            Assert.That(nearCommitted, Is.True, "A short wall directly in front of the player must commit.");
            Assert.That(farCommitted, Is.True, "A distant visible planet stroke must commit just as reliably.");
            Assert.That(commandsAfter, Is.EqualTo(commandsBefore + 2));
            Assert.That(farDistance, Is.GreaterThan(nearDistance + 2f));
        }

        [UnityTest]
        public IEnumerator PhysicalMouseRouteCommitsNearAndFarWallsThroughShippingRouter()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            MagicInputController input = FindInScene<MagicInputController>(scene);
            MagicExecutor executor = input != null ? input.EarthExecutor : null;
            EarthActionRouterBehaviour router = FindInScene<EarthActionRouterBehaviour>(scene);
            PlayerInput playerInput = FindInScene<PlayerInput>(scene);
            Camera camera = input.CastCamera;
            EarthWallPool wallPool = executor != null ? executor.WallPool : null;
            GameObject player = FindByName(scene, "Planet Character");
            Collider proxy = input.PlanetCollider;
            Assert.That(input, Is.Not.Null);
            Assert.That(executor, Is.Not.Null);
            Assert.That(router, Is.Not.Null,
                "The runtime-added shipping action router must be active before device input is injected.");
            Assert.That(playerInput, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Assert.That(wallPool, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(proxy, Is.Not.Null);
            Physics.SyncTransforms();
            Assert.That(TryFindNearAndFarPlanetStrokes(
                camera,
                proxy,
                player.transform.position,
                out List<float2> nearStroke,
                out List<float2> farStroke), Is.True);

            string latestStatus = string.Empty;
            input.StatusChanged += status => latestStatus = status;
            Mouse routedMouse = CreateRoutedMouse(playerInput, "Earth Routed Input Test Mouse");
            int commandsBefore = executor.SuccessfulCommandCount;

            // Near: the meaningful movement arrives on the release frame, inside
            // the chord window. Far: a normal multi-frame stroke crosses it.
            yield return DrivePhysicalPrimaryStroke(routedMouse, nearStroke[0], nearStroke[1], 0f);
            int commandsAfterNear = executor.SuccessfulCommandCount;
            // Keep the concrete pooled object as evidence; Unity 6 no longer
            // permits legacy instance ids in newly compiled player code.
            EarthWall nearWall = wallPool.LastAcquired;
            float nearDistance = wallPool.LastAcquired != null
                ? Vector3.Distance(player.transform.position, wallPool.LastAcquired.Start)
                : float.PositiveInfinity;
            yield return DrivePhysicalPrimaryStroke(routedMouse, farStroke[0], farStroke[1], 0.18f);
            int commandsAfterFar = executor.SuccessfulCommandCount;
            EarthWall farWall = wallPool.LastAcquired;
            float farDistance = wallPool.LastAcquired != null
                ? Vector3.Distance(player.transform.position, wallPool.LastAcquired.Start)
                : 0f;
            string finalPhase = input.CurrentBendPhase.ToString();
            string finalOwner = router.Owner.ToString();

            InputSystem.RemoveDevice(routedMouse);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;

            Assert.That(nearWall, Is.Not.Null,
                $"A real <Mouse>/leftButton near stroke did not commit. " +
                $"commands={commandsBefore}->{commandsAfterNear}, phase={finalPhase}, " +
                $"owner={finalOwner}, status={latestStatus}");
            Assert.That(farWall, Is.Not.SameAs(nearWall),
                $"A real <Mouse>/leftButton far stroke did not commit. " +
                $"commands={commandsAfterNear}->{commandsAfterFar}, phase={finalPhase}, " +
                $"owner={finalOwner}, status={latestStatus}");
            Assert.That(farDistance, Is.GreaterThan(nearDistance + 1f),
                "The second physical stroke must land on a meaningfully more distant visible surface band.");
        }

        [UnityTest]
        public IEnumerator PhysicalMouseClosedStrokeCommitsPlatformThroughShippingRouter()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            MagicInputController input = FindInScene<MagicInputController>(scene);
            MagicExecutor executor = input != null ? input.EarthExecutor : null;
            EarthActionRouterBehaviour router = FindInScene<EarthActionRouterBehaviour>(scene);
            PlayerInput playerInput = FindInScene<PlayerInput>(scene);
            Camera camera = input.CastCamera;
            EarthPlatformPool platformPool = executor != null ? executor.PlatformPool : null;
            Collider proxy = input.PlanetCollider;
            Assert.That(input, Is.Not.Null);
            Assert.That(executor, Is.Not.Null);
            Assert.That(router, Is.Not.Null);
            Assert.That(playerInput, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Assert.That(platformPool, Is.Not.Null);
            Assert.That(proxy, Is.Not.Null);
            Physics.SyncTransforms();
            Assert.That(TryFindClosedPlanetContour(camera, proxy, out float2[] contour), Is.True);

            Mouse routedMouse = CreateRoutedMouse(playerInput, "Earth Platform Input Test Mouse");
            int commandsBefore = executor.SuccessfulCommandCount;
            QueuePrimaryMouseState(routedMouse, contour[0], true);
            yield return null;
            for (int index = 1; index < contour.Length - 1; index++)
            {
                QueuePrimaryMouseState(routedMouse, contour[index], true);
                yield return null;
            }
            QueuePrimaryMouseState(routedMouse, contour[contour.Length - 1], false);
            yield return null;
            yield return null;

            int commandsAfter = executor.SuccessfulCommandCount;
            bool acquiredPlatform = platformPool.LastAcquired != null;
            InputSystem.RemoveDevice(routedMouse);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;

            Assert.That(commandsAfter, Is.EqualTo(commandsBefore + 1),
                "A real closed LMB contour must commit exactly one platform command.");
            Assert.That(acquiredPlatform, Is.True,
                "The physical input route classified the contour but did not acquire a pooled platform.");
        }

        [UnityTest]
        public IEnumerator PhysicalMouseStationaryHoldStartsTerrainExtractionThroughShippingRouter()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            MagicInputController input = FindInScene<MagicInputController>(scene);
            MagicExecutor executor = input != null ? input.EarthExecutor : null;
            EarthActionRouterBehaviour router = FindInScene<EarthActionRouterBehaviour>(scene);
            PlayerInput playerInput = FindInScene<PlayerInput>(scene);
            EarthInputAdapter inputAdapter = FindInScene<EarthInputAdapter>(scene);
            Camera camera = input.CastCamera;
            GameObject player = FindByName(scene, "Planet Character");
            Collider proxy = input.PlanetCollider;
            Assert.That(input, Is.Not.Null);
            Assert.That(executor, Is.Not.Null);
            Assert.That(router, Is.Not.Null);
            Assert.That(playerInput, Is.Not.Null);
            Assert.That(inputAdapter, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(proxy, Is.Not.Null);
            Physics.SyncTransforms();
            Assert.That(TryFindClearTerrainPoint(camera, proxy, player.transform, out float2 terrainPoint), Is.True);

            string latestStatus = string.Empty;
            input.StatusChanged += status => latestStatus = status;
            Mouse routedMouse = CreateRoutedMouse(playerInput, "Earth Extraction Input Test Mouse");
            int commandsBefore = executor.SuccessfulCommandCount;
            bool sawPhysicalEarth = false;
            bool sawTerrainFragment = false;
            bool sawPrimaryHeld = false;
            string acquiredBodyName = string.Empty;
            float maximumPointerError = 0f;
            // Match a real player gesture: the cursor is already over the target
            // before the button goes down. Moving and pressing a synthetic Pointer
            // in the same queued state can leave the PassThrough Pointer action one
            // frame behind the mouse button and select a neighbouring decor rock.
            QueuePrimaryMouseState(routedMouse, terrainPoint, false);
            yield return null;
            QueuePrimaryMouseState(routedMouse, terrainPoint, true);
            float holdStartedAt = Time.unscaledTime;
            do
            {
                QueuePrimaryMouseState(routedMouse, terrainPoint, true);
                sawPrimaryHeld |= inputAdapter.BendPrimaryHeld;
                maximumPointerError = Mathf.Max(
                    maximumPointerError,
                    Vector2.Distance(
                        inputAdapter.PointerPixels,
                        new Vector2(terrainPoint.x, terrainPoint.y)));
                sawPhysicalEarth |= executor.HasPendingExtraction ||
                                    executor.ReservedOrHeldFragment != null ||
                                    executor.HeldBody != null;
                sawTerrainFragment |= executor.HasPendingExtraction ||
                                      executor.ReservedOrHeldFragment != null;
                if (executor.HeldBody != null && executor.ReservedOrHeldFragment == null)
                    acquiredBodyName = executor.HeldBody.name;
                yield return null;
            } while (Time.unscaledTime - holdStartedAt < 0.55f);
            int commandsAfterHold = executor.SuccessfulCommandCount;
            sawPhysicalEarth |= executor.HasPendingExtraction ||
                                executor.ReservedOrHeldFragment != null ||
                                executor.HeldBody != null;
            sawTerrainFragment |= executor.HasPendingExtraction ||
                                  executor.ReservedOrHeldFragment != null;
            if (executor.HeldBody != null && executor.ReservedOrHeldFragment == null)
                acquiredBodyName = executor.HeldBody.name;
            QueuePrimaryMouseState(routedMouse, terrainPoint, false);
            yield return null;
            yield return null;

            InputSystem.RemoveDevice(routedMouse);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;

            Assert.That(commandsAfterHold, Is.EqualTo(commandsBefore + 1),
                $"A stationary physical LMB hold never issued terrain extraction. " +
                $"held={sawPrimaryHeld}, pointerError={maximumPointerError:F1}px, " +
                $"acquiredBody={acquiredBodyName}, status={latestStatus}");
            Assert.That(sawPhysicalEarth, Is.True,
                $"Extraction command ran without reserving, staging or holding physical Earth. status={latestStatus}");
            Assert.That(sawTerrainFragment, Is.True,
                $"The hold grabbed an existing object instead of extracting terrain. " +
                $"acquiredBody={acquiredBodyName}, status={latestStatus}");
        }

        [UnityTest]
        public IEnumerator PhysicalMouseHoldMovesVisibleDecorRockThroughShippingRouter()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            MagicInputController input = FindInScene<MagicInputController>(scene);
            MagicExecutor executor = input != null ? input.EarthExecutor : null;
            EarthActionRouterBehaviour router = FindInScene<EarthActionRouterBehaviour>(scene);
            PlayerInput playerInput = FindInScene<PlayerInput>(scene);
            EarthTelekinesisController telekinesis = FindInScene<EarthTelekinesisController>(scene);
            Camera camera = input != null ? input.CastCamera : null;
            Assert.That(input, Is.Not.Null);
            Assert.That(executor, Is.Not.Null);
            Assert.That(router, Is.Not.Null);
            Assert.That(playerInput, Is.Not.Null);
            Assert.That(telekinesis, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Physics.SyncTransforms();

            EarthDestructibleDecorRock target = null;
            Vector2 targetPointer = default;
            EarthDestructibleDecorRock[] rocks =
                Object.FindObjectsByType<EarthDestructibleDecorRock>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int index = 0; index < rocks.Length; index++)
            {
                EarthDestructibleDecorRock candidate = rocks[index];
                if (candidate == null || candidate.gameObject.scene != scene ||
                    !candidate.IsEarthTargetValid || candidate.Body == null) continue;
                Collider shape = candidate.GetComponent<Collider>();
                if (shape == null || !shape.enabled) continue;
                Vector3 screen = camera.WorldToScreenPoint(shape.bounds.center);
                if (screen.z <= 0f || screen.x < 24f || screen.y < 24f ||
                    screen.x > Screen.width - 24f || screen.y > Screen.height - 24f) continue;
                Ray ray = camera.ScreenPointToRay(screen);
                if (!Physics.Raycast(ray, out RaycastHit firstHit, 200f, ~0,
                        QueryTriggerInteraction.Ignore)) continue;
                if (firstHit.collider.GetComponentInParent<EarthDestructibleDecorRock>() != candidate)
                    continue;
                target = candidate;
                targetPointer = new Vector2(screen.x, screen.y);
                break;
            }
            Assert.That(target, Is.Not.Null, "No directly visible destructible decor rock was available.");

            Mouse routedMouse = CreateRoutedMouse(playerInput, "Earth Telekinesis Input Test Mouse");
            QueuePrimaryMouseState(routedMouse, targetPointer, false);
            yield return null;
            QueuePrimaryMouseState(routedMouse, targetPointer, true);
            float acquireStartedAt = Time.unscaledTime;
            do
            {
                QueuePrimaryMouseState(routedMouse, targetPointer, true);
                yield return null;
            } while (Time.unscaledTime - acquireStartedAt < 0.42f && executor.HeldBody == null);

            Rigidbody acquiredBody = executor.HeldBody;
            Vector3 positionBeforeMove = target.Body.position;
            Vector2 movedPointer = targetPointer + new Vector2(90f, 50f);
            float moveStartedAt = Time.unscaledTime;
            do
            {
                QueuePrimaryMouseState(routedMouse, movedPointer, true);
                yield return null;
            } while (Time.unscaledTime - moveStartedAt < 0.36f);
            float movedDistance = Vector3.Distance(positionBeforeMove, target.Body.position);
            float controlForce = telekinesis.LastAppliedControlForce.magnitude;
            QueuePrimaryMouseState(routedMouse, movedPointer, false);
            yield return null;
            yield return null;

            InputSystem.RemoveDevice(routedMouse);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;

            Assert.That(acquiredBody, Is.SameAs(target.Body),
                "A visible decor rock must be acquired through the real LMB route.");
            Assert.That(controlForce, Is.GreaterThan(1f),
                "Telekinesis acquired the rock but never applied control force.");
            Assert.That(movedDistance, Is.GreaterThan(0.04f),
                "Moving the held pointer must visibly move the acquired rock.");
        }

        private static IEnumerator DrivePhysicalPrimaryStroke(
            Mouse mouse,
            float2 start,
            float2 end,
            float drawSeconds)
        {
            QueuePrimaryMouseState(mouse, start, true);
            yield return null;

            if (drawSeconds <= 0f)
            {
                QueuePrimaryMouseState(mouse, end, false);
                yield return null;
                yield return null;
                yield break;
            }

            float startedAt = Time.unscaledTime;
            do
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / drawSeconds);
                QueuePrimaryMouseState(mouse, math.lerp(start, end, progress), true);
                yield return null;
            } while (Time.unscaledTime - startedAt < drawSeconds);

            QueuePrimaryMouseState(mouse, end, false);
            yield return null;
            yield return null;
        }

        private static void QueuePrimaryMouseState(Mouse mouse, float2 point, bool pressed)
        {
            var state = new MouseState { position = new Vector2(point.x, point.y) };
            state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, pressed);
            InputSystem.QueueStateEvent(mouse, state);
        }

        private static Mouse CreateRoutedMouse(PlayerInput playerInput, string displayName)
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>(displayName);
            if (Keyboard.current != null)
                playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", Keyboard.current, mouse);
            else
                InputUser.PerformPairingWithDevice(mouse, playerInput.user);
            playerInput.ActivateInput();
            playerInput.currentActionMap?.Enable();
            return mouse;
        }

        private static bool TryFindClosedPlanetContour(Camera camera, Collider proxy, out float2[] contour)
        {
            contour = null;
            int width = Mathf.Max(320, Screen.width);
            int height = Mathf.Max(200, Screen.height);
            int halfWidth = Mathf.Max(28, width / 44);
            int halfHeight = Mathf.Max(20, height / 44);
            for (int y = halfHeight + 16; y < height - halfHeight - 16; y += 16)
            for (int x = halfWidth + 16; x < width - halfWidth - 16; x += 16)
            {
                var candidate = new[]
                {
                    new float2(x - halfWidth, y - halfHeight),
                    new float2(x + halfWidth, y - halfHeight),
                    new float2(x + halfWidth, y + halfHeight),
                    new float2(x - halfWidth, y + halfHeight),
                    new float2(x - halfWidth, y - halfHeight)
                };
                bool valid = true;
                for (int index = 0; index < candidate.Length; index++)
                {
                    Vector2 point = new Vector2(candidate[index].x, candidate[index].y);
                    if (proxy.Raycast(camera.ScreenPointToRay(point), out _, 200f)) continue;
                    valid = false;
                    break;
                }
                if (!valid) continue;
                contour = candidate;
                return true;
            }
            return false;
        }

        private static bool TryFindClearTerrainPoint(
            Camera camera,
            Collider proxy,
            Transform player,
            out float2 point)
        {
            point = default;
            int width = Mathf.Max(320, Screen.width);
            int height = Mathf.Max(200, Screen.height);
            for (int y = 24; y < height - 24; y += 12)
            for (int x = 24; x < width - 24; x += 12)
            {
                Ray ray = camera.ScreenPointToRay(new Vector2(x, y));
                if (!proxy.Raycast(ray, out RaycastHit terrainHit, 200f)) continue;
                RaycastHit[] hits = Physics.RaycastAll(ray, terrainHit.distance + 0.1f, ~0, QueryTriggerInteraction.Ignore);
                bool blockedByOtherSurface = false;
                for (int index = 0; index < hits.Length; index++)
                {
                    Collider collider = hits[index].collider;
                    if (collider == null || collider == proxy ||
                        collider.transform.IsChildOf(proxy.transform)) continue;
                    if (player != null && collider.transform.IsChildOf(player)) continue;
                    blockedByOtherSurface = true;
                    break;
                }
                if (blockedByOtherSurface) continue;
                point = new float2(x, y);
                return true;
            }
            return false;
        }

        private static Vector2[] ToVector2Array(IReadOnlyList<float2> points)
        {
            var converted = new Vector2[points.Count];
            for (int index = 0; index < points.Count; index++)
                converted[index] = new Vector2(points[index].x, points[index].y);
            return converted;
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

        private static bool TryFindNearAndFarPlanetStrokes(
            Camera camera,
            Collider proxy,
            Vector3 playerPosition,
            out List<float2> nearStroke,
            out List<float2> farStroke)
        {
            nearStroke = null;
            farStroke = null;
            float nearestDistance = float.PositiveInfinity;
            float farthestDistance = float.NegativeInfinity;
            int width = Mathf.Max(320, Screen.width);
            int height = Mathf.Max(200, Screen.height);
            for (int y = 16; y < height - 16; y += 12)
            {
                int first = -1;
                int last = -1;
                Vector3 firstPoint = default;
                Vector3 lastPoint = default;
                for (int x = 16; x < width - 16; x += 12)
                {
                    if (!proxy.Raycast(
                            camera.ScreenPointToRay(new Vector2(x, y)),
                            out RaycastHit hit,
                            200f)) continue;
                    if (first < 0)
                    {
                        first = x;
                        firstPoint = hit.point;
                    }
                    last = x;
                    lastPoint = hit.point;
                }
                if (last - first < 72) continue;
                float distance = 0.5f * (
                    Vector3.Distance(playerPosition, firstPoint) +
                    Vector3.Distance(playerPosition, lastPoint));
                var stroke = new List<float2>
                {
                    new float2(first + 12, y),
                    new float2(last - 12, y)
                };
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearStroke = stroke;
                }
                if (distance > farthestDistance)
                {
                    farthestDistance = distance;
                    farStroke = stroke;
                }
            }
            return nearStroke != null && farStroke != null && farthestDistance > nearestDistance + 2f;
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
