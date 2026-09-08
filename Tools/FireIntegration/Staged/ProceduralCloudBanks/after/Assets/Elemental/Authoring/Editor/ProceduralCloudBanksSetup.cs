using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
namespace Elemental.Authoring.Editor
{
 public static class ProceduralCloudBanksSetup
 {
  [MenuItem("Elemental/Graphics/Install Complementary Procedural Cloud Banks")]
  public static void Install()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Leave Play before installing cloud banks.");
   var scene=SceneManager.GetActiveScene();if(scene.name!="EarthCoreSlice")throw new System.InvalidOperationException("Open EarthCoreSlice.");
   ValleyAtmosphereController frame=null;FrontendFlowController flow=null;
   foreach(var root in scene.GetRootGameObjects()){frame=root.GetComponentInChildren<ValleyAtmosphereController>(true)??frame;flow=root.GetComponentInChildren<FrontendFlowController>(true)??flow;}
   if(frame==null)throw new System.InvalidOperationException("Install authored Valley Atmosphere V2 first.");
   var shader=Shader.Find("Elemental/Procedural Cloud Banks");if(shader==null || ShaderUtil.ShaderHasError(shader))throw new System.InvalidOperationException("Import and compile ProceduralCloudBanks.shader first.");
   const string path="Assets/Elemental/Content/Materials/ProceduralCloudBanks.mat";
   var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
   var child=frame.transform.Find(ProceduralCloudBanks.OwnedName);
   if(child!=null && child.GetComponent<ProceduralCloudBanks>()==null)throw new System.InvalidOperationException("Owned name collision.");
   var go=child!=null?child.gameObject:new GameObject(ProceduralCloudBanks.OwnedName);
   if(child==null)Undo.RegisterCreatedObjectUndo(go,"Install procedural clouds");
   var banks=go.GetComponent<ProceduralCloudBanks>();if(banks==null)banks=Undo.AddComponent<ProceduralCloudBanks>(go);
   banks.Configure(frame,flow,Resources.GetBuiltinResource<Mesh>("Cube.fbx"),material);go.SetActive(true);
   EditorUtility.SetDirty(banks);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
   Debug.Log("Six complementary procedural banks installed; existing image clouds preserved. Scene is dirty for visual review.");
  }
 }
}
