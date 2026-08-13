using System.Collections;
using Elemental.Runtime.Capabilities;
using Elemental.Simulation.Capabilities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class CapabilityRuntimeTests
    {
        [UnityTest]
        public IEnumerator WebLabLoadsEarthSliceWithExplicitProfileAndFiniteTelemetry()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "Assets/Elemental/Content/Scenes/WebLab.unity", LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null); yield return load;
            CapabilityRuntimeBehaviour runtime = Object.FindAnyObjectByType<CapabilityRuntimeBehaviour>();
            Assert.That(runtime, Is.Not.Null);
            for (int tick = 0; tick < 30; tick++) yield return new WaitForFixedUpdate();
            Assert.That(runtime.Profile.Kind, Is.EqualTo(CapabilityProfileKind.WebLab));
            Assert.That(runtime.Profile.SupportsCompute, Is.False);
            Assert.That(runtime.Profile.Budgets.ActiveChunks, Is.EqualTo(64));
            Assert.That(runtime.Decision.CanonicalActiveRulesChanged, Is.False);
            Assert.That(float.IsFinite(runtime.MemoryMegabytes), Is.True);
            Assert.That(runtime.MemoryMegabytes, Is.LessThan(runtime.Profile.Budgets.MemoryMegabytes));
            AsyncOperation unload = SceneManager.UnloadSceneAsync(
                SceneManager.GetSceneByPath("Assets/Elemental/Content/Scenes/WebLab.unity"));
            if (unload != null) yield return unload;
        }
    }
}
