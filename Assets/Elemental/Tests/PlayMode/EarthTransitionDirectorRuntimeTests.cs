using System.Collections;
using System.Text.RegularExpressions;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthTransitionDirectorRuntimeTests
    {
        private const string CanonicalScenePath =
            "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private static readonly int LocomotionHash =
            Animator.StringToHash("Base Layer.Locomotion");
        private static readonly int TurnHash =
            Animator.StringToHash("Base Layer.Turn In Place");
        private static readonly int JumpHash =
            Animator.StringToHash("Base Layer.Jump");
        private EarthTransitionProfile _profile;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_profile != null)
            {
                UnityEngine.Object.Destroy(_profile);
                _profile = null;
                yield return null;
            }
            Scene scene = SceneManager.GetSceneByPath(CanonicalScenePath);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator CanonicalDirectorExecutesPairsWarnsOnlyOnUsedFallbackAndDrainsQueue()
        {
            yield return SceneManager.LoadSceneAsync(CanonicalScenePath, LoadSceneMode.Additive);
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(CanonicalScenePath);
            Animator animator = FindCanonicalAnimator(scene);
            Assert.That(animator, Is.Not.Null);
            IsolateAnimator(scene, animator.transform.root);
            EarthTransitionDirector director =
                animator.GetComponent<EarthTransitionDirector>();
            if (director == null)
                director = animator.gameObject.AddComponent<EarthTransitionDirector>();
            EarthAnimationGraph graph = animator.GetComponent<EarthAnimationGraph>();
            if (graph == null)
                graph = animator.gameObject.AddComponent<EarthAnimationGraph>();
            DisableCompetingBehaviours(animator.transform.root, director, graph);
            director.enabled = true;
            animator.speed = 0f;
            animator.Play(LocomotionHash, 0, 0.25f);
            animator.Update(0f);
            EarthAnimationGraphSettings graphSettings =
                new EarthAnimationGraphSettings(true, true);
            Assert.That(graph.Configure(animator, in graphSettings), Is.True);
            director.Configure(animator, null);
            _profile = CreateProfile();
            director.ConfigureTransitionProfile(_profile);
            director.SynchronizeState(
                EarthMotionStateId.Locomotion,
                LocomotionHash,
                EarthAnimationTransitionPriority.Idle);
            EarthAnimationTransitionContext turnContext = Context(
                EarthMotionStateId.Locomotion,
                EarthMotionStateId.TurnInPlace,
                EarthMotionCategory.Locomotion,
                EarthMotionCategory.Turn,
                0.25f,
                EarthAnimationTransitionPriority.Idle);

            Assert.That(director.RequestTransition(TurnHash, in turnContext), Is.True);
            Assert.That(director.ActiveState, Is.EqualTo(EarthMotionStateId.TurnInPlace));
            EarthTransitionDirectorDiagnostics authored = director.Diagnostics;
            Assert.That(authored.ProfileEnabled, Is.True);
            Assert.That(authored.QueueEnabled, Is.True);
            Assert.That(authored.LastResolution,
                Is.EqualTo(EarthTransitionProfileResolution.AuthoredPair));
            Assert.That(authored.AuthoredPairExecutionCount, Is.EqualTo(1u));
            Assert.That(authored.LastRule.Family,
                Is.EqualTo(EarthTransitionFamily.PoseInertialized));
            Assert.That(graph.ActiveTransitionPositionHalfLifeSeconds,
                Is.EqualTo(0.055f).Within(0.0001f));
            Assert.That(graph.ActiveTransitionRotationHalfLifeSeconds,
                Is.EqualTo(0.055f).Within(0.0001f));
            Assert.That(graph.ActiveTransitionMaximumDurationSeconds,
                Is.EqualTo(0.33f).Within(0.0001f));
            Assert.That(graph.ActiveTransitionBodyMask,
                Is.EqualTo(EarthTransitionBodyMask.Spine |
                           EarthTransitionBodyMask.LeftArm |
                           EarthTransitionBodyMask.RightArm));

            EarthAnimationTransitionContext fallbackContext = Context(
                EarthMotionStateId.TurnInPlace,
                EarthMotionStateId.Jump,
                EarthMotionCategory.Turn,
                EarthMotionCategory.Airborne,
                0.4f,
                EarthAnimationTransitionPriority.Idle);
            LogAssert.Expect(
                LogType.Warning,
                new Regex("EarthTransitionProfile used generic fixed crossfade.*TurnInPlace -> Jump"));
            Assert.That(director.RequestTransition(JumpHash, in fallbackContext), Is.True);
            Assert.That(director.LastReason,
                Is.EqualTo(EarthAnimationTransitionReason.ProfileFallback));
            Assert.That(director.Diagnostics.GenericFallbackExecutionCount, Is.EqualTo(1u));

            Assert.That(director.RequestTransition(JumpHash, in fallbackContext), Is.True);
            LogAssert.NoUnexpectedReceived();
            Assert.That(director.Diagnostics.GenericFallbackExecutionCount, Is.EqualTo(2u));

            graph.Play(JumpHash, 0, 0.5f);
            yield return null;
            director.SynchronizeState(
                EarthMotionStateId.Jump,
                JumpHash,
                EarthAnimationTransitionPriority.Idle);
            EarthAnimationTransitionContext queuedContext = Context(
                EarthMotionStateId.Jump,
                EarthMotionStateId.Locomotion,
                EarthMotionCategory.Airborne,
                EarthMotionCategory.Locomotion,
                0.5f,
                EarthAnimationTransitionPriority.Idle);
            Assert.That(director.RequestTransition(LocomotionHash, in queuedContext), Is.False);
            Assert.That(director.Diagnostics.QueuedRequestCount, Is.EqualTo(1));
            Assert.That(director.Diagnostics.QueuedRequestCountTotal, Is.EqualTo(1u));

            graph.Play(JumpHash, 0, 0.95f);
            yield return null;

            EarthTransitionDirectorDiagnostics drained = director.Diagnostics;
            Assert.That(drained.QueuedRequestCount, Is.Zero);
            Assert.That(drained.DequeuedExecutionCount, Is.EqualTo(1u));
            Assert.That(director.ActiveState, Is.EqualTo(EarthMotionStateId.Locomotion));
            Assert.That(drained.LastResolution,
                Is.EqualTo(EarthTransitionProfileResolution.Queued));
            LogAssert.NoUnexpectedReceived();
        }

        private static Animator FindCanonicalAnimator(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Animator[] animators = root.GetComponentsInChildren<Animator>(true);
                for (int index = 0; index < animators.Length; index++)
                {
                    Animator candidate = animators[index];
                    if (candidate.isHuman && candidate.runtimeAnimatorController != null)
                        return candidate;
                }
            }
            return null;
        }

        private static void IsolateAnimator(Scene scene, Transform keptRoot)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.transform != keptRoot) root.SetActive(false);
        }

        private static void DisableCompetingBehaviours(
            Transform root,
            EarthTransitionDirector director,
            EarthAnimationGraph graph)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour == director || behaviour == graph ||
                    behaviour.GetType().Namespace == "UnityEngine.Animations.Rigging")
                    continue;
                behaviour.enabled = false;
            }
        }

        private EarthTransitionProfile CreateProfile()
        {
            EarthTransitionRule authored = Rule(
                EarthTransitionFamily.PoseInertialized,
                EarthAnimationTransitionPriority.Locomotion,
                default,
                false,
                0.055f,
                EarthTransitionBodyMask.Spine |
                EarthTransitionBodyMask.LeftArm |
                EarthTransitionBodyMask.RightArm);
            EarthNormalizedAnimationWindow protectedWindow =
                new EarthNormalizedAnimationWindow(true, 0f, 0.8f);
            EarthTransitionRule queued = Rule(
                EarthTransitionFamily.PhaseSynchronized,
                EarthAnimationTransitionPriority.Locomotion,
                protectedWindow,
                true,
                0.07f,
                EarthTransitionBodyMask.FullBody);
            var profile = ScriptableObject.CreateInstance<EarthTransitionProfile>();
            profile.Configure(
                true,
                true,
                4,
                0.08f,
                new[]
                {
                    new EarthTransitionPairOverride(
                        EarthMotionStateId.Locomotion,
                        EarthMotionStateId.TurnInPlace,
                        in authored),
                    new EarthTransitionPairOverride(
                        EarthMotionStateId.Jump,
                        EarthMotionStateId.Locomotion,
                        in queued)
                });
            return profile;
        }

        private static EarthTransitionRule Rule(
            EarthTransitionFamily family,
            EarthAnimationTransitionPriority priority,
            EarthNormalizedAnimationWindow protectedWindow,
            bool queueWhenBlocked,
            float halfLifeSeconds,
            EarthTransitionBodyMask bodyMask)
        {
            EarthNormalizedAnimationWindow cancelWindow = default;
            return new EarthTransitionRule(
                true,
                family,
                priority,
                halfLifeSeconds,
                0.10f,
                EarthTransitionGaitPhaseRule.PreserveSource,
                EarthTransitionContactPolicy.PreserveCurrentPlants,
                EarthTransitionCancelPolicy.OutsideProtectedWindow,
                in protectedWindow,
                in cancelWindow,
                0f,
                bodyMask,
                EarthTransitionFootReleasePolicy.PreservePlanted,
                0f,
                queueWhenBlocked);
        }

        private static EarthAnimationTransitionContext Context(
            EarthMotionStateId source,
            EarthMotionStateId destination,
            EarthMotionCategory sourceCategory,
            EarthMotionCategory destinationCategory,
            float sourcePhase,
            EarthAnimationTransitionPriority activePriority) =>
            new EarthAnimationTransitionContext(
                source,
                destination,
                sourceCategory,
                destinationCategory,
                EarthAnimationTransitionPriority.Locomotion,
                activePriority,
                sourcePhase,
                sourcePhase,
                1f,
                0.6f,
                0.1f,
                false,
                true,
                false,
                false);
    }
}
