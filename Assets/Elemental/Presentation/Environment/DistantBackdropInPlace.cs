using System;
using System.Collections.Generic;
using UnityEngine;
namespace Elemental.Presentation.DistantScenery
{
    public sealed partial class DistantBackdrop
    {
        public sealed class ExistingGeometrySnapshot
        {
            internal readonly Dictionary<Transform,Bounds[]> Solids=new();
        }
        // Capture BEFORE CopySerialized mutates the existing Mesh identities.
        public ExistingGeometrySnapshot CaptureExistingGeometry()
        {
            if(generatedRoot==null)throw new InvalidOperationException("Saved backdrop root is required; in-place integration never regenerates it.");
            var result=new ExistingGeometrySnapshot();
            foreach(Transform pivot in generatedRoot)
            {
                var solids=new List<Bounds>();
                foreach(var filter in pivot.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.sharedMesh==null)continue;
                    foreach(var component in ConnectedComponentBounds(filter.sharedMesh))
                        solids.Add(WorldBounds(component,filter.transform.localToWorldMatrix));
                }
                if(solids.Count>0)result.Solids.Add(pivot,solids.ToArray());
            }
            if(result.Solids.Count==0)throw new InvalidOperationException("Saved backdrop has no mesh solids.");
            return result;
        }
        private static bool AnyIntersection(Bounds[] first,Bounds[] second)
        {foreach(var a in first)foreach(var b in second)if(a.Intersects(b))return true;return false;}
        // Conservative broad-phase conflicts are reported as bounds conflicts, not proven triangle intersections.
        public string[] InspectNewMeshConflicts(ExistingGeometrySnapshot before)
        {
            var current=CaptureExistingGeometry();var conflicts=new List<string>();
            Vector3 center=planetCenter.position;
            float exclusion=profile.planetRadius+profile.arenaExclusionPadding;
            Vector3 up=stagingUp.normalized,forward=Vector3.ProjectOnPlane(profile.heroViewDirection,up).normalized;
            var exclusionFrame=Matrix4x4.TRS(center,Quaternion.LookRotation(forward,up),Vector3.one);
            foreach(var item in current.Solids)
            {
                if(!IsExistingFloating(item.Key))continue;
                if(!before.Solids.TryGetValue(item.Key,out var original))throw new InvalidOperationException("An existing root changed identity before geometry validation.");
                bool near=false,wasNear=false;
                foreach(var bound in item.Value)near|=bound.SqrDistance(center)<exclusion*exclusion;
                foreach(var bound in original)wasNear|=bound.SqrDistance(center)<exclusion*exclusion;
                if(near&&!wasNear)conflicts.Add(item.Key.name+" new bounds enter arena exclusion");
                if(profile.exclusionVolumes!=null)foreach(var box in profile.exclusionVolumes)
                {
                    var world=new[]{WorldBounds(box,exclusionFrame)};
                    if(AnyIntersection(item.Value,world)&&!AnyIntersection(original,world))conflicts.Add(item.Key.name+" new bounds enter authored exclusion");
                }
                foreach(var other in current.Solids)
                {
                    if(other.Key==item.Key)continue;
                    if(AnyIntersection(item.Value,other.Value)&&before.Solids.TryGetValue(other.Key,out var oldOther)&&!AnyIntersection(original,oldOther))
                        conflicts.Add(item.Key.name+" newly overlaps component bounds of "+other.Key.name);
                }
            }
            return conflicts.ToArray();
        }
        private bool IsExistingFloating(Transform pivot)
        {
            foreach(var item in drift)if(item.target==pivot&&(item.amplitude>0||item.angle!=0||item.spinDegreesPerSecond!=0))return true;
            if(profile.viewLandmarks!=null)foreach(var item in profile.viewLandmarks)if("View_"+item.name==pivot.name)return item.airborne;
            if(profile.nearLandmarks!=null)foreach(var item in profile.nearLandmarks)if("View_"+item.name==pivot.name)return item.airborne;
            return pivot.name.StartsWith("Satellite_");
        }
        public void RefreshExistingGeometryAndAddSupplements(ExistingGeometrySnapshot before)
        {
            if(before==null||profile==null||planetCenter==null)throw new ArgumentException("Explicit saved geometry baseline and profile required.");
            string[] conflicts=InspectNewMeshConflicts(before);
            if(conflicts.Length>0)throw new InvalidOperationException("Saved geography was not moved. Candidate mesh bounds conflict: "+string.Join("; ",conflicts));
            _up=stagingUp.normalized;Vector3 forward=Vector3.ProjectOnPlane(profile.heroViewDirection,_up).normalized;
            Vector3 right=Vector3.Cross(_up,forward).normalized,center=planetCenter.position;
            var occupied=new List<Bounds>();var current=CaptureExistingGeometry();
            foreach(var item in current.Solids)occupied.AddRange(item.Value);
            // Existing roots, child transforms, Mesh references, materials and LOD thresholds remain intact.
            for(int i=0;i<drift.Count;i++)
            {
                Drift item=drift[i];if(item.target==null)continue;
                item.origin=item.target.position;item.rotation=item.target.rotation;
                if(IsExistingFloating(item.target))
                {
                    var filter=item.target.Find("LOD0")?.GetComponent<MeshFilter>();
                    if(filter==null||filter.sharedMesh==null)continue;
                    Mesh mesh=filter.sharedMesh;
                    Bounds body=WorldBounds(mesh.bounds,filter.transform.localToWorldMatrix);
                    item.amplitude=FloatingAmplitude(body.size.y,profile.levitationAmplitude);
                    item.horizontalFraction=profile.horizontalMotionFraction;item.tangent=right;item.bitangent=forward;
                    item.centerOffset=Quaternion.Inverse(item.rotation)*(filter.transform.TransformPoint(mesh.bounds.center)-item.origin);
                    // Stable per-root tuning adopts the active profile while preserving saved phase/yaw.
                    uint rootSeed=2166136261u;
                    foreach(char letter in item.target.name)rootSeed=unchecked((rootSeed^letter)*16777619u);
                    var tuning=new RockRandom(unchecked((int)(rootSeed^(uint)profile.motionSeed)));
                    item.period=tuning.Next(profile.levitationPeriod.x,profile.levitationPeriod.y);
                    item.angle=tuning.Next(profile.rockingDegrees.x,profile.rockingDegrees.y);
                    occupied.Add(FloatingMotionEnvelope(new Bounds(Vector3.zero,body.size),1,body.center,Quaternion.identity,item.amplitude,item.horizontalFraction,item.angle));
                }
                drift[i]=item;
                var lod=item.target.GetComponent<LODGroup>();if(lod!=null)lod.RecalculateBounds();
            }
            if(!profile.hardPolishSupplements)return;
            if(profile.nearLandmarks!=null)for(int i=0;i<Mathf.Min(5,profile.nearLandmarks.Length);i++)
            {
                var landmark=profile.nearLandmarks[i];if(generatedRoot.Find("View_"+landmark.name)!=null)continue;
                int variant=Mathf.Clamp(landmark.variant,0,profile.islandSilhouettes.Length-1);
                Mesh high=profile.islandSilhouettes[variant],low=profile.islandLodSilhouettes!=null&&variant<profile.islandLodSilhouettes.Length?profile.islandLodSilhouettes[variant]:null;
                Vector3 position=center+right*landmark.position.x+_up*landmark.position.y+forward*landmark.position.z;
                Quaternion rotation=Quaternion.LookRotation(forward,_up);float scale=Mathf.Clamp(landmark.scale,1,600);
                float amplitude=FloatingAmplitude(high.bounds.size.y*scale,profile.levitationAmplitude);
                Bounds envelope=FloatingMotionEnvelope(high.bounds,scale,position,rotation,amplitude,profile.horizontalMotionFraction,profile.rockingDegrees.y);
                bool blocked=envelope.SqrDistance(center)<Mathf.Pow(profile.planetRadius+profile.arenaExclusionPadding,2);
                foreach(var prior in occupied)if(prior.Intersects(envelope))blocked=true;
                if(profile.exclusionVolumes!=null)foreach(var box in profile.exclusionVolumes)
                    if(envelope.Intersects(WorldBounds(box,Matrix4x4.TRS(center,Quaternion.LookRotation(forward,_up),Vector3.one))))blocked=true;
                if(blocked){RejectedPlacements++;continue;}
                var pivot=new GameObject("View_"+landmark.name).transform;pivot.SetParent(generatedRoot,false);pivot.SetPositionAndRotation(position,rotation);
                var highRenderers=new List<Renderer>();AddRenderer(pivot,"LOD0",high,Vector3.one*scale,highRenderers);
                if(low!=null){var lowRenderers=new List<Renderer>();AddRenderer(pivot,"LOD1",low,Vector3.one*scale,lowRenderers);var lod=pivot.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.13f,highRenderers.ToArray()),new LOD(.003f,lowRenderers.ToArray())});lod.RecalculateBounds();}
                var motion=new RockRandom(unchecked(profile.motionSeed+(200+i)*19349663));
                drift.Add(new Drift{target=pivot,origin=position,rotation=rotation,tangent=right,bitangent=forward,phase=motion.Next(0,1),period=motion.Next(profile.levitationPeriod.x,profile.levitationPeriod.y),amplitude=amplitude,angle=motion.Next(profile.rockingDegrees.x,profile.rockingDegrees.y),horizontalFraction=profile.horizontalMotionFraction,centerOffset=high.bounds.center*scale});
                occupied.Add(envelope);
            }
            if(profile.viewLandmarks!=null)for(int i=0;i<profile.viewLandmarks.Length;i++)
            {
                var landmark=profile.viewLandmarks[i];if(!landmark.airborne||(!landmark.name.StartsWith("MainIsland")&&!landmark.name.StartsWith("CombatIsland")))continue;
                var pivot=generatedRoot.Find("View_"+landmark.name);if(pivot==null)continue;
                int index=100+i;bool exists=false;foreach(Transform child in generatedRoot)if(child.name.StartsWith("Satellite_"+index+"_")){exists=true;break;}if(exists)continue;
                var high=pivot.Find("LOD0")?.GetComponent<MeshFilter>();var low=pivot.Find("LOD1")?.GetComponent<MeshFilter>();if(high==null)continue;
                float scale=high.transform.lossyScale.x;
                Bounds envelope=WorldBounds(high.sharedMesh.bounds,high.transform.localToWorldMatrix);
                AddSatellites(index,pivot.position,pivot.rotation,scale,high.sharedMesh,low!=null?low.sharedMesh:null,envelope,occupied,center,forward,right);
            }
        }
    }
}
