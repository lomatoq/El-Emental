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
using Elemental.Runtime.Physics;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Matter;
using Elemental.Runtime.World;
using Elemental.Runtime.Geometry;
using Elemental.Simulation.Magic;
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
        private const string PhysicsFeelProfilePath = "Assets/Elemental/Content/Profiles/EarthPhysicsFeelProfile.asset";
        private const string QuickCastProfilePath = "Assets/Elemental/Content/Profiles/EarthQuickCastProfile.asset";
        private const string ArmorProfilePath = "Assets/Elemental/Content/Profiles/EarthArmorProfile.asset";
        private const string ArmorShellPath = "Assets/Elemental/Content/Profiles/EarthArmorShellDefinition.asset";
        private const string ResonanceProfilePath = "Assets/Elemental/Content/Profiles/EarthResonanceProfile.asset";
        private const string SurfProfilePath = "Assets/Elemental/Content/Profiles/EarthSurfProfile.asset";
        private const string StructureFractureProfilePath = "Assets/Elemental/Content/Profiles/EarthStructureFractureProfile.asset";
        private const string EarthMaterialProfilePath = "Assets/Elemental/Content/Profiles/EarthMaterialProfile.asset";
        private const string EarthFeedbackProfilePath = "Assets/Elemental/Content/Profiles/EarthFeedbackProfile.asset";
        private const string GestureProfilePath = "Assets/Elemental/Content/Profiles/EarthGestureProfile.asset";
        private const string TechniquePresentationProfilePath =
            "Assets/Elemental/Content/Profiles/EarthTechniquePresentationProfile.asset";
        private const string MotorFeelProfilePath = "Assets/Elemental/Content/Profiles/PlanetMotorFeelProfile.asset";
        private const string EarthCameraProfilePath = "Assets/Elemental/Content/Profiles/EarthCameraProfile.asset";
        private const string ShapeGrammarProfilePath = "Assets/Elemental/Content/Profiles/EarthShapeGrammarProfile.asset";
        private const string EarthStoneAlbedoPath = "Assets/Elemental/Content/Textures/EarthStoneAlbedo.png";
        private const string CharacterModelPath = "Assets/ThirdParty/Mixamo/X Bot.fbx";
        private const string MixamoWalkPath = "Assets/ThirdParty/Mixamo/X Bot@Walking.fbx";
        private const string MixamoWalkBackPath = "Assets/ThirdParty/Mixamo/X Bot@Walking Backwards.fbx";
        private const string MixamoPunchPath = "Assets/ThirdParty/Mixamo/X Bot@Punching.fbx";
        private const string MageControllerPath = "Assets/Elemental/Content/Animation/KayKitMage.controller";
        private const string MageMaskPath = "Assets/Elemental/Content/Animation/KayKitMageUpperBody.mask";

        [MenuItem("Elemental/Setup/Create M3 Earth Core Slice")]
        public static void Configure()
        {
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

            Material earthMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Elemental/Content/Materials/VoxelPlanetSurface.mat");
            EarthCoreVisualStyle style = CreateOrLoadVisualStyle();
            EarthMaterialProfile earthMaterialProfile = CreateOrLoadProfile<EarthMaterialProfile>(
                EarthMaterialProfilePath,
                "Earth Material Profile");
            ApplyEarthMaterial(earthMaterial, style, earthMaterialProfile, false);
            earthMaterial.SetFloat("_UsePlanetFrame", 1f);
            Material looseEarthMaterial = CreateOrLoadEarthMaterial(
                "EarthLooseStone.mat", style.StoneColor, style.StoneSmoothness, style.StoneEmission * 0.35f);
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
            Mesh fragmentMesh = fragmentMeshes[0];
            EarthRockProfile rockProfile = CreateOrLoadRockProfile();
            EarthPhysicsFeelProfile physicsFeel = CreateOrLoadProfile<EarthPhysicsFeelProfile>(
                PhysicsFeelProfilePath,
                "Earth Physics Feel Profile");
            EarthRockDebrisPool debrisPool = magicRoot.AddComponent<EarthRockDebrisPool>();
            debrisPool.Configure(72, looseEarthMaterial, fragmentMesh, gravityWorld, rockProfile);
            debrisPool.ConfigureMeshVariants(fragmentMeshes);
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
            Material wallMaterial = CreateOrLoadEarthMaterial(
                "EarthWall.mat", style.StoneColor * 0.95f, 0.05f, style.StoneEmission * 0.5f);
            earthMaterialProfile.Apply(wallMaterial, false);
            Material fractureInteriorMaterial = CreateOrLoadEarthMaterial(
                "EarthFractureInterior.mat", earthMaterialProfile.FreshInteriorTint, 0.025f, Color.black);
            earthMaterialProfile.Apply(fractureInteriorMaterial, true);
            EarthWallPool wallPool = magicRoot.AddComponent<EarthWallPool>();
            wallPool.Configure(8, wallMesh, wallMaterial, CreateOrLoadWallProfile());
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
            platformPool.ConfigureFractureProfile(structureFracture);
            platformPool.ConfigureSurfaceQueries(surfaceQueries);
            platformPool.ConfigurePhysicsFeel(physicsFeel);
            platformPool.ConfigurePieceMeshes(fragmentMeshes);
            EarthPillarWaveProfile waveProfile = CreateOrLoadWaveProfile();
            EarthPillarWavePool wavePool = magicRoot.AddComponent<EarthPillarWavePool>();
            wavePool.Configure(96, wallMesh, wallMaterial, collisionProxy.transform, waveProfile);
            wavePool.ConfigureMeshVariants(fragmentMeshes);
            EarthTelekinesisController telekinesis = magicRoot.AddComponent<EarthTelekinesisController>();
            telekinesis.ConfigureHover(hoverProfile, collisionProxy.transform);
            MagicExecutor executor = magicRoot.AddComponent<MagicExecutor>();
            executor.Configure(voxelPlanet, pool, collisionProxy.transform, wallPool, heldFragmentAnchor);
            executor.ConfigureTelekinesis(telekinesis);
            executor.ConfigureEarthExtensions(
                CreateOrLoadVectorFieldProfile(), platformPool, CreateOrLoadGravityWellProfile());
            executor.ConfigureWallProfile(1.25f, 10.5f, 22f);
            EarthAudioDirector audioDirector = magicRoot.AddComponent<EarthAudioDirector>();
            audioDirector.Configure(executor);
            Material indirectDebrisMaterial = CreateOrLoadShaderMaterial(
                "EarthIndirectDebris.mat", "Elemental/Earth Indirect Debris");
            indirectDebrisMaterial.SetColor("_BaseColor", style.StoneColor * 0.82f);
            EarthIndirectDebrisRenderer indirectDebris = magicRoot.AddComponent<EarthIndirectDebrisRenderer>();
            indirectDebris.Configure(executor, fragmentMesh, indirectDebrisMaterial);
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
                looseEarthMaterial);
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
                gravityWorld, wavePool, collisionProxy.transform, worldProfile);
            CreateImpactDummy(gravityWorld, looseEarthMaterial, style);
            CreatePushBoulders(gravityWorld, looseEarthMaterial, worldProfile.Radius, physicsFeel);
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
            if (profile != null) return profile;
            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/Profiles");
            profile = ScriptableObject.CreateInstance<EarthPillarWaveProfile>();
            profile.name = "Earth Pillar Wave Profile";
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
            if (shader != null && material.shader != shader) material.shader = shader;
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
            else if (material.shader != shader) material.shader = shader;
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
            EarthPillarWavePool wavePool,
            Transform planetCenter,
            PlanetWorldProfile worldProfile)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = style.AmbientColor;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = style.SkyColor;
            RenderSettings.fogDensity = 0.0035f;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = style.SkyColor;
            camera.fieldOfView = 64f;
            camera.allowHDR = true;
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
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
            if (cameraRig != null)
            {
                EarthCameraDirector cameraDirector = camera.GetComponent<EarthCameraDirector>();
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
            CreatePlanetLandmarks(earthMaterial, style, worldProfile.Radius);
            CreateWorldAndSpace(camera, executor, planetCenter, worldProfile, style);
            CreateEarthFeedback(executor, input, cameraRig, style, wavePool, planetCenter);
            CreateGravityWellFeedback(executor, cameraRig, style, planetCenter);
            CreateEarthPillarFeedback(pillarMobility, cameraRig, style);
            CreateHud(input, executor, pillarMobility, landingCushion);
            CreatePostProcessing();
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
            EarthCinemachineCameraController controller = system.AddComponent<EarthCinemachineCameraController>();
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
                sun.color = style.SunColor;
                sun.intensity = style.SunIntensity;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.78f;
                sun.transform.rotation = Quaternion.Euler(44f, -38f, -12f);
            }

            GameObject rimObject = GameObject.Find("Earth Rim Light") ?? new GameObject("Earth Rim Light");
            Light rim = rimObject.GetComponent<Light>();
            if (rim == null) rim = rimObject.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.30f, 0.45f, 1f);
            rim.intensity = style.RimIntensity;
            rim.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.Euler(220f, 130f, 0f);

            GameObject fillObject = GameObject.Find("Earth Warm Fill") ?? new GameObject("Earth Warm Fill");
            Light fill = fillObject.GetComponent<Light>();
            if (fill == null) fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(1f, 0.42f, 0.10f);
            fill.intensity = 22f;
            fill.range = 18f;
            fill.shadows = LightShadows.None;
            fillObject.transform.position = new Vector3(-4f, 31f, -2f);
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
            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/Meshes");
            string[] paths =
            {
                FragmentMeshPath,
                "Assets/Elemental/Content/Meshes/BeveledEarthBlockB.asset",
                "Assets/Elemental/Content/Meshes/BeveledEarthBlockC.asset",
                "Assets/Elemental/Content/Meshes/BeveledEarthBlockD.asset"
            };
            var meshes = new Mesh[paths.Length];
            for (int index = 0; index < paths.Length; index++)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(paths[index]);
                if (mesh == null) mesh = new Mesh { name = $"Beveled Earth Block {index + 1}" };
                BuildBeveledBlock(mesh, 0.10f + (index * 0.018f), index);
                if (!AssetDatabase.Contains(mesh)) AssetDatabase.CreateAsset(mesh, paths[index]);
                else EditorUtility.SetDirty(mesh);
                meshes[index] = mesh;
            }
            return meshes;
        }

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
            if (existing != null) return existing;

            System.IO.Directory.CreateDirectory("Assets/Elemental/Content/Meshes");
            Vector2[] outline =
            {
                new Vector2(-0.50f, -0.50f),
                new Vector2(0.50f, -0.50f),
                new Vector2(0.50f, 0.34f),
                new Vector2(0.43f, 0.48f),
                new Vector2(0.18f, 0.45f),
                new Vector2(-0.04f, 0.50f),
                new Vector2(-0.27f, 0.46f),
                new Vector2(-0.45f, 0.50f),
                new Vector2(-0.50f, 0.38f)
            };
            var vertices = new List<Vector3>(outline.Length * 6);
            var triangles = new List<int>(outline.Length * 12);

            int frontCenter = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, -0.5f));
            int frontStart = vertices.Count;
            for (int index = 0; index < outline.Length; index++)
                vertices.Add(new Vector3(outline[index].x, outline[index].y, -0.5f));
            for (int index = 0; index < outline.Length; index++)
            {
                int next = (index + 1) % outline.Length;
                triangles.Add(frontCenter);
                triangles.Add(frontStart + next);
                triangles.Add(frontStart + index);
            }

            int backCenter = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, 0.5f));
            int backStart = vertices.Count;
            for (int index = 0; index < outline.Length; index++)
                vertices.Add(new Vector3(outline[index].x, outline[index].y, 0.5f));
            for (int index = 0; index < outline.Length; index++)
            {
                int next = (index + 1) % outline.Length;
                triangles.Add(backCenter);
                triangles.Add(backStart + index);
                triangles.Add(backStart + next);
            }

            for (int index = 0; index < outline.Length; index++)
            {
                int next = (index + 1) % outline.Length;
                int side = vertices.Count;
                vertices.Add(new Vector3(outline[index].x, outline[index].y, -0.5f));
                vertices.Add(new Vector3(outline[next].x, outline[next].y, -0.5f));
                vertices.Add(new Vector3(outline[next].x, outline[next].y, 0.5f));
                vertices.Add(new Vector3(outline[index].x, outline[index].y, 0.5f));
                triangles.Add(side);
                triangles.Add(side + 1);
                triangles.Add(side + 2);
                triangles.Add(side);
                triangles.Add(side + 2);
                triangles.Add(side + 3);
            }

            Mesh mesh = new Mesh { name = "Chipped Earth Wall" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, WallMeshPath);
            return mesh;
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
                CreateHumanoidPresentation(character, input, executor, pillarMobility);
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
            EarthPillarMobility pillarMobility)
        {
            ConfigureCharacterImporters();
            GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (characterPrefab == null)
            {
                Debug.LogWarning("[Elemental] Mixamo X Bot is unavailable; keeping the primitive presentation fallback.");
                return;
            }

            Avatar avatar = FindAvatar(CharacterModelPath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogWarning("[Elemental] Mixamo X Bot did not produce a valid Humanoid avatar; keeping the primitive presentation fallback.");
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
                Vector3.one * 1.08f);
            EditorUtility.SetDirty(profile);

            Transform old = character.transform.Find("KayKit Mage Presentation");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            old = character.transform.Find("KayKit Rogue Presentation");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            old = character.transform.Find("KayKit Knight Presentation");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            old = character.transform.Find("Mixamo X Bot Presentation");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            GameObject presentationObject = PrefabUtility.InstantiatePrefab(characterPrefab) as GameObject;
            if (presentationObject == null) return;
            presentationObject.name = "Mixamo X Bot Presentation";
            presentationObject.transform.SetParent(character.transform, false);
            presentationObject.transform.localPosition = profile.LocalPosition;
            presentationObject.transform.localRotation = profile.LocalRotation;
            presentationObject.transform.localScale = profile.LocalScale;

            foreach (Renderer renderer in presentationObject.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            Animator animator = presentationObject.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = presentationObject.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

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
                pillarMobility);
            EarthStompContactPresenter stomp = presentationObject.GetComponent<EarthStompContactPresenter>();
            if (stomp == null) stomp = presentationObject.AddComponent<EarthStompContactPresenter>();
            stomp.Configure(pillarMobility);
            HumanoidRagdollBridge bridge = presentationObject.GetComponent<HumanoidRagdollBridge>();
            if (bridge == null) bridge = presentationObject.AddComponent<HumanoidRagdollBridge>();
            bridge.Configure(animator, puppet, presentationObject.transform);
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
                // CopyFromOther therefore reports a false hierarchy mismatch. Each
                // motion owns a valid Humanoid Avatar and Mecanim retargets it onto
                // X Bot at runtime without requiring identical transform paths.
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
            bool needsTranslationDof = sourceAvatar == null && !human.hasTranslationDoF;
            bool animationLoopingChanged = ConfigureAnimationLooping(importer, isAnimationSource);
            bool dirty = forceReimport ||
                         importer.animationType != ModelImporterAnimationType.Human ||
                         importer.avatarSetup != desiredSetup ||
                         (sourceAvatar != null && importer.sourceAvatar != sourceAvatar) ||
                         (sourceAvatar == null && importer.sourceAvatar != null) ||
                         needsTranslationDof ||
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

                // KayKit locomotion contains meaningful upper-leg and hip translation.
                // Without Humanoid Translate DoF Unity discards those curves during
                // retargeting, leaving rigid-piece characters visibly frozen.
                if (needsTranslationDof)
                {
                    human.hasTranslationDoF = true;
                    importer.humanDescription = human;
                }
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

        private static void CreatePlanetLandmarks(Material earthMaterial, EarthCoreVisualStyle style, float planetRadius)
        {
            Transform old = GameObject.Find("Earth Diorama Landmarks")?.transform;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            GameObject root = new GameObject("Earth Diorama Landmarks");
            Material darkStone = CreateOrLoadLitMaterial("DarkStrata.mat", style.StoneColor * 0.48f, 0.06f, Color.black);
            Material crystal = CreateOrLoadLitMaterial("AmberCrystal.mat", style.SparkColor, 0.42f, style.SparkColor * 2.4f);

            Vector3[] dirs =
            {
                new Vector3(-0.22f, 0.96f, 0.16f).normalized,
                new Vector3(0.30f, 0.93f, 0.20f).normalized,
                new Vector3(-0.42f, 0.88f, -0.18f).normalized,
                new Vector3(0.50f, 0.84f, -0.12f).normalized
            };
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3 p = dirs[i] * (planetRadius + 0.8f);
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, dirs[i]);
                CreatePart($"Rock Formation {i + 1}", PrimitiveType.Cube, root.transform, p,
                    new Vector3(1.4f + i * 0.18f, 1.8f + (i % 2) * 0.8f, 1.2f), i % 2 == 0 ? darkStone : earthMaterial,
                    rot.eulerAngles + new Vector3(0f, i * 31f, 12f));
                Vector3 cp = dirs[i] * (planetRadius + 2.2f);
                CreatePart($"Amber Crystal {i + 1}", PrimitiveType.Cube, root.transform, cp,
                    new Vector3(0.22f, 0.72f, 0.22f), crystal, rot.eulerAngles + new Vector3(0f, 45f, 45f));
            }
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
            CreateOrLoadProfile<CharacterPresentationProfile>(CharacterProfilePath, "Character Presentation Profile");
            CreateOrLoadProfile<EarthPhysicsFeelProfile>(PhysicsFeelProfilePath, "Earth Physics Feel Profile");

            Material sky = CreateOrLoadShaderMaterial(
                "ProceduralStarSkybox.mat",
                "Elemental/Procedural Stars");
            Material fullscreenAtmosphere = CreateOrLoadShaderMaterial(
                "AtmosphereFullscreen.mat",
                "Elemental/Atmosphere Fullscreen");
            ConfigureAtmosphereRendererFeature(fullscreenAtmosphere);
            sky.SetColor("_Tint", new Color(0.42f, 0.58f, 1f));
            sky.SetFloat("_Seed", worldProfile.Seed);
            RenderSettings.skybox = sky;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, celestial.ScaledSpaceDistance * 1.35f);

            Material sunMaterial = CreateOrLoadUnlitMaterial("ScaledSun.mat", celestial.SunDiscColor);
            Material moonMaterial = CreateOrLoadLitMaterial("ScaledMoon.mat", celestial.MoonColor, 0.12f, Color.black);
            Material distantMaterial = CreateOrLoadLitMaterial("ScaledPlanet.mat", celestial.DistantPlanetColor, 0.22f, Color.black);
            GameObject sunDisc = CreatePart("Visible Sun", PrimitiveType.Sphere, backdrop.transform, Vector3.zero, Vector3.one, sunMaterial);
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
            system.Configure(
                celestial,
                atmosphere,
                skyProfile,
                planetCenter,
                camera,
                sunLight,
                sunDisc.transform,
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
            ParticleSystem streaks = CreateDistantMeteorStreaks(meteorRoot.transform, celestial, meteors);
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
                streaks);
        }

        private static ParticleSystem CreateDistantMeteorStreaks(
            Transform parent,
            CelestialSystemProfile celestial,
            MeteorShowerProfile profile)
        {
            GameObject streakObject = new GameObject("Scaled Space Meteor Streaks");
            streakObject.transform.SetParent(parent, false);
            ParticleSystem particles = streakObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = profile.DistantPoolSize;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(55f, 95f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = profile.DistantRatePerSecond;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Min(240f, celestial.ScaledSpaceDistance * 0.2f);
            shape.radiusThickness = 0.05f;
            ParticleSystemRenderer renderer = streakObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 3.5f;
            renderer.sharedMaterial = CreateOrLoadUnlitMaterial("MeteorStreak.mat", new Color(1.8f, 0.62f, 0.18f));
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
            else if (material.shader != shader) material.shader = shader;
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
            renderer.sharedMaterial = CreateOrLoadLitMaterial(
                materialName ?? (glow ? "AmberShardVfx.mat" : "EarthDustVfx.mat"),
                color,
                0.04f,
                glow ? color * 2f : Color.black);
            return ps;
        }

        private static Mesh[] LoadEarthParticleMeshVariants()
        {
            string[] paths =
            {
                FragmentMeshPath,
                "Assets/Elemental/Content/Meshes/BeveledEarthBlockB.asset",
                "Assets/Elemental/Content/Meshes/BeveledEarthBlockC.asset",
                "Assets/Elemental/Content/Meshes/BeveledEarthBlockD.asset"
            };
            var variants = new Mesh[paths.Length];
            for (int index = 0; index < paths.Length; index++)
            {
                variants[index] = AssetDatabase.LoadAssetAtPath<Mesh>(paths[index]);
                if (variants[index] == null)
                    throw new UnityEditor.Build.BuildFailedException(
                        $"Earth particle mesh variant is missing: {paths[index]}");
            }
            return variants;
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
            GameObject old = GameObject.Find("Earth Pillar Feedback");
            if (old != null) Object.DestroyImmediate(old);
            GameObject root = new GameObject("Earth Pillar Feedback");
            Material pillarMaterial = CreateOrLoadEarthMaterial(
                "EarthPillar.mat", style.StoneColor * 0.92f, 0.045f, style.StoneEmission * 0.35f);
            GameObject pillar = CreatePart(
                "Rising Earth Pillar", PrimitiveType.Cylinder, root.transform,
                Vector3.zero, Vector3.one, pillarMaterial);

            // Fixed authored chips break the cylinder silhouette without adding physics bodies.
            for (int index = 0; index < 9; index++)
            {
                float angle = index * (360f / 9f) * Mathf.Deg2Rad;
                CreatePart(
                    $"Pillar Edge Chip {index + 1:00}",
                    PrimitiveType.Cube,
                    pillar.transform,
                    new Vector3(Mathf.Cos(angle) * 0.88f, -0.72f + ((index % 4) * 0.46f), Mathf.Sin(angle) * 0.88f),
                    new Vector3(0.25f, 0.19f + ((index % 3) * 0.07f), 0.2f),
                    pillarMaterial,
                    new Vector3(index * 13f, index * 29f, index * 7f));
            }

            var chips = new Transform[20];
            for (int index = 0; index < chips.Length; index++)
            {
                GameObject chip = CreatePart(
                    $"Lift Ground Chip {index + 1:00}", PrimitiveType.Cube, root.transform,
                    Vector3.zero, Vector3.one * 0.15f, pillarMaterial);
                chips[index] = chip.transform;
            }
            EarthPillarFeedback feedback = root.AddComponent<EarthPillarFeedback>();
            feedback.Configure(mobility, pillar.transform, chips, cameraRig);
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

        private static void CreatePostProcessing()
        {
            GameObject old = GameObject.Find("Earth Core Post Processing");
            if (old != null) Object.DestroyImmediate(old);
            GameObject go = new GameObject("Earth Core Post Processing");
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Earth Core Volume Profile";
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(0.8f);
            bloom.scatter.Override(0.65f);
            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.intensity.Override(0.25f);
            vignette.smoothness.Override(0.55f);
            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.active = true;
            color.postExposure.Override(-0.2f);
            color.contrast.Override(18f);
            color.saturation.Override(-6f);
            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(profile);
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            return profile.Add<T>();
        }

        private static Material CreateOrLoadLitMaterial(string fileName, Color color, float smoothness, Color emission)
        {
            const string folder = "Assets/Elemental/Content/Materials/";
            string path = folder + fileName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new UnityEditor.Build.BuildFailedException("URP Lit shader was not found.");
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(fileName) };
                AssetDatabase.CreateAsset(material, path);
            }
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
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) throw new UnityEditor.Build.BuildFailedException("URP Unlit shader was not found.");
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(fileName) };
                AssetDatabase.CreateAsset(material, path);
            }
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

        private static void CreateImpactDummy(GravityWorldBehaviour gravityWorld, Material material, EarthCoreVisualStyle style)
        {
            GameObject existing = GameObject.Find("Earth Impact Dummy");
            if (existing != null)
            {
                return;
            }

            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = "Earth Impact Dummy";
            dummy.transform.position = new Vector3(0f, 27f, 6f);
            dummy.transform.localScale = new Vector3(1.2f, 1.5f, 1.2f);
            dummy.GetComponent<MeshRenderer>().sharedMaterial = material;
            CreateCharacterVisual(dummy, null, null, style);
            Rigidbody body = dummy.AddComponent<Rigidbody>();
            body.mass = 18f;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            GravityBody gravityBody = dummy.AddComponent<GravityBody>();
            gravityBody.Configure(gravityWorld, body);
            PhysicalImpactTarget target = dummy.AddComponent<PhysicalImpactTarget>();
            target.Configure(body, 0.35f);
            EarthCombatDummy combat = dummy.AddComponent<EarthCombatDummy>();
            combat.Configure(EarthCombatArchetype.Shaper, 150f, 620f);

            CreateCombatArchetype(
                "Earth Combat Scout",
                new Vector3(-4.4f, 26.4f, 8.2f),
                new Vector3(0.82f, 1.05f, 0.82f),
                12f,
                105f,
                430f,
                EarthCombatArchetype.Scout,
                gravityWorld,
                material);
            CreateCombatArchetype(
                "Earth Combat Sentinel",
                new Vector3(4.8f, 27.3f, 8.8f),
                new Vector3(1.35f, 1.65f, 1.35f),
                42f,
                260f,
                1050f,
                EarthCombatArchetype.Sentinel,
                gravityWorld,
                material);
            CreateCombatTrap(material);
        }

        private static void CreateCombatTrap(Material material)
        {
            if (GameObject.Find("Earth Combat Trap") != null) return;
            GameObject trap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trap.name = "Earth Combat Trap";
            trap.transform.position = new Vector3(0f, 24.5f, 10.2f);
            trap.transform.rotation = Quaternion.FromToRotation(Vector3.up, trap.transform.position.normalized);
            trap.transform.localScale = new Vector3(1.55f, 0.08f, 1.55f);
            trap.GetComponent<MeshRenderer>().sharedMaterial = material;
            EarthTrapController controller = trap.AddComponent<EarthTrapController>();
            controller.Configure(2.4f, 380f, true);
        }

        private static void CreateCombatArchetype(
            string name,
            Vector3 position,
            Vector3 scale,
            float mass,
            float staggerImpulse,
            float ragdollImpulse,
            EarthCombatArchetype archetype,
            GravityWorldBehaviour gravityWorld,
            Material material)
        {
            GameObject actor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            actor.name = name;
            actor.transform.position = position;
            actor.transform.localScale = scale;
            actor.GetComponent<MeshRenderer>().sharedMaterial = material;
            Rigidbody body = actor.AddComponent<Rigidbody>();
            body.mass = mass;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            GravityBody gravity = actor.AddComponent<GravityBody>();
            gravity.Configure(gravityWorld, body);
            PhysicalImpactTarget target = actor.AddComponent<PhysicalImpactTarget>();
            target.Configure(body, 0.35f);
            EarthCombatDummy combat = actor.AddComponent<EarthCombatDummy>();
            combat.Configure(archetype, staggerImpulse, ragdollImpulse);
            combat.SetBraced(archetype == EarthCombatArchetype.Sentinel);
        }

        private static void CreatePushBoulders(
            GravityWorldBehaviour gravityWorld,
            Material material,
            float planetRadius,
            EarthPhysicsFeelProfile physicsFeel)
        {
            GameObject existing = GameObject.Find("Magic Push Boulders");
            if (existing != null) Object.DestroyImmediate(existing);
            GameObject root = new GameObject("Magic Push Boulders");
            CreatePushBoulder(root.transform, "Light Push Boulder", new Vector3(-3.8f, planetRadius + 0.15f, 3.7f),
                0.72f, 55f, gravityWorld, material);
            CreatePushBoulder(root.transform, "Heavy Push Boulder", new Vector3(4.2f, planetRadius + 0.35f, 4.1f),
                1.05f, 320f, gravityWorld, material);
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
            Material material)
        {
            GameObject boulder = new GameObject(name);
            boulder.transform.SetParent(parent, false);
            boulder.transform.position = position;
            boulder.transform.localScale = Vector3.one * (radius * 2f);
            boulder.transform.rotation = Quaternion.Euler(17f, mass * 0.19f, -11f);
            Mesh mesh = CreateOrLoadFragmentMesh();
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
