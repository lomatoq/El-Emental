using System.Collections.Generic;
using Elemental.Authoring.Editor;
using Elemental.Authoring.Fracture;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Tests.EditMode
{
    public sealed class BrokenCrownArenaImporterTests
    {
        [Test]
        public void RebuildProducesValidatedCauseDrivenCatalog()
        {
            EarthArenaFractureCatalog catalog = BrokenCrownArenaImporter.Rebuild();

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.SchemaVersion, Is.EqualTo(1));
            Assert.That(catalog.ImportedModel, Is.Not.Null);
            Assert.That(catalog.Structures, Has.Length.EqualTo(8));
            Assert.That(catalog.LooseRockObjectNames, Has.Length.EqualTo(7));
            Assert.That(catalog.CosmeticRubbleObjectNames, Has.Length.EqualTo(2));

            int totalPieces = 0;
            for (int index = 0; index < catalog.Structures.Length; index++)
            {
                EarthArenaFractureEntry entry = catalog.Structures[index];
                Assert.That(entry.fractureAsset, Is.Not.Null, entry.structureId);
                EarthFractureValidationResult validation =
                    EarthFractureValidator.Validate(entry.fractureAsset);
                Assert.That(validation.IsValid, Is.True,
                    $"{entry.structureId}: {validation.Error} at {validation.Index}");
                totalPieces += entry.fractureAsset.PieceCount;
            }
            Assert.That(totalPieces, Is.EqualTo(90));

            Transform[] importedTransforms =
                catalog.ImportedModel.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < catalog.Structures.Length; index++)
            {
                EarthArenaFractureEntry entry = catalog.Structures[index];
                Transform intact = null;
                for (int candidate = 0; candidate < importedTransforms.Length; candidate++)
                    if (importedTransforms[candidate].name == entry.intactObjectName)
                    {
                        intact = importedTransforms[candidate];
                        break;
                    }
                Assert.That(intact, Is.Not.Null, entry.intactObjectName);
                Mesh mesh = intact.GetComponent<MeshFilter>()?.sharedMesh;
                Assert.That(mesh, Is.Not.Null, entry.intactObjectName);
                Assert.That(mesh.subMeshCount, Is.EqualTo(1),
                    $"{entry.intactObjectName} must keep one continuous exterior submesh.");
                Color32[] colors = mesh.colors32;
                bool hasInteriorMask = false;
                for (int colorIndex = 0; colorIndex < colors.Length; colorIndex++)
                    if (colors[colorIndex].g > colors[colorIndex].r)
                    {
                        hasInteriorMask = true;
                        break;
                    }
                Assert.That(hasInteriorMask, Is.False,
                    $"{entry.intactObjectName} incorrectly marks repaired source holes as fresh fracture.");

                EarthFractureAsset fractureAsset = entry.fractureAsset;
                for (int pieceIndex = 0; pieceIndex < fractureAsset.PieceCount; pieceIndex++)
                {
                    Mesh piece = fractureAsset.GetPieceRenderMesh(pieceIndex);
                    Assert.That(piece, Is.Not.Null, $"{entry.structureId}:{pieceIndex}");
                    Assert.That(piece.subMeshCount, Is.EqualTo(2),
                        $"{entry.structureId}:{pieceIndex} must keep exterior + fracture interior.");
                    Assert.That(piece.GetIndexCount(1), Is.GreaterThan(0),
                        $"{entry.structureId}:{pieceIndex} lost its sandstone fracture cut.");
                }
            }

            Assert.That(catalog.TryGet("arena_floor_base", out EarthArenaFractureEntry floor), Is.True);
            Assert.That(floor.activationCause, Is.EqualTo(EarthArenaFractureCause.MeteorImpact));
            Assert.That(floor.ordinaryDamageEnabled, Is.False);
            Assert.That(floor.repairable, Is.False);
            Assert.That(floor.usesVirtualDormantSupport, Is.True);
            Assert.That(floor.fractureProfile, Is.EqualTo("meteor_radial_plane_v1"));
            Assert.That(floor.fractureAsset.PieceCount, Is.EqualTo(36));

            AssertImpactStructure(
                catalog, "arena_gate", "architectural_plane_split_v1", 12);
            AssertWall(catalog, "arena_wall_east");
            AssertWall(catalog, "arena_wall_west");
            AssertImpactStructure(
                catalog, "arena_column_north_west", "column_break_plane_split_v1", 5);
            AssertImpactStructure(
                catalog, "arena_column_north_east", "column_break_plane_split_v1", 5);
            AssertImpactStructure(
                catalog, "arena_column_south_east", "column_break_plane_split_v1", 4);
            AssertImpactStructure(
                catalog, "arena_column_south_west", "column_break_plane_split_v1", 4);
        }

        [Test]
        public void ModelImporterKeepsMeshesReadableWithoutAnimationOrMaterials()
        {
            AssetDatabase.ImportAsset(
                BrokenCrownArenaImporter.ModelPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(
                BrokenCrownArenaImporter.ModelPath) as ModelImporter;

            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.isReadable, Is.True);
            Assert.That(importer.importAnimation, Is.False);
            Assert.That(importer.importCameras, Is.False);
            Assert.That(importer.importLights, Is.False);
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None));
            Assert.That(importer.meshCompression, Is.EqualTo(ModelImporterMeshCompression.Off));
            Assert.That(importer.weldVertices, Is.True);
        }

        [Test]
        public void SavedArenaPreservesEveryAuthoredChildTransform()
        {
            const string scenePath =
                "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool loadedByTest = !scene.IsValid() || !scene.isLoaded;
            if (loadedByTest)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                GameObject arena = null;
                GameObject bot = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int index = 0; index < roots.Length; index++)
                {
                    if (roots[index].name == "Broken Crown Arena")
                    {
                        arena = roots[index];
                    }
                    else if (roots[index].name == "Rumble Linebreaker Bot")
                        bot = roots[index];
                }

                EarthArenaFractureCatalog catalog =
                    AssetDatabase.LoadAssetAtPath<EarthArenaFractureCatalog>(
                        BrokenCrownArenaSceneIntegrator.CatalogPath);
                Assert.That(arena, Is.Not.Null);
                Assert.That(bot, Is.Not.Null);
                Assert.That(catalog, Is.Not.Null);
                Assert.That(catalog.ImportedModel, Is.Not.Null);
                Assert.That(arena.transform.position.x, Is.Zero.Within(0.0005f));
                Assert.That(arena.transform.position.y, Is.EqualTo(54.12f).Within(0.0005f),
                    "The approved arena embed must not be lifted back onto the pedestal.");
                Assert.That(arena.transform.position.z, Is.Zero.Within(0.0005f));
                Assert.That(bot.transform.position.x, Is.EqualTo(-0.26751554f).Within(0.0005f));
                Assert.That(bot.transform.position.y, Is.EqualTo(58f).Within(0.0005f),
                    "The approved opponent spawn must survive generated-scene rebuilds.");
                Assert.That(bot.transform.position.z, Is.EqualTo(3.5498571f).Within(0.0005f));
                CapsuleCollider botCapsule = bot.GetComponent<CapsuleCollider>();
                PlanetMotor botMotor = bot.GetComponent<PlanetMotor>();
                Assert.That(botCapsule, Is.Not.Null);
                Assert.That(botMotor, Is.Not.Null);
                Assert.That(botMotor.GroundContactSkin, Is.GreaterThanOrEqualTo(0.04f),
                    "The rival needs a serialized mesh-contact skin for the crater floor.");

                Transform sourceRoot = catalog.ImportedModel.transform;
                Transform[] sourceTransforms =
                    catalog.ImportedModel.GetComponentsInChildren<Transform>(true);
                int compared = 0;
                for (int index = 0; index < sourceTransforms.Length; index++)
                {
                    Transform source = sourceTransforms[index];
                    if (source == sourceRoot) continue;
                    string path = AnimationUtility.CalculateTransformPath(source, sourceRoot);
                    Transform actual = arena.transform.Find(path);
                    Assert.That(actual, Is.Not.Null, path);
                    Assert.That(Vector3.Distance(actual.localPosition, source.localPosition),
                        Is.LessThanOrEqualTo(0.0001f), $"{path} local position drifted.");
                    Assert.That(Quaternion.Angle(actual.localRotation, source.localRotation),
                        Is.LessThanOrEqualTo(0.01f), $"{path} local rotation drifted.");
                    Assert.That(Vector3.Distance(actual.localScale, source.localScale),
                        Is.LessThanOrEqualTo(0.0001f), $"{path} local scale drifted.");
                    compared++;
                }

                Assert.That(compared, Is.GreaterThanOrEqualTo(375));
                Renderer[] arenaRenderers = arena.GetComponentsInChildren<Renderer>(true);
                for (int index = 0; index < arenaRenderers.Length; index++)
                {
                    Assert.That(arenaRenderers[index].shadowCastingMode,
                        Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off),
                        arenaRenderers[index].name);
                    Assert.That(arenaRenderers[index].receiveShadows, Is.False,
                        arenaRenderers[index].name);
                }
            }
            finally
            {
                if (loadedByTest) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void OrdinaryCombatUsesBoundedLocalReleaseWhileMeteorOwnsTheFloorSwap()
        {
            EarthArenaFractureDecision weak = EarthArenaFractureGate.Resolve(
                true, EarthArenaFractureTrigger.OrdinaryImpact, 40f, 12);
            EarthArenaFractureDecision ordinary = EarthArenaFractureGate.Resolve(
                true, EarthArenaFractureTrigger.OrdinaryImpact, 900f, 12);
            EarthArenaFractureDecision protectedFloor = EarthArenaFractureGate.Resolve(
                false, EarthArenaFractureTrigger.MagicPluck, 0f, 36);
            EarthArenaFractureDecision meteor = EarthArenaFractureGate.Resolve(
                false, EarthArenaFractureTrigger.MeteorImpact, 0f, 36);

            Assert.That(weak.Accepted, Is.False);
            Assert.That(ordinary.Accepted, Is.True);
            Assert.That(ordinary.ReleaseCount, Is.EqualTo(2));
            Assert.That(protectedFloor.Accepted, Is.False);
            Assert.That(meteor.Accepted, Is.True);
            Assert.That(meteor.ReleaseCount, Is.EqualTo(36));
        }

        [Test]
        public void FloorSeatingOnlyOverridesAPropWhenTheFloorWasActuallyHit()
        {
            EarthArenaPropSeatingDecision outside = EarthArenaPropSeatingSolver.Resolve(
                10.2f, false, 11f, 0.02f);
            EarthArenaPropSeatingDecision onFloor = EarthArenaPropSeatingSolver.Resolve(
                10.2f, true, 10.5f, 0.02f);

            Assert.That(outside.UsesArenaFloor, Is.False);
            Assert.That(outside.ShiftAlongUp, Is.Zero,
                "An exterior rock must keep its sphere seat when there is no floor beneath it.");
            Assert.That(onFloor.UsesArenaFloor, Is.True);
            Assert.That(onFloor.ShiftAlongUp, Is.EqualTo(0.28f).Within(0.0001f));
        }

        [Test]
        public void VisibleSupportPatchUsesMedianWitnessWithoutExceedingControlledInset()
        {
            float[] prop = { 10.20f, 10.22f, 10.18f, 10.70f };
            bool[] hits = { true, true, true, false };
            float[] floor = { 10.50f, 10.51f, 10.49f, 0f };

            EarthArenaPropSupportDecision decision =
                EarthArenaPropSeatingSolver.ResolveSupportPatch(
                    prop, hits, floor, 4, 0.01f);

            Assert.That(decision.UsesArenaFloor, Is.True);
            Assert.That(decision.ValidSampleCount, Is.EqualTo(3));
            Assert.That(decision.ShiftAlongUp, Is.EqualTo(0.30f).Within(0.0001f));
            Assert.That(decision.MinimumGapAfterShift, Is.EqualTo(-0.01f).Within(0.0001f));
            Assert.That(decision.MedianGapAfterShift, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void SupportPatchRequiresThreeUniqueWitnessesAroundCenterOfMass()
        {
            float[] prop = { 5f, 5f, 5f, 5f };
            bool[] hits = { true, true, true, true };
            float[] floor = { 5f, 5f, 5f, 5f };
            float2[] triangle =
            {
                new float2(-0.5f, -0.4f),
                new float2(0.5f, -0.4f),
                new float2(0f, 0.6f),
                new float2(0f, 0.6f)
            };

            EarthArenaPropSupportDecision supported =
                EarthArenaPropSeatingSolver.ResolveSupportPatch(
                    prop, hits, floor, triangle, float2.zero, 4, 0.01f, true);
            EarthArenaPropSupportDecision unsupported =
                EarthArenaPropSeatingSolver.ResolveSupportPatch(
                    prop, hits, floor, triangle, new float2(1.2f, 1.2f), 4, 0.01f, true);

            Assert.That(supported.Accepted, Is.True);
            Assert.That(supported.ContactWitnessCount, Is.EqualTo(3));
            Assert.That(supported.ContactPatchArea, Is.GreaterThan(0.1f));
            Assert.That(supported.CenterSupported, Is.True);
            Assert.That(unsupported.Accepted, Is.False);
            Assert.That(unsupported.CenterSupported, Is.False);
        }

        [Test]
        public void SupportPatchCapsSamplesAndRejectsMoreThanOneCentimetreBurial()
        {
            float[] prop = new float[10];
            bool[] hits = new bool[10];
            float[] floor = new float[10];
            for (int index = 0; index < 10; index++)
            {
                prop[index] = 10f;
                floor[index] = 10.20f;
                hits[index] = true;
            }

            EarthArenaPropSupportDecision decision =
                EarthArenaPropSeatingSolver.ResolveSupportPatch(
                    prop, hits, floor, 10, 0.01f);

            Assert.That(decision.ValidSampleCount,
                Is.EqualTo(EarthArenaPropSeatingSolver.MaximumSupportSamples));
            Assert.That(decision.MinimumGapAfterShift,
                Is.InRange(EarthArenaPropSeatingSolver.MinimumAcceptedGap - 0.0001f,
                    EarthArenaPropSeatingSolver.MaximumAcceptedGap));
            Assert.That(decision.MaximumGapAfterShift,
                Is.InRange(EarthArenaPropSeatingSolver.MinimumAcceptedGap - 0.0001f,
                    EarthArenaPropSeatingSolver.MaximumAcceptedGap));
            Assert.That(decision.Accepted, Is.True);
        }

        [Test]
        public void NativeArenaLookdevUsesAuthoredNormalsHighShadowFilteringAndCleanContactAo()
        {
            Material arena = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Elemental/Content/GraphicsV5/Materials/RumbleArenaSandstone.mat");
            Assert.That(arena, Is.Not.Null);
            Assert.That(arena.GetFloat("_SideShadingSmoothness"), Is.Zero,
                "Imported architectural normals must not be replaced by radial rock normals.");
            Assert.That(arena.GetFloat("_SideShadowFade"), Is.EqualTo(1f).Within(0.001f),
                "Architectural side faces must reject travelling cascade bands and use stable analytic form depth.");
            Assert.That(arena.GetFloat("_StableSideFormOcclusion"),
                Is.InRange(0.06f, 0.10f),
                "Stable side/recess form depth must stay subtle and cannot become a broad dirty wash.");
            Assert.That(arena.GetFloat("_FractureInteriorDepth"),
                Is.InRange(0.12f, 0.20f));
            Assert.That(arena.GetFloat("_AmbientStrength"),
                Is.EqualTo(0.76f).Within(0.001f),
                "Broad arena form needs restrained ambient fill beside stable SSAO and analytic recess depth.");
            Assert.That(arena.GetFloat("_FacetContrast"),
                Is.EqualTo(0.16f).Within(0.001f));
            Material fractureInterior = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Elemental/Content/GraphicsV5/Materials/RumbleSandstoneFractureInterior.mat");
            Assert.That(fractureInterior, Is.Not.Null);
            Assert.That(fractureInterior.GetColor("_BaseColor").grayscale,
                Is.LessThan(arena.GetColor("_BaseColor").grayscale),
                "Fresh fracture interiors must sit below the weathered exterior in value while staying in the same palette.");
            Assert.That(arena.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0),
                "RumbleRockLit must participate in the Base camera DepthNormals prepass used by SSAO.");
            Assert.That(arena.HasProperty("_ReceiverPlanetCenter"), Is.True,
                "The stable receiver must serialize its planet frame for both Scene and Game rendering.");
            Vector4 receiverCenter = arena.GetVector("_ReceiverPlanetCenter");
            Assert.That(float.IsFinite(receiverCenter.x) &&
                        float.IsFinite(receiverCenter.y) &&
                        float.IsFinite(receiverCenter.z), Is.True);
            Assert.That(arena.GetFloat("_MacroStrength"), Is.LessThanOrEqualTo(0.025f));
            Assert.That(fractureInterior.GetFloat("_AmbientStrength"),
                Is.EqualTo(arena.GetFloat("_AmbientStrength")).Within(0.001f));
            Assert.That(fractureInterior.GetFloat("_FacetContrast"),
                Is.EqualTo(arena.GetFloat("_FacetContrast")).Within(0.001f));
            Assert.That(fractureInterior.GetFloat("_MacroStrength"),
                Is.EqualTo(arena.GetFloat("_MacroStrength")).Within(0.001f));

            Object pipeline = AssetDatabase.LoadMainAssetAtPath(
                "Assets/Settings/ElEmentalURP.asset");
            Assert.That(pipeline, Is.Not.Null);
            var pipelineSerialized = new SerializedObject(pipeline);
            Assert.That(
                pipelineSerialized.FindProperty("m_MainLightShadowmapResolution")?.intValue,
                Is.EqualTo(4096));
            Assert.That(
                pipelineSerialized.FindProperty("m_ShadowCascadeCount")?.intValue,
                Is.EqualTo(4));
            Assert.That(
                pipelineSerialized.FindProperty("m_SoftShadowQuality")?.intValue,
                Is.EqualTo(3),
                "Native High must use URP's High 7x7 soft-shadow filter.");
            Assert.That(
                pipelineSerialized.FindProperty("m_ShadowDistance")?.floatValue,
                Is.EqualTo(90f).Within(0.001f));
            Assert.That(
                pipelineSerialized.FindProperty("m_ShadowDepthBias")?.floatValue,
                Is.EqualTo(0.50f).Within(0.001f));
            Assert.That(
                pipelineSerialized.FindProperty("m_ShadowNormalBias")?.floatValue,
                Is.EqualTo(0.30f).Within(0.001f));

            Object renderer = AssetDatabase.LoadMainAssetAtPath(
                "Assets/Settings/ElEmentalRenderer.asset");
            Assert.That(renderer, Is.Not.Null);
            var rendererSerialized = new SerializedObject(renderer);
            SerializedProperty features = rendererSerialized.FindProperty("m_RendererFeatures");
            Object ao = null;
            for (int index = 0; features != null && index < features.arraySize; index++)
            {
                Object feature = features.GetArrayElementAtIndex(index).objectReferenceValue;
                if (feature != null && feature.name == "Elemental Contact SSAO")
                {
                    ao = feature;
                    break;
                }
            }
            Assert.That(ao, Is.Not.Null);
            var aoSerialized = new SerializedObject(ao);
            Assert.That(aoSerialized.FindProperty("m_Settings.Downsample")?.boolValue,
                Is.False);
            Assert.That(aoSerialized.FindProperty("m_Settings.AfterOpaque")?.boolValue,
                Is.False);
            Assert.That(aoSerialized.FindProperty("m_Settings.Source")?.intValue,
                Is.EqualTo(1),
                "Game-camera SSAO must consume authored DepthNormals.");
            Assert.That(aoSerialized.FindProperty("m_Settings.Intensity")?.floatValue,
                Is.EqualTo(0.82f).Within(0.001f));
            Assert.That(aoSerialized.FindProperty("m_Settings.DirectLightingStrength")?.floatValue,
                Is.EqualTo(0.08f).Within(0.001f));
            Assert.That(aoSerialized.FindProperty("m_Settings.Radius")?.floatValue,
                Is.EqualTo(0.065f).Within(0.001f));
            Assert.That(aoSerialized.FindProperty("m_Settings.Samples")?.intValue,
                Is.EqualTo(0));
            Assert.That(aoSerialized.FindProperty("m_Settings.BlurQuality")?.intValue,
                Is.EqualTo(0));
        }

        private static void AssertWall(EarthArenaFractureCatalog catalog, string id)
        {
            AssertImpactStructure(catalog, id, "masonry_watershed_power_v1", 12);
        }

        private static void AssertImpactStructure(
            EarthArenaFractureCatalog catalog,
            string id,
            string profile,
            int pieceCount)
        {
            Assert.That(catalog.TryGet(id, out EarthArenaFractureEntry structure), Is.True);
            Assert.That(structure.activationCause, Is.EqualTo(EarthArenaFractureCause.Impact));
            Assert.That(structure.ordinaryDamageEnabled, Is.True);
            Assert.That(structure.repairable, Is.True);
            Assert.That(structure.usesVirtualDormantSupport, Is.False);
            Assert.That(structure.fractureProfile, Is.EqualTo(profile));
            Assert.That(structure.fractureAsset.PieceCount, Is.EqualTo(pieceCount));
        }
    }
}
