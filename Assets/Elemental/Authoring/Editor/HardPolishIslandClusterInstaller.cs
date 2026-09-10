using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Presentation.DistantScenery;
using Elemental.Presentation.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Elemental.Authoring.Editor
{
    // Adds a selected subordinate composition. Never rebuilds the backdrop or edits old meshes.
    public static class HardPolishIslandClusterInstaller
    {
        [Serializable] public sealed class Stone
        {
            public string name,anchor,highPath,lowPath;public Vector3 position;public Quaternion rotation;
            public float scale,amplitude,period,angle,phase;public Bounds envelope;
            [NonSerialized] public Mesh high,low;[NonSerialized] public MeshRenderer materialSource;
        }
        [Serializable] public sealed class Rejection
        {
            public string stone,anchor,nearestBlocker;public int attempt;public Vector3 position;
            public Bounds envelope,nearestBlockingBounds;public bool arenaRejected;public string[] blockedBy;
        }
        [Serializable] public sealed class Plan
        {
            public List<Stone> stones=new();public List<Rejection> rejections=new();
            public int rejectedCandidates;public string status;
        }
        // The first 48 proposals preserve the existing accepted placement order.
        // Fallback explores nearby free sides of an anchor; it creates no orbital ring.
        private static readonly float[] SearchYaw={-28,0,28,-55,55,-85,85,-115,115,155,-155,180};
        private static readonly float[] SearchHeight={-.19f,-.105f,-.02f,.065f,.15f,.28f,-.32f,.43f,-.46f};
        public const int MaximumPlacementAttempts=48+6*12*9;
        public static void ResolveCandidateOffset(Bounds anchor,int stone,int attempt,float height,
            out float yaw,out float distance,out float lift)
        {
            if(attempt<0||attempt>=MaximumPlacementAttempts)throw new ArgumentOutOfRangeException(nameof(attempt));
            if(attempt<48)
            {
                yaw=-32+((stone*17+attempt*11)%65);
                distance=anchor.extents.magnitude*.6f+height+anchor.size.y*(.18f+.08f*(attempt/8));
                lift=anchor.size.y*(-.19f+.085f*((stone+attempt)%5));return;
            }
            int search=attempt-48,ring=search/108,tier=(search/12)%9;
            yaw=SearchYaw[(search+stone*3)%12];
            distance=anchor.extents.magnitude*.6f+height+anchor.size.y*(.18f+.12f*ring);
            lift=anchor.size.y*SearchHeight[(tier+stone)%9];
        }
        private const string Prefix="Cluster_";
        public static Plan Preview(DistantBackdrop owner)
        {
            if(owner==null||owner.profile==null||owner.planetCenter==null)throw new InvalidOperationException("Explicit saved backdrop required.");
            var data=new SerializedObject(owner);var generated=(Transform)data.FindProperty("generatedRoot").objectReferenceValue;
            if(generated==null)throw new InvalidOperationException("Saved backdrop root missing.");
            var occupied=new List<Bounds>();var occupiedNames=new List<string>();var drift=data.FindProperty("drift");
            foreach(Transform root in generated)
            {
                float amplitude=0,angle=0,fraction=0,spin=0;Vector3 rotationCenter=root.position;
                for(int i=0;i<drift.arraySize;i++)
                {
                    var d=drift.GetArrayElementAtIndex(i);if(d.FindPropertyRelative("target").objectReferenceValue!=root)continue;
                    amplitude=d.FindPropertyRelative("amplitude").floatValue;angle=d.FindPropertyRelative("angle").floatValue;
                    fraction=d.FindPropertyRelative("horizontalFraction").floatValue;spin=d.FindPropertyRelative("spinDegreesPerSecond").floatValue;
                    rotationCenter=root.position+root.rotation*d.FindPropertyRelative("centerOffset").vector3Value;break;
                }
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.sharedMesh==null)continue;
                    Bounds world=WorldBounds(filter.sharedMesh.bounds,filter.transform.localToWorldMatrix);
                    float pad=Mathf.Abs(amplitude)*(1+Mathf.Abs(fraction)*1.414214f)+
                        2*world.extents.magnitude*Mathf.Sin(Mathf.Min(90,Mathf.Abs(angle)*2)*Mathf.Deg2Rad*.5f);
                    if(spin!=0){float radius=new Vector2(world.extents.x,world.extents.z).magnitude;var e=world.extents;e.x=e.z=radius;world.extents=e;}
                    if(angle!=0||spin!=0)pad+=2*Vector3.Distance(world.center,rotationCenter);
                    world.Expand(pad*2);occupied.Add(world);occupiedNames.Add(root.name+"/"+filter.name);
                }
            }
            Vector3 up=owner.stagingUp.normalized,center=owner.planetCenter.position;
            Vector3 forward=Vector3.ProjectOnPlane(owner.profile.heroViewDirection,up).normalized;
            Vector3 right=Vector3.Cross(up,forward).normalized;
            var exclusionFrame=Matrix4x4.TRS(center,Quaternion.LookRotation(forward,up),Vector3.one);
            var plan=new Plan();string[] selected={"View_NearWest","View_NearEast"};
            for(int group=0;group<selected.Length;group++)
            {
                string anchorName=selected[group];Transform anchor=generated.Find(anchorName);
                if(anchor==null)throw new InvalidOperationException("Required NEW landmark missing: "+anchorName);
                int wanted=group==0?3:4;
                int existing=generated.Cast<Transform>().Count(x=>x.name.StartsWith(Prefix+anchorName+"_",StringComparison.Ordinal));
                if(existing==wanted)continue;
                if(existing!=0)throw new InvalidOperationException("Partial named cluster requires review: "+anchorName);
                var source=anchor.Find("LOD0")?.GetComponent<MeshFilter>();
                if(source==null||source.sharedMesh==null)throw new InvalidOperationException("New landmark has no LOD0.");
                Bounds anchorBounds=WorldBounds(source.sharedMesh.bounds,source.transform.localToWorldMatrix);
                Vector3 outward=Vector3.ProjectOnPlane(anchorBounds.center-center,up).normalized;
                if(outward.sqrMagnitude<.1f)outward=right;
                for(int stone=0;stone<wanted;stone++)
                {
                    string[] families={"V5_Boulder_02","V5_Wedge_14","V5_Slab_09","V5_Pebble_18"};
                    string meshPath="Assets/Elemental/Content/GraphicsV5/Rocks/"+families[(stone+group)%families.Length]+".asset";
                    Mesh high=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath),low=high;
                    if(high==null||!RumbleRockMeshFactory.Validate(high,out string reason))
                        throw new InvalidOperationException("Approved rock source missing or invalid: "+meshPath);
                    float[] fractions={.20f,.105f,.15f,.075f};float height=anchorBounds.size.y*fractions[stone];
                    Vector3 size=high.bounds.size;float scale=height/Mathf.Max(.001f,Mathf.Max(size.x,Mathf.Max(size.y,size.z)));
                    float amplitude=Mathf.Min(.18f,size.y*scale*.02f);
                    Quaternion rotation=Quaternion.LookRotation(Quaternion.AngleAxis(27*stone+13*group,up)*forward,up);
                    Stone accepted=null;
                    for(int attempt=0;attempt<MaximumPlacementAttempts;attempt++)
                    {
                        ResolveCandidateOffset(anchorBounds,stone,attempt,height,out float yaw,out float distance,out float lift);
                        Vector3 direction=Quaternion.AngleAxis(yaw,up)*outward;
                        Vector3 position=anchorBounds.center+direction*distance+up*lift-rotation*(high.bounds.center*scale);
                        Bounds envelope=ClusterMotionEnvelope(high.bounds,scale,position,rotation,amplitude,.27f,.4f);
                        envelope.Encapsulate(ClusterMotionEnvelope(low.bounds,scale,position,rotation,amplitude,.27f,.4f));
                        bool arenaRejected=envelope.SqrDistance(center)<Mathf.Pow(owner.profile.planetRadius+owner.profile.arenaExclusionPadding,2);
                        var blockers=new List<string>();string nearest=null;Bounds nearestBounds=default;float nearestDistance=float.PositiveInfinity;
                        void RecordBlocker(string name,Bounds bounds)
                        {
                            if(!bounds.Intersects(envelope))return;
                            blockers.Add(name);float distanceToCenter=(bounds.center-envelope.center).sqrMagnitude;
                            if(distanceToCenter<nearestDistance){nearestDistance=distanceToCenter;nearest=name;nearestBounds=bounds;}
                        }
                        for(int occupiedIndex=0;occupiedIndex<occupied.Count;occupiedIndex++)
                            RecordBlocker(occupiedNames[occupiedIndex],occupied[occupiedIndex]);
                        if(owner.profile.exclusionVolumes!=null)for(int exclusion=0;exclusion<owner.profile.exclusionVolumes.Length;exclusion++)
                            RecordBlocker("exclusion["+exclusion+"]",WorldBounds(owner.profile.exclusionVolumes[exclusion],exclusionFrame));
                        if(arenaRejected||blockers.Count>0)
                        {
                            plan.rejectedCandidates++;
                            plan.rejections.Add(new Rejection{stone=Prefix+anchorName+"_"+stone,anchor=anchorName,attempt=attempt,
                                position=position,envelope=envelope,arenaRejected=arenaRejected,blockedBy=blockers.ToArray(),
                                nearestBlocker=nearest,nearestBlockingBounds=nearestBounds});continue;
                        }
                        accepted=new Stone{name=Prefix+anchorName+"_"+stone,anchor=anchorName,highPath=meshPath,lowPath=meshPath,
                            high=high,low=low,position=position,rotation=rotation,scale=scale,amplitude=amplitude,period=36+stone*4+group*2,
                            angle=.4f,phase=(stone*.273f+group*.19f)%1,envelope=envelope,materialSource=source.GetComponent<MeshRenderer>()};break;
                    }
                    if(accepted==null)
                    {
                        plan.status="FAILED_NO_FIT:"+Prefix+anchorName+"_"+stone;
                        Directory.CreateDirectory("BuildReports/HardPolish/Islands");
                        const string failurePath="BuildReports/HardPolish/Islands/ClusterPreviewFailure.json";
                        File.WriteAllText(failurePath,JsonUtility.ToJson(plan,true));
                        Rejection last=plan.rejections[plan.rejections.Count-1];
                        throw new InvalidOperationException("Cannot fit "+last.stone+" after "+MaximumPlacementAttempts+
                            " bounded candidates. Last blockers: "+string.Join(", ",last.blockedBy)+
                            "; arena="+last.arenaRejected+"; nearest="+last.nearestBlocker+" "+last.nearestBlockingBounds+
                            ". All rejection bounds: "+failurePath+". No partial scene publication.");
                    }
                    occupied.Add(accepted.envelope);occupiedNames.Add(accepted.name);plan.stones.Add(accepted);
                }
            }
            plan.status=plan.stones.Count==0?"ALREADY_INSTALLED":"VALIDATED_ADDITIVE_PLAN";return plan;
        }
        public static void InstallSavedScene()
        {EditorSceneManager.OpenScene("Assets/Elemental/Content/Scenes/EarthCoreSlice.unity");InstallCurrentScene();}
        [MenuItem("Elemental/QA/Hard Polish/Add Selected New Island Rock Clusters")]
        public static void InstallCurrentScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install outside Play mode.");
            Scene scene=SceneManager.GetActiveScene();if(scene.name!="EarthCoreSlice")throw new InvalidOperationException("Use the saved production scene.");
            var owner=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DistantBackdrop>(true)).Single();
            Plan plan=Preview(owner);var data=new SerializedObject(owner);var generated=(Transform)data.FindProperty("generatedRoot").objectReferenceValue;
            var drift=data.FindProperty("drift");var created=new List<GameObject>();int priorSize=drift.arraySize;
            try
            {
                foreach(var stone in plan.stones)
                {
                    var root=new GameObject(stone.name);created.Add(root);root.transform.SetParent(generated,false);root.transform.SetPositionAndRotation(stone.position,stone.rotation);
                    MeshRenderer Add(string name,Mesh mesh)
                    {
                        var item=new GameObject(name);item.transform.SetParent(root.transform,false);item.transform.localScale=Vector3.one*stone.scale;
                        item.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=item.AddComponent<MeshRenderer>();
                        renderer.sharedMaterials=stone.materialSource.sharedMaterials;renderer.renderingLayerMask=stone.materialSource.renderingLayerMask;
                        renderer.shadowCastingMode=stone.materialSource.shadowCastingMode;renderer.receiveShadows=stone.materialSource.receiveShadows;return renderer;
                    }
                    // Seven small approved sources retain their exact contour until distant cull.
                    // No duplicate runtime mesh/renderer or claimed geometric simplification.
                    var high=Add("LOD0",stone.high);var lod=root.AddComponent<LODGroup>();
                    lod.SetLODs(new[]{new LOD(.003f,new Renderer[]{high})});lod.RecalculateBounds();
                    drift.InsertArrayElementAtIndex(drift.arraySize);var d=drift.GetArrayElementAtIndex(drift.arraySize-1);
                    d.FindPropertyRelative("target").objectReferenceValue=root.transform;d.FindPropertyRelative("origin").vector3Value=stone.position;
                    d.FindPropertyRelative("rotation").quaternionValue=stone.rotation;d.FindPropertyRelative("phase").floatValue=stone.phase;
                    d.FindPropertyRelative("period").floatValue=stone.period;d.FindPropertyRelative("amplitude").floatValue=stone.amplitude;
                    d.FindPropertyRelative("angle").floatValue=stone.angle;d.FindPropertyRelative("horizontalFraction").floatValue=.27f;
                    Vector3 up=owner.stagingUp.normalized,forward=Vector3.ProjectOnPlane(owner.profile.heroViewDirection,up).normalized;
                    d.FindPropertyRelative("tangent").vector3Value=Vector3.Cross(up,forward).normalized;d.FindPropertyRelative("bitangent").vector3Value=forward;
                    d.FindPropertyRelative("centerOffset").vector3Value=stone.high.bounds.center*stone.scale;
                    d.FindPropertyRelative("spinAxis").vector3Value=Vector3.zero;d.FindPropertyRelative("spinDegreesPerSecond").floatValue=0;
                }
                data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(owner);
                EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save cluster scene.");
            }
            catch
            {
                data.Update();data.FindProperty("drift").arraySize=priorSize;data.ApplyModifiedPropertiesWithoutUndo();
                foreach(var item in created)UnityEngine.Object.DestroyImmediate(item);throw;
            }
            Directory.CreateDirectory("BuildReports/HardPolish/Islands");File.WriteAllText("BuildReports/HardPolish/Islands/ClusterInstall.json",JsonUtility.ToJson(plan,true));
        }
        public static Bounds ClusterMotionEnvelope(Bounds meshBounds,float scale,Vector3 position,Quaternion rotation,
            float amplitude,float horizontalFraction,float angle)
        {
            // Keep the original envelope, then also contain the actual rotated
            // baseline. Arena up is tilted; local Y extent alone misses that tilt.
            Bounds envelope=DistantBackdrop.FloatingMotionEnvelope(meshBounds,scale,position,rotation,amplitude,horizontalFraction,angle);
            Bounds rotated=WorldBounds(meshBounds,Matrix4x4.TRS(position,rotation,Vector3.one*scale));
            float rotationPad=2*(meshBounds.extents*scale).magnitude*Mathf.Sin(Mathf.Min(90,Mathf.Abs(angle)*2)*Mathf.Deg2Rad*.5f);
            float translationPad=Mathf.Abs(amplitude)*(1+Mathf.Abs(horizontalFraction)*Mathf.Sqrt(2));
            rotated.Expand(2*(rotationPad+translationPad));envelope.Encapsulate(rotated);return envelope;
        }
        private static Bounds WorldBounds(Bounds local,Matrix4x4 matrix)
        {
            Vector3 e=local.extents;Vector3 x=matrix.MultiplyVector(new Vector3(e.x,0,0)),y=matrix.MultiplyVector(new Vector3(0,e.y,0)),z=matrix.MultiplyVector(new Vector3(0,0,e.z));
            Vector3 ext=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            return new Bounds(matrix.MultiplyPoint3x4(local.center),ext*2);
        }
    }
}
