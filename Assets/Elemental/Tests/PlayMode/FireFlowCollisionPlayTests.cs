using System.Collections;
using System.IO;
using System.Text;
using Elemental.Runtime.Fire;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
 public sealed class FireFlowCollisionPlayTests
 {
  [UnityTest] public IEnumerator PhysXHeadOnObliqueAndInsideCornerTransportRealParticles()
  {
   var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);var corner=GameObject.CreatePrimitive(PrimitiveType.Cube);
   Vector3 origin=new Vector3(0,100,0);wall.transform.position=origin+Vector3.forward*3.3f;wall.transform.localScale=new Vector3(12,12,.6f);
   corner.transform.position=origin+new Vector3(1.3f,0,1.5f);corner.transform.localScale=new Vector3(.6f,12,8);
   var report=new StringBuilder("case,contacts,peakParcels,peakQueries,spatialSpread,saturations,maxStepMs\n");
   try
   {
    for(int scenario=0;scenario<3;scenario++)
    {
     corner.SetActive(scenario==2);Physics.SyncTransforms();yield return null;
     var solver=new FireFlowParticleSolver();using var world=new FireFlowCollisionAdapter(~0);
     Assert.That(wall.GetComponent<Collider>().enabled&&wall.activeInHierarchy,Is.True);
     Assert.That(Physics.SphereCast(origin,.24f,Vector3.forward,out RaycastHit direct,5,~0,QueryTriggerInteraction.Ignore),Is.True,"Actual wall must be queryable.");
     Assert.That(direct.collider,Is.EqualTo(wall.GetComponent<Collider>()));
     Assert.That(direct.point.z,Is.EqualTo(3).Within(.01f));
     report.AppendLine("direct-wall bounds="+wall.GetComponent<Collider>().bounds+" point="+direct.point+" distance="+direct.distance+" normal="+direct.normal);
     float3 direction=math.normalize(new float3(scenario==1?.6f:scenario==2?.2f:0,0,1));
     float spread=0;int peak=0,queries=0;double maximum=0;
     for(int frame=0;frame<90;frame++)
     {
      long start=System.Diagnostics.Stopwatch.GetTimestamp();solver.Step(1f/90,true,1,origin,direction,new float3(0,1,0),world);
      maximum=System.Math.Max(maximum,(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency);
      peak=System.Math.Max(peak,solver.Count);queries=System.Math.Max(queries,solver.QueryCount);
      for(int i=0;i<solver.Count;i++)
      {
       var p=solver.Particles[i];
       if(p.Position.z>=3.001f||(scenario==2&&p.Position.x>=1.001f))
           report.AppendLine("VIOLATION scenario="+scenario+" frame="+frame+" id="+p.Id+" position="+p.Position+" velocity="+p.Velocity+" age="+p.Age+" radius="+p.Radius+" path="+p.Distance+" contacts="+p.Contacts+" planeA="+p.ClipPlaneA+" planeB="+p.ClipPlaneB+" wallBounds="+wall.GetComponent<Collider>().bounds);
       Assert.That(p.Position.z,Is.LessThan(3.001f));
       if(scenario==2)Assert.That(p.Position.x,Is.LessThan(1.001f));
       if(p.Contacts>0)spread=math.max(spread,math.length((p.Position-(float3)origin).xy));
      }
      if(solver.InvalidContacts>0)
      {
       report.AppendLine("INVALID scenario="+scenario+" frame="+frame+" center="+solver.LastInvalidCenter+" radius="+solver.LastInvalidRadius+" hitPoint="+solver.LastInvalidContact.Point+" hitNormal="+solver.LastInvalidContact.Normal+" fraction="+solver.LastInvalidContact.Fraction);
       Assert.That(solver.InvalidContacts,Is.Zero,"Invalid PhysX contact must be diagnosed, not counted as acceptance through rejection.");
      }
      if(frame%10==0)yield return null;
     }
     Assert.That(solver.Collisions,Is.GreaterThan(15));Assert.That(spread,Is.GreaterThan(.65f));
     Assert.That(world.Saturations,Is.Zero);Assert.That(queries,Is.LessThanOrEqualTo(FireFlowParticleSolver.MaximumQueriesPerStep));
     report.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6}",scenario,solver.Collisions,peak,queries,spread,world.Saturations,maximum));
    }
   }
   finally {Object.Destroy(wall);Object.Destroy(corner);Directory.CreateDirectory("BuildReports/HardPolish/G05/FluidDemonstration");File.WriteAllText("BuildReports/HardPolish/G05/FluidDemonstration/PhysX.csv",report.ToString());}
  }
 }
}
