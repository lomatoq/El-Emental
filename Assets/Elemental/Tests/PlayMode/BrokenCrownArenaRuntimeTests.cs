using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class BrokenCrownArenaRuntimeTests
    {
        [UnityTest]
        public IEnumerator LocalDamagePluckGravityAndProtectedFloorShareOneBoundedContract()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool loadedByTest = !scene.IsValid() || !scene.isLoaded;
            if (loadedByTest)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                Assert.That(load, Is.Not.Null);
                yield return load;
                scene = SceneManager.GetSceneByPath(scenePath);
            }

            EarthMvpBotController bot = FindInScene<EarthMvpBotController>(scene);
            bool botWasEnabled = bot != null && bot.enabled;
            if (bot != null) bot.enabled = false;
            EarthArenaStructure[] spawnStructures = FindAllInScene<EarthArenaStructure>(scene);
            PlanetMotor playerMotor = FindByName(scene, "Planet Character")
                ?.GetComponent<PlanetMotor>();
            Assert.That(playerMotor, Is.Not.Null);
            PlanetMotor[] spawnMotors = FindAllInScene<PlanetMotor>(scene);
            Assert.That(spawnMotors, Has.Length.GreaterThanOrEqualTo(2));
            Physics.SyncTransforms();
            for (int motorIndex = 0; motorIndex < spawnMotors.Length; motorIndex++)
            {
            Collider[] motorColliders = spawnMotors[motorIndex]
                .GetComponentsInChildren<Collider>(true);
            for (int bodyIndex = 0; bodyIndex < motorColliders.Length; bodyIndex++)
            for (int arenaIndex = 0; arenaIndex < spawnStructures.Length; arenaIndex++)
            {
                Collider bodyCollider = motorColliders[bodyIndex];
                Collider arenaCollider = spawnStructures[arenaIndex].GetComponent<Collider>();
                if (bodyCollider == null || !bodyCollider.enabled ||
                    arenaCollider == null || !arenaCollider.enabled) continue;
                bool penetrates = Physics.ComputePenetration(
                    bodyCollider,
                    bodyCollider.transform.position,
                    bodyCollider.transform.rotation,
                    arenaCollider,
                    arenaCollider.transform.position,
                    arenaCollider.transform.rotation,
                    out _,
                    out float depth);
                Assert.That(penetrates && depth > 0.001f, Is.False,
                    $"{spawnMotors[motorIndex].name} must not spawn {depth:0.000} m " +
                    $"inside {spawnStructures[arenaIndex].name}.");
            }
            }

            AssertArenaPropsAreSeated(scene, "loaded");

            yield return null;
            // Other independent authored structures can share this scene and runtime.
            EarthArenaStructure[] structures = FindByName(scene, "Broken Crown Arena")
                .GetComponentsInChildren<EarthArenaStructure>(true);
            MagicExecutor executor = FindInScene<MagicExecutor>(scene);
            EarthPillarWavePool wavePool = FindInScene<EarthPillarWavePool>(scene);
            EarthWallPool wallPool = FindInScene<EarthWallPool>(scene);
            EarthPlatformPool platformPool = FindInScene<EarthPlatformPool>(scene);
            Assert.That(structures, Has.Length.EqualTo(8));
            Assert.That(executor, Is.Not.Null);
            Assert.That(wavePool, Is.Not.Null);
            Assert.That(wallPool, Is.Not.Null);
            Assert.That(platformPool, Is.Not.Null);

            EarthWall[] generatedWalls = FindAllInScene<EarthWall>(scene);
            EarthPlatform[] generatedPlatforms = FindAllInScene<EarthPlatform>(scene);
            Assert.That(generatedWalls, Has.Length.GreaterThanOrEqualTo(1));
            Assert.That(generatedPlatforms, Has.Length.GreaterThanOrEqualTo(1));
            for (int wallIndex = 0; wallIndex < generatedWalls.Length; wallIndex++)
            {
                Rigidbody[] fractureBodies =
                    generatedWalls[wallIndex].GetComponentsInChildren<Rigidbody>(true);
                Assert.That(fractureBodies, Has.Length.GreaterThan(1));
                for (int index = 0; index < fractureBodies.Length; index++)
                    Assert.That(fractureBodies[index].GetComponent<GravityBody>(), Is.Not.Null,
                        $"Released wall body {fractureBodies[index].name} must retain planet gravity.");
            }
            for (int platformIndex = 0; platformIndex < generatedPlatforms.Length; platformIndex++)
            {
                Rigidbody[] fractureBodies =
                    generatedPlatforms[platformIndex].GetComponentsInChildren<Rigidbody>(true);
                Assert.That(fractureBodies, Has.Length.GreaterThan(1));
                for (int index = 0; index < fractureBodies.Length; index++)
                    Assert.That(fractureBodies[index].GetComponent<GravityBody>(), Is.Not.Null,
                        $"Released platform body {fractureBodies[index].name} must retain planet gravity.");
            }

            HumanoidCharacterPresentation[] characters =
                FindAllInScene<HumanoidCharacterPresentation>(scene);
            Assert.That(characters, Has.Length.GreaterThanOrEqualTo(2));
            for (int index = 0; index < characters.Length; index++)
            {
                Assert.That(characters[index].FootContactController, Is.Not.Null,
                    $"{characters[index].name} has no independent foot-contact owner.");
                Assert.That(characters[index].FootContactController.enabled, Is.True,
                    $"{characters[index].name} foot-contact owner is disabled.");
            }

            Camera gameplayCamera = FindInScene<Camera>(scene);
            Assert.That(gameplayCamera, Is.Not.Null);
            UniversalAdditionalCameraData cameraData =
                gameplayCamera.GetUniversalAdditionalCameraData();
            Assert.That(cameraData.antialiasing,
                Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
            Assert.That(cameraData.antialiasingQuality, Is.EqualTo(AntialiasingQuality.High));
            Assert.That(cameraData.stopNaN, Is.True);
            Assert.That(cameraData.dithering, Is.True);
            Assert.That(cameraData.requiresDepthTexture, Is.True);
            Assert.That(cameraData.renderShadows, Is.False,
                "EarthCore Game camera must not render the striped realtime shadow map.");
            Light sun = RenderSettings.sun;
            Assert.That(sun, Is.Not.Null);
            Assert.That(sun.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(sun.shadowStrength, Is.Zero.Within(0.001f));

            AssertArenaPropsAreSeated(scene, "settled-frame");

            for (int index = 0; index < structures.Length; index++)
            {
                Renderer intactRenderer = structures[index].GetComponent<Renderer>();
                Assert.That(intactRenderer, Is.Not.Null, structures[index].name);
                Assert.That(intactRenderer.shadowCastingMode,
                    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off),
                    $"{structures[index].name} must not cast striped realtime shadows.");
                Assert.That(intactRenderer.receiveShadows, Is.False,
                    $"{structures[index].name} must use stable SSAO/analytic form instead of realtime shadow bands.");
                Assert.That(intactRenderer.sharedMaterials, Has.Length.EqualTo(1),
                    $"{structures[index].name} must remain one continuous exterior material; " +
                    "only true fracture cuts may use an interior slot.");
                Assert.That(intactRenderer.sharedMaterials[0], Is.Not.Null, structures[index].name);

                EarthArenaSurfaceProvider provider =
                    structures[index].GetComponent<EarthArenaSurfaceProvider>();
                Collider intactCollider = structures[index].GetComponent<Collider>();
                Assert.That(provider, Is.Not.Null, structures[index].name);
                Assert.That(intactCollider, Is.Not.Null, structures[index].name);
                Vector3 up = (structures[index].transform.position - Vector3.zero).normalized;
                if (up.sqrMagnitude < 0.5f) up = Vector3.up;
                var surfaceQuery = new EarthSurfaceQuery(
                    new Unity.Mathematics.float3(
                        intactCollider.bounds.center.x + up.x * 10f,
                        intactCollider.bounds.center.y + up.y * 10f,
                        intactCollider.bounds.center.z + up.z * 10f),
                    new Unity.Mathematics.float3(-up.x, -up.y, -up.z),
                    20f,
                    EarthSurfaceCapabilities.Support | EarthSurfaceCapabilities.Pillar);
                Assert.That(provider.TrySample(in surfaceQuery, out EarthSurfaceSample surface), Is.True,
                    $"{structures[index].name} top face must accept support/pillar magic.");
                Assert.That(surface.Supports(
                    EarthSurfaceCapabilities.Draw | EarthSurfaceCapabilities.Support |
                    EarthSurfaceCapabilities.Pillar | EarthSurfaceCapabilities.LandingCushion), Is.True,
                    $"{structures[index].name} must expose the complete constructed-Earth top-face contract.");
            }

            EarthArenaStructure floor = null;
            EarthArenaStructure gate = null;
            EarthArenaStructure vectorStructure = null;
            EarthArenaStructure gravityStructure = null;
            for (int index = 0; index < structures.Length; index++)
            {
                EarthArenaStructure structure = structures[index];
                if (!structure.OrdinaryDamageEnabled) floor = structure;
                else if (structure.name.Contains("Gate")) gate = structure;
                else if (vectorStructure == null) vectorStructure = structure;
                else if (gravityStructure == null) gravityStructure = structure;
            }

            Assert.That(floor, Is.Not.Null);
            Assert.That(gate, Is.Not.Null);
            Assert.That(vectorStructure, Is.Not.Null);
            Assert.That(gravityStructure, Is.Not.Null);

            Rigidbody playerBody = playerMotor.GetComponent<Rigidbody>();
            Vector3 playerUp = playerMotor.LocalUp.sqrMagnitude > 0.5f
                ? playerMotor.LocalUp.normalized
                : playerMotor.transform.up;
            Vector3 playerForward = Vector3.ProjectOnPlane(playerMotor.FacingForward, playerUp).normalized;
            Assert.That(playerBody, Is.Not.Null);
            int waveCellCount = wavePool.Launch(
                playerBody.worldCenterOfMass - playerUp * 1.25f,
                playerUp,
                playerForward,
                0.35f,
                0.65f,
                playerBody);
            Assert.That(waveCellCount, Is.GreaterThan(0));
            int crestCellCount = wavePool.LaunchCrest(
                playerBody.worldCenterOfMass + playerMotor.transform.right * 2.5f -
                playerUp * 1.25f,
                playerUp,
                playerForward,
                3,
                playerBody);
            Assert.That(crestCellCount, Is.EqualTo(3));
            var pillarRecords = new Dictionary<uint, PillarSupportRecord>(64);
            float waveTimeout = Time.time + 2.8f;
            while (Time.time < waveTimeout)
            {
                EarthPillarWaveColumn[] columns = FindAllInScene<EarthPillarWaveColumn>(scene);
                for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
                {
                    EarthPillarWaveColumn column = columns[columnIndex];
                    if (!column.TryGetVisiblePlacementDiagnostic(
                            out Mesh mesh,
                            out Matrix4x4 matrix,
                            out Vector3 surface,
                            out Vector3 supportUp,
                            out float visualHeight01,
                            out bool polygonCell) ||
                        Mathf.Abs(visualHeight01 - 1f) > 0.0001f) continue;
                    float gap = MeasureMatrixSupportGap(
                        mesh, matrix, surface, supportUp);
                    if (!pillarRecords.TryGetValue(column.StableEarthId, out PillarSupportRecord record))
                    {
                        record = new PillarSupportRecord
                        {
                            stableId = column.StableEarthId,
                            polygonCell = polygonCell,
                            minimumGapMeters = gap,
                            maximumGapMeters = gap,
                            sampleCount = 0,
                            accepted = true
                        };
                    }
                    record.minimumGapMeters = Mathf.Min(record.minimumGapMeters, gap);
                    record.maximumGapMeters = Mathf.Max(record.maximumGapMeters, gap);
                    record.sampleCount++;
                    record.accepted &= gap >= -0.0105f && gap <= 0.015f;
                    pillarRecords[column.StableEarthId] = record;
                }
                yield return null;
            }
            WritePillarSupportReport(pillarRecords);
            int polygonPillars = 0;
            int legacyPillars = 0;
            var pillarFailures = new List<string>();
            foreach (KeyValuePair<uint, PillarSupportRecord> pair in pillarRecords)
            {
                if (pair.Value.polygonCell) polygonPillars++;
                else legacyPillars++;
                if (!pair.Value.accepted)
                    pillarFailures.Add(
                        $"{pair.Key}: [{pair.Value.minimumGapMeters:0.0000}, " +
                        $"{pair.Value.maximumGapMeters:0.0000}] m");
            }
            Assert.That(polygonPillars, Is.GreaterThan(0),
                "Fault Line polygon pillars never reached a measurable full-rise seat.");
            Assert.That(legacyPillars, Is.GreaterThan(0),
                "Crest non-polygon pillars never reached a measurable full-rise seat.");
            Assert.That(pillarFailures, Is.Empty,
                $"Half-buried/full-rise pillar regression: {string.Join("; ", pillarFailures)}");
            Assert.That(wavePool.LastFaultLineTargetStructureId, Is.EqualTo(floor.StructureId),
                "Fault Line must rise from the protected Broken Crown floor instead of the hidden planet shell.");
            Assert.That(floor.ReleasedPieceCount, Is.Zero,
                "Fault Line may use the arena floor as a surface but must not cut or cascade-fracture it.");

            var weak = new EarthStructureImpact(
                gate.transform.position, Vector3.forward, 40f,
                EarthStructureImpactKind.Projectile, 1u);
            Assert.That(gate.ApplyEarthImpact(in weak), Is.False);

            var strong = new EarthStructureImpact(
                gate.transform.position, Vector3.forward, 900f,
                EarthStructureImpactKind.Projectile, 2u);
            Assert.That(gate.ApplyEarthImpact(in strong), Is.True);
            yield return null;
            ParticleSystem fractureDust =
                FindByName(scene, "Arena Fracture Dust")?.GetComponent<ParticleSystem>();
            Assert.That(fractureDust, Is.Not.Null,
                "Broken Crown fracture needs a dedicated dense dust presenter.");
            Assert.That(fractureDust.particleCount, Is.GreaterThanOrEqualTo(120),
                "A structural break must be hidden inside a dense dust pulse, not expose a dry proxy swap.");
            var targets = new IEarthPhysicalTarget[48];
            int activeTargets = gate.CopyActiveTargetsNonAlloc(targets);
            Assert.That(activeTargets, Is.EqualTo(2));
            EarthArenaPiece[] fracturePieces = FindAllInScene<EarthArenaPiece>(scene);
            int visibleFractureMeshes = 0;
            for (int rendererIndex = 0; rendererIndex < fracturePieces.Length; rendererIndex++)
            {
                EarthArenaPiece piece = fracturePieces[rendererIndex];
                if (piece == null || piece.Owner != gate || !piece.gameObject.activeInHierarchy) continue;
                Renderer renderer = piece.GetComponent<Renderer>();
                if (renderer == null) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                visibleFractureMeshes++;
                Assert.That(filter.sharedMesh.name, Does.EndWith("Render"),
                    $"{renderer.name} must swap to the baked normal-preserving fracture mesh.");
                Assert.That(filter.sharedMesh.normals,
                    Has.Length.EqualTo(filter.sharedMesh.vertexCount),
                    $"{renderer.name} must carry explicit exterior/cut normals at runtime.");
                Assert.That(filter.sharedMesh.subMeshCount, Is.EqualTo(2), renderer.name);
                Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(2),
                    $"{renderer.name} must render exterior stone plus a sandstone cut interior.");
                Assert.That(renderer.sharedMaterials[0], Is.Not.Null, renderer.name);
                Assert.That(renderer.sharedMaterials[1], Is.Not.Null, renderer.name);
            }
            Assert.That(visibleFractureMeshes, Is.GreaterThan(0));
            for (int index = 0; index < activeTargets; index++)
            {
                Assert.That(targets[index].IsEarthTargetValid, Is.True);
                Assert.That(targets[index].Body, Is.Not.Null);
                Assert.That(targets[index].Body.isKinematic, Is.False);
            }

            var floorHit = new EarthStructureImpact(
                floor.transform.position, Vector3.up, 3000f,
                EarthStructureImpactKind.Projectile, 3u);
            Assert.That(floor.ApplyEarthImpact(in floorHit), Is.False);
            Assert.That(floor.ReleasedPieceCount, Is.Zero);

            Collider vectorCollider = vectorStructure.GetComponent<Collider>();
            Assert.That(executor.TryBeginVectorField(
                vectorCollider, null, vectorCollider.bounds.center, Vector3.forward), Is.True);
            Assert.That(vectorStructure.ReleasedPieceCount, Is.EqualTo(1));
            Assert.That(executor.VectorFieldBody, Is.Not.Null);
            Assert.That(executor.VectorFieldBody.isKinematic, Is.False);
            executor.CancelVectorField();

            Collider gravityCollider = gravityStructure.GetComponent<Collider>();
            Assert.That(executor.TryBeginGravityWell(
                gravityCollider,
                gravityCollider.bounds.center + Vector3.up,
                Vector3.up,
                true), Is.True);
            executor.SetGravityStructureGesture(EarthGravityStructureIntent.Disassemble, 0.55f);
            Assert.That(gravityStructure.ReleasedPieceCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(executor.GravityWellCapturedCount, Is.GreaterThanOrEqualTo(2));
            executor.SetGravityStructureGesture(EarthGravityStructureIntent.Repair, 1f);
            Assert.That(gravityStructure.IsFractured, Is.False);
            Assert.That(gravityStructure.ReleasedPieceCount, Is.Zero);
            executor.CancelGravityWell();

            if (bot != null) bot.enabled = botWasEnabled;
            if (loadedByTest)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) yield return unload;
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            T[] all = FindAllInScene<T>(scene);
            return all.Length > 0 ? all[0] : null;
        }

        private static void AssertArenaPropsAreSeated(Scene scene, string stage)
        {
            GameObject arena = FindByName(scene, "Broken Crown Arena");
            GameObject floorObject = FindByName(scene, "Arena_FloorBase_INTACT");
            VoxelPlanetBehaviour planet = FindInScene<VoxelPlanetBehaviour>(scene);
            Assert.That(arena, Is.Not.Null);
            Assert.That(floorObject, Is.Not.Null);
            Assert.That(planet, Is.Not.Null);
            Collider floor = floorObject.GetComponent<Collider>();
            Assert.That(floor, Is.TypeOf<MeshCollider>(),
                "The visible cratered floor needs a matching static mesh collider; an AABB proxy cannot seat props.");

            string sidecarPath = Path.Combine(
                Application.dataPath,
                "Elemental/Content/Arena/BrokenCrown/BrokenCrownArena.fracture.json");
            Assert.That(File.Exists(sidecarPath), Is.True, sidecarPath);
            SemanticSidecar sidecar = JsonUtility.FromJson<SemanticSidecar>(
                File.ReadAllText(sidecarPath));
            Assert.That(sidecar?.semanticObjects, Has.Length.EqualTo(18));

            var records = new List<PropSupportRecord>(sidecar.semanticObjects.Length);
            var failures = new List<string>(8);
            for (int index = 0; index < sidecar.semanticObjects.Length; index++)
            {
                SemanticObject semantic = sidecar.semanticObjects[index];
                GameObject itemObject = FindByName(scene, semantic.name);
                var record = new PropSupportRecord
                {
                    name = semantic.name,
                    role = semantic.role,
                    supportDomain = "authored-assembly",
                    minimumGapMeters = 0f,
                    maximumGapMeters = 0f,
                    centerSupported = true,
                    accepted = itemObject != null
                };
                if (itemObject == null)
                {
                    failures.Add($"{semantic.name}: semantic object missing from scene");
                    records.Add(record);
                    continue;
                }

                Transform item = itemObject.transform;
                bool isLooseContact = semantic.role == "loose_rock" ||
                                      semantic.role == "cosmetic_rubble";
                if (semantic.name == "Arena_FloorBase_INTACT")
                {
                    record.supportDomain = "authored-planet-embed";
                    record.minimumGapMeters = MeasureMinimumRadialGap(
                        item, planet.transform.position, planet.Radius);
                    record.maximumGapMeters = record.minimumGapMeters;
                    // The approved scene intentionally embeds the broad court
                    // shell into the sphere. This removes the visible void below
                    // the platform instead of balancing it on the minimum vertex.
                    record.accepted = record.minimumGapMeters >= -0.87f &&
                                      record.minimumGapMeters <= -0.84f;
                }
                else if (isLooseContact)
                {
                    MeasureCurrentSupport(
                        item,
                        floor,
                        planet.transform.position,
                        planet.Radius,
                        out record.supportDomain,
                        out record.minimumGapMeters,
                        out record.maximumGapMeters,
                        out record.meshSupportHits,
                        out record.contactWitnessCount,
                        out record.contactPatchArea,
                        out record.centerSupported,
                        out record.accepted);
                    // Loose rocks and rubble are one rigid imported composition.
                    // Per-piece seating was the regression that lifted and rotated
                    // Arena_Rock_SouthWest_Slab. EditMode compares every child to
                    // the FBX; runtime only verifies that the approved authored
                    // transforms remain finite and anchored before an impact.
                    record.supportDomain = "authored-rigid-assembly";
                    Renderer renderer = item.GetComponent<Renderer>();
                    Rigidbody body = item.GetComponent<Rigidbody>();
                    Vector3 localPosition = item.localPosition;
                    Quaternion localRotation = item.localRotation;
                    record.accepted = renderer != null && renderer.enabled &&
                                      float.IsFinite(localPosition.x) &&
                                      float.IsFinite(localPosition.y) &&
                                      float.IsFinite(localPosition.z) &&
                                      float.IsFinite(localRotation.x) &&
                                      float.IsFinite(localRotation.y) &&
                                      float.IsFinite(localRotation.z) &&
                                      float.IsFinite(localRotation.w) &&
                                      (body == null || body.isKinematic);
                }
                else
                {
                    Renderer renderer = item.GetComponent<Renderer>();
                    Collider collider = item.GetComponent<Collider>();
                    record.accepted = renderer != null && renderer.enabled &&
                                      collider != null && collider.enabled;
                }
                records.Add(record);
                if (!record.accepted)
                    failures.Add(
                        $"{record.name} ({record.role}/{record.supportDomain}): " +
                        $"gap=[{record.minimumGapMeters:0.0000}, " +
                        $"{record.maximumGapMeters:0.0000}] m, " +
                        $"hits={record.meshSupportHits}, contacts={record.contactWitnessCount}, " +
                        $"COM={record.centerSupported}");
            }

            WritePropSupportReport(stage, records, failures);
            Assert.That(records, Has.Count.EqualTo(18));
            Assert.That(failures, Is.Empty,
                $"[{stage}] Broken Crown semantic placement failures: " +
                string.Join("; ", failures));
        }

        private static void WritePropSupportReport(
            string stage,
            List<PropSupportRecord> records,
            List<string> failures)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BuildReports"));
            Directory.CreateDirectory(directory);
            var report = new PropSupportReport
            {
                schema = "broken-crown-authored-assembly-v3",
                utc = DateTime.UtcNow.ToString("O"),
                stage = stage,
                accepted = failures.Count == 0,
                checkedPropCount = records.Count,
                problematicNames = failures.ToArray(),
                props = records.ToArray()
            };
            File.WriteAllText(
                Path.Combine(directory, "BrokenCrownPropValidationLatest.json"),
                JsonUtility.ToJson(report, true));
        }

        private static void WritePillarSupportReport(
            Dictionary<uint, PillarSupportRecord> records)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BuildReports"));
            Directory.CreateDirectory(directory);
            var values = new PillarSupportRecord[records.Count];
            int index = 0;
            bool accepted = records.Count > 0;
            foreach (KeyValuePair<uint, PillarSupportRecord> pair in records)
            {
                values[index++] = pair.Value;
                accepted &= pair.Value.accepted;
            }
            var report = new PillarSupportReport
            {
                schema = "broken-crown-pillar-support-v1",
                utc = DateTime.UtcNow.ToString("O"),
                accepted = accepted,
                checkedPillarCount = values.Length,
                pillars = values
            };
            File.WriteAllText(
                Path.Combine(directory, "BrokenCrownPillarValidationLatest.json"),
                JsonUtility.ToJson(report, true));
        }

        [Serializable]
        private sealed class PropSupportReport
        {
            public string schema;
            public string utc;
            public string stage;
            public bool accepted;
            public int checkedPropCount;
            public string[] problematicNames;
            public PropSupportRecord[] props;
        }

        [Serializable]
        private sealed class PropSupportRecord
        {
            public string name;
            public string role;
            public string supportDomain;
            public float minimumGapMeters;
            public float maximumGapMeters;
            public int meshSupportHits;
            public int contactWitnessCount;
            public float contactPatchArea;
            public bool centerSupported;
            public bool accepted;
        }

        [Serializable]
        private sealed class SemanticSidecar
        {
            public SemanticObject[] semanticObjects;
        }

        [Serializable]
        private sealed class SemanticObject
        {
            public string name;
            public string role;
        }

        [Serializable]
        private sealed class PillarSupportReport
        {
            public string schema;
            public string utc;
            public bool accepted;
            public int checkedPillarCount;
            public PillarSupportRecord[] pillars;
        }

        [Serializable]
        private sealed class PillarSupportRecord
        {
            public uint stableId;
            public bool polygonCell;
            public float minimumGapMeters;
            public float maximumGapMeters;
            public int sampleCount;
            public bool accepted;
        }

        private static float MeasureMatrixSupportGap(
            Mesh mesh,
            Matrix4x4 matrix,
            Vector3 surface,
            Vector3 up)
        {
            if (mesh == null || up.sqrMagnitude < 0.5f) return float.PositiveInfinity;
            up.Normalize();
            float minimum = float.PositiveInfinity;
            Vector3[] vertices = mesh.vertices;
            for (int index = 0; index < vertices.Length; index++)
                minimum = Mathf.Min(
                    minimum,
                    Vector3.Dot(matrix.MultiplyPoint3x4(vertices[index]) - surface, up));
            return minimum;
        }

        private static void MeasureCurrentSupport(
            Transform prop,
            Collider floor,
            Vector3 planetCenter,
            float planetRadius,
            out string supportDomain,
            out float minimumGap,
            out float maximumGap,
            out int supportHits,
            out int contactWitnessCount,
            out float contactPatchArea,
            out bool centerSupported,
            out bool accepted)
        {
            const int capacity = EarthArenaPropSeatingSolver.MaximumSupportSamples;
            Vector3[] points = new Vector3[capacity];
            Renderer renderer = prop.GetComponent<Renderer>();
            Collider collider = prop.GetComponent<Collider>();
            Bounds bounds = collider != null && collider.enabled
                ? collider.bounds
                : renderer.bounds;
            EarthArenaAuthoredSupport authoredSupport =
                prop.GetComponent<EarthArenaAuthoredSupport>();
            bool forceSphere = authoredSupport != null &&
                               authoredSupport.Domain == EarthArenaSupportDomain.PlanetSphere;
            Vector3 up = authoredSupport != null
                ? authoredSupport.SupportUp
                : (bounds.center - planetCenter).normalized;
            if (up.sqrMagnitude < 0.5f) up = Vector3.up;
            float probeLift = floor.bounds.extents.magnitude + bounds.extents.magnitude + 1f;
            if (authoredSupport == null && floor.Raycast(
                    new Ray(bounds.center + up * probeLift, -up),
                    out RaycastHit centerSurface,
                    probeLift * 2f + 1f) &&
                centerSurface.normal.sqrMagnitude > 0.5f)
            {
                Vector3 floorUp = centerSurface.normal.normalized;
                up = Vector3.Dot(floorUp, up) >= 0f ? floorUp : -floorUp;
            }
            int count = EarthArenaMeshSupportProbe.CollectSpreadLowPoints(
                prop, planetCenter, up, points);
            supportHits = 0;
            minimumGap = float.PositiveInfinity;
            maximumGap = float.NegativeInfinity;
            contactWitnessCount = 0;
            contactPatchArea = 0f;
            centerSupported = false;
            accepted = false;
            supportDomain = "unresolved";
            if (count == 0) return;

            float[] propProjections = new float[count];
            bool[] hits = new bool[count];
            float[] supportProjections = new float[count];
            var tangentHits = new Unity.Mathematics.float2[count];
            EarthArenaMeshSupportProbe.BuildTangentFrame(
                up, out Vector3 tangentX, out Vector3 tangentY);
            for (int index = 0; index < count; index++)
            {
                propProjections[index] = Vector3.Dot(points[index] - planetCenter, up);
                RaycastHit hit = default;
                hits[index] = !forceSphere && floor.Raycast(
                        new Ray(points[index] + up * probeLift, -up),
                        out hit,
                        probeLift * 2f + 1f);
                if (!hits[index]) continue;
                supportHits++;
                supportProjections[index] = Vector3.Dot(hit.point - planetCenter, up);
                tangentHits[index] = new Unity.Mathematics.float2(
                    Vector3.Dot(hit.point - planetCenter, tangentX),
                    Vector3.Dot(hit.point - planetCenter, tangentY));
            }
            supportDomain = supportHits > 0 ? "arena-floor" : "planet-sphere";
            if (supportHits == 0)
            {
                for (int index = 0; index < count; index++)
                {
                    Vector3 relative = points[index] - planetCenter;
                    float alongUp = Vector3.Dot(relative, up);
                    Vector3 perpendicular = relative - up * alongUp;
                    float heightSquared = planetRadius * planetRadius - perpendicular.sqrMagnitude;
                    hits[index] = heightSquared >= 0f;
                    if (!hits[index]) continue;
                    supportProjections[index] = Mathf.Sqrt(heightSquared);
                    Vector3 spherePoint = planetCenter + perpendicular +
                                          up * supportProjections[index];
                    tangentHits[index] = new Unity.Mathematics.float2(
                        Vector3.Dot(spherePoint - planetCenter, tangentX),
                        Vector3.Dot(spherePoint - planetCenter, tangentY));
                    supportHits++;
                }
            }
            Rigidbody body = prop.GetComponent<Rigidbody>();
            MeshFilter filter = prop.GetComponent<MeshFilter>();
            Vector3 center = body != null
                ? body.worldCenterOfMass
                : prop.TransformPoint(filter.sharedMesh.bounds.center);
            var projectedCenter = new Unity.Mathematics.float2(
                Vector3.Dot(center - planetCenter, tangentX),
                Vector3.Dot(center - planetCenter, tangentY));
            EarthArenaPropSupportDecision decision =
                EarthArenaPropSeatingSolver.ResolveSupportPatch(
                    propProjections,
                    hits,
                    supportProjections,
                    tangentHits,
                    projectedCenter,
                    count,
                    0.01f,
                    true);
            for (int index = 0; index < count; index++)
            {
                if (!hits[index]) continue;
                float gap = propProjections[index] - supportProjections[index];
                minimumGap = Mathf.Min(minimumGap, gap);
                maximumGap = Mathf.Max(maximumGap, gap);
            }
            contactWitnessCount = decision.ContactWitnessCount;
            contactPatchArea = decision.ContactPatchArea;
            centerSupported = decision.CenterSupported;
            accepted = decision.Accepted &&
                       Mathf.Abs(decision.ShiftAlongUp) <= 0.0015f &&
                       minimumGap >= -0.0105f && minimumGap <= 0.015f;
        }

        private static float MeasureMinimumRadialGap(
            Transform item,
            Vector3 planetCenter,
            float planetRadius)
        {
            MeshFilter filter = item.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return float.PositiveInfinity;
            float minimum = float.PositiveInfinity;
            Vector3[] vertices = mesh.vertices;
            for (int index = 0; index < vertices.Length; index++)
                minimum = Mathf.Min(
                    minimum,
                    Vector3.Distance(item.TransformPoint(vertices[index]), planetCenter) - planetRadius);
            return minimum;
        }

        private static GameObject FindByName(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Transform[] transforms = roots[index].GetComponentsInChildren<Transform>(true);
                for (int childIndex = 0; childIndex < transforms.Length; childIndex++)
                    if (transforms[childIndex].name == name)
                        return transforms[childIndex].gameObject;
            }
            return null;
        }

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            var output = new System.Collections.Generic.List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
                output.AddRange(roots[index].GetComponentsInChildren<T>(true));
            return output.ToArray();
        }
    }
}
