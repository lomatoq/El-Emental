using System.Collections;
using Elemental.Runtime.Missions;
using Elemental.Simulation.Missions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class MissionRuntimeTests
    {
        [UnityTest]
        public IEnumerator VolcanoVillageRunsBoundedCrisisAndCivilianPresentation()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "Assets/Elemental/Content/Scenes/VolcanoVillage.unity",
                LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            MissionDirectorBehaviour director = Object.FindAnyObjectByType<MissionDirectorBehaviour>();
            CrisisPresentationPool crisis = Object.FindAnyObjectByType<CrisisPresentationPool>();
            CivilianProxyBehaviour[] civilians = Object.FindObjectsByType<CivilianProxyBehaviour>(FindObjectsInactive.Include);
            MissionTerrainLever[] terrain = Object.FindObjectsByType<MissionTerrainLever>();
            Assert.That(director, Is.Not.Null);
            Assert.That(crisis, Is.Not.Null);
            Assert.That(civilians.Length, Is.EqualTo(12));
            Assert.That(terrain.Length, Is.EqualTo(6));

            for (int tick = 0; tick < 250; tick++) yield return new WaitForFixedUpdate();
            Assert.That(director.Simulation.Director.ActiveCount, Is.LessThanOrEqualTo(12));
            Assert.That(director.Simulation.Director.TimelineCount, Is.GreaterThan(0));
            Assert.That(crisis.ShownCount, Is.GreaterThan(0));
            Assert.That(
                director.Simulation.Outcome == MissionOutcome.Running || director.Simulation.Outcome == MissionOutcome.Win,
                Is.True);

            director.SelectStrategy(MissionStrategyKind.AirEvacuate);
            for (int tick = 0; tick < 900 && director.Simulation.Outcome == MissionOutcome.Running; tick++)
                director.Simulation.Tick(0.1f);
            Assert.That(director.Simulation.Outcome, Is.EqualTo(MissionOutcome.Win));
            Assert.That(director.Simulation.RescuedCount, Is.GreaterThanOrEqualTo(8));

            AsyncOperation unload = SceneManager.UnloadSceneAsync(
                SceneManager.GetSceneByPath("Assets/Elemental/Content/Scenes/VolcanoVillage.unity"));
            if (unload != null) yield return unload;
        }
    }
}
