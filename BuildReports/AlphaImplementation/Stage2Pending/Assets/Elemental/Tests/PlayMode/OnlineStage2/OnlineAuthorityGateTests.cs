using System.Collections;
using Elemental.Online;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class OnlineAuthorityGateTests
    {
        [UnityTest]
        public IEnumerator ReplicaIgnoresLocalDamageRestartAndClockTick()
        {
            var root = new GameObject("Replica gate test");
            try
            {
                var duel = root.AddComponent<EarthMvpDuelController>();
                duel.ConfigureOnlineAuthority(false);
                Assert.That(duel.ApplyReplicaMatch(42, 68, 2, 3, 120, true), Is.True);
                Assert.That(duel.ApplyDamage(EarthDuelFighterId.Player, 100, RagdollHandoff.Uniform(Vector3.up)), Is.False);
                duel.RestartRound();
                yield return new WaitForFixedUpdate();
                Assert.That(duel.PlayerHealth, Is.EqualTo(42));
                Assert.That(duel.BotScore, Is.EqualTo(3));
                Assert.That(duel.RoundRemainingSeconds, Is.EqualTo(120));
            }
            finally { Object.Destroy(root); }
        }

        [Test]
        public void ReplicaStoneImpactDoesNotApplyOutcomeOrImpulse()
        {
            var root = new GameObject("Replica impact test");
            try
            {
                var body = root.AddComponent<Rigidbody>(); body.useGravity = false;
                var impact = root.AddComponent<EarthCharacterImpactTarget>();
                impact.Configure(EarthDuelFighterId.Player, 1, body);
                impact.ConfigureOnlineAuthority(false);
                var result = impact.ApplyStoneImpact(Vector3.zero, Vector3.right, 40, 12,
                    EarthCharacterImpactSourceKind.LooseStone, 123);
                Assert.That(result, Is.EqualTo(EarthCharacterImpactResponse.Ignore));
                Assert.That(impact.AcceptedImpactCount, Is.Zero);
                Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RepeatedJumpEdgeSurvivesLostInputWithoutRepeatingTheJump()
        {
            var root = new GameObject("Remote input test");
            try
            {
                var input = root.AddComponent<OnlineRemoteMotorInput>(); input.Configure(root.transform);
                var packet = new OnlinePacket { Kind = OnlineMessage.MotorInput, Id = 2,
                    Aux = 7, B = Vector3.forward, C = Vector3.up };
                Assert.That(input.Receive(packet), Is.True);
                Assert.That(input.SampleCommand(1).JumpPressed, Is.True);
                Assert.That(input.Receive(packet), Is.True);
                Assert.That(input.SampleCommand(2).JumpPressed, Is.False);
                packet.Aux = 8; input.Receive(packet);
                Assert.That(input.SampleCommand(3).JumpPressed, Is.True);
                input.Configure(root.transform); packet.Aux = 1; input.Receive(packet);
                Assert.That(input.SampleCommand(4).JumpPressed, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
