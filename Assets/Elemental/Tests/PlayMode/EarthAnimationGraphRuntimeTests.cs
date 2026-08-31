using System.Collections;
using Elemental.Presentation.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthAnimationGraphRuntimeTests
    {
        [UnityTest]
        public IEnumerator MissingControllerFailsLoudlyToLegacyAnimatorOwnership()
        {
            var root = new GameObject("Animation Graph Fallback Test");
            try
            {
                Animator animator = root.AddComponent<Animator>();
                EarthAnimationGraph graph = root.AddComponent<EarthAnimationGraph>();
                var settings = new EarthAnimationGraphSettings(true, true);

                Assert.That(graph.Configure(animator, in settings), Is.False);
                yield return null;

                EarthAnimationGraphDiagnostics diagnostics = graph.Diagnostics;
                Assert.That(diagnostics.GraphValid, Is.False);
                Assert.That(diagnostics.LegacyFallbackActive, Is.True);
                Assert.That(diagnostics.FallbackReason,
                    Is.EqualTo(EarthAnimationGraphFallbackReason.MissingController));
                Assert.That(animator.enabled, Is.True);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator DefaultSettingsLeavePlayableGraphAndInertializationOff()
        {
            var root = new GameObject("Animation Graph Default Off Test");
            try
            {
                Animator animator = root.AddComponent<Animator>();
                EarthAnimationGraph graph = root.AddComponent<EarthAnimationGraph>();
                EarthAnimationGraphSettings settings = EarthAnimationGraphSettings.Disabled;

                Assert.That(settings.UsePlayablesAnimationGraph, Is.False);
                Assert.That(settings.UsePoseInertialization, Is.False);
                Assert.That(graph.Configure(animator, in settings), Is.False);
                yield return null;

                Assert.That(graph.Diagnostics.FallbackReason,
                    Is.EqualTo(EarthAnimationGraphFallbackReason.FeatureDisabled));
                Assert.That(graph.IsActive, Is.False);
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
