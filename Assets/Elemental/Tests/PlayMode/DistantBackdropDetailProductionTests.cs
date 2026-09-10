#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class DistantBackdropProductionTests
    {
        [UnityTest] public IEnumerator SavedOuterHalfArchActualRenderDetail()
        {
            string folder="BuildReports/HardPolish/G01/HalfArch-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
            var filters=All<MeshFilter>().Where(f=>f.name.Contains("OuterArch")&&f.name.Contains("INTACT")).OrderBy(f=>f.name).ToArray();
            Assert.That(filters.Length,Is.EqualTo(7));var log=new StringBuilder();
            Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            var pipeline=camera.GetUniversalAdditionalCameraData();bool post=pipeline.renderPostProcessing;
            try
            {
                foreach(var f in filters)
                {
                    var r=f.GetComponent<Renderer>();log.AppendLine(f.name+" mesh="+f.sharedMesh.name+" tris="+f.sharedMesh.triangles.Length/3+" source="+AssetDatabase.GetAssetPath(f.sharedMesh)+" enabled="+r.enabled);
                    Vector3 up=flow.MatchController.PlayerTransform.up;
                    float radius=r.bounds.extents.magnitude;
                    Vector3 inward=Vector3.ProjectOnPlane(flow.MatchController.PlayerTransform.position-r.bounds.center,up).normalized;
                    Vector3 side=Vector3.Cross(up,inward);
                    // Inspect from outside the ring so the arena's small front columns
                    // cannot occlude the half-arch whose geometry is under review.
                    camera.transform.position=r.bounds.center+(-inward*.8f+side*.6f).normalized*radius*2.4f+up*radius*.15f;
                    camera.transform.rotation=Quaternion.LookRotation(r.bounds.center-camera.transform.position,up);camera.fieldOfView=52;
                    Capture(f.name+"-production",folder);
                    pipeline.renderPostProcessing=false;Capture(f.name+"-no-post-diagnostic",folder);pipeline.renderPostProcessing=post;
                    var owner=f.GetComponent<EarthArenaStructure>();
                    var piece=All<EarthArenaPiece>().Last(p=>p.Owner==owner);
                    var pieceFilter=piece.GetComponent<MeshFilter>();
                    Assert.That(owner.TryPluckCell(pieceFilter.transform.TransformPoint(pieceFilter.sharedMesh.bounds.center),out var released),Is.True);
                    Capture(f.name+"-after-damage",folder);
                    owner.RestoreArenaStructure();
                    Capture(f.name+"-restored",folder);
                }
            }
            finally{foreach(var f in filters)f.GetComponent<EarthArenaStructure>().RestoreArenaStructure();camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;pipeline.renderPostProcessing=post;File.WriteAllText(folder+"/owners.txt",log.ToString());}
            yield return null;
        }
        [UnityTest] public IEnumerator SavedSurroundingDecorDetailComparison()
        {
            string folder="BuildReports/HardPolish/G01/DecorDetail-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(folder);
            var groups=backdrop.GetComponentsInChildren<LODGroup>(true);
            Assert.That(groups.Length,Is.GreaterThan(0));
            bool active=backdrop.enabled;backdrop.enabled=false;
            Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
            var data=new StringBuilder("view,name,relativeHeight,highThreshold,estimatedLod,highTriangles,lowTriangles\n");
            try
            {
                for(int view=0;view<3;view++)
                {
                    camera.transform.rotation=Quaternion.AngleAxis(view*120,camera.transform.up)*rotation;
                    var planes=GeometryUtility.CalculateFrustumPlanes(camera);
                    foreach(var group in groups)
                    {
                        var levels=group.GetLODs();if(levels.Length<2)continue;
                        var r=levels[0].renderers[0];if(!GeometryUtility.TestPlanesAABB(planes,r.bounds))continue;
                        Vector3 scale=group.transform.lossyScale;
                        float height=group.size*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z))*QualitySettings.lodBias/
                            (2*Vector3.Distance(camera.transform.position,group.transform.TransformPoint(group.localReferencePoint))*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f));
                        int high=r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3;
                        int low=levels[1].renderers[0].GetComponent<MeshFilter>().sharedMesh.triangles.Length/3;
                        data.AppendLine(FormattableString.Invariant($"{view},{group.name},{height},{levels[0].screenRelativeTransitionHeight},{(height>=levels[0].screenRelativeTransitionHeight?0:1)},{high},{low}"));
                    }
                    foreach(var group in groups)group.ForceLOD(-1);
                    Capture("view-"+view+"-automatic",folder);
                    foreach(var group in groups)group.ForceLOD(0);
                    Capture("view-"+view+"-high",folder);
                }
            }
            finally
            {
                foreach(var group in groups)group.ForceLOD(-1);
                camera.transform.SetPositionAndRotation(position,rotation);backdrop.enabled=active;
                File.WriteAllText(folder+"/lod-estimate.csv",data.ToString());
            }
            yield return null;
        }
    }
}
#endif
