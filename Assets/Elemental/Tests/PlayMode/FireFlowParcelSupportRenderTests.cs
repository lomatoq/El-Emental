using System.Collections;
using System.IO;
using Elemental.Presentation.Fire;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
 public sealed class FireFlowParcelSupportRenderTests
 {
  [UnityTest] public IEnumerator IsolatedTransportedParcelOutsideRotatedAndInsideNativeDiagnostic()
  {
   const string path="BuildReports/HardPolish/G05/FluidSupport";Directory.CreateDirectory(path);
   var owner=new GameObject("Owned isolated flow support proof");var cameraObject=new GameObject("Owned support proof camera");
   var camera=cameraObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.02f,.02f);
   camera.cullingMask=1<<31;camera.depth=1000;camera.nearClipPlane=.02f;
   camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
   var output=new ProductionCaptureResolution();FireFlowVolumeBackend gas=null;
   try
   {
    yield return output.WaitForRenderedSize(camera);
    gas=new FireFlowVolumeBackend(owner.transform,Resources.Load<Shader>("FireFlowParcel"),0);gas.SetRenderingLayerForQa(31);
    var snap=new FirePresentationSnapshot{Lifecycle=FireLifecycle.Active,Energy=1,NodeCount=1,FreeUp=new float3(0,1,0)};
    float3 centre=new float3(0,100,0);snap.Nodes[0]=FireFieldNode.Stream(centre,centre+new float3(0,0,8),new float3(0,0,14),new float3(0,1,0));
    gas.Step(snap,.0042f,camera);Assert.That(gas.Solver.Count,Is.EqualTo(1));snap.Energy=0;
    for(int shot=0;shot<5;shot++)
    {
     var particle=gas.Solver.Particles[0];particle.Position=centre;particle.Age=.15f;particle.Velocity=shot==0?new float3(0,0,14):new float3(8,5,10);
     gas.Solver.Particles[0]=particle;gas.SetDebugViewForQa(shot==4?0:2);gas.Step(snap,0,camera);
     Vector3 offset=shot==3?Vector3.zero:Quaternion.AngleAxis(shot*35,Vector3.up)*new Vector3(0,0,-2);
     camera.transform.position=(Vector3)centre+offset;camera.transform.LookAt((Vector3)centre+(shot==3?Vector3.forward:Vector3.zero));
     yield return new WaitForEndOfFrame();ProductionCaptureResolution.SaveScreen(Path.Combine(path,"parcel-"+shot+".png"));
    }
    Assert.That(gas.Solver.Collisions,Is.Zero,"The isolated proof must not contain contact-plane cuts.");
   }
   finally{gas?.Dispose();output.Dispose();Object.Destroy(owner);Object.Destroy(cameraObject);File.WriteAllText(Path.Combine(path,"SCOPE.txt"),"One transported parcel, zero collision mask/planes, padded rotated proxy. Shots0-2 depth-free analytic support, shot3 camera-inside support, shot4 additive radiance without postprocessing. Use to diagnose proxy clipping; no art/bloom acceptance inferred.");}
  }
 }
}
