#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Elemental.Tests.PlayMode
{
    public sealed class HardPolishProductionTemporalTests
    {
        private const string Folder="BuildReports/HardPolish/G01/ProductionTemporal";
        private const int W=1920,H=1080,SW=320,SH=180,N=24;
        private Scene scene,previous;
        private ProductionCaptureResolution resolution;
        private float oldScale;
        private Camera camera;
        private ScriptableRendererFeature ssao,bokeh;
        private EarthCinematicDepthOfFieldController dofController;
        private bool oldCaptureOverride;private EarthCinematicDepthOfFieldDebugView oldCaptureDebug;
        private bool oldSsao,oldBokeh,featuresCaptured,cameraCaptured;
        private readonly List<Object> owned=new();
        private readonly Dictionary<Behaviour,bool> behaviours=new();
        private Vector3 cameraPosition;private Quaternion cameraRotation;
        private float fov;
        [Serializable] private sealed class Provenance{public int id;public string renderer,meshGuid,fractureGuid,materialGuid;public int vertices,triangles;}
        [Serializable] private sealed class Pair{public string mode,motion,before,after;public float seconds,meanInteriorDelta,maxRendererMean;public int classifiedPixels,suspectRenderer;public bool ssaoActive,bokehActive,controllerEnabled;public float nativeDofRadius;}
        [Serializable] private sealed class Evidence
        {
            public string rendererPath,rendererGuid,gpu,api;
            public bool originalSsao,originalProjectDof,originalMiniBokeh,originalDofRuntimeActive;
            public string configuration="Four explicit SSAO/project-local cinematic DOF combinations using the existing controller capture override. Not a change to shipped defaults; dormant legacy MiniBokeh remains untouched.";
            public string meaning="Actual saved EarthCoreSlice and its bound camera/URP renderer (ADR0032 project-local DOF; not dormant legacy MiniBokeh), actual released fracture pieces arranged into a controlled24-piece pile with original meshes/materials. Four actual renderer-feature modes, each10seconds for camera orbit and object motion.16 native UI-inclusive frames are repeated identical-pose pairs after motion. Interior renderer-ID mask is a separate diagnostic depth pass, not a replacement production material. Differences are time/history-dependent radiance, not proof of geometry flicker; animated lighting/fog/transparency may contribute. No raw moving-image delta is classified as flicker. Zero pair delta does not certify every motion frame.";
            public List<Provenance> pieces=new();public List<Pair> pairs=new();
        }
        private T[] All<T>()where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        [UnityTest,Timeout(600000)] public IEnumerator SavedRendererSsaoAndProjectDofTenSecondRubbleMotionPairs()
        {
            oldScale=Time.timeScale;previous=SceneManager.GetActiveScene();resolution=new ProductionCaptureResolution();
            const string scenePath="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Assert.That(SceneManager.GetSceneByPath(scenePath).isLoaded,Is.False);
            yield return SceneManager.LoadSceneAsync(scenePath,LoadSceneMode.Additive);scene=SceneManager.GetSceneByPath(scenePath);SceneManager.SetActiveScene(scene);
            var flow=All<FrontendFlowController>().Single();double end=Time.realtimeSinceStartupAsDouble+150;
            while((!flow.IsWorldReady||flow.State==FrontendState.Loading)&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Assert.That(flow.BeginBot(),Is.True);end=Time.realtimeSinceStartupAsDouble+150;
            while(flow.State!=FrontendState.Combat&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Assert.That(flow.State,Is.EqualTo(FrontendState.Combat));
            var duel=flow.MatchController;camera=All<CelestialSystemBehaviour>().Single().TargetCamera;
            yield return resolution.WaitForRenderedSize(camera);
            var data=camera.GetUniversalAdditionalCameraData();var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            var serializedPipeline=new SerializedObject(pipeline);int rendererIndex=new SerializedObject(data).FindProperty("m_RendererIndex").intValue;
            if(rendererIndex<0)rendererIndex=serializedPipeline.FindProperty("m_DefaultRendererIndex").intValue;
            var rendererData=(ScriptableRendererData)serializedPipeline.FindProperty("m_RendererDataList").GetArrayElementAtIndex(rendererIndex).objectReferenceValue;
            Assert.That(pipeline.GetRenderer(rendererIndex),Is.SameAs(data.scriptableRenderer));
            ssao=rendererData.rendererFeatures.SingleOrDefault(f=>f!=null&&f.GetType().Name=="ScreenSpaceAmbientOcclusion");
            bokeh=rendererData.rendererFeatures.SingleOrDefault(f=>f is EarthCinematicDepthOfFieldFeature);
            var legacyMini=rendererData.rendererFeatures.SingleOrDefault(f=>f!=null&&f.GetType().Name=="MiniBokehFeature");
            Assert.That(ssao,Is.Not.Null,"Actual selected renderer has no SSAO feature.");Assert.That(bokeh,Is.Not.Null,"Actual selected renderer has no project-local cinematic DOF feature (ADR0032).");
            oldSsao=ssao.isActive;oldBokeh=bokeh.isActive;featuresCaptured=true;
            dofController=camera.GetComponent<EarthCinematicDepthOfFieldController>();Assert.That(dofController,Is.Not.Null);
            oldCaptureOverride=dofController.HasCaptureOverride;oldCaptureDebug=dofController.CaptureDebugView;
            bool originalRuntime=dofController.IsRuntimeActive;
            dofController.SetCaptureOverride(true);
            Assert.That(dofController.TryGetRenderSettings(out var initialDof),Is.True,"Existing capture override must activate the real project DOF with valid subject bindings.");
            cameraPosition=camera.transform.position;cameraRotation=camera.transform.rotation;fov=camera.fieldOfView;cameraCaptured=true;
            PauseBehaviour(camera.GetComponent<CinemachineBrain>());
            PauseBehaviour(camera.GetComponent<Elemental.Presentation.Camera.EarthCameraDirector>());
            foreach(var bot in All<EarthMvpBotController>())PauseBehaviour(bot);
            Vector3 up=duel.PlayerTransform.up,forward=Vector3.ProjectOnPlane(duel.PlayerTransform.forward,up).normalized;
            Vector3 right=Vector3.Cross(up,forward).normalized;Vector3 center=duel.PlayerTransform.position+forward*5;
            var report=new Evidence{originalSsao=oldSsao,originalProjectDof=oldBokeh,originalMiniBokeh=legacyMini!=null&&legacyMini.isActive,originalDofRuntimeActive=originalRuntime,rendererPath=AssetDatabase.GetAssetPath(rendererData),rendererGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(rendererData)),gpu=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString()};
            var pieces=new List<MeshRenderer>();
            foreach(var structure in All<EarthArenaStructure>().Where(s=>s.isActiveAndEnabled&&s.OrdinaryDamageEnabled).OrderBy(s=>(s.transform.position-center).sqrMagnitude))
            {
                structure.SetMagicDisassemblyProgress(1,structure.transform.position,up);
                var serialized=new SerializedObject(structure);var source=serialized.FindProperty("fractureAssetObject").objectReferenceValue;
                var array=serialized.FindProperty("pieces");
                for(int i=0;i<array.arraySize&&pieces.Count<N;i++)
                {
                    var t=array.GetArrayElementAtIndex(i).objectReferenceValue as Transform;if(t==null)continue;
                    var renderer=t.GetComponent<MeshRenderer>();var mesh=t.GetComponent<MeshFilter>();var body=t.GetComponent<Rigidbody>();
                    if(renderer==null||mesh==null||mesh.sharedMesh==null||body==null||!renderer.enabled||!t.gameObject.activeInHierarchy)continue;
                    body.isKinematic=true;body.interpolation=RigidbodyInterpolation.None;pieces.Add(renderer);
                    report.pieces.Add(new Provenance{id=pieces.Count,renderer=Hierarchy(t),meshGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mesh.sharedMesh)),fractureGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)),materialGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer.sharedMaterial)),vertices=mesh.sharedMesh.vertexCount,triangles=mesh.sharedMesh.triangles.Length/3});
                }
                if(pieces.Count==N)break;
            }
            Assert.That(pieces.Count,Is.EqualTo(N),"Fixture must use actually released production pieces, never fabricated geometry.");
            Time.timeScale=0;
            var positions=new Vector3[N];var rotations=new Quaternion[N];
            for(int i=0;i<N;i++)
            {
                positions[i]=center+right*((i%6-2.5f)*.50f)+forward*((i/6%2-.5f)*.50f)+up*(.35f+i/12*.45f);
                rotations[i]=pieces[i].transform.rotation;pieces[i].transform.position=positions[i];
            }
            var idMaterials=new Material[N+1];var shader=Shader.Find("Hidden/Elemental/QA/TemporalRendererId");Assert.That(shader,Is.Not.Null);
            for(int i=0;i<=N;i++){idMaterials[i]=Own(new Material(shader));idMaterials[i].SetColor("_TemporalRendererId",new Color(i*7/255f,0,0,1));}
            var idTarget=Own(new RenderTexture(SW,SH,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear));idTarget.Create();
            var small=Own(new RenderTexture(SW,SH,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear));small.Create();
            var readable=Own(new Texture2D(SW,SH,TextureFormat.RGBA32,false,true));
            var sceneRenderers=All<Renderer>().Where(r=>r is MeshRenderer||r is SkinnedMeshRenderer).ToArray();
            Directory.CreateDirectory(Folder);camera.fieldOfView=50;
            for(int mode=0;mode<4;mode++)for(int motion=0;motion<2;motion++)
            {
                ssao.SetActive(mode!=1&&mode!=3);bokeh.SetActive(mode!=2&&mode!=3);
                string label=mode==0?"explicit-ssao-and-project-dof":mode==1?"explicit-ssao-off":mode==2?"explicit-project-dof-off":"explicit-both-off";
                double start=Time.realtimeSinceStartupAsDouble;float age;
                do
                {
                    age=(float)(Time.realtimeSinceStartupAsDouble-start);
                    Vector3 viewDirection=Quaternion.AngleAxis(motion==0?age*36:0,up)*forward;
                    camera.transform.SetPositionAndRotation(center-viewDirection*4.8f+up*1.8f,Quaternion.LookRotation(viewDirection-up*.22f,up));
                    for(int i=0;i<N;i++){pieces[i].transform.position=positions[i]+(motion==1?up*(Mathf.Sin(age*1.7f+i)*.06f):Vector3.zero);pieces[i].transform.rotation=motion==1?Quaternion.AngleAxis(Mathf.Sin(age+i)*9,up)*rotations[i]:rotations[i];}
                    yield return new WaitForEndOfFrame();
                }while(age<10);
                // Hold every measured object/camera pose over real frames; settle focus after motion before pair.
                for(int frame=0;frame<12;frame++)yield return new WaitForEndOfFrame();
                Vector3 heldCamera=camera.transform.position;Quaternion heldRotation=camera.transform.rotation;
                var heldPieces=pieces.Select(p=>p.transform.localToWorldMatrix).ToArray();
                string stem=label+(motion==0?"-camera-orbit":"-object-motion");
                var image=ScreenCapture.CaptureScreenshotAsTexture();Assert.That(image.width,Is.EqualTo(W));Assert.That(image.height,Is.EqualTo(H));
                File.WriteAllBytes(Folder+"/"+stem+"-a.png",image.EncodeToPNG());var a=ReadSmall(image,small,readable);Object.Destroy(image);
                yield return new WaitForEndOfFrame();
                Assert.That(camera.transform.position,Is.EqualTo(heldCamera));Assert.That(camera.transform.rotation,Is.EqualTo(heldRotation));
                for(int i=0;i<N;i++)Assert.That(pieces[i].transform.localToWorldMatrix,Is.EqualTo(heldPieces[i]),"Another transform owner invalidated a repeated-pose pair.");
                image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Folder+"/"+stem+"-b.png",image.EncodeToPNG());var b=ReadSmall(image,small,readable);Object.Destroy(image);
                var ids=RenderIds(camera,sceneRenderers,pieces,idMaterials,idTarget,readable);
                File.WriteAllBytes(Folder+"/"+stem+"-ids.png",readable.EncodeToPNG());
                var pair=new Pair{mode=label,motion=motion==0?"camera":"objects",seconds=age,before=stem+"-a.png",after=stem+"-b.png",ssaoActive=ssao.isActive,bokehActive=bokeh.isActive,controllerEnabled=dofController.enabled,nativeDofRadius=dofController.TryGetRenderSettings(out var currentDof)?currentDof.MaxRadiusPixels:0};
                Attribute(a,b,ids,pair);
                report.pairs.Add(pair);File.WriteAllText(Folder+"/evidence.json",JsonUtility.ToJson(report,true));
                Assert.That(pair.classifiedPixels,Is.GreaterThan(20),"Repeated pair has insufficient visible interior rubble coverage; inspect the saved diagnostic -ids.png and evidence.json.");
            }
        }
        private void PauseBehaviour(Behaviour value){if(value==null)return;behaviours[value]=value.enabled;value.enabled=false;}
        private T Own<T>(T value)where T:Object{owned.Add(value);return value;}
        private static string Hierarchy(Transform t)=>t.parent==null?t.name:Hierarchy(t.parent)+"/"+t.name;
        private static Color32[] ReadSmall(Texture source,RenderTexture target,Texture2D readable)
        {var previous=RenderTexture.active;try{Graphics.Blit(source,target);RenderTexture.active=target;readable.ReadPixels(new Rect(0,0,SW,SH),0,0);readable.Apply(false,false);return readable.GetPixels32();}finally{RenderTexture.active=previous;}}
        private static Color32[] RenderIds(Camera camera,Renderer[] renderers,List<MeshRenderer> pieces,Material[] materials,RenderTexture target,Texture2D readable)
        {
            using(var command=new CommandBuffer())
            {
                command.SetRenderTarget(target);command.SetViewport(new Rect(0,0,SW,SH));command.ClearRenderTarget(true,true,Color.black);
                command.SetGlobalMatrix("_TemporalViewProjection",GL.GetGPUProjectionMatrix(camera.projectionMatrix,true)*camera.worldToCameraMatrix);
                command.SetViewProjectionMatrices(camera.worldToCameraMatrix,GL.GetGPUProjectionMatrix(camera.projectionMatrix,true));
                foreach(var renderer in renderers)
                {
                    if(!renderer.enabled||renderer.forceRenderingOff||!renderer.gameObject.activeInHierarchy||(camera.cullingMask&(1<<renderer.gameObject.layer))==0)continue;
                    int id=renderer is MeshRenderer mesh?pieces.IndexOf(mesh)+1:0;var slots=renderer.sharedMaterials;
                    for(int sub=0;sub<slots.Length;sub++)
                    {
                        // Transparent flame/cloud cards must not become opaque black occluders
                        // in this categorical pass. Match the opaque production depth domain.
                        if(slots[sub]==null||slots[sub].renderQueue>2500||slots[sub].GetTag("RenderType",false,"")=="Transparent")continue;
                        command.DrawRenderer(renderer,materials[id],sub,0);
                    }
                }
                Graphics.ExecuteCommandBuffer(command);
            }
            var previous=RenderTexture.active;try{RenderTexture.active=target;readable.ReadPixels(new Rect(0,0,SW,SH),0,0);readable.Apply(false,false);return readable.GetPixels32();}finally{RenderTexture.active=previous;}
        }
        private static void Attribute(Color32[] a,Color32[] b,Color32[] ids,Pair pair)
        {
            var sum=new float[N+1];var count=new int[N+1];
            for(int y=2;y<SH-2;y++)for(int x=2;x<SW-2;x++)
            {
                int p=y*SW+x,id=Mathf.RoundToInt(ids[p].r/7f);if(id<1||id>N||ids[p].g>1||ids[p].b>1)continue;bool interior=true;
                for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)if(Mathf.RoundToInt(ids[p+dy*SW+dx].r/7f)!=id)interior=false;
                if(!interior)continue;float delta=(Mathf.Abs(a[p].r-b[p].r)+Mathf.Abs(a[p].g-b[p].g)+Mathf.Abs(a[p].b-b[p].b))/(3f*255);
                sum[id]+=delta;count[id]++;pair.meanInteriorDelta+=delta;pair.classifiedPixels++;
            }
            if(pair.classifiedPixels>0)pair.meanInteriorDelta/=pair.classifiedPixels;
            for(int id=1;id<=N;id++)if(count[id]>=4&&sum[id]/count[id]>pair.maxRendererMean){pair.maxRendererMean=sum[id]/count[id];pair.suspectRenderer=id;}
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(featuresCaptured){ssao.SetActive(oldSsao);bokeh.SetActive(oldBokeh);}
            if(dofController!=null)dofController.SetCaptureOverride(oldCaptureOverride,oldCaptureDebug);
            if(camera!=null&&cameraCaptured){camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);camera.fieldOfView=fov;}
            foreach(var pair in behaviours)if(pair.Key!=null)pair.Key.enabled=pair.Value;behaviours.Clear();
            resolution?.Dispose();resolution=null;Time.timeScale=oldScale;
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            foreach(var item in owned)if(item!=null)Object.Destroy(item);owned.Clear();
        }
    }
}
#endif
