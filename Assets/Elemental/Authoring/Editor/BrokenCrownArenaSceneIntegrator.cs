using System;
using System.Collections.Generic;
using Elemental.Authoring.Fracture;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Geometry;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Elemental.Simulation.Structures;

namespace Elemental.Authoring.Editor
{
    public static class BrokenCrownArenaSceneIntegrator
    {
        private const int CameraPassthroughLayer = 29;
        public const string CatalogPath =
            "Assets/Elemental/Content/Arena/BrokenCrown/Generated/BrokenCrownArenaCatalog.asset";
        private const string SandstoneMaterialPath =
            "Assets/Elemental/Content/GraphicsV5/Materials/RumbleSandstone.mat";
        private const string ArenaSandstoneMaterialPath =
            "Assets/Elemental/Content/GraphicsV5/Materials/RumbleArenaSandstone.mat";
        private const string FractureInteriorMaterialPath =
            "Assets/Elemental/Content/GraphicsV5/Materials/RumbleSandstoneFractureInterior.mat";
        private const string DustMaterialPath =
            "Assets/Elemental/Content/Materials/LightDustMote.mat";
        // Captured from the user-approved EarthCoreSlice placement. The authored
        // court is intentionally embedded into the planet so its broad underside
        // cannot read as a platform balanced on one small pedestal.
        private const float AuthoredArenaRootEmbed = 0.98f;

        [MenuItem("Elemental/Arena/Integrate Broken Crown Into Current Scene")]
        public static void IntegrateCurrentScene()
        {
            if (Application.isPlaying)
                throw new BuildFailedException("Stop Play Mode before integrating Broken Crown.");

            VoxelPlanetBehaviour planet = UnityEngine.Object.FindAnyObjectByType<VoxelPlanetBehaviour>();
            GravityWorldBehaviour gravity = UnityEngine.Object.FindAnyObjectByType<GravityWorldBehaviour>();
            EarthRockDebrisPool debris = UnityEngine.Object.FindAnyObjectByType<EarthRockDebrisPool>();
            Material sandstone = AssetDatabase.LoadAssetAtPath<Material>(SandstoneMaterialPath);
            if (planet == null || gravity == null || debris == null || sandstone == null)
                throw new BuildFailedException(
                    "Broken Crown integration requires the M3 planet, gravity, debris pool and RumbleSandstone material.");

            Integrate(planet.transform.position, planet.Radius, gravity, debris, sandstone);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        public static GameObject Integrate(
            Vector3 planetCenter,
            float planetRadius,
            GravityWorldBehaviour gravityWorld,
            EarthRockDebrisPool debrisPool,
            Material rockMaterial)
        {
            GameObject oldCourt = GameObject.Find("Rumble Stone Amphitheatre");
            if (oldCourt != null) UnityEngine.Object.DestroyImmediate(oldCourt);
            GameObject oldArena = GameObject.Find("Broken Crown Arena");
            if (oldArena != null) UnityEngine.Object.DestroyImmediate(oldArena);

            EarthArenaFractureCatalog catalog =
                AssetDatabase.LoadAssetAtPath<EarthArenaFractureCatalog>(CatalogPath);
            if (catalog == null || catalog.ImportedModel == null)
                throw new BuildFailedException(
                    "Broken Crown catalog/model is missing. Rebuild the import first.");
            if (rockMaterial == null)
                throw new BuildFailedException("Broken Crown requires the shared rock material.");
            Material arenaRockMaterial = GetOrCreateArenaExteriorMaterial(
                rockMaterial,
                planetCenter);
            Material fractureInteriorMaterial = GetOrCreateFractureInteriorMaterial(arenaRockMaterial);
            EarthSurfaceQueryService surfaceQueries =
                UnityEngine.Object.FindAnyObjectByType<EarthSurfaceQueryService>();

            GameObject root = PrefabUtility.InstantiatePrefab(catalog.ImportedModel) as GameObject;
            if (root == null)
                throw new BuildFailedException("Unable to instantiate the Broken Crown FBX.");
            root.name = "Broken Crown Arena";
            SetLayerRecursively(root.transform, CameraPassthroughLayer);
            root.transform.SetPositionAndRotation(
                planetCenter + Vector3.up * planetRadius,
                Quaternion.FromToRotation(Vector3.forward, Vector3.up) *
                Quaternion.Euler(0f, 0f, 180f));
            root.transform.localScale = Vector3.one;

            var intactNames = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < catalog.Structures.Length; index++)
                intactNames.Add(catalog.Structures[index].intactObjectName);
            var looseNames = new HashSet<string>(
                catalog.LooseRockObjectNames, StringComparer.Ordinal);
            var rubbleNames = new HashSet<string>(
                catalog.CosmeticRubbleObjectNames, StringComparer.Ordinal);

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform item = transforms[index];
                if (item == root.transform) continue;
                if (item.name.StartsWith("FR_", StringComparison.Ordinal) &&
                    item.name.EndsWith("_ROOT", StringComparison.Ordinal))
                {
                    item.gameObject.SetActive(false);
                    continue;
                }

                MeshFilter filter = item.GetComponent<MeshFilter>();
                Renderer renderer = item.GetComponent<Renderer>();
                if (item.name.StartsWith("COL_FR_", StringComparison.Ordinal))
                {
                    if (renderer != null) renderer.enabled = false;
                    item.gameObject.SetActive(false);
                    continue;
                }
                if (item.name.StartsWith("BOND_", StringComparison.Ordinal))
                {
                    item.gameObject.SetActive(false);
                    continue;
                }
                if (renderer != null)
                {
                    // Only actual fracture pieces expose sandstone interiors.
                    // Repaired holes in the intact source remain continuous exterior
                    // stone, so they never receive a visibly different patch material.
                    renderer.sharedMaterials = item.name.StartsWith(
                                                   "FR_", StringComparison.Ordinal) &&
                                               filter != null &&
                                               filter.sharedMesh != null &&
                                               filter.sharedMesh.subMeshCount > 1
                        ? new[] { arenaRockMaterial, fractureInteriorMaterial }
                        : new[] { arenaRockMaterial };
                    ConfigureArenaRenderer(renderer);
                }
                if (filter == null || filter.sharedMesh == null) continue;

                if (item.name.StartsWith("FR_", StringComparison.Ordinal))
                {
                    item.gameObject.SetActive(false);
                    continue;
                }

                bool isIntact = intactNames.Contains(item.name) ||
                                item.name.EndsWith("_STATIC", StringComparison.Ordinal);
                bool isLoose = looseNames.Contains(item.name);
                if (isIntact)
                    ConfigureStaticCollider(item.gameObject, filter.sharedMesh);
                else if (isLoose)
                    ConfigureLooseRock(
                        item.gameObject,
                        filter.sharedMesh,
                        gravityWorld,
                        debrisPool,
                        index);
                else if (rubbleNames.Contains(item.name))
                {
                    // Cosmetic rubble remains render-only so it cannot snag movement.
                }
            }

