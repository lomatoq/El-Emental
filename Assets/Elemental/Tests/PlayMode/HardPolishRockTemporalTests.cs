#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Runtime.Geometry;
using Elemental.Presentation.Rendering;
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
    public sealed class HardPolishRockTemporalTests
    {
        private const string Folder="BuildReports/HardPolish/G01/Temporal";
        private const int Width=1920,Height=1080,SmallWidth=160,SmallHeight=90,Count=36;
        [Serializable] private sealed class Rock
        {public int id;public string source,sourceGuid;public uint seed,fingerprint;public Vector3 restPosition;public Quaternion restRotation;[NonSerialized]public Mesh mesh;[NonSerialized]public MeshRenderer renderer;}
        [Serializable] private sealed class Frame
        {public Vector3 cameraPosition;public Quaternion cameraRotation;public Vector3[] rockPositions;public Quaternion[] rockRotations;public int index;public float seconds,deltaSeconds,movingImageDelta;public bool repeatedPose;public float repeatedPoseMean,repeatedPoseMaximum;public int suspectRendererId,classifiedPixels;public string beforeImage,afterImage;}
        [Serializable] private sealed class Track
        {public string motion,mode;public List<Frame> frames=new();public float worstRepeatedPose;public int worstFrame,worstRendererId;}
        [Serializable] private sealed class Report
        {
            public string utc,gpu,pipeline,bakeProof;
            public string interpretation="36 production baked rocks in an isolated preview scene, rendered through the active pipeline at 1920x1080. Raw moving-image differences are expected motion, not flicker. Repeated-pose probes hold every rock/camera/light pose across a real frame boundary; eroded ID pixels attribute change to visible renderers. Stable mesh fingerprints and validated closedness rule out changed CPU geometry only. A high repeated-pose signal implicates time/history-dependent rendering; a zero signal does not prove all temporal stability. Material AO and local URP DOF are not isolated production SSAO/MiniBokeh.";
            public List<Rock> rocks=new();public List<Track> tracks=new();
        }
        private readonly List<Object> owned=new();private Scene scene;
        private readonly List<Vector3> vertices=new(8192);private readonly List<int> indices=new(16384);
        [UnityTest,Timeout(900000)]
        public IEnumerator DenseRubbleTenSecondMotionAndRepeatedPoseAttribution()
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            string proof="BuildReports/HardPolish/G01/Bake.json";Assert.That(File.Exists(proof),Is.True);string bake=File.ReadAllText(proof);Assert.That(bake,Does.Contain("\"published\": true"));
            Directory.CreateDirectory(Folder);
            var report=new Report{utc=DateTime.UtcNow.ToString("O"),gpu=SystemInfo.graphicsDeviceName,pipeline=GraphicsSettings.currentRenderPipeline.name,bakeProof=bake};
            scene=EditorSceneManager.NewPreviewScene();
            var camera=Create("Temporal camera").AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.cullingMask=1<<29;camera.fieldOfView=42;camera.nearClipPlane=.02f;camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.10f,.14f);camera.aspect=(float)Width/Height;
            var data=camera.GetUniversalAdditionalCameraData();data.antialiasing=AntialiasingMode.None;data.dithering=false;data.volumeLayerMask=1<<29;
            var light=Create("Fixed temporal key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.cullingMask=1<<29;light.transform.rotation=Quaternion.Euler(48,-35,0);
            var authored=AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/GraphicsV5/Materials/RumbleSandstone.mat");Assert.That(authored,Is.Not.Null);
            var lit=Own(new Material(authored));lit.SetFloat("_UsePlanetFrame",0);float smoothing=lit.GetFloat("_SideShadingSmoothness");
            var unlit=Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")));unlit.SetColor("_BaseColor",Color.white);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(floor,scene);floor.layer=29;Object.DestroyImmediate(floor.GetComponent<Collider>());floor.transform.position=new Vector3(0,-.2f,0);floor.transform.localScale=new Vector3(15,.2f,15);floor.GetComponent<MeshRenderer>().sharedMaterial=lit;
            var volume=Create("Temporal local DOF").AddComponent<Volume>();volume.isGlobal=true;volume.priority=10000;var volumeProfile=Own(ScriptableObject.CreateInstance<VolumeProfile>());volume.sharedProfile=volumeProfile;
            var dof=volumeProfile.Add<DepthOfField>(true);dof.mode.Override(DepthOfFieldMode.Bokeh);dof.aperture.Override(2);dof.focalLength.Override(75);
            string[] paths=AssetDatabase.FindAssets("t:Mesh",new[]{"Assets/Elemental/Content/GraphicsV5/Rocks"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal).ToArray();Assert.That(paths.Length,Is.GreaterThanOrEqualTo(20));
            for(int i=0;i<Count;i++)
            {
                string path=paths[i%paths.Length];Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);Assert.That(RumbleRockMeshFactory.Validate(mesh,out string reason),Is.True,reason);
                uint seed=(uint)(77131+i*7919);float phase=seed*.001f;var item=Create("Rubble renderer "+(i+1));item.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=item.AddComponent<MeshRenderer>();renderer.sharedMaterial=lit;
                Vector3 position=new Vector3((i%6-2.5f)*.68f,(i/18)*.50f+.28f,(i/6%3-1)*.68f);Quaternion rotation=Quaternion.Euler(Mathf.Sin(phase)*18,seed%360,Mathf.Cos(phase)*16);
                item.transform.SetPositionAndRotation(position,rotation);item.transform.localScale=Vector3.one*(.85f/Mathf.Max(.001f,mesh.bounds.size.magnitude));
                report.rocks.Add(new Rock{id=i+1,source=path,sourceGuid=AssetDatabase.AssetPathToGUID(path),seed=seed,fingerprint=Fingerprint(mesh),mesh=mesh,renderer=renderer,restPosition=position,restRotation=rotation});
            }
            var full=Own(new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB));full.Create();
            var small=Own(new RenderTexture(SmallWidth,SmallHeight,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear));small.Create();
            var readback=Own(new Texture2D(SmallWidth,SmallHeight,TextureFormat.RGBA32,false,true));
            var block=new MaterialPropertyBlock();
            try
            {
                foreach(string motion in new[]{"camera-orbit-static-rubble","fixed-camera-moving-rubble"})
                foreach(string mode in new[]{"unlit","geometric-normals","lit-no-shadows-no-ao","lit-shadows-no-ao","lit-shadows-ao","lit-local-dof"})
                {
                    var track=new Track{motion=motion,mode=mode};report.tracks.Add(track);string prefix=motion+"-"+mode;
                    Material material=mode=="unlit"?unlit:lit;
                    lit.SetFloat("_DebugMode",mode=="geometric-normals"?2:0);lit.SetFloat("_SideShadingSmoothness",mode=="geometric-normals"?0:smoothing);
                    lit.SetFloat("_OcclusionStrength",mode=="lit-shadows-ao"||mode=="lit-local-dof"?1:0);
                    bool showFloor=mode!="unlit"&&mode!="geometric-normals";floor.SetActive(showFloor);light.shadows=mode=="lit-no-shadows-no-ao"?LightShadows.None:LightShadows.Soft;
                    data.renderPostProcessing=mode=="lit-local-dof";foreach(var rock in report.rocks){rock.renderer.sharedMaterial=material;rock.renderer.SetPropertyBlock(null);}
                    double start=Time.realtimeSinceStartupAsDouble;float previousTime=0,nextProbe=0;int frame=0;Color32[] previous=null;
                    while(true)
                    {
                        float elapsed=Mathf.Min(10,(float)(Time.realtimeSinceStartupAsDouble-start));
                        Vector3 focus=new Vector3(0,.7f,0);float orbit=motion.StartsWith("camera")?elapsed*36:25;
                        camera.transform.SetPositionAndRotation(focus+Quaternion.Euler(0,orbit,0)*new Vector3(0,3,6),Quaternion.identity);camera.transform.LookAt(focus);dof.focusDistance.Override(Vector3.Distance(camera.transform.position,focus));
                        foreach(var rock in report.rocks)
                        {
                            bool moving=motion.StartsWith("fixed");float phase=rock.seed*.001f;
                            rock.renderer.transform.SetPositionAndRotation(rock.restPosition+(moving?new Vector3(.08f*Mathf.Sin(elapsed*.8f+phase),.07f*Mathf.Sin(elapsed+phase),.06f*Mathf.Cos(elapsed*.7f+phase)):Vector3.zero),
                                moving?Quaternion.AngleAxis(18*Mathf.Sin(elapsed*.7f+phase),Vector3.up)*rock.restRotation:rock.restRotation);
                        }
                        Submit(camera,full);Color32[] pixels=ReadSmall(full,small,readback);
                        var row=new Frame{cameraPosition=camera.transform.position,cameraRotation=camera.transform.rotation,rockPositions=report.rocks.Select(r=>r.renderer.transform.position).ToArray(),rockRotations=report.rocks.Select(r=>r.renderer.transform.rotation).ToArray(),index=frame,seconds=elapsed,deltaSeconds=elapsed-previousTime,movingImageDelta=previous==null?0:Difference(previous,pixels)};track.frames.Add(row);
                        if(frame==0)Save(full,prefix+"-first.png");
                        if(elapsed>=nextProbe)
                        {
                            nextProbe+=2;row.repeatedPose=true;
                            Texture2D before=ReadFull(full);
                            try
                            {
                                yield return null; // Actual history/time advances; all explicit scene poses remain identical.
                                Submit(camera,full);Color32[] repeated=ReadSmall(full,small,readback);
                                bool post=data.renderPostProcessing;data.renderPostProcessing=false;floor.SetActive(false);camera.backgroundColor=Color.black;
                                foreach(var rock in report.rocks){rock.renderer.sharedMaterial=unlit;block.Clear();block.SetColor("_BaseColor",new Color(rock.id/255f,0,0,1));rock.renderer.SetPropertyBlock(block);}
                                Submit(camera,small);Color32[] ids=ReadSmall(small,small,readback);
                                foreach(var rock in report.rocks){rock.renderer.sharedMaterial=material;rock.renderer.SetPropertyBlock(null);Assert.That(Fingerprint(rock.mesh),Is.EqualTo(rock.fingerprint),"Geometry changed during track: renderer "+rock.id);}
                                data.renderPostProcessing=post;floor.SetActive(showFloor);camera.backgroundColor=new Color(.08f,.10f,.14f);
                                Attribute(pixels,repeated,ids,row);
                                Assert.That(row.classifiedPixels,Is.GreaterThan(16),"Object-ID probe did not identify enough interior pixels; this is missing evidence, not zero flicker.");
                                if(frame==0||row.repeatedPoseMaximum>track.worstRepeatedPose)
                                {
                                    foreach(var previousRow in track.frames){previousRow.beforeImage=null;previousRow.afterImage=null;}
                                    track.worstRepeatedPose=row.repeatedPoseMaximum;track.worstFrame=frame;track.worstRendererId=row.suspectRendererId;
                                    row.beforeImage=prefix+"-worst-before.png";row.afterImage=prefix+"-worst-after.png";
                                    File.WriteAllBytes(Folder+"/"+row.beforeImage,before.EncodeToPNG());Save(full,row.afterImage);
                                }
                            }
                            finally{Object.DestroyImmediate(before);}
                        }
                        previous=pixels;previousTime=elapsed;frame++;
                        if(elapsed>=10){Save(full,prefix+"-last.png");break;}
                        yield return null;
                    }
                    Assert.That(track.frames.Count,Is.GreaterThan(10),"Too few frames for a temporal diagnostic; do not call this ten seconds of usable motion evidence.");
                    File.WriteAllText(Folder+"/Report.json",JsonUtility.ToJson(report,true));
                }
            }
            finally{File.WriteAllText(Folder+"/Report.json",JsonUtility.ToJson(report,true));}
        }
        private static void Attribute(Color32[] a,Color32[] b,Color32[] ids,Frame frame)
        {
            float[] sum=new float[Count+1];int[] count=new int[Count+1];float total=0;
            for(int y=1;y<SmallHeight-1;y++)for(int x=1;x<SmallWidth-1;x++)
            {
                int index=y*SmallWidth+x,id=ids[index].r;if(id<1||id>Count||ids[index].g>1||ids[index].b>1)continue;bool interior=true;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(ids[index+dy*SmallWidth+dx].r!=id||ids[index+dy*SmallWidth+dx].g>1||ids[index+dy*SmallWidth+dx].b>1)interior=false;
                if(!interior)continue;float difference=(Mathf.Abs(a[index].r-b[index].r)+Mathf.Abs(a[index].g-b[index].g)+Mathf.Abs(a[index].b-b[index].b))/(3f*255);
                sum[id]+=difference;count[id]++;total+=difference;frame.classifiedPixels++;
            }
            frame.repeatedPoseMean=frame.classifiedPixels>0?total/frame.classifiedPixels:0;
            for(int id=1;id<=Count;id++)if(count[id]>=4&&sum[id]/count[id]>frame.repeatedPoseMaximum){frame.repeatedPoseMaximum=sum[id]/count[id];frame.suspectRendererId=id;}
        }
        private uint Fingerprint(Mesh mesh)
        {uint hash=2166136261;mesh.GetVertices(vertices);foreach(var vertex in vertices){hash=(hash^(uint)vertex.x.GetHashCode())*16777619;hash=(hash^(uint)vertex.y.GetHashCode())*16777619;hash=(hash^(uint)vertex.z.GetHashCode())*16777619;}for(int sub=0;sub<mesh.subMeshCount;sub++){mesh.GetIndices(indices,sub);foreach(int index in indices)hash=(hash^(uint)index)*16777619;}return hash;}
        private static float Difference(Color32[] a,Color32[] b){long total=0;for(int i=0;i<a.Length;i++)total+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);return total/(a.Length*3f*255);}
        private static void Submit(Camera camera,RenderTexture target){var request=new RenderPipeline.StandardRequest{destination=target};Assert.That(RenderPipeline.SupportsRenderRequest(camera,request),Is.True);RenderPipeline.SubmitRenderRequest(camera,request);}
        private static Color32[] ReadSmall(RenderTexture source,RenderTexture target,Texture2D texture)
        {var old=RenderTexture.active;try{if(source!=target)Graphics.Blit(source,target);RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,SmallWidth,SmallHeight),0,0);texture.Apply(false,false);return texture.GetPixels32();}finally{RenderTexture.active=old;}}
        private static Texture2D ReadFull(RenderTexture source){var texture=new Texture2D(Width,Height,TextureFormat.RGB24,false,false);var old=RenderTexture.active;try{RenderTexture.active=source;texture.ReadPixels(new Rect(0,0,Width,Height),0,0);texture.Apply(false,false);return texture;}finally{RenderTexture.active=old;}}
        private static void Save(RenderTexture source,string name){var texture=ReadFull(source);try{File.WriteAllBytes(Folder+"/"+name,texture.EncodeToPNG());}finally{Object.DestroyImmediate(texture);}}
        private GameObject Create(string name){var item=new GameObject(name);item.layer=29;SceneManager.MoveGameObjectToScene(item,scene);return item;}
        private T Own<T>(T value) where T:Object{owned.Add(value);return value;}
        [TearDown] public void Cleanup(){if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);foreach(var item in owned)if(item!=null)Object.DestroyImmediate(item);owned.Clear();}
    }
}
#endif
