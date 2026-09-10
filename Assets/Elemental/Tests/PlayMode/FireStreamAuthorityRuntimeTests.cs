using System.Reflection;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Fire;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.PlayMode
{
    public sealed class FireStreamAuthorityRuntimeTests
    {
        private GameObject root;
        private EarthMvpDuelController duel;
        private FireStreamSession session;
        private FireWorldBehaviour world;
        private Transform target, muzzle;
        private float oldStep, oldScale;
        private static readonly MethodInfo Tick = typeof(FireStreamSession).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo Reset = typeof(FireStreamSession).GetMethod("OnRoundRestart", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp] public void Setup()
        {
            oldStep = Time.fixedDeltaTime; oldScale = Time.timeScale; Time.timeScale = 1;
            root = new GameObject("Fire authority fixture");
            var player = Child("Player"); player.transform.position = new Vector3(0, 1, 0);
            Rigidbody playerBody = player.AddComponent<Rigidbody>(); playerBody.isKinematic = true;
            player.AddComponent<CapsuleCollider>();
            var bot = Child("Bot"); target = bot.transform; target.position = new Vector3(0, 1, 4);
            Rigidbody botBody = bot.AddComponent<Rigidbody>(); botBody.isKinematic = true;
            CapsuleCollider capsule = bot.AddComponent<CapsuleCollider>(); capsule.radius = .3f; capsule.height = 2;
            var duplicate = new GameObject("Compound second collider"); duplicate.layer = 30;
            duplicate.transform.SetParent(target, false);
            var second = duplicate.AddComponent<CapsuleCollider>(); second.radius = .3f; second.height = 2;
            muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(player.transform, false);
            var gravity = Child("Gravity").AddComponent<GravityWorldBehaviour>();
            world = Child("FireWorld").AddComponent<FireWorldBehaviour>();
            world.Configure(gravity, FireWorldSettings.Default, 1 << 30);
            duel = Child("Duel").AddComponent<EarthMvpDuelController>();
            duel.Configure(null, playerBody, null, null, null, null, botBody, capsule, null, null, null, null, null);
            session = player.AddComponent<FireStreamSession>();
            session.Configure(world, duel, EarthDuelFighterId.Player, muzzle, 1 << 30);
            session.SetLocalCapability(true); UnityEngine.Physics.SyncTransforms();
        }
        private GameObject Child(string name)
        { var child = new GameObject(name); child.layer = 30; child.transform.SetParent(root.transform); return child; }
        [TearDown] public void Cleanup()
        { if (root != null) Object.DestroyImmediate(root); Time.fixedDeltaTime = oldStep; Time.timeScale = oldScale; }
        private void Step(int count)
        { for (int i = 0; i < count; i++) { UnityEngine.Physics.SyncTransforms(); Tick.Invoke(session, null); } }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void CompoundTargetReceivesSixteenHpPerSecondAfterFrontArrives(int rate)
        {
            Time.fixedDeltaTime = 1f / rate;
            Assert.That(session.TryBegin(target.position), Is.True);
            FireGroupHandle handle = session.Group; uint token = session.Generation;
            Step(rate / 5); float before = duel.BotHealth;
            Step(rate);
            Assert.That(before - duel.BotHealth, Is.EqualTo(16f).Within(.04f), "Compound colliders must not multiply authority contact time.");
            Assert.That(session.Group, Is.EqualTo(handle)); Assert.That(session.Generation, Is.EqualTo(token));
            Assert.That(session.QuerySaturations, Is.Zero);
            session.Stop(); float stopped = duel.BotHealth;
            Step(rate); Assert.That(duel.BotHealth, Is.EqualTo(stopped));
            var snapshot = new FirePresentationSnapshot();
            Assert.That(world.CopySnapshot(handle, snapshot), Is.True);
            Assert.That(snapshot.Lifecycle, Is.EqualTo(FireLifecycle.Draining));
        }
        [Test]
        public void FirstCoverBlocksDamageAndRemovingItRestoresTheSameSession()
        {
            Time.fixedDeltaTime = .02f;
            var cover = Child("Cover"); cover.transform.position = new Vector3(0, 1, 2);
            cover.AddComponent<BoxCollider>().size = new Vector3(3, 3, .25f);
            cover.AddComponent<FireSurfaceBinding>().Configure(501);
            Assert.That(session.TryBegin(target.position), Is.True);
            var group = session.Group; Step(50);
            Assert.That(duel.BotHealth, Is.EqualTo(duel.MaximumHealth)); Assert.That(session.CurrentLength, Is.LessThan(2));
            cover.SetActive(false); Step(50);
            Assert.That(duel.BotHealth, Is.LessThan(duel.MaximumHealth - 14)); Assert.That(session.Group, Is.EqualTo(group));
        }
        [Test]
        public void CapabilityAndRoundResetRetireWithoutOldRoundDamage()
        {
            Time.fixedDeltaTime = .02f;
            session.SetLocalCapability(false); Assert.That(session.TryBegin(target.position), Is.False);
            session.SetLocalCapability(true); Assert.That(session.TryBegin(target.position), Is.True);
            Step(11); float before = duel.BotHealth;
            Reset.Invoke(session, null);
            Assert.That(session.IsActive, Is.False); Assert.That(duel.BotHealth, Is.EqualTo(before));
            uint first = session.Generation;
            Assert.That(session.TryBegin(target.position), Is.True); Assert.That(session.Generation, Is.Not.EqualTo(first));
            Time.timeScale = 0; Step(1); Assert.That(session.IsActive, Is.False);
            Time.timeScale = 1; Assert.That(session.IsActive, Is.False);
        }
        [Test]
        public void StopCallbacksCannotAdmitOrDrainAnotherGeneration()
        {
            Time.fixedDeltaTime = .02f;
            Assert.That(session.TryBegin(target.position), Is.True); Step(11);
            var oldGroup = session.Group; uint oldGeneration = session.Generation;
            int stateCallbacks = 0, endedCallbacks = 0;
            System.Action onState = () => { stateCallbacks++; Assert.That(session.TryBegin(target.position), Is.False); };
            System.Action<uint> onEnded = token => { endedCallbacks++; Assert.That(token, Is.EqualTo(oldGeneration)); Assert.That(session.TryBegin(target.position), Is.False); };
            duel.StateChanged += onState; session.Ended += onEnded;
            try { session.Stop(); }
            finally { duel.StateChanged -= onState; session.Ended -= onEnded; }
            Assert.That(stateCallbacks, Is.GreaterThan(0), "Release must flush the accrued fraction through the reentrant damage event.");
            Assert.That(endedCallbacks, Is.EqualTo(1)); Assert.That(session.IsActive, Is.False);
            Assert.That(session.TryBegin(target.position), Is.True);
            Assert.That(session.Generation, Is.Not.EqualTo(oldGeneration));
            var snapshot = new FirePresentationSnapshot();
            Assert.That(world.CopySnapshot(oldGroup, snapshot), Is.True); Assert.That(snapshot.Lifecycle, Is.EqualTo(FireLifecycle.Draining));
            Assert.That(world.CopySnapshot(session.Group, snapshot), Is.True); Assert.That(snapshot.Lifecycle, Is.Not.EqualTo(FireLifecycle.Draining));
        }
        [Test]
        public void PendingContactCannotDamageAfterOwnerKnockout()
        {
            Time.fixedDeltaTime = .02f; Assert.That(session.TryBegin(target.position), Is.True); Step(11);
            float before = duel.BotHealth;
            duel.RequestKnockout(EarthDuelFighterId.Player, RagdollHandoff.Uniform(Vector3.zero));
            session.Stop(); Assert.That(duel.BotHealth, Is.EqualTo(before));
        }
        [Test]
        public void DisabledWorldCannotAdmitOrKeepAnAuthorityHold()
        {
            Assert.That(session.TryBegin(target.position), Is.True);
            world.enabled = false;
            Assert.That(world.IsReady, Is.False); Step(1); Assert.That(session.IsActive, Is.False);
            Assert.That(session.TryBegin(target.position), Is.False);
        }
    }
}
