using System.Collections;
using System.Reflection;
using Elemental.Runtime.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class BootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator WorldBootstrap_AdvancesInPlayMode()
        {
            GameObject gameObject = new GameObject("World Bootstrap Test");
            WorldBootstrap bootstrap = gameObject.AddComponent<WorldBootstrap>();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(bootstrap.CurrentTick.Value, Is.GreaterThanOrEqualTo(1u));
            Object.Destroy(gameObject);
        }

        [UnityTest]
        public IEnumerator WorldBootstrap_RebuildsNonSerializedClockBeforeFixedTick()
        {
            GameObject gameObject = new GameObject("World Bootstrap Domain Reload Test");
            WorldBootstrap bootstrap = gameObject.AddComponent<WorldBootstrap>();
            yield return new WaitForFixedUpdate();

            FieldInfo clockField = typeof(WorldBootstrap).GetField(
                "_clock",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(clockField, Is.Not.Null);
            clockField.SetValue(bootstrap, null);

            yield return new WaitForFixedUpdate();

            Assert.That(bootstrap.CurrentTick.Value, Is.GreaterThanOrEqualTo(1u));
            Object.Destroy(gameObject);
        }
    }
}
