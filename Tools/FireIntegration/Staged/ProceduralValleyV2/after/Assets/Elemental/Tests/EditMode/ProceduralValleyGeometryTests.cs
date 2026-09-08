using System;
using Elemental.Presentation.DistantScenery;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Elemental.Tests.EditMode
{
    public sealed class ProceduralValleyGeometryTests
    {
        [Test] public void ClipCreatesClosedCapAndRejectsDestructiveCut()
        {
            var box=RockPolyhedron.Box(new float3(-1),new float3(1));
            Assert.That(box.Clip(new float3(1,0,0),0,out var half),Is.True);
            Assert.That(half.Validate(out double volume),Is.True);Assert.That(volume,Is.EqualTo(4).Within(.0001));
            Assert.That(half.Clip(new float3(1,0,0),-4,out var unchanged),Is.False);Assert.That(unchanged,Is.SameAs(half));
        }
        [Test] public void FortyEightSeedsBothLodsHaveClosedConvexPartsPositiveVolumeAndBoundedBudgets()
        {
            for(int seed=0;seed<48;seed++)foreach(bool low in new[]{false,true})
            {
                var shape=RockShapeBuilder.Pillar(seed*7381,low,RockShapeSettings.Default);
                Assert.That(shape.Validate(out double volume),Is.True,"seed "+seed+" low "+low);Assert.That(volume,Is.GreaterThan(0));
                Assert.That(shape.Parts.Count,Is.InRange(2,4));Assert.That(shape.TriangleCount,Is.InRange(40,low?160:500));
            }
        }
        [Test] public void LodRetainsEveryCompoundTransformAndCoarseSilhouette()
        {
            for(int family=0;family<6;family++)
            {
                int seed=13771+family*131;
                var high=RockShapeBuilder.Group(seed,2+family%4,true,family,false,RockShapeSettings.Default);
                var low=RockShapeBuilder.Group(seed,2+family%4,true,family,true,RockShapeSettings.Default);
                Assert.That(high.Validate(out _),Is.True);Assert.That(low.Validate(out _),Is.True);
                Assert.That(high.Parts.Count,Is.EqualTo(low.Parts.Count));Assert.That(high.TriangleCount,Is.GreaterThan(low.TriangleCount));
                for(int i=0;i<high.Parts.Count;i++)
                {
                    Assert.That(high.Parts[i].Translation,Is.EqualTo(low.Parts[i].Translation));
                    Assert.That(high.Parts[i].Scale,Is.EqualTo(low.Parts[i].Scale));
                    Assert.That(high.Parts[i].Rotation,Is.EqualTo(low.Parts[i].Rotation));
                }
                high.Bounds(out float3 hmin,out float3 hmax);low.Bounds(out float3 lmin,out float3 lmax);
                Assert.That(math.all(hmin>=lmin-.0001f)&&math.all(hmax<=lmax+.0001f),Is.True);
                Assert.That(math.cmax(math.abs((hmax-hmin)-(lmax-lmin))),Is.LessThan(.08f));
            }
        }
        [Test] public void PublishedMeshHasFlatPlaneNormalsAndDeterministicArrays()
        {
            Mesh first=ProceduralRockMesh.Pillar(13771,false,RockShapeSettings.Default),second=null;
            try
            {
                second=ProceduralRockMesh.Pillar(13771,false,RockShapeSettings.Default);
                CollectionAssert.AreEqual(first.vertices,second.vertices);CollectionAssert.AreEqual(first.normals,second.normals);CollectionAssert.AreEqual(first.triangles,second.triangles);
                Vector3[] vertices=first.vertices,normals=first.normals;int[] indices=first.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    Vector3 cross=Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]);
                    Assert.That(Vector3.Dot(cross.normalized,normals[indices[i]]),Is.GreaterThan(.999f));
                    Assert.That(normals[indices[i]],Is.EqualTo(normals[indices[i+1]]));Assert.That(normals[indices[i]],Is.EqualTo(normals[indices[i+2]]));
                }
            }
            finally{Object.DestroyImmediate(first);if(second!=null)Object.DestroyImmediate(second);}
        }
        [Test] public void PreviewSeedsAreDistinctAndUnityGlobalRandomIsUntouched()
        {
            var state=UnityEngine.Random.state;var signatures=new System.Collections.Generic.HashSet<string>();
            for(int i=0;i<12;i++)
            {
                var shape=RockShapeBuilder.Pillar(13771+i*1013,false,RockShapeSettings.Default);
                shape.Bounds(out float3 min,out float3 max);
                signatures.Add(shape.TriangleCount+":"+min+":"+max);
            }
            Assert.That(signatures.Count,Is.EqualTo(12));Assert.That(UnityEngine.Random.state,Is.EqualTo(state));
        }
    }
}
