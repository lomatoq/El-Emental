using Elemental.Presentation.VFX;
using NUnit.Framework;
using UnityEngine;
namespace Elemental.Tests.EditMode
{
    public sealed class SurfaceDustMeshTests
    {
        [Test] public void CarrierHasCurvatureAndCannotSampleAdjacentAtlasCells()
        {
            var mesh=SurfaceDustMesh.Create();
            try
            {
                Assert.That(mesh.vertexCount,Is.EqualTo(81));
                Assert.That(mesh.bounds.size.z,Is.GreaterThan(.025f));
                foreach(var uv in mesh.uv){Assert.That(uv.x,Is.InRange(.002f,.998f));Assert.That(uv.y,Is.InRange(.002f,.998f));}
                var v=mesh.vertices;Assert.That(v[40].z,Is.GreaterThan(v[0].z+.01f));
                foreach(var n in mesh.normals)Assert.That(n.z,Is.GreaterThan(0));
            }
            finally{Object.DestroyImmediate(mesh);}
        }
    }
}
