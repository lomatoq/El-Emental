using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Elemental.Online;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class ArenaRoundResetTests
    {
        [UnityTest]
        public IEnumerator LivesPreserveDamageAndMatchBoundariesRestoreTerrainDecorAndMatter()
        {
            Scene prior = SceneManager.GetActiveScene(); Scene scene = SceneManager.CreateScene("Arena reset fixture");
            SceneManager.SetActiveScene(scene);
            try
            {
                var planet = new GameObject("planet").AddComponent<VoxelPlanetBehaviour>();
                var kernel = new GameObject("kernel").AddComponent<EarthMatterKernelBehaviour>();
                var holder = new GameObject("authored parent");
                var other = new GameObject("temporary grip parent");
                GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cube); stone.name = "authored free boulder";
                stone.transform.SetParent(holder.transform); stone.transform.position = new Vector3(10, 10, 10);
                Rigidbody body = stone.AddComponent<Rigidbody>(); body.useGravity = false;
                var decor = stone.AddComponent<EarthDestructibleDecorRock>(); decor.enabled = false; // no pool needed for this damage/restore fixture
                decor.Configure(77, body, stone.GetComponent<Collider>(), null, null, .5f, 900, false);
                var identity = stone.GetComponent<EarthMatterIdentity>();
                var authored = new EarthMatterRecord { Phase = EarthMatterPhase.FreeDynamic,
                    Representation = EarthRepresentationTier.HeroPhysical, Material = EarthMaterialKind.Stone,
                    Volume = 1, Mass = 5, Integrity = 1, Shape = EarthShapeSemantic.NaturalRock,
                    RestPose = EarthMatterPose.Identity, CurrentPose = EarthMatterPose.Identity };
                Assert.That(identity.Configure(kernel, authored, body), Is.True);
                for (int i = 0; i < 120 && !planet.GeometryReady; i++) yield return null;
                Assert.That(planet.GeometryReady, Is.True);
                var duel = new GameObject("duel").AddComponent<EarthMvpDuelController>(); duel.SetRoundReady(true); duel.RestartRound();
                Vector3 baseline = body.position;
                var pendingOwner = new GameObject("pending extraction"); pendingOwner.SetActive(false);
                var executor = pendingOwner.AddComponent<MagicExecutor>();
                var pending = (List<TerrainExtractionTransaction>)typeof(MagicExecutor).GetField("_pendingExtractions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(executor);
                for (int round = 0; round < 3; round++)
                {
                    if (round > 0) { duel.RestartRound(); duel.SetRoundReady(true); }
                    EarthMatterId old = identity.MatterId;
                    stone.transform.SetParent(other.transform); body.position += Vector3.right * 4;
                    decor.ApplyImpact(body.position, Vector3.right, 100);
                    planet.ApplySphereEdit(new Vector3(0, 1, 0), .45f, false);
                    pending.Add(new TerrainExtractionTransaction(default, null, 1, default(AbilityId), Vector3.zero, Vector3.zero, Vector3.up, Vector3.zero, 1, 1));
                    Assert.That(executor.HasPendingExtraction, Is.True);
                    duel.KnockoutPlayer(Vector3.zero); if (round == 1) duel.KnockoutBot();
                    float respawnDeadline = Time.realtimeSinceStartup + 15f;
                    while ((duel.PlayerHealth < 100 || duel.BotHealth < 100) && Time.realtimeSinceStartup < respawnDeadline) yield return null;
                    Assert.That(duel.PlayerHealth, Is.EqualTo(100)); Assert.That(duel.BotHealth, Is.EqualTo(100));
                    Assert.That(duel.ArenaResetCount, Is.EqualTo(round), "A lost life must preserve the damaged arena.");
                    Assert.That(planet.State.EditCount, Is.GreaterThan(0));
                    Assert.That(decor.CaptureArenaIntegrity(), Is.LessThan(900));
                    Assert.That(stone.transform.parent, Is.EqualTo(other.transform));
                    Assert.That(identity.MatterId, Is.EqualTo(old)); Assert.That(executor.HasPendingExtraction, Is.True);
                    Assert.That(duel.BotScore, Is.EqualTo(1)); Assert.That(duel.PlayerScore, Is.EqualTo(round == 1 ? 1 : 0));
                    if (round == 0) duel.RestoreArenaForMatchBoundary(); // Return to Main.
                    else if (round == 1)
                    {
                        // Let the normal match clock expire; no forced reset call.
                        var match = (EarthDuelMatchState)typeof(EarthMvpDuelController).GetProperty("Match", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(duel);
                        typeof(EarthDuelMatchState).GetField("<RemainingSeconds>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(match, .01f);
                        yield return new WaitForFixedUpdate();
                    }
                    else duel.RestartRound(); // Start a new whole match.
                    Assert.That(duel.CombatAllowed, Is.False, "Gameplay waits for the boundary restoration.");
                    float resetDeadline = Time.realtimeSinceStartup + 15f;
                    while (duel.ArenaResetCount <= round && Time.realtimeSinceStartup < resetDeadline) yield return null;
                    Assert.That(duel.ArenaResetError, Is.Null);
                    Assert.That(duel.ArenaResetCount, Is.EqualTo(round + 1)); Assert.That(planet.GeometryReady, Is.True);
                    Assert.That(planet.State.EditCount, Is.Zero); Assert.That(executor.HasPendingExtraction, Is.False);
                    Assert.That(stone.transform.parent, Is.EqualTo(holder.transform));
                    Assert.That(Vector3.Distance(body.position, baseline), Is.LessThan(.01f));
                    Assert.That(body.isKinematic, Is.False); Assert.That(body.linearVelocity.magnitude, Is.LessThan(.01f));
                    Assert.That(decor.CaptureArenaIntegrity(), Is.EqualTo(900)); Assert.That(decor.IsShattered, Is.False);
                    Assert.That(identity.MatterId, Is.Not.EqualTo(old)); Assert.That(kernel.TryGet(old, out _), Is.False);
                    Assert.That(identity.IsRegistered, Is.True);
                    Assert.That(duel.PlayerHealth, Is.EqualTo(100)); Assert.That(duel.BotHealth, Is.EqualTo(100));
                    Assert.That(duel.BotScore, Is.EqualTo(round == 2 ? 0 : 1));
                    Assert.That(duel.PlayerScore, Is.EqualTo(round == 1 ? 1 : 0));
                    if (round == 1) Assert.That(duel.IsRoundOver, Is.True);
                }
            }
            finally { SceneManager.SetActiveScene(prior); SceneManager.UnloadSceneAsync(scene); }
        }
        [Test]
        public void PreviousWorldAcknowledgementCannotCompleteNewResetCheckpoint()
        {
            var root = new GameObject("checkpoint fixture");
            try
            {
                var world = root.AddComponent<OnlineWorldReplicaRegistry>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(OnlineWorldReplicaRegistry).GetField("_authority", flags).SetValue(world, true);
                typeof(OnlineWorldReplicaRegistry).GetField("_syncRevision", flags).SetValue(world, 2u);
                typeof(OnlineWorldReplicaRegistry).GetField("_initialEndSent", flags).SetValue(world, true);
                Assert.That(world.AcceptAcknowledgement(new OnlinePacket { Kind = OnlineMessage.WorldAck, Aux = 1 }), Is.True);
                Assert.That(world.WorldSynchronized, Is.False);
                Assert.That(world.AcceptAcknowledgement(new OnlinePacket { Kind = OnlineMessage.WorldAck, Aux = 3 }), Is.False);
                Assert.That(world.AcceptAcknowledgement(new OnlinePacket { Kind = OnlineMessage.WorldAck, Aux = 2 }), Is.True);
                Assert.That(world.WorldSynchronized, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}

