using Elemental.Online.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class OnlineActorGraphAuthoringTests
    {
        [Test]
        public void CrossRootOwnerReferencesPointToTheSameCloneGraph()
        {
            var player = new GameObject("Authored Player"); var runtime = new GameObject("Authored Magic");
            OnlineActorGraphAuthoring.Result result = null;
            try
            {
                Rigidbody playerBody = player.AddComponent<Rigidbody>(); Rigidbody runtimeBody = runtime.AddComponent<Rigidbody>();
                ConfigurableJoint sourceJoint = player.AddComponent<ConfigurableJoint>(); sourceJoint.connectedBody = runtimeBody;
                result = OnlineActorGraphAuthoring.CloneOwnerGraph(new[] { player, runtime }, null, null);
                Assert.That(result.Container.activeSelf, Is.False);
                Assert.That(result.CloneOf(playerBody), Is.Not.SameAs(playerBody));
                Assert.That(result.CloneOf(sourceJoint).connectedBody, Is.SameAs(result.CloneOf(runtimeBody)));
                Assert.That(sourceJoint.connectedBody, Is.SameAs(runtimeBody));
            }
            finally
            {
                if (result != null) Object.DestroyImmediate(result.Container);
                Object.DestroyImmediate(player); Object.DestroyImmediate(runtime);
            }
        }
        [Test]
        public void RetainedWorldServiceReplacesItsCopyInBothInternalAndCrossRootReferences()
        {
            var player = new GameObject("Player"); var runtime = new GameObject("Runtime");
            OnlineActorGraphAuthoring.Result result = null;
            try
            {
                player.AddComponent<Rigidbody>(); Rigidbody shared = runtime.AddComponent<Rigidbody>();
                ConfigurableJoint cross = player.AddComponent<ConfigurableJoint>(); cross.connectedBody = shared;
                var child = new GameObject("Internal reference"); child.transform.SetParent(runtime.transform, false);
                child.AddComponent<Rigidbody>(); ConfigurableJoint inside = child.AddComponent<ConfigurableJoint>(); inside.connectedBody = shared;
                result = OnlineActorGraphAuthoring.CloneOwnerGraph(new[] { player, runtime }, null, new Component[] { shared });
                Assert.That(result.CloneOf(cross).connectedBody, Is.SameAs(shared));
                Assert.That(result.CloneOf(inside).connectedBody, Is.SameAs(shared));
                Assert.That(result.CloneOf(runtime).GetComponent<Rigidbody>(), Is.Null);
                Assert.That(result.ExternalSceneReferences.Count, Is.GreaterThanOrEqualTo(2));
            }
            finally
            {
                if (result != null) Object.DestroyImmediate(result.Container);
                Object.DestroyImmediate(player); Object.DestroyImmediate(runtime);
            }
        }
    }
}
