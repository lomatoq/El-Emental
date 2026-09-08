using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Elemental.Presentation.Rendering;

public class CommandScript : IRunCommand
{
 public void Execute(ExecutionResult result)
 {
  if(!Application.isPlaying)throw new InvalidOperationException("Run only in Play after a real rock/column fracture. This command does not fracture anything.");
  CelestialSystemBehaviour sky=null;
  foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())sky=root.GetComponentInChildren<CelestialSystemBehaviour>(true)??sky;
  Camera camera=sky!=null?sky.TargetCamera:null;if(camera==null)throw new InvalidOperationException("Missing production celestial camera.");
  MeshRenderer chosen=null;Mesh chosenMesh=null;Vector3 chosenPoint=default,chosenNormal=default;float best=float.MaxValue;
  foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(false))
  {
   if(!renderer.enabled || !renderer.gameObject.activeInHierarchy)continue;
   var filter=renderer.GetComponent<MeshFilter>();if(filter==null || filter.sharedMesh==null || !filter.sharedMesh.isReadable)continue;
   bool rock=false;foreach(var material in renderer.sharedMaterials)if(material!=null && material.shader.name=="Elemental/Graphics V5/Rumble Rock Lit")rock=true;
   if(!rock)continue;
   var mesh=filter.sharedMesh;var colors=mesh.colors;var vertices=mesh.vertices;if(colors.Length!=vertices.Length)continue;
   var triangles=mesh.triangles;
   for(int i=0;i<triangles.Length;i+=3)
   {
    int ia=triangles[i],ib=triangles[i+1],ic=triangles[i+2];
    if(colors[ia].g*(1-colors[ia].r)<.5f || colors[ib].g*(1-colors[ib].r)<.5f || colors[ic].g*(1-colors[ic].r)<.5f)continue;
    Vector3 a=renderer.transform.TransformPoint(vertices[ia]),b=renderer.transform.TransformPoint(vertices[ib]),c=renderer.transform.TransformPoint(vertices[ic]);
    Vector3 normal=Vector3.Cross(b-a,c-a);if(normal.sqrMagnitude<.01f)continue;normal.Normalize();Vector3 point=(a+b+c)/3;
    float distance=(point-camera.transform.position).sqrMagnitude;
    if(Vector3.Dot(normal,camera.transform.position-point)<=0 || distance>=best)continue;
    chosen=renderer;chosenMesh=mesh;chosenPoint=point;chosenNormal=normal;best=distance;
   }
  }
  if(chosen==null)throw new InvalidOperationException("No active visible green-classified cut triangle found. Perform a real fracture first; no fabricated surrogate was captured.");
  var materials=new List<Material>();var modes=new List<float>();
  foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<Renderer>(false))foreach(var material in renderer.sharedMaterials)
   if(material!=null && material.shader.name=="Elemental/Graphics V5/Rumble Rock Lit" && !materials.Contains(material)){materials.Add(material);modes.Add(material.GetFloat("_DebugMode"));}
  Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView;var target=camera.targetTexture;var active=RenderTexture.active;
  var urp=camera.GetComponent<UniversalAdditionalCameraData>();bool post=urp!=null && urp.renderPostProcessing;
  var dof=camera.GetComponent<EarthCinematicDepthOfFieldController>();bool dofEnabled=dof!=null && dof.enabled;
  var rt=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);rt.Create();var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);
  string folder="BuildReports/StoneSurfaceSoftness/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmss");Directory.CreateDirectory(folder);
  try
  {
   float range=Mathf.Clamp(chosen.bounds.extents.magnitude*1.7f,1.5f,7);
   Vector3 up=Vector3.ProjectOnPlane(camera.transform.up,chosenNormal).normalized;if(up.sqrMagnitude<.1f)up=Vector3.ProjectOnPlane(Vector3.forward,chosenNormal).normalized;
   camera.transform.SetPositionAndRotation(chosenPoint+chosenNormal*range+up*range*.18f,Quaternion.LookRotation(-chosenNormal-up*.18f,up));
   camera.fieldOfView=40;camera.targetTexture=rt;if(urp!=null)urp.renderPostProcessing=false;if(dof!=null)dof.enabled=false;
   int[] values={0,5,2};string[] names={"Lit-no-post","Albedo","Normals"};
   for(int i=0;i<values.Length;i++)
   {
    foreach(var material in materials)material.SetFloat("_DebugMode",values[i]);camera.Render();RenderTexture.active=rt;
    texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();File.WriteAllBytes(folder+"/"+names[i]+".png",texture.EncodeToPNG());
   }
   File.WriteAllText(folder+"/evidence.txt","UTC="+DateTime.UtcNow.ToString("O")+"\nRenderer="+chosen.name+"\nMesh="+chosenMesh.name+"\nCutPoint="+chosenPoint+"\nCutNormal="+chosenNormal+"\nPost processing and DOF temporarily disabled consistently; actual active cut triangle, geometry unchanged. Camera/material debug states restored.\n");
   Debug.Log("Actual cut-face diagnostics: "+folder+" renderer="+chosen.name);
  }
  finally
  {
   for(int i=0;i<materials.Count;i++)materials[i].SetFloat("_DebugMode",modes[i]);
   camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;camera.targetTexture=target;RenderTexture.active=active;
   if(urp!=null)urp.renderPostProcessing=post;if(dof!=null)dof.enabled=dofEnabled;
   UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
  }
 }
}
