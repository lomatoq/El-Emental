using System.Collections.Generic;
using Elemental.Authoring.Assets;
using Elemental.Authoring.Bakers;
using Elemental.Authoring.Fracture;
using Elemental.Input.Gestures;
using Elemental.Input.Actions;
using Elemental.Presentation.Camera;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Diagnostics;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Presentation.MotionMatching;
using Elemental.Authoring.Editor.MotionMatching;
using MotionMatching;
using Elemental.Runtime.Physics;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Matter;
using Elemental.Runtime.World;
using Elemental.Runtime.Geometry;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Bending;
using MiniBokeh;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using UnityEngine.VFX;

namespace Elemental.Authoring.Editor
{
    public static class M3EarthCoreSetup
    {
        public const string EarthCoreScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string AbilityFolder = "Assets/Elemental/Content/Abilities/";
        private const string PreviewMaterialPath = "Assets/Elemental/Content/Materials/MagicPreview.mat";
        private const string StylePath = "Assets/Elemental/Content/VisualStyles/EarthCoreVisualStyle.asset";
        private const string HudPath = "Assets/Elemental/Content/UI/EarthCoreHud.uxml";
        private const string HudPanelPath = "Assets/Elemental/Content/UI/EarthCorePanelSettings.asset";
        private const string VolumeProfilePath = "Assets/Elemental/Content/VisualStyles/EarthCoreVolumeProfile.asset";
        private const string WallMeshPath = "Assets/Elemental/Content/Meshes/ChippedEarthWall.asset";
        private const string PillarMeshPath = "Assets/Elemental/Content/Meshes/BeveledEarthPillar.asset";
        private const string FragmentMeshPath = "Assets/Elemental/Content/Meshes/ChunkyEarthFragment.asset";
        private const string WallProfilePath = "Assets/Elemental/Content/Profiles/EarthWallProfile.asset";
        private const string RockProfilePath = "Assets/Elemental/Content/Profiles/EarthRockProfile.asset";
        private const string WaveProfilePath = "Assets/Elemental/Content/Profiles/EarthPillarWaveProfile.asset";
        private const string VectorFieldProfilePath = "Assets/Elemental/Content/Profiles/EarthVectorFieldProfile.asset";
        private const string PlatformProfilePath = "Assets/Elemental/Content/Profiles/EarthPlatformProfile.asset";
        private const string LandingCushionProfilePath = "Assets/Elemental/Content/Profiles/EarthLandingCushionProfile.asset";
        private const string HoverProfilePath = "Assets/Elemental/Content/Profiles/EarthHoverProfile.asset";
        private const string GravityWellProfilePath = "Assets/Elemental/Content/Profiles/EarthGravityWellProfile.asset";
        private const string RepairProfilePath = "Assets/Elemental/Content/Profiles/EarthRepairProfile.asset";
        private const string CelestialProfilePath = "Assets/Elemental/Content/Profiles/CelestialSystemProfile.asset";
        private const string AtmosphereProfilePath = "Assets/Elemental/Content/Profiles/AtmosphereProfile.asset";
        private const string SkyProfilePath = "Assets/Elemental/Content/Profiles/EarthSkyProfile.asset";
        private const string MeteorProfilePath = "Assets/Elemental/Content/Profiles/MeteorShowerProfile.asset";
        private const string CharacterProfilePath = "Assets/Elemental/Content/Profiles/CharacterPresentationProfile.asset";
        internal const string CharacterImpactProfilePath =
            "Assets/Elemental/Content/Profiles/CharacterImpactResponseProfile.asset";
        private const string PlayerMaterialFolder = "Assets/Elemental/Content/Materials/MvpPlayer";
        private const string RivalMaterialFolder = "Assets/Elemental/Content/Materials/MvpRival";
        private const string PhysicsFeelProfilePath = "Assets/Elemental/Content/Profiles/EarthPhysicsFeelProfile.asset";
        private const string QuickCastProfilePath = "Assets/Elemental/Content/Profiles/EarthQuickCastProfile.asset";
        private const string ArmorProfilePath = "Assets/Elemental/Content/Profiles/EarthArmorProfile.asset";
        private const string ArmorShellPath = "Assets/Elemental/Content/Profiles/EarthArmorShellDefinition.asset";
        private const string ResonanceProfilePath = "Assets/Elemental/Content/Profiles/EarthResonanceProfile.asset";
        private const string SurfProfilePath = "Assets/Elemental/Content/Profiles/EarthSurfProfile.asset";
        private const string StructureFractureProfilePath = "Assets/Elemental/Content/Profiles/EarthStructureFractureProfile.asset";
        private const string EarthMaterialProfilePath = "Assets/Elemental/Content/Profiles/EarthMaterialProfile.asset";
        private const string EarthFeedbackProfilePath = "Assets/Elemental/Content/Profiles/EarthFeedbackProfile.asset";
        public const string EarthEffectsProfilePath =
            "Assets/Elemental/Content/Profiles/EarthEffectsTuningProfile.asset";
        private const string GestureProfilePath = "Assets/Elemental/Content/Profiles/EarthGestureProfile.asset";
        private const string TechniquePresentationProfilePath =
            "Assets/Elemental/Content/Profiles/EarthTechniquePresentationProfile.asset";
        private const string MotorFeelProfilePath = "Assets/Elemental/Content/Profiles/PlanetMotorFeelProfile.asset";
        private const string EarthCameraProfilePath = "Assets/Elemental/Content/Profiles/EarthCameraProfile.asset";
        private const string ShapeGrammarProfilePath = "Assets/Elemental/Content/Profiles/EarthShapeGrammarProfile.asset";
        private const string EarthStoneAlbedoPath = "Assets/Elemental/Content/Textures/EarthStoneAlbedo.png";
        private const string RumbleMaterialFolder = "Assets/Elemental/Content/GraphicsV5/Materials/";
        private const string RumbleRockFolder = "Assets/Elemental/Content/GraphicsV5/Rocks/";
        private const string RumbleShaderName = "Elemental/Graphics V5/Rumble Rock Lit";
        private const string CharacterModelPath =
            "Assets/Elemental/Content/Characters/Linebreaker/Linebreaker.fbx";
        private const string MixamoWalkPath = "Assets/ThirdParty/Mixamo/X Bot@Walking.fbx";
        private const string MixamoWalkBackPath = "Assets/ThirdParty/Mixamo/X Bot@Walking Backwards.fbx";
        private const string MixamoPunchPath = "Assets/ThirdParty/Mixamo/X Bot@Punching.fbx";
        private const string MixamoIdlePath = "Assets/ThirdParty/Mixamo/X Bot@Idle.fbx";
        private const string MixamoTurnPath = "Assets/ThirdParty/Mixamo/X Bot@Left Turn.fbx";
        private const string EammLibraryPath =
            "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibrary.asset";
        private const string EammDataPath =
            "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibraryData.asset";
        private const string EammSearchPath =
            "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibraryData_EnvironmentSearch.asset";
        private const string EammRuntimeProfilePath =
            "Assets/Elemental/Content/Profiles/EAMMRuntimeProfile.asset";
        private const string MageControllerPath = "Assets/Elemental/Content/Animation/KayKitMage.controller";
        private const string MageMaskPath = "Assets/Elemental/Content/Animation/KayKitMageUpperBody.mask";

        [MenuItem("Elemental/Setup/Create M3 Earth Core Slice")]
        public static void Configure()
        {
            EarthRenderQualitySetup.ConfigureProfiles();
            M2VoxelPlanetSetup.Configure();
            Scene scene = SceneManager.GetActiveScene();
            PlanetWorldProfile worldProfile = M2VoxelPlanetSetup.CreateOrLoadWorldProfile();

            VoxelPlanetBehaviour voxelPlanet = Object.FindAnyObjectByType<VoxelPlanetBehaviour>();
            GravityWorldBehaviour gravityWorld = Object.FindAnyObjectByType<GravityWorldBehaviour>();
            GameObject collisionProxy = GameObject.Find("Planet Collision Proxy");
            GameObject character = GameObject.Find("Planet Character");
            UnityEngine.Camera camera = Object.FindAnyObjectByType<UnityEngine.Camera>();
            if (voxelPlanet == null || gravityWorld == null || collisionProxy == null || character == null || camera == null)
            {
                throw new UnityEditor.Build.BuildFailedException("M2 scene dependencies are missing.");
            }

            Material earthMaterial = LoadRumbleMaterial("RumbleGround.mat") ??
                                     AssetDatabase.LoadAssetAtPath<Material>(
                                         "Assets/Elemental/Content/Materials/VoxelPlanetSurface.mat");
            EarthCoreVisualStyle style = CreateOrLoadVisualStyle();
            EarthEffectsTuningProfile effectsProfile = CreateOrLoadEffectsProfile();
            EarthMaterialProfile earthMaterialProfile = CreateOrLoadProfile<EarthMaterialProfile>(
                EarthMaterialProfilePath,
                "Earth Material Profile");
            if (!IsRumbleMaterial(earthMaterial))
                ApplyEarthMaterial(earthMaterial, style, earthMaterialProfile, false);
            earthMaterial.SetFloat("_UsePlanetFrame", 1f);
            voxelPlanet.Configure(worldProfile, earthMaterial);
            Material looseEarthMaterial = LoadRumbleMaterial("RumbleSandstone.mat") ??
                                          CreateOrLoadEarthMaterial(
                                              "EarthLooseStone.mat", style.StoneColor,
                                              style.StoneSmoothness, style.StoneEmission * 0.35f);
            if (!IsRumbleMaterial(looseEarthMaterial))
                earthMaterialProfile.Apply(looseEarthMaterial, false);
            looseEarthMaterial.SetFloat("_UsePlanetFrame", 0f);
            Transform heldFragmentAnchor = CreateHeldFragmentAnchor(character);
            GameObject magicRoot = new GameObject("Earth Magic Runtime");
            magicRoot.SetActive(false);
            EarthMatterKernelBehaviour matterKernel = magicRoot.AddComponent<EarthMatterKernelBehaviour>();
            EarthSurfaceQueryService surfaceQueries = magicRoot.AddComponent<EarthSurfaceQueryService>();
            VoxelPlanetEarthSurfaceProvider planetSurface =
                collisionProxy.GetComponent<VoxelPlanetEarthSurfaceProvider>();
            if (planetSurface == null)
                planetSurface = collisionProxy.AddComponent<VoxelPlanetEarthSurfaceProvider>();
            planetSurface.Configure(collisionProxy.GetComponent<Collider>(), voxelPlanet, surfaceQueries);
            Mesh[] fragmentMeshes = CreateOrLoadFragmentMeshes();
            Mesh[] debrisMeshes = CreateOrLoadDebrisMeshes();
            Mesh fragmentMesh = fragmentMeshes[0];
            EarthRockProfile rockProfile = CreateOrLoadRockProfile();
            EarthPhysicsFeelProfile physicsFeel = CreateOrLoadProfile<EarthPhysicsFeelProfile>(
                PhysicsFeelProfilePath,
                "Earth Physics Feel Profile");
            EarthRockDebrisPool debrisPool = magicRoot.AddComponent<EarthRockDebrisPool>();
            debrisPool.Configure(72, looseEarthMaterial, debrisMeshes[0], gravityWorld, rockProfile);
            debrisPool.ConfigureMeshVariants(debrisMeshes);
            EarthShapeGrammarProfile shapeGrammar = CreateOrLoadProfile<EarthShapeGrammarProfile>(
                ShapeGrammarProfilePath, "Earth Shape Grammar Profile");
            debrisPool.ConfigureShapeGrammar(shapeGrammar);
            EarthFragmentPool pool = magicRoot.AddComponent<EarthFragmentPool>();
            pool.Configure(32, looseEarthMaterial, gravityWorld, fragmentMesh, rockProfile, debrisPool);
            pool.ConfigureMeshVariants(fragmentMeshes);
            pool.ConfigureShapeGrammar(shapeGrammar);
            pool.ConfigurePhysicsFeel(physicsFeel);
            EarthHoverProfile hoverProfile = CreateOrLoadHoverProfile();
            pool.ConfigureHover(hoverProfile);
            Mesh wallMesh = CreateOrLoadChippedWallMesh();
            Material wallMaterial = LoadRumbleMaterial("RumbleSandstone.mat") ??
                                    CreateOrLoadEarthMaterial(
                                        "EarthWall.mat", style.StoneColor * 0.95f, 0.05f,
                                        style.StoneEmission * 0.5f);
            if (!IsRumbleMaterial(wallMaterial)) earthMaterialProfile.Apply(wallMaterial, false);
            Material fractureInteriorMaterial = LoadRumbleMaterial("RumbleSandstone.mat") ??
                                                CreateOrLoadEarthMaterial(
                                                    "EarthFractureInterior.mat",
                                                    earthMaterialProfile.FreshInteriorTint, 0.025f,
                                                    Color.black);
            if (!IsRumbleMaterial(fractureInteriorMaterial))
                earthMaterialProfile.Apply(fractureInteriorMaterial, true);
            EarthWallPool wallPool = magicRoot.AddComponent<EarthWallPool>();
            wallPool.Configure(8, wallMesh, wallMaterial, CreateOrLoadWallProfile());
            wallPool.ConfigureNaturalFracture(debrisPool);
            wallPool.ConfigureGravity(gravityWorld);
            wallPool.ConfigureShapeGrammar(shapeGrammar);
            EarthStructureFractureProfile structureFracture =
                CreateOrLoadProfile<EarthStructureFractureProfile>(
                    StructureFractureProfilePath,
                    "Earth Structure Fracture Profile");
            wallPool.ConfigureStructureFracture(structureFracture);
            wallPool.ConfigureSurfaceQueries(surfaceQueries);
            wallPool.ConfigureFractureMaterials(wallMaterial, fractureInteriorMaterial);
            wallPool.ConfigurePhysicsFeel(physicsFeel);
            wallPool.ConfigureRepair(CreateOrLoadProfile<EarthRepairProfile>(
                RepairProfilePath,
                "Earth Repair Profile"));
            EarthFractureAsset wallFracture = EarthFractureBaker.CreateOrLoadProductionWall(
                wallMesh, wallMesh);
            wallPool.ConfigureFractureAsset(wallFracture, false);
            EarthPlatformProfile platformProfile = CreateOrLoadPlatformProfile();
            EarthPlatformPool platformPool = magicRoot.AddComponent<EarthPlatformPool>();
            platformPool.Configure(6, wallMaterial, platformProfile);
            platformPool.ConfigureGravity(gravityWorld);
            platformPool.ConfigureFractureProfile(structureFracture);
            platformPool.ConfigureSurfaceQueries(surfaceQueries);
            platformPool.ConfigurePhysicsFeel(physicsFeel);
            platformPool.ConfigurePieceMeshes(fragmentMeshes);
            platformPool.PrewarmAll();
            EarthPillarWaveProfile waveProfile = CreateOrLoadWaveProfile();
            waveProfile.ConfigureMotionMode(WaveMotionMode.PremiumVisual);
            EditorUtility.SetDirty(waveProfile);
            EarthPillarWavePool wavePool = magicRoot.AddComponent<EarthPillarWavePool>();
            wavePool.Configure(96, wallMesh, wallMaterial, collisionProxy.transform, waveProfile);
            wavePool.ConfigureSurfaceQueries(surfaceQueries);
            wavePool.ConfigureMeshVariants(fragmentMeshes);
            EarthTelekinesisController telekinesis = magicRoot.AddComponent<EarthTelekinesisController>();
            telekinesis.ConfigureHover(hoverProfile, collisionProxy.transform);
            MagicExecutor executor = magicRoot.AddComponent<MagicExecutor>();
            executor.Configure(voxelPlanet, pool, collisionProxy.transform, wallPool, heldFragmentAnchor);
            executor.ConfigureTelekinesis(telekinesis);
            executor.ConfigureEarthExtensions(
                CreateOrLoadVectorFieldProfile(), platformPool, CreateOrLoadGravityWellProfile());
            executor.ConfigureWallProfile(1.5f, 4.0f, 14f, 0.95f);
            EarthAudioDirector audioDirector = magicRoot.AddComponent<EarthAudioDirector>();
            audioDirector.Configure(executor);
            Material indirectDebrisMaterial = CreateOrLoadShaderMaterial(
                "EarthIndirectDebris.mat", "Elemental/Earth Indirect Debris");
            indirectDebrisMaterial.SetColor("_BaseColor", style.StoneColor * 0.82f);
            EarthIndirectDebrisRenderer indirectDebris = magicRoot.AddComponent<EarthIndirectDebrisRenderer>();
            indirectDebris.Configure(executor, debrisMeshes[0], indirectDebrisMaterial);
            EarthPerformanceTelemetry performanceTelemetry = magicRoot.AddComponent<EarthPerformanceTelemetry>();
            performanceTelemetry.Configure(matterKernel, indirectDebris);
            EarthVfxGraphBridge vfxGraphBridge = magicRoot.AddComponent<EarthVfxGraphBridge>();
            VisualEffect impactGraph = CreateVfxGraphLayer(magicRoot.transform, "Earth Impact VFX Graph");
            VisualEffect returnGraph = CreateVfxGraphLayer(magicRoot.transform, "Earth Return VFX Graph");
            vfxGraphBridge.Configure(executor, impactGraph, returnGraph);
            AbilityRecipeAsset[] recipes = CreateOrLoadRecipes();
            AbilityRegistryBootstrap registry = magicRoot.AddComponent<AbilityRegistryBootstrap>();
            registry.Configure(executor, recipes);
            magicRoot.SetActive(true);

            character.SetActive(false);
            PlayerInput playerInput = character.GetComponent<PlayerInput>();
            EarthInputAdapter inputAdapter = character.GetComponent<EarthInputAdapter>();
            if (inputAdapter == null) inputAdapter = character.AddComponent<EarthInputAdapter>();
            inputAdapter.Configure(playerInput);
            Rigidbody characterBody = character.GetComponent<Rigidbody>();
            PlanetMotor characterMotor = character.GetComponent<PlanetMotor>();
            EarthPillarMobility pillarMobility = character.GetComponent<EarthPillarMobility>();
            if (pillarMobility == null) pillarMobility = character.AddComponent<EarthPillarMobility>();
            SerializedObject pillarSettings = new SerializedObject(pillarMobility);
            pillarSettings.FindProperty("minimumVelocityChange").floatValue = 12f;
            pillarSettings.FindProperty("maximumVelocityChange").floatValue = 25f;
            pillarSettings.FindProperty("chargeExponent").floatValue = 1.55f;
            pillarSettings.ApplyModifiedPropertiesWithoutUndo();
            pillarMobility.Configure(characterBody, characterMotor, surfaceQueries);
            EarthPillarWaveAbility pillarWave = character.GetComponent<EarthPillarWaveAbility>();
            if (pillarWave == null) pillarWave = character.AddComponent<EarthPillarWaveAbility>();
            pillarWave.Configure(characterBody, characterMotor, wavePool, waveProfile);
            EarthLandingCushion cushion = character.GetComponent<EarthLandingCushion>();
            if (cushion == null) cushion = character.AddComponent<EarthLandingCushion>();
            Transform cushionVisual = CreateLandingCushionVisual(wallMesh, wallMaterial);
            cushion.Configure(
                characterBody,
                characterMotor,
                character.GetComponent<ActiveRagdollPuppet>(),
                collisionProxy.GetComponent<Collider>(),
                CreateOrLoadLandingCushionProfile(),
                cushionVisual,
                surfaceQueries);
            PlanetInputReader inputReader = character.GetComponent<PlanetInputReader>();
            inputReader?.Configure(inputAdapter, pillarMobility, pillarWave, cushion);
            LineRenderer preview = character.GetComponent<LineRenderer>();
            if (preview == null)
            {
                preview = character.AddComponent<LineRenderer>();
            }

            preview.useWorldSpace = true;
            preview.loop = false;
            preview.widthMultiplier = 0.08f;
            preview.sharedMaterial = CreateOrLoadPreviewMaterial();
            preview.positionCount = 0;
            MagicInputController input = character.GetComponent<MagicInputController>();
            if (input == null)
            {
                input = character.AddComponent<MagicInputController>();
            }

            input.Configure(
                playerInput,
                camera,
                executor,
                collisionProxy.GetComponent<Collider>(),
                preview);
            input.ConfigureGestureProfile(CreateOrLoadProfile<EarthGestureProfile>(
                GestureProfilePath,
                "Earth Gesture Profile"));
            input.ConfigureEarthTechniques(pillarWave);
            input.ConfigureEarthSurfaceQueries(surfaceQueries);
            EarthArmorProfile armorProfile = CreateOrLoadProfile<EarthArmorProfile>(
                ArmorProfilePath,
                "Earth Armor Profile");
            EarthArmorShellDefinition armorShell = CreateOrLoadProfile<EarthArmorShellDefinition>(
                ArmorShellPath,
                "Earth Armor Shell Definition");
            if (!armorShell.IsValid)
            {
                armorShell.BakeDefaultHumanoidShell();
                EditorUtility.SetDirty(armorShell);
            }
            armorProfile.ConfigureShellDefinition(armorShell);
            EditorUtility.SetDirty(armorProfile);
            input.ConfigureEarthFeatureProfiles(
                CreateOrLoadProfile<EarthQuickCastProfile>(QuickCastProfilePath, "Earth Quick Cast Profile"),
                armorProfile);
            EarthResonanceController resonance = character.GetComponent<EarthResonanceController>();
            if (resonance == null) resonance = character.AddComponent<EarthResonanceController>();
            resonance.Configure(
                characterBody,
                characterMotor,
                collisionProxy.transform,
                pool,
                executor,
                CreateOrLoadProfile<EarthResonanceProfile>(ResonanceProfilePath, "Earth Resonance Profile"));
            EarthSurfController surf = character.GetComponent<EarthSurfController>();
            if (surf == null) surf = character.AddComponent<EarthSurfController>();
            surf.Configure(
                characterBody,
                characterMotor,
                collisionProxy.transform,
                CreateOrLoadProfile<EarthSurfProfile>(SurfProfilePath, "Earth Surf Profile"),
                looseEarthMaterial,
                effectsProfile.Materials.SurfDust,
                effectsProfile);
            CreateOrLoadProfile<EarthTechniquePresentationProfile>(
                TechniquePresentationProfilePath,
                "Earth Technique Presentation Profile");
            character.SetActive(true);

            MagicFeedbackRouter feedback = camera.GetComponent<MagicFeedbackRouter>();
            if (feedback == null)
            {
                feedback = camera.gameObject.AddComponent<MagicFeedbackRouter>();
            }

            feedback.Configure(executor);
            ConfigurePresentation(
                scene, character, camera, executor, input, pillarMobility, cushion, preview, style, looseEarthMaterial,
                gravityWorld, debrisPool, wavePool, collisionProxy.transform, worldProfile);
            CreateMvpLinebreaker(
                gravityWorld,
                worldProfile.Radius,
                character,
                collisionProxy.transform,
                pool);
            // Both fighters must exist before the arena's final collision seating.
            // Creating the bot afterwards left it inside the gate and floor.
            BrokenCrownArenaSceneIntegrator.Integrate(
                collisionProxy.transform.position,
                worldProfile.Radius,
                gravityWorld,
                debrisPool,
                looseEarthMaterial);
            RestoreApprovedLinebreakerSpawn(
                collisionProxy.transform.position,
                worldProfile.Radius);
            GameObject focusEnemy = GameObject.Find("Rumble Linebreaker Bot");
            Transform enemyFocusProxy = focusEnemy != null
                ? focusEnemy.transform.Find("EnemyFocusProxy")
                : null;
            RepairGameplayCameraWiring(camera, character, focusEnemy);
            EarthCinematicDepthOfFieldController cinematicDepthOfField =
                camera.GetComponent<EarthCinematicDepthOfFieldController>();
            if (cinematicDepthOfField != null)
                EditorUtility.SetDirty(cinematicDepthOfField);
            ConfigureMiniBokeh(camera, enemyFocusProxy);
            CreatePushBoulders(
                gravityWorld,
                looseEarthMaterial,
                worldProfile.Radius,
                physicsFeel,
                fragmentMeshes);
            if (!EarthParticleMaterialValidator.ValidateScene(scene, out string particleError))
                throw new UnityEditor.Build.BuildFailedException(particleError);
            EditorSceneManager.SaveScene(scene, EarthCoreScenePath);
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(item => item.path == EarthCoreScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(EarthCoreScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Elemental] M3 Earth Core Slice configured.");
        }

        [MenuItem("Elemental/Setup/Integrate EAMM Into Current Earth Core Slice")]
        public static void IntegrateEammIntoCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                throw new UnityEditor.Build.BuildFailedException("Open EarthCoreSlice before integrating EAMM.");

            GameObject player = GameObject.Find("Planet Character");
            GameObject bot = GameObject.Find("Rumble Linebreaker Bot");
            if (player == null || bot == null)
                throw new UnityEditor.Build.BuildFailedException(
                    "Current scene must contain Planet Character and Rumble Linebreaker Bot.");

            IntegrateEammActor(player, true);
            IntegrateEammActor(bot, false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[EAMM] Integrated the baked base-pose source into player and bot without rebuilding arena or render authoring.");
        }

        [MenuItem("Elemental/Setup/Repair Earth Ability Registry Wiring")]
        public static void RepairEarthAbilityRegistryWiring()
        {
            if (Application.isPlaying)
                throw new System.InvalidOperationException(
                    "Stop Play Mode before repairing the Earth ability registry.");

            MagicExecutor executor =
                UnityEngine.Object.FindAnyObjectByType<MagicExecutor>(FindObjectsInactive.Include);
            if (executor == null)
                throw new System.InvalidOperationException(
                    "Earth ability registry repair requires the existing MagicExecutor.");
            AbilityRegistryBootstrap registry = executor.GetComponent<AbilityRegistryBootstrap>();
            if (registry == null)
                registry = executor.gameObject.AddComponent<AbilityRegistryBootstrap>();
            registry.Configure(executor, CreateOrLoadRecipes());
            EditorUtility.SetDirty(registry);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Elemental] Earth ability registry runtime wiring repaired.");
        }

