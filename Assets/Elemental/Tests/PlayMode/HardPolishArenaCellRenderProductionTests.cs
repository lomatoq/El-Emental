using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Runtime.World;
using System.Text;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator ActualArenaCellsUseClosedPreparedRenders()
        {
            yield return null;
            var pieces=_scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<EarthArenaPiece>(true)).ToArray();
            Assert.That(pieces.Length,Is.GreaterThan(0));var report=new StringBuilder();int checkedCount=0,authoredCount=0;
            try
            {
                foreach(var piece in pieces)
                {
                    Mesh render=piece.GetComponent<MeshFilter>().sharedMesh;
                    Mesh collider=piece.GetComponent<MeshCollider>().sharedMesh;
                    Assert.That(render,Is.Not.Null,piece.name);Assert.That(collider,Is.Not.Null,piece.name);
                    Assert.That(render,Is.Not.SameAs(collider),"Render repair must not replace/mutate the physical collider "+piece.name);
                    Assert.That(EarthContainedRenderRepair.IsWatertight(render),Is.True,piece.name+" / "+render.name);
                    var normals=render.normals;var vertices=render.vertices;var triangles=render.triangles;
                    Assert.That(normals.Length,Is.EqualTo(vertices.Length));
                    foreach(var normal in normals)Assert.That(normal.sqrMagnitude,Is.InRange(.999f,1.001f),piece.name);
                    if(piece.Owner.PreservesAuthoredFractureRendering)
                    {
                        Assert.That(render,Is.SameAs(AuthoredAsset(piece.Owner).GetPieceRenderMesh(piece.PieceIndex)),piece.name);
                        authoredCount++;
                    }
                    else for(int t=0;t<triangles.Length;t+=3)
                    {
                        Assert.That(EarthContainedRenderRepair.TryGeometricNormal(vertices[triangles[t]],vertices[triangles[t+1]],vertices[triangles[t+2]],out Vector3 face),Is.True,piece.name);
                        for(int c=0;c<3;c++)Assert.That(Vector3.Dot(face,normals[triangles[t+c]]),Is.GreaterThan(.999f),piece.name);
                    }
                    if(!piece.Owner.PreservesAuthoredFractureRendering)
                    {
                        Mesh source=AuthoredAsset(piece.Owner).GetPieceRenderMesh(piece.PieceIndex);
                        Assert.That(render.triangles.Length,Is.EqualTo(source.triangles.Length),piece.name+" lost authored surface triangles");
                        var original=new System.Collections.Generic.HashSet<Vector3>(source.vertices);
                        foreach(var point in vertices)Assert.That(original.Contains(point),Is.True,piece.name+" changed authored detail coordinates");
                        Assert.That(piece.Owner.FractureRenderHullFallbackCount,Is.Zero);
                    }
                    checkedCount++;report.AppendLine(piece.name+" authored="+piece.Owner.PreservesAuthoredFractureRendering+" render="+render.name+" triangles="+triangles.Length/3);
                }
                Assert.That(authoredCount,Is.EqualTo(85));
                Assert.That(checkedCount-authoredCount,Is.EqualTo(90));
            }
            finally
            {
                Directory.CreateDirectory("BuildReports/HardPolish/G01");
                File.WriteAllText("BuildReports/HardPolish/G01/production-arena-cell-renders.txt","checked="+checkedCount+" total="+pieces.Length+"\n"+report);
            }
        }
        static IEarthFractureAssetRuntimeData AuthoredAsset(EarthArenaStructure owner) =>
            (IEarthFractureAssetRuntimeData)typeof(EarthArenaStructure).GetField("fractureAssetObject",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);

        [UnityTest]
        public IEnumerator ActualOuterArchesPreserveEveryAuthoredCellAcrossDamageAndRestore()
        {
            yield return null;
            var owners=_scene.GetRootGameObjects().Where(root=>root.name=="Outer Stone Ring")
                .SelectMany(root=>root.GetComponentsInChildren<EarthArenaStructure>(true)).ToArray();
            Assert.That(owners.Length,Is.EqualTo(7));var report=new StringBuilder();int verified=0;
            try
            {
                foreach(var owner in owners)
                {
                    Assert.That(owner.PreservesAuthoredFractureRendering,Is.True,owner.name);
                    owner.RestoreArenaStructure();
                    var intact=owner.GetComponent<MeshFilter>();Mesh originalIntact=intact.sharedMesh;
                    var pieces=_scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<EarthArenaPiece>(true)).Where(piece=>piece.Owner==owner).ToArray();
                    Assert.That(pieces.Length,Is.EqualTo(owner.PieceCount));
                    var asset=AuthoredAsset(owner);int smoothCorners=0;
                    var originalNormals=pieces.ToDictionary(piece=>piece.PieceIndex,piece=>asset.GetPieceRenderMesh(piece.PieceIndex).normals);
                    foreach(var piece in pieces)
                    {
                        var mesh=asset.GetPieceRenderMesh(piece.PieceIndex);var v=mesh.vertices;var n=mesh.normals;var tri=mesh.triangles;
                        for(int i=0;i<tri.Length;i+=3)
                            if(EarthContainedRenderRepair.TryGeometricNormal(v[tri[i]],v[tri[i+1]],v[tri[i+2]],out Vector3 face))
                                for(int k=0;k<3;k++)if(Vector3.Dot(face,n[tri[i+k]])<.999f)smoothCorners++;
                    }
                    Assert.That(smoothCorners,Is.GreaterThan(0),"Fixture must contain authored curved normals: "+owner.name);
                    Assert.That(owner.TryPluckCell(pieces[pieces.Length-1].GetComponent<MeshFilter>().transform.TransformPoint(
                        asset.GetPieceRenderMesh(pieces[pieces.Length-1].PieceIndex).bounds.center),out var detached),Is.True,owner.name);
                    Assert.That(detached,Is.Not.Null);Assert.That(owner.GetComponent<Renderer>().enabled,Is.False,"First release must expose full fractured arch.");
                    yield return null;
                    int attached=0;
                    foreach(var piece in pieces)
                    {
                        Mesh expected=asset.GetPieceRenderMesh(piece.PieceIndex);var filter=piece.GetComponent<MeshFilter>();
                        Assert.That(piece.gameObject.activeInHierarchy,Is.True,piece.name);
                        Assert.That(filter.sharedMesh,Is.SameAs(expected),"Every attached and released cell must preserve authored topology and normals: "+piece.name);
                        CollectionAssert.AreEqual(originalNormals[piece.PieceIndex],filter.sharedMesh.normals);
                        if(!owner.IsPieceReleased(piece.PieceIndex))attached++;
                    }
                    Assert.That(attached,Is.GreaterThan(0),"Test must retain attached arch surfaces after first damage.");
                    owner.RestoreArenaStructure();yield return null;
                    Assert.That(owner.GetComponent<Renderer>().enabled,Is.True);Assert.That(intact.sharedMesh,Is.SameAs(originalIntact));
                    Assert.That(owner.ReleasedPieceCount,Is.Zero);
                    foreach(var piece in pieces)
                    {
                        Assert.That(piece.gameObject.activeInHierarchy,Is.False);
                        Assert.That(piece.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(asset.GetPieceRenderMesh(piece.PieceIndex)));
                    }
                    Assert.That(owner.FractureRenderHullFallbackCount,Is.Zero);
                    verified++;report.AppendLine(owner.name+" pieces="+pieces.Length+" retainedAttached="+attached+" smoothCorners="+smoothCorners+" restored=true");
                }
            }
            finally
            {
                foreach(var owner in owners)if(owner!=null)owner.RestoreArenaStructure();
                Directory.CreateDirectory("BuildReports/HardPolish/G01");
                File.WriteAllText("BuildReports/HardPolish/G01/outer-arch-authored-damage-restore.txt","verified="+verified+"/7\n"+report);
            }
        }
    }
}
