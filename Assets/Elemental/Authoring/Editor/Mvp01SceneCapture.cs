using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Presentation.Rendering;
using Elemental.Simulation.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Captures the actual game camera without depending on the Editor window compositor.
    /// This is useful on DX11 machines where desktop automation can see a blank Game View.
    /// </summary>
    public static class Mvp01SceneCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;

        [MenuItem("Elemental/QA/Capture M3 Game Camera")]
        public static void Capture()
        {
            Camera camera = Camera.main != null
                ? Camera.main
                : UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (camera == null)
                throw new InvalidOperationException("The active scene does not contain a camera.");

            string fileName = Application.isPlaying
                ? "Mvp01CombatLatest.png"
                : "Mvp01SceneLatest.png";
            string path = Path.GetFullPath(Path.Combine("BuildReports", fileName));
            CaptureToPath(camera, path);
        }

        [MenuItem("Elemental/QA/Capture Rendering A-B Matrix")]
        public static void CaptureRenderingMatrix()
        {
            Camera camera = Camera.main != null
                ? Camera.main
                : UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (camera == null)
                throw new InvalidOperationException("The active scene does not contain a camera.");

            UniversalAdditionalCameraData cameraData =
                camera.GetUniversalAdditionalCameraData();
            bool originalCameraShadows = cameraData.renderShadows;
            bool originalPost = cameraData.renderPostProcessing;
            Light sun = RenderSettings.sun;
            LightShadows originalSunShadows = sun != null ? sun.shadows : LightShadows.None;
            ScriptableRendererFeature ssao = FindRendererFeature("Elemental Contact SSAO");
            ScriptableRendererFeature atmosphere = FindRendererFeature("Elemental Atmosphere Fullscreen");
            ScriptableRendererFeature miniBokeh = FindRendererFeature("Elemental MiniBokeh");
            ScriptableRendererFeature cinematicDof =
                FindRendererFeature("Elemental Cinematic Depth Of Field");
            bool originalSsao = ssao != null && ssao.isActive;
            bool originalAtmosphere = atmosphere != null && atmosphere.isActive;
            bool originalMiniBokeh = miniBokeh != null && miniBokeh.isActive;
            bool originalCinematicDof = cinematicDof != null && cinematicDof.isActive;
            EarthCinematicDepthOfFieldController dofController =
                camera.GetComponent<EarthCinematicDepthOfFieldController>();
            bool originalDofOverride =
                dofController != null && dofController.HasCaptureOverride;
            EarthCinematicDepthOfFieldDebugView originalDofDebug =
                dofController != null
                    ? dofController.CaptureDebugView
                    : EarthCinematicDepthOfFieldDebugView.Off;
            List<MaterialFloatSnapshot> flatMaterialState = CaptureMaterialState();

            try
            {
                CaptureVariant(camera, "beauty");

                if (cinematicDof != null && dofController != null)
                {
                    GameObject player = GameObject.Find("Planet Character");
                    GameObject opponent = GameObject.Find("Rumble Linebreaker Bot");
                    dofController.ConfigureSubjects(
                        player != null ? player.transform : null,
                        opponent != null ? opponent.transform : null);
                    float primaryDepth = ResolveSubjectDepth(camera, player);
                    float secondaryDepth = ResolveSubjectDepth(camera, opponent);
                    float captureFocus = ResolveCaptureFocus(
                        primaryDepth,
                        secondaryDepth);
                    dofController.ApplyPolicy(
                        false,
                        captureFocus,
                        5.6f,
                        50f);
                    cinematicDof.SetActive(true);
                    dofController.SetCaptureOverride(
                        true,
                        EarthCinematicDepthOfFieldDebugView.Off);
                    CaptureVariant(camera, "cinematic-dof-on");
                    dofController.SetCaptureOverride(
                        true,
                        EarthCinematicDepthOfFieldDebugView.SignedCircleOfConfusion);
                    CaptureVariant(camera, "cinematic-dof-signed-coc");
                    dofController.SetCaptureOverride(
                        true,
                        EarthCinematicDepthOfFieldDebugView.Coverage);
                    CaptureVariant(camera, "cinematic-dof-coverage");
                    dofController.SetCaptureOverride(false);
                    cinematicDof.SetActive(false);
                    CaptureVariant(camera, "cinematic-dof-off");
                    WriteDualSubjectDofMetrics(
                        camera,
                        dofController,
                        player,
                        opponent,
                        primaryDepth,
                        secondaryDepth);
                    cinematicDof.SetActive(originalCinematicDof);
                }

                cameraData.renderShadows = false;
                if (sun != null) sun.shadows = LightShadows.None;
                CaptureVariant(camera, "shadows-off");
                ApplyFlatMaterialState(flatMaterialState);
                CaptureVariant(camera, "shadows-and-facet-off");
                RestoreMaterialState(flatMaterialState);
                cameraData.renderShadows = originalCameraShadows;
                if (sun != null) sun.shadows = originalSunShadows;

                ApplyNormalDebug(flatMaterialState);
                CaptureVariant(camera, "normals");
                RestoreMaterialState(flatMaterialState);

                ApplyArenaSideShadowFade(flatMaterialState, 1f);
                CaptureVariant(camera, "arena-side-shadow-reception-off");
                RestoreMaterialState(flatMaterialState);

                ApplyArenaStableSideFormOcclusion(flatMaterialState, 0f);
                CaptureVariant(camera, "arena-stable-form-ao-off");
                RestoreMaterialState(flatMaterialState);

                if (ssao != null) ssao.SetActive(false);
                CaptureVariant(camera, "ssao-off");
                if (ssao != null) ssao.SetActive(originalSsao);

                ApplyFlatMaterialState(flatMaterialState);
                CaptureVariant(camera, "facet-macro-off");
                RestoreMaterialState(flatMaterialState);

                if (atmosphere != null) atmosphere.SetActive(false);
                CaptureVariant(camera, "atmosphere-off");
                if (atmosphere != null) atmosphere.SetActive(originalAtmosphere);

                if (miniBokeh != null) miniBokeh.SetActive(false);
                CaptureVariant(camera, "bokeh-off");
                if (miniBokeh != null) miniBokeh.SetActive(originalMiniBokeh);
                if (cinematicDof != null) cinematicDof.SetActive(originalCinematicDof);
                if (dofController != null)
                    dofController.SetCaptureOverride(
                        originalDofOverride,
                        originalDofDebug);

                ApplyAlbedoDebug(flatMaterialState);
                CaptureVariant(camera, "albedo");
                if (miniBokeh != null) miniBokeh.SetActive(false);
                CaptureVariant(camera, "albedo-bokeh-off");
                if (miniBokeh != null) miniBokeh.SetActive(originalMiniBokeh);
                RestoreMaterialState(flatMaterialState);

                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlit != null)
                {
                    camera.SetReplacementShader(unlit, string.Empty);
                    CaptureVariant(camera, "unlit-override");
                    camera.ResetReplacementShader();
                }

                cameraData.renderPostProcessing = false;
                CaptureVariant(camera, "post-off");
                cameraData.renderPostProcessing = originalPost;
            }
            finally
            {
                cameraData.renderShadows = originalCameraShadows;
                cameraData.renderPostProcessing = originalPost;
                if (sun != null) sun.shadows = originalSunShadows;
                if (ssao != null) ssao.SetActive(originalSsao);
                if (atmosphere != null) atmosphere.SetActive(originalAtmosphere);
                if (miniBokeh != null) miniBokeh.SetActive(originalMiniBokeh);
                if (cinematicDof != null) cinematicDof.SetActive(originalCinematicDof);
                if (dofController != null)
                    dofController.SetCaptureOverride(
                        originalDofOverride,
                        originalDofDebug);
                camera.ResetReplacementShader();
                RestoreMaterialState(flatMaterialState);
            }

            WriteArenaFormDepthMetrics();
            Debug.Log("[Elemental] Rendering A-B matrix captured in BuildReports/RenderingAB.");
        }

        [MenuItem("Elemental/QA/Capture Shadow Bias Matrix")]
        public static void CaptureShadowBiasMatrix()
        {
            Camera camera = Camera.main != null
                ? Camera.main
                : UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (camera == null)
                throw new InvalidOperationException("The active scene does not contain a camera.");

            UniversalRenderPipelineAsset pipeline =
                UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
                throw new InvalidOperationException("The current render pipeline is not URP.");

            Light sun = RenderSettings.sun;
            float originalDepthBias = pipeline.shadowDepthBias;
            float originalNormalBias = pipeline.shadowNormalBias;
            float originalSunDepthBias = sun != null ? sun.shadowBias : originalDepthBias;
            float originalSunNormalBias = sun != null ? sun.shadowNormalBias : originalNormalBias;
            float[] depthBiases = { 0.35f, 0.50f, 0.65f };
            float[] normalBiases = { 0.20f, 0.30f, 0.40f };

            try
            {
                for (int depthIndex = 0; depthIndex < depthBiases.Length; depthIndex++)
                for (int normalIndex = 0; normalIndex < normalBiases.Length; normalIndex++)
                {
                    float depthBias = depthBiases[depthIndex];
                    float normalBias = normalBiases[normalIndex];
                    pipeline.shadowDepthBias = depthBias;
                    pipeline.shadowNormalBias = normalBias;
                    if (sun != null)
                    {
                        sun.shadowBias = depthBias;
                        sun.shadowNormalBias = normalBias;
                    }
                    CaptureVariant(
                        camera,
                        $"shadow-bias-d{Mathf.RoundToInt(depthBias * 100f):00}-n{Mathf.RoundToInt(normalBias * 100f):00}");
                }
            }
            finally
            {
                pipeline.shadowDepthBias = originalDepthBias;
                pipeline.shadowNormalBias = originalNormalBias;
                if (sun != null)
                {
                    sun.shadowBias = originalSunDepthBias;
                    sun.shadowNormalBias = originalSunNormalBias;
                }
            }

            Debug.Log("[Elemental] Shadow bias matrix captured in BuildReports/RenderingAB.");
        }

        [MenuItem("Elemental/QA/Capture Shadow Temporal Pan")]
        public static void CaptureShadowTemporalPan()
        {
            Camera camera = Camera.main != null
                ? Camera.main
                : UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (camera == null)
                throw new InvalidOperationException("The active scene does not contain a camera.");
            if (!TryResolveShadowPanAnchor(
                    camera,
                    out RaycastHit anchorHit,
                    out Renderer anchorRenderer,
                    out float radialUpAlignment))
                throw new InvalidOperationException(
                    "No visible Broken Crown radial-side receiver was found for the temporal pan capture.");

            Vector3 originalPosition = camera.transform.position;
            Quaternion originalRotation = camera.transform.rotation;
            Vector3 originalRight = camera.transform.right;
            Vector3 originalUp = camera.transform.up;
            Vector3 lookTarget = originalPosition + camera.transform.forward * 12f;
            float[] offsets = { -0.06f, -0.03f, 0f, 0.03f, 0.06f };
            string folder = Path.GetFullPath(Path.Combine(
                "BuildReports",
                "RenderingAB"));
            var productionFramePaths = new string[offsets.Length];
            var productionAnchorViewports = new Vector3[offsets.Length];
            var bypassFramePaths = new string[offsets.Length];
            var bypassAnchorViewports = new Vector3[offsets.Length];
            List<MaterialFloatSnapshot> materialState = CaptureMaterialState();
            try
            {
                ApplyArenaSideShadowFade(materialState, 0f);
                CaptureShadowTemporalSequence(
                    camera,
                    originalPosition,
                    originalRight,
                    originalUp,
                    lookTarget,
                    anchorHit.point,
                    offsets,
                    folder,
                    "shadow-temporal-pan-production",
                    productionFramePaths,
                    productionAnchorViewports);

                ApplyArenaSideShadowFade(materialState, 1f);
                CaptureShadowTemporalSequence(
                    camera,
                    originalPosition,
                    originalRight,
                    originalUp,
                    lookTarget,
                    anchorHit.point,
                    offsets,
                    folder,
                    "shadow-temporal-pan-bypass",
                    bypassFramePaths,
                    bypassAnchorViewports);
            }
            finally
            {
                camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
                RestoreMaterialState(materialState);
            }

            MeasureTemporalSequence(
                productionFramePaths,
                productionAnchorViewports,
                42,
                out float[] productionMae,
                out float[] productionRms,
                out float productionMeanMae,
                out float productionMaximumRms);
            MeasureTemporalSequence(
                bypassFramePaths,
                bypassAnchorViewports,
                42,
                out float[] bypassMae,
                out float[] bypassRms,
                out float bypassMeanMae,
                out float bypassMaximumRms);

            Material material = anchorRenderer != null
                ? anchorRenderer.sharedMaterial
                : null;
            GameObject planet = GameObject.Find("Planet Collision Proxy");
            Vector3 planetCenter = planet != null
                ? planet.transform.position
                : Vector3.zero;
            Vector4 serializedCenter4 = material != null &&
                                        material.HasProperty("_ReceiverPlanetCenter")
                ? material.GetVector("_ReceiverPlanetCenter")
                : new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
            Vector3 serializedCenter = new Vector3(
                serializedCenter4.x,
                serializedCenter4.y,
                serializedCenter4.z);
            float centerError = float.IsFinite(serializedCenter.x)
                ? Vector3.Distance(serializedCenter, planetCenter)
                : float.PositiveInfinity;
            bool productionUsesRealtimeSideShadows = material != null &&
                                                     material.HasProperty("_SideShadowFade") &&
                                                     material.GetFloat("_SideShadowFade") < 0.5f;
            float stableFormOcclusion = material != null &&
                                        material.HasProperty("_StableSideFormOcclusion")
                ? material.GetFloat("_StableSideFormOcclusion")
                : 0f;
            var metrics = new ShadowTemporalPanMetrics
            {
                schema = "shadow-temporal-pan-v3",
                capturedUtc = DateTime.UtcNow.ToString("O"),
                anchorObject = anchorHit.collider != null
                    ? anchorHit.collider.name
                    : string.Empty,
                anchorWorld = anchorHit.point,
                anchorNormal = anchorHit.normal,
                radialUpAlignment = radialUpAlignment,
                productionUsesRealtimeSideShadows = productionUsesRealtimeSideShadows,
                stableSideFormOcclusion = stableFormOcclusion,
                scenePlanetCenter = planetCenter,
                serializedReceiverPlanetCenter = serializedCenter,
                receiverPlanetCenterErrorMeters = centerError,
                sideMaskClassifiesAnchor =
                    radialUpAlignment < 0.72f && centerError <= 0.001f,
                panOffsetMeters = offsets,
                productionAnchorViewport = productionAnchorViewports,
                productionPairMae255 = productionMae,
                productionPairRms255 = productionRms,
                productionMeanPairMae255 = productionMeanMae,
                productionMaximumPairRms255 = productionMaximumRms,
                bypassPairMae255 = bypassMae,
                bypassPairRms255 = bypassRms,
                bypassMeanPairMae255 = bypassMeanMae,
                bypassMaximumPairRms255 = bypassMaximumRms,
                bypassRelativeTemporalChange01 = productionMeanMae > 0.0001f
                    ? bypassMeanMae / productionMeanMae - 1f
                    : 0f,
                productionRoiMeanPairMaeThreshold255 = 1f,
                productionTemporalPass = productionUsesRealtimeSideShadows &&
                                         centerError <= 0.001f &&
                                         radialUpAlignment < 0.72f &&
                                         productionMeanMae >= 0f &&
                                         productionMeanMae <= 1f,
                roiRadiusPixels = 42
            };
            string reportPath = Path.Combine(
                folder,
                "Mvp01-shadow-temporal-pan-metrics.json");
            Directory.CreateDirectory(folder);
            File.WriteAllText(reportPath, JsonUtility.ToJson(metrics, true));
            Debug.Log(
                $"[Elemental] Shadow temporal pan captured. Production real-shadow " +
                $"ROI MAE {metrics.productionMeanPairMae255:F3}/255 versus bypass " +
                $"{metrics.bypassMeanPairMae255:F3}/255; production max RMS " +
                $"{metrics.productionMaximumPairRms255:F3}/255; absolute production gate " +
                $"{(metrics.productionTemporalPass ? "PASS" : "FAIL")}. " +
                $"Report: {reportPath}");
        }

        private static void CaptureShadowTemporalSequence(
            Camera camera,
            Vector3 origin,
            Vector3 cameraRight,
            Vector3 cameraUp,
            Vector3 lookTarget,
            Vector3 anchorWorld,
            float[] offsets,
            string folder,
            string suffix,
            string[] framePaths,
            Vector3[] anchorViewports)
        {
            for (int index = 0; index < offsets.Length; index++)
            {
                Vector3 position = origin + cameraRight * offsets[index];
                camera.transform.SetPositionAndRotation(
                    position,
                    Quaternion.LookRotation(lookTarget - position, cameraUp));
                anchorViewports[index] = camera.WorldToViewportPoint(anchorWorld);
                framePaths[index] = Path.Combine(
                    folder,
                    $"Mvp01-{suffix}-{index:00}.png");
                CaptureToPath(camera, framePaths[index]);
            }
        }

        private static void MeasureTemporalSequence(
            string[] framePaths,
            Vector3[] anchorViewports,
            int roiRadiusPixels,
            out float[] mae,
            out float[] rms,
            out float meanMae,
            out float maximumRms)
        {
            int pairCount = Mathf.Max(0, framePaths.Length - 1);
            mae = new float[pairCount];
            rms = new float[pairCount];
            float maeSum = 0f;
            int validCount = 0;
            maximumRms = 0f;
            for (int index = 0; index < pairCount; index++)
            {
                if (!TryMeasureReprojectedDifference(
                        framePaths[index],
                        framePaths[index + 1],
                        anchorViewports[index],
                        anchorViewports[index + 1],
                        roiRadiusPixels,
                        out mae[index],
                        out rms[index]))
                    continue;
                maeSum += mae[index];
                maximumRms = Mathf.Max(maximumRms, rms[index]);
                validCount++;
            }
            meanMae = validCount > 0 ? maeSum / validCount : -1f;
            if (validCount == 0) maximumRms = -1f;
        }

        private static void CaptureVariant(Camera camera, string suffix)
        {
            string path = Path.GetFullPath(Path.Combine(
                "BuildReports",
                "RenderingAB",
                $"Mvp01-{suffix}.png"));
            CaptureToPath(camera, path);
        }

        private static void CaptureToPath(Camera camera, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "MVP 0.1 QA Capture"
            };
            Texture2D pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0, false);
                pixels.Apply(false, false);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Debug.Log($"[Elemental] MVP 0.1 camera captured: {path}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static ScriptableRendererFeature FindRendererFeature(string featureName)
        {
            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                EarthRenderQualitySetup.RendererPath);
            if (renderer == null) return null;
            for (int index = 0; index < renderer.rendererFeatures.Count; index++)
            {
                ScriptableRendererFeature feature = renderer.rendererFeatures[index];
                if (feature != null && string.Equals(
                        feature.name,
                        featureName,
                        StringComparison.Ordinal))
                    return feature;
            }
            return null;
        }

        private static List<MaterialFloatSnapshot> CaptureMaterialState()
        {
            var snapshots = new List<MaterialFloatSnapshot>();
            var visited = new HashSet<Material>();
            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null || !visited.Add(material)) continue;
                    snapshots.Add(new MaterialFloatSnapshot(material));
                }
            }
            return snapshots;
        }

        private static void ApplyFlatMaterialState(List<MaterialFloatSnapshot> snapshots)
        {
            for (int index = 0; index < snapshots.Count; index++)
                snapshots[index].ApplyFlat();
        }

        private static void RestoreMaterialState(List<MaterialFloatSnapshot> snapshots)
        {
            for (int index = 0; index < snapshots.Count; index++)
                snapshots[index].Restore();
        }

        private static void ApplyAlbedoDebug(List<MaterialFloatSnapshot> snapshots)
        {
            for (int index = 0; index < snapshots.Count; index++)
                snapshots[index].ApplyAlbedoDebug();
        }

        private static void ApplyNormalDebug(List<MaterialFloatSnapshot> snapshots)
        {
            for (int index = 0; index < snapshots.Count; index++)
                snapshots[index].ApplyNormalDebug();
        }

        private static void ApplyArenaSideShadowFade(
            List<MaterialFloatSnapshot> snapshots,
            float value)
        {
            for (int index = 0; index < snapshots.Count; index++)
                snapshots[index].ApplyArenaSideShadowFade(value);
        }

        private static void ApplyArenaStableSideFormOcclusion(
            List<MaterialFloatSnapshot> snapshots,
            float value)
        {
            for (int index = 0; index < snapshots.Count; index++)
                snapshots[index].ApplyArenaStableSideFormOcclusion(value);
        }

        private static float ResolveSubjectDepth(Camera camera, GameObject subject)
        {
            if (camera == null || subject == null) return float.NaN;
            return TryResolveSubjectDepthRange(
                camera,
                subject,
                out float near,
                out float far,
                out _)
                ? (near + far) * 0.5f
                : float.NaN;
        }

        private static float ResolveCaptureFocus(
            float primaryDepth,
            float secondaryDepth)
        {
            bool primaryValid = float.IsFinite(primaryDepth) && primaryDepth > 0f;
            bool secondaryValid = float.IsFinite(secondaryDepth) && secondaryDepth > 0f;
            if (primaryValid && secondaryValid)
                return Mathf.Clamp((primaryDepth + secondaryDepth) * 0.5f, 1.25f, 36f);
            if (primaryValid) return Mathf.Clamp(primaryDepth, 1.25f, 36f);
            if (secondaryValid) return Mathf.Clamp(secondaryDepth, 1.25f, 36f);
            return 8f;
        }

        private static void WriteDualSubjectDofMetrics(
            Camera camera,
            EarthCinematicDepthOfFieldController controller,
            GameObject primary,
            GameObject secondary,
            float primaryDepth,
            float secondaryDepth)
        {
            float near = controller.SharpNearDistance;
            float far = controller.SharpFarDistance;
            TryResolveSubjectDepthRange(
                camera,
                primary,
                out float primaryNear,
                out float primaryFar,
                out _);
            TryResolveSubjectDepthRange(
                camera,
                secondary,
                out float secondaryNear,
                out float secondaryFar,
                out _);
            var metrics = new DualSubjectDofMetrics
            {
                schema = "cinematic-dof-dual-subject-v1",
                capturedUtc = DateTime.UtcNow.ToString("O"),
                primarySubject = primary != null ? primary.name : string.Empty,
                secondarySubject = secondary != null ? secondary.name : string.Empty,
                primaryDepth = primaryDepth,
                secondaryDepth = secondaryDepth,
                primaryNearDepth = primaryNear,
                primaryFarDepth = primaryFar,
                secondaryNearDepth = secondaryNear,
                secondaryFarDepth = secondaryFar,
                sharpNearDepth = near,
                sharpFarDepth = far,
                silhouettePadding = controller.SilhouettePadding,
                primaryInsideSharpEnvelope = IsInside(primaryDepth, near, far),
                secondaryInsideSharpEnvelope = IsInside(secondaryDepth, near, far),
                primarySilhouetteInsideSharpEnvelope =
                    IsRangeInside(primaryNear, primaryFar, near, far),
                secondarySilhouetteInsideSharpEnvelope =
                    IsRangeInside(secondaryNear, secondaryFar, near, far),
                primaryViewport = ResolveViewport(camera, primary),
                secondaryViewport = ResolveViewport(camera, secondary),
                nearOutsideCoc = EarthCinematicDepthOfFieldSolver.SignedCircleOfConfusion(
                    Mathf.Max(0.05f, near - 1.15f), near, far, 1.15f, 5.5f),
                farOutsideCoc = EarthCinematicDepthOfFieldSolver.SignedCircleOfConfusion(
                    far + 5.5f, near, far, 1.15f, 5.5f)
            };
            string imageFolder = Path.GetFullPath(Path.Combine(
                "BuildReports",
                "RenderingAB"));
            string onPath = Path.Combine(imageFolder, "Mvp01-cinematic-dof-on.png");
            string offPath = Path.Combine(imageFolder, "Mvp01-cinematic-dof-off.png");
            TryMeasureImageDifference(
                onPath,
                offPath,
                new Vector3(0.5f, 0.5f, 1f),
                -1,
                out metrics.fullFrameMae255,
                out metrics.fullFrameRms255);
            TryMeasureImageDifference(
                onPath,
                offPath,
                metrics.primaryViewport,
                56,
                out metrics.primarySharpRoiMae255,
                out metrics.primarySharpRoiRms255);
            TryMeasureImageDifference(
                onPath,
                offPath,
                metrics.secondaryViewport,
                56,
                out metrics.secondarySharpRoiMae255,
                out metrics.secondarySharpRoiRms255);
            string path = Path.GetFullPath(Path.Combine(
                "BuildReports",
                "RenderingAB",
                "Mvp01-cinematic-dof-metrics.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonUtility.ToJson(metrics, true));
        }

        private static void WriteArenaFormDepthMetrics()
        {
            string folder = Path.GetFullPath(Path.Combine(
                "BuildReports",
                "RenderingAB"));
            string beautyPath = Path.Combine(folder, "Mvp01-beauty.png");
            string analyticOffPath = Path.Combine(
                folder,
                "Mvp01-arena-stable-form-ao-off.png");
            string ssaoOffPath = Path.Combine(folder, "Mvp01-ssao-off.png");
            TryMeasureImageDifference(
                beautyPath,
                analyticOffPath,
                new Vector3(0.5f, 0.5f, 1f),
                -1,
                out float analyticMae,
                out float analyticRms);
            TryMeasureImageDifference(
                beautyPath,
                ssaoOffPath,
                new Vector3(0.5f, 0.5f, 1f),
                -1,
                out float ssaoMae,
                out float ssaoRms);

            Material arena = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Elemental/Content/GraphicsV5/Materials/RumbleArenaSandstone.mat");
            ScriptableRendererFeature ssao = FindRendererFeature("Elemental Contact SSAO");
            SerializedObject serializedSsao = ssao != null
                ? new SerializedObject(ssao)
                : null;
            var metrics = new ArenaFormDepthMetrics
            {
                schema = "arena-stable-form-depth-v2",
                capturedUtc = DateTime.UtcNow.ToString("O"),
                stableSideFormOcclusion = arena != null &&
                                          arena.HasProperty("_StableSideFormOcclusion")
                    ? arena.GetFloat("_StableSideFormOcclusion")
                    : 0f,
                arenaHasDepthNormalsPass = arena != null &&
                                           arena.FindPass("DepthNormals") >= 0,
                analyticAoOnOffMae255 = analyticMae,
                analyticAoOnOffRms255 = analyticRms,
                ssaoOnOffMae255 = ssaoMae,
                ssaoOnOffRms255 = ssaoRms,
                ssaoIntensity = serializedSsao?.FindProperty(
                    "m_Settings.Intensity")?.floatValue ?? 0f,
                ssaoDirectContribution = serializedSsao?.FindProperty(
                    "m_Settings.DirectLightingStrength")?.floatValue ?? 0f,
                ssaoRadius = serializedSsao?.FindProperty(
                    "m_Settings.Radius")?.floatValue ?? 0f,
                ssaoUsesDepthNormals = serializedSsao?.FindProperty(
                    "m_Settings.Source")?.intValue == 1,
                ssaoFullResolution = serializedSsao?.FindProperty(
                    "m_Settings.Downsample")?.boolValue == false,
                ssaoBeforeOpaques = serializedSsao?.FindProperty(
                    "m_Settings.AfterOpaque")?.boolValue == false,
                ssaoHighSamples = serializedSsao?.FindProperty(
                    "m_Settings.Samples")?.intValue == 0,
                ssaoHighBilateral = serializedSsao?.FindProperty(
                    "m_Settings.BlurQuality")?.intValue == 0
            };
            File.WriteAllText(
                Path.Combine(folder, "Mvp01-arena-form-depth-metrics.json"),
                JsonUtility.ToJson(metrics, true));
        }

        private static bool TryMeasureImageDifference(
            string firstPath,
            string secondPath,
            Vector3 centerViewport,
            int radiusPixels,
            out float mae255,
            out float rms255)
        {
            mae255 = -1f;
            rms255 = -1f;
            if (!File.Exists(firstPath) || !File.Exists(secondPath) ||
                centerViewport.z <= 0f)
                return false;

            var first = new Texture2D(2, 2, TextureFormat.RGB24, false);
            var second = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                if (!first.LoadImage(File.ReadAllBytes(firstPath), false) ||
                    !second.LoadImage(File.ReadAllBytes(secondPath), false) ||
                    first.width != second.width || first.height != second.height)
                    return false;
                Color32[] firstPixels = first.GetPixels32();
                Color32[] secondPixels = second.GetPixels32();
                int minimumX = 0;
                int maximumX = first.width - 1;
                int minimumY = 0;
                int maximumY = first.height - 1;
                if (radiusPixels > 0)
                {
                    int centerX = Mathf.RoundToInt(centerViewport.x * (first.width - 1));
                    int centerY = Mathf.RoundToInt(centerViewport.y * (first.height - 1));
                    minimumX = Mathf.Clamp(centerX - radiusPixels, 0, maximumX);
                    maximumX = Mathf.Clamp(centerX + radiusPixels, 0, maximumX);
                    minimumY = Mathf.Clamp(centerY - radiusPixels, 0, maximumY);
                    maximumY = Mathf.Clamp(centerY + radiusPixels, 0, maximumY);
                }

                double absolute = 0.0;
                double squared = 0.0;
                long channelCount = 0;
                for (int y = minimumY; y <= maximumY; y++)
                for (int x = minimumX; x <= maximumX; x++)
                {
                    int index = y * first.width + x;
                    AccumulateDifference(firstPixels[index].r, secondPixels[index].r,
                        ref absolute, ref squared);
                    AccumulateDifference(firstPixels[index].g, secondPixels[index].g,
                        ref absolute, ref squared);
                    AccumulateDifference(firstPixels[index].b, secondPixels[index].b,
                        ref absolute, ref squared);
                    channelCount += 3;
                }
                if (channelCount <= 0) return false;
                mae255 = (float)(absolute / channelCount);
                rms255 = (float)Math.Sqrt(squared / channelCount);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        private static void AccumulateDifference(
            byte first,
            byte second,
            ref double absolute,
            ref double squared)
        {
            double difference = Math.Abs(first - second);
            absolute += difference;
            squared += difference * difference;
        }

        private static bool TryMeasureReprojectedDifference(
            string firstPath,
            string secondPath,
            Vector3 firstViewport,
            Vector3 secondViewport,
            int radiusPixels,
            out float mae255,
            out float rms255)
        {
            mae255 = -1f;
            rms255 = -1f;
            if (!File.Exists(firstPath) || !File.Exists(secondPath) ||
                firstViewport.z <= 0f || secondViewport.z <= 0f)
                return false;
            var first = new Texture2D(2, 2, TextureFormat.RGB24, false);
            var second = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                if (!first.LoadImage(File.ReadAllBytes(firstPath), false) ||
                    !second.LoadImage(File.ReadAllBytes(secondPath), false) ||
                    first.width != second.width || first.height != second.height)
                    return false;
                Color32[] firstPixels = first.GetPixels32();
                Color32[] secondPixels = second.GetPixels32();
                int firstX = Mathf.RoundToInt(firstViewport.x * (first.width - 1));
                int firstY = Mathf.RoundToInt(firstViewport.y * (first.height - 1));
                int secondX = Mathf.RoundToInt(secondViewport.x * (second.width - 1));
                int secondY = Mathf.RoundToInt(secondViewport.y * (second.height - 1));
                double absolute = 0.0;
                double squared = 0.0;
                long channelCount = 0;
                for (int y = -radiusPixels; y <= radiusPixels; y++)
                for (int x = -radiusPixels; x <= radiusPixels; x++)
                {
                    int ax = firstX + x;
                    int ay = firstY + y;
                    int bx = secondX + x;
                    int by = secondY + y;
                    if (ax < 0 || ax >= first.width || ay < 0 || ay >= first.height ||
                        bx < 0 || bx >= second.width || by < 0 || by >= second.height)
                        continue;
                    Color32 a = firstPixels[ay * first.width + ax];
                    Color32 b = secondPixels[by * second.width + bx];
                    AccumulateDifference(a.r, b.r, ref absolute, ref squared);
                    AccumulateDifference(a.g, b.g, ref absolute, ref squared);
                    AccumulateDifference(a.b, b.b, ref absolute, ref squared);
                    channelCount += 3;
                }
                if (channelCount <= 0) return false;
                mae255 = (float)(absolute / channelCount);
                rms255 = (float)Math.Sqrt(squared / channelCount);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        private static bool TryResolveShadowPanAnchor(
            Camera camera,
            out RaycastHit anchorHit,
            out Renderer anchorRenderer,
            out float radialUpAlignment)
        {
            anchorHit = default;
            anchorRenderer = null;
            radialUpAlignment = 1f;
            GameObject planet = GameObject.Find("Planet Collision Proxy");
            Vector3 planetCenter = planet != null ? planet.transform.position : Vector3.zero;
            Vector2[] candidates =
            {
                new Vector2(0.82f, 0.60f),
                new Vector2(0.72f, 0.62f),
                new Vector2(0.24f, 0.61f),
                new Vector2(0.87f, 0.48f),
                new Vector2(0.18f, 0.52f),
                new Vector2(0.66f, 0.72f)
            };
            Physics.SyncTransforms();
            for (int index = 0; index < candidates.Length; index++)
            {
                Ray ray = camera.ViewportPointToRay(new Vector3(
                    candidates[index].x,
                    candidates[index].y,
                    0f));
                RaycastHit[] hits = Physics.RaycastAll(
                    ray,
                    200f,
                    ~0,
                    QueryTriggerInteraction.Ignore);
                Array.Sort(hits, static (a, b) => a.distance.CompareTo(b.distance));
                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    RaycastHit hit = hits[hitIndex];
                    Renderer renderer = hit.collider != null
                        ? hit.collider.GetComponent<Renderer>() ??
                          hit.collider.GetComponentInParent<Renderer>() ??
                          hit.collider.GetComponentInChildren<Renderer>()
                        : null;
                    Material material = renderer != null
                        ? renderer.sharedMaterial
                        : null;
                    if (material == null ||
                        material.name.IndexOf(
                            "Arena",
                            StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    Vector3 surfaceUp = (hit.point - planetCenter).normalized;
                    float alignment = Vector3.Dot(hit.normal, surfaceUp);
                    if (alignment >= 0.72f) continue;
                    anchorHit = hit;
                    anchorRenderer = renderer;
                    radialUpAlignment = alignment;
                    return true;
                }
            }
            return false;
        }

        private static Vector3 ResolveViewport(Camera camera, GameObject subject)
        {
            if (camera == null || subject == null) return new Vector3(-1f, -1f, -1f);
            return TryResolveSubjectDepthRange(
                camera,
                subject,
                out _,
                out _,
                out Vector3 center)
                ? camera.WorldToViewportPoint(center)
                : new Vector3(-1f, -1f, -1f);
        }

        private static bool TryResolveSubjectDepthRange(
            Camera camera,
            GameObject subject,
            out float nearDepth,
            out float farDepth,
            out Vector3 boundsCenter)
        {
            nearDepth = float.PositiveInfinity;
            farDepth = float.NegativeInfinity;
            boundsCenter = Vector3.zero;
            if (camera == null || subject == null) return false;
            Renderer[] renderers = subject.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds combined = default;
            Vector3 forward = camera.transform.forward;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy ||
                    (renderer is not SkinnedMeshRenderer &&
                     renderer is not MeshRenderer))
                    continue;
                Bounds bounds = renderer.bounds;
                if (!hasBounds) combined = bounds;
                else combined.Encapsulate(bounds);
                float centerDepth = Vector3.Dot(
                    bounds.center - camera.transform.position,
                    forward);
                Vector3 extents = bounds.extents;
                float radius = Mathf.Abs(forward.x) * extents.x +
                               Mathf.Abs(forward.y) * extents.y +
                               Mathf.Abs(forward.z) * extents.z;
                nearDepth = Mathf.Min(nearDepth, centerDepth - radius);
                farDepth = Mathf.Max(farDepth, centerDepth + radius);
                hasBounds = true;
            }
            if (!hasBounds) return false;
            boundsCenter = combined.center;
            return float.IsFinite(nearDepth) && float.IsFinite(farDepth) &&
                   farDepth > 0.05f;
        }

        private static bool IsInside(float depth, float near, float far)
        {
            return float.IsFinite(depth) && depth >= near && depth <= far;
        }

        private static bool IsRangeInside(
            float rangeNear,
            float rangeFar,
            float envelopeNear,
            float envelopeFar)
        {
            return float.IsFinite(rangeNear) && float.IsFinite(rangeFar) &&
                   Mathf.Min(rangeNear, rangeFar) >= envelopeNear &&
                   Mathf.Max(rangeNear, rangeFar) <= envelopeFar;
        }

        [Serializable]
        private sealed class DualSubjectDofMetrics
        {
            public string schema;
            public string capturedUtc;
            public string primarySubject;
            public string secondarySubject;
            public float primaryDepth;
            public float secondaryDepth;
            public float primaryNearDepth;
            public float primaryFarDepth;
            public float secondaryNearDepth;
            public float secondaryFarDepth;
            public float sharpNearDepth;
            public float sharpFarDepth;
            public float silhouettePadding;
            public bool primaryInsideSharpEnvelope;
            public bool secondaryInsideSharpEnvelope;
            public bool primarySilhouetteInsideSharpEnvelope;
            public bool secondarySilhouetteInsideSharpEnvelope;
            public Vector3 primaryViewport;
            public Vector3 secondaryViewport;
            public float nearOutsideCoc;
            public float farOutsideCoc;
            public float fullFrameMae255;
            public float fullFrameRms255;
            public float primarySharpRoiMae255;
            public float primarySharpRoiRms255;
            public float secondarySharpRoiMae255;
            public float secondarySharpRoiRms255;
        }

        [Serializable]
        private sealed class ArenaFormDepthMetrics
        {
            public string schema;
            public string capturedUtc;
            public float stableSideFormOcclusion;
            public bool arenaHasDepthNormalsPass;
            public float analyticAoOnOffMae255;
            public float analyticAoOnOffRms255;
            public float ssaoOnOffMae255;
            public float ssaoOnOffRms255;
            public float ssaoIntensity;
            public float ssaoDirectContribution;
            public float ssaoRadius;
            public bool ssaoUsesDepthNormals;
            public bool ssaoFullResolution;
            public bool ssaoBeforeOpaques;
            public bool ssaoHighSamples;
            public bool ssaoHighBilateral;
        }

        [Serializable]
        private sealed class ShadowTemporalPanMetrics
        {
            public string schema;
            public string capturedUtc;
            public string anchorObject;
            public Vector3 anchorWorld;
            public Vector3 anchorNormal;
            public float radialUpAlignment;
            public bool productionUsesRealtimeSideShadows;
            public float stableSideFormOcclusion;
            public Vector3 scenePlanetCenter;
            public Vector3 serializedReceiverPlanetCenter;
            public float receiverPlanetCenterErrorMeters;
            public bool sideMaskClassifiesAnchor;
            public float[] panOffsetMeters;
            public Vector3[] productionAnchorViewport;
            public float[] productionPairMae255;
            public float[] productionPairRms255;
            public float productionMeanPairMae255;
            public float productionMaximumPairRms255;
            public float[] bypassPairMae255;
            public float[] bypassPairRms255;
            public float bypassMeanPairMae255;
            public float bypassMaximumPairRms255;
            public float bypassRelativeTemporalChange01;
            public float productionRoiMeanPairMaeThreshold255;
            public bool productionTemporalPass;
            public int roiRadiusPixels;
        }

        private sealed class MaterialFloatSnapshot
        {
            private static readonly string[] Properties =
            {
                "_FacetContrast",
                "_MacroStrength",
                "_MacroVariation",
                "_ProceduralDetail",
                "_DebugMode",
                "_SideShadowFade",
                "_StableSideFormOcclusion",
                "_FractureInteriorDepth"
            };

            private readonly Material _material;
            private readonly float[] _values;
            private readonly bool[] _present;

            public MaterialFloatSnapshot(Material material)
            {
                _material = material;
                _values = new float[Properties.Length];
                _present = new bool[Properties.Length];
                for (int index = 0; index < Properties.Length; index++)
                {
                    _present[index] = material.HasProperty(Properties[index]);
                    if (_present[index]) _values[index] = material.GetFloat(Properties[index]);
                }
            }

            public void ApplyFlat()
            {
                if (_material == null) return;
                // This diagnostic isolates authored facet/macro variation. Shadow
                // reception, analytic form AO and fracture depth remain untouched
                // so the capture name describes exactly one changed system.
                for (int index = 0; index < 4; index++)
                    if (_present[index]) _material.SetFloat(Properties[index], 0f);
            }

            public void Restore()
            {
                if (_material == null) return;
                for (int index = 0; index < Properties.Length; index++)
                    if (_present[index]) _material.SetFloat(Properties[index], _values[index]);
            }

            public void ApplyAlbedoDebug()
            {
                if (_material != null && _material.HasProperty("_DebugMode"))
                    _material.SetFloat("_DebugMode", 5f);
            }

            public void ApplyNormalDebug()
            {
                if (_material != null && _material.HasProperty("_DebugMode"))
                    _material.SetFloat("_DebugMode", 2f);
            }

            public void ApplyArenaSideShadowFade(float value)
            {
                if (_material == null ||
                    _material.name.IndexOf(
                        "Arena",
                        StringComparison.OrdinalIgnoreCase) < 0 ||
                    !_material.HasProperty("_SideShadowFade"))
                    return;
                _material.SetFloat("_SideShadowFade", Mathf.Clamp01(value));
            }

            public void ApplyArenaStableSideFormOcclusion(float value)
            {
                if (_material == null ||
                    _material.name.IndexOf(
                        "Arena",
                        StringComparison.OrdinalIgnoreCase) < 0 ||
                    !_material.HasProperty("_StableSideFormOcclusion"))
                    return;
                _material.SetFloat(
                    "_StableSideFormOcclusion",
                    Mathf.Clamp(value, 0f, 0.12f));
            }
        }
    }
}