        [MenuItem("Elemental/Setup/Repair Character Test Runtime Wiring")]
        public static void RepairCharacterTestRuntimeWiring()
        {
            if (Application.isPlaying)
                throw new System.InvalidOperationException(
                    "Stop Play Mode before repairing character test wiring.");

            NormalizeLoadedSceneMaterialShaderState();

            // Keep the rendered voxel shell on the same canonical world profile as
            // gravity, spawns and the authored arena. Older generated scenes can
            // retain VoxelPlanetBehaviour's one-metre defaults after a partial
            // wiring repair, which leaves gameplay at the real surface radius while
            // the visible planet is stranded at the world origin.
            PlanetWorldProfile worldProfile = M2VoxelPlanetSetup.CreateOrLoadWorldProfile();
            VoxelPlanetBehaviour voxelPlanet =
                UnityEngine.Object.FindAnyObjectByType<VoxelPlanetBehaviour>(FindObjectsInactive.Include);
            Material planetMaterial = LoadRumbleMaterial("RumbleGround.mat") ??
                                      AssetDatabase.LoadAssetAtPath<Material>(
                                          "Assets/Elemental/Content/Materials/VoxelPlanetSurface.mat");
            if (voxelPlanet == null || worldProfile == null || planetMaterial == null)
                throw new System.InvalidOperationException(
                    "Character test repair requires the voxel planet, canonical world profile and planet material.");
            voxelPlanet.Configure(worldProfile, planetMaterial);
            EditorUtility.SetDirty(voxelPlanet);

            GravityWorldBehaviour gravityWorld =
                UnityEngine.Object.FindAnyObjectByType<GravityWorldBehaviour>(FindObjectsInactive.Include);
            PointPlanetGravitySource[] gravitySources =
                UnityEngine.Object.FindObjectsByType<PointPlanetGravitySource>(
                    FindObjectsInactive.Include);
            GameObject player = GameObject.Find("Planet Character");
            GameObject bot = GameObject.Find("Rumble Linebreaker Bot");
            UnityEngine.Camera camera = UnityEngine.Camera.main ??
                UnityEngine.Object.FindAnyObjectByType<UnityEngine.Camera>(FindObjectsInactive.Include);
            if (gravityWorld == null || gravitySources.Length == 0 ||
                player == null || bot == null || camera == null)
                throw new System.InvalidOperationException(
                    "Character test repair requires gravity world/source, player, bot and game camera.");

            for (int index = 0; index < gravitySources.Length; index++)
            {
                PointPlanetGravitySource gravitySource = gravitySources[index];
                if (gravitySource == null) continue;
                gravitySource.Configure(worldProfile);
                EditorUtility.SetDirty(gravitySource);
            }
            gravityWorld.Configure(gravitySources);
            RepairActorGravityAndMotor(
                player,
                gravityWorld,
                player.GetComponent<PlanetInputReader>(),
                camera.transform,
                true);
            RepairActorGravityAndMotor(
                bot,
                gravityWorld,
                bot.GetComponent<EarthMvpBotController>(),
                bot.transform,
                false);

            Vector3 arenaCenter = BrokenCrownArenaSceneIntegrator.RepairCharacterTestSpawns(
                player,
                bot,
                gravitySources[0].transform.position);
            BrokenCrownArenaSceneIntegrator.RepairCurrentSceneRuntimeWiring();
            EarthMvpBotController botController = bot.GetComponent<EarthMvpBotController>();
            botController.Configure(
                player.transform,
                player.GetComponent<Rigidbody>(),
                player.GetComponent<PhysicalImpactTarget>(),
                player.GetComponent<ActiveRagdollPuppet>(),
                gravitySources[0].transform,
                bot.GetComponent<Rigidbody>(),
                bot.GetComponent<PlanetMotor>(),
                bot.GetComponent<EarthCombatDummy>(),
                arenaCenter,
                6.5f);

            PlanetCameraRig cameraRig = camera.GetComponent<PlanetCameraRig>();
            if (cameraRig == null) cameraRig = camera.gameObject.AddComponent<PlanetCameraRig>();
            cameraRig.Configure(player.transform, player.GetComponent<Rigidbody>(), gravityWorld);
            RepairGameplayCameraWiring(camera, player, bot);
            RepairMagicRuntimeCoreWiring(voxelPlanet, gravityWorld, player);
            RepairPlayerInputAndMagicWiring(player, camera);
            RepairCharacterCombatAndLandingWiring(
                player,
                bot,
                gravityWorld,
                arenaCenter);

            RepairCanonicalEffectsProfileWiring(CreateOrLoadEffectsProfile());
            RepairEarthMobilityVisualBindings(
                player,
                GameObject.Find("Planet Collision Proxy").transform,
                cameraRig,
                UnityEngine.Object.FindAnyObjectByType<EarthPillarFeedback>(FindObjectsInactive.Include));

            IntegrateEammActor(player, true);
            IntegrateEammActor(bot, false);
            EditorUtility.SetDirty(gravityWorld);
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(bot);
            EditorUtility.SetDirty(camera.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[Elemental] Character test runtime wiring repaired: camera, radial gravity, motors and EAMM bridges.");
        }

        [MenuItem("Elemental/Setup/Repair Surf And Launch Pillar Bindings")]
        public static void RepairSurfAndLaunchPillarBindings()
        {
            if (Application.isPlaying)
                throw new System.InvalidOperationException("Stop Play Mode before repairing mobility bindings.");
            GameObject planet = GameObject.Find("Planet Collision Proxy");
            int changed = RepairEarthMobilityVisualBindings(
                GameObject.Find("Planet Character"),
                planet != null ? planet.transform : null,
                UnityEngine.Object.FindAnyObjectByType<PlanetCameraRig>(FindObjectsInactive.Include),
                UnityEngine.Object.FindAnyObjectByType<EarthPillarFeedback>(FindObjectsInactive.Include));
            if (changed > 0)
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Elemental] Restored {changed} missing surf/launch-pillar bindings; existing tuning and transforms preserved.");
        }

        /// <summary>
        /// Repairs only absent references, without rebuilding the scene, material assets,
        /// runtime surf views, or authored launch geometry. Returns changed components.
        /// </summary>
        public static int RepairEarthMobilityVisualBindings(
            GameObject player,
            Transform planetCenter,
            PlanetCameraRig cameraRig,
            EarthPillarFeedback feedback)
        {
            if (Application.isPlaying)
                throw new System.InvalidOperationException("Mobility binding repair is an Edit Mode operation.");
            EarthSurfController surf = player != null ? player.GetComponent<EarthSurfController>() : null;
            EarthPillarMobility mobility = player != null ? player.GetComponent<EarthPillarMobility>() : null;
            if (surf == null || mobility == null || feedback == null)
                throw new System.InvalidOperationException(
                    "Mobility repair requires the existing player surf/mobility components and Earth Pillar Feedback; it does not generate replacements.");

            var surfData = new SerializedObject(surf);
            var feedbackData = new SerializedObject(feedback);
            EarthEffectsTuningProfile surfEffects =
                surfData.FindProperty("effectsProfile").objectReferenceValue as EarthEffectsTuningProfile ??
                AssetDatabase.LoadAssetAtPath<EarthEffectsTuningProfile>(EarthEffectsProfilePath);
            EarthEffectsTuningProfile pillarEffects =
                feedbackData.FindProperty("effectsProfile").objectReferenceValue as EarthEffectsTuningProfile ?? surfEffects;
            FillMissingMobilityReference(surfData, "casterBody", player.GetComponent<Rigidbody>());
            FillMissingMobilityReference(surfData, "motor", player.GetComponent<PlanetMotor>());
            FillMissingMobilityReference(surfData, "planetCenter", planetCenter);
            FillMissingMobilityReference(surfData, "profile", AssetDatabase.LoadAssetAtPath<EarthSurfProfile>(SurfProfilePath));
            FillMissingMobilityReference(surfData, "effectsProfile", surfEffects);
            FillMissingMobilityReference(surfData, "material", LoadRumbleMaterial("RumbleSandstone.mat"));
            FillMissingMobilityReference(surfData, "dustMaterial", surfEffects != null ? surfEffects.Materials.SurfDust : null);

            FillMissingMobilityReference(feedbackData, "mobility", mobility);
            FillMissingMobilityReference(feedbackData, "pillar", FindDescendantByName(feedback.transform, "Rising Earth Pillar"));
            FillMissingMobilityReference(feedbackData, "cameraRig", cameraRig);
            FillMissingMobilityReference(feedbackData, "effectsProfile", pillarEffects);
            var existingChips = new List<Transform>();
            foreach (Transform child in feedback.GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("Lift Ground Chip ", System.StringComparison.Ordinal))
                    existingChips.Add(child);
            SerializedProperty chips = feedbackData.FindProperty("groundChips");
            if (chips.arraySize == 0)
            {
                if (existingChips.Count == 0)
                    throw new System.InvalidOperationException("Launch feedback has no authored Lift Ground Chip children to bind.");
                chips.arraySize = existingChips.Count;
            }
            for (int index = 0; index < chips.arraySize; index++)
            {
                SerializedProperty chip = chips.GetArrayElementAtIndex(index);
                if (chip.objectReferenceValue != null) continue;
                if (index >= existingChips.Count)
                    throw new System.InvalidOperationException($"No existing launch chip is available for slot {index}; restore the authored child.");
                chip.objectReferenceValue = existingChips[index];
            }

            int changed = 0;
            if (surfData.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(surf);
                EditorSceneManager.MarkSceneDirty(surf.gameObject.scene);
                changed++;
            }
            if (feedbackData.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(feedback);
                EditorSceneManager.MarkSceneDirty(feedback.gameObject.scene);
                changed++;
            }
            return changed;
        }

