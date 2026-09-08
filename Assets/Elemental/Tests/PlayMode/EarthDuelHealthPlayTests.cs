using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthDuelHealthPlayTests
    {
        [UnityTest]
        public IEnumerator StoneWeightAddsFortyPercentShoveWithoutChangingNormalizedSeverity()
        {
            var actor = new GameObject("Stone weight transfer fixture");
            try
            {
                Rigidbody body = actor.AddComponent<Rigidbody>();
                body.mass = 80f;
                body.useGravity = false;
                body.linearDamping = 0f;
                var target = actor.AddComponent<EarthCharacterImpactTarget>();
                target.Configure(EarthDuelFighterId.Player, 98401u, body);
                EarthCharacterImpactResponse response = target.ApplyStoneImpact(
                    Vector3.zero, Vector3.right, 40f, 20f,
                    EarthCharacterImpactSourceKind.LooseStone, 98402u);
                Assert.That(response, Is.EqualTo(EarthCharacterImpactResponse.Stagger));
                Assert.That(target.LastReactionVelocityChange, Is.EqualTo(2f).Within(0.001f));
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Assert.That(body.linearVelocity.x, Is.EqualTo(1.3f * 1.4f).Within(0.02f));
            }
            finally { Object.Destroy(actor); }
        }

        [UnityTest]
        public IEnumerator SharedStoneRouteKeepsPebbleLocalAndLetsBoulderKnockDown()
        {
            var actor = new GameObject("Stone mass normalization fixture");
            try
            {
                Rigidbody body = actor.AddComponent<Rigidbody>();
                body.mass = 80f;
                body.isKinematic = true;
                var target = actor.AddComponent<EarthCharacterImpactTarget>();
                target.Configure(EarthDuelFighterId.Player, 98101u, body);
                Assert.That(target.ApplyStoneImpact(Vector3.zero, Vector3.right, 0.5f, 60f,
                    EarthCharacterImpactSourceKind.StonePunch, 98102u),
                    Is.EqualTo(EarthCharacterImpactResponse.Ignore));
                Assert.That(target.ApplyStoneImpact(Vector3.zero, Vector3.right, 40f, 20f,
                    EarthCharacterImpactSourceKind.LooseStone, 98103u),
                    Is.EqualTo(EarthCharacterImpactResponse.Stagger));
                float mediumShove = target.LastEffectiveVelocityChange;
                Assert.That(target.ApplyStoneImpact(Vector3.zero, Vector3.right, 1200f, 24f,
                    EarthCharacterImpactSourceKind.LooseStone, 98104u),
                    Is.EqualTo(EarthCharacterImpactResponse.RecoverableKnockdown));
                Assert.That(target.LastEffectiveVelocityChange, Is.GreaterThan(mediumShove));
                Assert.That(target.LastEffectiveVelocityChange, Is.LessThanOrEqualTo(3f));
                yield return null;
            }
            finally { Object.Destroy(actor); }
        }

        [UnityTest]
        public IEnumerator RestartReassertsClosedControlGateWithoutLosingOriginalFlags()
        {
            var host = new GameObject("Closed restart control gate fixture");
            var controlHost = new GameObject("Originally enabled control");
            var disabledHost = new GameObject("Originally disabled control");
            try
            {
                // Behaviour-only surrogates never render: all assertions run
                // synchronously, before the sole yield with both cameras disabled.
                var originallyEnabled = controlHost.AddComponent<Camera>();
                var originallyDisabled = disabledHost.AddComponent<Camera>();
                originallyEnabled.enabled = true; originallyDisabled.enabled = false;
                var duel = host.AddComponent<EarthMvpDuelController>();
                duel.ConfigureRoundControls(originallyEnabled, originallyDisabled);
                duel.SetRoundReady(false);
                Assert.That(originallyEnabled.enabled, Is.False);
                // Model the independent ragdoll-reset adapter restoring its own
                // controls while the outer match gate remains closed.
                originallyEnabled.enabled = originallyDisabled.enabled = true;
                duel.RestartRound();
                Assert.That(duel.CombatAllowed, Is.False);
                Assert.That(originallyEnabled.enabled, Is.False, "Restart must reassert an already closed input gate.");
                Assert.That(originallyDisabled.enabled, Is.False);
                duel.SetRoundReady(true);
                Assert.That(originallyEnabled.enabled, Is.True);
                Assert.That(originallyDisabled.enabled, Is.False, "Repeated suspension must not replace the original enabled-state ledger.");
                originallyEnabled.enabled = false;
                yield return null;
            }
            finally
            {
                Object.Destroy(host); Object.Destroy(controlHost); Object.Destroy(disabledHost);
            }
        }

        [UnityTest]
        public IEnumerator ReadinessCapturedBeforeAwakeStillRestoresControls()
        {
            var host = new GameObject("Inactive duel startup fixture");
            var controlHost = new GameObject("Round control fixture");
            host.SetActive(false);
            try
            {
                // A disabled camera is a dependency-free Behaviour surrogate;
                // it never renders because the test does not yield while enabled.
                Camera control = controlHost.AddComponent<Camera>();
                control.enabled = true;
                var duel = host.AddComponent<EarthMvpDuelController>();
                duel.ConfigureRoundControls(control);
                Assert.That(control.enabled, Is.False);
                host.SetActive(true);
                duel.SetRoundReady(true);
                Assert.That(control.enabled, Is.True,
                    "Awake discarded the readiness gate's previously captured enabled state.");
                control.enabled = false;
                yield return null;
            }
            finally
            {
                Object.Destroy(host);
                Object.Destroy(controlHost);
            }
        }

        [UnityTest]
        public IEnumerator AcceptedStoneDamageDeduplicatesAndDeathScoresOnce()
        {
            var host = new GameObject("Duel health fixture");
            var actor = new GameObject("Fighter health fixture");
            try
            {
                Rigidbody body = actor.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.mass = 80f;
                var target = actor.AddComponent<EarthCharacterImpactTarget>();
                var duel = host.AddComponent<EarthMvpDuelController>();
                target.Configure(EarthDuelFighterId.Player, 91001u, body, duel);
                duel.Configure(null, body, null, null, null, null, null, null,
                    null, null, null, target, null);
                target.ApplyStoneImpact(Vector3.zero, Vector3.right, 40f, 20f,
                    EarthCharacterImpactSourceKind.StonePunch, 912u);
                Assert.That(duel.PlayerHealth, Is.EqualTo(92f));
                target.ApplyStoneImpact(Vector3.zero, Vector3.right, 40f, 20f,
                    EarthCharacterImpactSourceKind.StonePunch, 912u);
                Assert.That(duel.PlayerHealth, Is.EqualTo(92f));
                Assert.That(duel.BotScore, Is.Zero);
                duel.ApplyDamage(EarthDuelFighterId.Player, 100f, RagdollHandoff.Uniform(Vector3.zero));
                duel.RequestKnockout(EarthDuelFighterId.Player, RagdollHandoff.Uniform(Vector3.zero));
                Assert.That(duel.BotScore, Is.EqualTo(1));
                Assert.That(duel.PlayerPhase, Is.EqualTo(EarthDuelFighterPhase.KnockedOut));
                duel.RestartRound();
                Assert.That(duel.PlayerHealth, Is.EqualTo(100f));
                Assert.That(duel.BotScore, Is.Zero);
                Assert.That(duel.PlayerPhase, Is.EqualTo(EarthDuelFighterPhase.Active));
                yield return null;
            }
            finally
            {
                Object.Destroy(host);
                Object.Destroy(actor);
            }
        }
    }
}