            ConfigureFractureStructures(
                root,
                catalog,
                gravityWorld,
                arenaRockMaterial,
                fractureInteriorMaterial,
                surfaceQueries,
                (root.transform.position - planetCenter).normalized);
            // Fracture piece renderers are materialized by the structure pass,
            // after the imported hierarchy loop above. Apply the same shadow-free
            // contract to intact, dormant and later-released geometry.
            Renderer[] finalArenaRenderers = root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < finalArenaRenderers.Length; index++)
                ConfigureArenaRenderer(finalArenaRenderers[index]);
            ConfigureFractureDust(root);

            // Component setup (most notably nested rigidbodies and fracture proxies)
            // must never become an authoring pass. Reapply the imported FBX's local
            // transform graph as one atomic assembly before seating the root. This
            // removes all historical per-piece Y offsets and makes any regression a
            // hard setup failure instead of a new gap in the arena.
            RestoreAuthoredAssembly(root, catalog.ImportedModel);
            ValidateAuthoredAssembly(root, catalog.ImportedModel);
            NormalizePlanetCollisionProxy(planetCenter, planetRadius);
            UnityEngine.Physics.SyncTransforms();
            ApplyAuthoredArenaRootPlacement(root, planetCenter, planetRadius);
            UnityEngine.Physics.SyncTransforms();
            // Preserve the imported Broken Crown assembly as a rigid authored
            // composition. Per-prop seating rotated and translated loose slabs,
            // including Arena_Rock_SouthWest_Slab, away from their source pose.
            ValidateAuthoredAssembly(root, catalog.ImportedModel);
            NormalizePlanetMotors(planetCenter, planetRadius);
            UnityEngine.Physics.SyncTransforms();
            ResolveMotorArenaPenetration(root);
            SeatPlayerSpawnAboveArenaFloor(root, planetCenter);
            UnityEngine.Physics.SyncTransforms();