        private static void FillMissingMobilityReference(
            SerializedObject target, string propertyName, UnityEngine.Object fallback)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property.objectReferenceValue != null) return;
            if (fallback == null)
                throw new System.InvalidOperationException(
                    $"Cannot repair {target.targetObject.name}.{propertyName}: the existing authored dependency/asset is missing.");
            property.objectReferenceValue = fallback;
        }

        [MenuItem("Elemental/Setup/Repair Gameplay Camera Wiring")]
        public static void RepairGameplayCameraOnly()
        {
            if (Application.isPlaying)
                throw new System.InvalidOperationException(
                    "Stop Play Mode before repairing gameplay camera wiring.");

            GameObject player = GameObject.Find("Planet Character");
            GameObject bot = GameObject.Find("Rumble Linebreaker Bot");
            UnityEngine.Camera camera = UnityEngine.Camera.main ??
                UnityEngine.Object.FindAnyObjectByType<UnityEngine.Camera>(FindObjectsInactive.Include);
            GravityWorldBehaviour gravityWorld =
                UnityEngine.Object.FindAnyObjectByType<GravityWorldBehaviour>(FindObjectsInactive.Include);
            if (player == null || camera == null || gravityWorld == null)
                throw new System.InvalidOperationException(
                    "Gameplay camera repair requires the player, main camera and gravity world.");

            PlanetCameraRig cameraRig = camera.GetComponent<PlanetCameraRig>();
            if (cameraRig == null) cameraRig = camera.gameObject.AddComponent<PlanetCameraRig>();
            cameraRig.Configure(player.transform, player.GetComponent<Rigidbody>(), gravityWorld);
            RepairGameplayCameraWiring(camera, player, bot);
            EditorUtility.SetDirty(camera.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Elemental] Gameplay camera target, spherical world-up frame and aim pivot repaired.");
        }

        private static void RepairMagicRuntimeCoreWiring(
            VoxelPlanetBehaviour voxelPlanet,
            GravityWorldBehaviour gravityWorld,
            GameObject player)
        {
            GameObject magicRoot = GameObject.Find("Earth Magic Runtime");
            GameObject collisionProxy = GameObject.Find("Planet Collision Proxy");
            if (magicRoot == null || collisionProxy == null ||
                collisionProxy.GetComponent<Collider>() == null)
                throw new System.InvalidOperationException(
                    "Magic runtime repair requires the existing Earth Magic Runtime and planet collision proxy.");

            EarthSurfaceQueryService surfaceQueries =
                magicRoot.GetComponent<EarthSurfaceQueryService>();
            EarthRockDebrisPool debrisPool = magicRoot.GetComponent<EarthRockDebrisPool>();
            EarthFragmentPool fragmentPool = magicRoot.GetComponent<EarthFragmentPool>();
            EarthWallPool wallPool = magicRoot.GetComponent<EarthWallPool>();
            EarthPlatformPool platformPool = magicRoot.GetComponent<EarthPlatformPool>();
            EarthPillarWavePool wavePool = magicRoot.GetComponent<EarthPillarWavePool>();
            EarthTelekinesisController telekinesis =
                magicRoot.GetComponent<EarthTelekinesisController>();
            MagicExecutor executor = magicRoot.GetComponent<MagicExecutor>();
            if (surfaceQueries == null || debrisPool == null || fragmentPool == null ||
                wallPool == null || platformPool == null || wavePool == null ||
                telekinesis == null || executor == null)
                throw new System.InvalidOperationException(
                    "Magic runtime repair requires all existing Earth pools and controllers.");

            VoxelPlanetEarthSurfaceProvider planetSurface =
                collisionProxy.GetComponent<VoxelPlanetEarthSurfaceProvider>();
            if (planetSurface == null)
                planetSurface = collisionProxy.AddComponent<VoxelPlanetEarthSurfaceProvider>();
            planetSurface.Configure(
                collisionProxy.GetComponent<Collider>(),
                voxelPlanet,
                surfaceQueries);

            Material looseEarthMaterial = LoadRumbleMaterial("RumbleSandstone.mat") ??
                                          CreateOrLoadEarthMaterial(
                                              "EarthLooseStone.mat",
                                              new Color(0.52f, 0.31f, 0.16f),
                                              0.05f,
                                              Color.black);
            Material wallMaterial = looseEarthMaterial;
            Material fractureInteriorMaterial = LoadRumbleMaterial("RumbleSandstone.mat") ??
                                                wallMaterial;
            Mesh[] fragmentMeshes = CreateOrLoadFragmentMeshes();
            Mesh[] debrisMeshes = CreateOrLoadDebrisMeshes();
            Mesh wallMesh = CreateOrLoadChippedWallMesh();
            EarthRockProfile rockProfile = CreateOrLoadRockProfile();
            EarthPhysicsFeelProfile physicsFeel =
                CreateOrLoadProfile<EarthPhysicsFeelProfile>(
                    PhysicsFeelProfilePath,
                    "Earth Physics Feel Profile");
            EarthShapeGrammarProfile shapeGrammar =
                CreateOrLoadProfile<EarthShapeGrammarProfile>(
                    ShapeGrammarProfilePath,
                    "Earth Shape Grammar Profile");
            EarthHoverProfile hoverProfile = CreateOrLoadHoverProfile();
            EarthStructureFractureProfile structureFracture =
                CreateOrLoadProfile<EarthStructureFractureProfile>(
                    StructureFractureProfilePath,
                    "Earth Structure Fracture Profile");

            debrisPool.Configure(72, looseEarthMaterial, debrisMeshes[0], gravityWorld, rockProfile);
            debrisPool.ConfigureMeshVariants(debrisMeshes);
            debrisPool.ConfigureShapeGrammar(shapeGrammar);
            fragmentPool.Configure(
                32,
                looseEarthMaterial,
                gravityWorld,
                fragmentMeshes[0],
                rockProfile,
                debrisPool);
            fragmentPool.ConfigureMeshVariants(fragmentMeshes);
            fragmentPool.ConfigureShapeGrammar(shapeGrammar);
            fragmentPool.ConfigurePhysicsFeel(physicsFeel);
            fragmentPool.ConfigureHover(hoverProfile);

            wallPool.Configure(8, wallMesh, wallMaterial, CreateOrLoadWallProfile());
            wallPool.ConfigureGravity(gravityWorld);
            wallPool.ConfigureShapeGrammar(shapeGrammar);
            wallPool.ConfigureStructureFracture(structureFracture);
            wallPool.ConfigureSurfaceQueries(surfaceQueries);
            wallPool.ConfigureFractureMaterials(wallMaterial, fractureInteriorMaterial);
            wallPool.ConfigurePhysicsFeel(physicsFeel);
            wallPool.ConfigureRepair(CreateOrLoadProfile<EarthRepairProfile>(
                RepairProfilePath,
                "Earth Repair Profile"));
            wallPool.ConfigureFractureAsset(
                EarthFractureBaker.CreateOrLoadProductionWall(wallMesh, wallMesh),
                false);

            EarthPlatformProfile platformProfile = CreateOrLoadPlatformProfile();
            platformPool.Configure(6, wallMaterial, platformProfile);
            platformPool.ConfigureGravity(gravityWorld);
            platformPool.ConfigureFractureProfile(structureFracture);
            platformPool.ConfigureSurfaceQueries(surfaceQueries);
            platformPool.ConfigurePhysicsFeel(physicsFeel);
            platformPool.ConfigurePieceMeshes(fragmentMeshes);

            EarthPillarWaveProfile waveProfile = CreateOrLoadWaveProfile();
            waveProfile.ConfigureMotionMode(WaveMotionMode.PremiumVisual);
            wavePool.Configure(96, wallMesh, wallMaterial, collisionProxy.transform, waveProfile);
            wavePool.ConfigureSurfaceQueries(surfaceQueries);
            wavePool.ConfigureMeshVariants(fragmentMeshes);
            telekinesis.ConfigureHover(hoverProfile, collisionProxy.transform);

            Transform heldFragmentAnchor = player.transform.Find("Held Earth Anchor");
            if (heldFragmentAnchor == null)
            {
                GameObject anchor = new GameObject("Held Earth Anchor");
                heldFragmentAnchor = anchor.transform;
                heldFragmentAnchor.SetParent(player.transform, false);
                heldFragmentAnchor.localPosition = new Vector3(0.82f, 1.18f, 0.62f);
            }
            executor.Configure(
                voxelPlanet,
                fragmentPool,
                collisionProxy.transform,
                wallPool,
                heldFragmentAnchor);
            executor.ConfigureTelekinesis(telekinesis);
            executor.ConfigureEarthExtensions(
                CreateOrLoadVectorFieldProfile(),
                platformPool,
                CreateOrLoadGravityWellProfile());
            executor.ConfigureWallProfile(1.5f, 4.0f, 14f, 0.95f);

            EditorUtility.SetDirty(planetSurface);
            EditorUtility.SetDirty(debrisPool);
            EditorUtility.SetDirty(fragmentPool);
            EditorUtility.SetDirty(wallPool);
            EditorUtility.SetDirty(platformPool);
            EditorUtility.SetDirty(wavePool);
            EditorUtility.SetDirty(telekinesis);
            EditorUtility.SetDirty(executor);
            EditorUtility.SetDirty(waveProfile);
        }

        private static void RepairPlayerInputAndMagicWiring(
            GameObject player,
            UnityEngine.Camera camera)
        {
            MagicExecutor executor =
                UnityEngine.Object.FindAnyObjectByType<MagicExecutor>(FindObjectsInactive.Include);
            EarthSurfaceQueryService surfaceQueries =
                UnityEngine.Object.FindAnyObjectByType<EarthSurfaceQueryService>(FindObjectsInactive.Include);
            GameObject collisionProxy = GameObject.Find("Planet Collision Proxy");
            PlayerInput playerInput = player.GetComponent<PlayerInput>();
            MagicInputController magicInput = player.GetComponent<MagicInputController>();
            PlanetInputReader motorInput = player.GetComponent<PlanetInputReader>();
            if (executor == null || surfaceQueries == null || collisionProxy == null ||
                collisionProxy.GetComponent<Collider>() == null || playerInput == null ||
                magicInput == null || motorInput == null)
                throw new System.InvalidOperationException(
                    "Character test repair requires the existing Earth magic runtime, surface queries, collision proxy and player input components.");

            EarthInputAdapter inputAdapter = player.GetComponent<EarthInputAdapter>();
            if (inputAdapter == null) inputAdapter = player.AddComponent<EarthInputAdapter>();
            inputAdapter.Configure(playerInput);

            LineRenderer preview = player.GetComponent<LineRenderer>();
            if (preview == null) preview = player.AddComponent<LineRenderer>();
            preview.useWorldSpace = true;
            preview.loop = false;
            preview.widthMultiplier = 0.08f;
            preview.sharedMaterial = CreateOrLoadPreviewMaterial();
            preview.positionCount = 0;

            EarthPillarMobility pillarMobility = player.GetComponent<EarthPillarMobility>();
            EarthPillarWaveAbility pillarWave = player.GetComponent<EarthPillarWaveAbility>();
            EarthLandingCushion landingCushion = player.GetComponent<EarthLandingCushion>();
            EarthPillarWavePool wavePool =
                UnityEngine.Object.FindAnyObjectByType<EarthPillarWavePool>(FindObjectsInactive.Include);
            if (pillarMobility == null || pillarWave == null || wavePool == null)
                throw new System.InvalidOperationException(
                    "Character test repair requires the existing pillar mobility, pillar wave ability and wave pool.");
            pillarMobility.Configure(
                player.GetComponent<Rigidbody>(),
                player.GetComponent<PlanetMotor>(),
                surfaceQueries);
            pillarWave.Configure(
                player.GetComponent<Rigidbody>(),
                player.GetComponent<PlanetMotor>(),
                wavePool,
                CreateOrLoadProfile<EarthPillarWaveProfile>(
                    WaveProfilePath,
                    "Earth Pillar Wave Profile"));
            EarthActionRouterBehaviour actionRouter = player.GetComponent<EarthActionRouterBehaviour>();
            if (actionRouter == null) actionRouter = player.AddComponent<EarthActionRouterBehaviour>();

            magicInput.Configure(
                playerInput,
                camera,
                executor,
                collisionProxy.GetComponent<Collider>(),
                preview);
            magicInput.ConfigureGestureProfile(CreateOrLoadProfile<EarthGestureProfile>(
                GestureProfilePath,
                "Earth Gesture Profile"));
            magicInput.ConfigureEarthTechniques(pillarWave);
            magicInput.ConfigureEarthSurfaceQueries(surfaceQueries);
            magicInput.ConfigureEarthFeatureProfiles(
                CreateOrLoadProfile<EarthQuickCastProfile>(
                    QuickCastProfilePath,
                    "Earth Quick Cast Profile"),
                CreateOrLoadProfile<EarthArmorProfile>(
                    ArmorProfilePath,
                    "Earth Armor Profile"));
            motorInput.Configure(
                inputAdapter,
                pillarMobility,
                pillarWave,
                landingCushion,
                actionRouter);
            player.GetComponent<PlanetMotor>()?.ConfigureInputSource(motorInput);

            AbilityRegistryBootstrap registry = executor.GetComponent<AbilityRegistryBootstrap>();
            if (registry == null) registry = executor.gameObject.AddComponent<AbilityRegistryBootstrap>();
            registry.Configure(executor, CreateOrLoadRecipes());

            EditorUtility.SetDirty(inputAdapter);
            EditorUtility.SetDirty(magicInput);
            EditorUtility.SetDirty(motorInput);
            EditorUtility.SetDirty(pillarMobility);
            EditorUtility.SetDirty(pillarWave);
            EditorUtility.SetDirty(actionRouter);
            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(preview);
        }

        private static void RepairCharacterCombatAndLandingWiring(
            GameObject player,
            GameObject bot,
            GravityWorldBehaviour gravityWorld,
            Vector3 arenaCenter)
        {
            GameObject collisionProxy = GameObject.Find("Planet Collision Proxy");
            EarthSurfaceQueryService surfaceQueries =
                UnityEngine.Object.FindAnyObjectByType<EarthSurfaceQueryService>(FindObjectsInactive.Include);
            EarthFragmentPool projectilePool =
                UnityEngine.Object.FindAnyObjectByType<EarthFragmentPool>(FindObjectsInactive.Include);
            if (player == null || bot == null || gravityWorld == null || collisionProxy == null ||
                collisionProxy.GetComponent<Collider>() == null || surfaceQueries == null ||
                projectilePool == null)
                throw new System.InvalidOperationException(
                    "Combat repair requires both fighters, gravity, the planet collider, surface queries and projectile pool.");

            Rigidbody playerBody = player.GetComponent<Rigidbody>();
            CapsuleCollider playerCollider = player.GetComponent<CapsuleCollider>();
            PlanetMotor playerMotor = player.GetComponent<PlanetMotor>();
            MagicInputController magicInput = player.GetComponent<MagicInputController>();
            PlanetInputReader inputReader = player.GetComponent<PlanetInputReader>();
            PhysicalImpactTarget playerPhysicalImpact = player.GetComponent<PhysicalImpactTarget>();
            ActiveRagdollPuppet playerPuppet = player.GetComponent<ActiveRagdollPuppet>();
            Rigidbody botBody = bot.GetComponent<Rigidbody>();
            CapsuleCollider botCollider = bot.GetComponent<CapsuleCollider>();
            PlanetMotor botMotor = bot.GetComponent<PlanetMotor>();
            EarthCombatDummy botCombat = bot.GetComponent<EarthCombatDummy>();
            EarthMvpBotController botController = bot.GetComponent<EarthMvpBotController>();
            if (playerBody == null || playerCollider == null || playerMotor == null ||
                magicInput == null || inputReader == null || playerPhysicalImpact == null ||
                playerPuppet == null || botBody == null || botCollider == null ||
                botMotor == null || botCombat == null || botController == null)
                throw new System.InvalidOperationException(
                    "Combat repair requires the authored player and bot physics/control components.");

            playerPhysicalImpact.Configure(playerBody, 0.34f);
            RepairPlayerPhysicalPuppet(
                player,
                playerPuppet,
                playerBody,
                playerMotor,
                playerPhysicalImpact,
                gravityWorld,
                magicInput,
                inputReader);

            HumanoidCharacterPresentation playerPresentation =
                player.GetComponentInChildren<HumanoidCharacterPresentation>(true);
            HumanoidCharacterPresentation botPresentation =
                bot.GetComponentInChildren<HumanoidCharacterPresentation>(true);
            Animator playerAnimator = playerPresentation != null
                ? playerPresentation.GetComponent<Animator>() ??
                  playerPresentation.GetComponentInChildren<Animator>(true)
                : null;
            Animator botAnimator = botPresentation != null
                ? botPresentation.GetComponent<Animator>() ??
                  botPresentation.GetComponentInChildren<Animator>(true)
                : null;
            HumanoidRagdollRig playerRagdoll = playerPresentation != null
                ? playerPresentation.GetComponent<HumanoidRagdollRig>()
                : null;
            HumanoidRagdollRig botRagdoll = botPresentation != null
                ? botPresentation.GetComponent<HumanoidRagdollRig>()
                : null;
            if (playerAnimator == null || botAnimator == null ||
                playerRagdoll == null || botRagdoll == null)
                throw new System.InvalidOperationException(
                    "Combat repair requires both visible Humanoid animators and ragdoll rigs.");

            EarthEffectsTuningProfile effectsProfile = CreateOrLoadEffectsProfile();
            CharacterImpactResponseProfile impactResponseProfile =
                CreateOrLoadProfile<CharacterImpactResponseProfile>(
                    CharacterImpactProfilePath,
                    "Character Impact Response Profile");
            impactResponseProfile.ConfigureMode(ImpactResponseMode.Calibrated);
            EditorUtility.SetDirty(impactResponseProfile);

            playerRagdoll.ConfigureAndBuild(
                playerAnimator,
                playerBody,
                playerCollider,
                gravityWorld,
                playerPuppet,
                playerPresentation.GetComponentInChildren<ParticleSystem>(true),
                playerPuppet,
                playerMotor,
                magicInput,
                inputReader);
            playerRagdoll.ConfigureEffectsProfile(effectsProfile);
            playerRagdoll.ConfigureLocalizedReactionProfile(impactResponseProfile);
            botRagdoll.ConfigureAndBuild(
                botAnimator,
                botBody,
                botCollider,
                gravityWorld,
                null,
                botPresentation.GetComponentInChildren<ParticleSystem>(true));
            botRagdoll.ConfigureEffectsProfile(effectsProfile);
            botRagdoll.ConfigureLocalizedReactionProfile(impactResponseProfile);

            CharacterPresentationProfile characterProfile =
                CreateOrLoadProfile<CharacterPresentationProfile>(
                    CharacterProfilePath,
                    "Character Presentation Profile");
            MagicExecutor executor =
                UnityEngine.Object.FindAnyObjectByType<MagicExecutor>(FindObjectsInactive.Include);
            Transform leftHandTarget = FindDescendantByName(player.transform, "Left Hand IK");
            Transform rightHandTarget = FindDescendantByName(player.transform, "Right Hand IK");
            playerPresentation.Configure(
                characterProfile,
                playerAnimator,
                leftHandTarget,
                rightHandTarget,
                playerMotor,
                playerBody,
                playerPuppet,
                magicInput,
                executor,
                CreateOrLoadProfile<EarthTechniquePresentationProfile>(
                    TechniquePresentationProfilePath,
                    "Earth Technique Presentation Profile"),
                player.GetComponent<EarthPillarMobility>(),
                playerRagdoll,
                true);
            botPresentation.Configure(
                characterProfile,
                botAnimator,
                null,
                null,
                botMotor,
                botBody,
                null,
                null,
                null,
                null,
                null,
                botRagdoll,
                false);
            HumanoidOrganicIdle playerOrganicIdle =
                playerPresentation.GetComponent<HumanoidOrganicIdle>();
            playerOrganicIdle?.Configure(
                playerAnimator,
                playerPresentation,
                playerMotor,
                playerRagdoll,
                characterProfile.OrganicIdleBlendInSeconds,
                characterProfile.OrganicIdleBlendOutSeconds);
            HumanoidOrganicIdle botOrganicIdle =
                botPresentation.GetComponent<HumanoidOrganicIdle>();
            botOrganicIdle?.Configure(
                botAnimator,
                botPresentation,
                botMotor,
                botRagdoll,
                characterProfile.OrganicIdleBlendInSeconds,
                characterProfile.OrganicIdleBlendOutSeconds);

            EarthCharacterImpactTarget playerCharacterImpact =
                player.GetComponent<EarthCharacterImpactTarget>();
            if (playerCharacterImpact == null)
                playerCharacterImpact = player.AddComponent<EarthCharacterImpactTarget>();
            EarthCharacterImpactTarget botCharacterImpact =
                bot.GetComponent<EarthCharacterImpactTarget>();
            if (botCharacterImpact == null)
                botCharacterImpact = bot.AddComponent<EarthCharacterImpactTarget>();
            playerCharacterImpact.Configure(
                EarthDuelFighterId.Player,
                0xC0010001u,
                playerBody,
                null,
                impactResponseProfile);
            botCharacterImpact.Configure(
                EarthDuelFighterId.Bot,
                0xC0010002u,
                botBody,
                null,
                impactResponseProfile);
            playerPhysicalImpact.ConfigureCharacterImpactTarget(playerCharacterImpact);
            botCombat.SetCharacterImpactAuthority(botCharacterImpact);

            EarthMvpDuelController duel = bot.GetComponent<EarthMvpDuelController>();
            if (duel == null) duel = bot.AddComponent<EarthMvpDuelController>();
            duel.Configure(
                playerPuppet,
                playerBody,
                playerPhysicalImpact,
                botController,
                botCombat,
                botMotor,
                botBody,
                botCollider,
                botAnimator,
                playerRagdoll,
                botRagdoll,
                playerCharacterImpact,
                botCharacterImpact,
                3.5f);
            botController.Configure(
                player.transform,
                playerBody,
                playerPhysicalImpact,
                playerPuppet,
                gravityWorld.transform,
                botBody,
                botMotor,
                botCombat,
                arenaCenter,
                6.5f);
            botController.ConfigureTuning(5.8f, 0.82f, 15f, 0.24f, 0.72f, 1.0f);
            botController.ConfigureMagic(projectilePool, botCollider, duel);
            botController.enabled = true;
            // Reassert the authored rival motor feel during repair as well as
            // initial scene generation. Older scenes carried PlanetMotor defaults,
            // which made the repaired bot strafe and accelerate like the player.
            botMotor.ConfigureInputSource(botController);
            botMotor.ConfigureFeel(3.1f, 18f, 0.18f);
            botMotor.ConfigureTankSteering(true, 245f);
            botMotor.ConfigureOrientationFeel(62f, 13f, 150f);

            EarthMvpBotPresenter botPresenter = bot.GetComponent<EarthMvpBotPresenter>();
            LineRenderer strikeLine = bot.GetComponent<LineRenderer>();
            if (botPresenter != null && strikeLine != null)
                botPresenter.Configure(
                    botController,
                    strikeLine,
                    botAnimator.GetComponentsInChildren<Renderer>(true),
                    botAnimator,
                    botMotor,
                    botBody,
                    botPresentation);

            EarthLandingCushion landingCushion = player.GetComponent<EarthLandingCushion>();
            Transform cushionVisual = FindTransformByNameIncludingInactive(
                "Earth Landing Cushion Preview");
            if (landingCushion == null || cushionVisual == null)
                throw new System.InvalidOperationException(
                    "Combat repair requires the authored landing cushion and preview visual.");
            landingCushion.Configure(
                playerBody,
                playerMotor,
                playerPuppet,
                collisionProxy.GetComponent<Collider>(),
                CreateOrLoadProfile<EarthLandingCushionProfile>(
                    LandingCushionProfilePath,
                    "Earth Landing Cushion Profile"),
                cushionVisual,
                surfaceQueries);

            EditorUtility.SetDirty(playerPhysicalImpact);
            EditorUtility.SetDirty(playerPuppet);
            EditorUtility.SetDirty(playerRagdoll);
            EditorUtility.SetDirty(botRagdoll);
            EditorUtility.SetDirty(playerPresentation);
            EditorUtility.SetDirty(botPresentation);
            if (playerOrganicIdle != null) EditorUtility.SetDirty(playerOrganicIdle);
            if (botOrganicIdle != null) EditorUtility.SetDirty(botOrganicIdle);
            EditorUtility.SetDirty(playerCharacterImpact);
            EditorUtility.SetDirty(botCharacterImpact);
            EditorUtility.SetDirty(duel);
            EditorUtility.SetDirty(botController);
            if (botPresenter != null) EditorUtility.SetDirty(botPresenter);
            EditorUtility.SetDirty(landingCushion);
        }

        private static void RepairPlayerPhysicalPuppet(
            GameObject player,
            ActiveRagdollPuppet puppet,
            Rigidbody playerBody,
            PlanetMotor playerMotor,
            PhysicalImpactTarget physicalImpact,
            GravityWorldBehaviour gravityWorld,
            MagicInputController magicInput,
            PlanetInputReader inputReader)
        {
            GameObject physicalRoot = FindTransformByNameIncludingInactive(
                "Earth Shaper Puppet")?.gameObject;
            Transform chest = physicalRoot != null
                ? FindDescendantByName(physicalRoot.transform, "Puppet Chest")
                : null;
            if (physicalRoot == null || chest == null)
                throw new System.InvalidOperationException(
                    "Player puppet repair requires the authored Earth Shaper Puppet hierarchy.");

            // The physical puppet lives outside the character Rigidbody hierarchy
            // by design. Whenever the authored player spawn is repaired, move the
            // detached puppet root to the same frame before reconnecting its
            // joints; otherwise the old world-space bodies pull the motor out of
            // the arena as soon as Play Mode starts.
            physicalRoot.transform.SetPositionAndRotation(
                player.transform.position - player.transform.up * 0.12f,
                player.transform.rotation);

            var targetNames = new Dictionary<string, string>
            {
                { "Puppet Chest", "Chest Target" },
                { "Puppet Head", "Head Target" },
                { "Puppet Arm L", "Left Arm Target" },
                { "Puppet Arm R", "Right Arm Target" },
                { "Puppet Upper Leg L", "Left Upper Leg Target" },
                { "Puppet Upper Leg R", "Right Upper Leg Target" },
                { "Puppet Lower Leg L", "Left Lower Leg Target" },
                { "Puppet Lower Leg R", "Right Lower Leg Target" }
            };
            var limits = new Dictionary<string, float>
            {
                { "Puppet Chest", 36f },
                { "Puppet Head", 42f },
                { "Puppet Arm L", 58f },
                { "Puppet Arm R", 58f },
                { "Puppet Upper Leg L", 48f },
                { "Puppet Upper Leg R", 48f },
                { "Puppet Lower Leg L", 52f },
                { "Puppet Lower Leg R", 52f }
            };
            ActiveRagdollJoint[] joints =
                physicalRoot.GetComponentsInChildren<ActiveRagdollJoint>(true);
            for (int index = 0; index < joints.Length; index++)
            {
                ActiveRagdollJoint jointDriver = joints[index];
                Rigidbody body = jointDriver.GetComponent<Rigidbody>();
                ConfigurableJoint joint = jointDriver.GetComponent<ConfigurableJoint>();
                if (body == null || joint == null ||
                    !targetNames.TryGetValue(jointDriver.name, out string targetName))
                    throw new System.InvalidOperationException(
                        $"Player puppet joint '{jointDriver.name}' cannot be repaired.");
                Transform poseTarget = FindDescendantByName(player.transform, targetName);
                if (poseTarget == null)
                    throw new System.InvalidOperationException(
                        $"Player puppet pose target '{targetName}' is missing.");
                body.position = poseTarget.position;
                body.rotation = poseTarget.rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                GravityBody gravityBody = jointDriver.GetComponent<GravityBody>();
                gravityBody?.Configure(gravityWorld, body);
                jointDriver.Configure(
                    body,
                    joint,
                    poseTarget,
                    900f,
                    65f,
                    1400f,
                    limits[jointDriver.name]);
                if (gravityBody != null) EditorUtility.SetDirty(gravityBody);
                EditorUtility.SetDirty(body);
                EditorUtility.SetDirty(jointDriver);
            }
            UnityEngine.Physics.SyncTransforms();
            if (joints.Length != 8)
                throw new System.InvalidOperationException(
                    $"Player puppet repair expected 8 driven joints, found {joints.Length}.");

            var selfColliders = new List<Collider>();
            Collider playerCollider = player.GetComponent<Collider>();
            if (playerCollider != null) selfColliders.Add(playerCollider);
            Collider[] puppetColliders = physicalRoot.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < puppetColliders.Length; index++)
                if (puppetColliders[index] != null && !selfColliders.Contains(puppetColliders[index]))
                    selfColliders.Add(puppetColliders[index]);
            puppet.Configure(
                1u,
                gravityWorld,
                playerBody,
                playerMotor,
                physicalImpact,
                chest,
                joints,
                selfColliders.ToArray());
            puppet.ConfigureControlBehaviours(magicInput, inputReader);

            // The production HumanoidRagdollRig is now the sole visible/dynamic
            // body handoff. Keeping the retired proxy bodies active at the same
            // time creates a second articulated mass connected to the motor root;
            // their ground depenetration can launch the player out of the arena.
            // Preserve the hierarchy for authoring/fallback inspection but keep it
            // outside the live PhysX scene.
            physicalRoot.SetActive(false);
            EditorUtility.SetDirty(physicalRoot);
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
                if (descendants[index] != null && descendants[index].name == name)
                    return descendants[index];
            return null;
        }

        private static Transform FindTransformByNameIncludingInactive(string name)
        {
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < transforms.Length; index++)
                if (transforms[index] != null && transforms[index].name == name)
                    return transforms[index];
            return null;
        }

        private static void RepairCanonicalEffectsProfileWiring(EarthEffectsTuningProfile profile)
        {
            RepairEffectsProfileReferences<EarthArenaFractureDustPresenter>(profile);
            RepairEffectsProfileReferences<EarthMagicFeedback>(profile);
            RepairEffectsProfileReferences<EarthSurfController>(profile);
            RepairEffectsProfileReferences<MeteorShowerBehaviour>(profile);
            RepairEffectsProfileReferences<EarthPillarFeedback>(profile);
            RepairEffectsProfileReferences<HumanoidRagdollRig>(profile);
        }

        private static void RepairEffectsProfileReferences<T>(EarthEffectsTuningProfile profile)
            where T : MonoBehaviour
        {
            T[] components = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include);
            for (int index = 0; index < components.Length; index++)
            {
                SerializedObject serialized = new SerializedObject(components[index]);
                SerializedProperty property = serialized.FindProperty("effectsProfile");
                if (property == null || property.objectReferenceValue == profile) continue;
                property.objectReferenceValue = profile;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(components[index]);
            }
        }

        [MenuItem("Elemental/Setup/Normalize Loaded Scene Material Shader State")]
        public static void NormalizeLoadedSceneMaterialShaderState()
        {
            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include);
            HashSet<Material> materials = new HashSet<Material>();
            int normalizedCount = 0;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] shared = renderers[rendererIndex].sharedMaterials;
                for (int materialIndex = 0; materialIndex < shared.Length; materialIndex++)
                {
                    Material material = shared[materialIndex];
                    if (material == null || !materials.Add(material)) continue;
                    string assetPath = AssetDatabase.GetAssetPath(material);
                    if (!string.IsNullOrEmpty(assetPath) &&
                        assetPath.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    MaterialShaderStateUtility.NormalizeKeywords(material);
                    normalizedCount++;
                    if (AssetDatabase.Contains(material)) EditorUtility.SetDirty(material);
                }
            }

            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            Debug.Log($"[Elemental] Normalized shader keyword state for {normalizedCount} loaded scene materials.");
        }

        private static void RepairActorGravityAndMotor(
            GameObject actor,
            GravityWorldBehaviour gravityWorld,
            MonoBehaviour inputSource,
            Transform cameraFrame,
            bool player)
        {
            Rigidbody body = actor.GetComponent<Rigidbody>();
            CapsuleCollider capsule = actor.GetComponent<CapsuleCollider>();
            PlanetMotor motor = actor.GetComponent<PlanetMotor>();
            GravityBody gravity = actor.GetComponent<GravityBody>();
            if (body == null || capsule == null || motor == null || gravity == null || inputSource == null)
                throw new System.InvalidOperationException(
                    $"{actor.name} is missing Rigidbody, CapsuleCollider, PlanetMotor, GravityBody or input source.");
            body.useGravity = false;
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            gravity.Configure(gravityWorld, body);
            motor.Configure(gravityWorld, body, capsule, inputSource, cameraFrame);
            // The Broken Crown floor is a detailed non-convex mesh. Persist the
            // same contact skin used by generated actors so edit-time repair does
            // not silently restore the legacy 1 cm grounding tolerance.
            motor.ConfigureGroundContactSkin(0.045f);
            if (player)
            {
                motor.ConfigureFeel(CreateOrLoadProfile<PlanetMotorFeelProfile>(
                    MotorFeelProfilePath,
                    "Planet Motor Feel Profile"));
                motor.ConfigureTankSteering(true, 170f);
                motor.ConfigureOrientationFeel(60f, 12f, 140f);
            }
            else
            {
                motor.ConfigureFeel(3.1f, 18f, 0.18f);
                motor.ConfigureTankSteering(true, 245f);
                motor.ConfigureOrientationFeel(62f, 13f, 150f);
            }
            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(gravity);
            EditorUtility.SetDirty(motor);
            EarthGravityRuntimeAudit audit = actor.GetComponent<EarthGravityRuntimeAudit>();
            if (audit == null) audit = actor.AddComponent<EarthGravityRuntimeAudit>();
            audit.Configure(gravity, motor);
        }

        private static void RepairGameplayCameraWiring(
            Camera camera,
            GameObject player,
            GameObject opponent)
        {
            if (camera == null || player == null) return;
            PlanetCameraRig cameraRig = camera.GetComponent<PlanetCameraRig>();
            if (cameraRig == null) cameraRig = camera.gameObject.AddComponent<PlanetCameraRig>();
            PlanetMotor motor = player.GetComponent<PlanetMotor>();
            MagicInputController input = player.GetComponent<MagicInputController>();
            if (input == null)
                input = UnityEngine.Object.FindAnyObjectByType<MagicInputController>(
                    FindObjectsInactive.Include);
            MagicExecutor executor = player.GetComponent<MagicExecutor>();
            if (executor == null)
                executor = UnityEngine.Object.FindAnyObjectByType<MagicExecutor>(
                    FindObjectsInactive.Include);

            EarthCameraDirector director = camera.GetComponent<EarthCameraDirector>();
            if (director == null) director = camera.gameObject.AddComponent<EarthCameraDirector>();
            director.Configure(
                cameraRig,
                camera,
                player.transform,
                player.GetComponent<Rigidbody>(),
                motor,
                input,
                player.GetComponent<EarthInputAdapter>(),
                executor,
                player.GetComponent<ActiveRagdollPuppet>(),
                CreateOrLoadProfile<EarthCameraProfile>(
                    EarthCameraProfilePath,
                    "Earth Camera Profile"));

            EarthCinemachineCameraController oldController =
                camera.GetComponent<EarthCinemachineCameraController>();
            if (oldController != null) UnityEngine.Object.DestroyImmediate(oldController);
            ConfigureCinemachineCamera(camera, player, cameraRig, director, motor);

            EarthChargeCameraLookdevV2 lookdev = camera.GetComponent<EarthChargeCameraLookdevV2>();
            lookdev?.BindDirector(director);
            EarthCinematicDepthOfFieldController depthOfField =
                camera.GetComponent<EarthCinematicDepthOfFieldController>();
            if (depthOfField == null)
                depthOfField = camera.gameObject.AddComponent<EarthCinematicDepthOfFieldController>();
            depthOfField.ConfigureSubjects(
                player.transform,
                opponent != null ? opponent.transform : null);
            EarthCameraRuntimeAudit cameraAudit = camera.GetComponent<EarthCameraRuntimeAudit>();
            if (cameraAudit == null) cameraAudit = camera.gameObject.AddComponent<EarthCameraRuntimeAudit>();
            cameraAudit.Configure(
                director,
                camera.GetComponent<EarthCinemachineCameraController>(),
                depthOfField);

            EditorUtility.SetDirty(cameraRig);
            EditorUtility.SetDirty(director);
            if (lookdev != null) EditorUtility.SetDirty(lookdev);
            EditorUtility.SetDirty(depthOfField);
            EditorUtility.SetDirty(cameraAudit);
        }

        private static void IntegrateEammActor(GameObject gameplayRoot, bool player)
        {
            HumanoidCharacterPresentation presentation =
                gameplayRoot.GetComponentInChildren<HumanoidCharacterPresentation>(true);
            if (presentation == null)
                throw new UnityEditor.Build.BuildFailedException(
                    $"{gameplayRoot.name} has no HumanoidCharacterPresentation.");
            Animator animator = presentation.GetComponent<Animator>();
            if (animator == null) animator = presentation.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                throw new UnityEditor.Build.BuildFailedException(
                    $"{gameplayRoot.name} has no valid Humanoid Animator on its visible presentation.");
            HumanoidRagdollRig ragdoll = presentation.GetComponent<HumanoidRagdollRig>();
            if (ragdoll == null) ragdoll = presentation.GetComponentInChildren<HumanoidRagdollRig>(true);
            ConfigureEammBasePose(
                gameplayRoot,
                presentation.gameObject,
                animator,
                presentation,
                ragdoll,
                player);
        }

        [MenuItem("Elemental Suite/Character/Refresh Mixamo Presentation Assets")]
        public static void RefreshCharacterAnimationAssets()
        {
            ConfigureCharacterImporters();
            AnimatorController controller = CreateOrLoadMageController();
            if (controller == null)
                throw new System.InvalidOperationException("Mixamo presentation AnimatorController could not be created.");
            AssetDatabase.SaveAssets();
            Debug.Log("[Elemental] Mixamo X Bot presentation, locomotion and earth-cast clips refreshed.");
        }

        private static EarthCoreVisualStyle CreateOrLoadVisualStyle()
        {
            EarthCoreVisualStyle style = AssetDatabase.LoadAssetAtPath<EarthCoreVisualStyle>(StylePath);
            if (style != null) return style;
            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/VisualStyles");
            style = ScriptableObject.CreateInstance<EarthCoreVisualStyle>();
            style.name = "Earth Core Visual Style";
            AssetDatabase.CreateAsset(style, StylePath);
            return style;
        }

        private static EarthWallProfile CreateOrLoadWallProfile()
        {
            EarthWallProfile profile = AssetDatabase.LoadAssetAtPath<EarthWallProfile>(WallProfilePath);
            if (profile != null) return profile;
            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/Profiles");
            profile = ScriptableObject.CreateInstance<EarthWallProfile>();
            profile.name = "Earth Wall Profile";
            AssetDatabase.CreateAsset(profile, WallProfilePath);
            return profile;
        }

        private static EarthRockProfile CreateOrLoadRockProfile()
        {
            EarthRockProfile profile = AssetDatabase.LoadAssetAtPath<EarthRockProfile>(RockProfilePath);
            if (profile != null) return profile;
            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/Profiles");
            profile = ScriptableObject.CreateInstance<EarthRockProfile>();
            profile.name = "Earth Rock Profile";
            AssetDatabase.CreateAsset(profile, RockProfilePath);
            return profile;
        }

        private static EarthPillarWaveProfile CreateOrLoadWaveProfile()
        {
            EarthPillarWaveProfile profile = AssetDatabase.LoadAssetAtPath<EarthPillarWaveProfile>(WaveProfilePath);
            if (profile != null)
            {
                const string canonicalAssetName = "EarthPillarWaveProfile";
                if (profile.name != canonicalAssetName)
                {
                    profile.name = canonicalAssetName;
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssetIfDirty(profile);
                }
                return profile;
            }
            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/Profiles");
            profile = ScriptableObject.CreateInstance<EarthPillarWaveProfile>();
            profile.name = "EarthPillarWaveProfile";
            AssetDatabase.CreateAsset(profile, WaveProfilePath);
            return profile;
        }

        private static EarthVectorFieldProfile CreateOrLoadVectorFieldProfile()
        {
            EarthVectorFieldProfile profile = AssetDatabase.LoadAssetAtPath<EarthVectorFieldProfile>(VectorFieldProfilePath);
            if (profile != null) return profile;
            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/Profiles");
            profile = ScriptableObject.CreateInstance<EarthVectorFieldProfile>();
            profile.name = "Earth Vector Field Profile";
            AssetDatabase.CreateAsset(profile, VectorFieldProfilePath);
            return profile;
        }

        private static EarthPlatformProfile CreateOrLoadPlatformProfile()
        {
            EarthPlatformProfile profile = AssetDatabase.LoadAssetAtPath<EarthPlatformProfile>(PlatformProfilePath);
            if (profile != null) return profile;
            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/Profiles");
            profile = ScriptableObject.CreateInstance<EarthPlatformProfile>();
            profile.name = "Earth Platform Profile";
            AssetDatabase.CreateAsset(profile, PlatformProfilePath);
            return profile;
        }

        private static EarthLandingCushionProfile CreateOrLoadLandingCushionProfile()
        {
            EarthLandingCushionProfile profile = AssetDatabase.LoadAssetAtPath<EarthLandingCushionProfile>(LandingCushionProfilePath);
            if (profile != null) return profile;
            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/Profiles");
            profile = ScriptableObject.CreateInstance<EarthLandingCushionProfile>();
            profile.name = "Earth Landing Cushion Profile";
            AssetDatabase.CreateAsset(profile, LandingCushionProfilePath);
            return profile;
        }

        private static EarthHoverProfile CreateOrLoadHoverProfile()
        {
            EarthHoverProfile profile = AssetDatabase.LoadAssetAtPath<EarthHoverProfile>(HoverProfilePath);
            if (profile != null) return profile;
            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/Profiles");
            profile = ScriptableObject.CreateInstance<EarthHoverProfile>();
            profile.name = "Earth Hover Profile";
            AssetDatabase.CreateAsset(profile, HoverProfilePath);
            return profile;
        }

        private static EarthGravityWellProfile CreateOrLoadGravityWellProfile()
        {
            EarthGravityWellProfile profile = AssetDatabase.LoadAssetAtPath<EarthGravityWellProfile>(GravityWellProfilePath);
            if (profile != null) return profile;
            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/Profiles");
            profile = ScriptableObject.CreateInstance<EarthGravityWellProfile>();
            profile.name = "Earth Gravity Well Profile";
            AssetDatabase.CreateAsset(profile, GravityWellProfilePath);
            return profile;
        }

        private static void ApplyEarthMaterial(
            Material material,
            EarthCoreVisualStyle style,
            EarthMaterialProfile profile,
            bool freshInterior)
        {
            if (material == null) return;
            ConfigureEarthTextureImport();
            Shader shader = Shader.Find("Elemental/SG Earth Master");
            if (shader != null) MaterialShaderStateUtility.RebindShader(material, shader);
            material.color = style.StoneColor;
            material.SetColor("_BaseColor", style.StoneColor);
            material.SetColor("_EmissionColor", style.StoneEmission);
            material.SetFloat("_Smoothness", style.StoneSmoothness);
            material.SetFloat("_WorldTiling", 0.48f);
            material.SetFloat("_TriplanarSharpness", 5.5f);
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(EarthStoneAlbedoPath);
            if (albedo != null) material.SetTexture("_BaseMap", albedo);
            profile?.Apply(material, freshInterior);
            EditorUtility.SetDirty(material);
        }

        private static Material CreateOrLoadEarthMaterial(
            string fileName, Color color, float smoothness, Color emission)
        {
            const string folder = "Assets/Elemental/Content/Materials/";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(folder + fileName);
            Shader shader = Shader.Find("Elemental/SG Earth Master");
            if (shader == null)
                throw new UnityEditor.Build.BuildFailedException("Elemental/SG Earth Master shader was not found.");
            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(fileName) };
                AssetDatabase.CreateAsset(material, folder + fileName);
            }
            else MaterialShaderStateUtility.RebindShader(material, shader);
            ConfigureEarthTextureImport();
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", emission);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_WorldTiling", 0.48f);
            material.SetFloat("_TriplanarSharpness", 5.5f);
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(EarthStoneAlbedoPath);
            if (albedo != null) material.SetTexture("_BaseMap", albedo);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureEarthTextureImport()
        {
            TextureImporter importer = AssetImporter.GetAtPath(EarthStoneAlbedoPath) as TextureImporter;
            if (importer == null) return;
            bool dirty = importer.wrapMode != TextureWrapMode.Repeat ||
                         importer.filterMode != FilterMode.Trilinear ||
                         importer.maxTextureSize != 2048 || !importer.sRGBTexture;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 6;
            importer.maxTextureSize = 2048;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (dirty) importer.SaveAndReimport();
        }

        private static void ConfigurePresentation(
            Scene scene,
            GameObject character,
            UnityEngine.Camera camera,
            MagicExecutor executor,
            MagicInputController input,
            EarthPillarMobility pillarMobility,
            EarthLandingCushion landingCushion,
            LineRenderer preview,
            EarthCoreVisualStyle style,
            Material earthMaterial,
            GravityWorldBehaviour gravityWorld,
            EarthRockDebrisPool debrisPool,
            EarthPillarWavePool wavePool,
            Transform planetCenter,
            PlanetWorldProfile worldProfile)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Restore the cooler pre-rescue grade. The hotter key/exposure pass
            // turned the whole authored sandstone assembly into a flat orange wash.
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.39f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.18f, 0.17f);
            RenderSettings.ambientGroundColor = new Color(0.075f, 0.065f, 0.06f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.72f;
            // The depth-aware atmosphere pass is the one fog authority. Keeping
            // legacy RenderSettings fog here would attenuate geometry twice.
            RenderSettings.fog = false;
            QualitySettings.shadowDistance = 90f;
            QualitySettings.shadowCascades = 4;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = style.SkyColor;
            camera.usePhysicalProperties = true;
            camera.sensorSize = new Vector2(36f, 24f);
            camera.focalLength = 47f;
            camera.nearClipPlane = 0.1f;
            camera.allowHDR = true;
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            // EarthCore uses restrained SSAO plus analytic material form depth.
            // Realtime directional shadows are intentionally disabled because
            // their moving cascade bands were the source of the visible stripes.
            cameraData.renderShadows = true;
            cameraData.requiresDepthTexture = true;
            cameraData.stopNaN = true;
            cameraData.dithering = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            PlanetCameraRig cameraRig = camera.GetComponent<PlanetCameraRig>();
            cameraRig?.ConfigureFraming(
                style.CameraDistance,
                style.CameraHeight,
                style.CameraFocusHeight,
                style.CameraLookAheadDistance,
                style.CameraShoulderOffset);
            cameraRig?.ConfigureFeel(0.14f, 6.5f);
            PlanetMotor motor = character.GetComponent<PlanetMotor>();
            motor?.ConfigureFeel(CreateOrLoadProfile<PlanetMotorFeelProfile>(
                MotorFeelProfilePath,
                "Planet Motor Feel Profile"));
            motor?.ConfigureTankSteering(true, 170f);
            motor?.ConfigureOrientationFeel(60f, 12f, 140f);
            EarthCameraDirector cameraDirector = camera.GetComponent<EarthCameraDirector>();
            if (cameraRig != null)
            {
                if (cameraDirector == null) cameraDirector = camera.gameObject.AddComponent<EarthCameraDirector>();
                cameraDirector.Configure(
                    cameraRig,
                    camera,
                    character.transform,
                    character.GetComponent<Rigidbody>(),
                    motor,
                    input,
                    character.GetComponent<EarthInputAdapter>(),
                    executor,
                    character.GetComponent<ActiveRagdollPuppet>(),
                    CreateOrLoadProfile<EarthCameraProfile>(EarthCameraProfilePath, "Earth Camera Profile"));
            }
            if (camera.GetComponent<VisualQaCaptureBehaviour>() == null)
                camera.gameObject.AddComponent<VisualQaCaptureBehaviour>();

            ConfigureLights(style);
            ConfigurePreview(preview, style);
            CreateGroundFootprintPreview(input, preview, style);
            CreateAbilityPreview(input, executor, style);
            CreateCharacterVisual(character, input, executor, style, gravityWorld, pillarMobility);
            if (cameraRig != null)
                ConfigureCinemachineCamera(
                    camera,
                    character,
                    cameraRig,
                    camera.GetComponent<EarthCameraDirector>(),
                    motor);
            HideTechnicalGravityToyProps();
            CreatePlanetLandmarks(
                earthMaterial,
                style,
                worldProfile.Radius,
                gravityWorld,
                debrisPool);
            CreateWorldAndSpace(camera, executor, planetCenter, worldProfile, style);
            CreateEarthFeedback(executor, input, cameraRig, style, wavePool, planetCenter);
            CreateGravityWellFeedback(executor, cameraRig, style, planetCenter);
            CreateEarthPillarFeedback(pillarMobility, cameraRig, style);
            CreateHud(input, executor, pillarMobility, landingCushion);
            Volume postVolume = CreatePostProcessing();
            ParticleSystem lightMotes = CreateAmbientLightMotes(
                camera.transform,
                CreateOrLoadEffectsProfile());
            EarthChargeCameraLookdevV2 clarity = camera.GetComponent<EarthChargeCameraLookdevV2>();
            if (clarity == null) clarity = camera.gameObject.AddComponent<EarthChargeCameraLookdevV2>();
            clarity.Configure(cameraDirector, postVolume, lightMotes);
            EditorUtility.SetDirty(clarity);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ConfigureCinemachineCamera(
            Camera camera,
            GameObject character,
            PlanetCameraRig legacyRig,
            EarthCameraDirector director,
            PlanetMotor motor)
        {
            if (camera == null || character == null || motor == null) return;
            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();
            SetTagRecursively(character, "Player");
            GameObject puppetRoot = GameObject.Find("Earth Shaper Puppet");
            if (puppetRoot != null) SetTagRecursively(puppetRoot, "Player");

            GameObject oldSystem = GameObject.Find("Earth Cinemachine System");
            if (oldSystem != null) Object.DestroyImmediate(oldSystem);
            GameObject system = new GameObject("Earth Cinemachine System");
            GameObject worldUpObject = new GameObject("Earth Camera World Up");
            worldUpObject.transform.SetParent(system.transform, false);
            GameObject aimObject = new GameObject("Earth Camera Aim Pivot");
            aimObject.transform.SetParent(worldUpObject.transform, false);
            GameObject virtualCameraObject = new GameObject("Earth Gameplay Camera");
            virtualCameraObject.transform.SetParent(system.transform, false);

            CinemachineBrain brain = camera.GetComponent<CinemachineBrain>();
            if (brain == null) brain = camera.gameObject.AddComponent<CinemachineBrain>();
            CinemachineCamera virtualCamera = virtualCameraObject.AddComponent<CinemachineCamera>();
            CinemachineThirdPersonFollow follow = virtualCameraObject.AddComponent<CinemachineThirdPersonFollow>();
            // Keep all live gameplay-camera and MiniBokeh tuning controls on the
            // real camera so artists can tune them together during Play Mode.
            EarthCinemachineCameraController controller =
                camera.gameObject.AddComponent<EarthCinemachineCameraController>();
            controller.Configure(
                camera,
                brain,
                virtualCamera,
                follow,
                legacyRig,
                director,
                motor,
                character.transform,
                worldUpObject.transform,
                aimObject.transform);
        }

        private static void SetTagRecursively(GameObject root, string tag)
        {
            if (root == null) return;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
                transforms[index].gameObject.tag = tag;
        }

        private static Transform CreateHeldFragmentAnchor(GameObject character)
        {
            Transform existing = character.transform.Find("Held Earth Anchor");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            GameObject anchor = new GameObject("Held Earth Anchor");
            anchor.transform.SetParent(character.transform, false);
            anchor.transform.localPosition = new Vector3(0.82f, 1.18f, 0.62f);
            return anchor.transform;
        }

        private static void HideTechnicalGravityToyProps()
        {
            GameObject ramp = GameObject.Find("Top Ramp");
            if (ramp != null) ramp.SetActive(false);
            for (int index = 1; index <= 32; index++)
            {
                GameObject body = GameObject.Find($"Gravity Body {index:00}");
                if (body != null) body.SetActive(false);
            }
        }

private static void ConfigureLights(EarthCoreVisualStyle style)
        {
            Light sun = GameObject.Find("Sun")?.GetComponent<Light>();
            if (sun != null)
            {
                sun.color = new Color(1f, 0.91f, 0.78f);
                sun.intensity = 1.28f;
                // Keep readable contact with the spherical terrain and props.
                // The high-quality URP profile supplies the filtering needed to
                // avoid the old travelling-band artifact.
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.554f;
                sun.transform.rotation = Quaternion.Euler(38f, -36f, 0f);
                RenderSettings.sun = sun;
            }

            GameObject rimObject = GameObject.Find("Earth Rim Light");
            if (rimObject != null) Object.DestroyImmediate(rimObject);
            GameObject fillObject = GameObject.Find("Earth Warm Fill");
            if (fillObject != null) Object.DestroyImmediate(fillObject);
        }

        private static void ConfigurePreview(LineRenderer preview, EarthCoreVisualStyle style)
        {
            preview.widthMultiplier = 0.13f;
            preview.numCornerVertices = 4;
            preview.numCapVertices = 4;
            preview.textureMode = LineTextureMode.Tile;
            preview.startColor = style.PreviewCoreColor;
            preview.endColor = style.PreviewEdgeColor;
            Material material = preview.sharedMaterial;
            if (material != null)
            {
                material.color = style.PreviewCoreColor;
                material.SetColor("_BaseColor", style.PreviewCoreColor);
                material.SetColor("_EmissionColor", style.PreviewCoreColor * 2.8f);
                material.EnableKeyword("_EMISSION");
                EditorUtility.SetDirty(material);
            }
        }

        private static void CreateGroundFootprintPreview(
            MagicInputController input,
            LineRenderer linePreview,
            EarthCoreVisualStyle style)
        {
            if (linePreview != null) linePreview.enabled = false;
            GameObject old = GameObject.Find("Earth Ground Footprint Preview");
            if (old != null) Object.DestroyImmediate(old);

            GameObject root = new GameObject("Earth Ground Footprint Preview");
            Material material = CreateOrLoadLitMaterial(
                "GroundPreviewPebble.mat",
                style.PreviewCoreColor * 0.58f,
                0.08f,
                style.PreviewCoreColor * 0.35f);
            Transform[] markers = new Transform[24];
            for (int index = 0; index < markers.Length; index++)
            {
                float width = 0.075f + ((index % 3) * 0.018f);
                GameObject pebble = CreatePart(
                    $"Footprint Pebble {index + 1:00}",
                    PrimitiveType.Cube,
                    root.transform,
                    Vector3.zero,
                    new Vector3(width, 0.035f, width * 1.25f),
                    material,
                    new Vector3(0f, index * 37f, (index % 2 == 0 ? -1f : 1f) * 8f));
                pebble.SetActive(false);
                markers[index] = pebble.transform;
            }

            EarthFootprintPreview footprint = root.AddComponent<EarthFootprintPreview>();
            footprint.Configure(input, markers);
        }

        private static void CreateAbilityPreview(
            MagicInputController input,
            MagicExecutor executor,
            EarthCoreVisualStyle style)
        {
            GameObject old = GameObject.Find("Earth Ability Preview");
            if (old != null) Object.DestroyImmediate(old);
            GameObject root = new GameObject("Earth Ability Preview");
            Material previewMaterial = CreateOrLoadUnlitMaterial(
                "EarthPreviewWire.mat", new Color(1.2f, 0.53f, 0.10f, 0.78f));

            GameObject volume = new GameObject("Extraction Volume");
            volume.transform.SetParent(root.transform, false);
            for (int ringIndex = 0; ringIndex < 3; ringIndex++)
            {
                GameObject ringObject = new GameObject($"Extraction Ring {ringIndex + 1}");
                ringObject.transform.SetParent(volume.transform, false);
                ringObject.transform.localRotation = ringIndex == 0
                    ? Quaternion.identity
                    : ringIndex == 1 ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(0f, 0f, 90f);
                LineRenderer ring = ringObject.AddComponent<LineRenderer>();
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = 32;
                ring.widthMultiplier = 0.018f;
                ring.sharedMaterial = previewMaterial;
                ring.startColor = new Color(1f, 0.48f, 0.09f, 0.72f);
                ring.endColor = ring.startColor;
                for (int index = 0; index < ring.positionCount; index++)
                {
                    float angle = index * Mathf.PI * 2f / ring.positionCount;
                    ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f));
                }
            }

            GameObject trajectoryObject = new GameObject("Flick Trajectory");
            trajectoryObject.transform.SetParent(root.transform, false);
            LineRenderer trajectory = trajectoryObject.AddComponent<LineRenderer>();
            trajectory.useWorldSpace = true;
            trajectory.widthMultiplier = 0.045f;
            trajectory.numCapVertices = 4;
            trajectory.sharedMaterial = previewMaterial;
            trajectory.startColor = new Color(1f, 0.66f, 0.16f, 0.8f);
            trajectory.endColor = new Color(1f, 0.28f, 0.04f, 0.05f);

            GameObject fieldObject = new GameObject("Vector Field Direction");
            fieldObject.transform.SetParent(root.transform, false);
            LineRenderer fieldGuide = fieldObject.AddComponent<LineRenderer>();
            fieldGuide.useWorldSpace = true;
            fieldGuide.widthMultiplier = 0.10f;
            fieldGuide.numCapVertices = 5;
            fieldGuide.sharedMaterial = previewMaterial;
            fieldGuide.startColor = new Color(1f, 0.74f, 0.18f, 0.96f);
            fieldGuide.endColor = new Color(1f, 0.30f, 0.03f, 0.08f);

            GameObject platformObject = new GameObject("Platform Height Outline");
            platformObject.transform.SetParent(root.transform, false);
            LineRenderer platformGuide = platformObject.AddComponent<LineRenderer>();
            platformGuide.useWorldSpace = true;
            platformGuide.loop = true;
            platformGuide.widthMultiplier = 0.055f;
            platformGuide.numCornerVertices = 3;
            platformGuide.sharedMaterial = previewMaterial;
            platformGuide.startColor = new Color(1f, 0.52f, 0.08f, 0.76f);
            platformGuide.endColor = platformGuide.startColor;

            EarthAbilityPreview abilityPreview = root.AddComponent<EarthAbilityPreview>();
            abilityPreview.Configure(input, executor, volume.transform, trajectory, fieldGuide, platformGuide);
        }

        private static Mesh CreateOrLoadFragmentMesh()
        {
            return CreateOrLoadFragmentMeshes()[0];
        }

        private static Mesh[] CreateOrLoadFragmentMeshes()
        {
            return RumblePhysicsRockAssetBuilder.CreateOrUpdateHeroLibrary();
        }

        private static Mesh[] CreateOrLoadDebrisMeshes() =>
            RumblePhysicsRockAssetBuilder.CreateOrUpdateDebrisLibrary();

        private static void BuildBeveledBlock(Mesh mesh, float bevel, int variant)
        {
            float half = 0.5f;
            float inner = half - Mathf.Clamp(bevel, 0.06f, 0.19f);
            var vertices = new List<Vector3>(96);
            var triangles = new List<int>(132);
            AddSurface(vertices, triangles, new[]
            {
                new Vector3(half, -inner, -inner), new Vector3(half, -inner, inner),
                new Vector3(half, inner, inner), new Vector3(half, inner, -inner)
            }, Vector3.right);
            AddSurface(vertices, triangles, new[]
            {
                new Vector3(-half, -inner, inner), new Vector3(-half, -inner, -inner),
                new Vector3(-half, inner, -inner), new Vector3(-half, inner, inner)
            }, Vector3.left);
            AddSurface(vertices, triangles, new[]
            {
                new Vector3(-inner, half, -inner), new Vector3( inner, half, -inner),
                new Vector3( inner, half,  inner), new Vector3(-inner, half,  inner)
            }, Vector3.up);
            AddSurface(vertices, triangles, new[]
            {
                new Vector3(-inner, -half, inner), new Vector3(inner, -half, inner),
                new Vector3(inner, -half, -inner), new Vector3(-inner, -half, -inner)
            }, Vector3.down);
            AddSurface(vertices, triangles, new[]
            {
                new Vector3(-inner, -inner, half), new Vector3(-inner, inner, half),
                new Vector3(inner, inner, half), new Vector3(inner, -inner, half)
            }, Vector3.forward);
            AddSurface(vertices, triangles, new[]
            {
                new Vector3(inner, -inner, -half), new Vector3(inner, inner, -half),
                new Vector3(-inner, inner, -half), new Vector3(-inner, -inner, -half)
            }, Vector3.back);

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                AddSurface(vertices, triangles, new[]
                {
                    new Vector3(sx * half, sy * inner, -inner),
                    new Vector3(sx * inner, sy * half, -inner),
                    new Vector3(sx * inner, sy * half, inner),
                    new Vector3(sx * half, sy * inner, inner)
                }, new Vector3(sx, sy, 0f).normalized);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                AddSurface(vertices, triangles, new[]
                {
                    new Vector3(sx * half, -inner, sz * inner),
                    new Vector3(sx * inner, -inner, sz * half),
                    new Vector3(sx * inner, inner, sz * half),
                    new Vector3(sx * half, inner, sz * inner)
                }, new Vector3(sx, 0f, sz).normalized);
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                AddSurface(vertices, triangles, new[]
                {
                    new Vector3(-inner, sy * half, sz * inner),
                    new Vector3(-inner, sy * inner, sz * half),
                    new Vector3(inner, sy * inner, sz * half),
                    new Vector3(inner, sy * half, sz * inner)
                }, new Vector3(0f, sy, sz).normalized);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                float chip = ((variant + (sx > 0 ? 1 : 0) + (sy > 0 ? 2 : 0) + (sz > 0 ? 3 : 0)) % 4) * 0.008f;
                float cornerInner = inner - chip;
                AddSurface(vertices, triangles, new[]
                {
                    new Vector3(sx * half, sy * cornerInner, sz * cornerInner),
                    new Vector3(sx * cornerInner, sy * half, sz * cornerInner),
                    new Vector3(sx * cornerInner, sy * cornerInner, sz * half)
                }, new Vector3(sx, sy, sz).normalized);
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static void AddSurface(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3[] polygon,
            Vector3 outward)
        {
            int start = vertices.Count;
            bool reverse = polygon.Length >= 3 &&
                           Vector3.Dot(Vector3.Cross(polygon[1] - polygon[0], polygon[2] - polygon[0]), outward) < 0f;
            if (reverse)
                for (int index = polygon.Length - 1; index >= 0; index--) vertices.Add(polygon[index]);
            else
                for (int index = 0; index < polygon.Length; index++) vertices.Add(polygon[index]);
            for (int index = 1; index < polygon.Length - 1; index++)
            {
                triangles.Add(start);
                triangles.Add(start + index);
                triangles.Add(start + index + 1);
            }
        }

        private static Mesh CreateOrLoadChippedWallMesh()
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(WallMeshPath);
            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/Meshes");
            Mesh generated = EarthSafeMeshFactory.CreateBeveledBox(
                "ChippedEarthWall",
                new Bounds(Vector3.zero, Vector3.one),
                0.065f,
                0xEA4711u);
            generated.hideFlags = HideFlags.None;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, WallMeshPath);
                return generated;
            }
            EditorUtility.CopySerialized(generated, existing);
            existing.name = "ChippedEarthWall";
            existing.hideFlags = HideFlags.None;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(generated);
            return existing;
        }

        private static Mesh CreateOrLoadBeveledPillarMesh()
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(PillarMeshPath);
            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/Meshes");
            Mesh generated = EarthSafeMeshFactory.CreateBeveledBox(
                "BeveledEarthPillar",
                new Bounds(Vector3.zero, new Vector3(1f, 2f, 1f)),
                0.095f,
                0xF11A7u);
            generated.hideFlags = HideFlags.None;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, PillarMeshPath);
                return generated;
            }
            EditorUtility.CopySerialized(generated, existing);
            existing.name = "BeveledEarthPillar";
            existing.hideFlags = HideFlags.None;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(generated);
            return existing;
        }

        private static void CreateCharacterVisual(
            GameObject character,
            MagicInputController input,
            MagicExecutor executor,
            EarthCoreVisualStyle style,
            GravityWorldBehaviour gravityWorld = null,
            EarthPillarMobility pillarMobility = null)
        {
            MeshRenderer capsuleRenderer = character.GetComponent<MeshRenderer>();
            if (capsuleRenderer != null) capsuleRenderer.enabled = false;
            Transform old = character.transform.Find("Earth Shaper Visual");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform oldPuppetRoot = character.transform.Find("Earth Shaper Puppet");
            if (oldPuppetRoot != null) Object.DestroyImmediate(oldPuppetRoot.gameObject);

            Material body = CreateOrLoadLitMaterial("EarthShaperBody.mat", style.BodyColor, 0.22f, Color.black);
            Material scarf = CreateOrLoadLitMaterial("EarthShaperScarf.mat", style.ScarfColor, 0.32f, style.ScarfColor * 0.08f);
            Material eye = CreateOrLoadLitMaterial("EarthShaperEyes.mat", style.EyeColor, 0.55f, style.EyeColor * 2f);
            Material boot = CreateOrLoadLitMaterial("EarthShaperBoots.mat", new Color(0.055f, 0.04f, 0.035f), 0.12f, Color.black);

            if (gravityWorld != null && character.GetComponent<PlanetMotor>() != null && input != null)
            {
                CreateActivePuppetVisual(
                    character, input, executor, gravityWorld, body, scarf, eye, boot);
                CreateHumanoidPresentation(character, input, executor, pillarMobility, gravityWorld);
                return;
            }

            GameObject root = new GameObject("Earth Shaper Visual");
            root.transform.SetParent(character.transform, false);
            root.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            CreatePart("Body", PrimitiveType.Capsule, root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.72f, 0.68f, 0.60f), body);
            CreatePart("Head", PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.78f, 0f), new Vector3(0.96f, 0.86f, 0.88f), body);
            CreatePart("Scarf", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.68f, 0.08f, 0.68f), scarf);
            CreatePart("Scarf Tail", PrimitiveType.Cube, root.transform, new Vector3(-0.37f, 0.34f, -0.14f), new Vector3(0.10f, 0.38f, 0.18f), scarf, new Vector3(0f, 0f, 18f));
            GameObject leftArm = CreatePart("Left Arm", PrimitiveType.Capsule, root.transform, new Vector3(-0.48f, 0.12f, 0f), new Vector3(0.22f, 0.42f, 0.22f), body, new Vector3(0f, 0f, -12f));
            GameObject rightArm = CreatePart("Right Arm", PrimitiveType.Capsule, root.transform, new Vector3(0.48f, 0.12f, 0f), new Vector3(0.22f, 0.42f, 0.22f), body, new Vector3(0f, 0f, 12f));
            CreatePart("Left Boot", PrimitiveType.Sphere, root.transform, new Vector3(-0.25f, -0.67f, 0.10f), new Vector3(0.34f, 0.20f, 0.46f), boot);
            CreatePart("Right Boot", PrimitiveType.Sphere, root.transform, new Vector3(0.25f, -0.67f, 0.10f), new Vector3(0.34f, 0.20f, 0.46f), boot);
            CreatePart("Left Eye", PrimitiveType.Sphere, root.transform, new Vector3(-0.20f, 0.84f, 0.43f), new Vector3(0.12f, 0.16f, 0.08f), eye);
            CreatePart("Right Eye", PrimitiveType.Sphere, root.transform, new Vector3(0.20f, 0.84f, 0.43f), new Vector3(0.12f, 0.16f, 0.08f), eye);
            EarthMagicPoseDriver pose = character.GetComponent<EarthMagicPoseDriver>();
            if (pose == null) pose = character.AddComponent<EarthMagicPoseDriver>();
            pose.Configure(input, executor, root.transform, leftArm.transform, rightArm.transform);
        }

        private static void CreateHumanoidPresentation(
            GameObject character,
            MagicInputController input,
            MagicExecutor executor,
            EarthPillarMobility pillarMobility,
            GravityWorldBehaviour gravityWorld)
        {
            ConfigureCharacterImporters();
            GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (characterPrefab == null)
            {
                Debug.LogWarning("[Elemental] Linebreaker character is unavailable; keeping the primitive presentation fallback.");
                return;
            }

            Avatar avatar = FindAvatar(CharacterModelPath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogWarning("[Elemental] Linebreaker character did not produce a valid Humanoid avatar; keeping the primitive presentation fallback.");
                return;
            }

            AnimatorController controller = CreateOrLoadMageController();
            CharacterPresentationProfile profile = CreateOrLoadProfile<CharacterPresentationProfile>(
                CharacterProfilePath,
                "Character Presentation Profile");
            profile.Configure(
                characterPrefab,
                controller,
                avatar,
                new Vector3(0f, -1.02f, 0f),
                Vector3.zero,
                Vector3.one * 2.02f);
            EditorUtility.SetDirty(profile);

            Transform old = character.transform.Find("KayKit Mage Presentation");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            old = character.transform.Find("KayKit Rogue Presentation");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            old = character.transform.Find("KayKit Knight Presentation");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            old = character.transform.Find("Mixamo X Bot Presentation");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            old = character.transform.Find("Linebreaker Presentation");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            GameObject presentationObject = PrefabUtility.InstantiatePrefab(characterPrefab) as GameObject;
            if (presentationObject == null) return;
            // The generated scene owns this rig. Keeping a live Model Prefab link
            // lets later AssetDatabase refreshes silently restore imported Animator
            // and material values, which previously removed the controller and the
            // Rumble shader from the player while leaving the rival intact.
            PrefabUtility.UnpackPrefabInstance(
                presentationObject,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            presentationObject.name = "Linebreaker Presentation";
            presentationObject.transform.SetParent(character.transform, false);
            presentationObject.transform.localPosition = profile.LocalPosition;
            presentationObject.transform.localRotation = profile.LocalRotation;
            presentationObject.transform.localScale = profile.LocalScale;

            foreach (Renderer renderer in presentationObject.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            ApplyPersistentRumbleCharacterMaterials(presentationObject, false);

            Animator animator = presentationObject.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = presentationObject.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            ConfigureSecondaryCharacterMotion(presentationObject, animator);

            foreach (Renderer renderer in character.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(presentationObject.transform)) continue;
                if (renderer is LineRenderer || renderer is ParticleSystemRenderer) continue;
                renderer.enabled = false;
            }
            GameObject puppetRoot = GameObject.Find("Earth Shaper Puppet");
            if (puppetRoot != null)
                foreach (Renderer renderer in puppetRoot.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;

            Transform targets = character.transform.Find("Humanoid Magic Targets");
            if (targets != null) Object.DestroyImmediate(targets.gameObject);
            GameObject targetRoot = new GameObject("Humanoid Magic Targets");
            targetRoot.transform.SetParent(character.transform, false);
            Transform leftTarget = CreatePoseTarget("Left Hand IK", targetRoot.transform, new Vector3(-0.34f, 0.55f, 0.58f));
            Transform rightTarget = CreatePoseTarget("Right Hand IK", targetRoot.transform, new Vector3(0.34f, 0.55f, 0.58f));

            ActiveRagdollPuppet puppet = character.GetComponent<ActiveRagdollPuppet>();
            EarthEffectsTuningProfile effectsProfile = CreateOrLoadEffectsProfile();
            ParticleSystem stoneFadeDust = CreateStoneFadeDust(presentationObject.transform, effectsProfile);
            HumanoidRagdollRig visibleRagdoll = presentationObject.GetComponent<HumanoidRagdollRig>();
            if (visibleRagdoll == null) visibleRagdoll = presentationObject.AddComponent<HumanoidRagdollRig>();
            visibleRagdoll.ConfigureAndBuild(
                animator,
                character.GetComponent<Rigidbody>(),
                character.GetComponent<Collider>(),
                gravityWorld,
                puppet,
                stoneFadeDust,
                puppet,
                character.GetComponent<PlanetMotor>(),
                input,
                character.GetComponent<PlanetInputReader>());
            visibleRagdoll.ConfigureEffectsProfile(effectsProfile);
            HumanoidCharacterPresentation presentation = presentationObject.GetComponent<HumanoidCharacterPresentation>();
            if (presentation == null) presentation = presentationObject.AddComponent<HumanoidCharacterPresentation>();
            presentation.Configure(
                profile,
                animator,
                leftTarget,
                rightTarget,
                character.GetComponent<PlanetMotor>(),
                character.GetComponent<Rigidbody>(),
                puppet,
                input,
                executor,
                CreateOrLoadProfile<EarthTechniquePresentationProfile>(
                    TechniquePresentationProfilePath,
                    "Earth Technique Presentation Profile"),
                pillarMobility,
                visibleRagdoll,
                true);
            HumanoidOrganicIdle organicIdle = presentationObject.GetComponent<HumanoidOrganicIdle>();
            if (organicIdle == null) organicIdle = presentationObject.AddComponent<HumanoidOrganicIdle>();
            organicIdle.Configure(
                animator,
                presentation,
                character.GetComponent<PlanetMotor>(),
                visibleRagdoll,
                profile.OrganicIdleBlendInSeconds,
                profile.OrganicIdleBlendOutSeconds);
            ConfigureEammBasePose(character, presentationObject, animator, presentation, visibleRagdoll, true);
            EarthStompContactPresenter stomp = presentationObject.GetComponent<EarthStompContactPresenter>();
            if (stomp == null) stomp = presentationObject.AddComponent<EarthStompContactPresenter>();
            stomp.Configure(pillarMobility);
            HumanoidRagdollBridge bridge = presentationObject.GetComponent<HumanoidRagdollBridge>();
            if (bridge != null) Object.DestroyImmediate(bridge);
        }

        private static void BindRigidCharacterPart(
            Transform presentationRoot,
            Animator animator,
            string partName,
            HumanBodyBones targetBone)
        {
            if (presentationRoot == null || animator == null) return;
            Transform part = null;
            foreach (Transform candidate in presentationRoot.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != partName) continue;
                part = candidate;
                break;
            }
            Transform bone = animator.GetBoneTransform(targetBone);
            if (part == null || bone == null || part == bone || part.IsChildOf(bone)) return;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer == null || renderer is SkinnedMeshRenderer) return;

            // KayKit characters use separate rigid visual pieces beside the Humanoid
            // skeleton. Humanoid retargeting animates the bones but not those sibling
            // mesh transforms, so bind each visible piece to its semantic bone while
            // preserving the authored bind pose.
            part.SetParent(bone, true);
        }

        private static void ConfigureCharacterImporters()
        {
            bool avatarChanged = ConfigureHumanoidImporter(
                CharacterModelPath,
                null,
                isAnimationSource: false);
            Avatar avatar = FindAvatar(CharacterModelPath);
            string[] mixamoAnimationPaths =
            {
                MixamoWalkPath,
                MixamoWalkBackPath,
                MixamoPunchPath
            };
            for (int index = 0; index < mixamoAnimationPaths.Length; index++)
                // Mixamo's FBX-for-Unity exporter is not consistent about retaining
                // the `mixamorig:` namespace between character and motion downloads.
                // Each motion therefore keeps its valid Humanoid Avatar; Mecanim
                // retargets it onto Linebreaker at runtime without requiring an
                // identical transform hierarchy.
                ConfigureHumanoidImporter(
                    mixamoAnimationPaths[index],
                    null,
                    isAnimationSource: true,
                    forceReimport: avatarChanged);

            // KayKit remains a temporary motion fallback until every curated
            // Mixamo semantic slot is present. The visible character and all
            // locomotion deformation are nevertheless driven by the skinned
            // Mixamo Humanoid, never by the old rigid-part Knight.
            string[] animationPaths =
            {
                "Assets/ThirdParty/KayKit/Animations/Rig_Medium_General.fbx",
                "Assets/ThirdParty/KayKit/Animations/Rig_Medium_MovementBasic.fbx",
                "Assets/ThirdParty/KayKit/Animations/Rig_Medium_MovementAdvanced.fbx",
                "Assets/ThirdParty/KayKit/Animations/Rig_Medium_CombatRanged.fbx"
            };
            for (int index = 0; index < animationPaths.Length; index++)
                // These clips use KayKit transform names and must keep their own
                // Humanoid Avatar when retargeted onto Mixamo X Bot.
                ConfigureHumanoidImporter(
                    animationPaths[index],
                    null,
                    isAnimationSource: true,
                    forceReimport: avatarChanged);

            EarthHumanoidMotionSetup.ConfigureCuratedImporters();
        }

        private static bool ConfigureHumanoidImporter(
            string path,
            Avatar sourceAvatar,
            bool isAnimationSource,
            bool forceReimport = false)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return false;
            ModelImporterAvatarSetup desiredSetup = sourceAvatar == null
                ? ModelImporterAvatarSetup.CreateFromThisModel
                : ModelImporterAvatarSetup.CopyFromOther;
            HumanDescription human = importer.humanDescription;
            bool translationDofChanged = human.hasTranslationDoF;
            bool animationLoopingChanged = ConfigureAnimationLooping(importer, isAnimationSource);
            bool dirty = forceReimport ||
                         importer.animationType != ModelImporterAnimationType.Human ||
                         importer.avatarSetup != desiredSetup ||
                         (sourceAvatar != null && importer.sourceAvatar != sourceAvatar) ||
                         (sourceAvatar == null && importer.sourceAvatar != null) ||
                         translationDofChanged ||
                         animationLoopingChanged;
            if (!dirty) return false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = desiredSetup;
            if (sourceAvatar != null)
            {
                importer.sourceAvatar = sourceAvatar;
            }
            else
            {
                // Clear stale CopyFromOther data before switching the importer back
                // to CreateFromThisModel. Unity otherwise keeps reporting a copied
                // Avatar hierarchy mismatch even though avatarSetup has changed.
                importer.sourceAvatar = null;

            }
            // Humanoid retargeting owns limb rotation; PlanetMotor owns root
            // translation. Translation DoF imported incompatible per-bone
            // offsets from otherwise valid source clips and made knees/feet jump
            // metres between adjacent samples. Keep it disabled on model Avatars
            // and on CopyFromOther motion importers alike.
            if (translationDofChanged)
            {
                human.hasTranslationDoF = false;
                importer.humanDescription = human;
            }
            importer.importAnimation = true;
            importer.SaveAndReimport();
            return true;
        }

        private static bool ConfigureAnimationLooping(ModelImporter importer, bool animationSource)
        {
            if (!animationSource) return false;
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return false;
            bool changed = false;
            for (int index = 0; index < clips.Length; index++)
            {
                ModelImporterClipAnimation clip = clips[index];
                bool shouldLoop = IsLoopingLocomotionClip(clip.name);
                if (clip.loopTime == shouldLoop && clip.loopPose == shouldLoop) continue;
                clip.loopTime = shouldLoop;
                clip.loopPose = shouldLoop;
                changed = true;
            }
            if (changed) importer.clipAnimations = clips;
            return changed;
        }

        private static bool IsLoopingLocomotionClip(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return false;
            string value = clipName.ToLowerInvariant();
            return value.Contains("idle") || value.Contains("walk") ||
                   value.Contains("run") || value.Contains("sprint") ||
                   value.Contains("strafe") || value.Contains("crouch") ||
                   value.Contains("crawl");
        }

        private static Avatar FindAvatar(string path)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int index = 0; index < assets.Length; index++)
                if (assets[index] is Avatar avatar) return avatar;
            return null;
        }

        private static AnimatorController CreateOrLoadMageController()
        {
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(MageControllerPath);
            if (existing != null)
            {
                UpgradeMageController(existing);
                return existing;
            }
            EnsureFolder("Assets/Elemental/Content/Animation");
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(MageControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Cast", AnimatorControllerParameterType.Bool);
            controller.AddParameter("CastKind", AnimatorControllerParameterType.Int);
            controller.AddParameter("Impact", AnimatorControllerParameterType.Trigger);
            AddChoreographyParameters(controller);

            List<AnimationClip> clips = LoadCharacterClips();
            AnimationClip idle = FindClip(clips, "idle");
            AnimationClip walk = FindClip(clips, "walk");
            AnimationClip run = FindClip(clips, "run");
            AnimationClip jump = FindClip(clips, "jump");
            AnimationClip fall = FindClip(clips, "fall");
            AnimationClip land = FindClip(clips, "land");
            AnimationClip cast = FindClip(clips, "spell", "cast", "shoot", "throw");
            AnimationClip impact = FindClip(clips, "hit", "damage", "stagger");

            BlendTree locomotion;
            AnimatorState locomotionState = controller.CreateBlendTreeInController("Locomotion", out locomotion, 0);
            locomotion.blendType = BlendTreeType.Simple1D;
            locomotion.blendParameter = "Speed";
            locomotion.useAutomaticThresholds = false;
            if (idle != null) locomotion.AddChild(idle, 0f);
            if (walk != null) locomotion.AddChild(walk, 2f);
            if (run != null) locomotion.AddChild(run, 6f);
            AnimatorStateMachine baseMachine = controller.layers[0].stateMachine;
            baseMachine.defaultState = locomotionState;
            AnimatorState jumpState = baseMachine.AddState("Jump");
            jumpState.motion = jump ?? fall ?? idle;
            AnimatorState fallState = baseMachine.AddState("Fall");
            fallState.motion = fall ?? jump ?? idle;
            AnimatorState landState = baseMachine.AddState("Land");
            landState.motion = land ?? idle;
            AddConditionTransition(baseMachine, locomotionState, jumpState, AnimatorConditionMode.IfNot, 0f, "Grounded", 0.08f);
            AddConditionTransition(baseMachine, jumpState, fallState, AnimatorConditionMode.Less, 0f, "VerticalSpeed", 0.08f);
            AddConditionTransition(baseMachine, fallState, landState, AnimatorConditionMode.If, 0f, "Grounded", 0.06f);
            AnimatorStateTransition returnToMove = landState.AddTransition(locomotionState);
            returnToMove.hasExitTime = true;
            returnToMove.exitTime = 0.72f;
            returnToMove.duration = 0.1f;

            AvatarMask upperMask = CreateOrLoadUpperBodyMask();
            controller.AddLayer("Earth Magic Upper Body");
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer magicLayer = layers[layers.Length - 1];
            magicLayer.avatarMask = upperMask;
            magicLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            magicLayer.defaultWeight = 1f;
            AnimatorState magicIdle = magicLayer.stateMachine.AddState("Ready");
            magicIdle.motion = idle;
            AnimatorState castState = magicLayer.stateMachine.AddState("Earth Cast");
            castState.motion = cast ?? idle;
            magicLayer.stateMachine.defaultState = magicIdle;
            AnimatorStateTransition castIn = magicIdle.AddTransition(castState);
            castIn.AddCondition(AnimatorConditionMode.If, 0f, "Cast");
            castIn.duration = 0.1f;
            AnimatorStateTransition castOut = castState.AddTransition(magicIdle);
            castOut.AddCondition(AnimatorConditionMode.IfNot, 0f, "Cast");
            castOut.duration = 0.12f;

            controller.AddLayer("Impact Additive");
            layers = controller.layers;
            AnimatorControllerLayer impactLayer = layers[layers.Length - 1];
            impactLayer.avatarMask = upperMask;
            impactLayer.blendingMode = AnimatorLayerBlendingMode.Additive;
            impactLayer.defaultWeight = 0.28f;
            AnimatorState calm = impactLayer.stateMachine.AddState("Calm");
            calm.motion = idle;
            AnimatorState recoil = impactLayer.stateMachine.AddState("Recoil");
            recoil.motion = impact ?? idle;
            impactLayer.stateMachine.defaultState = calm;
            AnimatorStateTransition recoilIn = calm.AddTransition(recoil);
            recoilIn.AddCondition(AnimatorConditionMode.If, 0f, "Impact");
            recoilIn.duration = 0.04f;
            AnimatorStateTransition recoilOut = recoil.AddTransition(calm);
            recoilOut.hasExitTime = true;
            recoilOut.exitTime = 0.8f;
            recoilOut.duration = 0.12f;
            controller.layers = layers;

            layers = controller.layers;
            for (int index = 0; index < layers.Length; index++)
            {
                layers[index].iKPass = true;
            }
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            UpgradeMageController(controller);
            return controller;
        }

        private static void UpgradeMageController(AnimatorController controller)
        {
            AddChoreographyParameters(controller);
            List<AnimationClip> clips = LoadCharacterClips();
            UpgradeMixamoLocomotion(controller, clips);
            if (controller.layers == null || controller.layers.Length < 2) return;
            AnimatorState castState = FindAnimatorState(
                controller.layers[1].stateMachine, "Earth Cast");
            if (castState == null) return;
            AnimationClip fallback = FindClip(clips, "Ranged_Magic_Spellcasting", "idle");
            AnimationClip punch = LoadAnimationClip(MixamoPunchPath);
            string[][] terms =
            {
                new[] { "Ranged_Magic_Raise", "PickUp" },
                new[] { "Throw", "Ranged_Magic_Shoot" },
                new[] { "Ranged_Magic_Shoot", "Throw" },
                new[] { "Ranged_Magic_Spellcasting_Long", "Ranged_Magic_Spellcasting" },
                new[] { "Ranged_Magic_Summon", "Ranged_Magic_Raise" },
                new[] { "Ranged_2H_Shoot", "Ranged_Magic_Shoot" },
                new[] { "Interact", "Ranged_Magic_Raise" },
                new[] { "Ranged_Bow_Release_Up", "Ranged_Magic_Summon" }
            };
            if (castState.motion is BlendTree existingTree && existingTree.children.Length >= 8 &&
                existingTree.blendParameter == "EarthPose")
            {
                ChildMotion[] children = existingTree.children;
                for (int index = 0; index < terms.Length && index < children.Length; index++)
                {
                    // Rebind every semantic slot after a Humanoid importer change.
                    // Unity sub-asset file IDs can legitimately change on reimport,
                    // leaving serialized BlendTree children null even though the FBX
                    // and its animation clip are both valid.
                    AnimationClip motion = index == 2 && punch != null
                        ? punch
                        : FindClip(clips, terms[index]) ?? fallback;
                    children[index].motion = motion;
                    children[index].threshold = index + 1f;
                }
                existingTree.children = children;
                EditorUtility.SetDirty(existingTree);
                EditorUtility.SetDirty(controller);
                EarthHumanoidMotionSetup.UpgradeController(controller);
                AssetDatabase.SaveAssets();
                return;
            }

            var tree = new BlendTree
            {
                name = "Earth Hero Casts",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "EarthPose",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            for (int index = 0; index < terms.Length; index++)
            {
                AnimationClip motion = index == 2 && punch != null
                    ? punch
                    : FindClip(clips, terms[index]) ?? fallback;
                tree.AddChild(motion, index + 1f);
            }
            castState.motion = tree;
            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(castState);
            EditorUtility.SetDirty(controller);
            EarthHumanoidMotionSetup.UpgradeController(controller);
            AssetDatabase.SaveAssets();
        }

        private static void AddChoreographyParameters(AnimatorController controller)
        {
            AddParameterIfMissing(controller, "EarthEffort", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "EarthBrace", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "EarthGrounding", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "EarthPrecision", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "EarthPhase", AnimatorControllerParameterType.Int);
            AddParameterIfMissing(controller, "EarthDialect", AnimatorControllerParameterType.Int);
            AddParameterIfMissing(controller, "EarthPose", AnimatorControllerParameterType.Float);
        }

        private static void AddParameterIfMissing(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType type)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int index = 0; index < parameters.Length; index++)
                if (parameters[index].name == parameterName) return;
            controller.AddParameter(parameterName, type);
        }

        private static AnimatorState FindAnimatorState(AnimatorStateMachine machine, string stateName)
        {
            if (machine == null) return null;
            ChildAnimatorState[] states = machine.states;
            for (int index = 0; index < states.Length; index++)
                if (states[index].state != null && states[index].state.name == stateName)
                    return states[index].state;
            ChildAnimatorStateMachine[] children = machine.stateMachines;
            for (int index = 0; index < children.Length; index++)
            {
                AnimatorState found = FindAnimatorState(children[index].stateMachine, stateName);
                if (found != null) return found;
            }
            return null;
        }

        private static void AddConditionTransition(
            AnimatorStateMachine machine,
            AnimatorState from,
            AnimatorState to,
            AnimatorConditionMode mode,
            float threshold,
            string parameter,
            float duration)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = duration;
            transition.AddCondition(mode, threshold, parameter);
        }

        private static void UpgradeMixamoLocomotion(
            AnimatorController controller,
            List<AnimationClip> fallbackClips)
        {
            if (controller.layers == null || controller.layers.Length == 0) return;
            AnimatorState locomotionState = FindAnimatorState(
                controller.layers[0].stateMachine, "Locomotion");
            if (locomotionState?.motion is not BlendTree locomotion) return;

            AnimationClip walk = LoadAnimationClip(MixamoWalkPath);
            AnimationClip walkBack = LoadAnimationClip(MixamoWalkBackPath);
            AnimationClip idle = FindClip(fallbackClips, "idle");
            AnimationClip run = FindClip(fallbackClips, "run");
            var motions = new List<ChildMotion>(4);
            if (walkBack != null)
                motions.Add(new ChildMotion { motion = walkBack, threshold = -2f, timeScale = 1f });
            if (idle != null)
                motions.Add(new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f });
            if (walk != null)
                motions.Add(new ChildMotion { motion = walk, threshold = 2f, timeScale = 1f });
            if (run != null)
                motions.Add(new ChildMotion { motion = run, threshold = 6f, timeScale = 1f });
            if (motions.Count < 2) return;

            locomotion.blendType = BlendTreeType.Simple1D;
            locomotion.blendParameter = "Speed";
            locomotion.useAutomaticThresholds = false;
            locomotion.children = motions.ToArray();
            EditorUtility.SetDirty(locomotion);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip LoadAnimationClip(string path)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int index = 0; index < assets.Length; index++)
                if (assets[index] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            return null;
        }

        private static List<AnimationClip> LoadCharacterClips()
        {
            var clips = new List<AnimationClip>(64);
            string[] paths =
            {
                MixamoWalkPath,
                MixamoWalkBackPath,
                MixamoPunchPath,
                "Assets/ThirdParty/KayKit/Animations/Rig_Medium_General.fbx",
                "Assets/ThirdParty/KayKit/Animations/Rig_Medium_MovementBasic.fbx",
                "Assets/ThirdParty/KayKit/Animations/Rig_Medium_MovementAdvanced.fbx",
                "Assets/ThirdParty/KayKit/Animations/Rig_Medium_CombatRanged.fbx"
            };
            for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(paths[pathIndex]);
                for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                {
                    if (assets[assetIndex] is AnimationClip clip && !clip.name.StartsWith("__preview__")) clips.Add(clip);
                }
            }
            return clips;
        }

        private static AnimationClip FindClip(List<AnimationClip> clips, params string[] terms)
        {
            for (int termIndex = 0; termIndex < terms.Length; termIndex++)
                for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
                    if (clips[clipIndex].name.IndexOf(terms[termIndex], System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return clips[clipIndex];
            return clips.Count > 0 ? clips[0] : null;
        }

        private static AvatarMask CreateOrLoadUpperBodyMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MageMaskPath);
            if (mask != null) return mask;
            mask = new AvatarMask { name = "KayKit Mage Upper Body" };
            for (int index = 0; index < (int)AvatarMaskBodyPart.LastBodyPart; index++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)index, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            AssetDatabase.CreateAsset(mask, MageMaskPath);
            return mask;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static void CreateActivePuppetVisual(
            GameObject character,
            MagicInputController input,
            MagicExecutor executor,
            GravityWorldBehaviour gravityWorld,
            Material bodyMaterial,
            Material scarfMaterial,
            Material eyeMaterial,
            Material bootMaterial)
        {
            Rigidbody rootBody = character.GetComponent<Rigidbody>();
            PlanetMotor motor = character.GetComponent<PlanetMotor>();
            rootBody.mass = 12f;
            GameObject targetsRootObject = new GameObject("Earth Shaper Visual");
            targetsRootObject.transform.SetParent(character.transform, false);
            targetsRootObject.transform.localPosition = new Vector3(0f, -0.12f, 0f);
            Transform targetsRoot = targetsRootObject.transform;
            GameObject physicalRootObject = new GameObject("Earth Shaper Puppet");
            // Never parent simulated rigidbodies below the character rigidbody. Moving a
            // Rigidbody ancestor also moves every child transform outside PhysX and can
            // inject unbounded joint error into an active ragdoll.
            physicalRootObject.transform.SetParent(character.transform.parent, true);
            physicalRootObject.transform.SetPositionAndRotation(
                character.transform.position + (character.transform.up * -0.12f),
                character.transform.rotation);

            Transform chestTarget = CreatePoseTarget("Chest Target", targetsRoot, new Vector3(0f, 0.42f, 0f));
            Transform headTarget = CreatePoseTarget("Head Target", targetsRoot, new Vector3(0f, 1.18f, 0f));
            Transform leftArmTarget = CreatePoseTarget("Left Arm Target", targetsRoot, new Vector3(-0.54f, 0.42f, 0f));
            Transform rightArmTarget = CreatePoseTarget("Right Arm Target", targetsRoot, new Vector3(0.54f, 0.42f, 0f));
            Transform leftUpperTarget = CreatePoseTarget("Left Upper Leg Target", targetsRoot, new Vector3(-0.24f, -0.36f, 0f));
            Transform rightUpperTarget = CreatePoseTarget("Right Upper Leg Target", targetsRoot, new Vector3(0.24f, -0.36f, 0f));
            Transform leftLowerTarget = CreatePoseTarget("Left Lower Leg Target", targetsRoot, new Vector3(-0.24f, -0.88f, 0.04f));
            Transform rightLowerTarget = CreatePoseTarget("Right Lower Leg Target", targetsRoot, new Vector3(0.24f, -0.88f, 0.04f));

            CreatePart("Pelvis Body", PrimitiveType.Capsule, character.transform,
                new Vector3(0f, -0.12f, 0f), new Vector3(0.66f, 0.46f, 0.56f), bodyMaterial);
            EarthPuppetPart chest = CreateEarthPuppetPart(
                "Puppet Chest", PrimitiveType.Capsule, physicalRootObject.transform, chestTarget,
                new Vector3(0f, 0.42f, 0f), new Vector3(0.74f, 0.52f, 0.58f),
                bodyMaterial, 7f, rootBody, gravityWorld, 36f);
            EarthPuppetPart head = CreateEarthPuppetPart(
                "Puppet Head", PrimitiveType.Sphere, physicalRootObject.transform, headTarget,
                new Vector3(0f, 1.18f, 0f), new Vector3(0.92f, 0.82f, 0.86f),
                bodyMaterial, 3f, chest.Body, gravityWorld, 42f);
            EarthPuppetPart leftArm = CreateEarthPuppetPart(
                "Puppet Arm L", PrimitiveType.Capsule, physicalRootObject.transform, leftArmTarget,
                new Vector3(-0.54f, 0.42f, 0f), new Vector3(0.22f, 0.46f, 0.22f),
                bodyMaterial, 2f, chest.Body, gravityWorld, 58f);
            EarthPuppetPart rightArm = CreateEarthPuppetPart(
                "Puppet Arm R", PrimitiveType.Capsule, physicalRootObject.transform, rightArmTarget,
                new Vector3(0.54f, 0.42f, 0f), new Vector3(0.22f, 0.46f, 0.22f),
                bodyMaterial, 2f, chest.Body, gravityWorld, 58f);
            EarthPuppetPart leftUpper = CreateEarthPuppetPart(
                "Puppet Upper Leg L", PrimitiveType.Capsule, physicalRootObject.transform, leftUpperTarget,
                new Vector3(-0.24f, -0.36f, 0f), new Vector3(0.28f, 0.42f, 0.28f),
                bodyMaterial, 3.5f, rootBody, gravityWorld, 48f);
            EarthPuppetPart rightUpper = CreateEarthPuppetPart(
                "Puppet Upper Leg R", PrimitiveType.Capsule, physicalRootObject.transform, rightUpperTarget,
                new Vector3(0.24f, -0.36f, 0f), new Vector3(0.28f, 0.42f, 0.28f),
                bodyMaterial, 3.5f, rootBody, gravityWorld, 48f);
            EarthPuppetPart leftLower = CreateEarthPuppetPart(
                "Puppet Lower Leg L", PrimitiveType.Capsule, physicalRootObject.transform, leftLowerTarget,
                new Vector3(-0.24f, -0.88f, 0.04f), new Vector3(0.30f, 0.42f, 0.32f),
                bootMaterial, 2.5f, leftUpper.Body, gravityWorld, 52f);
            EarthPuppetPart rightLower = CreateEarthPuppetPart(
                "Puppet Lower Leg R", PrimitiveType.Capsule, physicalRootObject.transform, rightLowerTarget,
                new Vector3(0.24f, -0.88f, 0.04f), new Vector3(0.30f, 0.42f, 0.32f),
                bootMaterial, 2.5f, rightUpper.Body, gravityWorld, 52f);

            CreatePart("Scarf", PrimitiveType.Cylinder, chest.Transform,
                new Vector3(0f, 0.42f, 0f), new Vector3(0.68f, 0.08f, 0.68f), scarfMaterial);
            CreatePart("Left Eye", PrimitiveType.Sphere, head.Transform,
                new Vector3(-0.20f, 0.06f, 0.43f), new Vector3(0.12f, 0.16f, 0.08f), eyeMaterial);
            CreatePart("Right Eye", PrimitiveType.Sphere, head.Transform,
                new Vector3(0.20f, 0.06f, 0.43f), new Vector3(0.12f, 0.16f, 0.08f), eyeMaterial);

            PhysicalImpactTarget impactTarget = character.GetComponent<PhysicalImpactTarget>();
            if (impactTarget == null) impactTarget = character.AddComponent<PhysicalImpactTarget>();
            impactTarget.Configure(rootBody, 0.34f);
            ActiveRagdollPuppet oldPuppet = character.GetComponent<ActiveRagdollPuppet>();
            if (oldPuppet != null) Object.DestroyImmediate(oldPuppet);
            ActiveRagdollPuppet puppet = character.AddComponent<ActiveRagdollPuppet>();
            ActiveRagdollJoint[] joints =
            {
                chest.Joint, head.Joint, leftArm.Joint, rightArm.Joint,
                leftUpper.Joint, rightUpper.Joint, leftLower.Joint, rightLower.Joint
            };
            Collider[] selfColliders =
            {
                character.GetComponent<Collider>(), chest.Collider, head.Collider,
                leftArm.Collider, rightArm.Collider, leftUpper.Collider,
                rightUpper.Collider, leftLower.Collider, rightLower.Collider
            };
            puppet.Configure(
                1u, gravityWorld, rootBody, motor, impactTarget,
                chest.Transform, joints, selfColliders);
            puppet.ConfigureControlBehaviours(input, character.GetComponent<PlanetInputReader>());

            EarthMagicPoseDriver pose = character.GetComponent<EarthMagicPoseDriver>();
            if (pose == null) pose = character.AddComponent<EarthMagicPoseDriver>();
            pose.Configure(input, executor, targetsRoot, leftArmTarget, rightArmTarget);
            EarthShaperLocomotionDriver locomotion = character.GetComponent<EarthShaperLocomotionDriver>();
            if (locomotion == null) locomotion = character.AddComponent<EarthShaperLocomotionDriver>();
            locomotion.Configure(
                rootBody, motor, puppet, executor, targetsRoot, chestTarget,
                leftArmTarget, rightArmTarget, leftUpperTarget, leftLowerTarget,
                rightUpperTarget, rightLowerTarget);
        }

        private static Transform CreatePoseTarget(string name, Transform parent, Vector3 localPosition)
        {
            GameObject target = new GameObject(name);
            target.transform.SetParent(parent, false);
            target.transform.localPosition = localPosition;
            target.transform.localRotation = Quaternion.identity;
            return target.transform;
        }

        private static EarthPuppetPart CreateEarthPuppetPart(
            string name,
            PrimitiveType primitive,
            Transform parent,
            Transform target,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            float mass,
            Rigidbody connectedBody,
            GravityWorldBehaviour gravityWorld,
            float angularLimit)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = 20f;
            GravityBody gravityBody = go.AddComponent<GravityBody>();
            gravityBody.Configure(gravityWorld, body);
            ConfigurableJoint joint = go.AddComponent<ConfigurableJoint>();
            joint.connectedBody = connectedBody;
            joint.autoConfigureConnectedAnchor = true;
            ActiveRagdollJoint driver = go.AddComponent<ActiveRagdollJoint>();
            driver.Configure(body, joint, target, 900f, 65f, 1400f, angularLimit);
            return new EarthPuppetPart(go.transform, body, go.GetComponent<Collider>(), driver);
        }

        private static void CreatePlanetLandmarks(
            Material earthMaterial,
            EarthCoreVisualStyle style,
            float planetRadius,
            GravityWorldBehaviour gravityWorld,
            EarthRockDebrisPool debrisPool)
        {
            Transform old = GameObject.Find("Earth Diorama Landmarks")?.transform;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            GameObject root = new GameObject("Earth Diorama Landmarks");
            Material sandstone = LoadRumbleMaterial("RumbleSandstone.mat") ?? earthMaterial;
            Material limestone = LoadRumbleMaterial("RumbleLimestone.mat") ?? earthMaterial;
            Material basalt = LoadRumbleMaterial("RumbleBasalt.mat") ?? earthMaterial;
            Material[] materials = { sandstone, limestone, basalt };

            Vector3[] dirs =
            {
                new Vector3(-0.48f, 0.86f, 0.18f).normalized,
                new Vector3(0.52f, 0.83f, 0.20f).normalized,
                new Vector3(-0.46f, 0.84f, -0.29f).normalized,
                new Vector3(0.55f, 0.80f, -0.24f).normalized,
                new Vector3(-0.16f, 0.78f, 0.61f).normalized,
                new Vector3(0.22f, 0.75f, 0.63f).normalized
            };
            for (int i = 0; i < dirs.Length; i++)
            {
                Mesh mesh = LoadRumbleMesh($"V5_Boulder_{i % 8:00}.asset");
                Vector3 up = dirs[i];
                Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
                if (forward.sqrMagnitude < 0.1f) forward = Vector3.ProjectOnPlane(Vector3.right, up).normalized;
                Quaternion rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(0f, i * 47f, 0f);
                float scale = 1.15f + (i % 3) * 0.28f;
                CreateRumbleRock(
                    $"Rumble Formation {i + 1:00}",
                    mesh,
                    materials[i % materials.Length],
                    root.transform,
                    up * planetRadius,
                    rotation,
                    new Vector3(scale * 1.18f, scale, scale),
                    true,
                    400 + i,
                    Vector3.zero,
                    gravityWorld,
                    debrisPool);
            }
        }

        private static void CreateRumbleAmphitheatre(
            Vector3 planetCenter,
            float planetRadius,
            GravityWorldBehaviour gravityWorld,
            EarthRockDebrisPool debrisPool)
        {
            GameObject old = GameObject.Find("Rumble Stone Amphitheatre");
            if (old != null) Object.DestroyImmediate(old);
            GameObject root = new GameObject("Rumble Stone Amphitheatre");
            Material sandstone = LoadRumbleMaterial("RumbleSandstone.mat");
            Material[] materials = { sandstone };

            // Two low, regular sandstone tiers read as a deliberate fighting court.
            // The open front keeps the player entrance and camera sightline clear.
            CreateAmphitheatreRing(root.transform, planetCenter, planetRadius, 7.15f, 12, -136f, 136f,
                "V5_Slab_", 8, 4, materials, 0, gravityWorld, debrisPool);
            CreateAmphitheatreRing(root.transform, planetCenter, planetRadius, 8.55f, 14, -138f, 138f,
                "V5_Slab_", 8, 4, materials, 100, gravityWorld, debrisPool);
        }

        private static void CreateAmphitheatreRing(
            Transform parent,
            Vector3 planetCenter,
            float planetRadius,
            float ringRadius,
            int count,
            float minimumAngle,
            float maximumAngle,
            string meshPrefix,
            int meshStart,
            int meshCount,
            Material[] materials,
            int seedOffset,
            GravityWorldBehaviour gravityWorld,
            EarthRockDebrisPool debrisPool)
        {
            for (int index = 0; index < count; index++)
            {
                float angleDegrees = Mathf.Lerp(minimumAngle, maximumAngle, index / (float)(count - 1));
                float angle = angleDegrees * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Sin(angle) * ringRadius, 0f, Mathf.Cos(angle) * ringRadius);
                Vector3 surface = ProjectTangentPointToPlanet(planetCenter, planetRadius, offset);
                Vector3 up = (surface - planetCenter).normalized;
                Vector3 forward = Vector3.ProjectOnPlane(offset, up).normalized;
                if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
                Quaternion rotation = Quaternion.LookRotation(forward, up) *
                                      Quaternion.Euler(0f, (index % 2 == 0 ? -1.5f : 1.5f), 0f);
                bool outer = ringRadius > 8f;
                Vector3 scale = outer
                    ? new Vector3(0.72f, 0.48f, 0.62f)
                    : new Vector3(0.68f, 0.32f, 0.60f);
                CreateRumbleRock(
                    outer ? $"Outer Stand {index + 1:00}" : $"Inner Stand {index + 1:00}",
                    LoadRumbleMesh($"{meshPrefix}{meshStart + index % meshCount:00}.asset"),
                    materials[index % materials.Length],
                    parent,
                    surface,
                    rotation,
                    scale,
                    true,
                    seedOffset + index,
                    planetCenter,
                    gravityWorld,
                    debrisPool);
            }
        }

        private static Vector3 ProjectTangentPointToPlanet(Vector3 center, float radius, Vector3 tangentOffset)
        {
            float planarRadiusSq = tangentOffset.x * tangentOffset.x + tangentOffset.z * tangentOffset.z;
            float y = Mathf.Sqrt(Mathf.Max(0.01f, radius * radius - planarRadiusSq));
            return center + new Vector3(tangentOffset.x, y, tangentOffset.z);
        }

        private static void CreateStarField(EarthCoreVisualStyle style)
        {
            GameObject old = GameObject.Find("Diorama Star Field");
            if (old != null) Object.DestroyImmediate(old);
            GameObject root = new GameObject("Diorama Star Field");
            var random = new System.Random(0xE17);
            Material star = CreateOrLoadLitMaterial("DioramaStars.mat", new Color(0.55f, 0.68f, 1f), 0f, new Color(0.7f, 0.82f, 1f) * 2f);
            for (int i = 0; i < 42; i++)
            {
                float x = (float)(random.NextDouble() * 72.0 - 36.0);
                float y = (float)(random.NextDouble() * 30.0 + 10.0);
                float z = (float)(random.NextDouble() * 34.0 + 15.0);
                float size = (float)(0.025 + random.NextDouble() * 0.08);
                CreatePart($"Star {i:00}", PrimitiveType.Sphere, root.transform, new Vector3(x, y, z), Vector3.one * size, star);
            }
        }

        private static void CreateWorldAndSpace(
            UnityEngine.Camera camera,
            MagicExecutor executor,
            Transform planetCenter,
            PlanetWorldProfile worldProfile,
            EarthCoreVisualStyle style)
        {
            GameObject oldStars = GameObject.Find("Diorama Star Field");
            if (oldStars != null) Object.DestroyImmediate(oldStars);
            new GameObject("Diorama Star Field");

            GameObject oldBackdrop = GameObject.Find("Celestial Diorama Backdrop");
            if (oldBackdrop != null) Object.DestroyImmediate(oldBackdrop);
            GameObject backdrop = new GameObject("Celestial Diorama Backdrop");

            CelestialSystemProfile celestial = CreateOrLoadProfile<CelestialSystemProfile>(CelestialProfilePath, "Celestial System Profile");
            AtmosphereProfile atmosphere = CreateOrLoadProfile<AtmosphereProfile>(AtmosphereProfilePath, "Atmosphere Profile");
            EarthSkyProfile skyProfile = CreateOrLoadProfile<EarthSkyProfile>(SkyProfilePath, "Earth Sky Profile");
            MeteorShowerProfile meteors = CreateOrLoadProfile<MeteorShowerProfile>(MeteorProfilePath, "Meteor Shower Profile");
            EarthEffectsTuningProfile effectsProfile = CreateOrLoadEffectsProfile();
            CreateOrLoadProfile<CharacterPresentationProfile>(CharacterProfilePath, "Character Presentation Profile");
            CreateOrLoadProfile<EarthPhysicsFeelProfile>(PhysicsFeelProfilePath, "Earth Physics Feel Profile");

            Material sky = DayNightSkyRestore.PrepareSkyMaterial(skyProfile);
            Material fullscreenAtmosphere = CreateOrLoadShaderMaterial(
                "AtmosphereFullscreen.mat",
                "Elemental/Atmosphere Fullscreen");
            ConfigureAtmosphereRendererFeature(fullscreenAtmosphere);
            Material cinematicDepthOfField = CreateOrLoadShaderMaterial(
                "EarthCinematicDepthOfField.mat",
                "Hidden/Elemental/Cinematic Depth Of Field");
            ConfigureCinematicDepthOfFieldRendererFeature(cinematicDepthOfField);
            EarthCinematicDepthOfFieldController depthOfFieldController =
                camera.GetComponent<EarthCinematicDepthOfFieldController>();
            if (depthOfFieldController == null)
                depthOfFieldController = camera.gameObject.AddComponent<
                    EarthCinematicDepthOfFieldController>();
            EditorUtility.SetDirty(depthOfFieldController);
            sky.SetColor("_Tint", new Color(0.42f, 0.58f, 1f));
            sky.SetFloat("_Seed", skyProfile.StarSeed);
            RenderSettings.skybox = sky;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, celestial.ScaledSpaceDistance * 1.35f);

            Material sunMaterial = CreateOrLoadUnlitMaterial("ScaledSun.mat", celestial.SunDiscColor);
            Material moonMaterial = CreateOrLoadLitMaterial("ScaledMoon.mat", celestial.MoonColor, 0.12f, Color.black);
            Material distantMaterial = CreateOrLoadLitMaterial("ScaledPlanet.mat", celestial.DistantPlanetColor, 0.22f, Color.black);
            GameObject sunDisc = CreatePart("Visible Sun", PrimitiveType.Sphere, backdrop.transform, Vector3.zero, Vector3.one, sunMaterial);
            sunDisc.SetActive(false); // The sky shader owns the sole solar disc.
            GameObject moon = CreatePart("Distant Moon", PrimitiveType.Sphere, backdrop.transform, Vector3.zero, Vector3.one, moonMaterial);
            GameObject farPlanet = CreatePart("Ringed Ember Planet", PrimitiveType.Sphere, backdrop.transform, Vector3.zero, Vector3.one, distantMaterial);

            GameObject atmosphereObject = CreatePart(
                "Planet Atmosphere Limb",
                PrimitiveType.Sphere,
                null,
                planetCenter.position,
                Vector3.one * worldProfile.Radius * 2f * atmosphere.OuterRadiusMultiplier,
                CreateOrLoadShaderMaterial("PlanetAtmosphere.mat", "Elemental/Atmosphere Shell"));
            atmosphereObject.transform.SetParent(planetCenter, true);
            MeshRenderer atmosphereRenderer = atmosphereObject.GetComponent<MeshRenderer>();
            atmosphereRenderer.shadowCastingMode = ShadowCastingMode.Off;
            atmosphereRenderer.receiveShadows = false;

            Light sunLight = GameObject.Find("Sun")?.GetComponent<Light>();
            EarthSkyController skyController = backdrop.AddComponent<EarthSkyController>();
            skyController.Configure(skyProfile, camera, sky);
            CelestialSystemBehaviour system = backdrop.AddComponent<CelestialSystemBehaviour>();
            // Arena content is integrated later in this generator. Author its stable
            // north-pole lighting reference explicitly, independent of camera position.
            GameObject lightingAnchor = new GameObject("Celestial Lighting Anchor");
            lightingAnchor.transform.SetParent(backdrop.transform, false);
            lightingAnchor.transform.position = planetCenter.position + Vector3.up * worldProfile.Radius;
            system.ConfigureLightingAnchor(lightingAnchor.transform);
            system.Configure(
                celestial,
                atmosphere,
                skyProfile,
                planetCenter,
                camera,
                sunLight,
                null,
                moon.transform,
                farPlanet.transform,
                atmosphereRenderer,
                sky);

            GameObject oldMeteors = GameObject.Find("Meteor Shower Runtime");
            if (oldMeteors != null) Object.DestroyImmediate(oldMeteors);
            GameObject meteorRoot = new GameObject("Meteor Shower Runtime");
            Material meteorMaterial = CreateOrLoadLitMaterial(
                "MeteorStone.mat",
                new Color(0.16f, 0.11f, 0.08f),
                0.08f,
                new Color(1.8f, 0.24f, 0.025f));
            ParticleSystem streaks = CreateDistantMeteorStreaks(
                meteorRoot.transform,
                celestial,
                meteors,
                effectsProfile);
            MeteorShowerBehaviour meteorSystem = meteorRoot.AddComponent<MeteorShowerBehaviour>();
            meteorSystem.ConfigurePhysicsFeel(CreateOrLoadProfile<EarthPhysicsFeelProfile>(
                PhysicsFeelProfilePath,
                "Earth Physics Feel Profile"));
            meteorSystem.Configure(
                meteors,
                Object.FindAnyObjectByType<VoxelPlanetBehaviour>(),
                planetCenter,
                executor,
                meteorMaterial,
                streaks,
                effectsProfile);
        }

        private static ParticleSystem CreateDistantMeteorStreaks(
            Transform parent,
            CelestialSystemProfile celestial,
            MeteorShowerProfile profile,
            EarthEffectsTuningProfile effectsProfile)
        {
            GameObject streakObject = new GameObject("Scaled Space Meteor Streaks");
            streakObject.transform.SetParent(parent, false);
            ParticleSystem particles = streakObject.AddComponent<ParticleSystem>();
            EarthMeteorEffectsTuning tuning = effectsProfile != null ? effectsProfile.Meteor : null;
            if (tuning != null)
                EarthParticleSystemTuningApplier.Apply(
                    particles,
                    tuning.Streaks,
                    effectsProfile.Materials.MeteorStreaks);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            if (tuning == null)
            {
                main.maxParticles = profile.DistantPoolSize;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(55f, 95f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
            }
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = tuning != null ? tuning.DistantRate : profile.DistantRatePerSecond;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Min(
                tuning != null ? tuning.Radius : 240f,
                celestial.ScaledSpaceDistance * 0.2f);
            shape.radiusThickness = tuning != null ? tuning.RadiusThickness : 0.05f;
            ParticleSystemRenderer renderer = streakObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = tuning != null ? tuning.VelocityScale : 0.12f;
            renderer.lengthScale = tuning != null ? tuning.LengthScale : 3.5f;
            renderer.sharedMaterial = effectsProfile != null
                ? effectsProfile.Materials.MeteorStreaks
                : CreateOrLoadUnlitMaterial("MeteorStreak.mat", new Color(1.8f, 0.62f, 0.18f));
            return particles;
        }

        private static T CreateOrLoadProfile<T>(string path, string displayName) where T : ScriptableObject
        {
            T profile = AssetDatabase.LoadAssetAtPath<T>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<T>();
            profile.name = displayName;
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        internal static EarthEffectsTuningProfile CreateOrLoadEffectsProfile()
        {
            EarthEffectsTuningProfile profile = CreateOrLoadProfile<EarthEffectsTuningProfile>(
                EarthEffectsProfilePath,
                "Earth Effects Tuning Profile");
            if (profile.SchemaVersion >= EarthEffectsTuningProfile.CurrentSchemaVersion) return profile;

            Material dust = LoadRumbleMaterial("RumbleDustLit.mat");
            Material sparks = CreateOrLoadLitMaterial(
                "AmberShardVfx.mat",
                new Color(1f, 0.38f, 0.045f),
                0.04f,
                new Color(2f, 0.42f, 0.035f));
            Material rubble = CreateOrLoadLitMaterial(
                "LooseEarthChipVfx.mat",
                new Color(0.54f, 0.38f, 0.23f),
                0.04f,
                Color.black);
            Material surfTrail = LoadRumbleMaterial("RumbleSandstone.mat");
            Material ambient = CreateOrLoadShaderMaterial(
                "LightDustMote.mat",
                "Elemental/Light Dust Mote");
            Material meteor = CreateOrLoadUnlitMaterial(
                "MeteorStreak.mat",
                new Color(1.8f, 0.62f, 0.18f));
            Material pillar = LoadRumbleMaterial("RumbleSandstone.mat");
            if (dust == null || sparks == null || rubble == null || surfTrail == null ||
                ambient == null || meteor == null || pillar == null)
                throw new UnityEditor.Build.BuildFailedException(
                    "EarthEffectsTuningProfile could not resolve every required effect material.");
            profile.InitializeAuthoringDefaults(
                dust,
                sparks,
                rubble,
                surfTrail,
                ambient,
                meteor,
                pillar);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static Material CreateOrLoadShaderMaterial(string fileName, string shaderName)
        {
            string path = "Assets/Elemental/Content/Materials/" + fileName;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new UnityEditor.Build.BuildFailedException($"{shaderName} shader was not found.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(fileName) };
                AssetDatabase.CreateAsset(material, path);
            }
            else MaterialShaderStateUtility.RebindShader(material, shader);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static VisualEffect CreateVfxGraphLayer(Transform parent, string name)
        {
            const string template = "Packages/com.unity.visualeffectgraph/Editor/Templates/Simple_Burst.vfx";
            string assetName = name.Replace(" ", string.Empty) + ".vfx";
            string path = "Assets/Elemental/Content/VFX/" + assetName;
            VisualEffectAsset asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (asset == null)
            {
                System.IO.Directory.CreateDirectory("Assets/Elemental/Content/VFX");
                if (!AssetDatabase.CopyAsset(template, path))
                    throw new UnityEditor.Build.BuildFailedException($"Unable to create VFX Graph asset from {template}.");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            }
            if (asset == null)
                throw new UnityEditor.Build.BuildFailedException($"VFX Graph asset was not imported: {path}");

            GameObject layer = new GameObject(name);
            layer.transform.SetParent(parent, false);
            VisualEffect effect = layer.AddComponent<VisualEffect>();
            effect.visualEffectAsset = asset;
            effect.pause = false;
            effect.Stop();
            return effect;
        }

        private static void ConfigureMiniBokeh(
            Camera camera,
            Transform enemyFocusProxy)
        {
            if (camera == null || enemyFocusProxy == null || enemyFocusProxy.parent == null) return;

            ConfigureMiniBokehRendererFeature();

            GameObject oldPlane = GameObject.Find("ArenaBokehPlane");
            if (oldPlane != null) Object.DestroyImmediate(oldPlane);
            GameObject plane = new GameObject("ArenaBokehPlane");
            plane.transform.SetParent(camera.transform, false);
            plane.transform.localPosition = new Vector3(0f, 0f, 43f);
            plane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            MiniBokehController controller = camera.GetComponent<MiniBokehController>();
            if (controller == null)
                controller = camera.gameObject.AddComponent<MiniBokehController>();
            controller.enabled = false;
            controller.ReferencePlane = plane.transform;
            controller.AutoFocus = false;
            controller.FocusDistance = 43f;
            controller.BokehStrength = 2f;
            controller.MaxBlurRadius = 0.9f;
            controller.BoundaryFade = 0.56f;
            controller.DownsampleMode = MiniBokehController.ResolutionMode.Half;
            controller.BokehMode = MiniBokehController.BokehType.Circular;

            EarthMiniBokehCameraPlane cameraPlane =
                camera.GetComponent<EarthMiniBokehCameraPlane>();
            if (cameraPlane == null)
                cameraPlane = camera.gameObject.AddComponent<EarthMiniBokehCameraPlane>();
            cameraPlane.Configure(controller, plane.transform);
            cameraPlane.enabled = false;

            EarthMiniBokehFocus focus = camera.GetComponent<EarthMiniBokehFocus>();
            if (focus == null) focus = camera.gameObject.AddComponent<EarthMiniBokehFocus>();
            focus.enabled = false;
            focus.Configure(enemyFocusProxy, controller, plane.transform);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(cameraPlane);
            EditorUtility.SetDirty(focus);
        }

        private static void ConfigureMiniBokehRendererFeature()
        {
            UniversalRenderPipelineAsset pipeline =
                GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            SerializedObject pipelineObject = new SerializedObject(pipeline);
            SerializedProperty rendererList = pipelineObject.FindProperty("m_RendererDataList");
            ScriptableRendererData rendererData = rendererList != null && rendererList.arraySize > 0
                ? rendererList.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData
                : null;
            if (rendererData == null) return;

            MiniBokehFeature feature = null;
            for (int index = 0; index < rendererData.rendererFeatures.Count; index++)
                if (rendererData.rendererFeatures[index] is MiniBokehFeature existing)
                    feature = existing;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<MiniBokehFeature>();
                feature.name = "Elemental MiniBokeh";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
            }

            Shader shader = Shader.Find("Hidden/MiniBokeh");
            if (shader == null)
                shader = AssetDatabase.LoadAssetAtPath<Shader>(
                    "Packages/jp.keijiro.minibokeh/Shaders/MiniBokeh.shader");
            SerializedObject featureObject = new SerializedObject(feature);
            SerializedProperty shaderProperty = featureObject.FindProperty("_shader");
            if (shaderProperty != null) shaderProperty.objectReferenceValue = shader;
            featureObject.ApplyModifiedPropertiesWithoutUndo();
            feature.Create();
            // MiniBokeh is a planar/miniature fallback. Earth Core is a layered
            // 3D arena, so the native-high owner is the project-local depth-aware
            // RenderGraph feature.
            feature.SetActive(false);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
        }

        private static void ConfigureAtmosphereRendererFeature(Material atmosphereMaterial)
        {
            UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            SerializedObject pipelineObject = new SerializedObject(pipeline);
            SerializedProperty rendererList = pipelineObject.FindProperty("m_RendererDataList");
            ScriptableRendererData rendererData = rendererList != null && rendererList.arraySize > 0
                ? rendererList.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData
                : null;
            if (rendererData == null) return;
            AtmosphereFullscreenFeature feature = null;
            for (int index = 0; index < rendererData.rendererFeatures.Count; index++)
                if (rendererData.rendererFeatures[index] is AtmosphereFullscreenFeature existing)
                    feature = existing;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<AtmosphereFullscreenFeature>();
                feature.name = "Elemental Atmosphere Fullscreen";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
            }
            feature.Configure(atmosphereMaterial);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
        }

        private static void ConfigureCinematicDepthOfFieldRendererFeature(
            Material depthOfFieldMaterial)
        {
            UniversalRenderPipelineAsset pipeline =
                GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null || depthOfFieldMaterial == null) return;
            SerializedObject pipelineObject = new SerializedObject(pipeline);
            SerializedProperty rendererList = pipelineObject.FindProperty("m_RendererDataList");
            ScriptableRendererData rendererData = rendererList != null && rendererList.arraySize > 0
                ? rendererList.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData
                : null;
            if (rendererData == null) return;

            EarthCinematicDepthOfFieldFeature feature = null;
            for (int index = 0; index < rendererData.rendererFeatures.Count; index++)
                if (rendererData.rendererFeatures[index] is EarthCinematicDepthOfFieldFeature existing)
                    feature = existing;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<EarthCinematicDepthOfFieldFeature>();
                feature.name = "Elemental Cinematic Depth Of Field";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
            }

            feature.Configure(depthOfFieldMaterial);
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
        }

        private static void CreateCelestialBackdrop(Transform cameraTransform, EarthCoreVisualStyle style)
        {
            GameObject old = GameObject.Find("Celestial Diorama Backdrop");
            if (old != null) Object.DestroyImmediate(old);
            GameObject root = new GameObject("Celestial Diorama Backdrop");
            root.transform.SetPositionAndRotation(cameraTransform.position, cameraTransform.rotation);

            Material sun = CreateOrLoadUnlitMaterial(
                "DioramaSun.mat", new Color(3.4f, 1.55f, 0.34f, 1f));
            Material planet = CreateOrLoadLitMaterial(
                "DistantPlanet.mat", new Color(0.12f, 0.16f, 0.28f, 1f), 0.25f, new Color(0.018f, 0.03f, 0.08f));
            Material moon = CreateOrLoadLitMaterial(
                "DistantMoon.mat", new Color(0.34f, 0.29f, 0.25f, 1f), 0.08f, Color.black);
            Material ring = CreateOrLoadUnlitMaterial(
                "DistantPlanetRing.mat", new Color(1.25f, 0.48f, 0.12f, 1f));

            CreatePart("Visible Sun", PrimitiveType.Sphere, root.transform,
                new Vector3(-13.5f, 7.2f, 43f), Vector3.one * 4.3f, sun);
            GameObject farPlanet = CreatePart("Ringed Ember Planet", PrimitiveType.Sphere, root.transform,
                new Vector3(16.5f, 4.5f, 55f), Vector3.one * 6.2f, planet);
            CreatePart("Distant Moon", PrimitiveType.Sphere, root.transform,
                new Vector3(9.5f, -1.8f, 39f), Vector3.one * 1.8f, moon);
            CreatePart("Tiny Moon", PrimitiveType.Sphere, root.transform,
                new Vector3(22.5f, 9.2f, 66f), Vector3.one * 1.35f, moon);

            GameObject ringObject = new GameObject("Planet Ring");
            ringObject.transform.SetParent(root.transform, false);
            ringObject.transform.localPosition = farPlanet.transform.localPosition;
            ringObject.transform.localRotation = Quaternion.Euler(68f, 12f, -18f);
            LineRenderer line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 64;
            line.widthMultiplier = 0.42f;
            line.numCornerVertices = 2;
            line.sharedMaterial = ring;
            line.startColor = new Color(1f, 0.42f, 0.12f, 0.9f);
            line.endColor = line.startColor;
            for (int index = 0; index < line.positionCount; index++)
            {
                float angle = index * (Mathf.PI * 2f / line.positionCount);
                line.SetPosition(index, new Vector3(Mathf.Cos(angle) * 8.6f, 0f, Mathf.Sin(angle) * 3.1f));
            }
        }

        private static void CreateEarthFeedback(
            MagicExecutor executor,
            MagicInputController input,
            PlanetCameraRig cameraRig,
            EarthCoreVisualStyle style,
            EarthPillarWavePool wavePool,
            Transform planetCenter)
        {
            EarthEffectsTuningProfile effectsProfile = CreateOrLoadEffectsProfile();
            GameObject old = GameObject.Find("Earth Magic Feedback");
            if (old != null) Object.DestroyImmediate(old);
            GameObject root = new GameObject("Earth Magic Feedback");
            ParticleSystem dust = CreateParticles(root.transform, "Chunky Earth Dust", style.DustColor, 0.14f, 0.68f, 1.25f, false);
            ParticleSystem sparks = CreateParticles(root.transform, "Amber Shards", style.SparkColor, 0.07f, 0.48f, 3.2f, true);
            ParticleSystem rubble = CreateParticles(root.transform, "Loose Earth Chips",
                style.StoneColor * 1.35f, 0.18f, 0.92f, 3.4f, false, "LooseEarthChipVfx.mat");
            ParticleSystem.MainModule rubbleMain = rubble.main;
            rubbleMain.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.24f);
            // Gravity is radial on the planet and is applied by the bounded adapter
            // below; Unity's global -Y particle gravity would be visibly wrong here.
            rubbleMain.gravityModifier = 0f;
            ParticleSystem.RotationOverLifetimeModule rubbleRotation = rubble.rotationOverLifetime;
            rubbleRotation.enabled = true;
            rubbleRotation.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
            rubbleRotation.y = new ParticleSystem.MinMaxCurve(-5f, 5f);
            rubbleRotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            GameObject lightObject = new GameObject("Earth Pulse Light");
            lightObject.transform.SetParent(root.transform, false);
            Light pulse = lightObject.AddComponent<Light>();
            pulse.type = LightType.Point;
            pulse.color = style.SparkColor;
            pulse.range = 7f;
            pulse.intensity = 0f;
            Material crackMaterial = CreateOrLoadUnlitMaterial(
                "EarthStrainCrack.mat", new Color(1.4f, 0.34f, 0.035f, 1f));
            LineRenderer[] cracks = new LineRenderer[7];
            for (int index = 0; index < cracks.Length; index++)
            {
                GameObject crackObject = new GameObject($"Strain Crack {index + 1:00}");
                crackObject.transform.SetParent(root.transform, false);
                LineRenderer crack = crackObject.AddComponent<LineRenderer>();
                crack.useWorldSpace = true;
                crack.widthMultiplier = 0.035f;
                crack.numCapVertices = 2;
                crack.sharedMaterial = crackMaterial;
                crack.startColor = new Color(1f, 0.30f, 0.04f, 0.92f);
                crack.endColor = new Color(0.24f, 0.055f, 0.01f, 0.1f);
                crackObject.SetActive(false);
                cracks[index] = crack;
            }
            EarthMagicFeedback feedback = root.AddComponent<EarthMagicFeedback>();
            feedback.Configure(
                executor, dust, sparks, pulse, cracks, rubble, cameraRig, input, wavePool, planetCenter);
            feedback.ConfigureEffectsProfile(effectsProfile);
            PlanetaryParticleGravity particleGravity = root.AddComponent<PlanetaryParticleGravity>();
            particleGravity.Configure(
                planetCenter,
                new[] { dust, sparks, rubble },
                new[] { 2.2f, 5.5f, 11.5f });
            EarthFeedbackProfile feedbackProfile = CreateOrLoadProfile<EarthFeedbackProfile>(
                EarthFeedbackProfilePath,
                "Earth Feedback Profile");
            feedback.ConfigureImpactProfile(feedbackProfile);
            Material scarMaterial = CreateOrLoadEarthScarDecalMaterial();
            ConfigureDecalRendererFeature();
            EarthSurfaceScarPool scarPool = root.AddComponent<EarthSurfaceScarPool>();
            scarPool.Configure(executor, feedbackProfile, scarMaterial, planetCenter);
        }

        private static Material CreateOrLoadEarthScarDecalMaterial()
        {
            const string path = "Assets/Elemental/Content/Materials/EarthSurfaceScarDecal.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Material template = AssetDatabase.LoadAssetAtPath<Material>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Decal.mat");
                if (template == null)
                    throw new UnityEditor.Build.BuildFailedException("The URP 17 decal template was not found.");
                material = new Material(template) { name = "Earth Surface Scar Decal" };
                AssetDatabase.CreateAsset(material, path);
            }
            Color scar = new Color(0.115f, 0.078f, 0.052f, 0.78f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", scar);
            if (material.HasProperty("_Color")) material.SetColor("_Color", scar);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.01f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureDecalRendererFeature()
        {
            UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            SerializedObject pipelineObject = new SerializedObject(pipeline);
            SerializedProperty rendererList = pipelineObject.FindProperty("m_RendererDataList");
            ScriptableRendererData rendererData = rendererList != null && rendererList.arraySize > 0
                ? rendererList.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData
                : null;
            if (rendererData == null) return;
            for (int index = 0; index < rendererData.rendererFeatures.Count; index++)
                if (rendererData.rendererFeatures[index] is DecalRendererFeature) return;
            DecalRendererFeature feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
            feature.name = "Elemental Earth Surface Scars";
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            rendererData.rendererFeatures.Add(feature);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
        }

        private static ParticleSystem CreateParticles(
            Transform parent, string name, Color color, float size, float lifetime, float speed, bool glow,
            string materialName = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = 180;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.42f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(color, new Color(color.r, color.g, color.b, 0f));
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.72f, 0f, 2.1f),
                new Keyframe(0.18f, 1f, 0f, 0f),
                new Keyframe(0.72f, 0.82f, -0.6f, -0.6f),
                new Keyframe(1f, 0f, -2.2f, 0f)));
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            bool softDust = !glow && string.IsNullOrEmpty(materialName);
            renderer.renderMode = softDust
                ? ParticleSystemRenderMode.Billboard
                : ParticleSystemRenderMode.Mesh;
            if (!softDust)
            {
                Mesh[] variants = LoadEarthParticleMeshVariants();
                renderer.SetMeshes(variants, variants.Length);
            }
            renderer.sharedMaterial = softDust
                ? LoadRumbleMaterial("RumbleDustLit.mat")
                : CreateOrLoadLitMaterial(
                    materialName ?? (glow ? "AmberShardVfx.mat" : "EarthDustVfx.mat"),
                    color,
                    0.04f,
                    glow ? color * 2f : Color.black);
            if (softDust && renderer.sharedMaterial == null)
                throw new UnityEditor.Build.BuildFailedException(
                    "RumbleDustLit is required for billboard dust particles.");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static ParticleSystem CreateStoneFadeDust(
            Transform parent,
            EarthEffectsTuningProfile effectsProfile)
        {
            Transform existing = parent != null ? parent.Find("Stone Fade Dust") : null;
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            GameObject go = new GameObject("Stone Fade Dust");
            go.transform.SetParent(parent, false);
            ParticleSystem dust = go.AddComponent<ParticleSystem>();
            if (effectsProfile != null)
                EarthParticleSystemTuningApplier.ApplyDust(
                    dust,
                    effectsProfile.StoneFade.Dust,
                    effectsProfile.Materials.StoneFadeDust);
            ParticleSystem.MainModule main = dust.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            if (effectsProfile == null)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.82f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.65f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.28f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.46f, 0.33f, 0.22f, 0.72f),
                    new Color(0.24f, 0.17f, 0.12f, 0.48f));
                main.maxParticles = 32;
            }
            ParticleSystem.EmissionModule emission = dust.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = effectsProfile != null ? effectsProfile.StoneFade.EmitterRadius : 0.46f;
            ParticleSystem.ColorOverLifetimeModule color = dust.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(
                Color.white,
                new Color(1f, 1f, 1f, 0f));
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = effectsProfile != null
                ? effectsProfile.Materials.StoneFadeDust
                : LoadRumbleMaterial("RumbleDustLit.mat");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return dust;
        }

        private static Mesh[] LoadEarthParticleMeshVariants()
        {
            return CreateOrLoadDebrisMeshes();
        }

        private static void CreateGravityWellFeedback(
            MagicExecutor executor,
            PlanetCameraRig cameraRig,
            EarthCoreVisualStyle style,
            Transform planetCenter)
        {
            GameObject old = GameObject.Find("Earth Gravity Well Feedback");
            if (old != null) Object.DestroyImmediate(old);
            GameObject root = new GameObject("Earth Gravity Well Feedback");
            GameObject ringRootObject = new GameObject("Gravity Focus Rings");
            ringRootObject.transform.SetParent(root.transform, false);
            Material ringMaterial = CreateOrLoadUnlitMaterial(
                "EarthGravityWell.mat", new Color(1.15f, 0.34f, 0.045f, 0.82f));
            var rings = new LineRenderer[3];
            for (int ringIndex = 0; ringIndex < rings.Length; ringIndex++)
            {
                GameObject ringObject = new GameObject($"Gravity Orbit {ringIndex + 1:00}");
                ringObject.transform.SetParent(ringRootObject.transform, false);
                LineRenderer ring = ringObject.AddComponent<LineRenderer>();
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = 48;
                ring.widthMultiplier = 0.025f + ringIndex * 0.009f;
                ring.sharedMaterial = ringMaterial;
                ring.startColor = new Color(1f, 0.28f, 0.035f, 0.78f - ringIndex * 0.12f);
                ring.endColor = ring.startColor;
                for (int point = 0; point < ring.positionCount; point++)
                {
                    float angle = point * Mathf.PI * 2f / ring.positionCount;
                    ring.SetPosition(point, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
                }
                rings[ringIndex] = ring;
            }
            ParticleSystem motes = CreateParticles(
                root.transform, "Orbiting Earth Motes", style.SparkColor,
                0.12f, 0.75f, 0f, true, "EarthGravityMote.mat");
            ParticleSystem.ShapeModule moteShape = motes.shape;
            moteShape.enabled = false;
            GameObject lightObject = new GameObject("Gravity Focus Light");
            lightObject.transform.SetParent(root.transform, false);
            Light focusLight = lightObject.AddComponent<Light>();
            focusLight.type = LightType.Point;
            focusLight.color = style.SparkColor;
            focusLight.range = 5.5f;
            focusLight.intensity = 0f;
            EarthGravityWellFeedback feedback = root.AddComponent<EarthGravityWellFeedback>();
            feedback.Configure(
                executor, ringRootObject.transform, rings, motes, focusLight, cameraRig, planetCenter);
            PlanetaryParticleGravity gravity = root.AddComponent<PlanetaryParticleGravity>();
            gravity.Configure(planetCenter, new[] { motes }, new[] { 1.4f });
        }

        private static void CreateEarthPillarFeedback(
            EarthPillarMobility mobility,
            PlanetCameraRig cameraRig,
            EarthCoreVisualStyle style)
        {
            EarthEffectsTuningProfile effectsProfile = CreateOrLoadEffectsProfile();
            GameObject old = GameObject.Find("Earth Pillar Feedback");
            if (old != null) Object.DestroyImmediate(old);
            GameObject root = new GameObject("Earth Pillar Feedback");
            Material pillarMaterial = effectsProfile.Materials.PillarChips;
            Mesh pillarMesh = CreateOrLoadBeveledPillarMesh();
            Mesh chipMesh = CreateOrLoadChippedWallMesh();
            if (!IsRumbleMaterial(pillarMaterial))
                throw new UnityEditor.Build.BuildFailedException(
                    "Earth pillar requires GraphicsV5/Materials/RumbleSandstone.mat and Rumble Rock Lit.");
            GameObject pillar = CreatePart(
                "Rising Earth Pillar", PrimitiveType.Cylinder, root.transform,
                Vector3.zero, Vector3.one, pillarMaterial);
            pillar.GetComponent<MeshFilter>().sharedMesh = pillarMesh;

            // Fixed authored chips break the cylinder silhouette without adding physics bodies.
            for (int index = 0; index < 9; index++)
            {
                float angle = index * (360f / 9f) * Mathf.Deg2Rad;
                GameObject edgeChip = CreatePart(
                    $"Pillar Edge Chip {index + 1:00}",
                    PrimitiveType.Cube,
                    pillar.transform,
                    new Vector3(Mathf.Cos(angle) * 0.88f, -0.72f + ((index % 4) * 0.46f), Mathf.Sin(angle) * 0.88f),
                    new Vector3(0.25f, 0.19f + ((index % 3) * 0.07f), 0.2f),
                    pillarMaterial,
                    new Vector3(index * 13f, index * 29f, index * 7f));
                edgeChip.GetComponent<MeshFilter>().sharedMesh = chipMesh;
            }

            var chips = new Transform[effectsProfile.Pillar.ChipPoolCount];
            for (int index = 0; index < chips.Length; index++)
            {
                GameObject chip = CreatePart(
                    $"Lift Ground Chip {index + 1:00}", PrimitiveType.Cube, root.transform,
                    Vector3.zero, Vector3.one * 0.15f, pillarMaterial);
                chip.GetComponent<MeshFilter>().sharedMesh = chipMesh;
                chips[index] = chip.transform;
            }
            EarthPillarFeedback feedback = root.AddComponent<EarthPillarFeedback>();
            feedback.Configure(mobility, pillar.transform, chips, cameraRig, effectsProfile);
        }

        private static Transform CreateLandingCushionVisual(Mesh mesh, Material material)
        {
            GameObject old = GameObject.Find("Earth Landing Cushion Preview");
            if (old != null) Object.DestroyImmediate(old);
            GameObject go = new GameObject("Earth Landing Cushion Preview");
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            go.SetActive(false);
            return go.transform;
        }

        private static void CreateHud(
            MagicInputController input,
            MagicExecutor executor,
            EarthPillarMobility pillarMobility,
            EarthLandingCushion landingCushion)
        {
            GameObject old = GameObject.Find("Earth Core HUD");
            if (old != null) Object.DestroyImmediate(old);
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudPath);
            PanelSettings panel = CreateOrLoadPanelSettings();
            if (tree == null || panel == null)
                throw new UnityEditor.Build.BuildFailedException("Earth Core HUD assets are missing.");
            GameObject go = new GameObject("Earth Core HUD");
            UIDocument document = go.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = tree;
            document.sortingOrder = 100f;
            EarthCoreHud hud = go.AddComponent<EarthCoreHud>();
            hud.Configure(input, executor, pillarMobility, landingCushion);
            BendingDebugOverlay debugOverlay = go.AddComponent<BendingDebugOverlay>();
            debugOverlay.Configure(input, executor);
        }

        private static PanelSettings CreateOrLoadPanelSettings()
        {
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(HudPanelPath);
            if (panel != null) return panel;
            PanelSettings source = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Elemental/Content/UI/ElementLabPanelSettings.asset");
            panel = Object.Instantiate(source);
            panel.name = "EarthCorePanelSettings";
            AssetDatabase.CreateAsset(panel, HudPanelPath);
            return panel;
        }

