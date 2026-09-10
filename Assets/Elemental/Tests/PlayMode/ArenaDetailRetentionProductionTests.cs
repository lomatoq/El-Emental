using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Runtime.Physics;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest,Timeout(240000)] public IEnumerator ActualArenaKeepsDetailedAttachedCellsAfterFirstDamage()
        {
            yield return null;var roots=_scene.GetRootGameObjects();
            var owners=roots.SelectMany(r=>r.GetComponentsInChildren<EarthArenaStructure>(true)).Where(o=>!o.PreservesAuthoredFractureRendering).ToArray();
            var camera=roots.SelectMany(r=>r.GetComponentsInChildren<CelestialSystemBehaviour>(true)).Single().TargetCamera;
            var capture=camera.gameObject.AddComponent<FireVisualCaptureCamera>();string folder="BuildReports/HardPolish/G01/ArenaDetail-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
            try
            {
                foreach(var owner in owners)
                {
                    owner.RestoreArenaStructure();
                    if(!owner.OrdinaryDamageEnabled){Assert.That(owner.TryPluckCell(owner.transform.position,out _),Is.False,"Protected arena base must retain its authored immunity");continue;}
                    var pieces=roots.SelectMany(r=>r.GetComponentsInChildren<EarthArenaPiece>(true)).Where(p=>p.Owner==owner).ToArray();
                    var original=pieces.ToDictionary(p=>p.PieceIndex,p=>p.GetComponent<MeshFilter>().sharedMesh);
                    var bounds=owner.GetComponent<Renderer>().bounds;bool show=owner.name.Contains("Floor")||owner.name.Contains("Gate");
                    if(show){capture.Place(bounds.center+(Vector3.up*.7f+Vector3.forward)*Mathf.Max(5,bounds.extents.magnitude*1.5f),bounds.center);yield return new WaitForEndOfFrame();ProductionCaptureResolution.SaveScreen(folder+"/"+owner.name+"-intact.png");}
                    Assert.That(owner.TryPluckCell(pieces[0].transform.TransformPoint(AuthoredAsset(owner).GetPieceRenderMesh(pieces[0].PieceIndex).bounds.center),out var detached),Is.True,owner.name);
                    yield return null;int attached=0;
                    foreach(var piece in pieces)
                    {Assert.That(piece.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(original[piece.PieceIndex]));if(!owner.IsPieceReleased(piece.PieceIndex))attached++;}
                    Assert.That(attached,Is.GreaterThan(0));Assert.That(owner.FractureRenderHullFallbackCount,Is.Zero);
                    if(show){yield return new WaitForEndOfFrame();ProductionCaptureResolution.SaveScreen(folder+"/"+owner.name+"-partially-damaged.png");}
                    owner.RestoreArenaStructure();
                }
            }
            finally{foreach(var owner in owners)if(owner!=null)owner.RestoreArenaStructure();Object.Destroy(capture);}
        }
    }
}
