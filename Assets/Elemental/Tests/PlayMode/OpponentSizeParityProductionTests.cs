using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode {
 public sealed partial class HardPolishFireStreamBindingRuntimeTests {
  [Serializable] private sealed class BodyParityReport {public float playerWorldVertexHeight,botWorldVertexHeight,ratio;public Vector3 playerScale,botScale;}
  [UnityTest,Timeout(240000)] public IEnumerator ActualSavedOpponentMatchesPlayerSkinnedGeometryHeight(){
   yield return EnterCombat();yield return new WaitForEndOfFrame();
   var report=new BodyParityReport{playerWorldVertexHeight=ActualBodyVertexHeight(duel.PlayerTransform),botWorldVertexHeight=ActualBodyVertexHeight(duel.BotTransform),playerScale=duel.PlayerTransform.localScale,botScale=duel.BotTransform.localScale};
   report.ratio=report.botWorldVertexHeight/report.playerWorldVertexHeight;
   Directory.CreateDirectory("BuildReports/HardPolish/CharacterParity");File.WriteAllText("BuildReports/HardPolish/CharacterParity/vertices.json",JsonUtility.ToJson(report,true));
   Assert.That(report.ratio,Is.InRange(.95f,1.05f),"Actual skinned world-vertex height differs; transform parity alone is insufficient.");
  }
  private static float ActualBodyVertexHeight(Transform root){float min=float.PositiveInfinity,max=float.NegativeInfinity;int count=0;
   foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){if(!skin.enabled||!skin.gameObject.activeInHierarchy||skin.sharedMesh==null)continue;
    foreach(var world in IndependentSkinWorldVertices(skin)){float h=Vector3.Dot(world-root.position,root.up);min=Mathf.Min(min,h);max=Mathf.Max(max,h);count++;}}
   Assert.That(count,Is.GreaterThan(0));return max-min;
  }
 }
}