private static Volume CreatePostProcessing()
        {
            GameObject old = GameObject.Find("Earth Core Post Processing");
            if (old != null) Object.DestroyImmediate(old);
            GameObject go = new GameObject("Earth Core Post Processing");
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 910f;
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Earth Core Volume Profile";
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            profile.components.RemoveAll(component => component == null);
            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.intensity.Override(0f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.58f);
            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.intensity.Override(0.10f);
            vignette.smoothness.Override(0.52f);
            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.active = true;
            color.postExposure.Override(0f);
            color.contrast.Override(7f);
            color.saturation.Override(-8f);
            WhiteBalance whiteBalance = GetOrAdd<WhiteBalance>(profile);
            whiteBalance.active = true;
            whiteBalance.temperature.Override(2f);
            whiteBalance.tint.Override(-1f);
            DepthOfField depthOfField = GetOrAdd<DepthOfField>(profile);
            depthOfField.active = true;
            depthOfField.mode.Override(DepthOfFieldMode.Off);
            depthOfField.focusDistance.Override(8f);
            depthOfField.aperture.Override(5.6f);
            depthOfField.focalLength.Override(50f);
            depthOfField.bladeCount.Override(7);
            depthOfField.bladeCurvature.Override(0.82f);
            depthOfField.bladeRotation.Override(18f);
            depthOfField.gaussianStart.Override(8.55f);
            depthOfField.gaussianEnd.Override(12.75f);
            depthOfField.gaussianMaxRadius.Override(2f);
            depthOfField.highQualitySampling.Override(false);
            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(vignette);
            EditorUtility.SetDirty(color);
            EditorUtility.SetDirty(whiteBalance);
            EditorUtility.SetDirty(depthOfField);
            EditorUtility.SetDirty(tonemapping);
            EditorUtility.SetDirty(profile);
            volume.sharedProfile = profile;
            return volume;
        }

