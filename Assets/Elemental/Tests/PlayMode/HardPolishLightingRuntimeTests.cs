#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Elemental.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Elemental.Tests.PlayMode
{
    // Deliberately isolated lighting: no Volume, atmosphere feature or production asset writes.
    public sealed class HardPolishLightingRuntimeTests
    {
        [Serializable] private sealed class Sample
        {
            public string shader, mode;
            public Color mainOnly, bothDirectionals, withPoint, nightClock;
            public Color hiddenPoint, matchingWithoutPoint, hiddenDirectional, matchingDirectional;
            public bool renderingLayersEnabled;
            public uint ordinaryReceiverMask = 1, stageReceiverMask = MatchPresentationStage.StageRenderingLayer, stageLightMask = MatchPresentationStage.StageRenderingLayer;
            public uint definedRenderingLayers;
        }
        [Serializable] private sealed class Report
        {
            public string utc, gpu, graphicsApi;
            public List<Sample> samples = new List<Sample>();
        }

        [UnityTest]
        public IEnumerator MaterialFamiliesReceiveMoonAndPointInForwardAndForwardPlus()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null),
                "This is rendered evidence; run without -nographics.");
            uint definedLayers=RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue();
            Assert.That(definedLayers&MatchPresentationStage.StageRenderingLayer,Is.Not.Zero,
                "URP masks all lights against named project layers. Run the G08 installer to define the actual stage bit before testing isolation.");
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString() };
            const string folder = "BuildReports/HardPolish/G02";
            Directory.CreateDirectory(folder);
            RenderPipelineAsset oldQuality = QualitySettings.renderPipeline, oldDefault = GraphicsSettings.defaultRenderPipeline;
            RenderTexture oldActive = RenderTexture.active;
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.CreateScene("HardPolish lighting isolation");
            SceneManager.SetActiveScene(scene);
            Light[] existingLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            bool[] enabled = existingLights.Select(x => x.enabled).ToArray();
            foreach (Light light in existingLights) light.enabled = false;
            float oldNight = Shader.GetGlobalFloat("_ElementalNight01");
            Vector4 oldPlanet = Shader.GetGlobalVector("_ElementalPlanetCenterRadius");
            var owned = new List<Object>();
            var pipelines = new List<UniversalRenderPipelineAsset>();
            var renderers = new List<UniversalRendererData>();
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.black;
                RenderSettings.ambientIntensity = 0;
                RenderSettings.ambientProbe = new SphericalHarmonicsL2();
                RenderSettings.fog = false;
                var root = new GameObject("Lighting evidence"); owned.Add(root);
                var camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                camera.transform.position = new Vector3(0, 0, -3); camera.transform.rotation = Quaternion.identity;
                camera.orthographic = true; camera.orthographicSize = 1.2f; camera.nearClipPlane = .1f;
                camera.farClipPlane = 20; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black; camera.cullingMask = 1 << 31; camera.enabled = false;
                camera.allowHDR = true; camera.allowMSAA = false;
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false; data.requiresDepthTexture = true;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.transform.SetParent(root.transform);
                quad.layer = 31; Object.Destroy(quad.GetComponent<Collider>());
                MeshRenderer surface = quad.GetComponent<MeshRenderer>();
                Mesh mesh = Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh); owned.Add(mesh);
                mesh.colors = Enumerable.Repeat(Color.white, mesh.vertexCount).ToArray();
                quad.GetComponent<MeshFilter>().sharedMesh = mesh;
                Light main = MakeLight(root, "Main", LightType.Directional, Color.red, 1);
                Light moon = MakeLight(root, "Moon", LightType.Directional, Color.blue, 1);
                Light point = MakeLight(root, "Point", LightType.Point, Color.green, .5f);
                point.transform.position = new Vector3(0, 0, -1.5f); point.range = 5;
                RenderSettings.sun = main;
                Shader.SetGlobalVector("_ElementalPlanetCenterRadius", new Vector4(0, -100, 0, 100));
                string[] names = { "Elemental/Environment/DistantStoneURP", "Elemental/Graphics V5/Rumble Rock Lit", "Elemental/Light Dust Mote" };
                foreach (RenderingMode mode in new[] { RenderingMode.Forward, RenderingMode.ForwardPlus })
                {
                    var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    renderer.name = "Lighting evidence " + mode; renderer.renderingMode = mode; renderers.Add(renderer);
                    var pipeline = UniversalRenderPipelineAsset.Create(renderer); pipelines.Add(pipeline);
                    var serialized = new SerializedObject(pipeline);
                    serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
                    serialized.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
                    serialized.FindProperty("m_SupportsLightLayers").boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    Assert.That(pipeline.useRenderingLayers, Is.True);
                    pipeline.maxAdditionalLightsCount = 8;
                    QualitySettings.renderPipeline = pipeline; GraphicsSettings.defaultRenderPipeline = pipeline;
                    yield return null;
                    foreach (string name in names)
                    {
                        Shader shader = Shader.Find(name); Assert.That(shader, Is.Not.Null, name);
                        var material = new Material(shader); owned.Add(material);
                        material.SetColor("_BaseColor", new Color(.6f,.6f,.6f,1));
                        if (material.HasProperty("_Brightness")) material.SetFloat("_Brightness", 1);
                        if (material.HasProperty("_SurfaceWisp")) material.SetFloat("_SurfaceWisp", 0);
                        if (material.HasProperty("_DetailStrength")) material.SetFloat("_DetailStrength", 0);
                        // A centered quad has no cylindrical side normal. This
                        // fixture measures radiance against its authored normal.
                        if (material.HasProperty("_SideShadingSmoothness")) material.SetFloat("_SideShadingSmoothness", 0);
                        surface.sharedMaterial = material;
                        // Global sun/moon must illuminate either receiver mask, so a changed
                        // main light cannot masquerade as successful point-light isolation.
                        main.GetUniversalAdditionalLightData().renderingLayers = -1;
                        moon.GetUniversalAdditionalLightData().renderingLayers = -1;
                        point.GetUniversalAdditionalLightData().renderingLayers = (int)MatchPresentationStage.StageRenderingLayer;
                        surface.renderingLayerMask = 1;
                        moon.enabled = false; point.enabled = false;
                        Shader.SetGlobalFloat("_ElementalNight01", 0);
                        yield return null;
                        var sample = new Sample { shader = name, mode = mode.ToString(), renderingLayersEnabled = pipeline.useRenderingLayers, definedRenderingLayers=definedLayers };
                        report.samples.Add(sample);
                        sample.mainOnly = Capture(camera);
                        moon.enabled = true;
                        yield return null;
                        sample.bothDirectionals = Capture(camera);
                        point.enabled = true;
                        yield return null;
                        sample.hiddenPoint = Capture(camera);
                        Assert.That(Vector3.Distance(Rgb(sample.hiddenPoint), Rgb(sample.bothDirectionals)), Is.LessThan(.005f),
                            name + " " + mode + " isolated stage point leaked onto ordinary receiver mask 1.");
                        point.enabled = false; surface.renderingLayerMask = MatchPresentationStage.StageRenderingLayer;
                        yield return null;
                        sample.matchingWithoutPoint = Capture(camera);
                        Assert.That(Vector3.Distance(Rgb(sample.matchingWithoutPoint), Rgb(sample.bothDirectionals)), Is.LessThan(.005f),
                            name + " " + mode + " global sun/moon changed with receiver layer: " + sample.bothDirectionals + " to " + sample.matchingWithoutPoint);
                        point.enabled = true;
                        yield return null;
                        sample.withPoint = Capture(camera);
                        Shader.SetGlobalFloat("_ElementalNight01", 1);
                        yield return null;
                        sample.nightClock = Capture(camera);

                        float blue = sample.bothDirectionals.b - sample.mainOnly.b;
                        float green = sample.withPoint.g - sample.matchingWithoutPoint.g;
                        Assert.That(blue, Is.GreaterThan(.05f), name + " " + mode + " lost the non-main directional.");
                        Assert.That(green, Is.GreaterThan(.005f), name + " " + mode + " lost punctual lighting.");
                        Assert.That(Mathf.Abs(sample.bothDirectionals.r - sample.mainOnly.r), Is.LessThan(.015f),
                            "Adding the moon must not duplicate the red main light.");
                        if (name == names[2])
                        {
                            Assert.That(Vector3.Distance(Rgb(sample.nightClock), Rgb(sample.withPoint)), Is.LessThan(.005f),
                                "The clock alone must not re-expose dust while actual environment radiance stays fixed.");
                            Assert.That(sample.nightClock.a, Is.EqualTo(sample.withPoint.a).Within(.001f));
                        }
                        // Exercise the additional directional path too: Forward+ handles it
                        // separately from punctual lights, but must apply the same mask filter.
                        Shader.SetGlobalFloat("_ElementalNight01", 0);
                        point.enabled = false; moon.GetUniversalAdditionalLightData().renderingLayers = (int)MatchPresentationStage.StageRenderingLayer;
                        surface.renderingLayerMask = 1;
                        yield return null;
                        sample.hiddenDirectional = Capture(camera);
                        Assert.That(Vector3.Distance(Rgb(sample.hiddenDirectional), Rgb(sample.mainOnly)), Is.LessThan(.005f),
                            name + " " + mode + " non-main directional leaked onto the other receiver mask.");
                        surface.renderingLayerMask = MatchPresentationStage.StageRenderingLayer;
                        yield return null;
                        sample.matchingDirectional = Capture(camera);
                        Assert.That(sample.matchingDirectional.b - sample.mainOnly.b, Is.GreaterThan(.05f),
                            name + " " + mode + " matching additional directional was incorrectly excluded.");
                        Assert.That(Mathf.Abs(sample.matchingDirectional.r - sample.mainOnly.r), Is.LessThan(.015f),
                            "The global main light must survive both rendering-layer tests.");
                        moon.GetUniversalAdditionalLightData().renderingLayers = -1;
                        var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
                        Assert.That(errors, Is.Empty, name + " rendered variant compiler errors.");
                    }
                }
                for (int i = 0; i < names.Length; i++)
                {
                    Sample forward = report.samples[i], clustered = report.samples[i + names.Length];
                    Vector3 a = Rgb(forward.bothDirectionals) - Rgb(forward.mainOnly);
                    Vector3 b = Rgb(clustered.bothDirectionals) - Rgb(clustered.mainOnly);
                    Assert.That(Vector3.Distance(a,b), Is.LessThan(.02f), names[i] + " Forward+ repeats or loses the moon.");
                }
            }
            finally
            {
                File.WriteAllText(Path.Combine(folder,"TwoLightModes.json"),JsonUtility.ToJson(report,true));
                QualitySettings.renderPipeline = oldQuality; GraphicsSettings.defaultRenderPipeline = oldDefault;
                RenderTexture.active = oldActive;
                Shader.SetGlobalFloat("_ElementalNight01",oldNight); Shader.SetGlobalVector("_ElementalPlanetCenterRadius",oldPlanet);
                for (int i=0;i<existingLights.Length;i++) if(existingLights[i]!=null) existingLights[i].enabled=enabled[i];
                SceneManager.SetActiveScene(previous);
                foreach(Object item in owned) if(item!=null) Object.Destroy(item);
                foreach(var pipeline in pipelines) Object.Destroy(pipeline);
                foreach(var renderer in renderers) Object.Destroy(renderer);
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        private static Light MakeLight(GameObject root,string name,LightType type,Color color,float intensity)
        {
            var light=new GameObject(name).AddComponent<Light>(); light.transform.SetParent(root.transform);
            light.type=type; light.color=color; light.intensity=intensity; light.shadows=LightShadows.None;
            light.cullingMask=1<<31; light.transform.rotation=Quaternion.identity; return light;
        }
        private static Vector3 Rgb(Color c) => new Vector3(c.r,c.g,c.b);
        private static Color Capture(Camera camera)
        {
            var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
            var pixels=new Texture2D(64,64,TextureFormat.RGBAFloat,false,true);
            RenderTexture previous=RenderTexture.active;
            try
            {
                target.Create();
                var request=new UniversalRenderPipeline.SingleCameraRequest { destination=target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera,request),Is.True);
                RenderPipeline.SubmitRenderRequest(camera,request); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,64,64),0,0); pixels.Apply(false,false);
                Color sum=Color.clear;
                for(int y=30;y<34;y++) for(int x=30;x<34;x++) sum+=pixels.GetPixel(x,y);
                return sum/16f;
            }
            finally { RenderTexture.active=previous; target.Release(); Object.Destroy(target); Object.Destroy(pixels); }
        }
    }
}
#endif
