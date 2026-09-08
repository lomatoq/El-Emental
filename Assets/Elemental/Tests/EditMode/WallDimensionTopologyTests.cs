using System;
using Elemental.Runtime.Geometry;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class WallDimensionTopologyTests
    {
        [TestCase(1f,4f)]
        [TestCase(3f,7f)]
        [TestCase(8f,4f)]
        public void PhysicalDimensionsProduceClosedConservativeDomains(float width,float height)
        {
            var d = new float3(width,height,.55f);
            var plan = EarthWallDimensionFracture.BuildPlan(d,0xE17F1002u);
            Assert.That(plan.IsValid,Is.True);
            Assert.That(EarthVolumetricFractureSolver.HasClosedTopology(in plan),Is.True);
            var aspects = new float[plan.Cells.Length];
            for(int i=0;i<plan.Cells.Length;i++)
            {
                float3 minimum = new(float.PositiveInfinity),maximum = new(float.NegativeInfinity);
                foreach(var vertex in plan.Cells[i].Vertices)
                { minimum=math.min(minimum,vertex); maximum=math.max(maximum,vertex); }
                float3 size=maximum-minimum;
                aspects[i]=math.max(size.x,size.y)/math.max(.001f,math.min(size.x,size.y));
                Assert.That(math.cmax(math.abs(minimum)/d),Is.LessThanOrEqualTo(.501f));
                Assert.That(math.cmax(math.abs(maximum)/d),Is.LessThanOrEqualTo(.501f));
            }
            Array.Sort(aspects);
            Assert.That(aspects[aspects.Length/2],Is.LessThan(4f),"Physical face domains must not become stretched thin template strips.");
        }

        [Test] public void NarrowWallUsesFewerDomainsAndFreshShellHasNoInteriorFaces()
        {
            using var narrow = new EarthWallDimensionFracture(new Vector3(1,4,.55f),0xE17F1002u);
            using var broad = new EarthWallDimensionFracture(new Vector3(8,4,.55f),0xE17F1002u);
            Assert.That(narrow.PieceCount,Is.LessThan(broad.PieceCount));
            foreach(var data in new[]{narrow,broad})
            {
                var pieces=new EarthPieceDefinition[data.PieceCount];var bonds=new EarthBondDefinition[data.BondCount];
                Assert.That(data.CopyDefinitions(pieces,bonds),Is.True);
                Assert.That(EarthBondGraph.Validate(pieces,pieces.Length,bonds,bonds.Length).IsValid,Is.True);
                float volume=0;foreach(var piece in pieces)volume+=piece.Volume;
                Assert.That(volume,Is.EqualTo(1f).Within(.02f));
                // Shell points lie on actual clipped source planes; interior
                // domain boundaries cannot introduce inward/recessed seams.
                foreach(Vector3 p in data.IntactRenderMesh.vertices)
                {
                    float distance=Mathf.Min(Mathf.Abs(Mathf.Abs(p.x)-.5f),Mathf.Abs(Mathf.Abs(p.y)-.5f),Mathf.Abs(Mathf.Abs(p.z)-.5f));
                    distance=Mathf.Min(distance,Mathf.Abs(.10f*p.x+p.y-.5f),Mathf.Abs(-.08f*p.x+p.y-.49f));
                    distance=Mathf.Min(distance,Mathf.Abs(p.y+Mathf.Abs(p.z)-.95f),Mathf.Abs(Mathf.Abs(p.x)+Mathf.Abs(p.z)-.95f));
                    Assert.That(distance,Is.LessThan(.002f));
                }
            }
        }
    }
}
