using System;
using System.Collections;
using System.IO;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Elemental.Tests.PlayMode
{
    /// <summary>Actual URP pixels: cosmetic fragments do not hide dust, opaque matter does.</summary>
    public sealed class EarthDustCompositingRuntimeTests
    {
        private const string ProfilePath = "Assets/Elemental/Content/Profiles/EarthEffectsTuningProfile.asset";
        private const string ReportFolder = "BuildReports/DustCompositing";
        private const int Resolution = 96;
        private const int TestLayer = 31;
        private const int RoiHalfSize = 6;

        [Serializable]
        private sealed class Sample
        {
            public string renderer;
            public string sourceMaterial;
            public string shader;
            public Color chipOnly;
            public Color chipWithDust;
            public float chipVisibleDelta;
            public float dustVisibleDelta;
            public float dustOverChipDelta;
            public float opaqueWallOcclusionDelta;
        }

        [Serializable]
        private sealed class Report
        {
            public string utc;
            public string graphicsDevice;
            public string renderPipeline;
            public string dustMaterial;
            public string ambientMoteMaterial;
            public bool passed;
            public int captureResolution = Resolution;
            public int centerRoiSize = RoiHalfSize * 2;
            public float chipCameraDistance = 5f;
            public float dustCameraDistance = 6.5f;
            public float opaqueWallCameraDistance = 4f;
            public Sample[] samples = new Sample[2];
            public float denseDustLightingDelta;
            public Color denseDustUnlitPixels;
            public Color denseDustDayPixels;
            public Color denseDustDuskPixels;
            public Color denseDustNightPixels;
            public float dayNightLightingDelta;
            public float nightDustVisibilityDelta;
            public Color neutralLegacyPixels;
            public Color neutralPatchedPixels;
            public float neutralLegacyVisibilityDelta;
            public float neutralReferenceDelta;
            public float neutralReferenceFootprintDelta;
            public float neutralReferenceMaxChannelDelta;
            public int neutralReferenceFootprintPixels;
            public string scope = "Production materials and shared compositing policy, isolated stationary mesh particle and pillar-style MeshRenderer. Preview camera runs the active URP renderer without gameplay atmosphere/DOF. Not an all-technique visual acceptance.";
        }

        [UnityTest]
        public IEnumerator ProductionDustOverlaysCosmeticChipsAndOpaqueWallsOccludeBoth()
        {
#if !UNITY_EDITOR
            Assert.Ignore("This asset-backed rendering regression runs in Editor PlayMode; run it without -nographics.");
            yield break;
#else
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null),
                "This pixel regression needs a graphics device; run Editor PlayMode without -nographics.");
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.InstanceOf<UniversalRenderPipelineAsset>(),
                "The regression must render through the project's URP pipeline.");
            var profile = AssetDatabase.LoadAssetAtPath<EarthEffectsTuningProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null, ProfilePath);
            Assert.That(profile.Materials.ImpactDust, Is.Not.Null);
            Assert.That(profile.Materials.AmbientMotes, Is.Not.Null);
            Assert.That(profile.Materials.ImpactRubble, Is.Not.Null);
            Assert.That(profile.Materials.PillarChips, Is.Not.Null);
            Assert.That(profile.Materials.PillarChips.renderQueue, Is.LessThan(2501),
                "The shared world-stone source must remain opaque.");

            var report = new Report
            {
                utc = DateTime.UtcNow.ToString("O"),
                graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                renderPipeline = GraphicsSettings.currentRenderPipeline.name,
                dustMaterial = profile.Materials.ImpactDust.name,
                ambientMoteMaterial = profile.Materials.AmbientMotes.name
            };
            Assert.That(profile.Materials.ImpactDust.shader.name,
                Is.EqualTo("Elemental/Light Dust Mote"));
            Assert.That(profile.Materials.AmbientMotes.shader.name,
                Is.EqualTo("Elemental/Light Dust Mote"));
            Directory.CreateDirectory(ReportFolder);
            GameObject root = null;
            Mesh chipMesh = null;
            Material chipMaterial = null;
            Material neutralUnlitReference = null;
            var savedAmbientMode = RenderSettings.ambientMode;
            Color savedAmbientLight = RenderSettings.ambientLight;
            Color savedAmbientSky = RenderSettings.ambientSkyColor;
            Color savedAmbientEquator = RenderSettings.ambientEquatorColor;
            Color savedAmbientGround = RenderSettings.ambientGroundColor;
            float savedAmbientIntensity = RenderSettings.ambientIntensity;
            Light savedSun = RenderSettings.sun;
            try
            {
                root = new GameObject("Dust compositing regression") { hideFlags = HideFlags.DontSave };
                chipMesh = CreateChipMesh();
                Camera camera = CreateChild(root, "Compositing camera").AddComponent<Camera>();
                camera.enabled = false;
                camera.cameraType = CameraType.Preview;
                camera.transform.localPosition = new Vector3(0f, 0f, -6f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.16f, 0.32f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = 1.6f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 20f;
                camera.cullingMask = 1 << TestLayer;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.depthTextureMode = DepthTextureMode.Depth;
                UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = false;
                cameraData.renderShadows = false;
                cameraData.requiresDepthTexture = true;
                cameraData.volumeLayerMask = 0;

                Light light = CreateChild(root, "Compositing key light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = Color.white;
                light.intensity = 1.2f;
                light.shadows = LightShadows.None;
                light.cullingMask = 1 << TestLayer;

                ParticleSystem dust = CreateParticles(root, "Production dust");
                EarthParticleSystemTuningApplier.ApplyDust(dust, profile.Impact.Dust, profile.Materials.ImpactDust);
                ConfigureStationary(dust);
                ParticleSystemRenderer dustRenderer = dust.GetComponent<ParticleSystemRenderer>();
                dustRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                dustRenderer.maxParticleSize = 2f;
                dustRenderer.receiveShadows = false;
                EmitStationary(dust, new Vector3(0f, 0f, 0.5f), 2.5f);

                GameObject wall = CreateChild(root, "Opaque world-stone occluder");
                wall.transform.localPosition = new Vector3(0f, 0f, -2f);
                wall.transform.localScale = Vector3.one * 3f;
                wall.AddComponent<MeshFilter>().sharedMesh = chipMesh;
                MeshRenderer wallRenderer = wall.AddComponent<MeshRenderer>();
                wallRenderer.sharedMaterial = profile.Materials.PillarChips;
                wallRenderer.shadowCastingMode = ShadowCastingMode.Off;
                wallRenderer.receiveShadows = false;
                wallRenderer.enabled = false;

                // Let the active SRP and particle buffers initialize before manual captures.
                yield return null;
                for (int index = 0; index < 2; index++)
                {
                    bool meshParticle = index == 0;
                    Material source = meshParticle ? profile.Materials.ImpactRubble : profile.Materials.PillarChips;
                    chipMaterial = new Material(source) { name = source.name + " (Compositing Test)", hideFlags = HideFlags.DontSave };
                    EarthEffectRenderOrder.ConfigureCosmeticMaterial(chipMaterial);
                    GameObject chip;
                    Renderer chipRenderer;
                    if (meshParticle)
                    {
                        ParticleSystem chips = CreateParticles(root, "Mesh particle chip");
                        ConfigureStationary(chips);
                        var particleRenderer = chips.GetComponent<ParticleSystemRenderer>();
                        particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
                        particleRenderer.alignment = ParticleSystemRenderSpace.World;
                        particleRenderer.mesh = chipMesh;
                        particleRenderer.enableGPUInstancing = false;
                        EmitStationary(chips, new Vector3(0f, 0f, -1f), 1f);
                        chip = chips.gameObject;
                        chipRenderer = particleRenderer;
                    }
                    else
                    {
                        chip = CreateChild(root, "Pillar-style mesh chip");
                        chip.transform.localPosition = new Vector3(0f, 0f, -1f);
                        chip.AddComponent<MeshFilter>().sharedMesh = chipMesh;
                        chipRenderer = chip.AddComponent<MeshRenderer>();
                    }
                    EarthEffectRenderOrder.ApplyCosmeticRenderer(chipRenderer, chipMaterial);
                    chipRenderer.receiveShadows = false;
                    yield return null;

                    string prefix = meshParticle ? "MeshParticle" : "PillarMesh";
                    chipRenderer.enabled = false;
                    dustRenderer.enabled = false;
                    Color[] empty = Capture(camera, prefix + "-Empty");
                    dustRenderer.enabled = true;
                    Color[] dustOnly = Capture(camera, prefix + "-DustOnly");
                    dustRenderer.enabled = false;
                    chipRenderer.enabled = true;
                    Color[] chipOnly = Capture(camera, prefix + "-ChipOnly");
                    dustRenderer.enabled = true;
                    Color[] together = Capture(camera, prefix + "-DustOverFrontChip");
                    dustRenderer.enabled = false;
                    chipRenderer.enabled = false;
                    wallRenderer.enabled = true;
                    Color[] wallOnly = Capture(camera, prefix + "-WallOnly");
                    dustRenderer.enabled = true;
                    chipRenderer.enabled = true;
                    Color[] occluded = Capture(camera, prefix + "-WallOccludesBoth");

                    var sample = new Sample
                    {
                        renderer = prefix,
                        sourceMaterial = source.name,
                        shader = source.shader.name,
                        chipOnly = CenterMean(chipOnly),
                        chipWithDust = CenterMean(together),
                        chipVisibleDelta = CenterDifference(empty, chipOnly),
                        dustVisibleDelta = CenterDifference(empty, dustOnly),
                        dustOverChipDelta = CenterDifference(chipOnly, together),
                        opaqueWallOcclusionDelta = CenterDifference(wallOnly, occluded)
                    };
                    report.samples[index] = sample;
                    Assert.That(sample.chipVisibleDelta, Is.GreaterThan(0.015f), prefix + ": chip must actually render.");
                    Assert.That(sample.dustVisibleDelta, Is.GreaterThan(0.015f), prefix + ": production dust must actually render.");
                    Assert.That(sample.dustOverChipDelta, Is.GreaterThan(0.01f),
                        prefix + ": dust behind a cosmetic chip must change the chip's center pixels.");
                    Assert.That(sample.opaqueWallOcclusionDelta, Is.LessThanOrEqualTo(1f / 255f),
                        prefix + ": a closer opaque world-stone surface must occlude both effects.");

                    wallRenderer.enabled = false;
                    UnityEngine.Object.DestroyImmediate(chip);
                    UnityEngine.Object.DestroyImmediate(chipMaterial);
                    chipMaterial = null;
                }

                // Regression for the ordinary non-celestial use case: under a
                // neutral white key, the patched production dust must retain the
                // former Particles/Unlit appearance with the same texture, tint,
                // particle color/alpha and soft-particle geometry.
                camera.backgroundColor = new Color(.16f, .19f, .22f, 1f);
                RenderSettings.ambientMode = AmbientMode.Trilight;
                ApplyLighting(light, 1f, Color.white,
                    new Color(.20f, .20f, .20f), new Color(.16f, .16f, .16f),
                    new Color(.10f, .10f, .10f), 1f);
                yield return null;
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                Assert.That(unlitShader, Is.Not.Null);
                neutralUnlitReference = CreateLegacyUnlitReference(
                    unlitShader, profile.Materials.ImpactDust);
                dustRenderer.enabled = false;
                Color[] neutralBackground = Capture(camera, "Neutral-Background");
                dustRenderer.enabled = true;
                dustRenderer.sharedMaterial = neutralUnlitReference;
                Color[] neutralLegacy = Capture(camera, "Neutral-LegacyUnlit");
                dustRenderer.sharedMaterial = profile.Materials.ImpactDust;
                Color[] neutralPatched = Capture(camera, "Neutral-PatchedLitDust");
                report.neutralLegacyPixels = CenterMean(neutralLegacy);
                report.neutralPatchedPixels = CenterMean(neutralPatched);
                report.neutralLegacyVisibilityDelta = CenterDifference(neutralBackground, neutralLegacy);
                report.neutralReferenceDelta = CenterDifference(neutralLegacy, neutralPatched);
                MeasureForegroundDifference(neutralBackground, neutralLegacy, neutralPatched,
                    out report.neutralReferenceFootprintPixels,
                    out report.neutralReferenceFootprintDelta,
                    out report.neutralReferenceMaxChannelDelta);
                Assert.That(report.neutralLegacyVisibilityDelta, Is.GreaterThan(.015f),
                    "The serialized legacy Particles/Unlit reference must render before it can be used as a baseline.");
                Assert.That(report.neutralReferenceFootprintPixels, Is.GreaterThan(32),
                    "The legacy reference must cover a measurable sprite footprint.");
                Assert.That(report.neutralReferenceDelta, Is.LessThanOrEqualTo(2f / 255f),
                    "Neutral daylight must retain the prior Particles/Unlit dust appearance.");
                Assert.That(report.neutralReferenceFootprintDelta, Is.LessThanOrEqualTo(2f / 255f),
                    "Neutral daylight must retain the prior Particles/Unlit appearance over the whole visible sprite.");
                Assert.That(report.neutralReferenceMaxChannelDelta, Is.LessThanOrEqualTo(2f / 255f),
                    "No visible neutral-daylight dust pixel may drift by more than two 8-bit levels.");
                Assert.That(Mathf.Abs(Luminance(report.neutralLegacyPixels) -
                                      Luminance(report.neutralPatchedPixels)),
                    Is.LessThanOrEqualTo(.01f));

                // The same particles must derive their radiance from the real key
                // light and ambient probe. Counts, density and alpha remain untouched.
                camera.backgroundColor = new Color(.035f, .04f, .05f, 1f);
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.sun = light;
                ParticleSystem.MainModule denseMain = dust.main;
                denseMain.maxParticles = 16;
                for (int i = 0; i < 8; i++) EmitStationary(dust, new Vector3(0, 0, .5f + i * .01f), 2.5f);

                ApplyLighting(light, 1.55f, new Color(1f, .9f, .74f),
                    new Color(.18f, .23f, .31f), new Color(.12f, .105f, .10f),
                    new Color(.045f, .035f, .03f), .82f);
                yield return null;
                Color[] denseDay = Capture(camera, "DenseDust-Day");

                ApplyLighting(light, .34f, new Color(1f, .55f, .30f),
                    new Color(.08f, .075f, .11f), new Color(.055f, .05f, .07f),
                    new Color(.03f, .028f, .045f), .95f);
                yield return null;
                Color[] denseDusk = Capture(camera, "DenseDust-Dusk");

                light.intensity = 0;
                // Exact current production night ambient base/factors.
                Color productionNightAmbient = new Color(.05f, .07f, .12f);
                RenderSettings.ambientSkyColor = productionNightAmbient * 1.55f;
                RenderSettings.ambientEquatorColor = productionNightAmbient * 1.05f;
                RenderSettings.ambientGroundColor = productionNightAmbient * .65f;
                RenderSettings.ambientIntensity = 1.05f;
                DynamicGI.UpdateEnvironment();
                yield return null;
                Color[] denseNight = Capture(camera, "DenseDust-Night");
                dustRenderer.enabled = false;
                Color[] nightEmpty = Capture(camera, "DenseDust-NightEmpty");
                dustRenderer.enabled = true;

                report.denseDustDayPixels = CenterMean(denseDay);
                report.denseDustDuskPixels = CenterMean(denseDusk);
                report.denseDustNightPixels = CenterMean(denseNight);
                report.denseDustUnlitPixels = report.denseDustNightPixels;
                report.dayNightLightingDelta = CenterDifference(denseDay, denseNight);
                report.denseDustLightingDelta = report.dayNightLightingDelta;
                report.nightDustVisibilityDelta = CenterDifference(nightEmpty, denseNight);
                Assert.That(Luminance(report.denseDustDayPixels), Is.GreaterThan(Luminance(report.denseDustDuskPixels) + .015f));
                Assert.That(Luminance(report.denseDustDuskPixels), Is.GreaterThan(Luminance(report.denseDustNightPixels) + .006f));
                Assert.That(report.dayNightLightingDelta, Is.GreaterThan(.025f),
                    "Production dust must not retain equal radiance between day and night.");
                Assert.That(RedShare(report.denseDustDuskPixels), Is.GreaterThan(RedShare(report.denseDustNightPixels) + .025f),
                    "Dusk dust must inherit the warm horizon key light.");
                Assert.That(report.nightDustVisibilityDelta, Is.GreaterThan(.003f),
                    "Night ambient must retain a subtle readable dust silhouette.");
                report.passed = true;
            }
            finally
            {
                RenderSettings.ambientMode = savedAmbientMode;
                RenderSettings.ambientLight = savedAmbientLight;
                RenderSettings.ambientSkyColor = savedAmbientSky;
                RenderSettings.ambientEquatorColor = savedAmbientEquator;
                RenderSettings.ambientGroundColor = savedAmbientGround;
                RenderSettings.ambientIntensity = savedAmbientIntensity;
                RenderSettings.sun = savedSun;
                DynamicGI.UpdateEnvironment();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (chipMaterial != null) UnityEngine.Object.DestroyImmediate(chipMaterial);
                if (neutralUnlitReference != null)
                    UnityEngine.Object.DestroyImmediate(neutralUnlitReference);
                if (chipMesh != null) UnityEngine.Object.DestroyImmediate(chipMesh);
                File.WriteAllText(Path.Combine(ReportFolder, "Latest.json"), JsonUtility.ToJson(report, true));
            }
#endif
        }

        private static GameObject CreateChild(GameObject root, string name)
        {
            var child = new GameObject(name) { layer = TestLayer, hideFlags = HideFlags.DontSave };
            child.transform.SetParent(root.transform, false);
            return child;
        }

        private static ParticleSystem CreateParticles(GameObject root, string name)
        {
            var system = CreateChild(root, name).AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        private static void ConfigureStationary(ParticleSystem system)
        {
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var noise = system.noise; noise.enabled = false;
            var color = system.colorOverLifetime; color.enabled = false;
            var size = system.sizeOverLifetime; size.enabled = false;
            system.useAutoRandomSeed = false;
            system.randomSeed = 41;
        }

        private static void EmitStationary(ParticleSystem system, Vector3 position, float size)
        {
            system.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Vector3.zero,
                startLifetime = 60f,
                startSize = size,
                startColor = Color.white,
                rotation3D = Vector3.zero,
                randomSeed = 41
            }, 1);
            system.Simulate(0.01f, false, false);
            system.Pause(false);
        }

        private static Mesh CreateChipMesh()
        {
            var mesh = new Mesh { name = "Compositing test chip", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(-0.65f, -0.65f, 0f), new Vector3(-0.65f, 0.65f, 0f), new Vector3(0.65f, 0.65f, 0f), new Vector3(0.65f, -0.65f, 0f) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Color[] Capture(Camera camera, string name)
        {
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var pixels = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0, false);
                pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(ReportFolder, name + ".png"), pixels.EncodeToPNG());
                return pixels.GetPixels();
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(pixels);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static Color CenterMean(Color[] pixels)
        {
            Color sum = Color.clear;
            int count = 0;
            for (int y = Resolution / 2 - RoiHalfSize; y < Resolution / 2 + RoiHalfSize; y++)
                for (int x = Resolution / 2 - RoiHalfSize; x < Resolution / 2 + RoiHalfSize; x++)
                {
                    sum += pixels[y * Resolution + x];
                    count++;
                }
            return sum / count;
        }

        private static Material CreateLegacyUnlitReference(Shader unlitShader, Material productionDust)
        {
            // Reconstruct the serialized RumbleDustLit Particles/Unlit state. Do
            // not CopyPropertiesFromMaterial here: productionDust now uses the
            // custom shader, so URP-only hidden vectors such as
            // _SoftParticleFadeParams are not material properties on the source.
            var material = new Material(unlitShader)
            {
                name = "RumbleDustLit legacy neutral reference",
                hideFlags = HideFlags.DontSave,
                renderQueue = 3000,
                enableInstancing = true
            };
            Texture baseMap = productionDust.GetTexture("_BaseMap");
            material.SetTexture("_BaseMap", baseMap);
            material.SetTextureScale("_BaseMap", productionDust.GetTextureScale("_BaseMap"));
            material.SetTextureOffset("_BaseMap", productionDust.GetTextureOffset("_BaseMap"));
            Color baseColor = productionDust.GetColor("_BaseColor");
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_Color", baseColor);
            material.SetColor("_EmissionColor", Color.black);
            material.SetVector("_BaseColorAddSubDiff", new Vector4(-1f, 0f, 0f, 0f));
            material.SetVector("_SoftParticleFadeParams", new Vector4(.12f, .7246377f, 0f, 0f));
            material.SetVector("_CameraFadeParams", new Vector4(0f, float.PositiveInfinity, 0f, 0f));
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_AlphaToMask", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_BlendOp", 0f);
            material.SetFloat("_CameraFadingEnabled", 0f);
            material.SetFloat("_ColorMode", 0f);
            material.SetFloat("_Cull", 2f);
            material.SetFloat("_Cutoff", .5f);
            material.SetFloat("_DstBlend", 10f);
            material.SetFloat("_DstBlendAlpha", 10f);
            material.SetFloat("_FlipbookBlending", 0f);
            material.SetFloat("_SoftParticlesEnabled", 1f);
            material.SetFloat("_SoftParticlesFarFadeDistance", 1.5f);
            material.SetFloat("_SoftParticlesNearFadeDistance", .12f);
            material.SetFloat("_SrcBlend", 5f);
            material.SetFloat("_SrcBlendAlpha", 1f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("SHADOWCASTER", false);
            material.EnableKeyword("_FADING_ON");
            material.EnableKeyword("_SOFTPARTICLES_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return material;
        }

        private static void MeasureForegroundDifference(Color[] background, Color[] reference, Color[] candidate,
            out int foregroundPixels, out float meanChannelDelta, out float maxChannelDelta)
        {
            foregroundPixels = 0;
            float sum = 0f;
            maxChannelDelta = 0f;
            const float visibleThreshold = 1f / 255f;
            for (int i = 0; i < background.Length; i++)
            {
                float referenceVisibility = MaxRgbDifference(background[i], reference[i]);
                float candidateVisibility = MaxRgbDifference(background[i], candidate[i]);
                if (Mathf.Max(referenceVisibility, candidateVisibility) <= visibleThreshold) continue;

                foregroundPixels++;
                float red = Mathf.Abs(reference[i].r - candidate[i].r);
                float green = Mathf.Abs(reference[i].g - candidate[i].g);
                float blue = Mathf.Abs(reference[i].b - candidate[i].b);
                sum += red + green + blue;
                maxChannelDelta = Mathf.Max(maxChannelDelta, red, green, blue);
            }

            meanChannelDelta = foregroundPixels > 0 ? sum / (foregroundPixels * 3f) : float.PositiveInfinity;
        }

        private static float MaxRgbDifference(Color a, Color b) =>
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));

        private static void ApplyLighting(Light light, float intensity, Color color,
            Color sky, Color equator, Color ground, float ambientIntensity)
        {
            light.color = color;
            light.intensity = intensity;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientIntensity = ambientIntensity;
            DynamicGI.UpdateEnvironment();
        }

        private static float Luminance(Color color) =>
            color.r * .2126f + color.g * .7152f + color.b * .0722f;

        private static float RedShare(Color color) =>
            color.r / Mathf.Max(.0001f, color.r + color.g + color.b);

        private static float CenterDifference(Color[] a, Color[] b)
        {
            float sum = 0f;
            int count = 0;
            for (int y = Resolution / 2 - RoiHalfSize; y < Resolution / 2 + RoiHalfSize; y++)
                for (int x = Resolution / 2 - RoiHalfSize; x < Resolution / 2 + RoiHalfSize; x++)
                {
                    int index = y * Resolution + x;
                    sum += Mathf.Abs(a[index].r - b[index].r) + Mathf.Abs(a[index].g - b[index].g) + Mathf.Abs(a[index].b - b[index].b);
                    count += 3;
                }
            return sum / count;
        }
    }
}
