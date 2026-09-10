using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Elemental.Presentation.Rendering;
using Elemental.Authoring.Editor;
using Elemental.Presentation.DistantScenery;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Elemental.Tests.EditMode
{
    public sealed class HardPolishIslandClusterTests
    {
        [Test] public void SearchPreservesOriginalCandidatesAndExploresOtherNearbySides()
        {
            var anchor=new Bounds(Vector3.zero,new Vector3(30,28,36));
            for(int stone=0;stone<4;stone++)for(int attempt=0;attempt<48;attempt++)
            {
                HardPolishIslandClusterInstaller.ResolveCandidateOffset(anchor,stone,attempt,4,out float yaw,out float distance,out float lift);
                Assert.That(yaw,Is.EqualTo(-32+((stone*17+attempt*11)%65)));
                Assert.That(distance,Is.EqualTo(anchor.extents.magnitude*.6f+4+anchor.size.y*(.18f+.08f*(attempt/8))).Within(.0001f));
                Assert.That(lift,Is.EqualTo(anchor.size.y*(-.19f+.085f*((stone+attempt)%5))).Within(.0001f));
            }
            bool foundSide=false;
            for(int attempt=48;attempt<HardPolishIslandClusterInstaller.MaximumPlacementAttempts;attempt++)
            {
                HardPolishIslandClusterInstaller.ResolveCandidateOffset(anchor,0,attempt,4,out float yaw,out float distance,out float lift);
                foundSide|=Mathf.Abs(yaw)>32;
                Assert.That(distance,Is.LessThan(anchor.extents.magnitude+anchor.size.y+4));
                Assert.That(Mathf.Abs(lift),Is.LessThanOrEqualTo(anchor.size.y*.46001f));
            }
            Assert.That(foundSide,Is.True,"Narrow outward-only proposals could not escape ValleyGroup_03.");
        }
        [Test] public void PlannedClustersHaveVariedSizeBothLodsAndNonoverlappingCompleteHoverBounds()
        {
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var owner=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DistantBackdrop>(true)).Single();
                var before=owner.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.position,t.rotation,t.localScale));
                var plan=HardPolishIslandClusterInstaller.Preview(owner);
                var data=new SerializedObject(owner);
                var generated=(Transform)data.FindProperty("generatedRoot").objectReferenceValue;
                var installed=generated.Cast<Transform>().Where(t=>t.name.StartsWith("Cluster_",StringComparison.Ordinal)).ToArray();
                var stones=new List<HardPolishIslandClusterInstaller.Stone>();
                if(installed.Length==0)
                {
                    Assert.That(plan.status,Is.EqualTo("VALIDATED_ADDITIVE_PLAN"));
                    Assert.That(plan.stones.Count,Is.EqualTo(7));stones.AddRange(plan.stones);
                }
                else
                {
                    Assert.That(plan.status,Is.EqualTo("ALREADY_INSTALLED"));
                    Assert.That(plan.stones,Is.Empty,"Installed cluster preview must not plan duplicate roots.");
                    Assert.That(installed.Length,Is.EqualTo(7),"Partial or additional cluster publication is invalid.");
                    var expected=Enumerable.Range(0,3).Select(i=>"Cluster_View_NearWest_"+i)
                        .Concat(Enumerable.Range(0,4).Select(i=>"Cluster_View_NearEast_"+i)).ToArray();
                    CollectionAssert.AreEquivalent(expected,installed.Select(t=>t.name).ToArray());
                    foreach(var root in installed)stones.Add(ReadInstalledStone(root,data));
                }
                foreach(var stone in stones)
                {
                    Assert.That(stone.high,Is.Not.Null);
                    Assert.That(RumbleRockMeshFactory.Validate(stone.high,out string geometryReason),Is.True,stone.name+": "+geometryReason);
                    if(installed.Length>0)
                    {
                        var renderer=generated.Find(stone.name).GetComponentInChildren<MeshRenderer>(true);
                        var anchorRenderer=generated.Find(stone.anchor).Find("LOD0").GetComponent<MeshRenderer>();
                        CollectionAssert.AreEqual(anchorRenderer.sharedMaterials,renderer.sharedMaterials,stone.name+": preserve the landmark material family.");
                    }
                }
                foreach(var group in stones.GroupBy(s=>s.anchor))
                {
                    Assert.That(group.Count(),Is.InRange(2,5));Assert.That(group.Select(s=>s.highPath).Distinct().Count(),Is.GreaterThanOrEqualTo(3));
                    var heights=group.Select(s=>Mathf.Max(s.high.bounds.size.x,Mathf.Max(s.high.bounds.size.y,s.high.bounds.size.z))*s.scale).ToArray();Assert.That(heights.Max()/heights.Min(),Is.GreaterThan(1.7f));
                    foreach(var s in group){Assert.That(s.period,Is.InRange(36,50));Assert.That(s.amplitude,Is.LessThanOrEqualTo(s.high.bounds.size.y*s.scale*.02001f));Assert.That(s.low,Is.Not.Null);}
                }
                for(int i=0;i<stones.Count;i++)for(int j=i+1;j<stones.Count;j++)
                    Assert.That(stones[i].envelope.Intersects(stones[j].envelope),Is.False,stones[i].name+" overlaps "+stones[j].name);
                CheckExistingExclusions(owner,generated,data,stones);
                foreach(var item in before){Assert.That(item.Key.position,Is.EqualTo(item.Value.Item1));Assert.That(item.Key.rotation,Is.EqualTo(item.Value.Item2));Assert.That(item.Key.localScale,Is.EqualTo(item.Value.Item3));}
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }

        private static HardPolishIslandClusterInstaller.Stone ReadInstalledStone(Transform root,SerializedObject data)
        {
            Assert.That(root.localScale,Is.EqualTo(Vector3.one));
            var filters=root.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(filters.Length,Is.EqualTo(1),root.name+": small approved contour has exactly one saved renderer, no fake duplicate LOD.");
            var filter=filters[0];Assert.That(filter.sharedMesh,Is.Not.Null);
            Assert.That(RumbleRockMeshFactory.Validate(filter.sharedMesh,out string reason),Is.True,root.name+": "+reason);
            Assert.That(filter.transform.localPosition,Is.EqualTo(Vector3.zero));
            Assert.That(filter.transform.localRotation,Is.EqualTo(Quaternion.identity));
            float scale=filter.transform.localScale.x;
            Assert.That(scale,Is.GreaterThan(0));Assert.That(filter.transform.localScale,Is.EqualTo(Vector3.one*scale));
            var lod=root.GetComponent<LODGroup>();Assert.That(lod,Is.Not.Null);
            var levels=lod.GetLODs();Assert.That(levels.Length,Is.EqualTo(1));
            Assert.That(levels[0].screenRelativeTransitionHeight,Is.EqualTo(.003f).Within(.000001f));
            Assert.That(levels[0].renderers.Length,Is.EqualTo(1));
            Assert.That(levels[0].renderers[0],Is.EqualTo(filter.GetComponent<MeshRenderer>()));
            var drift=data.FindProperty("drift");SerializedProperty entry=null;int matches=0;
            for(int i=0;i<drift.arraySize;i++)
            {
                var candidate=drift.GetArrayElementAtIndex(i);
                if(candidate.FindPropertyRelative("target").objectReferenceValue==root){matches++;entry=candidate.Copy();}
            }
            Assert.That(matches,Is.EqualTo(1),root.name+": must have exactly one absolute motion owner.");
            Vector3 origin=entry.FindPropertyRelative("origin").vector3Value;
            Quaternion rotation=entry.FindPropertyRelative("rotation").quaternionValue;
            float amplitude=entry.FindPropertyRelative("amplitude").floatValue,angle=entry.FindPropertyRelative("angle").floatValue;
            float horizontal=entry.FindPropertyRelative("horizontalFraction").floatValue;
            Vector3 pivot=entry.FindPropertyRelative("centerOffset").vector3Value;
            Assert.That(Vector3.Distance(root.position,origin),Is.LessThan(.001f),root.name+": saved root moved away from authored baseline.");
            Assert.That(Quaternion.Angle(root.rotation,rotation),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(pivot,filter.sharedMesh.bounds.center*scale),Is.LessThan(.0001f));
            Assert.That(angle,Is.EqualTo(.4f));Assert.That(horizontal,Is.EqualTo(.27f));
            Assert.That(entry.FindPropertyRelative("spinDegreesPerSecond").floatValue,Is.Zero);
            float phase=entry.FindPropertyRelative("phase").floatValue;
            Assert.That(phase,Is.InRange(0f,1f));
            var stone=new HardPolishIslandClusterInstaller.Stone { name=root.name,
                anchor=root.name.StartsWith("Cluster_View_NearWest_",StringComparison.Ordinal)?"View_NearWest":"View_NearEast",
                high=filter.sharedMesh,low=filter.sharedMesh,highPath=AssetDatabase.GetAssetPath(filter.sharedMesh),
                scale=scale,position=origin,rotation=rotation,amplitude=amplitude,angle=angle,phase=phase,
                period=entry.FindPropertyRelative("period").floatValue,
                envelope=HardPolishIslandClusterInstaller.ClusterMotionEnvelope(filter.sharedMesh.bounds,scale,origin,rotation,amplitude,horizontal,angle)};
            // Independently sample every extreme of the bounded translation/rocking
            // components, about the saved geometric pivot. No live Transform changes.
            Vector3 up=((DistantBackdrop)data.targetObject).stagingUp.normalized;
            Vector3 tangent=entry.FindPropertyRelative("tangent").vector3Value,bitangent=entry.FindPropertyRelative("bitangent").vector3Value;
            Bounds local=filter.sharedMesh.bounds;Bounds tolerance=stone.envelope;tolerance.Expand(.001f);
            foreach(int u in new[]{-1,1})foreach(int a in new[]{-1,1})foreach(int b in new[]{-1,1})
            foreach(int rx in new[]{-1,1})foreach(int rz in new[]{-1,1})
            {
                Quaternion moved=rotation*Quaternion.Euler(rx*angle,0,rz*angle);
                Vector3 position=origin+up*(u*amplitude)+(tangent*a+bitangent*b)*(amplitude*horizontal)+rotation*pivot-moved*pivot;
                var matrix=Matrix4x4.TRS(position,moved,Vector3.one*scale);
                foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
                    Assert.That(tolerance.Contains(matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z)))),Is.True,
                        root.name+": saved motion can leave its declared full-motion envelope.");
            }
            return stone;
        }
        private static void CheckExistingExclusions(DistantBackdrop owner,Transform generated,SerializedObject data,
            List<HardPolishIslandClusterInstaller.Stone> stones)
        {
            var drift=data.FindProperty("drift");var occupied=new List<(string,Bounds)>();
            foreach(Transform root in generated)
            {
                if(root.name.StartsWith("Cluster_",StringComparison.Ordinal))continue;
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
                    world.Expand(pad*2);occupied.Add((root.name+"/"+filter.name,world));
                }
            }
            Vector3 up=owner.stagingUp.normalized,center=owner.planetCenter.position;
            Vector3 forward=Vector3.ProjectOnPlane(owner.profile.heroViewDirection,up).normalized;
            var frame=Matrix4x4.TRS(center,Quaternion.LookRotation(forward,up),Vector3.one);
            if(owner.profile.exclusionVolumes!=null)for(int i=0;i<owner.profile.exclusionVolumes.Length;i++)
                occupied.Add(("exclusion["+i+"]",WorldBounds(owner.profile.exclusionVolumes[i],frame)));
            foreach(var stone in stones)
            {
                Assert.That(stone.envelope.SqrDistance(center),Is.GreaterThanOrEqualTo(Mathf.Pow(owner.profile.planetRadius+owner.profile.arenaExclusionPadding,2)),stone.name+": arena exclusion.");
                foreach(var blocker in occupied)Assert.That(stone.envelope.Intersects(blocker.Item2),Is.False,stone.name+" overlaps full envelope "+blocker.Item1);
            }
        }
        private static Bounds WorldBounds(Bounds local,Matrix4x4 matrix)
        {
            Vector3 e=local.extents;
            Vector3 x=matrix.MultiplyVector(new Vector3(e.x,0,0)),y=matrix.MultiplyVector(new Vector3(0,e.y,0)),z=matrix.MultiplyVector(new Vector3(0,0,e.z));
            Vector3 ext=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            return new Bounds(matrix.MultiplyPoint3x4(local.center),ext*2);
        }
    }
}
