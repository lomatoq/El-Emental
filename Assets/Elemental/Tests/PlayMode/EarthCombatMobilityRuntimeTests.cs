using System.Collections;
using System.Linq;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.Matter;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthCombatMobilityRuntimeTests
    {
        [UnityTest]
        public IEnumerator ArmorCollisionsAccumulateAndReleasedColumnSplitsWithoutLosingMass()
        {
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(path);
            GameObject projectile = null;
            GameObject obstacle = null;
            AsyncOperation unload = null;
            try
            {
                foreach (var bot in All<EarthMvpBotController>(scene)) bot.enabled = false;
                yield return null;
                var column = All<EarthArenaStructure>(scene).First(x => x.name.Contains("Column") && x.OrdinaryDamageEnabled);
                var pool = All<EarthRockDebrisPool>(scene).First();
                var originalMeshes = All<EarthArenaPiece>(scene).Where(x => x.Owner == column)
                    .ToDictionary(x => x, x => x.GetComponent<MeshFilter>().sharedMesh);
                var originalColliders = originalMeshes.Keys.ToDictionary(x => x, x => x.GetComponent<MeshCollider>().sharedMesh);
                var intact = column.GetComponent<Collider>();
                Vector3 direction = column.transform.right.normalized;
                Vector3 contact = intact.ClosestPoint(intact.bounds.center + direction * 20f);
                var ray = new Ray(intact.bounds.center + direction * 20f, -direction);
                Assert.That(intact.Raycast(ray, out var surfaceHit, 40f), Is.True);
                contact = surfaceHit.point;
                projectile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var body = projectile.AddComponent<Rigidbody>();
                body.useGravity = false;
                var plate = projectile.AddComponent<EarthArmorPiece>();
                var probe = projectile.AddComponent<EarthCombatCollisionProbe>();
                var shape = projectile.GetComponent<Collider>();
                // The column overlaps the arena wall: isolate the projectile's target
                // while retaining real PhysX contact and the production damage route.
                foreach (var other in All<Collider>(scene))
                    if (other != intact) Physics.IgnoreCollision(shape, other, true);
                plate.Configure(null, 123, body, shape, projectile.GetComponent<MeshFilter>().sharedMesh);
                for (uint shot = 1; shot <= 12 && column.ReleasedPieceCount == 0; shot++)
                {
                    plate.Activate(shot, contact + direction * .65f, Quaternion.identity);
                    body.position = contact + direction * .65f;
                    Physics.SyncTransforms();
                    plate.Release(-direction * 10f, 3f, 1f);
                    shape.enabled = true;
                    body.detectCollisions = true;
                    Physics.SyncTransforms();
                    for (int tick = 0; tick < 15 && projectile.activeSelf; tick++)
                        yield return new WaitForFixedUpdate();
                    Assert.That(column.AccumulatedImpactImpulse > 0 || column.ReleasedPieceCount > 0,
                        Is.True, $"Armor impact: target={column.name} contact={contact} end={body.position} active={projectile.activeSelf} hit={probe.Hit} speed={body.linearVelocity}");
                    if (shot == 1) Assert.That(column.ReleasedPieceCount, Is.Zero,
                        "A single weak plate should not destroy this column.");
                }
                Assert.That(column.ReleasedPieceCount, Is.GreaterThan(0), "Weak armor hits must accumulate.");
                var cells = All<EarthArenaPiece>(scene).Where(x => x.Owner == column).ToArray();
                var attached = cells.First(x => !x.IsEarthTargetValid && x.gameObject.activeInHierarchy);
                float previous = column.AccumulatedImpactImpulse;
                var weak = new EarthStructureImpact(attached.transform.position, -direction, 5f,
                    EarthStructureImpactKind.Projectile, 778899u);
                EarthStructureImpactRouter.Apply(attached.GetComponent<Collider>(), in weak);
                Assert.That(column.AccumulatedImpactImpulse, Is.GreaterThan(previous),
                    "Hits on an attached cell must continue damaging its owner after fracture.");

                var chunk = cells.First(x => x.IsEarthTargetValid);
                Rigidbody chunkBody = chunk.Body;
                float parentMass = chunkBody.mass;
                uint parentSource = chunk.StableEarthId;
                chunkBody.linearVelocity = Vector3.zero;
                chunkBody.angularVelocity = Vector3.zero;
                chunkBody.isKinematic = true;
                chunkBody.position += Vector3.up * 100f;
                Physics.SyncTransforms();
                Assert.That(chunk.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(originalMeshes[chunk]),
                    "Detaching a cell must preserve its exact authored render shape.");
                Assert.That(chunk.GetComponent<MeshCollider>().sharedMesh, Is.SameAs(originalColliders[chunk]));
                yield return new WaitForSeconds(.25f);
                int before = column.ShatteredPieceCount;
                for (uint hit = 0; hit < 20 && chunk.gameObject.activeSelf; hit++)
                {
                    var impact = new EarthStructureImpact(chunkBody.position, Vector3.right,
                        Mathf.Max(12f, parentMass * 2f), EarthStructureImpactKind.Projectile, 900000u + hit);
                    bool broke = chunk.ApplyEarthImpact(in impact);
                    if (hit == 0) Assert.That(broke, Is.False, "Repeated subthreshold hits should build fatigue.");
                }
                Assert.That(column.ShatteredPieceCount, Is.EqualTo(before + 1));
                Assert.That(chunk.gameObject.activeSelf, Is.False);
                var children = pool.GetComponentsInChildren<EarthRockDebris>(false).Where(x =>
                    x.MatterIdentity.TryRead(out var record) && record.Source.SourceStableId == parentSource).ToArray();
                Assert.That(children.Length, Is.GreaterThanOrEqualTo(3));
                Assert.That(children.Sum(x => x.EarthMass), Is.EqualTo(parentMass).Within(.01f));
                Assert.That(children.All(x => x.IsEarthTargetValid && x.Body.angularVelocity.sqrMagnitude > .01f), Is.True);
                Assert.That(children.Select(x => x.Body.angularVelocity).Distinct().Count(), Is.GreaterThan(1));
                // A consumed cell must not be restored alongside its live children.
                column.SetMagicRepairProgress(1f);
                Assert.That(chunk.gameObject.activeSelf, Is.False);

                Assert.That(column.TryPluckCell(contact, out var thrown), Is.True);
                var thrownCell = (EarthArenaPiece)thrown;
                Assert.That(thrownCell.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(originalMeshes[thrownCell]));
                Assert.That(thrownCell.GetComponent<MeshCollider>().sharedMesh, Is.SameAs(originalColliders[thrownCell]));
                var thrownBody = thrownCell.Body;
                thrownCell.GetComponent<GravityBody>().enabled = false;
                thrownBody.linearVelocity = Vector3.zero;
                thrownBody.angularVelocity = Vector3.zero;
                thrownBody.position = new Vector3(1000f, 1000f, 1000f);
                Physics.SyncTransforms();
                yield return new WaitForSeconds(.25f);
                obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.transform.localScale = new Vector3(.5f, 20f, 20f);
                obstacle.transform.position = thrownBody.position + Vector3.right *
                    (thrownCell.GetComponent<Collider>().bounds.extents.x + 1f);
                Physics.SyncTransforms();
                thrownBody.linearVelocity = Vector3.right * 20f;
                for (int tick = 0; tick < 45 && thrownCell.gameObject.activeSelf; tick++)
                    yield return new WaitForFixedUpdate();
                Assert.That(thrownCell.gameObject.activeSelf, Is.False,
                    "A thrown column cell must split when it collides with solid geometry.");

                var walls = All<EarthWallPool>(scene).First();
                var wall = walls.Acquire(new Vector3(-2, 120, 0), new Vector3(2, 120, 0),
                    Vector3.zero, 2f, 1f, 777u, Vector3.up);
                Assert.That(wall, Is.Not.Null);
                yield return new WaitForSeconds(.6f);
                for (int hit = 0; hit < 5; hit++)
                    Assert.That(wall.ApplyRockImpact(wall.transform.position, Vector3.forward, 10f), Is.False);
                Assert.That(wall.ApplyRockImpact(wall.transform.position, Vector3.forward, 10f), Is.True);
                var natural = All<EarthWallPiece>(scene).Where(x => x.Owner == wall)
                    .Select(x => x.GetComponent<MeshFilter>()).Where(x =>
                    x.sharedMesh != null && x.sharedMesh.name.Contains("Natural Fracture Stone")).ToArray();
                Assert.That(natural.Length, Is.GreaterThan(1), "The wall must break into rounded ground-stone shapes.");
                Assert.That(natural.All(x => x.GetComponent<Renderer>().sharedMaterial == pool.StoneMaterial), Is.True);
                foreach (var stone in natural)
                {
                    Assert.That(stone.sharedMesh.subMeshCount, Is.EqualTo(1));
                    Assert.That(stone.GetComponent<Renderer>().sharedMaterials,
                        Is.EqualTo(new[] { pool.StoneMaterial }),
                        "No retained clay slot may redraw the natural stone submesh.");
                }
            }
            finally
            {
                if (projectile != null) Object.Destroy(projectile);
                if (obstacle != null) Object.Destroy(obstacle);
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }

        private static T[] All<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();
    }

    public sealed class EarthCombatCollisionProbe : MonoBehaviour
    {
        public string Hit { get; private set; } = "none";
        private void OnCollisionEnter(Collision collision) => Hit = collision.collider.name + ": " + collision.relativeVelocity;
    }
}
