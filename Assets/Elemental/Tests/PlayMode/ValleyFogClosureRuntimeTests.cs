#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Elemental.Tests.PlayMode
{
    public sealed class ValleyFogClosureRuntimeTests
    {
        private const string ScenePath="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string Folder="BuildReports/ValleyFogClosure";

        [UnityTest]
        public IEnumerator ProductionFogFullyClosesFarSourceAndPreservesNearContrast()
        {
            var previous=SceneManager.GetActiveScene();
            Scene scene=SceneManager.GetSceneByPath(ScenePath);
            bool loadedHere=!scene.IsValid()||!scene.isLoaded;
            AsyncOperation unloadOperation=null;
            GameObject cameraObject=null,target=null;Material material=null;
            ValleyAtmosphereController owner=null;
            bool oldFog=false,oldClouds=false,oldArt=false,oldMotion=false;int oldDebug=0;
            float oldSeismic=Shader.GetGlobalFloat("_EarthSeismicVision");
            try
            {
                if(loadedHere)
                {
                    yield return SceneManager.LoadSceneAsync(ScenePath,LoadSceneMode.Additive);
                    scene=SceneManager.GetSceneByPath(ScenePath);
                }
                SceneManager.SetActiveScene(scene);
                var gate=Find<EarthSceneReadinessGate>(scene);
                Assert.That(gate,Is.Not.Null);
                double deadline=Time.realtimeSinceStartupAsDouble+130;
                while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(gate.IsReady,Is.True,gate.Status);
                owner=Find<ValleyAtmosphereController>(scene);
                Assert.That(owner,Is.Not.Null);
                oldFog=owner.FogEnabled;oldClouds=owner.CloudsEnabled;oldArt=owner.FarArtEnabled;
                oldMotion=owner.AnimateClouds;oldDebug=owner.DebugMode;
                var celestial=Find<CelestialSystemBehaviour>(scene);
                Assert.That(celestial,Is.Not.Null);
                var source=celestial.TargetCamera;
                Assert.That(source,Is.Not.Null,"Use the celestial owner's actual gameplay camera, not the first scene camera.");
                int layer=UnusedRendererLayer();
                cameraObject=new GameObject("Fog closure isolated production renderer camera");
                var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(source);
                camera.enabled=false;camera.targetTexture=null;camera.cullingMask=1<<layer;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.nearClipPlane=.1f;camera.farClipPlane=6000;camera.orthographic=false;
                camera.fieldOfView=60;camera.aspect=640f/360f;camera.allowMSAA=false;
                var data=camera.GetUniversalAdditionalCameraData();data.SetRenderer(0);
                Assert.That(data.scriptableRenderer,Is.SameAs(source.GetUniversalAdditionalCameraData().scriptableRenderer),
                    "QA must use the same URP renderer as the actual production camera.");
                data.renderPostProcessing=false;data.antialiasing=AntialiasingMode.None;data.dithering=false;
                data.requiresDepthTexture=true;data.renderType=CameraRenderType.Base;
                float radius=Shader.GetGlobalVector("_ElementalPlanetCenterRadius").w;
                Assert.That(radius,Is.GreaterThan(0));
                camera.transform.SetPositionAndRotation(owner.transform.TransformPoint(new Vector3(0,radius+20,0)),owner.transform.rotation);
                var shader=Shader.Find("Universal Render Pipeline/Unlit");Assert.That(shader,Is.Not.Null);
                material=new Material(shader);material.SetColor("_BaseColor",Color.black);
                target=GameObject.CreatePrimitive(PrimitiveType.Quad);target.name="Fog closure opaque reference";target.layer=layer;
                Object.DestroyImmediate(target.GetComponent<Collider>());
                target.GetComponent<Renderer>().sharedMaterial=material;
                target.transform.rotation=camera.transform.rotation;
                target.transform.localScale=new Vector3(12000,12000,1);
                target.transform.position=camera.transform.position+camera.transform.forward*4000;
                owner.CloudsEnabled=false;owner.AnimateClouds=false;owner.FarArtEnabled=true;owner.DebugMode=0;
                Shader.SetGlobalFloat("_EarthSeismicVision",0);
                owner.FogEnabled=false;owner.Publish();
                material.SetColor("_BaseColor",Color.black);var farOffBlack=Capture(camera,"far-off-black");
                material.SetColor("_BaseColor",Color.white);var farOffWhite=Capture(camera,"far-off-white");
                long farOff=CentreDifference(farOffBlack,farOffWhite);
                Assert.That(farOff,Is.GreaterThan(100000),"Control must prove that actual opaque geometry and its material reach the production pass.");
                owner.FogEnabled=true;owner.Publish();
                material.SetColor("_BaseColor",Color.black);var farBlack=Capture(camera,"far-closed-black");
                material.SetColor("_BaseColor",Color.white);var farWhite=Capture(camera,"far-closed-white");
                long closed=CentreDifference(farBlack,farWhite);
                target.transform.position=camera.transform.position+camera.transform.forward*4500;
                var farOtherDepth=Capture(camera,"far-closed-other-depth");
                long hiddenDepth=CentreDifference(farWhite,farOtherDepth);
                target.transform.position=camera.transform.position+camera.transform.forward*20;
                target.transform.localScale=new Vector3(40,40,1);
                material.SetColor("_BaseColor",Color.black);var nearBlack=Capture(camera,"near-black");
                material.SetColor("_BaseColor",Color.white);var nearWhite=Capture(camera,"near-white");
                long near=CentreDifference(nearBlack,nearWhite);
                // Original top-cap coverage missed the near actor under the
                // equator/underside seal. Probe the same production pass there.
                foreach(float height in new[]{0f,-radius-20f})
                {
                    camera.transform.position=owner.transform.TransformPoint(new Vector3(0,height,0));
                    target.transform.position=camera.transform.position+camera.transform.forward*6f;
                    material.SetColor("_BaseColor",Color.black);var localBlack=Capture(camera,"near-radial-"+height+"-black");
                    material.SetColor("_BaseColor",Color.white);var localWhite=Capture(camera,"near-radial-"+height+"-white");
                    Assert.That(CentreDifference(localBlack,localWhite),Is.GreaterThan(700000),
                        "Immediate opaque geometry must retain contrast below the top cap at height "+height);
                }
                target.transform.position=camera.transform.position+camera.transform.forward*80f;
                target.transform.localScale=new Vector3(200,200,1);
                material.SetColor("_BaseColor",Color.black);var undersideBlack=Capture(camera,"underside-80m-black");
                material.SetColor("_BaseColor",Color.white);var undersideWhite=Capture(camera,"underside-80m-white");
                Assert.That(CentreDifference(undersideBlack,undersideWhite),Is.Zero,
                    "The distant lower-planet seal must remain fully opaque.");
                File.WriteAllText(Folder+"/evidence.txt","Production URP render request, 640x360, central32x32 RGB absolute differences.\n"+
                    "Far fog-off contrast="+farOff+"\nFar closed source difference="+closed+
                    "\nClosed 4000m/4500m depth difference="+hiddenDepth+"\nNear protected contrast="+near+
                    "\nGPU="+SystemInfo.graphicsDeviceName+";API="+SystemInfo.graphicsDeviceType);
                Assert.That(closed,Is.Zero,"Fully closed fog must be independent of hidden black/white material, including far artwork.");
                Assert.That(hiddenDepth,Is.Zero,"Fully closed fog must not reveal the hidden surface through its depth-dependent palette.");
                Assert.That(near,Is.GreaterThan(700000),"Near playable geometry must preserve almost all unlit black/white contrast.");
            }
            finally
            {
                if(owner!=null)
                {
                    owner.FogEnabled=oldFog;owner.CloudsEnabled=oldClouds;owner.FarArtEnabled=oldArt;
                    owner.AnimateClouds=oldMotion;owner.DebugMode=oldDebug;owner.Publish();
                }
                Shader.SetGlobalFloat("_EarthSeismicVision",oldSeismic);
                if(target!=null)Object.DestroyImmediate(target);
                if(material!=null)Object.DestroyImmediate(material);
                if(cameraObject!=null)Object.DestroyImmediate(cameraObject);
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                if(loadedHere&&scene.IsValid()&&scene.isLoaded)
                {
                    unloadOperation=SceneManager.UnloadSceneAsync(scene);
                    if(unloadOperation!=null)unloadOperation.completed+=_=>
                    {
                        if(previous.IsValid()&&previous.isLoaded)
                        {
                            var sky=Find<CelestialSystemBehaviour>(previous);if(sky!=null)sky.EvaluatePresentationForQa();
                            var priorOwner=Find<ValleyAtmosphereController>(previous);if(priorOwner!=null)priorOwner.Publish();
                        }
                        Shader.SetGlobalFloat("_EarthSeismicVision",oldSeismic);
                    };
                }
            }
            if(unloadOperation!=null)yield return unloadOperation;
        }
        private static int UnusedRendererLayer()
        {
            var renderers=Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            for(int layer=31;layer>=8;layer--)if(!renderers.Any(r=>r.gameObject.layer==layer))return layer;
            Assert.Fail("No unused renderer layer for isolated fog proof.");return 31;
        }
        private static T Find<T>(Scene scene)where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).FirstOrDefault();
        private static long CentreDifference(Color32[] a,Color32[] b)
        {
            long sum=0;
            for(int y=164;y<196;y++)for(int x=304;x<336;x++)
            {int i=y*640+x;sum+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);}
            return sum;
        }
        private static Color32[] Capture(Camera camera,string name)
        {
            Directory.CreateDirectory(Folder);
            var rt=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);
            var image=new Texture2D(640,360,TextureFormat.RGB24,false);var active=RenderTexture.active;
            try
            {
                rt.Create();var request=new RenderPipeline.StandardRequest{destination=rt};
                Assert.That(RenderPipeline.SupportsRenderRequest(camera,request),Is.True);
                RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply(false,false);
                File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());return image.GetPixels32();
            }
            finally{RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
#endif
