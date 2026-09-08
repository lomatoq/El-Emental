using System.Collections.Generic;
using System.Reflection;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthCharacterBodyTargetTests
    {
        private readonly List<GameObject> _objects = new();
        private GameObject Make(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RagdollBoneCannotBecomeEarthMatterEvenAfterDetachment(bool detached)
        {
            GameObject actor = Make("actor");
            GameObject bone = Make("corpse limb");
            bone.transform.SetParent(actor.transform);
            Rigidbody body = bone.AddComponent<Rigidbody>();
            body.useGravity = false;
            Collider shape = bone.AddComponent<BoxCollider>();
            bone.AddComponent<HumanoidRagdollBone>().enabled = false;
            PhysicalImpactTarget target = bone.AddComponent<PhysicalImpactTarget>();
            target.Configure(body);
            if (detached) bone.transform.SetParent(null);
            Assert.That(EarthTargetResolver.Resolve(shape, null).IsValid, Is.False);
            Assert.That(target.IsEarthTargetValid, Is.False);
            var control = Make("control").AddComponent<EarthTelekinesisController>();
            BendTuning tuning = BendTuning.Default;
            Assert.That(control.TryAcquire(body, Vector3.up, in tuning), Is.False,
                "Raw Rigidbody acquisition must not bypass the character filter.");
            Assert.That(control.TryAcquire(body, Vector3.up, in tuning, target), Is.False);
            Assert.That(control.Body, Is.Null);
            Assert.That(body.isKinematic, Is.False);
            target.ApplyImpact(Vector3.zero, Vector3.forward, 2f);
            Assert.That(target.ImpactCount, Is.EqualTo(1),
                "Rejecting Earth control must preserve ordinary stone collision/impact handling.");
            var executor = Make("executor").AddComponent<MagicExecutor>();
            Assert.That(executor.TryBeginVectorField(shape, body, Vector3.zero, Vector3.up), Is.False);
            MethodInfo gravityResolve = typeof(MagicExecutor).GetMethod("ResolveExplicitGravityTarget",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(gravityResolve.Invoke(executor, new object[] { shape }), Is.Null,
                "Area gravity must not classify a corpse impact receiver as a rock.");
        }

        [Test]
        public void InactiveCharacterAncestorStillExcludesUnmarkedProxyButAllowsSeparateStone()
        {
            GameObject actor = Make("disabled character");
            actor.AddComponent<EarthCharacterImpactTarget>();
            actor.SetActive(false);
            GameObject proxy = Make("unmarked proxy");
            proxy.transform.SetParent(actor.transform);
            Rigidbody proxyBody = proxy.AddComponent<Rigidbody>();
            Assert.That(EarthBodyTargetFilter.IsCharacterBody(proxyBody), Is.True);
            GameObject stone = Make("separate earth piece");
            stone.transform.SetParent(actor.transform);
            Rigidbody stoneBody = stone.AddComponent<Rigidbody>();
            stone.AddComponent<EarthPieceRuntime>();
            Assert.That(EarthBodyTargetFilter.IsCharacterBody(stoneBody), Is.False,
                "Actual independently owned matter does not become flesh through parenting.");
        }

        [Test]
        public void OrdinaryImpactRockStillResolvesAndCanBeAcquired()
        {
            GameObject stone = Make("stone");
            Rigidbody body = stone.AddComponent<Rigidbody>();
            body.useGravity = false;
            Collider shape = stone.AddComponent<BoxCollider>();
            PhysicalImpactTarget target = stone.AddComponent<PhysicalImpactTarget>();
            target.Configure(body);
            Assert.That(EarthTargetResolver.Resolve(shape, null).PhysicalTarget, Is.SameAs(target));
            Assert.That(target.IsEarthTargetValid, Is.True);
            var control = Make("control").AddComponent<EarthTelekinesisController>();
            BendTuning tuning = BendTuning.Default;
            Assert.That(control.TryAcquire(body, Vector3.up, in tuning, target), Is.True);
            Assert.That(control.Body, Is.SameAs(body));
            control.Clear();
            Assert.That(control.Body, Is.Null);
        }
    }
}
