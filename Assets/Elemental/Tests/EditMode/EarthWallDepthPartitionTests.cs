using System;
using System.Reflection;
using Elemental.Authoring.Editor;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthWallDepthPartitionTests
    {
        [Test] public void ProductionPlanConservesClippedSourceWithDifferentRealDepthSpans()
        {
            float2[] boundary = { new float2(-4,-.275f),new float2(4,-.275f),new float2(4,.275f),new float2(-4,.275f) };
            var plan = (EarthVolumetricFracturePlan)typeof(EarthFractureBaker).GetMethod("BuildProductionPlan",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[] { boundary });
            Assert.That(plan.IsValid,Is.True); Assert.That(plan.Cells.Length,Is.EqualTo(40));
            Assert.That(EarthVolumetricFractureSolver.HasClosedTopology(in plan),Is.True);
            Assert.That(plan.RelativeVolumeError,Is.LessThan(.005f));
            int partial=0, obliqueDepthFaces=0; float minimum=1f, maximum=0f;
            foreach (var cell in plan.Cells)
            {
                float near=float.PositiveInfinity,far=float.NegativeInfinity;
                foreach (float3 p in cell.Vertices) { near=math.min(near,p.z);far=math.max(far,p.z); }
                float span=(far-near)/.55f; if(span<.85f)partial++;
                minimum=math.min(minimum,span);maximum=math.max(maximum,span);
                Assert.That(cell.Volume,Is.GreaterThan(.0001f));
                Assert.That(cell.TriangleCount,Is.LessThanOrEqualTo(255));
                Assert.That(cell.AspectRatio,Is.LessThanOrEqualTo(8f));
                foreach (var face in cell.Faces)
                    if (!face.IsExterior && math.abs(face.Normal.z)>.7f && math.length(face.Normal.xy)>.01f) obliqueDepthFaces++;
            }
            Assert.That(partial,Is.GreaterThanOrEqualTo(8),"At least one fifth of real cells must not span the entire wall depth.");
            Assert.That(maximum-minimum,Is.GreaterThanOrEqualTo(.20f));
            Assert.That(obliqueDepthFaces,Is.GreaterThanOrEqualTo(8),"Depth divisions must be real oblique shared 3D faces, not duplicate slabs.");
        }
        [Test] public void OptInLeavesOrdinaryFracturePlansExactlyUnchanged()
        {
            float2[] boundary = {new float2(-2,-1),new float2(2,-1),new float2(2,1),new float2(-2,1)};
            var implicitDefault=EarthVolumetricFractureSolver.BuildConvexPrism(77u,boundary,-1,1,12);
            var explicitDefault=EarthVolumetricFractureSolver.BuildConvexPrism(77u,boundary,-1,1,12,null,false);
            Assert.That(implicitDefault.Cells.Length,Is.EqualTo(explicitDefault.Cells.Length));
            for(int i=0;i<implicitDefault.Cells.Length;i++)
            { CollectionAssert.AreEqual(implicitDefault.Cells[i].Vertices,explicitDefault.Cells[i].Vertices);CollectionAssert.AreEqual(implicitDefault.Cells[i].Triangles,explicitDefault.Cells[i].Triangles); }
        }
    }
}
