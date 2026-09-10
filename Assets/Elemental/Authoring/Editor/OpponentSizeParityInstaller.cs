using System;
using System.Linq;
using Elemental.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Elemental.Authoring.Editor {
 public static class OpponentSizeParityInstaller {
  [Serializable] private sealed class Report {public float playerVertexHeight,botVertexHeightBefore,botVertexHeightAfter,factor;public Vector3 botScaleBefore,botScaleAfter;}
  public static string ApplyCurrentScene(){
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Run only in idle Edit mode.");
   var scene=SceneManager.GetActiveScene();if(scene.name!="EarthCoreSlice")throw new InvalidOperationException("Open saved EarthCoreSlice first.");
   var flow=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<FrontendFlowController>(true)).Single();var player=flow.MatchController.PlayerTransform;var bot=flow.MatchController.BotTransform;
   var report=new Report{playerVertexHeight=Height(player),botVertexHeightBefore=Height(bot),botScaleBefore=bot.localScale};report.factor=report.playerVertexHeight/report.botVertexHeightBefore;
   if(!float.IsFinite(report.factor)||report.factor<.75f||report.factor>1.4f)throw new InvalidOperationException("Unexpected geometry ratio; inspect rigs before any scale mutation.");
   var capsule=bot.GetComponent<CapsuleCollider>();if(capsule==null||capsule.direction!=1)throw new InvalidOperationException("Expected authored upright motor capsule.");
   Vector3 feet=CapsuleFeet(capsule);Undo.RecordObject(bot,"Match opponent actual body height");
   bot.localScale*=report.factor;bot.position+=feet-CapsuleFeet(capsule);Physics.SyncTransforms();
   report.botScaleAfter=bot.localScale;report.botVertexHeightAfter=Height(bot);
   if(Mathf.Abs(report.botVertexHeightAfter/report.playerVertexHeight-1)>.005f)throw new InvalidOperationException("Post-scale geometry parity validation failed.");
   EditorUtility.SetDirty(bot);EditorSceneManager.MarkSceneDirty(scene);return JsonUtility.ToJson(report,true);
  }
  private static Vector3 CapsuleFeet(CapsuleCollider c)=>c.transform.TransformPoint(c.center)-c.transform.up*(c.height*Mathf.Abs(c.transform.lossyScale.y)*.5f);
  private static float Height(Transform root){
   float min=float.PositiveInfinity,max=float.NegativeInfinity;int total=0;Vector3 up=root.up;
   foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
    if(!skin.enabled||!skin.gameObject.activeInHierarchy||skin.sharedMesh==null)continue;
    var mesh=skin.sharedMesh;var vertices=mesh.vertices;var binds=mesh.bindposes;var bones=skin.bones;
    for(int i=0;i<mesh.blendShapeCount;i++)if(Mathf.Abs(skin.GetBlendShapeWeight(i))>.001f)throw new InvalidOperationException("Measure an unmodified authored pose before normalizing opponent.");
    using var counts=mesh.GetBonesPerVertex();using var weights=mesh.GetAllBoneWeights();int cursor=0;
    for(int v=0;v<vertices.Length;v++){Vector3 world=Vector3.zero;for(int j=0;j<counts[v];j++){var w=weights[cursor++];world+=(bones[w.boneIndex].localToWorldMatrix*binds[w.boneIndex]).MultiplyPoint3x4(vertices[v])*w.weight;}
     float height=Vector3.Dot(world-root.position,up);min=Mathf.Min(min,height);max=Mathf.Max(max,height);total++;}
   }
   if(total==0||max-min<.5f)throw new InvalidOperationException("No valid actual skinned body geometry.");return max-min;
  }
 }
}
