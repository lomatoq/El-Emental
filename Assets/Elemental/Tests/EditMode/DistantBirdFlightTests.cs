using System;
using Elemental.Presentation.DistantScenery;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Elemental.Tests.EditMode
{
    public sealed class DistantBirdFlightTests
    {
        [Test] public void FlightIsDeterministicContinuousAndBoundedOnBothSidesOfPlanet()
        {
            int front=0,rear=0;
            for(int index=0;index<48;index++)
            {
                var bird=DistantBirdFlight.Create(38217,index);if(bird.Center.z>0)front++;else rear++;
                for(int sample=0;sample<120;sample++)
                {
                    double time=sample*bird.Period/20;
                    var pose=DistantBirdFlight.Evaluate(bird,time,false);var duplicate=DistantBirdFlight.Evaluate(bird,time,false);
                    Assert.That(pose.Position,Is.EqualTo(duplicate.Position));Assert.That(math.all(math.isfinite(pose.Position)),Is.True);
                    Assert.That(math.distance(pose.Position,bird.Center),Is.LessThan(160));
                    Assert.That(math.length(pose.Forward),Is.EqualTo(1).Within(.0001));
                    var nearby=DistantBirdFlight.Evaluate(bird,time+.001,false);
                    Assert.That(math.distance(pose.Position,nearby.Position),Is.LessThan(.02f),"No orbital wrap jump.");
                }
                var frozen=DistantBirdFlight.Evaluate(bird,0,true);
                Assert.That(DistantBirdFlight.Evaluate(bird,12345,true).Position,Is.EqualTo(frozen.Position));
                Assert.That(frozen.WingAngle,Is.EqualTo(.12f));
            }
            Assert.That(front,Is.GreaterThan(0));Assert.That(rear,Is.GreaterThan(0));
        }
        [Test] public void PureFlightEvaluationAllocatesNoManagedMemory()
        {
            var bird=DistantBirdFlight.Create(13,1);float sum=0;
            for(int i=0;i<100;i++)sum+=DistantBirdFlight.Evaluate(bird,i,false).Position.x;
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<10000;i++)sum+=DistantBirdFlight.Evaluate(bird,i*.01,false).Position.x;
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.That(allocated,Is.Zero);Assert.That(float.IsNaN(sum),Is.False);
        }
        [Test] public void CosmeticFlockUsesOneMeshNoPhysicsAndRebuildDoesNotLeakChildren()
        {
            var root=new GameObject("Bird fixture");var planet=new GameObject("Planet");
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                var owner=root.AddComponent<DistantBirdFlock>();owner.Configure(planet.transform,Vector3.up,Vector3.forward,null,material);
                Assert.That(owner.BirdCount,Is.EqualTo(40));Assert.That(owner.GeneratedMesh.vertexCount,Is.EqualTo(320));
                Assert.That(owner.GeneratedMesh.triangles.Length/3,Is.EqualTo(240));
                owner.ApplyTime(21,true);Vector3[] baseline=owner.GeneratedMesh.vertices;owner.ApplyTime(1234,true);
                CollectionAssert.AreEqual(baseline,owner.GeneratedMesh.vertices);
                owner.Rebuild();Assert.That(root.transform.childCount,Is.Zero);
                owner.ConfigureViewCamera(null);
                for(int frame=0;frame<480;frame++)
                {
                    owner.ApplyTime(frame*.5,false);
                    foreach(Vector3 vertex in owner.GeneratedMesh.vertices)
                        Assert.That(owner.GeneratedMesh.bounds.Contains(vertex),Is.True,"Analytic flock bounds must contain every evaluated wingtip.");
                }
                Assert.That(root.GetComponentsInChildren<Collider>(),Is.Empty);Assert.That(root.GetComponentsInChildren<Rigidbody>(),Is.Empty);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(1));
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(planet);Object.DestroyImmediate(material);}
        }
    }
}
