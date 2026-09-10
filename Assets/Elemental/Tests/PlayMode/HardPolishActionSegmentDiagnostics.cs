#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Presentation.Animation;
using Elemental.Presentation.UI;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Elemental.Presentation.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Elemental.Tests.PlayMode
{
    /// <summary>Source comparison only: never poses the gameplay actors or installs a candidate clip.</summary>
    public sealed class HardPolishActionSegmentDiagnostics
    {
        private Scene productionScene,previousScene;
        [UnityTearDown] public IEnumerator RestoreProductionScene()
        {
            if(previousScene.IsValid()&&previousScene.isLoaded)SceneManager.SetActiveScene(previousScene);
            if(productionScene.IsValid()&&productionScene.isLoaded)yield return SceneManager.UnloadSceneAsync(productionScene);
        }
        private const string Folder="BuildReports/HardPolish/G04/Segments";
        private const string Controller="Assets/Elemental/Content/Animation/KayKitMage.controller";
        private static readonly HumanBodyBones[] Bones={HumanBodyBones.Hips,HumanBodyBones.Chest,HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,
            HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.Spine};
        [Serializable] private sealed class Frame
        {public float seconds;public Vector3[] positions;public Quaternion[] rotations;}
        [Serializable] private sealed class Segment
        {
            public string name,clip,path,sourceGuid;public float sourceSeconds,start,end,nativeSeconds;
            public bool compositeLoop;public float maxBoneStepDegrees,maxHandSpeed,peakSeconds,peakHalfHeightSeconds;
            public float loopPositionGap,loopRotationGap,loopVelocityGap;
            public int minimumDistinctPeakImages=16;public int minimumDistinctPeakGeometry=16;public float peakSourceSeconds,peakSourceNormalized;public float[] peakSheetSeconds;
            public List<Frame> frames=new();
            [NonSerialized] public AnimationClip asset;
        }
        [Serializable] private sealed class Boundary
        {public string from,to;public float maximumPositionGap,maximumRotationGap,maximumVelocityGap;}
        [Serializable] private sealed class Report
        {
            public string actorScene,actorName,avatarPath;public string[] bodyMaterials;public string utc;public string[] boneOrder=Bones.Select(b=>b.ToString()).ToArray();
            public string limitation="Native 60 Hz actual stone skeletons and full-weight cast + authored additive loop only. Every sample is CPU-baked into a reused mesh with original materials; isolated captures disable post-processing and temporarily lease atmosphere/fog off, then restore before returning. Geometry and image variation are checked independently. No gameplay IK, inertia, locomotion mask or contact events. These are comparisons, not approved segment bindings or complete G04 acceptance. View sheets in index order at their declared native duration; production 1x continuous tests remain required.";
            public float heavyNormalizedSpeed,heavyStrikeLowerBoundSeconds,heavyRecoveryLowerBoundSeconds;
            public bool heavyTimingFitsPlan;
            public List<Segment> segments=new();public List<Boundary> boundaries=new();
        }
        [UnityTest]
        public IEnumerator NativeEightSegmentComparisonAndHeavyThrowCandidateSheets()
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            Directory.CreateDirectory(Folder);
            var profile=AssetDatabase.LoadAssetAtPath<EarthMagicMotionProfile>("Assets/Elemental/Content/Profiles/EarthMagicMotionProfile.asset");
            Assert.That(profile,Is.Not.Null);Assert.That(profile.Validate(out string error),Is.True,error);
            var tree=AssetDatabase.LoadAllAssetsAtPath(Controller).OfType<BlendTree>().First(t=>t.name=="Earth Curated Casts");
            AnimationClip Slot(int index)=>(AnimationClip)tree.children.First(c=>c.directBlendParameter=="EarthPoseA"+index.ToString("00")).motion;
            AnimationClip loop=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Elemental/Content/Animation/Earth Living Hold.anim");
            Assert.That(loop,Is.Not.Null);Assert.That(loop.isLooping,Is.True);
            var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Elemental/Content/Animation/Earth Living Hold.mask");Assert.That(mask,Is.Not.Null);
            var report=new Report{utc=DateTime.UtcNow.ToString("O")};
            Segment Add(string name,AnimationClip clip,float start,float end,bool hold=false)
            {
                Assert.That(clip,Is.Not.Null);Assert.That(clip.humanMotion,Is.True,name);
                string path=AssetDatabase.GetAssetPath(clip);
                var item=new Segment{name=name,asset=clip,clip=clip.name,path=path,sourceGuid=AssetDatabase.AssetPathToGUID(path),sourceSeconds=clip.length,start=start,end=end,
                    compositeLoop=hold,nativeSeconds=hold?loop.length:(end-start)*clip.length};report.segments.Add(item);return item;
            }
            var gravity=profile.Find(3).timing;var fire=profile.Find(6).timing;var heavy=profile.Find(4).timing;
            Add("gravity-acquire",Slot(3),0,gravity.Contact);Add("gravity-living-hold",Slot(3),gravity.Contact,gravity.Contact,true);
            Add("gravity-release",Slot(3),gravity.Contact,gravity.RecoverEnd);
            Add("heavy-shipped-release",Slot(4),heavy.LoadEnd,heavy.RecoverEnd);
            Add("fire-brace",Slot(6),0,fire.Contact);Add("fire-living-hold",Slot(6),fire.Contact,fire.Contact,true);
            Add("fire-release",Slot(6),fire.Contact,fire.RecoverEnd);
            var candidate=AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/Mixamo/X Bot@Standing 2H Magic Attack 03.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            Add("heavy-candidate-2H-attack03",candidate,0,1);
            foreach (var owned in new[]{
                new[]{"heavy-candidate-2H-attack05","Assets/ThirdParty/Mixamo/X Bot@Standing 2H Magic Attack 05.fbx"},
                new[]{"heavy-candidate-2H-area02","Assets/ThirdParty/Mixamo/X Bot@Standing 2H Magic Area Attack 02.fbx"},
                new[]{"heavy-candidate-fast-rock-punch","Assets/ThirdParty/Mixamo/X Bot@ Fast Rock Punch.fbx"}})
            {
                var clip=AssetDatabase.LoadAllAssetsAtPath(owned[1]).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
                Add(owned[0],clip,0,1);
            }
            report.heavyNormalizedSpeed=EarthMagicClipClock.MaximumSpeedForSlot(4);
            report.heavyStrikeLowerBoundSeconds=(heavy.Contact-heavy.LoadEnd)/report.heavyNormalizedSpeed;
            report.heavyRecoveryLowerBoundSeconds=(heavy.RecoverEnd-heavy.Sustain)/report.heavyNormalizedSpeed;
            report.heavyTimingFitsPlan=report.heavyStrikeLowerBoundSeconds<=.09f&&report.heavyRecoveryLowerBoundSeconds<=.22f;
            const string productionPath="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            previousScene=SceneManager.GetActiveScene();
            Assert.That(SceneManager.GetSceneByPath(productionPath).isLoaded,Is.False,"Use an isolated production test launcher.");
            yield return SceneManager.LoadSceneAsync(productionPath,LoadSceneMode.Additive);
            productionScene=SceneManager.GetSceneByPath(productionPath);
            var flow=productionScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FrontendFlowController>(true)).Single();
            var gate=productionScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EarthSceneReadinessGate>(true)).Single();
            double deadline=Time.realtimeSinceStartupAsDouble+145;
            while((!gate.IsReady||flow.MatchController==null||flow.MatchController.PlayerTransform==null||
                flow.MatchController.PlayerTransform.GetComponentInChildren<Animator>()==null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);
            yield return null; // Let the saved avatar/material presentation complete its startup before copying.
            Transform productionActor=flow.MatchController.PlayerTransform;Assert.That(productionActor,Is.Not.Null);
            Animator productionAnimator=productionActor.GetComponentInChildren<Animator>();
            Assert.That(productionAnimator!=null&&productionAnimator.isHuman&&productionAnimator.avatar!=null,Is.True);
            report.actorScene=productionPath;report.actorName=productionActor.name;
            report.avatarPath=AssetDatabase.GetAssetPath(productionAnimator.avatar);
            Scene scene=SceneManager.CreateScene("G04 source diagnostic");GameObject actor=null,cameraObject=null,lightObject=null;
            PlayableGraph graph=default;BakedComparisonSkin[] baked=Array.Empty<BakedComparisonSkin>();
            try
            {
                actor=CloneProductionRenderRig(productionActor,productionAnimator,out Animator animator);SceneManager.MoveGameObjectToScene(actor,scene);
                report.bodyMaterials=actor.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Select(m=>AssetDatabase.GetAssetPath(m)+" | "+m.name+" | "+m.shader.name).Distinct().ToArray();
                foreach(var item in actor.GetComponentsInChildren<Transform>(true))item.gameObject.layer=29;
                actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                baked=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(skin=>new BakedComparisonSkin(skin)).ToArray();
                Assert.That(animator!=null&&animator.isHuman,Is.True);
                animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                Transform[] bones=Bones.Select(animator.GetBoneTransform).ToArray();Assert.That(bones.All(b=>b!=null),Is.True);
                cameraObject=new GameObject("G04 source camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.enabled=false;camera.cullingMask=1<<29;
                camera.aspect=256f/384f;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                camera.orthographic=true;camera.orthographicSize=1.25f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.17f,.21f);
                lightObject=new GameObject("G04 source key");SceneManager.MoveGameObjectToScene(lightObject,scene);var light=lightObject.AddComponent<Light>();
                light.type=LightType.Directional;light.intensity=1.2f;light.cullingMask=1<<29;light.transform.rotation=Quaternion.Euler(35,25,0);
                yield return null;
                foreach(Segment segment in report.segments)
                {
                    graph=PlayableGraph.Create("Native G04 segment");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var source=AnimationClipPlayable.Create(graph,segment.asset);source.SetApplyFootIK(false);source.SetSpeed(0);
                    var additive=AnimationClipPlayable.Create(graph,loop);additive.SetApplyFootIK(false);additive.SetSpeed(0);
                    var mixer=AnimationLayerMixerPlayable.Create(graph,2);graph.Connect(source,0,mixer,0);graph.Connect(additive,0,mixer,1);
                    mixer.SetInputWeight(0,1);mixer.SetInputWeight(1,segment.compositeLoop?EarthLivingHoldPolicy.MaximumWeight:0);
                    mixer.SetLayerAdditive(1,true);mixer.SetLayerMaskFromAvatarMask(1,mask);
                    AnimationPlayableOutput.Create(graph,"Source",animator).SetSourcePlayable(mixer);graph.Play();
                    void Sample(float seconds)
                    {
                        source.SetTime(segment.compositeLoop?segment.start*segment.sourceSeconds:segment.start*segment.sourceSeconds+seconds);
                        additive.SetTime(segment.compositeLoop?seconds:0);graph.Evaluate(0);
                        foreach(var skin in baked)skin.Bake();
                    }
                    Bounds framing=new Bounds(actor.transform.position,Vector3.zero);
                    int count=Mathf.CeilToInt(segment.nativeSeconds*60);
                    for(int frame=0;frame<=count;frame++)
                    {
                        float seconds=Mathf.Min(frame/60f,segment.nativeSeconds);Sample(seconds);
                        var row=new Frame{seconds=seconds,positions=new Vector3[bones.Length],rotations=new Quaternion[bones.Length]};
                        for(int b=0;b<bones.Length;b++){row.positions[b]=actor.transform.InverseTransformPoint(bones[b].position);row.rotations[b]=bones[b].localRotation;framing.Encapsulate(bones[b].position);
                            Assert.That(float.IsFinite(row.positions[b].sqrMagnitude),Is.True,segment.name+" non-finite source bone");}
                        segment.frames.Add(row);
                        foreach(var renderer in actor.GetComponentsInChildren<Renderer>())if(renderer.enabled)framing.Encapsulate(renderer.bounds);
                    }
                    Analyze(segment);
                    if(segment.name.StartsWith("heavy-"))
                    {
                        // Native peak +/- a coherent anticipation/recovery neighborhood, never retimed.
                        float start=Mathf.Max(0,segment.peakSeconds-.4f);
                        float end=Mathf.Min(segment.nativeSeconds,segment.peakSeconds+.35f);
                        segment.peakSheetSeconds=Enumerable.Range(0,16).Select(i=>Mathf.Lerp(start,end,i/15f)).ToArray();
                    }
                    foreach(int angle in new[]{0,90})
                    {
                        camera.orthographicSize=Mathf.Max(1.25f,(framing.size.y+.4f)*.5f,(Mathf.Max(framing.size.x,framing.size.z)+.4f)*.75f);
                        Vector3 focus=framing.center;Vector3 direction=Quaternion.Euler(0,angle,0)*Vector3.forward;
                        camera.transform.SetPositionAndRotation(focus+direction*4,Quaternion.LookRotation(-direction));
                        var sheet=new Texture2D(256*8,384,TextureFormat.RGB24,false);
                        try{for(int column=0;column<8;column++){Sample(segment.nativeSeconds*column/7f);CaptureCell(camera,sheet,column);}sheet.Apply();File.WriteAllBytes(Folder+"/"+segment.name+"-"+angle+".png",sheet.EncodeToPNG());}
                        finally{Object.DestroyImmediate(sheet);}
                        if(segment.peakSheetSeconds!=null)
                        {
                            var peakSheet=new Texture2D(256*16,384,TextureFormat.RGB24,false);
                            try
                            {
                                var imageHashes=new HashSet<ulong>();var geometryHashes=new HashSet<ulong>();
                                for(int column=0;column<segment.peakSheetSeconds.Length;column++)
                                {
                                    Sample(segment.peakSheetSeconds[column]);
                                    geometryHashes.Add(BakedGeometryHash(baked));
                                    CaptureCell(camera,peakSheet,column);imageHashes.Add(ImageCellHash(peakSheet,column));
                                }
                                segment.minimumDistinctPeakImages=Mathf.Min(segment.minimumDistinctPeakImages,imageHashes.Count);
                                segment.minimumDistinctPeakGeometry=Mathf.Min(segment.minimumDistinctPeakGeometry,geometryHashes.Count);
                                peakSheet.Apply();File.WriteAllBytes(Folder+"/"+segment.name+"-peak-"+angle+".png",peakSheet.EncodeToPNG());
                            }
                            finally{Object.DestroyImmediate(peakSheet);}
                        }
                    }
                    graph.Destroy();yield return null;
                }
                foreach(var pair in new[]{new[]{0,1},new[]{1,2},new[]{4,5},new[]{5,6}})
                {
                    var a=report.segments[pair[0]];var b=report.segments[pair[1]];var end=a.frames[a.frames.Count-1];var prior=a.frames[a.frames.Count-2];var begin=b.frames[0];var next=b.frames[1];
                    var boundary=new Boundary{from=a.name,to=b.name};
                    for(int bone=0;bone<Bones.Length;bone++)
                    {boundary.maximumPositionGap=Mathf.Max(boundary.maximumPositionGap,Vector3.Distance(end.positions[bone],begin.positions[bone]));
                        boundary.maximumRotationGap=Mathf.Max(boundary.maximumRotationGap,PreciseQuaternionAngle(end.rotations[bone],begin.rotations[bone]));
                        boundary.maximumVelocityGap=Mathf.Max(boundary.maximumVelocityGap,Vector3.Distance((end.positions[bone]-prior.positions[bone])/(end.seconds-prior.seconds),(next.positions[bone]-begin.positions[bone])/(next.seconds-begin.seconds)));}
                    report.boundaries.Add(boundary);
                }
                Assert.That(report.segments.Count,Is.EqualTo(11));
                foreach(var segment in report.segments.Where(s=>s.peakSheetSeconds!=null))
                {
                    Assert.That(segment.minimumDistinctPeakGeometry,Is.GreaterThan(1),segment.name+" baked geometry never changed.");
                    Assert.That(segment.minimumDistinctPeakImages,Is.GreaterThan(1),segment.name+" actual peak images stayed identical despite source motion.");
                }
                foreach (var segment in report.segments)
                    Assert.That(segment.maxBoneStepDegrees,Is.GreaterThan(.0001f),segment.name+" did not animate; its diagnostic graph is not valid evidence. All source comparisons were retained for diagnosis.");
            }
            finally
            {
                File.WriteAllText(Folder+"/NativeSegmentComparison.json",JsonUtility.ToJson(report,true));
                if(graph.IsValid())graph.Destroy();foreach(var skin in baked)skin.Dispose();if(actor!=null)Object.DestroyImmediate(actor);if(cameraObject!=null)Object.DestroyImmediate(cameraObject);if(lightObject!=null)Object.DestroyImmediate(lightObject);
                SceneManager.UnloadSceneAsync(scene);
            }
        }
        // Construct only transforms, native render components and one Animator. No cloned
        // MonoBehaviour, physics, network or graph owner can execute, even during activation.
        private static GameObject CloneProductionRenderRig(Transform source,Animator sourceAnimator,out Animator animator)
        {
            var root=new GameObject("Production stone source comparison");root.SetActive(false);
            var nodes=new Dictionary<Transform,Transform>{{source,root.transform}};
            void CopyChildren(Transform original,Transform copy)
            {
                foreach(Transform child in original)
                {
                    var target=new GameObject(child.name);target.transform.SetParent(copy,false);
                    target.transform.localPosition=child.localPosition;target.transform.localRotation=child.localRotation;target.transform.localScale=child.localScale;
                    target.SetActive(child.gameObject.activeSelf);nodes.Add(child,target.transform);CopyChildren(child,target.transform);
                }
            }
            try
            {
                root.transform.localScale=source.lossyScale;CopyChildren(source,root.transform);
                int count=0;
                foreach(var body in source.GetComponentsInChildren<Renderer>(true))
                {
                    if(body is not (MeshRenderer or SkinnedMeshRenderer)||!body.enabled||!body.gameObject.activeInHierarchy)continue;
                    var materials=body.sharedMaterials;
                    if(materials.Any(m=>m!=null&&(m.renderQueue>(int)RenderQueue.GeometryLast||m.GetTag("RenderType",false,"")=="Transparent"||(m.HasProperty("_Surface")&&m.GetFloat("_Surface")>.5f))))continue;
                    Assert.That(materials.All(m=>m!=null),Is.True,"Production body must retain its real materials.");
                    Renderer clone;
                    if(body is SkinnedMeshRenderer skin)
                    {
                        var target=nodes[body.transform].gameObject.AddComponent<SkinnedMeshRenderer>();
                        target.sharedMesh=skin.sharedMesh;target.bones=skin.bones.Select(b=>b==null?null:nodes[b]).ToArray();
                        target.rootBone=skin.rootBone==null?null:nodes[skin.rootBone];target.localBounds=skin.localBounds;
                        target.quality=skin.quality;target.updateWhenOffscreen=true;
                        for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)target.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i));
                        clone=target;
                    }
                    else
                    {
                        var mesh=body.GetComponent<MeshFilter>();if(mesh==null||mesh.sharedMesh==null)continue;
                        nodes[body.transform].gameObject.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                        clone=nodes[body.transform].gameObject.AddComponent<MeshRenderer>();
                    }
                    clone.sharedMaterials=materials;clone.shadowCastingMode=body.shadowCastingMode;clone.receiveShadows=body.receiveShadows;
                    // Source Main/results visibility is a lease; the isolated body renders independently.
                    clone.forceRenderingOff=false;clone.enabled=true;
                    var properties=new MaterialPropertyBlock();body.GetPropertyBlock(properties);clone.SetPropertyBlock(properties);
                    for(int i=0;i<materials.Length;i++){properties.Clear();body.GetPropertyBlock(properties,i);clone.SetPropertyBlock(properties,i);}
                    count++;
                }
                Assert.That(count,Is.GreaterThan(0),"The saved production stone body must exist; no mannequin fallback.");
                animator=nodes[sourceAnimator.transform].gameObject.AddComponent<Animator>();animator.avatar=sourceAnimator.avatar;
                animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true),Is.Empty);
                Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
                Assert.That(root.GetComponentsInChildren<Rigidbody>(true),Is.Empty);
                root.SetActive(true);animator.Rebind();return root;
            }
            catch{Object.DestroyImmediate(root);throw;}
        }
        // Quaternion.Angle intentionally snaps sufficiently close dot products to zero.
        // Authored .35-weight torso steps are ~.01 degrees, below that convenience threshold.
        // Measure the actual relative quaternion in double precision; acceptance is unchanged.
        private static float PreciseQuaternionAngle(Quaternion a,Quaternion b)
        {
            double x=(double)a.w*b.x-(double)a.x*b.w-(double)a.y*b.z+(double)a.z*b.y;
            double y=(double)a.w*b.y+(double)a.x*b.z-(double)a.y*b.w-(double)a.z*b.x;
            double z=(double)a.w*b.z-(double)a.x*b.y+(double)a.y*b.x-(double)a.z*b.w;
            double w=(double)a.w*b.w+(double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z;
            return (float)(2*Math.Atan2(Math.Sqrt(x*x+y*y+z*z),Math.Abs(w))*180/Math.PI);
        }
        [Test] public void DiagnosticRetainsSmallNativeRotationsAndQuaternionSignEquivalence()
        {
            Quaternion pose=Quaternion.Euler(13,27,-8);
            Quaternion step=pose*Quaternion.AngleAxis(.01f,Vector3.up);
            Assert.That(PreciseQuaternionAngle(pose,step),Is.EqualTo(.01f).Within(.00001f));
            Assert.That(PreciseQuaternionAngle(pose,new Quaternion(-pose.x,-pose.y,-pose.z,-pose.w)),Is.LessThan(.000001f));
            Assert.That(PreciseQuaternionAngle(Quaternion.identity,Quaternion.AngleAxis(45,Vector3.forward)),Is.EqualTo(45).Within(.0001f));
        }
        private static void Analyze(Segment segment)
        {
            float[] speed=new float[segment.frames.Count];int peak=0;
            for(int i=1;i<segment.frames.Count;i++)
            {
                var a=segment.frames[i-1];var b=segment.frames[i];float dt=b.seconds-a.seconds;
                for(int bone=0;bone<Bones.Length;bone++)segment.maxBoneStepDegrees=Mathf.Max(segment.maxBoneStepDegrees,PreciseQuaternionAngle(a.rotations[bone],b.rotations[bone]));
                speed[i]=Mathf.Max(Vector3.Distance(a.positions[5],b.positions[5]),Vector3.Distance(a.positions[6],b.positions[6]))/dt;
                if(speed[i]>speed[peak])peak=i;
            }
            segment.maxHandSpeed=speed[peak];segment.peakSeconds=segment.frames[peak].seconds;
            segment.peakSourceSeconds=segment.start*segment.sourceSeconds+(segment.compositeLoop?0:segment.peakSeconds);
            segment.peakSourceNormalized=segment.peakSourceSeconds/segment.sourceSeconds;
            int first=peak,last=peak;while(first>0&&speed[first-1]>=speed[peak]*.5f)first--;while(last+1<speed.Length&&speed[last+1]>=speed[peak]*.5f)last++;
            segment.peakHalfHeightSeconds=segment.frames[last].seconds-segment.frames[first].seconds;
            if(!segment.compositeLoop)return;
            var start=segment.frames[0];var next=segment.frames[1];var end=segment.frames[segment.frames.Count-1];var prior=segment.frames[segment.frames.Count-2];
            for(int bone=0;bone<Bones.Length;bone++)
            {segment.loopPositionGap=Mathf.Max(segment.loopPositionGap,Vector3.Distance(start.positions[bone],end.positions[bone]));segment.loopRotationGap=Mathf.Max(segment.loopRotationGap,PreciseQuaternionAngle(start.rotations[bone],end.rotations[bone]));
                segment.loopVelocityGap=Mathf.Max(segment.loopVelocityGap,Vector3.Distance((next.positions[bone]-start.positions[bone])/(next.seconds-start.seconds),(end.positions[bone]-prior.positions[bone])/(end.seconds-prior.seconds)));}
        }
        private sealed class BakedComparisonSkin:IDisposable
        {
            public readonly SkinnedMeshRenderer Source;public readonly Mesh Mesh;
            private readonly GameObject output;
            public BakedComparisonSkin(SkinnedMeshRenderer source)
            {
                Source=source;Mesh=new Mesh{name="Native source sample baked body"};Mesh.MarkDynamic();
                output=new GameObject("Sample baked "+source.name);output.layer=source.gameObject.layer;output.transform.SetParent(source.transform,false);
                output.AddComponent<MeshFilter>().sharedMesh=Mesh;
                var renderer=output.AddComponent<MeshRenderer>();renderer.sharedMaterials=source.sharedMaterials;
                renderer.shadowCastingMode=source.shadowCastingMode;renderer.receiveShadows=source.receiveShadows;
                var properties=new MaterialPropertyBlock();source.GetPropertyBlock(properties);renderer.SetPropertyBlock(properties);
                for(int i=0;i<source.sharedMaterials.Length;i++){properties.Clear();source.GetPropertyBlock(properties,i);renderer.SetPropertyBlock(properties,i);}
                source.enabled=false;
            }
            public void Bake(){Source.BakeMesh(Mesh);Mesh.RecalculateBounds();Mesh.UploadMeshData(false);}
            public void Dispose(){Object.DestroyImmediate(output);Object.DestroyImmediate(Mesh);}
        }
        private static ulong BakedGeometryHash(BakedComparisonSkin[] skins)
        {
            ulong hash=14695981039346656037UL;
            unchecked{foreach(var skin in skins)foreach(var p in skin.Mesh.vertices){hash=(hash^(uint)p.x.GetHashCode())*1099511628211UL;hash=(hash^(uint)p.y.GetHashCode())*1099511628211UL;hash=(hash^(uint)p.z.GetHashCode())*1099511628211UL;}}
            return hash;
        }
        private static ulong ImageCellHash(Texture2D sheet,int column)
        {
            ulong hash=14695981039346656037UL;
            unchecked{foreach(var value in sheet.GetPixels(column*256,0,256,384)){Color32 pixel=value;hash=(hash^pixel.r)*1099511628211UL;hash=(hash^pixel.g)*1099511628211UL;hash=(hash^pixel.b)*1099511628211UL;}}
            return hash;
        }
        private static void CaptureCell(Camera camera,Texture2D sheet,int column)
        {
            var target=new RenderTexture(256,384,24,RenderTextureFormat.ARGB32);var prior=RenderTexture.active;
            // A synchronous, scoped source comparison: no gameplay camera renders during
            // this lease. Restore every feature flag before returning or yielding.
            var atmosphere=Resources.FindObjectsOfTypeAll<AtmosphereFullscreenFeature>().Where(f=>f.isActive).ToArray();
            bool fog=RenderSettings.fog;RenderSettings.fog=false;foreach(var feature in atmosphere)feature.SetActive(false);
            try{var request=new RenderPipeline.StandardRequest{destination=target};Assert.That(RenderPipeline.SupportsRenderRequest(camera,request),Is.True);RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=target;sheet.ReadPixels(new Rect(0,0,256,384),column*256,0,false);}
            finally{foreach(var feature in atmosphere)feature.SetActive(true);RenderSettings.fog=fog;RenderTexture.active=prior;target.Release();Object.DestroyImmediate(target);}
        }
    }
}
#endif
