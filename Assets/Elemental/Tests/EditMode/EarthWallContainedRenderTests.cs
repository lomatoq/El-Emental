using Elemental.Runtime.Geometry;
using NUnit.Framework;
using UnityEngine;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthWallContainedRenderTests
    {
        [Test]
        public void OffsetNonuniformWallCellKeepsClosedGeometryAndUnchangedCollider()
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);Mesh source=Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);Mesh render=null;
            try
            {
                var vertices=source.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]+=new Vector3(2,.3f,-1);source.vertices=vertices;source.RecalculateBounds();
                var expected=source.vertices;var indices=source.triangles;
                render=EarthWallFractureVisual.Create(source,source,null,17u,new Vector3(8,3,.55f));
                Assert.That(EarthContainedRenderRepair.IsClosed(render),Is.True);
                CollectionAssert.AreEqual(expected,source.vertices);CollectionAssert.AreEqual(indices,source.triangles);
                foreach(var normal in render.normals)Assert.That(normal.sqrMagnitude,Is.InRange(.999f,1.001f));
                Bounds allowed=source.bounds;allowed.Expand(.00002f);
                foreach(var point in render.vertices)Assert.That(allowed.Contains(point),Is.True,point.ToString());
            }
            finally{Object.DestroyImmediate(cube);Object.DestroyImmediate(source);if(render!=null)Object.DestroyImmediate(render);}
        }
    }
}
