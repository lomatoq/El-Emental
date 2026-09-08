using System.Collections;
using Elemental.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Tests.PlayMode
{
    internal static class ProductionCombatTestFlow
    {
        public static IEnumerator BeginBotAfterReadiness(Scene scene)
        {
            FrontendFlowController flow = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var candidate = root.GetComponentInChildren<FrontendFlowController>(true);
                if (candidate != null) flow = candidate;
            }
            // Older test scenes intentionally have no frontend; production uses
            // exactly the same public Play-vs-Bot transition as a player's click.
            if (flow == null) yield break;
            double deadline = Time.realtimeSinceStartupAsDouble + 10d;
            while (flow.State == FrontendState.Loading && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.That(flow.IsWorldReady, Is.True);
            if (flow.State == FrontendState.Main) Assert.That(flow.BeginBot(), Is.True);
            while (flow.State == FrontendState.Starting && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.That(flow.State, Is.EqualTo(FrontendState.Combat), "Production fixture could not enter the playable duel.");
        }
    }
}
