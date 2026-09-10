#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.Geometry;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Elemental.Tests.PlayMode
{
    public sealed class HardPolishRockOrbitTests
    {
        private const string Folder="BuildReports/HardPolish/G01/Orbit";
        private readonly List<Object> owned=new List<Object>();
        private Scene preview;
        [Serializable] private sealed class Sample
        {public string mesh,sourceGuid,mode,image,sha256;public int bearing;public double captureCpuMilliseconds;}
        [Serializable] private sealed class Report
        {
            public string utc,gpu,graphicsApi,bakeProof;
            public string limitation="Fixed LOD geometry diagnostic in isolated preview scene. Geometric-normal mode disables only radial side shading; lit modes restore the authored smoothing value. Contact AO is the material AO path, not an isolated renderer SSAO test. Local URP DOF is not production MiniBokeh. Capture CPU includes rendering/readback/PNG; not a gameplay performance measurement.";
            public int samples;public List<Sample> frames=new List<Sample>();
        }
        [UnityTest,Timeout(900000)]
        public IEnumerator FixedLodDenseOrbitSeparatesUnlitNormalsShadowsContactAoAndLocalDof()
        {
            string proof="BuildReports/HardPolish/G01/Bake.json";
            Assert.That(File.Exists(proof),Is.True,"Run targeted G01 bake before orbit evidence.");
            string bakeProof=File.ReadAllText(proof);
            Assert.That(bakeProof,Does.Contain("\"published\": true"));
            Directory.CreateDirectory(Folder);
            var report=new Report{utc=DateTime.UtcNow.ToString("O"),gpu=SystemInfo.graphicsDeviceName,
                graphicsApi=SystemInfo.graphicsDeviceType.ToString(),bakeProof=bakeProof};
            preview=EditorSceneManager.NewPreviewScene();
            var camera=Create("G01 diagnostic camera").AddComponent<Camera>();
            camera.enabled=false;camera.scene=preview;camera.cullingMask=1<<29;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.09f,.11f,1);
            camera.fieldOfView=38;camera.nearClipPlane=.02f;camera.farClipPlane=100;camera.useOcclusionCulling=false;
            var cameraData=camera.GetUniversalAdditionalCameraData();cameraData.volumeLayerMask=1<<29;
            cameraData.antialiasing=AntialiasingMode.None;cameraData.dithering=false;cameraData.renderShadows=true;
            var light=Create("G01 fixed directional light").AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=1.2f;light.cullingMask=1<<29;
            light.transform.rotation=Quaternion.Euler(48,-35,0);light.shadowBias=.025f;light.shadowNormalBias=.2f;
            var authored=AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/GraphicsV5/Materials/RumbleSandstone.mat");
            Assert.That(authored,Is.Not.Null);
            Material lit=Own(new Material(authored));lit.SetFloat("_UsePlanetFrame",0);
            Assert.That(lit.HasProperty("_SideShadingSmoothness"),Is.True);
            float authoredSideSmoothing=lit.GetFloat("_SideShadingSmoothness");
            Shader unlitShader=Shader.Find("Universal Render Pipeline/Unlit");Assert.That(unlitShader,Is.Not.Null);
            Material unlit=Own(new Material(unlitShader));unlit.SetColor("_BaseColor",Color.white);
            var volume=Create("G01 local DOF diagnostic").AddComponent<Volume>();
            volume.isGlobal=true;volume.priority=10000;
            var volumeProfile=Own(ScriptableObject.CreateInstance<VolumeProfile>());volume.sharedProfile=volumeProfile;
            var dof=Own(volumeProfile.Add<DepthOfField>(true));dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.aperture.Override(2);dof.focalLength.Override(100);
            var rock=Create("G01 source mesh");var filter=rock.AddComponent<MeshFilter>();var renderer=rock.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(floor,preview);floor.layer=29;
            Object.DestroyImmediate(floor.GetComponent<Collider>());floor.transform.position=new Vector3(0,-.12f,0);
            floor.transform.localScale=new Vector3(15,.2f,15);floor.GetComponent<MeshRenderer>().sharedMaterial=lit;
            string[] sources={"V5_Boulder_00","V5_Slab_08","V5_Wedge_12","V5_Pebble_17","Pillar_77131"};
            string[] modes={"unlit","geometric-normals","lit-no-shadows-no-contact-ao","lit-shadows-no-contact-ao","lit-shadows-contact-ao","lit-local-dof"};
            foreach(string source in sources)
            {
                Mesh mesh;string guid;
                if(source.StartsWith("Pillar"))
                {
                    mesh=Own(RumbleRockMeshFactory.Build(RumbleRockMeshFactory.CreateDefaultRecipe(77131,RumbleRockFamily.Pillar,1.15f)));
                    guid="generated:77131:Pillar:1.15";
                }
                else
                {
                    string path="Assets/Elemental/Content/GraphicsV5/Rocks/"+source+".asset";
                    mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);guid=AssetDatabase.AssetPathToGUID(path);
                }
                Assert.That(mesh,Is.Not.Null);
                Assert.That(RumbleRockMeshFactory.Validate(mesh,out string reason),Is.True,reason);
                filter.sharedMesh=mesh;
                Vector3 subject=mesh.bounds.center;
                float distance=Mathf.Max(mesh.bounds.size.magnitude*1.7f,1);
                foreach(string mode in modes)
                {
                    floor.SetActive(mode!="unlit"&&mode!="geometric-normals");
                    renderer.sharedMaterial=mode=="unlit"?unlit:lit;
                    lit.SetFloat("_DebugMode",mode=="geometric-normals"?2:0);
                    lit.SetFloat("_SideShadingSmoothness",mode=="geometric-normals"?0:authoredSideSmoothing);
                    Assert.That(lit.GetFloat("_SideShadingSmoothness"),Is.EqualTo(mode=="geometric-normals"?0:authoredSideSmoothing));
                    lit.SetFloat("_OcclusionStrength",mode=="lit-shadows-contact-ao"||mode=="lit-local-dof"?1:0);
                    light.shadows=mode=="lit-no-shadows-no-contact-ao"?LightShadows.None:LightShadows.Soft;
                    cameraData.renderPostProcessing=mode=="lit-local-dof";
                    for(int bearing=0;bearing<360;bearing+=10)
                    {
                        float elevation=(bearing%90==0&&(mode=="unlit"||mode=="geometric-normals")?-8:18)*Mathf.Deg2Rad;
                        Vector3 radial=new Vector3(Mathf.Sin(bearing*Mathf.Deg2Rad)*Mathf.Cos(elevation),Mathf.Sin(elevation),Mathf.Cos(bearing*Mathf.Deg2Rad)*Mathf.Cos(elevation));
                        camera.transform.SetPositionAndRotation(subject+radial*distance,Quaternion.LookRotation(-radial,Vector3.up));
                        dof.focusDistance.Override(distance);
                        yield return null;
                        string relative=source+"-"+mode+"-"+bearing.ToString("000")+".png";
                        var timer=Stopwatch.StartNew();byte[] png=Capture(camera);timer.Stop();
                        File.WriteAllBytes(Folder+"/"+relative,png);
                        using(var sha=SHA256.Create())
                            report.frames.Add(new Sample{mesh=source,sourceGuid=guid,mode=mode,bearing=bearing,image=relative,
                                sha256=BitConverter.ToString(sha.ComputeHash(png)).Replace("-","").ToLowerInvariant(),captureCpuMilliseconds=timer.Elapsed.TotalMilliseconds});
                        report.samples++;
                    }
                    File.WriteAllText(Folder+"/Report.json",JsonUtility.ToJson(report,true));
                }
            }
            Assert.That(report.samples,Is.EqualTo(1080));
        }
        private GameObject Create(string name)
        {
            var item=new GameObject(name);item.layer=29;SceneManager.MoveGameObjectToScene(item,preview);return item;
        }
        private T Own<T>(T item) where T:Object{owned.Add(item);return item;}
        private static byte[] Capture(Camera camera)
        {
            var target=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32);
            var texture=new Texture2D(768,768,TextureFormat.RGB24,false);
            RenderTexture active=RenderTexture.active;
            try
            {
                var request=new RenderPipeline.StandardRequest{destination=target};
                Assert.That(RenderPipeline.SupportsRenderRequest(camera,request),Is.True);
                RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,768,768),0,0);texture.Apply(false,false);
                Color32[] pixels=texture.GetPixels32();int minimum=255,maximum=0;
                for(int i=0;i<pixels.Length;i+=17)
                {minimum=Math.Min(minimum,Math.Min(pixels[i].r,Math.Min(pixels[i].g,pixels[i].b)));maximum=Math.Max(maximum,Math.Max(pixels[i].r,Math.Max(pixels[i].g,pixels[i].b)));}
                Assert.That(maximum-minimum,Is.GreaterThan(24),"Diagnostic capture is blank or lacks visible geometry.");
                return texture.EncodeToPNG();
            }
            finally{RenderTexture.active=active;Object.DestroyImmediate(texture);target.Release();Object.DestroyImmediate(target);}
        }
        [TearDown] public void Cleanup()
        {
            if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);
            foreach(Object item in owned)if(item!=null)Object.DestroyImmediate(item);
            owned.Clear();
        }
    }
}
#endif
