using Elemental.Runtime.Fire;
using NUnit.Framework;
using UnityEngine;
namespace Elemental.Tests.EditMode
{
 public sealed class FireColliderSurfaceTests
 {
  [Test] public void ConcaveTriangleUsesRealSurfaceAndMissCannotBecomeBoundsCenter()
  {
   var go=new GameObject("Actual concave fire receiver");var mesh=new Mesh();
   try
   {
    mesh.vertices=new[]{new Vector3(-2,0,-2),new Vector3(0,0,2),new Vector3(2,0,-2)};mesh.triangles=new[]{0,1,2};mesh.RecalculateBounds();
    var c=go.AddComponent<MeshCollider>();c.sharedMesh=mesh;Physics.SyncTransforms();
    Assert.That(FireColliderSurface.SupportsClosestPoint(c),Is.False);
    Assert.That(FireColliderSurface.TryPoint(c,Vector3.up,Vector3.down,2,out var p),Is.True);Assert.That(p.y,Is.EqualTo(0).Within(.001));
    Assert.That(FireColliderSurface.ContainsSurfacePoint(c,p,Vector3.right),Is.True,"Tangential heat normals still validate a real triangle");
    Assert.That(FireColliderSurface.TryPoint(c,Vector3.up*4,Vector3.up,.2f,out _),Is.False);
    Assert.That(FireColliderSurface.ContainsSurfacePoint(c,Vector3.up,Vector3.up),Is.False);
    UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
   }
   finally{Object.DestroyImmediate(go);Object.DestroyImmediate(mesh);}
  }
 }
}
