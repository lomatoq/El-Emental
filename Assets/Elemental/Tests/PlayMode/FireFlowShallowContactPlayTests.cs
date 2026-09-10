using System.Collections;
using Elemental.Runtime.Fire;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
 public sealed class FireFlowShallowContactPlayTests
 {
  [UnityTest] public IEnumerator NonconvexGrowthOverlapUsesRealTriangleAndKeepsCornerClosed()
  {
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=31;
   floor.transform.position=new Vector3(0,100,0);floor.transform.localScale=new Vector3(20,1,20);
   floor.GetComponent<BoxCollider>().enabled=false;
   var mesh=floor.AddComponent<MeshCollider>();mesh.sharedMesh=floor.GetComponent<MeshFilter>().sharedMesh;
   var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=31;
   wall.transform.position=new Vector3(1,101,0);wall.transform.localScale=new Vector3(.2f,3,20);
   try
   {
    Physics.SyncTransforms();yield return null;using var adapter=new FireFlowCollisionAdapter(1<<31);
    float3 center=new float3(-.07990486f,100.9063f,-.02863461f);const float radius=.4563153f;
    Assert.That(adapter.Sweep(center,radius,new float3(.02f,0,0),out var contact),Is.True);
    Assert.That(contact.Blocked,Is.False);Assert.That(contact.Point.y,Is.EqualTo(100.5f).Within(.001f));
    Assert.That(contact.Normal.y,Is.GreaterThan(.99f));Assert.That(adapter.MeshRecoveryRays,Is.LessThanOrEqualTo(6));
    center.y=100.5f+radius+.004f;
    Assert.That(adapter.Sweep(center,radius,new float3(2,0,0),out contact),Is.True);
    Assert.That(contact.Blocked,Is.False);Assert.That(contact.Normal.x,Is.LessThan(-.99f));
    Assert.That(center.x+2*contact.Fraction+radius,Is.LessThanOrEqualTo(.901f));
   }
   finally{Object.Destroy(floor);Object.Destroy(wall);}
  }

  [UnityTest] public IEnumerator SurfaceBandKeepsDenseGasAboveNonconvexFloor()
  {
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Isolated fire nonconvex floor";floor.layer=31;
   floor.transform.position=new Vector3(0,100,0);floor.transform.localScale=new Vector3(20,1,20);
   var original=floor.GetComponent<BoxCollider>();original.enabled=false;
   var mesh=floor.AddComponent<MeshCollider>();mesh.sharedMesh=floor.GetComponent<MeshFilter>().sharedMesh;mesh.convex=false;
   try
   {
    Physics.SyncTransforms();yield return null;
    var nearby=Physics.OverlapBox(floor.transform.position,new Vector3(10,2,10),Quaternion.identity,1<<31,QueryTriggerInteraction.Ignore);
    Assert.That(nearby.Length,Is.EqualTo(1),"Diagnostic layer must contain only the known nonconvex floor.");
    Assert.That(nearby[0],Is.SameAs(mesh));
    using var adapter=new FireFlowCollisionAdapter(1<<31);
    var solver=new Elemental.Simulation.Fire.FireFlowParticleSolver(false,48);
    solver.Injection=new Elemental.Simulation.Fire.FireFlowInjection(new float3(1.8f,0,0),1.6f,240,.19f,4,1.15f,developmentScale:2,tailAgeScale:.85f,staggerBirths:true,birthTangent:new float3(.45f,0,0));
    int minimum=48;float spread=0;
    for(int frame=0;frame<90;frame++)
    {
     solver.Step(1f/90,true,1,new float3(0,100.98f,0),new float3(0,-1,0),new float3(0,1,0),adapter);
     if(frame>30)minimum=System.Math.Min(minimum,solver.Count);
     for(int i=0;i<solver.Count;i++){Assert.That(solver.Particles[i].Position.y,Is.GreaterThan(100.49f));spread=math.max(spread,math.abs(solver.Particles[i].Position.x));}
     Assert.That(solver.QueryCount,Is.LessThanOrEqualTo(288));
    }
    Assert.That(minimum,Is.GreaterThan(28),"Ground contact must retain a dense short-lived band, not discard nearly all gas.");
    Assert.That(spread,Is.GreaterThan(.4f));Assert.That(solver.Collisions,Is.GreaterThan(0));
    string diagnostic=$"Invalid={solver.InvalidContacts}, blocked={solver.BlockedParcels}, unresolvedMesh={adapter.UnresolvedMeshOverlaps}, recovered={adapter.RecoveredMeshOverlaps}, recoveryRays={adapter.MeshRecoveryRays}, deepMesh={adapter.DeepMeshOverlaps}, sentinel={adapter.InitialCastSentinels}, last={adapter.LastBlockReason}, collider={adapter.LastBlockedCollider}, center={adapter.LastBlockedPosition}, radius={adapter.LastBlockedRadius}, invalidCenter={solver.LastInvalidCenter}, invalidPoint={solver.LastInvalidContact.Point}, invalidRadius={solver.LastInvalidRadius}";
    Assert.That(solver.InvalidContacts,Is.Zero,diagnostic);
    Assert.That(solver.BlockedParcels,Is.Zero,diagnostic);
   }
   finally{Object.Destroy(floor);}
  }

  [UnityTest] public IEnumerator BroadphaseSkinAllowsTangentAndAwayButStillCatchesInwardMotion()
  {
   var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
   wall.transform.position=new Vector3(0,100,3.3f);wall.transform.localScale=new Vector3(12,12,.6f);
   try
   {
    Physics.SyncTransforms();yield return null;using var adapter=new FireFlowCollisionAdapter(~0);
    // Surface distance .402: outside the physical .4 sphere, inside its .404 broadphase shell.
    float3 position=new float3(0,100,2.598f);
    Assert.That(adapter.Sweep(position,.4f,new float3(.06f,0,0),out _),Is.False,"Broadphase skin cannot freeze tangential gas.");
    Assert.That(adapter.Sweep(position,.4f,new float3(0,0,-.06f),out _),Is.False,"Separated gas must leave the wall.");
    Assert.That(adapter.Sweep(position,.4f,new float3(0,0,.02f),out var contact),Is.True);
    Assert.That(contact.Blocked,Is.False);Assert.That(contact.Fraction,Is.GreaterThan(0));
    Assert.That(contact.Point.z,Is.EqualTo(3).Within(.001f));
    Assert.That(position.z+.02f*contact.Fraction+.4f,Is.LessThanOrEqualTo(3.001f));
   }
   finally{Object.Destroy(wall);}
  }

  [UnityTest] public IEnumerator ResolvedInitialPenetrationContinuesAlongWallIntoRealCorner()
  {
   var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);var corner=GameObject.CreatePrimitive(PrimitiveType.Cube);
   wall.transform.position=new Vector3(0,100,3.3f);wall.transform.localScale=new Vector3(12,12,.6f);
   corner.transform.position=new Vector3(1.3f,100,0);corner.transform.localScale=new Vector3(.6f,12,12);
   try
   {
    Physics.SyncTransforms();yield return null;using var adapter=new FireFlowCollisionAdapter(~0);
    float3 position=new float3(0,100,2.6002f);const float radius=.4f;
    Assert.That(adapter.Sweep(position,radius,new float3(.06f,0,0),out var contact),Is.True);
    Assert.That(contact.Blocked,Is.False);Assert.That(contact.Fraction,Is.Zero);
    position+=contact.Normal*math.max(0,radius+.004f-math.dot(position-contact.Point,contact.Normal));
    Assert.That(adapter.Sweep(position,radius,new float3(.06f,0,0),out _),Is.False,"One real correction must not generate repeated zero contacts.");
    Assert.That(adapter.Sweep(position,radius,new float3(1,0,0),out contact),Is.True,"Tangential travel must still hit the perpendicular wall.");
    Assert.That(contact.Blocked,Is.False);Assert.That(contact.Fraction,Is.GreaterThan(.5f));
    Assert.That(contact.Point.x,Is.EqualTo(1).Within(.001f));
    Assert.That(math.dot(contact.Normal,new float3(-1,0,0)),Is.GreaterThan(.999f));
    Assert.That(position.x+contact.Fraction+radius,Is.LessThanOrEqualTo(1.001f));
    Assert.That(position.z+radius,Is.LessThan(3));Assert.That(adapter.Saturations,Is.Zero);
   }
   finally{Object.Destroy(wall);Object.Destroy(corner);}
  }

  [UnityTest] public IEnumerator SubmillimetreGrowingSphereGrazesWallWithoutWorldOriginSentinel()
  {
   var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
   wall.transform.position=new Vector3(0,100,3.3f);wall.transform.localScale=new Vector3(12,12,.6f);
   try
   {
    Physics.SyncTransforms();yield return null;using var adapter=new FireFlowCollisionAdapter(~0);
    // Exact failed production test snapshot,0.18mm sphere/wall penetration at y100.
    float3 position=new float3(1.121329f,100.6905f,2.595302f);float radius=.4048774f;
    float3 displacement=new float3(.06f,.02785f,-.00003f);
    Assert.That(adapter.Sweep(position,radius,displacement,out var contact),Is.True);
    Assert.That(contact.Blocked,Is.False);
    Assert.That(contact.Fraction,Is.EqualTo(0));
    Assert.That(contact.Point.z,Is.EqualTo(3).Within(.001f));
    Assert.That(math.dot(contact.Normal,new float3(0,0,-1)),Is.GreaterThan(.999f));
    Assert.That(math.distance(position,contact.Point),Is.LessThan(radius+.001f));
    for(int step=0;step<30;step++)
    {
     radius+=.001f;
     if(adapter.Sweep(position,radius,displacement,out contact))
     {
      Assert.That(contact.Blocked,Is.False);
      Assert.That(contact.Point.z,Is.EqualTo(3).Within(.001f));
      float3 normal=math.normalizesafe(contact.Normal);
      float3 center=position+displacement*contact.Fraction;
      float correction=math.max(0,radius+.004f-math.dot(center-contact.Point,normal));
      Assert.That(correction,Is.LessThan(.007f),"A shallow contact must not project gas across metres.");
      position=center+normal*correction;
     }
     else position+=displacement;
     Assert.That(position.z+radius,Is.LessThanOrEqualTo(3.0001f));
    }
    Assert.That(adapter.Saturations,Is.Zero);
   }
   finally{Object.Destroy(wall);}
  }
 }
}
