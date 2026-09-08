using System.Collections;
using Elemental.Runtime.Fire;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class FireEarthFractureContactTests
    {
        [UnityTest] public IEnumerator ActualWallFractureInvalidatesIntactPatchAndExposesPieceIdentity()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject piece = new GameObject("Fire tested physical wall piece");
            try
            {
                root.name = "Fire canonical wall lifecycle test";
                EarthWall wall = root.AddComponent<EarthWall>();
                piece.transform.SetParent(root.transform, false);
                piece.transform.localScale = Vector3.one * 0.5f;
                Mesh sourceMesh = root.GetComponent<MeshFilter>().sharedMesh;
                piece.AddComponent<MeshFilter>().sharedMesh = sourceMesh;
                piece.AddComponent<MeshRenderer>();
                var collider = piece.AddComponent<MeshCollider>(); collider.sharedMesh = sourceMesh; collider.convex = true;
                var body = piece.AddComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true;
                wall.ConfigureCollapsePieces(new[] { piece.transform }, new[] { 1f }, null);
                Vector3 start = new Vector3(1000, 2, 0), end = new Vector3(1004, 2, 0), center = new Vector3(1002, -24, 0);
                wall.Initialize(95001, start, end, center, 2, 1, supportNormal: Vector3.up);
                for (int i = 0; i < 180 && !wall.IsSurfaceAvailable; i++) yield return new WaitForFixedUpdate();
                Assert.That(wall.IsSurfaceAvailable, Is.True, "The actual emergence lifecycle must expose its intact collider.");
                var resolver = new FireSurfaceResolver();
                var intact = (BoxCollider)wall.SurfaceCollider;
                Vector3 intactPoint = intact.transform.TransformPoint(intact.center + Vector3.forward * intact.size.z * 0.5f);
                Assert.That(resolver.TryResolve(intact, intactPoint, 0.1f, out var oldAnchor, out _));
                Assert.That(wall.ApplyRockImpact(intactPoint, Vector3.forward, 100000), Is.True);
                Assert.That(wall.IsCollapsing, Is.True);
                Assert.That(resolver.TryRefresh(oldAnchor, out _), Is.False);
                Assert.That(piece.activeInHierarchy && collider.enabled, Is.True);
                Vector3 piecePoint = piece.transform.TransformPoint(Vector3.forward * 0.5f);
                Assert.That(resolver.TryResolve(collider, piecePoint, 0.1f, out var released, out _));
                Assert.That(released.Handle.Namespace, Is.EqualTo(5));
                Assert.That(released.Handle.Id, Is.EqualTo(wall.WallId));
                Assert.That(released.Handle.Generation, Is.EqualTo(wall.Generation));
                Assert.That(released.Handle.Piece, Is.EqualTo(1));
                wall.Initialize(95001, start, end, center, 2, 1, supportNormal: Vector3.up);
                Assert.That(resolver.TryRefresh(released, out _), Is.False);
                yield return null;
            }
            finally
            {
                Object.Destroy(piece);
                Object.Destroy(root);
            }
        }
    }
}
