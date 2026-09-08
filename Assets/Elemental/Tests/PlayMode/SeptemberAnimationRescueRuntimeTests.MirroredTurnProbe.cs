using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Presentation.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [Serializable] private sealed class RawTurnSample
        {
            public string actor, controller, avatar;
            public float direction, requestedPhase, evaluatedPhase;
            public Vector3 leftFootLocal, rightFootLocal;
            public float leftContact, rightContact, leftPhase, rightPhase;
            public float resolvedLeftContact, resolvedRightContact, resolvedLeftPhase, resolvedRightPhase;
        }

        [Serializable] private sealed class RawTurnReport
        {
            public string utc;
            public string scope = "Raw production controller/avatar on transform-only diagnostic skeleton; no EAMM, contact IK, physics or gameplay owner.";
            public RawTurnSample[] samples;
            public string[] confirmedMismatches;
            public string[] resolvedChannelFailures;
            public int asymmetricMirroredPosePairs;
        }

        [UnityTest]
        public IEnumerator ProductionMirroredTurnRawPoseAndNamedContactCurvesProbe()
        {
            var samples = new List<RawTurnSample>();
            var confirmed = new List<string>();
            var resolvedFailures = new List<string>();
            int mirroredPairs = 0;
            int turnState = Animator.StringToHash("Base Layer.Turn In Place");
            foreach (Actor actor in _actors)
            {
                Animator source = actor.Presentation.GetComponent<Animator>();
                Assert.That(source, Is.Not.Null);
                Assert.That(source.avatar, Is.Not.Null);
                Assert.That(source.runtimeAnimatorController, Is.Not.Null);
                // Copy transforms only. Instantiating the actor would awaken gameplay,
                // ragdoll and EAMM owners and contaminate the controller measurement.
                GameObject clone = CopyRawTurnSkeleton(source.transform, null);
                clone.name = source.name + " Raw Turn Diagnostic";
                clone.SetActive(false);
                SceneManager.MoveGameObjectToScene(clone, _scene);
                clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                clone.transform.localScale = source.transform.lossyScale;
                _diagnosticClones.Add(clone);
                var animator = clone.AddComponent<Animator>();
                animator.avatar = source.avatar;
                animator.runtimeAnimatorController = source.runtimeAnimatorController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                clone.SetActive(true);
                animator.Rebind();
                animator.Update(0f);
                Assert.That(animator.isHuman && animator.isInitialized, Is.True);
                Assert.That(animator.HasState(0, turnState), Is.True);
                for (int layer = 1; layer < animator.layerCount; layer++) animator.SetLayerWeight(layer, 0f);
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Assert.That(left, Is.Not.Null); Assert.That(right, Is.Not.Null);
                for (int phaseIndex = 1; phaseIndex < 20; phaseIndex++)
                {
                    float phase = phaseIndex / 20f;
                    RawTurnSample negative = SampleRawTurn(animator, left, right, -1f, phase, turnState, source.name);
                    RawTurnSample positive = SampleRawTurn(animator, left, right, 1f, phase, turnState, source.name);
                    samples.Add(negative); samples.Add(positive);
                    bool resolvedCorrectly =
                        Mathf.Abs(positive.resolvedLeftContact - Mathf.Clamp01(negative.rightContact)) < .005f &&
                        Mathf.Abs(positive.resolvedRightContact - Mathf.Clamp01(negative.leftContact)) < .005f &&
                        Mathf.Abs(positive.resolvedLeftPhase - Mathf.Repeat(negative.rightPhase, 1f)) < .005f &&
                        Mathf.Abs(positive.resolvedRightPhase - Mathf.Repeat(negative.leftPhase, 1f)) < .005f &&
                        Mathf.Abs(negative.resolvedLeftContact - Mathf.Clamp01(negative.leftContact)) < .005f &&
                        Mathf.Abs(negative.resolvedRightContact - Mathf.Clamp01(negative.rightContact)) < .005f;
                    if (!resolvedCorrectly) resolvedFailures.Add($"{source.name}, phase {phase:F2}: production foot-channel reader did not preserve original/map mirrored channels.");
                    float negativeHeightDifference = negative.leftFootLocal.y - negative.rightFootLocal.y;
                    float positiveHeightDifference = positive.leftFootLocal.y - positive.rightFootLocal.y;
                    // Diagnose only a directly observed asymmetric pose swap. This
                    // does not assume that Unity mirrors arbitrary named parameters.
                    bool observedPoseSwap = Mathf.Abs(negativeHeightDifference) > .025f &&
                        Mathf.Abs(positiveHeightDifference) > .025f &&
                        negativeHeightDifference * positiveHeightDifference < 0f &&
                        Mathf.Abs(negativeHeightDifference + positiveHeightDifference) < .015f;
                    if (!observedPoseSwap) continue;
                    mirroredPairs++;
                    bool contactsAsymmetric = Mathf.Abs(negative.leftContact - negative.rightContact) > .15f;
                    bool namedContactsUnchanged = Mathf.Abs(negative.leftContact - positive.leftContact) < .005f &&
                        Mathf.Abs(negative.rightContact - positive.rightContact) < .005f;
                    if (contactsAsymmetric && namedContactsUnchanged)
                        confirmed.Add($"{source.name}, phase {phase:F2}: physical foot heights swapped " +
                            $"({negativeHeightDifference:F4}/{positiveHeightDifference:F4}m), but named contact curves stayed " +
                            $"L {negative.leftContact:F4}/{positive.leftContact:F4}, R {negative.rightContact:F4}/{positive.rightContact:F4}.");
                }
                clone.SetActive(false);
            }
            string directory = "BuildReports/GroundingAnimationFollowup";
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "RawMirroredTurnProbe.json"), JsonUtility.ToJson(
                new RawTurnReport { utc = DateTime.UtcNow.ToString("O"), samples = samples.ToArray(),
                    confirmedMismatches = confirmed.ToArray(), asymmetricMirroredPosePairs = mirroredPairs,
                    resolvedChannelFailures = resolvedFailures.ToArray() }, true));
            Debug.Log($"[Raw mirrored turn probe] {samples.Count} samples, {mirroredPairs} observed asymmetric pose swaps, " +
                $"{confirmed.Count} confirmed unchanged-contact mismatches. Full foot positions and four parameters: {directory}/RawMirroredTurnProbe.json");
            // The retained BeforeFix report proved this engine/controller fact.
            // Acceptance is now the production reader correcting that raw fact.
            Assert.That(confirmed.Count, Is.GreaterThan(0), "Probe did not reproduce the known raw named-channel mismatch.");
            Assert.That(resolvedFailures, Is.Empty, string.Join("\n", resolvedFailures));
            yield return null;
        }

        private static RawTurnSample SampleRawTurn(Animator animator, Transform left, Transform right,
            float direction, float phase, int state, string actor)
        {
            animator.SetFloat("Turn", direction);
            animator.Play(state, 0, phase);
            // Two zero-time evaluations settle the BlendTree parameter without
            // advancing the sampled phase or introducing a frame-rate dependency.
            animator.Update(0f); animator.Update(0f);
            AnimatorStateInfo evaluated = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(evaluated.fullPathHash, Is.EqualTo(state));
            Assert.That(Mathf.Repeat(evaluated.normalizedTime, 1f), Is.EqualTo(phase).Within(.002f));
            var sample = new RawTurnSample { actor = actor, controller = animator.runtimeAnimatorController.name,
                avatar = animator.avatar.name, direction = direction, requestedPhase = phase,
                evaluatedPhase = evaluated.normalizedTime,
                leftFootLocal = animator.transform.InverseTransformPoint(left.position),
                rightFootLocal = animator.transform.InverseTransformPoint(right.position),
                leftContact = animator.GetFloat("LeftFootContact"), rightContact = animator.GetFloat("RightFootContact"),
                leftPhase = animator.GetFloat("LeftFootPhase"), rightPhase = animator.GetFloat("RightFootPhase") };
            EarthFootContactController.ReadAuthoredFootChannels(animator, null,
                out sample.resolvedLeftPhase, out sample.resolvedRightPhase,
                out sample.resolvedLeftContact, out sample.resolvedRightContact);
            Debug.Log($"[Raw turn] {actor} sign={direction:F0}, phase={phase:F2}, " +
                $"feet L={sample.leftFootLocal:F4} R={sample.rightFootLocal:F4}, " +
                $"contact L/R={sample.leftContact:F4}/{sample.rightContact:F4}, phase L/R={sample.leftPhase:F4}/{sample.rightPhase:F4}");
            return sample;
        }

        private static GameObject CopyRawTurnSkeleton(Transform source, Transform parent)
        {
            var clone = new GameObject(source.name);
            clone.transform.SetParent(parent, false);
            clone.transform.localPosition = source.localPosition;
            clone.transform.localRotation = source.localRotation;
            clone.transform.localScale = source.localScale;
            for (int index = 0; index < source.childCount; index++)
                CopyRawTurnSkeleton(source.GetChild(index), clone.transform);
            return clone;
        }
    }
}
