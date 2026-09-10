using Elemental.Runtime.Geometry;
using NUnit.Framework;
using UnityEngine;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthContainedRenderRepairTests
    {
        [Test]
        public void TinyValidFacesRetainUnitNormalsAndOnlyZeroAreaFacesAreRemoved()
        {
            var source=new Mesh();Mesh repaired=null;
            try
            {
                source.vertices=new[]{Vector3.zero,new Vector3(.000001f,0,0),new Vector3(0,.000001f,0),Vector3.zero,Vector3.zero,Vector3.one};
                source.triangles=new[]{0,1,2,3,4,5};
                repaired=EarthContainedRenderRepair.FlatCopy(source,out int removed);
                Assert.That(removed,Is.EqualTo(1));Assert.That(repaired.vertexCount,Is.EqualTo(3));
                foreach(var normal in repaired.normals)Assert.That(Vector3.Dot(normal,Vector3.forward),Is.GreaterThan(.999f));
            }
            finally{Object.DestroyImmediate(source);if(repaired!=null)Object.DestroyImmediate(repaired);}
        }
        [Test]
        public void ReconstructionRetainsClosedHullAndNeverChangesColliderGeometry()
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);Mesh render=null;
            try
            {
                Mesh collider=cube.GetComponent<MeshFilter>().sharedMesh;var points=collider.vertices;var triangles=collider.triangles;
                render=EarthContainedRenderRepair.Create(collider,out _,out _);
                Assert.That(EarthContainedRenderRepair.IsClosed(render),Is.True);
                CollectionAssert.AreEqual(points,collider.vertices);CollectionAssert.AreEqual(triangles,collider.triangles);
                foreach(var point in render.vertices){Assert.That(Mathf.Abs(point.x),Is.LessThanOrEqualTo(.500001f));Assert.That(Mathf.Abs(point.y),Is.LessThanOrEqualTo(.500001f));Assert.That(Mathf.Abs(point.z),Is.LessThanOrEqualTo(.500001f));}
                foreach(var normal in render.normals)Assert.That(normal.sqrMagnitude,Is.InRange(.999f,1.001f));
            }
            finally{Object.DestroyImmediate(cube);if(render!=null)Object.DestroyImmediate(render);}
        }
        [Test]
        public void ClosureAuditRejectsMissingFace()
        {
            var mesh=new Mesh();
            try{mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.forward};mesh.triangles=new[]{0,2,1,0,1,3,0,3,2};mesh.RecalculateBounds();Assert.That(EarthContainedRenderRepair.IsClosed(mesh),Is.False);}
            finally{Object.DestroyImmediate(mesh);}
        }
    }
}
