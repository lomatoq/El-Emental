using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Elemental.Presentation.Fire;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
using Elemental.Runtime.Physics;
namespace Elemental.Authoring.Editor
{
 public static class ArenaColumnFireSetup
 {
  [MenuItem("Elemental/Graphics/Install Arena Column Fires")]
  public static void Install()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play before installing column flames.");
   var scene=SceneManager.GetActiveScene();if(scene.name!="EarthCoreSlice")throw new InvalidOperationException("Open EarthCoreSlice.");
   GameObject ring=null,owned=null;CelestialSystemBehaviour sky=null;FrontendFlowController flow=null;
   foreach(var root in scene.GetRootGameObjects())
   {if(root.name==OuterStoneRingImporter.SceneRootName)ring=root;if(root.name==ArenaColumnFires.OwnedName)owned=root;sky=root.GetComponentInChildren<CelestialSystemBehaviour>(true)??sky;flow=root.GetComponentInChildren<FrontendFlowController>(true)??flow;}
   if(ring==null || sky==null)throw new InvalidOperationException("Authored outer stone ring and celestial owner required.");
   var columns=ring.GetComponentsInChildren<EarthArenaStructure>(true);Array.Sort(columns,(a,b)=>string.CompareOrdinal(a.name,b.name));
   if(columns.Length!=7)throw new InvalidOperationException("Expected exactly seven canonical outer columns; refusing guessed locations.");
   var source=AssetDatabase.LoadAssetAtPath<FireVisualProfile>("Assets/Elemental/Content/VFX/Fire/Fire_Default.asset");
   if(source==null || source.CpuMaterial==null || ShaderUtil.ShaderHasError(source.CpuMaterial.shader))throw new InvalidOperationException("Import validated Fire_Default CPU presentation first.");
   const string path="Assets/Elemental/Content/VFX/Fire/Fire_ColumnDecor.asset";
   var profile=AssetDatabase.LoadAssetAtPath<FireVisualProfile>(path);if(profile==null){profile=UnityEngine.Object.Instantiate(source);profile.name="Fire_ColumnDecor";AssetDatabase.CreateAsset(profile,path);}
   profile.Backend=FireVisualBackendSelection.CpuMesh;profile.CpuCapacity=256;profile.SpawnRate=50;profile.Substeps=1;profile.MaximumSpeed=4;
   profile.MinLifetime=.5f;profile.MaxLifetime=.9f;profile.FlameMinWidth=.22f;profile.FlameMaxWidth=.42f;profile.FreeLift=1.5f;EditorUtility.SetDirty(profile);
   var seats=new List<ArenaColumnFires.Seat>();Vector3 up=ring.transform.up;
   foreach(var column in columns)
   {
    var filter=column.GetComponent<MeshFilter>();var renderer=column.GetComponent<MeshRenderer>();if(filter==null || renderer==null || filter.sharedMesh==null)throw new InvalidOperationException("Missing column mesh: "+column.name);
    var vertices=filter.sharedMesh.vertices;float top=float.MinValue,bottom=float.MaxValue;
    foreach(var v in vertices){float h=Vector3.Dot(filter.transform.TransformPoint(v),up);top=Mathf.Max(top,h);bottom=Mathf.Min(bottom,h);}
    Vector3 point=Vector3.zero;int count=0;float band=Mathf.Max(.08f,(top-bottom)*.025f);
    foreach(var v in vertices)if(Vector3.Dot(filter.transform.TransformPoint(v),up)>=top-band){point+=filter.transform.TransformPoint(v);count++;}
    if(count==0)throw new InvalidOperationException("No top vertices: "+column.name);point/=count;point+=up*.05f;
    seats.Add(new ArenaColumnFires.Seat{Source=renderer,LocalPoint=filter.transform.InverseTransformPoint(point),Up=up});
   }
   if(owned!=null && owned.GetComponent<ArenaColumnFires>()==null)throw new InvalidOperationException("Owned fire root name collision.");
   if(owned==null){owned=new GameObject(ArenaColumnFires.OwnedName);Undo.RegisterCreatedObjectUndo(owned,"Install column fires");}
   var fire=owned.GetComponent<ArenaColumnFires>();if(fire==null)fire=Undo.AddComponent<ArenaColumnFires>(owned);
   fire.Configure(seats.ToArray(),profile,sky,flow);EditorUtility.SetDirty(fire);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
   Debug.Log("Seven cosmetic column-top fires installed; four nearest nonshadowed warm lights, canonical column state untouched. Scene left dirty.");
  }
 }
}
