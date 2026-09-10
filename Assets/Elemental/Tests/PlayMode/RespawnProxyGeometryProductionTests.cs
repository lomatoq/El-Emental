using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Presentation.VFX;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode {
 public sealed partial class HardPolishFireStreamBindingRuntimeTests {
  [Serializable] private sealed class ProxyPartError {public string source;public int vertices;public float maxWorldError,rmsWorldError;public Vector3 expectedSize,proxySize;}
  [Serializable] private sealed class ProxyGeometryReport {public string scope="Actual saved fighter, same-pose independent CPU bone-weight/bindpose world vertices versus static gold proxy; no renderer culling bounds or transform-factor oracle.";public List<ProxyPartError> parts=new();}
  [UnityTest,Timeout(240000)] public IEnumerator ActualSavedGoldProxyMatchesIndependentSkinnedWorldVertices(){
   var actor=duel.PlayerTransform;var report=new ProxyGeometryReport();object proxy=null;GameObject parent=null;
   var type=typeof(GoldRespawnPresenter).Assembly.GetType("Elemental.Presentation.VFX.RespawnVisualProxy");
   string folder="BuildReports/HardPolish/RespawnGeometry-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
   try {
    yield return new WaitForEndOfFrame();
    parent=new GameObject("Owned gold geometry diagnostic");proxy=Activator.CreateInstance(type,true);
    Vector3 feet=actor.GetComponent<Elemental.Runtime.Characters.PlanetMotor>().SupportFeetPoint(actor.up);
    type.GetMethod("Capture").Invoke(proxy,new object[]{actor,parent.transform,feet,false});
    type.GetMethod("RenderAtActorPose").Invoke(proxy,new object[]{actor.position,actor.rotation,1f,0f,actor.up,Color.black});
    var parts=(IEnumerable)type.GetField("_parts",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(proxy);
    foreach(object part in parts){var partType=part.GetType();var source=(Renderer)partType.GetField("Source").GetValue(part);var view=(Renderer)partType.GetField("Renderer").GetValue(part);
     Vector3[] expected=IndependentSkinWorldVertices(source),local=view.GetComponent<MeshFilter>().sharedMesh.vertices;
     Assert.That(local.Length,Is.EqualTo(expected.Length));float max=0,sum=0;Bounds a=new Bounds(expected[0],Vector3.zero),b=new Bounds(view.transform.TransformPoint(local[0]),Vector3.zero);
     for(int i=0;i<local.Length;i++){Vector3 actual=view.transform.TransformPoint(local[i]);float error=Vector3.Distance(expected[i],actual);max=Mathf.Max(max,error);sum+=error*error;a.Encapsulate(expected[i]);b.Encapsulate(actual);}
     report.parts.Add(new ProxyPartError{source=source.name,vertices=local.Length,maxWorldError=max,rmsWorldError=Mathf.Sqrt(sum/local.Length),expectedSize=a.size,proxySize=b.size});
    }
    Assert.That(report.parts.Count,Is.GreaterThan(0));
    foreach(var part in report.parts)Assert.That(part.maxWorldError,Is.LessThan(.005f),part.source+" gold proxy changed actual rendered world vertices; expected "+part.expectedSize+" proxy "+part.proxySize);
   } finally{if(proxy is IDisposable disposable)disposable.Dispose();if(parent!=null)UnityEngine.Object.Destroy(parent);File.WriteAllText(folder+"/report.json",JsonUtility.ToJson(report,true));}
  }
  private static Vector3[] IndependentSkinWorldVertices(Renderer source){
   if(source is not SkinnedMeshRenderer skin){var mesh=source.GetComponent<MeshFilter>().sharedMesh;return mesh.vertices.Select(source.transform.TransformPoint).ToArray();}
   var shared=skin.sharedMesh;Vector3[] vertices=shared.vertices;
   for(int shape=0;shape<shared.blendShapeCount;shape++){float weight=skin.GetBlendShapeWeight(shape);if(Mathf.Abs(weight)<.00001f)continue;
    Assert.That(shared.GetBlendShapeFrameCount(shape),Is.EqualTo(1),"Oracle needs explicit multi-frame blendshape interpolation before accepting this actor.");
    var deltas=new Vector3[vertices.Length];shared.GetBlendShapeFrameVertices(shape,0,deltas,null,null);float factor=weight/shared.GetBlendShapeFrameWeight(shape,0);
    for(int i=0;i<vertices.Length;i++)vertices[i]+=deltas[i]*factor;
   }
   var bones=skin.bones;var bind=shared.bindposes;var matrices=new Matrix4x4[bind.Length];
   for(int i=0;i<bind.Length;i++){Assert.That(bones[i],Is.Not.Null);matrices[i]=bones[i].localToWorldMatrix*bind[i];}
   using var counts=shared.GetBonesPerVertex();using var weights=shared.GetAllBoneWeights();int cursor=0;
   for(int i=0;i<vertices.Length;i++){Vector3 v=Vector3.zero;float total=0;for(int j=0;j<counts[i];j++){var w=weights[cursor++];v+=matrices[w.boneIndex].MultiplyPoint3x4(vertices[i])*w.weight;total+=w.weight;}
    Assert.That(total,Is.GreaterThan(.99f));vertices[i]=v;
   }
   return vertices;
  }
 }
}