private static ParticleSystem CreateAmbientLightMotes(
            Transform cameraTransform,
            EarthEffectsTuningProfile effectsProfile)
        {
            GameObject old = GameObject.Find("Sunlit Air Motes");
            if (old != null) Object.DestroyImmediate(old);

            GameObject go = new GameObject("Sunlit Air Motes");
            go.transform.SetParent(cameraTransform, false);
            EarthAmbientEffectsTuning tuning = effectsProfile != null ? effectsProfile.Ambient : null;
            go.transform.localPosition = tuning != null ? tuning.LocalOffset : new Vector3(0f, 0.15f, 5.1f);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            if (tuning != null)
                EarthParticleSystemTuningApplier.Apply(
                    particles,
                    tuning.Motes,
                    effectsProfile.Materials.AmbientMotes);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            if (tuning == null)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.018f, 0.07f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.030f, 0.084f);
            }
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            if (tuning == null)
            {
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.76f, 0.40f, 0.24f),
                    new Color(1f, 0.95f, 0.78f, 0.58f));
                main.maxParticles = 64;
            }

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = tuning != null ? tuning.EmissionRate : 18f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = tuning != null ? tuning.BoxSize : new Vector3(10f, 5.5f, 9f);
            shape.randomDirectionAmount = 1f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            Vector2 horizontal = tuning != null ? tuning.HorizontalVelocity : new Vector2(-0.022f, 0.022f);
            Vector2 vertical = tuning != null ? tuning.VerticalVelocity : new Vector2(0.018f, 0.06f);
            velocity.x = new ParticleSystem.MinMaxCurve(horizontal.x, horizontal.y);
            velocity.y = new ParticleSystem.MinMaxCurve(vertical.x, vertical.y);
            velocity.z = new ParticleSystem.MinMaxCurve(horizontal.x, horizontal.y);

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.84f, 0.60f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.92f, 0.14f),
                    new GradientAlphaKey(0.72f, 0.74f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = alpha;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.sharedMaterial = effectsProfile != null
                ? effectsProfile.Materials.AmbientMotes
                : CreateOrLoadShaderMaterial("LightDustMote.mat", "Elemental/Light Dust Mote");
            renderer.sortingFudge = 0.4f;

            particles.Play();
            return particles;
        }

