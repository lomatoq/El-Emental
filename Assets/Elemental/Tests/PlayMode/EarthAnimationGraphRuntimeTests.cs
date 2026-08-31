using System;
using System.Collections;
using System.Reflection;
using Elemental.Presentation.Animation;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthAnimationGraphRuntimeTests
    {
        private const string CanonicalScenePath =
            "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private EarthAnimationGraphProfile _testProfile;

        [UnityTearDown]
        public IEnumerator UnloadCanonicalScene()
        {
            if (_testProfile != null)
            {
                UnityEngine.Object.Destroy(_testProfile);
                _testProfile = null;
                yield return null;
            }
            Scene scene = SceneManager.GetSceneByPath(CanonicalScenePath);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

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
                UnityEngine.Object.Destroy(root);
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
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator CanonicalRigRuntimeTogglePreservesStateAndRestoresLegacyGraph()
        {
            yield return SceneManager.LoadSceneAsync(
                CanonicalScenePath,
                LoadSceneMode.Additive);
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(CanonicalScenePath);
            Component rigBuilder = FindNonEmptyRigBuilder(scene);
            Assert.That(rigBuilder, Is.Not.Null,
                "The canonical character must exercise a real, non-empty RigBuilder layer.");
            var rigBehaviour = (Behaviour)rigBuilder;
            Animator animator = rigBuilder.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            EarthAnimationGraph graph = animator.GetComponent<EarthAnimationGraph>();
            if (graph == null)
                graph = animator.gameObject.AddComponent<EarthAnimationGraph>();
            Assert.That(graph, Is.Not.Null,
                "The test must explicitly install the optional graph on the canonical Animator.");
            _testProfile = CreateEnabledTestProfile();

            IsolateCanonicalCharacter(scene, animator.transform.root);
            DisableCompetingPresentationBehaviours(animator.gameObject, graph, rigBehaviour);
            animator.speed = 0f;
            StabilizeAnimator(animator);
            PlayableGraph firstLegacyGraph = ReadRigGraph(rigBuilder);
            Assert.That(rigBehaviour.enabled, Is.True);
            Assert.That(firstLegacyGraph.IsValid(), Is.True);

            AnimatorStateInfo legacyState = animator.GetCurrentAnimatorStateInfo(0);
            float legacyTime = Mathf.Repeat(legacyState.normalizedTime, 1f);
            Transform hips = animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Hips)
                : null;
            Vector3 legacyHipsPosition = hips != null ? hips.localPosition : Vector3.zero;
            Quaternion legacyHipsRotation = hips != null ? hips.localRotation : Quaternion.identity;
            float[] legacyLayerWeights = CaptureAnimatorLayerWeights(animator);

            Assert.That(graph.Configure(animator, _testProfile), Is.True,
                "The canonical scene intentionally has no enabled production profile; " +
                "this test profile must opt the graph in explicitly.");
            EarthAnimationGraphDiagnostics active = graph.Diagnostics;
            Assert.That(active.GraphValid, Is.True);
            Assert.That(active.ControllerPlayableValid, Is.True);
            Assert.That(active.AnimationScriptPlayableValid, Is.True);
            Assert.That(active.TopologyValid, Is.True);
            Assert.That(active.RigLayersAppended, Is.True);
            Assert.That(active.RigOutputCount, Is.GreaterThan(0));
            Assert.That(active.RigOutputsUsePreviousInputs, Is.True,
                "Every downstream rig output must sort after the base output and consume PreviousInputs.");
            Assert.That(rigBehaviour.enabled, Is.False);
            Assert.That(firstLegacyGraph.IsValid(), Is.False,
                "Enabling the external graph must destroy the previous RigBuilder-owned graph.");
            AssertPlayableParametersMatchAnimator(graph, animator);
            AssertPlayableLayerWeights(graph, legacyLayerWeights);

            yield return null;
            AssertStateEquivalent(legacyState, legacyTime, graph.GetCurrentAnimatorStateInfo(0));
            AssertPoseEquivalent(hips, legacyHipsPosition, legacyHipsRotation);

            const int evidenceFrameCount = EarthAnimationGraph.CaptureFrameCapacity;
            int globalGcFramesOverZero = 0;
            long globalMaximumGcBytes = 0L;
            int graphMarkerFrames = 0;
            ProfilerRecorderOptions recorderOptions =
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached |
                ProfilerRecorderOptions.SumAllSamplesInFrame;
            var graphUpdateRecorder = new ProfilerRecorder(
                "Elemental.Character.AnimationGraph",
                1,
                recorderOptions);
            Assert.That(graphUpdateRecorder.Valid, Is.True,
                "The active graph profiler marker must be available to Gate-1 capture.");
            try
            {
                graphUpdateRecorder.Start();
                // A null yield advances one complete player loop in both headed
                // and batchmode test runs. WaitForEndOfFrame is not scheduled in
                // batchmode and therefore cannot be used for Gate-1 evidence.
                yield return null;
                _ = graphUpdateRecorder.LastValue;
                graph.ResetHotPathEvidence();
                long gcWindowStart = GC.GetAllocatedBytesForCurrentThread();
                for (int frame = 0; frame < evidenceFrameCount; frame++)
                {
                    yield return null;
                    long gcWindowEnd = GC.GetAllocatedBytesForCurrentThread();
                    long frameAllocatedBytes = Math.Max(0L, gcWindowEnd - gcWindowStart);
                    if (frameAllocatedBytes > 0L) globalGcFramesOverZero++;
                    globalMaximumGcBytes = Math.Max(
                        globalMaximumGcBytes,
                        frameAllocatedBytes);
                    if (graphUpdateRecorder.LastValue > 0L) graphMarkerFrames++;
                    gcWindowStart = GC.GetAllocatedBytesForCurrentThread();
                }
            }
            finally
            {
                graphUpdateRecorder.Dispose();
            }

            EarthAnimationGraphHotPathEvidence hotPath = graph.HotPathEvidence;
            Assert.That(hotPath.ActiveUpdateCount, Is.EqualTo((uint)evidenceFrameCount));
            Assert.That(hotPath.JobEvaluationCount, Is.EqualTo((uint)evidenceFrameCount),
                "AnimationScriptPlayable must execute once for every active evidence frame.");
            Assert.That(hotPath.RigSyncCount, Is.EqualTo((uint)evidenceFrameCount),
                "The appended RigBuilder layers must synchronize every active evidence frame.");
            Assert.That(hotPath.AllocationSampleCount, Is.EqualTo(evidenceFrameCount));
            Assert.That(hotPath.AllocationFramesOverZero, Is.Zero);
            Assert.That(hotPath.TotalManagedAllocationBytes, Is.Zero);
            Assert.That(hotPath.MaximumManagedAllocationBytes, Is.Zero);
            Assert.That(graphMarkerFrames, Is.EqualTo(evidenceFrameCount));
            Assert.That(globalGcFramesOverZero, Is.Zero,
                $"The isolated active graph allocated in {globalGcFramesOverZero}/" +
                $"{evidenceFrameCount} frames; maximum={globalMaximumGcBytes} bytes.");
            EarthAnimationGraphCaptureSummary activeSummary = graph.GetCaptureSummary();
            Assert.That(activeSummary.SampleCount, Is.EqualTo(evidenceFrameCount));
            Assert.That(activeSummary.GraphActiveFrames, Is.EqualTo(evidenceFrameCount));
            Assert.That(activeSummary.TopologyFailureFrames, Is.Zero);

            SetDistinctPlayableParameters(graph, animator);
            SetDistinctPlayableLayerWeights(graph, animator.layerCount);
            AnimatorStateInfo activeState = graph.GetCurrentAnimatorStateInfo(0);
            float activeTime = Mathf.Repeat(activeState.normalizedTime, 1f);
            uint handoffsBeforeDisable = graph.Diagnostics.StateHandoffCount;
            EarthAnimationGraphSettings disabledSettings = EarthAnimationGraphSettings.Disabled;
            Assert.That(graph.Configure(animator, in disabledSettings), Is.False);
            Assert.That(graph.IsActive, Is.False);
            Assert.That(graph.Diagnostics.StateHandoffCount,
                Is.EqualTo(handoffsBeforeDisable + 1));
            Assert.That(rigBehaviour.enabled, Is.True);
            PlayableGraph restoredLegacyGraph = ReadRigGraph(rigBuilder);
            Assert.That(restoredLegacyGraph.IsValid(), Is.True);
            Assert.That(graph.Diagnostics.GraphValid, Is.False);
            Assert.That(graph.Diagnostics.RigOutputCount, Is.Zero);
            AssertStateEquivalent(activeState, activeTime, animator.GetCurrentAnimatorStateInfo(0));
            AssertAnimatorParametersHavePlayableValues(animator);
            AssertAnimatorLayerWeightsHavePlayableValues(animator);
            AssertPoseEquivalent(hips, legacyHipsPosition, legacyHipsRotation);

            Assert.That(graph.Configure(animator, _testProfile), Is.True,
                "A second OFF-to-ON handoff must not retain a stale graph handle.");
            Assert.That(restoredLegacyGraph.IsValid(), Is.False);
            Assert.That(graph.Diagnostics.TopologyValid, Is.True);
            AssertAnimatorParametersHavePlayableValues(graph, animator);
            AssertPlayableLayerWeightsHavePlayableValues(graph, animator.layerCount);
            Assert.That(graph.Configure(animator, in disabledSettings), Is.False);
            Assert.That(ReadRigGraph(rigBuilder).IsValid(), Is.True);

            yield return null;
            Assert.That(graph.CapturedFrameCount, Is.GreaterThan(0));
            var samples = new EarthAnimationGraphCaptureSample[
                EarthAnimationGraph.CaptureFrameCapacity];
            int copied = graph.CopyRecentCaptureSamplesNonAlloc(samples);
            EarthAnimationGraphCaptureSummary summary = graph.GetCaptureSummary();
            Assert.That(copied, Is.EqualTo(graph.CapturedFrameCount));
            Assert.That(summary.SampleCount, Is.EqualTo(copied));
            Assert.That(summary.GraphActiveFrames, Is.GreaterThan(0));
            Assert.That(summary.TopologyFailureFrames, Is.Zero);
            Assert.That(summary.FinalStateHandoffCount, Is.GreaterThanOrEqualTo(2u));

            graph.CopyRecentCaptureSamplesNonAlloc(samples);
            graph.GetCaptureSummary();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1000; index++)
            {
                graph.CopyRecentCaptureSamplesNonAlloc(samples);
                graph.GetCaptureSummary();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero,
                "Gate-1 telemetry reads must remain allocation-free after caller buffer setup.");
        }

        private static Component FindNonEmptyRigBuilder(Scene scene)
        {
            Type type = Type.GetType(
                "UnityEngine.Animations.Rigging.RigBuilder, Unity.Animation.Rigging");
            Assert.That(type, Is.Not.Null);
            PropertyInfo layersProperty = type.GetProperty("layers");
            Assert.That(layersProperty, Is.Not.Null);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Component[] builders = root.GetComponentsInChildren(type, true);
                for (int index = 0; index < builders.Length; index++)
                {
                    var layers = layersProperty.GetValue(builders[index]) as ICollection;
                    if (layers != null && layers.Count > 0) return builders[index];
                }
            }
            return null;
        }

        private static EarthAnimationGraphProfile CreateEnabledTestProfile()
        {
            EarthAnimationGraphProfile profile =
                ScriptableObject.CreateInstance<EarthAnimationGraphProfile>();
            FieldInfo graphFlag = typeof(EarthAnimationGraphProfile).GetField(
                "usePlayablesAnimationGraph",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo inertiaFlag = typeof(EarthAnimationGraphProfile).GetField(
                "usePoseInertialization",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(graphFlag, Is.Not.Null);
            Assert.That(inertiaFlag, Is.Not.Null);
            graphFlag.SetValue(profile, true);
            inertiaFlag.SetValue(profile, false);
            Assert.That(profile.UsePlayablesAnimationGraph, Is.True);
            Assert.That(profile.UsePoseInertialization, Is.False);
            return profile;
        }

        private static void IsolateCanonicalCharacter(Scene scene, Transform keptRoot)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.transform != keptRoot) root.SetActive(false);
        }

        private static PlayableGraph ReadRigGraph(Component rigBuilder)
        {
            PropertyInfo graphProperty = rigBuilder.GetType().GetProperty("graph");
            Assert.That(graphProperty, Is.Not.Null);
            return (PlayableGraph)graphProperty.GetValue(rigBuilder);
        }

        private static void DisableCompetingPresentationBehaviours(
            GameObject owner,
            EarthAnimationGraph graph,
            Behaviour rigBuilder)
        {
            MonoBehaviour[] behaviours =
                owner.transform.root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour == graph || behaviour == rigBuilder) continue;
                string behaviourNamespace = behaviour.GetType().Namespace;
                if (behaviourNamespace != null &&
                    behaviourNamespace.StartsWith("UnityEngine.Animations.Rigging",
                        StringComparison.Ordinal))
                    continue;
                behaviour.enabled = false;
            }
        }

        private static void StabilizeAnimator(Animator animator)
        {
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                if (state.fullPathHash != 0)
                    animator.Play(state.fullPathHash, layer, state.normalizedTime);
            }
            animator.Update(0f);
            for (int layer = 0; layer < animator.layerCount; layer++)
                Assert.That(animator.IsInTransition(layer), Is.False);
        }

        private static float[] CaptureAnimatorLayerWeights(Animator animator)
        {
            var weights = new float[animator.layerCount];
            for (int layer = 0; layer < weights.Length; layer++)
            {
                weights[layer] = layer == 0 ? animator.GetLayerWeight(layer) : 0.23f + layer * 0.11f;
                animator.SetLayerWeight(layer, weights[layer]);
            }
            return weights;
        }

        private static void AssertPlayableParametersMatchAnimator(
            EarthAnimationGraph graph,
            Animator animator)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                AnimatorControllerParameter parameter = parameters[index];
                int hash = parameter.nameHash;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        Assert.That(graph.ControllerPlayable.GetFloat(hash),
                            Is.EqualTo(animator.GetFloat(hash)).Within(0.0001f));
                        break;
                    case AnimatorControllerParameterType.Int:
                        Assert.That(graph.ControllerPlayable.GetInteger(hash),
                            Is.EqualTo(animator.GetInteger(hash)));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        Assert.That(graph.ControllerPlayable.GetBool(hash),
                            Is.EqualTo(animator.GetBool(hash)));
                        break;
                }
            }
        }

        private static void SetDistinctPlayableParameters(
            EarthAnimationGraph graph,
            Animator animator)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                AnimatorControllerParameter parameter = parameters[index];
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        graph.ControllerPlayable.SetFloat(parameter.nameHash, 0.61f + index * 0.007f);
                        break;
                    case AnimatorControllerParameterType.Int:
                        graph.ControllerPlayable.SetInteger(parameter.nameHash, 300 + index);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        graph.ControllerPlayable.SetBool(parameter.nameHash, (index & 1) != 0);
                        break;
                }
            }
        }

        private static void AssertAnimatorParametersHavePlayableValues(Animator animator)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                AnimatorControllerParameter parameter = parameters[index];
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        Assert.That(animator.GetFloat(parameter.nameHash),
                            Is.EqualTo(0.61f + index * 0.007f).Within(0.0001f));
                        break;
                    case AnimatorControllerParameterType.Int:
                        Assert.That(animator.GetInteger(parameter.nameHash), Is.EqualTo(300 + index));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        Assert.That(animator.GetBool(parameter.nameHash), Is.EqualTo((index & 1) != 0));
                        break;
                }
            }
        }

        private static void AssertAnimatorParametersHavePlayableValues(
            EarthAnimationGraph graph,
            Animator animator)
        {
            AssertPlayableParametersMatchAnimator(graph, animator);
            AssertAnimatorParametersHavePlayableValues(animator);
        }

        private static void AssertPlayableLayerWeights(
            EarthAnimationGraph graph,
            float[] expected)
        {
            for (int layer = 0; layer < expected.Length; layer++)
                Assert.That(graph.ControllerPlayable.GetLayerWeight(layer),
                    Is.EqualTo(expected[layer]).Within(0.0001f));
        }

        private static void SetDistinctPlayableLayerWeights(
            EarthAnimationGraph graph,
            int layerCount)
        {
            for (int layer = 0; layer < layerCount; layer++)
                graph.ControllerPlayable.SetLayerWeight(layer, layer == 0 ? 1f : 0.41f + layer * 0.07f);
        }

        private static void AssertAnimatorLayerWeightsHavePlayableValues(Animator animator)
        {
            for (int layer = 0; layer < animator.layerCount; layer++)
                Assert.That(animator.GetLayerWeight(layer),
                    Is.EqualTo(layer == 0 ? 1f : 0.41f + layer * 0.07f).Within(0.0001f));
        }

        private static void AssertPlayableLayerWeightsHavePlayableValues(
            EarthAnimationGraph graph,
            int layerCount)
        {
            for (int layer = 0; layer < layerCount; layer++)
                Assert.That(graph.ControllerPlayable.GetLayerWeight(layer),
                    Is.EqualTo(layer == 0 ? 1f : 0.41f + layer * 0.07f).Within(0.0001f));
        }

        private static void AssertStateEquivalent(
            AnimatorStateInfo expected,
            float expectedNormalizedTime,
            AnimatorStateInfo actual)
        {
            Assert.That(actual.fullPathHash, Is.EqualTo(expected.fullPathHash));
            Assert.That(Mathf.Repeat(actual.normalizedTime, 1f),
                Is.EqualTo(expectedNormalizedTime).Within(0.015f));
        }

        private static void AssertPoseEquivalent(
            Transform hips,
            Vector3 expectedPosition,
            Quaternion expectedRotation)
        {
            if (hips == null) return;
            Assert.That(Vector3.Distance(hips.localPosition, expectedPosition), Is.LessThan(0.02f));
            Assert.That(Quaternion.Angle(hips.localRotation, expectedRotation), Is.LessThan(2f));
        }
    }
}
