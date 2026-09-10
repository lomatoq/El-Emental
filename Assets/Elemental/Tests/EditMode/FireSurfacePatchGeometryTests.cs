using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
    public sealed class FireSurfacePatchGeometryTests
    {
        [Test]public void LargeTriangleProjectsToFaceRatherThanVerticesOrCentroid()
        {
            Assert.That(FireSurfacePatchGeometry.ClosestPoint(new float3(.2f,.3f,2),new float3(-10,-10,0),new float3(10,-10,0),new float3(0,10,0),out var p),Is.True);
            Assert.That(math.distance(p,new float3(.2f,.3f,0)),Is.LessThan(.0001f));
        }
        [Test]public void OutsidePointClampsToRealEdgeWithoutCrossingHole()
        {
            FireSurfacePatchGeometry.ClosestPoint(new float3(.8f,.8f,.1f),float3.zero,new float3(1,0,0),new float3(0,1,0),out var p);
            Assert.That(math.distance(p,new float3(.5f,.5f,0)),Is.LessThan(.0001f));
        }
        [Test]public void DegenerateAndNonFiniteTrianglesAreRejected()
        {
            Assert.That(FireSurfacePatchGeometry.ClosestPoint(float3.zero,float3.zero,float3.zero,float3.zero,out _),Is.False);
            Assert.That(FireSurfacePatchGeometry.ClosestPoint(new float3(float.NaN),float3.zero,new float3(1,0,0),new float3(0,1,0),out _),Is.False);
        }
        [Test]public void PatchCoversAreaWithStableDistinctSeeds()
        {
            float max=0;
            for(int i=0;i<FireSurfacePatchGeometry.Capacity;i++)
            {var p=FireSurfacePatchGeometry.DiskOffset(i,.9f);max=math.max(max,math.length(p));Assert.That(math.length(p),Is.LessThanOrEqualTo(.90001f));
             for(int j=0;j<i;j++)Assert.That(math.distance(p,FireSurfacePatchGeometry.DiskOffset(j,.9f)),Is.GreaterThan(.2f));}
            Assert.That(max,Is.GreaterThan(.85f));
        }
    }
}