private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>();
            if (AssetDatabase.Contains(profile) && !AssetDatabase.Contains(component))
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            return component;
        }

        private static Material CreateOrLoadLitMaterial(string fileName, Color color, float smoothness, Color emission)
        {
            const string folder = "Assets/Elemental/Content/Materials/";
            string path = folder + fileName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new UnityEditor.Build.BuildFailedException("URP Lit shader was not found.");
            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(fileName) };
                AssetDatabase.CreateAsset(material, path);
            }
            else MaterialShaderStateUtility.RebindShader(material, shader);
            material.color = color;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetColor("_EmissionColor", emission);
            if (emission.maxColorComponent > 0f) material.EnableKeyword("_EMISSION");
            else material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrLoadUnlitMaterial(string fileName, Color color)
        {
            const string folder = "Assets/Elemental/Content/Materials/";
            string path = folder + fileName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new UnityEditor.Build.BuildFailedException("URP Unlit shader was not found.");
            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(fileName) };
                AssetDatabase.CreateAsset(material, path);
            }
            else MaterialShaderStateUtility.RebindShader(material, shader);
            material.color = color;
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePart(
            string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, Vector3 euler = default)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return go;
        }

        private static Material LoadRumbleMaterial(string fileName) =>
            AssetDatabase.LoadAssetAtPath<Material>(RumbleMaterialFolder + fileName);

        private static Mesh LoadRumbleMesh(string fileName) =>
            AssetDatabase.LoadAssetAtPath<Mesh>(RumbleRockFolder + fileName);

        private static bool IsRumbleMaterial(Material material) =>
            material != null && material.shader != null && material.shader.name == RumbleShaderName;

        private static GameObject CreateRumbleRock(
            string name,
            Mesh mesh,
            Material material,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            bool createCollider,
            int variationSeed,
            Vector3 planetCenter,
            GravityWorldBehaviour gravityWorld,
            EarthRockDebrisPool debrisPool)
        {
            if (mesh == null || material == null)
                throw new UnityEditor.Build.BuildFailedException(
                    $"Graphics V5 asset missing while creating '{name}'.");

            GameObject rock = new GameObject(name);
            rock.transform.SetParent(parent, false);
            rock.transform.rotation = rotation;
            rock.transform.localScale = scale;
            rock.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = rock.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            if (createCollider)
            {
                BoxCollider collider = rock.AddComponent<BoxCollider>();
                collider.center = mesh.bounds.center;
                collider.size = Vector3.Max(mesh.bounds.size * 0.90f, Vector3.one * 0.08f);
                Rigidbody body = rock.AddComponent<Rigidbody>();
                Vector3 scaledSize = Vector3.Scale(mesh.bounds.size, new Vector3(
                    Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                float volume = Mathf.Max(0.05f, scaledSize.x * scaledSize.y * scaledSize.z * 0.62f);
                body.mass = Mathf.Clamp(volume * 120f, 45f, 1500f);
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.constraints = RigidbodyConstraints.FreezeAll;
                GravityBody gravityBody = rock.AddComponent<GravityBody>();
                gravityBody.Configure(gravityWorld, body);
                gravityBody.enabled = false;
                EarthDestructibleDecorRock destructible =
                    rock.AddComponent<EarthDestructibleDecorRock>();
                destructible.Configure(
                    0xD3000000u + unchecked((uint)Mathf.Max(1, variationSeed + 1)),
                    body,
                    collider,
                    gravityBody,
                    debrisPool,
                    scaledSize.magnitude * 0.28f,
                    Mathf.Clamp(body.mass * 5.5f, 420f, 2400f));
            }

            Vector3 surfaceNormal = (position - planetCenter).normalized;
            if (surfaceNormal.sqrMagnitude < 0.5f) surfaceNormal = rotation * Vector3.up;
            EarthSurfacePlacementResult placement = EarthSurfacePlacementSolver.Solve(
                mesh,
                position,
                surfaceNormal,
                rotation,
                scale,
                0.035f);
            rock.transform.position = placement.IsValid ? placement.RootPosition : position;

            float variant = Mathf.Repeat(variationSeed * 0.071f, 1f);
            Color baseColor = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : new Color(0.50f, 0.34f, 0.23f, 1f);
            baseColor = Color.Lerp(baseColor * 0.93f, baseColor * 1.05f, variant);
            baseColor.a = 1f;
            Color shadow = material.HasProperty("_ShadowColor")
                ? material.GetColor("_ShadowColor")
                : baseColor * 0.38f;
            Color edge = material.HasProperty("_EdgeColor")
                ? material.GetColor("_EdgeColor")
                : Color.Lerp(baseColor, Color.white, 0.18f);
            RumbleRockVariation variation = rock.AddComponent<RumbleRockVariation>();
            variation.Configure(
                baseColor,
                shadow,
                edge,
                Mathf.Lerp(2.8f, 5.2f, variant),
                Mathf.Lerp(0.05f, 0.10f, Mathf.Repeat(variationSeed * 0.173f, 1f)),
                Mathf.Lerp(0.18f, 0.30f, Mathf.Repeat(variationSeed * 0.217f, 1f)),
                true,
                planetCenter);
            return rock;
        }

        private static AbilityRecipeAsset[] CreateOrLoadRecipes()
        {
            return new[]
            {
                CreateOrLoadRecipe(
                    "LineWall.asset",
                    EarthAbilityIds.LineWall,
                    MagicSelectorKind.PlanetSurface,
                    MagicGeometryKind.WallSpline,
                    new[] { MagicOperatorKind.AddSolid },
                    0.45f,
                    1f),
                CreateOrLoadRecipe(
                    "RaisePlatform.asset",
                    EarthAbilityIds.RaisePlatform,
                    MagicSelectorKind.PlanetSurface,
                    MagicGeometryKind.WallSpline,
                    new[] { MagicOperatorKind.AddSolid },
                    0.45f,
                    1f),
                CreateOrLoadRecipe(
                    "PullRock.asset",
                    EarthAbilityIds.PullRock,
                    MagicSelectorKind.PlanetSurface,
                    MagicGeometryKind.AnchorSphere,
                    new[] { MagicOperatorKind.SubtractSolid, MagicOperatorKind.SpawnFragment },
                    0.75f,
                    1f),
                CreateOrLoadRecipe(
                    "FlickThrow.asset",
                    EarthAbilityIds.FlickThrow,
                    MagicSelectorKind.HeldFragment,
                    MagicGeometryKind.Direction,
                    new[] { MagicOperatorKind.ApplyImpulse },
                    0.25f,
                    12f)
            };
        }

        private static AbilityRecipeAsset CreateOrLoadRecipe(
            string fileName,
            AbilityId id,
            MagicSelectorKind selector,
            MagicGeometryKind geometry,
            MagicOperatorKind[] operators,
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

            asset.Configure(id, selector, geometry, operators, radius, strength);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Material CreateOrLoadPreviewMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(PreviewMaterialPath);
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
                name = "Magic Preview",
                color = new Color(0.32f, 0.95f, 0.72f, 0.85f)
            };
            AssetDatabase.CreateAsset(material, PreviewMaterialPath);
            return material;
        }

        private static void CreateMvpLinebreaker(
            GravityWorldBehaviour gravityWorld,
            float planetRadius,
            GameObject player,
            Transform planetCenter,
            EarthFragmentPool projectilePool)
        {
            string[] obsoleteNames =
            {
                "Earth Impact Dummy",
                "Earth Combat Scout",
                "Earth Combat Sentinel",
                "Earth Combat Trap",
                "Rumble Linebreaker Bot"
            };
            for (int index = 0; index < obsoleteNames.Length; index++)
            {
                GameObject obsolete = GameObject.Find(obsoleteNames[index]);
                if (obsolete != null) Object.DestroyImmediate(obsolete);
            }

            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            Vector3 arenaCenter = center + Vector3.up * planetRadius;
            // 5.25 m intersects the authored gate after Broken Crown integration.
            // 3.50 m is inside the clear court and is still finalized by the same
            // collision-seating pass as the player.
            Vector3 surface = ProjectTangentPointToPlanet(center, planetRadius, new Vector3(0f, 0f, 3.50f));
            Vector3 up = (surface - center).normalized;
            Vector3 facing = Vector3.ProjectOnPlane(player.transform.position - surface, up).normalized;
            if (facing.sqrMagnitude < 0.1f) facing = Vector3.back;

            GameObject bot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bot.name = "Rumble Linebreaker Bot";
            bot.SetActive(false);
            GameObject enemyFocusProxy = new GameObject("EnemyFocusProxy");
            enemyFocusProxy.transform.SetParent(bot.transform, false);
            enemyFocusProxy.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            // Half of the 2.15 m capsule: exact first-frame surface contact.
            bot.transform.position = surface + up * 1.075f;
            bot.transform.rotation = Quaternion.LookRotation(facing, up);
            MeshRenderer capsuleRenderer = bot.GetComponent<MeshRenderer>();
            if (capsuleRenderer != null) Object.DestroyImmediate(capsuleRenderer);
            MeshFilter capsuleFilter = bot.GetComponent<MeshFilter>();
            if (capsuleFilter != null) Object.DestroyImmediate(capsuleFilter);
            CapsuleCollider capsule = bot.GetComponent<CapsuleCollider>();
            capsule.height = 2.15f;
            capsule.radius = 0.56f;

            Rigidbody body = bot.AddComponent<Rigidbody>();
            body.mass = 42f;
            body.useGravity = false;
            body.linearDamping = 0.22f;
            body.angularDamping = 0.72f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            GravityBody gravityBody = bot.AddComponent<GravityBody>();
            gravityBody.Configure(gravityWorld, body);
            EarthCombatDummy combat = bot.AddComponent<EarthCombatDummy>();
            combat.Configure(EarthCombatArchetype.Scout, 135f, 620f);
            PlanetMotor motor = bot.AddComponent<PlanetMotor>();
            EarthMvpBotController controller = bot.AddComponent<EarthMvpBotController>();
            motor.Configure(gravityWorld, body, capsule, controller, bot.transform);
            // The authored crater floor is a detailed non-convex mesh. Persist a
            // wider solver skin on the motor so it is restored after scene load.
            motor.ConfigureGroundContactSkin(0.045f);
            motor.ConfigureFeel(3.1f, 18f, 0.18f);
            motor.ConfigureTankSteering(true, 245f);
            motor.ConfigureOrientationFeel(62f, 13f, 150f);
            controller.Configure(
                player.transform,
                player.GetComponent<Rigidbody>(),
                player.GetComponent<PhysicalImpactTarget>(),
                player.GetComponent<ActiveRagdollPuppet>(),
                planetCenter,
                body,
                motor,
                combat,
                arenaCenter,
                6.5f);
            controller.ConfigureTuning(5.8f, 0.82f, 15f, 0.24f, 0.72f, 1.0f);

            Animator humanoidAnimator = CreateLinebreakerHumanoidVisual(bot.transform);
            Transform botVisualRoot = bot.transform.Find("Linebreaker X Bot Presentation");
            if (botVisualRoot == null)
                throw new UnityEditor.Build.BuildFailedException(
                    "Linebreaker visible Humanoid root was not created.");
            EarthEffectsTuningProfile botEffectsProfile = CreateOrLoadEffectsProfile();
            ParticleSystem botFadeDust = CreateStoneFadeDust(botVisualRoot, botEffectsProfile);
            HumanoidRagdollRig botVisibleRagdoll = botVisualRoot.gameObject.AddComponent<HumanoidRagdollRig>();
            botVisibleRagdoll.ConfigureAndBuild(
                humanoidAnimator,
                body,
                capsule,
                gravityWorld,
                null,
                botFadeDust);
            botVisibleRagdoll.ConfigureEffectsProfile(botEffectsProfile);
            CharacterPresentationProfile characterProfile = CreateOrLoadProfile<CharacterPresentationProfile>(
                CharacterProfilePath,
                "Character Presentation Profile");
            HumanoidCharacterPresentation botSharedPresentation =
                botVisualRoot.gameObject.AddComponent<HumanoidCharacterPresentation>();
            botSharedPresentation.Configure(
                characterProfile,
                humanoidAnimator,
                null,
                null,
                motor,
                body,
                null,
                null,
                null,
                null,
                null,
                botVisibleRagdoll,
                false);
            HumanoidOrganicIdle botOrganicIdle = botVisualRoot.gameObject.AddComponent<HumanoidOrganicIdle>();
            botOrganicIdle.Configure(
                humanoidAnimator,
                botSharedPresentation,
                motor,
                botVisibleRagdoll,
                characterProfile.OrganicIdleBlendInSeconds,
                characterProfile.OrganicIdleBlendOutSeconds);
            ConfigureEammBasePose(bot, botVisualRoot.gameObject, humanoidAnimator, botSharedPresentation, botVisibleRagdoll, false);
            HumanoidRagdollRig playerVisibleRagdoll =
                player.GetComponentInChildren<HumanoidRagdollRig>(true);
            Rigidbody playerBody = player.GetComponent<Rigidbody>();
            PhysicalImpactTarget playerPhysicalImpact = player.GetComponent<PhysicalImpactTarget>();
            EarthCharacterImpactTarget playerCharacterImpact =
                player.GetComponent<EarthCharacterImpactTarget>();
            if (playerCharacterImpact == null)
                playerCharacterImpact = player.AddComponent<EarthCharacterImpactTarget>();
            CharacterImpactResponseProfile impactResponseProfile =
                CreateOrLoadProfile<CharacterImpactResponseProfile>(
                    CharacterImpactProfilePath,
                    "Character Impact Response Profile");
            impactResponseProfile.ConfigureMode(ImpactResponseMode.Calibrated);
            EditorUtility.SetDirty(impactResponseProfile);
            playerCharacterImpact.Configure(
                EarthDuelFighterId.Player,
                0xC0010001u,
                playerBody,
                null,
                impactResponseProfile);
            playerPhysicalImpact?.ConfigureCharacterImpactTarget(playerCharacterImpact);

            EarthCharacterImpactTarget botCharacterImpact =
                bot.AddComponent<EarthCharacterImpactTarget>();
            botCharacterImpact.Configure(
                EarthDuelFighterId.Bot,
                0xC0010002u,
                body,
                null,
                impactResponseProfile);
            combat.SetCharacterImpactAuthority(botCharacterImpact);
            EarthMvpDuelController duel = bot.AddComponent<EarthMvpDuelController>();
            duel.Configure(
                player.GetComponent<ActiveRagdollPuppet>(),
                playerBody,
                playerPhysicalImpact,
                controller,
                combat,
                motor,
                body,
                capsule,
                humanoidAnimator,
                playerVisibleRagdoll,
                botVisibleRagdoll,
                playerCharacterImpact,
                botCharacterImpact,
                3.5f);
            controller.ConfigureMagic(projectilePool, capsule, duel);

            LineRenderer strikeLine = bot.AddComponent<LineRenderer>();
            strikeLine.useWorldSpace = true;
            strikeLine.positionCount = 0;
            strikeLine.numCapVertices = 3;
            strikeLine.numCornerVertices = 2;
            strikeLine.alignment = LineAlignment.View;
            strikeLine.sharedMaterial = CreateOrLoadPreviewMaterial();
            strikeLine.shadowCastingMode = ShadowCastingMode.Off;
            strikeLine.receiveShadows = false;
            Renderer[] combatRenderers = humanoidAnimator.GetComponentsInChildren<Renderer>(true);
            EarthMvpBotPresenter presenter = bot.AddComponent<EarthMvpBotPresenter>();
            presenter.Configure(
                controller,
                strikeLine,
                combatRenderers,
                humanoidAnimator,
                motor,
                body,
                botSharedPresentation);
            bot.SetActive(true);
        }

        private static void RestoreApprovedLinebreakerSpawn(
            Vector3 planetCenter,
            float planetRadius)
        {
            GameObject bot = GameObject.Find("Rumble Linebreaker Bot");
            if (bot == null)
                throw new UnityEditor.Build.BuildFailedException(
                    "Rumble Linebreaker Bot was lost before authored spawn restoration.");

            // Captured from the user-approved saved EarthCoreSlice scene. Keeping
            // this final pose after arena collision integration prevents the bot
            // from being half-buried or silently shifted back toward the gate on
            // the next generated-scene rebuild.
            bot.transform.SetPositionAndRotation(
                planetCenter + new Vector3(
                    -0.26751554f,
                    planetRadius + 2.9f,
                    3.5498571f),
                new Quaternion(0f, 0.999495f, 0.031776477f, 0f));
            EditorUtility.SetDirty(bot.transform);
        }

        private static void ConfigureEammBasePose(
            GameObject gameplayRoot,
            GameObject visibleRoot,
            Animator animator,
            HumanoidCharacterPresentation presentation,
            HumanoidRagdollRig ragdoll,
            bool player)
        {
            MotionMatchingData data = EnsureDefaultEammDatabase();
            EnvironmentMotionMatchingSearch search =
                AssetDatabase.LoadAssetAtPath<EnvironmentMotionMatchingSearch>(EammSearchPath);
            if (data == null || search == null)
            {
                Debug.LogWarning("[EAMM] Database/search unavailable; character remains on safe Legacy locomotion.");
                return;
            }

            EAMMRuntimeProfile runtimeProfile = CreateOrLoadProfile<EAMMRuntimeProfile>(
                EammRuntimeProfilePath,
                "EAMM Runtime Profile");
            EarthRetargetBindPose bindPose = EnsureDefaultEammBindPose();
            // Early EAMM scenes serialized SurfaceMotionResolver while the type
            // lived in SurfaceMotionProfile.cs. Unity cannot persist a component
            // whose MonoScript does not match its file, leaving one missing
            // Behaviour on each character. Remove only those invalid entries on
            // the two integration roots before attaching the stable component.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameplayRoot);
            // A domain reload can resolve the old class name even though its
            // serialized m_Script pointer is still the invalid file-local id.
            // Recreate this stateless resolver so the scene records the new
            // dedicated MonoScript GUID. This touches no transform or authoring.
            SurfaceMotionResolver existingResolver = gameplayRoot.GetComponent<SurfaceMotionResolver>();
            if (existingResolver != null) Object.DestroyImmediate(existingResolver);
            gameplayRoot.AddComponent<SurfaceMotionResolver>();
            Transform previous = gameplayRoot.transform.Find("EAMM Hidden Driver");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            GameObject hidden = new GameObject("EAMM Hidden Driver");
            hidden.transform.SetParent(gameplayRoot.transform, false);
            hidden.hideFlags = HideFlags.HideInHierarchy;
            MotionMatchingController controller = hidden.AddComponent<MotionMatchingController>();
            PlanetEAMMCharacterController adapter = hidden.AddComponent<PlanetEAMMCharacterController>();
            adapter.MotionMatching = controller;
            adapter.Configure(gameplayRoot.GetComponent<PlanetMotor>(), runtimeProfile);
            controller.CharacterController = adapter;
            controller.MMData = data;
            controller.Search = search;
            controller.SearchTime = player
                ? runtimeProfile.PlayerSearchSeconds
                : runtimeProfile.BotSearchSeconds;
            controller.LockFPS = false;
            controller.FootLock = false;
            controller.Inertialize = true;
            controller.DebugSkeleton = false;
            controller.DebugFutureSkeleton = false;
            controller.DebugCurrent = false;
            controller.DebugPose = false;
            controller.DebugTrajectory = false;
            controller.DebugEnvironment = false;
            controller.DebugSearch = false;
            controller.DebugContacts = false;
            controller.DebugGUI = false;

            EAMMBasePoseBridge bridge = visibleRoot.GetComponent<EAMMBasePoseBridge>();
            if (bridge == null) bridge = visibleRoot.AddComponent<EAMMBasePoseBridge>();
            bridge.Configure(controller, runtimeProfile, bindPose, player);
            EditorUtility.SetDirty(gameplayRoot);
            EditorUtility.SetDirty(visibleRoot);
        }

        private static MotionMatchingData EnsureDefaultEammDatabase()
        {
            MotionMatchingData existing = AssetDatabase.LoadAssetAtPath<MotionMatchingData>(EammDataPath);
            if (existing != null) return existing;
            EnsureAssetFolder("Assets/Elemental/Content/Characters/MotionMatching");
            MotionLibraryAsset library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(EammLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<MotionLibraryAsset>();
                library.name = "EarthMotionLibraryData";
                library.sourceRig = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
                library.databaseRate = 30f;
                AddDefaultMotionClip(library, MixamoIdlePath, MotionClipRole.Idle, 0f, true);
                AddDefaultMotionClip(library, MixamoWalkPath, MotionClipRole.Locomotion, 2.4f, true);
                AddDefaultMotionClip(library, MixamoWalkBackPath, MotionClipRole.Locomotion, 1.8f, true);
                AddDefaultMotionClip(library, MixamoTurnPath, MotionClipRole.Pivot, 0f, false);
                AssetDatabase.CreateAsset(library, EammLibraryPath);
            }
            if (library.sourceRig == null || library.clips.Count == 0) return null;
            return MotionLibraryBuilder.Bake(library);
        }

        private static EarthRetargetBindPose EnsureDefaultEammBindPose()
        {
            EarthRetargetBindPose existing =
                AssetDatabase.LoadAssetAtPath<EarthRetargetBindPose>(
                    MotionLibraryBuilder.RetargetBindPosePath);
            if (existing != null) return existing;
            MotionLibraryAsset library =
                AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(EammLibraryPath);
            if (library == null || library.sourceRig == null) return null;
            return MotionLibraryBuilder.BakeRetargetBindPose(library);
        }

        private static void AddDefaultMotionClip(
            MotionLibraryAsset library,
            string assetPath,
            MotionClipRole role,
            float speed,
            bool loop)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is not AnimationClip clip || clip.name.StartsWith("__preview__")) continue;
                library.clips.Add(new MotionClipRecipe
                {
                    clip = clip,
                    role = role,
                    nominalSpeed = speed,
                    nominalYaw = role == MotionClipRole.Pivot ? -90f : 0f,
                    loop = loop
                });
                return;
            }
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

        private static Animator CreateLinebreakerHumanoidVisual(Transform bot)
        {
            ConfigureCharacterImporters();
            CharacterPresentationProfile profile = CreateOrLoadProfile<CharacterPresentationProfile>(
                CharacterProfilePath,
                "Character Presentation Profile");
            GameObject prefab = profile.HumanoidPrefab;
            Avatar avatar = profile.Avatar;
            RuntimeAnimatorController controller = profile.AnimatorController;
            if (prefab == null || avatar == null || !avatar.isValid || !avatar.isHuman || controller == null)
                throw new UnityEditor.Build.BuildFailedException(
                    "Linebreaker requires the same valid Humanoid prefab, Avatar and controller as the player.");

            GameObject visual = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (visual == null)
                throw new UnityEditor.Build.BuildFailedException(
                    "Linebreaker Humanoid presentation could not be instantiated.");
            // See the player path above: both generated characters must retain
            // their scene-authored controller, secondary rig and Rumble materials
            // across imports and editor domain reloads.
            PrefabUtility.UnpackPrefabInstance(
                visual,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            visual.name = "Linebreaker X Bot Presentation";
            visual.tag = "Untagged";
            visual.transform.SetParent(bot, false);
            visual.transform.localPosition = profile.LocalPosition;
            visual.transform.localRotation = profile.LocalRotation;
            visual.transform.localScale = profile.LocalScale;

            Animator animator = visual.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            ConfigureSecondaryCharacterMotion(visual, animator);

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            ApplyPersistentRumbleCharacterMaterials(visual, true);
            return animator;
        }

        private static void ConfigureSecondaryCharacterMotion(GameObject visual, Animator animator)
        {
            HumanoidSecondaryMotion secondaryMotion = visual.GetComponent<HumanoidSecondaryMotion>();
            if (secondaryMotion == null) secondaryMotion = visual.AddComponent<HumanoidSecondaryMotion>();
            secondaryMotion.ConfigureFromHierarchy(animator);
            if (!string.IsNullOrEmpty(secondaryMotion.ConfigurationDiagnostic))
                throw new UnityEditor.Build.BuildFailedException(
                    secondaryMotion.ConfigurationDiagnostic);
        }

        private static void ApplyPersistentRumbleCharacterMaterials(GameObject visual, bool rivalCharacter)
        {
            if (visual == null) return;
            string folder = rivalCharacter ? RivalMaterialFolder : PlayerMaterialFolder;
            EnsureFolder(folder);
            Shader rumbleShader = Shader.Find(RumbleShaderName);
            if (rumbleShader == null)
                throw new UnityEditor.Build.BuildFailedException($"Missing required character shader '{RumbleShaderName}'.");
            Color characterTint = rivalCharacter
                ? new Color(0.055f, 0.30f, 0.88f, 1f)
                : new Color(0.52f, 0.285f, 0.16f, 1f);
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material source = materials[materialIndex];
                    if (source == null || source.shader == null) continue;
                    string sourcePath = AssetDatabase.GetAssetPath(source);
                    string guid = AssetDatabase.AssetPathToGUID(sourcePath);
                    string token = !string.IsNullOrEmpty(guid) && guid.Length >= 8
                        ? guid.Substring(0, 8)
                        : materialIndex.ToString("00");
                    string safeName = SanitizeAssetFileName(source.name);
                    string path = $"{folder}/{safeName}_{token}.mat";
                    Material characterMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (characterMaterial == null)
                    {
                        characterMaterial = new Material(rumbleShader) { name = $"{safeName}_{token}" };
                        AssetDatabase.CreateAsset(characterMaterial, path);
                    }
                    else MaterialShaderStateUtility.RebindShader(characterMaterial, rumbleShader);
                    Color resting = source.HasProperty("_BaseColor")
                        ? source.GetColor("_BaseColor")
                        : source.HasProperty("_Color")
                            ? source.GetColor("_Color")
                            : Color.white;
                    float authoredValue = Mathf.Clamp(resting.grayscale, 0.42f, 0.92f);
                    Color baseColor = characterTint * Mathf.Lerp(0.82f, 1.13f, authoredValue);
                    baseColor.a = 1f;
                    characterMaterial.SetFloat("_SurfaceMode", 1f);
                    characterMaterial.SetColor("_BaseColor", baseColor);
                    characterMaterial.SetColor("_ShadowColor", Color.Lerp(baseColor, Color.black, 0.62f));
                    characterMaterial.SetColor("_EdgeColor", Color.Lerp(baseColor, Color.white, 0.18f));
                    characterMaterial.SetColor("_FractureColor", Color.Lerp(baseColor, new Color(0.78f, 0.64f, 0.52f), 0.24f));
                    characterMaterial.SetFloat("_TextureScale", 0.22f);
                    // In Character mode this is an authored-UV colour reveal:
                    // enough of the mapped texture survives to read clearly while
                    // the shared Rumble palette still ties it to the environment.
                    characterMaterial.SetFloat("_TextureStrength", 0.62f);
                    characterMaterial.SetFloat("_MacroScale", 3.8f);
                    characterMaterial.SetFloat("_MacroStrength", 0.04f);
                    characterMaterial.SetFloat("_FacetContrast", 0.20f);
                    characterMaterial.SetFloat("_Roughness", 0.86f);
                    characterMaterial.SetFloat("_BevelLight", 0f);
                    characterMaterial.SetFloat("_SideShadingSmoothness", 0f);
                    characterMaterial.SetFloat("_AmbientStrength", 0.86f);
                    characterMaterial.SetFloat("_UsePlanetFrame", 0f);
                    characterMaterial.SetFloat("_Fade", 1f);
                    Texture sourceTexture = source.HasProperty("_BaseMap")
                        ? source.GetTexture("_BaseMap")
                        : source.HasProperty("_MainTex")
                            ? source.GetTexture("_MainTex")
                            : null;
                    if (sourceTexture != null) characterMaterial.SetTexture("_BaseMap", sourceTexture);
                    characterMaterial.enableInstancing = true;
                    EditorUtility.SetDirty(characterMaterial);
                    materials[materialIndex] = characterMaterial;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private static string SanitizeAssetFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "RivalStone";
            char[] chars = value.ToCharArray();
            for (int index = 0; index < chars.Length; index++)
                if (!char.IsLetterOrDigit(chars[index]) && chars[index] != '-' && chars[index] != '_')
                    chars[index] = '_';
            return new string(chars);
        }

        private static void CreatePushBoulders(
            GravityWorldBehaviour gravityWorld,
            Material material,
            float planetRadius,
            EarthPhysicsFeelProfile physicsFeel,
            Mesh[] physicsRockMeshes)
        {
            if (physicsRockMeshes == null || physicsRockMeshes.Length == 0)
                throw new UnityEditor.Build.BuildFailedException(
                    "Push boulders require the centered Graphics V5 physics library.");
            GameObject existing = GameObject.Find("Magic Push Boulders");
            if (existing != null) Object.DestroyImmediate(existing);
            GameObject root = new GameObject("Magic Push Boulders");
            CreatePushBoulder(root.transform, "Light Push Boulder", new Vector3(-3.8f, planetRadius + 0.15f, 3.7f),
                0.72f, 55f, gravityWorld, material, physicsRockMeshes[0]);
            CreatePushBoulder(root.transform, "Heavy Push Boulder", new Vector3(4.2f, planetRadius + 0.35f, 4.1f),
                1.05f, 320f, gravityWorld, material,
                physicsRockMeshes[Mathf.Min(3, physicsRockMeshes.Length - 1)]);
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>())
                physicsFeel?.Apply(
                    body,
                    body.GetComponent<Collider>(),
                    body.mass >= 200f ? EarthPhysicsBodyClass.HeavyBlock : EarthPhysicsBodyClass.LightStone);
        }

        private static void CreatePushBoulder(
            Transform parent,
            string name,
            Vector3 position,
            float radius,
            float mass,
            GravityWorldBehaviour gravityWorld,
            Material material,
            Mesh mesh)
        {
            GameObject boulder = new GameObject(name);
            boulder.transform.SetParent(parent, false);
            boulder.transform.position = position;
            boulder.transform.localScale = Vector3.one * (radius * 2f);
            boulder.transform.rotation = Quaternion.Euler(17f, mass * 0.19f, -11f);
            MeshFilter filter = boulder.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = boulder.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            MeshCollider collider = boulder.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            Rigidbody body = boulder.AddComponent<Rigidbody>();
            body.mass = mass;
            body.useGravity = false;
            body.linearDamping = 0.16f;
            body.angularDamping = 0.28f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            GravityBody gravityBody = boulder.AddComponent<GravityBody>();
            gravityBody.Configure(gravityWorld, body);
            PhysicalImpactTarget target = boulder.AddComponent<PhysicalImpactTarget>();
            target.Configure(body, 0.5f);
        }

        private readonly struct EarthPuppetPart
        {
            public EarthPuppetPart(
                Transform transform,
                Rigidbody body,
                Collider collider,
                ActiveRagdollJoint joint)
            {
                Transform = transform;
                Body = body;
                Collider = collider;
                Joint = joint;
            }

            public Transform Transform { get; }
            public Rigidbody Body { get; }
            public Collider Collider { get; }
            public ActiveRagdollJoint Joint { get; }
        }
    }
}