            return root;
        }

        private static void RestoreAuthoredAssembly(GameObject root, GameObject authoredModel)
        {
            if (root == null || authoredModel == null)
                throw new BuildFailedException("Broken Crown authored assembly source is missing.");

            Transform authoredRoot = authoredModel.transform;
            Transform[] authored = authoredModel.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < authored.Length; index++)
            {
                Transform source = authored[index];
                if (source == authoredRoot) continue;
                string path = AnimationUtility.CalculateTransformPath(source, authoredRoot);
                Transform target = root.transform.Find(path);
                if (target == null)
                    throw new BuildFailedException(
                        $"Broken Crown instance is missing authored transform '{path}'.");

                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                EditorUtility.SetDirty(target);
            }
        }

        private static void ValidateAuthoredAssembly(GameObject root, GameObject authoredModel)
        {
            Transform authoredRoot = authoredModel.transform;
            Transform[] authored = authoredModel.GetComponentsInChildren<Transform>(true);
            int differences = 0;
            float maximumPositionDelta = 0f;
            string firstDifference = null;
            for (int index = 0; index < authored.Length; index++)
            {
                Transform source = authored[index];
                if (source == authoredRoot) continue;
                string path = AnimationUtility.CalculateTransformPath(source, authoredRoot);
                Transform target = root.transform.Find(path);
                if (target == null)
                    throw new BuildFailedException(
                        $"Broken Crown instance lost authored transform '{path}'.");

                float positionDelta = Vector3.Distance(
                    target.localPosition,
                    source.localPosition);
                float rotationDelta = Quaternion.Angle(
                    target.localRotation,
                    source.localRotation);
                float scaleDelta = Vector3.Distance(
                    target.localScale,
                    source.localScale);
                if (positionDelta <= 0.0001f && rotationDelta <= 0.01f &&
                    scaleDelta <= 0.0001f) continue;

                differences++;
                maximumPositionDelta = Mathf.Max(maximumPositionDelta, positionDelta);
                if (firstDifference == null)
                    firstDifference =
                        $"{path} (position {positionDelta:F5} m, rotation " +
                        $"{rotationDelta:F3} deg, scale {scaleDelta:F5})";
            }

            if (differences > 0)
                throw new BuildFailedException(
                    $"Broken Crown authored assembly drifted on {differences} transforms; " +
                    $"max position delta {maximumPositionDelta:F5} m; first: {firstDifference}.");
        }

        private static void ConfigureFractureDust(GameObject root)
        {
            GameObject dustObject = new GameObject("Arena Fracture Dust");
            dustObject.layer = CameraPassthroughLayer;
            dustObject.transform.SetParent(root.transform, false);
            dustObject.AddComponent<ParticleSystem>();
            EarthArenaFractureDustPresenter presenter =
                dustObject.AddComponent<EarthArenaFractureDustPresenter>();
            presenter.Configure(M3EarthCoreSetup.CreateOrLoadEffectsProfile());
        }

        private static void ConfigureArenaRenderer(Renderer renderer)
        {
            if (renderer == null) return;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
        }

        private static Material GetOrCreateArenaExteriorMaterial(
            Material source,
            Vector3 planetCenter)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ArenaSandstoneMaterialPath);
            bool createAsset = material == null;
            if (createAsset) material = new Material(source) { name = "RumbleArenaSandstone" };
            else
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }
            // Broken Crown already carries smooth authored normals and sharp rim
            // metadata. The generic rock material's radial side-normal synthesis
            // and macro contrast over-describe large temple walls as stripes/noise.
            if (material.HasProperty("_SideShadingSmoothness"))
                material.SetFloat("_SideShadingSmoothness", 0f);
            if (material.HasProperty("_FacetContrast")) material.SetFloat("_FacetContrast", 0.16f);
            if (material.HasProperty("_MacroStrength")) material.SetFloat("_MacroStrength", 0.012f);
            // The imported arena's red vertex channel groups entire masonry
            // courses. On hero rocks that is useful macro variation; on the arena
            // it becomes a repeating horizontal albedo stripe, so authored mesh
            // normals remain active while the coarse face-tone channel is ignored.
            if (material.HasProperty("_VertexFaceTone")) material.SetFloat("_VertexFaceTone", 0f);
            if (material.HasProperty("_MacroScale")) material.SetFloat("_MacroScale", 5f);
            if (material.HasProperty("_BevelLight")) material.SetFloat("_BevelLight", 0.20f);
            if (material.HasProperty("_AmbientStrength")) material.SetFloat("_AmbientStrength", 0.76f);
            if (material.HasProperty("_Roughness")) material.SetFloat("_Roughness", 0.92f);
            if (material.HasProperty("_TextureStrength")) material.SetFloat("_TextureStrength", 0.025f);
            // Architectural side/recess polygons use stable analytic form depth.
            // This is a fail-safe against travelling cascade bands if a quality
            // asset or preview light accidentally enables realtime shadows again.
            if (material.HasProperty("_SideShadowFade")) material.SetFloat("_SideShadowFade", 1f);
            if (material.HasProperty("_StableSideFormOcclusion"))
                material.SetFloat("_StableSideFormOcclusion", 0.075f);
            if (material.HasProperty("_FractureInteriorDepth"))
                material.SetFloat("_FractureInteriorDepth", 0.16f);
            // The stable receiver classification must use the exact same planet
            // frame in Scene view, edit-mode captures and runtime. A serialized
            // per-arena value is deterministic across domain reloads; it does not
            // depend on the runtime celestial global having run first.
            if (material.HasProperty("_ReceiverPlanetCenter"))
                material.SetVector("_ReceiverPlanetCenter", planetCenter);
            material.enableInstancing = true;
            if (createAsset) AssetDatabase.CreateAsset(material, ArenaSandstoneMaterialPath);
            else EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static Material GetOrCreateFractureInteriorMaterial(Material rockMaterial)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                FractureInteriorMaterialPath);
            if (material == null)
            {
                material = new Material(rockMaterial) { name = "RumbleSandstoneFractureInterior" };
                AssetDatabase.CreateAsset(material, FractureInteriorMaterialPath);
            }
            else
            {
                material.shader = rockMaterial.shader;
                material.CopyPropertiesFromMaterial(rockMaterial);
            }

            Color authoredCut = rockMaterial.HasProperty("_FractureColor")
                ? rockMaterial.GetColor("_FractureColor")
                : new Color(0.62f, 0.48f, 0.36f, 1f);
            Color shadow = rockMaterial.HasProperty("_ShadowColor")
                ? rockMaterial.GetColor("_ShadowColor")
                : new Color(0.20f, 0.15f, 0.12f, 1f);
            // A new break stays in the authored sandstone palette but sits below
            // the weathered exterior in value, making the fracture readable even
            // when dust temporarily covers the topology change.
            Color cut = Color.Lerp(authoredCut, shadow, 0.36f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", cut);
            if (material.HasProperty("_EdgeColor"))
                material.SetColor("_EdgeColor", Color.Lerp(cut, Color.white, 0.12f));
            if (material.HasProperty("_ShadowColor"))
                material.SetColor("_ShadowColor", Color.Lerp(cut, Color.black, 0.58f));
            if (material.HasProperty("_FractureColor")) material.SetColor("_FractureColor", cut);
            // Fracture interiors keep the same broad form response as the intact
            // arena; their darker authored palette alone identifies a fresh cut.
            if (material.HasProperty("_FacetContrast")) material.SetFloat("_FacetContrast", 0.16f);
            if (material.HasProperty("_FractureInteriorDepth"))
                material.SetFloat("_FractureInteriorDepth", 0.16f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static void NormalizePlanetCollisionProxy(
            Vector3 planetCenter,
            float planetRadius)
        {
            GameObject proxy = GameObject.Find("Planet Collision Proxy");
            SphereCollider sphere = proxy != null ? proxy.GetComponent<SphereCollider>() : null;
            if (sphere == null) return;
            Vector3 scale = sphere.transform.lossyScale;
            float currentWorldRadius = sphere.radius * Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            if (currentWorldRadius <= 0.001f) return;
            sphere.transform.position = planetCenter;
            sphere.transform.localScale *= planetRadius / currentWorldRadius;
        }

        private static void ApplyAuthoredArenaRootPlacement(
            GameObject root,
            Vector3 planetCenter,
            float planetRadius)
        {
            Vector3 radial = root.transform.position - planetCenter;
            Vector3 up = radial.sqrMagnitude > 0.001f ? radial.normalized : Vector3.up;
            root.transform.position =
                planetCenter + up * (planetRadius - AuthoredArenaRootEmbed);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
            EditorUtility.SetDirty(root.transform);
        }

        private static void NormalizePlanetMotors(Vector3 planetCenter, float planetRadius)
        {
            PlanetMotor[] motors = UnityEngine.Object.FindObjectsByType<PlanetMotor>(
                FindObjectsInactive.Include);
            for (int index = 0; index < motors.Length; index++)
            {
                PlanetMotor motor = motors[index];
                Vector3 radial = motor.transform.position - planetCenter;
                Vector3 up = radial.sqrMagnitude > 0.001f ? radial.normalized : Vector3.up;
                float minimumHeight = MinimumColliderProjection(
                    motor.GetComponentsInChildren<Collider>(true),
                    planetCenter,
                    up);
                if (!float.IsFinite(minimumHeight)) continue;
                MoveMotor(motor, up * ((planetRadius + 0.04f) - minimumHeight));
            }
        }

        private static void SeatArenaPropsOnFloor(
            GameObject arenaRoot,
            EarthArenaFractureCatalog catalog,
            Vector3 planetCenter,
            float planetRadius)
        {
            Transform[] all = arenaRoot.GetComponentsInChildren<Transform>(true);
            Transform floorTransform = FindNamed(all, "Arena_FloorBase_INTACT");
            Collider floor = floorTransform != null
                ? floorTransform.GetComponent<Collider>()
                : null;
            if (floor == null || !floor.enabled) return;

            Vector3 radial = floor.bounds.center - planetCenter;
            Vector3 up = radial.sqrMagnitude > 0.001f ? radial.normalized : Vector3.up;
            UnityEngine.Physics.SyncTransforms();

            for (int index = 0; index < catalog.LooseRockObjectNames.Length; index++)
                SeatPropOnFloor(
                    all, catalog.LooseRockObjectNames[index], planetCenter, planetRadius, floor);
            for (int index = 0; index < catalog.CosmeticRubbleObjectNames.Length; index++)
                SeatPropOnFloor(
                    all, catalog.CosmeticRubbleObjectNames[index], planetCenter, planetRadius, floor);
        }

        private static void SeatPropOnFloor(
            Transform[] all,
            string name,
            Vector3 planetCenter,
            float planetRadius,
            Collider floor,
            bool allowStableOrientation = true,
            bool forceSphereSupport = false,
            bool hasLockedSupportUp = false,
            Vector3 lockedSupportUp = default,
            bool validateOnly = false)
        {
            Transform item = FindNamed(all, name);
            if (item == null) return;
            Collider collider = item.GetComponent<Collider>();
            Bounds itemBounds = collider != null && collider.enabled
                ? collider.bounds
                : ComponentBounds(item);
            Vector3 itemRadial = itemBounds.center - planetCenter;
            Vector3 up = hasLockedSupportUp && lockedSupportUp.sqrMagnitude > 0.5f
                ? lockedSupportUp.normalized
                : itemRadial.sqrMagnitude > 0.001f
                    ? itemRadial.normalized
                    : Vector3.up;
            float probeLift = floor.bounds.extents.magnitude +
                              itemBounds.extents.magnitude + 1f;
            // The arena is cratered. A radial probe locates the local authored
            // support, then all contact rays and any stability reorientation use
            // that triangle's real normal rather than pretending the floor is a
            // tangent plane to the planet.
            if (!hasLockedSupportUp && !forceSphereSupport && floor.Raycast(
                    new Ray(itemBounds.center + up * probeLift, -up),
                    out RaycastHit centerSurface,
                    probeLift * 2f + 1f) &&
                centerSurface.normal.sqrMagnitude > 0.5f)
            {
                Vector3 floorUp = centerSurface.normal.normalized;
                up = Vector3.Dot(floorUp, up) >= 0f ? floorUp : -floorUp;
            }
            // Probe the actual visible underside rather than one AABB witness.
            // Rotated wedges and irregular boulders can have a correct bounds
            // minimum while every rendered vertex still floats above the floor.
            const int supportCapacity = EarthArenaPropSeatingSolver.MaximumSupportSamples;
            Vector3[] supportPoints = new Vector3[supportCapacity];
            int supportCount = EarthArenaMeshSupportProbe.CollectSpreadLowPoints(
                item,
                planetCenter,
                up,
                supportPoints);
            if (supportCount > 0)
            {
                float[] propProjections = new float[supportCount];
                bool[] floorHits = new bool[supportCount];
                float[] floorProjections = new float[supportCount];
                Unity.Mathematics.float2[] tangentHits =
                    new Unity.Mathematics.float2[supportCount];
                EarthArenaMeshSupportProbe.BuildTangentFrame(
                    up,
                    out Vector3 tangentX,
                    out Vector3 tangentY);
                for (int index = 0; index < supportCount; index++)
                {
                    Vector3 point = supportPoints[index];
                    propProjections[index] = Vector3.Dot(point - planetCenter, up);
                    RaycastHit floorHit = default;
                    floorHits[index] = !forceSphereSupport && floor.Raycast(
                        new Ray(point + up * probeLift, -up),
                        out floorHit,
                        probeLift * 2f + 1f);
                    if (floorHits[index])
                    {
                        floorProjections[index] = Vector3.Dot(
                            floorHit.point - planetCenter,
                            up);
                        tangentHits[index] = new Unity.Mathematics.float2(
                            Vector3.Dot(floorHit.point - planetCenter, tangentX),
                            Vector3.Dot(floorHit.point - planetCenter, tangentY));
                    }
                }

                Rigidbody body = item.GetComponent<Rigidbody>();
                Vector3 centerOfMass = body != null
                    ? body.worldCenterOfMass
                    : item.TransformPoint(item.GetComponent<MeshFilter>().sharedMesh.bounds.center);
                var projectedCenter = new Unity.Mathematics.float2(
                    Vector3.Dot(centerOfMass - planetCenter, tangentX),
                    Vector3.Dot(centerOfMass - planetCenter, tangentY));

                EarthArenaPropSupportDecision supportDecision =
                    EarthArenaPropSeatingSolver.ResolveSupportPatch(
                        propProjections,
                        floorHits,
                        floorProjections,
                        tangentHits,
                        projectedCenter,
                        supportCount,
                        0.01f,
                        true);
                if (supportDecision.UsesArenaFloor)
                {
                    if (!supportDecision.Accepted)
                    {
                        if (allowStableOrientation && TryAlignStableSupportFace(item, up))
                        {
                            UnityEngine.Physics.SyncTransforms();
                            SeatPropOnFloor(
                                all, name, planetCenter, planetRadius, floor,
                                false, false, true, up);
                            return;
                        }
                        if (!forceSphereSupport &&
                            supportDecision.ValidSampleCount < supportCount &&
                            !supportDecision.CenterSupported)
                        {
                            // A rock straddling the authored floor boundary is an
                            // exterior prop. Missing floor witnesses may not be
                            // invented; validate its complete patch against the
                            // underlying planet instead.
                            SeatPropOnFloor(
                                all, name, planetCenter, planetRadius, floor,
                                false, true, false, default);
                            return;
                        }
                        throw new BuildFailedException(
                            $"Arena prop '{name}' failed floor support: gap " +
                            $"[{supportDecision.MinimumGapAfterShift:F4}, " +
                            $"{supportDecision.MaximumGapAfterShift:F4}] m, " +
                            $"contacts={supportDecision.ContactWitnessCount}, " +
                            $"patch={supportDecision.ContactPatchArea:F5} m2, " +
                            $"COM={supportDecision.CenterSupported}.");
                    }
                    if (validateOnly && Mathf.Abs(supportDecision.ShiftAlongUp) > 0.0015f)
                        throw new BuildFailedException(
                            $"Arena prop '{name}' floor seat did not persist; " +
                            $"residual shift={supportDecision.ShiftAlongUp:F4} m.");
                    ApplyAuthoredShift(item, up * supportDecision.ShiftAlongUp);
                    if (!validateOnly)
                        ConfigureAuthoredSupport(
                            item, EarthArenaSupportDomain.ArenaFloor, up);
                    UnityEngine.Physics.SyncTransforms();
                    if (!validateOnly)
                        SeatPropOnFloor(
                            all, name, planetCenter, planetRadius, floor,
                            false, false, true, up, true);
                    return;
                }
            }

            Vector3 centerProbeOrigin = itemBounds.center + up * probeLift;
            bool centerOverFloor = !forceSphereSupport && floor.Raycast(
                new Ray(centerProbeOrigin, -up),
                out _,
                probeLift * 2f + 1f);
            if (centerOverFloor)
                throw new BuildFailedException(
                    $"Arena prop '{name}' is inside the floor footprint but has no visible mesh support patch.");

            if (supportCount > 0)
            {
                float[] propProjections = new float[supportCount];
                bool[] sphereHits = new bool[supportCount];
                float[] sphereProjections = new float[supportCount];
                Unity.Mathematics.float2[] tangentHits =
                    new Unity.Mathematics.float2[supportCount];
                EarthArenaMeshSupportProbe.BuildTangentFrame(
                    up,
                    out Vector3 tangentX,
                    out Vector3 tangentY);
                for (int index = 0; index < supportCount; index++)
                {
                    Vector3 relative = supportPoints[index] - planetCenter;
                    float alongUp = Vector3.Dot(relative, up);
                    Vector3 perpendicular = relative - up * alongUp;
                    float heightSquared = planetRadius * planetRadius -
                                          perpendicular.sqrMagnitude;
                    propProjections[index] = alongUp;
                    sphereHits[index] = heightSquared >= 0f;
                    if (!sphereHits[index]) continue;
                    float targetAlongUp = Mathf.Sqrt(heightSquared);
                    Vector3 spherePoint = planetCenter + perpendicular + up * targetAlongUp;
                    sphereProjections[index] = targetAlongUp;
                    tangentHits[index] = new Unity.Mathematics.float2(
                        Vector3.Dot(spherePoint - planetCenter, tangentX),
                        Vector3.Dot(spherePoint - planetCenter, tangentY));
                }
                Rigidbody body = item.GetComponent<Rigidbody>();
                Vector3 centerOfMass = body != null
                    ? body.worldCenterOfMass
                    : item.TransformPoint(item.GetComponent<MeshFilter>().sharedMesh.bounds.center);
                var projectedCenter = new Unity.Mathematics.float2(
                    Vector3.Dot(centerOfMass - planetCenter, tangentX),
                    Vector3.Dot(centerOfMass - planetCenter, tangentY));
                EarthArenaPropSupportDecision sphereDecision =
                    EarthArenaPropSeatingSolver.ResolveSupportPatch(
                        propProjections,
                        sphereHits,
                        sphereProjections,
                        tangentHits,
                        projectedCenter,
                        supportCount,
                        0.01f,
                        true);
                if (!sphereDecision.UsesArenaFloor || !sphereDecision.Accepted)
                {
                    if (allowStableOrientation && TryAlignStableSupportFace(item, up))
                    {
                        UnityEngine.Physics.SyncTransforms();
                        SeatPropOnFloor(
                            all, name, planetCenter, planetRadius, floor,
                            false, true, true, up);
                        return;
                    }
                    throw new BuildFailedException(
                        $"Exterior arena prop '{name}' failed sphere support: gap " +
                        $"[{sphereDecision.MinimumGapAfterShift:F4}, " +
                        $"{sphereDecision.MaximumGapAfterShift:F4}] m, " +
                        $"contacts={sphereDecision.ContactWitnessCount}, " +
                        $"COM={sphereDecision.CenterSupported}.");
                }
                if (validateOnly && Mathf.Abs(sphereDecision.ShiftAlongUp) > 0.0015f)
                    throw new BuildFailedException(
                        $"Exterior arena prop '{name}' sphere seat did not persist; " +
                        $"residual shift={sphereDecision.ShiftAlongUp:F4} m.");
                ApplyAuthoredShift(item, up * sphereDecision.ShiftAlongUp);
                if (!validateOnly)
                    ConfigureAuthoredSupport(
                        item, EarthArenaSupportDomain.PlanetSphere, up);
                UnityEngine.Physics.SyncTransforms();
                if (!validateOnly)
                    SeatPropOnFloor(
                        all, name, planetCenter, planetRadius, floor,
                        false, true, true, up, true);
                return;
            }

            throw new BuildFailedException(
                $"Arena prop '{name}' has no readable mesh vertices for support validation.");
        }

        private static bool TryAlignStableSupportFace(Transform item, Vector3 up)
        {
            MeshFilter filter = item != null ? item.GetComponent<MeshFilter>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.vertexCount < 3) return false;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Rigidbody body = item.GetComponent<Rigidbody>();
            Vector3 center = body != null
                ? body.worldCenterOfMass
                : item.TransformPoint(mesh.bounds.center);
            float bestArea = 0f;
            Vector3 bestOutward = Vector3.zero;
            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                Vector3 a = item.TransformPoint(vertices[triangles[index]]);
                Vector3 b = item.TransformPoint(vertices[triangles[index + 1]]);
                Vector3 c = item.TransformPoint(vertices[triangles[index + 2]]);
                Vector3 cross = Vector3.Cross(b - a, c - a);
                float twiceArea = cross.magnitude;
                if (twiceArea <= 0.0001f) continue;
                Vector3 normal = cross / twiceArea;
                Vector3 centroid = (a + b + c) / 3f;
                Vector3 outward = Vector3.Dot(normal, center - centroid) > 0f
                    ? -normal
                    : normal;
                bool isHullSupport = true;
                for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                {
                    Vector3 candidate = item.TransformPoint(vertices[vertexIndex]);
                    if (Vector3.Dot(candidate - a, outward) <= 0.002f) continue;
                    isHullSupport = false;
                    break;
                }
                if (!isHullSupport) continue;
                Vector3 candidateUp = -outward;
                var supportPoints = new Vector3[EarthArenaPropSeatingSolver.MaximumSupportSamples];
                int supportCount = EarthArenaMeshSupportProbe.CollectSpreadLowPoints(
                    item,
                    Vector3.zero,
                    candidateUp,
                    supportPoints);
                if (supportCount < 3) continue;
                float[] propProjections = new float[supportCount];
                bool[] planeHits = new bool[supportCount];
                float[] planeProjections = new float[supportCount];
                var tangentPoints = new Unity.Mathematics.float2[supportCount];
                float supportPlane = float.PositiveInfinity;
                for (int pointIndex = 0; pointIndex < supportCount; pointIndex++)
                {
                    propProjections[pointIndex] = Vector3.Dot(
                        supportPoints[pointIndex], candidateUp);
                    supportPlane = Mathf.Min(supportPlane, propProjections[pointIndex]);
                }
                EarthArenaMeshSupportProbe.BuildTangentFrame(
                    candidateUp,
                    out Vector3 tangentX,
                    out Vector3 tangentY);
                for (int pointIndex = 0; pointIndex < supportCount; pointIndex++)
                {
                    planeHits[pointIndex] = true;
                    planeProjections[pointIndex] = supportPlane;
                    tangentPoints[pointIndex] = new Unity.Mathematics.float2(
                        Vector3.Dot(supportPoints[pointIndex], tangentX),
                        Vector3.Dot(supportPoints[pointIndex], tangentY));
                }
                var projectedCenter = new Unity.Mathematics.float2(
                    Vector3.Dot(center, tangentX),
                    Vector3.Dot(center, tangentY));
                EarthArenaPropSupportDecision stability =
                    EarthArenaPropSeatingSolver.ResolveSupportPatch(
                        propProjections,
                        planeHits,
                        planeProjections,
                        tangentPoints,
                        projectedCenter,
                        supportCount,
                        0.01f,
                        true);
                if (!stability.Accepted) continue;
                float stabilityScore = stability.ContactPatchArea + twiceArea * 0.01f;
                if (stabilityScore <= bestArea) continue;
                bestArea = stabilityScore;
                bestOutward = outward;
            }
            if (bestArea <= 0f || bestOutward.sqrMagnitude < 0.5f) return false;
            up = up.sqrMagnitude > 0.5f ? up.normalized : Vector3.up;
            Vector3 localCenter = item.InverseTransformPoint(center);
            item.rotation = Quaternion.FromToRotation(bestOutward, -up) * item.rotation;
            // Rotate loose geology around its physical centre of mass, not its
            // arbitrary imported pivot. Pivot rotation used to slide a rock onto
            // another floor triangle, invalidating the support plane selected one
            // line earlier.
            item.position += center - item.TransformPoint(localCenter);
            if (body != null)
            {
                body.position = item.position;
                body.rotation = item.rotation;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            EditorUtility.SetDirty(item);
            return true;
        }

        private static void ApplyAuthoredShift(Transform item, Vector3 shift)
        {
            if (item == null ||
                !float.IsFinite(shift.x) ||
                !float.IsFinite(shift.y) ||
                !float.IsFinite(shift.z)) return;
            item.position += shift;
            Rigidbody body = item.GetComponent<Rigidbody>();
            if (body != null) body.position = item.position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            EditorUtility.SetDirty(item);
        }

        private static void ConfigureAuthoredSupport(
            Transform item,
            EarthArenaSupportDomain domain,
            Vector3 supportUp)
        {
            if (item == null) return;
            EarthArenaAuthoredSupport support =
                item.GetComponent<EarthArenaAuthoredSupport>();
            if (support == null)
                support = item.gameObject.AddComponent<EarthArenaAuthoredSupport>();
            support.Configure(domain, supportUp);
            EditorUtility.SetDirty(support);
        }

        private static void NormalizeDestructibleDecorRocks(
            Vector3 planetCenter,
            float planetRadius)
        {
            const float seatingDepth = 0.06f;
            EarthDestructibleDecorRock[] rocks =
                UnityEngine.Object.FindObjectsByType<EarthDestructibleDecorRock>(
                    FindObjectsInactive.Include);
            for (int index = 0; index < rocks.Length; index++)
            {
                EarthDestructibleDecorRock rock = rocks[index];
                Collider collider = rock != null ? rock.GetComponent<Collider>() : null;
                if (collider == null || !collider.enabled) continue;
                Vector3 radial = collider.bounds.center - planetCenter;
                Vector3 up = radial.sqrMagnitude > 0.001f ? radial.normalized : Vector3.up;
                float minimumHeight = MinimumColliderProjection(collider, planetCenter, up);
                rock.transform.position += up *
                    ((planetRadius - seatingDepth) - minimumHeight);
            }
        }

        private static void ResolveMotorArenaPenetration(GameObject arenaRoot)
        {
            const float surfaceClearance = 0.04f;
            EarthArenaStructure[] structures =
                arenaRoot.GetComponentsInChildren<EarthArenaStructure>(true);
            Collider[] arenaColliders = new Collider[structures.Length];
            for (int index = 0; index < structures.Length; index++)
                arenaColliders[index] = structures[index].GetComponent<Collider>();

            PlanetMotor[] motors = UnityEngine.Object.FindObjectsByType<PlanetMotor>(
                FindObjectsInactive.Include);
            for (int motorIndex = 0; motorIndex < motors.Length; motorIndex++)
            {
                PlanetMotor motor = motors[motorIndex];
                Collider[] motorColliders = motor.GetComponentsInChildren<Collider>(true);
                for (int pass = 0; pass < 8; pass++)
                {
                    UnityEngine.Physics.SyncTransforms();
                    float deepest = 0f;
                    Vector3 separation = Vector3.zero;
                    for (int bodyIndex = 0; bodyIndex < motorColliders.Length; bodyIndex++)
                    {
                        Collider bodyCollider = motorColliders[bodyIndex];
                        if (bodyCollider == null || !bodyCollider.enabled) continue;
                        for (int arenaIndex = 0; arenaIndex < arenaColliders.Length; arenaIndex++)
                        {
                            Collider arenaCollider = arenaColliders[arenaIndex];
                            if (arenaCollider == null || !arenaCollider.enabled) continue;
                            if (!UnityEngine.Physics.ComputePenetration(
                                    bodyCollider,
                                    bodyCollider.transform.position,
                                    bodyCollider.transform.rotation,
                                    arenaCollider,
                                    arenaCollider.transform.position,
                                    arenaCollider.transform.rotation,
                                    out Vector3 direction,
                                    out float distance) ||
                                distance <= deepest)
                                continue;
                            deepest = distance;
                            separation = direction;
                        }
                    }

                    if (deepest <= 0.0001f || separation.sqrMagnitude < 0.5f) break;
                    MoveMotor(
                        motor,
                        separation.normalized * (deepest + surfaceClearance));
                }
            }
        }

        private static void SeatPlayerSpawnAboveArenaFloor(
            GameObject arenaRoot,
            Vector3 planetCenter)
        {
            // Keep the hidden lower puppet clear of the floor without recreating a
            // visible suspension hover at the motor capsule. 0.40 m preserves the
            // active-puppet safety margin while leaving the production capsule below
            // the 0.20 m support-clearance gate for foot IK to close naturally.
            const float spawnClearance = 0.40f;
            Transform floorTransform = FindNamed(
                arenaRoot.GetComponentsInChildren<Transform>(true),
                "Arena_FloorBase_INTACT");
            Collider floor = floorTransform != null
                ? floorTransform.GetComponent<Collider>()
                : null;
            GameObject playerObject = GameObject.Find("Planet Character");
            PlanetMotor player = playerObject != null
                ? playerObject.GetComponent<PlanetMotor>()
                : null;
            if (floor == null || player == null || !floor.enabled) return;

            SerializedObject motorSettings = new SerializedObject(player);
            SerializedProperty probe = motorSettings.FindProperty("groundProbeDistance");
            if (probe != null && !Mathf.Approximately(probe.floatValue, spawnClearance + 0.08f))
            {
                probe.floatValue = spawnClearance + 0.08f;
                motorSettings.ApplyModifiedPropertiesWithoutUndo();
            }

            Vector3 radial = player.transform.position - planetCenter;
            Vector3 up = radial.sqrMagnitude > 0.001f ? radial.normalized : Vector3.up;
            float playerMinimum = MinimumColliderProjection(
                player.GetComponentsInChildren<Collider>(true),
                planetCenter,
                up);
            Collider[] playerColliders = player.GetComponentsInChildren<Collider>(true);
            Bounds playerBounds = default;
            bool hasPlayerBounds = false;
            for (int index = 0; index < playerColliders.Length; index++)
            {
                Collider playerCollider = playerColliders[index];
                if (playerCollider == null || !playerCollider.enabled) continue;
                if (!hasPlayerBounds)
                {
                    playerBounds = playerCollider.bounds;
                    hasPlayerBounds = true;
                }
                else playerBounds.Encapsulate(playerCollider.bounds);
            }
            if (!hasPlayerBounds) return;
            float probeLift = floor.bounds.extents.magnitude +
                              playerBounds.extents.magnitude + 1f;
            bool hasFloorHit = floor.Raycast(
                new Ray(playerBounds.center + up * probeLift, -up),
                out RaycastHit floorHit,
                probeLift * 2f + 1f);
            if (!float.IsFinite(playerMinimum) || !hasFloorHit) return;
            float floorSurface = Vector3.Dot(floorHit.point - planetCenter, up);

            float lift = floorSurface + spawnClearance - playerMinimum;
            if (lift > 0.0001f) MoveMotor(player, up * lift);
        }

        private static void MoveMotor(PlanetMotor motor, Vector3 delta)
        {
            if (motor == null || delta.sqrMagnitude <= 0f) return;
            Vector3 next = motor.transform.position + delta;
            motor.transform.position = next;
            if (motor.Body != null)
            {
                motor.Body.position = next;
                motor.Body.linearVelocity = Vector3.zero;
                motor.Body.angularVelocity = Vector3.zero;
            }

            // The active puppet is deliberately a sibling of the motor root. Its
            // joint bodies therefore do not follow an editor-time spawn correction.
            // Snap only the joints whose authored targets belong to this motor; this
            // is edit-time scene integration, before PhysX owns their poses.
            ActiveRagdollJoint[] joints = UnityEngine.Object.FindObjectsByType<ActiveRagdollJoint>(
                FindObjectsInactive.Include);
            for (int index = 0; index < joints.Length; index++)
            {
                ActiveRagdollJoint joint = joints[index];
                Transform target = joint != null ? joint.TargetPose : null;
                if (target == null || !target.IsChildOf(motor.transform)) continue;
                joint.transform.SetPositionAndRotation(target.position, target.rotation);
                Rigidbody body = joint.Body;
                if (body == null) continue;
                body.position = target.position;
                body.rotation = target.rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private static float MinimumColliderProjection(
            Collider[] colliders,
            Vector3 planetCenter,
            Vector3 up)
        {
            float minimum = float.PositiveInfinity;
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null || !collider.enabled) continue;
                minimum = Mathf.Min(
                    minimum,
                    MinimumColliderProjection(collider, planetCenter, up));
            }
            return minimum;
        }

        private static float MinimumColliderProjection(
            Collider collider,
            Vector3 planetCenter,
            Vector3 up)
        {
            if (collider is BoxCollider box)
            {
                Vector3 extents = box.size * 0.5f;
                float minimum = float.PositiveInfinity;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 local = box.center + Vector3.Scale(
                        extents,
                        new Vector3(x, y, z));
                    Vector3 world = box.transform.TransformPoint(local);
                    minimum = Mathf.Min(minimum, Vector3.Dot(world - planetCenter, up));
                }
                return minimum;
            }

            if (collider is SphereCollider sphere)
            {
                Vector3 scale = sphere.transform.lossyScale;
                float radius = sphere.radius * Mathf.Max(
                    Mathf.Abs(scale.x),
                    Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                Vector3 center = sphere.transform.TransformPoint(sphere.center);
                return Vector3.Dot(center - planetCenter, up) - radius;
            }

            if (collider is CapsuleCollider capsule)
            {
                Vector3 axis = capsule.direction == 0
                    ? Vector3.right
                    : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                float halfLine = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
                Vector3 a = capsule.transform.TransformPoint(capsule.center + axis * halfLine);
                Vector3 b = capsule.transform.TransformPoint(capsule.center - axis * halfLine);
                Vector3 scale = capsule.transform.lossyScale;
                float radialScale = capsule.direction == 0
                    ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))
                    : capsule.direction == 1
                        ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z))
                        : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                return Mathf.Min(
                           Vector3.Dot(a - planetCenter, up),
                           Vector3.Dot(b - planetCenter, up)) -
                       capsule.radius * radialScale;
            }

            Bounds bounds = collider.bounds;
            Vector3 absoluteUp = new Vector3(
                Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
            return Vector3.Dot(bounds.center - planetCenter, up) -
                   Vector3.Dot(bounds.extents, absoluteUp);
        }

        private static Bounds ComponentBounds(Transform item)
        {
            Collider collider = item.GetComponent<Collider>();
            if (collider != null) return collider.bounds;
            Renderer renderer = item.GetComponent<Renderer>();
            return renderer != null
                ? renderer.bounds
                : new Bounds(item.position, Vector3.one * 0.08f);
        }

        private static Vector3 SeatingShift(
            Bounds bounds,
            Vector3 planetCenter,
            float targetMinimumRadius)
        {
            float currentMinimum = MinimumBoundsRadius(bounds, planetCenter);
            Vector3 radial = bounds.center - planetCenter;
            Vector3 up = radial.sqrMagnitude > 0.001f ? radial.normalized : Vector3.up;
            return up * (targetMinimumRadius - currentMinimum);
        }

        private static float MinimumBoundsRadius(Bounds bounds, Vector3 center)
        {
            float minimum = float.PositiveInfinity;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = bounds.center + Vector3.Scale(
                    bounds.extents,
                    new Vector3(x, y, z));
                minimum = Mathf.Min(minimum, Vector3.Distance(corner, center));
            }
            return minimum;
        }

        private static void ConfigureFractureStructures(
            GameObject root,
            EarthArenaFractureCatalog catalog,
            GravityWorldBehaviour gravityWorld,
            Material rockMaterial,
            Material fractureInteriorMaterial,
            EarthSurfaceQueryService surfaceQueries,
            Vector3 surfaceUp)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int structureIndex = 0; structureIndex < catalog.Structures.Length; structureIndex++)
            {
                EarthArenaFractureEntry entry = catalog.Structures[structureIndex];
                if (entry.fractureAsset == null)
                    throw new BuildFailedException(
                        $"Broken Crown structure {entry.structureId} has no fracture asset.");
                Transform intact = FindNamed(all, entry.intactObjectName);
                Transform fractureRoot = FindNamed(all, $"FR_{entry.structureId}_ROOT");
                if (intact == null || fractureRoot == null)
                    throw new BuildFailedException(
                        $"Broken Crown structure {entry.structureId} is missing its intact or fracture root.");

                var pieces = new Transform[entry.fractureAsset.PieceCount];
                for (int pieceIndex = 0; pieceIndex < pieces.Length; pieceIndex++)
                {
                    pieces[pieceIndex] = FindNamed(
                        all,
                        $"FR_{entry.structureId}_P{pieceIndex + 1:000}");
                    if (pieces[pieceIndex] == null)
                        throw new BuildFailedException(
                            $"Broken Crown structure {entry.structureId} is missing piece {pieceIndex + 1}.");
                }

                Renderer intactRenderer = intact.GetComponent<Renderer>();
                Collider intactCollider = intact.GetComponent<Collider>();
                if (intactRenderer == null || intactCollider == null)
                    throw new BuildFailedException(
                        $"Broken Crown structure {entry.structureId} requires an intact renderer and collider.");
                EarthArenaStructure runtime = intact.GetComponent<EarthArenaStructure>();
                if (runtime == null) runtime = intact.gameObject.AddComponent<EarthArenaStructure>();
                if (!runtime.Configure(
                        entry.fractureAsset,
                        root.transform,
                        fractureRoot,
                        intactRenderer,
                        intactCollider,
                        pieces,
                        gravityWorld,
                        rockMaterial,
                        fractureInteriorMaterial,
                        StableStructureId(entry.structureId),
                        entry.ordinaryDamageEnabled,
                        entry.repairable))
                {
                    throw new BuildFailedException(
                        $"Broken Crown structure {entry.structureId} failed runtime fracture configuration.");
                }

                EarthArenaSurfaceProvider surfaceProvider =
                    intact.GetComponent<EarthArenaSurfaceProvider>();
                if (surfaceProvider == null)
                    surfaceProvider = intact.gameObject.AddComponent<EarthArenaSurfaceProvider>();
                surfaceProvider.Configure(
                    runtime,
                    intactCollider,
                    surfaceQueries,
                    surfaceUp,
                    true);
            }
        }

        private static Transform FindNamed(Transform[] transforms, string name)
        {
            for (int index = 0; index < transforms.Length; index++)
                if (string.Equals(transforms[index].name, name, StringComparison.Ordinal))
                    return transforms[index];
            return null;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            if (root == null) return;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
                transforms[index].gameObject.layer = layer;
        }

        private static uint StableStructureId(string value)
        {
            uint hash = 2166136261u;
            for (int index = 0; index < value.Length; index++)
                hash = unchecked((hash ^ value[index]) * 16777619u);
            return hash != 0u ? hash : 1u;
        }

        private static void ConfigureStaticCollider(GameObject target, Mesh mesh)
        {
            if (string.Equals(target.name, "Arena_FloorBase_INTACT", StringComparison.Ordinal))
            {
                BoxCollider oldBox = target.GetComponent<BoxCollider>();
                if (oldBox != null) UnityEngine.Object.DestroyImmediate(oldBox);
                MeshCollider floor = target.GetComponent<MeshCollider>();
                if (floor == null) floor = target.AddComponent<MeshCollider>();
                floor.sharedMesh = mesh;
                floor.convex = false;
                return;
            }

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null) collider = target.AddComponent<BoxCollider>();
            collider.center = mesh.bounds.center;
            collider.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.08f);
        }

        private static void ConfigureLooseRock(
            GameObject target,
            Mesh mesh,
            GravityWorldBehaviour gravityWorld,
            EarthRockDebrisPool debrisPool,
            int stableIndex)
        {
            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null) collider = target.AddComponent<BoxCollider>();
            collider.center = mesh.bounds.center;
            collider.size = Vector3.Max(mesh.bounds.size * 0.92f, Vector3.one * 0.08f);

            Rigidbody body = target.GetComponent<Rigidbody>();
            if (body == null) body = target.AddComponent<Rigidbody>();
            Vector3 size = Vector3.Scale(mesh.bounds.size, target.transform.lossyScale);
            float volume = Mathf.Max(0.05f, Mathf.Abs(size.x * size.y * size.z) * 0.62f);
            body.mass = Mathf.Clamp(volume * 120f, 45f, 1500f);
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeAll;

            GravityBody gravity = target.GetComponent<GravityBody>();
            if (gravity == null) gravity = target.AddComponent<GravityBody>();
            gravity.Configure(gravityWorld, body);
            gravity.enabled = false;

            EarthDestructibleDecorRock decor = target.GetComponent<EarthDestructibleDecorRock>();
            if (decor == null) decor = target.AddComponent<EarthDestructibleDecorRock>();
            decor.Configure(
                0xBC000000u + unchecked((uint)Mathf.Max(1, stableIndex + 1)),
                body,
                collider,
                gravity,
                debrisPool,
                size.magnitude * 0.28f,
                Mathf.Clamp(body.mass * 5.5f, 420f, 2400f));

            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
