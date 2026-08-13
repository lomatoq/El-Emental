using System.Collections;
using Elemental.Runtime.Networking;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class OnlineSpikeRuntimeTests
    {
        [UnityTest]
        public IEnumerator FourClientLabRunsLatencyLossAndBoundedCorrections()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "Assets/Elemental/Content/Scenes/OnlineSpike.unity", LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null); yield return load;
            OnlineSpikeDriver driver = Object.FindAnyObjectByType<OnlineSpikeDriver>();
            Assert.That(driver, Is.Not.Null);
            for (int tick = 0; tick < 180; tick++) yield return new WaitForFixedUpdate();
            Assert.That(driver.Harness.ClientCount, Is.EqualTo(4));
            Assert.That(driver.Harness.Authority.AcceptedCount, Is.GreaterThan(50));
            Assert.That(driver.Harness.DroppedCount, Is.GreaterThan(0));
            Assert.That(driver.Harness.QueueDebt, Is.LessThan(32));
            Assert.That(driver.CorrectionCount, Is.GreaterThan(0));
            Assert.That(driver.MaximumCorrectionError, Is.LessThan(0.36f));
            AsyncOperation unload = SceneManager.UnloadSceneAsync(
                SceneManager.GetSceneByPath("Assets/Elemental/Content/Scenes/OnlineSpike.unity"));
            if (unload != null) yield return unload;
        }
    }
}
