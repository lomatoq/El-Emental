using System.Collections;
using System.Linq;
using Elemental.Presentation.Animation;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class SeptemberRespawnRuntimeTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private Scene _scene;

        [UnityTest]
        public IEnumerator FreshRoundAndKnockedOutRoundRestoreBothPhysicalActors()
        {
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False);
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(ScenePath);
            var roots = _scene.GetRootGameObjects();
            var duel = roots.SelectMany(r => r.GetComponentsInChildren<EarthMvpDuelController>(true)).Single();
            double deadline = Time.realtimeSinceStartupAsDouble + 130d;
            while (!duel.CombatAllowed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(duel.CombatAllowed, Is.True, "Scene readiness must finish before testing a live restart.");
            var bots = roots.SelectMany(r => r.GetComponentsInChildren<EarthMvpBotController>(true)).ToArray();
            foreach (var bot in bots) bot.enabled = false;
            yield return new WaitForSeconds(.5f);
            var actors = new[] { duel.PlayerTransform, duel.BotTransform };
            var origins = actors.Select(a => a.position).ToArray();

            // Critical reproduction: neither actor has ever entered ragdoll.
            duel.RestartRound();
            foreach (var bot in bots) bot.enabled = false;
            for (int i = 0; i < 60; i++) yield return null;
            for (int i = 0; i < actors.Length; i++) AssertRestored(actors[i], origins[i]);

            duel.RequestKnockout(EarthDuelFighterId.Player, RagdollHandoff.Uniform(Vector3.right));
            duel.RequestKnockout(EarthDuelFighterId.Bot, RagdollHandoff.Uniform(Vector3.left));
            yield return new WaitForSeconds(.15f);
            duel.RestartRound();
            foreach (var bot in bots) bot.enabled = false;
            for (int i = 0; i < 60; i++) yield return null;
            for (int i = 0; i < actors.Length; i++) AssertRestored(actors[i], origins[i]);
            Assert.That(duel.PlayerHealth, Is.EqualTo(duel.MaximumHealth));
            Assert.That(duel.BotHealth, Is.EqualTo(duel.MaximumHealth));
            Assert.That(duel.PlayerScore + duel.BotScore, Is.Zero);
        }

        private static void AssertRestored(Transform actor, Vector3 origin)
        {
            var body = actor.GetComponent<Rigidbody>();
            var capsule = actor.GetComponent<CapsuleCollider>();
            var motor = actor.GetComponent<PlanetMotor>();
            Assert.That(body.detectCollisions, Is.True, actor.name + " root collisions lost on restart");
            Assert.That(capsule.enabled, Is.True, actor.name + " capsule lost on restart");
            Assert.That(body.isKinematic, Is.False, actor.name);
            Assert.That(motor.enabled, Is.True, actor.name);
            Assert.That(motor.TeleportSequence, Is.GreaterThan(0u));
            Assert.That(motor.HasStableSupport, Is.True, actor.name + " did not settle onto terrain");
            Assert.That(Vector3.Distance(actor.position, origin), Is.LessThan(.75f), actor.name + " fell away from spawn");
            var rig = actor.GetComponentInChildren<HumanoidRagdollRig>(true);
            Assert.That(rig.IsRagdollActive, Is.False);
            var animator = actor.GetComponentInChildren<Animator>(true);
            Assert.That(animator.enabled, Is.True, actor.name + " Animator was disabled by default snapshot");
            foreach (var boneId in new[] { HumanBodyBones.Hips, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                Vector3 p = animator.GetBoneTransform(boneId).position;
                Assert.That(float.IsFinite(p.x + p.y + p.z), Is.True);
                Assert.That(Vector3.Distance(p, actor.position), Is.LessThan(2.5f));
            }
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }
    }
}
