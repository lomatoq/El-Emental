using System.Collections;
using Elemental.Presentation.Fire;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed class FireSubmeshSubmissionTests
 {
  private sealed class OpenGas:IFireFlowCollision{public bool Sweep(float3 p,float r,float3 d,out FireFlowHit hit){hit=default;return false;}}
  [UnityTest]public IEnumerator ChangingHotAndSmokePartitionsNeverSubmitOverlappingIndexRanges()
  {
   GameObject owner=null,cameraObject=null;FireFlipbookAccentLayer layer=null;object surface=null;System.Type surfaceType=null;int overlapWarnings=0;
   void Log(string message,string trace,LogType type){if(message.Contains("shares part of its index buffer"))overlapWarnings++;}
   Application.logMessageReceived+=Log;
   try
   {
    owner=new GameObject("Atomic fire submesh proof");cameraObject=new GameObject("Atomic fire proof camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(0,1,-5);
    layer=new FireFlipbookAccentLayer(owner.transform);var mesh=owner.GetComponentInChildren<MeshFilter>(true).sharedMesh;
    var solver=new FireFlowParticleSolver(false,64);var world=new OpenGas();solver.Injection=new FireFlowInjection(0,4,110,.3f,5,.8f,coolingTail:.4f,smokeStride:4);
    int previousHot=0,growsOverSmoke=0,smokeFrames=0;
    for(int frame=0;frame<280;frame++)
    {
     layer.SetDetailBudget(frame%60<20?6:frame%60<40?18:12,12,.8f,.8f);
     solver.Step(1f/60,frame%70<48,1,0,new float3(0,1,0),new float3(0,1,0),world);layer.Step(solver,camera);
     if(layer.VisibleCards==0)continue;
     var hot=mesh.GetSubMesh(0);var smoke=mesh.GetSubMesh(1);
     Assert.That(hot.indexStart,Is.Zero);Assert.That(hot.indexCount,Is.EqualTo(layer.HotCards*6));
     Assert.That(smoke.indexStart,Is.EqualTo(hot.indexCount));Assert.That(smoke.indexCount,Is.EqualTo(layer.SmokeCards*6));
     Assert.That(smoke.indexStart+smoke.indexCount,Is.LessThanOrEqualTo(FireFlipbookAccentLayer.MaximumCards*6));
     if(layer.SmokeCards>0){smokeFrames++;if(layer.HotCards>previousHot)growsOverSmoke++;}previousHot=layer.HotCards;
    }
    Assert.That(growsOverSmoke,Is.GreaterThan(3),"Exercise the growing-hot-range state that used to overlap the previous smoke range.");
    Assert.That(smokeFrames,Is.GreaterThan(50));
    surfaceType=typeof(FireFlipbookAccentLayer).Assembly.GetType("Elemental.Presentation.Fire.FireSurfaceFlameRenderer",true);
    surface=System.Activator.CreateInstance(surfaceType,new object[]{owner.transform,Resources.Load<Material>("FireFlipbookAccentMaterial"),0});
    yield return null;
    Assert.That(overlapWarnings,Is.Zero,"Atomic submission must not emit Unity overlap warnings, including surface constructor setup.");
   }
   finally{Application.logMessageReceived-=Log;if(surface!=null)surfaceType.GetMethod("Dispose").Invoke(surface,null);layer?.Dispose();if(owner!=null)Object.Destroy(owner);if(cameraObject!=null)Object.Destroy(cameraObject);}
  }
 }
}
