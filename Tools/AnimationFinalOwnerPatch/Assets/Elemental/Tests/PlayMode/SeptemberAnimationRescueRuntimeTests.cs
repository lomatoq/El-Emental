using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Presentation.MotionMatching;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class SeptemberAnimationRescueRuntimeTests
    {
        private Scene _scene;
        private bool _loaded;
        private readonly List<Actor> _actors = new();
        private readonly List<EarthAnimationPoseSample> _samples = new();
        private readonly WaitForEndOfFrame _frame = new();
        private readonly List<UnityEngine.Object> _diagnosticClones = new();

        [UnitySetUp]
        public IEnumerator Load()
        {
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Assert.That(SceneManager.GetSceneByPath(path).isLoaded, Is.False, "Use the focused launcher so production scene can be restored safely.");
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(path); _loaded = true;
            var readinessGates = new List<EarthSceneReadinessGate>();
            foreach (var root in _scene.GetRootGameObjects())
                readinessGates.AddRange(root.GetComponentsInChildren<EarthSceneReadinessGate>(true));
            Assert.That(readinessGates.Count, Is.GreaterThan(0), "Production scene must expose its readiness boundary.");
            double readinessDeadline = Time.realtimeSinceStartupAsDouble + 125d;
            foreach (var gate in readinessGates)
            {
                while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < readinessDeadline)
                    yield return null;
                Assert.That(gate.IsReady, Is.True, $"Scene readiness failed or timed out: {gate.Status}.");
            }
            // The gate restores controls when ready; install deterministic input only afterward.
            foreach (var root in _scene.GetRootGameObjects())
            {
                foreach (var duel in root.GetComponentsInChildren<EarthMvpDuelController>()) duel.enabled = false;
                foreach (var bot in root.GetComponentsInChildren<EarthMvpBotController>()) bot.enabled = false;
                foreach (var impact in root.GetComponentsInChildren<EarthCharacterImpactTarget>()) impact.SuppressImpacts(120f);
                foreach (var presentation in root.GetComponentsInChildren<HumanoidCharacterPresentation>())
                {
                    var motor = presentation.GetComponentInParent<PlanetMotor>();
                    if (motor == null) continue;
                    var input = motor.gameObject.AddComponent<AnimationRescueInput>();
                    motor.ConfigureInputSource(input);
                    _actors.Add(new Actor { Presentation = presentation, Input = input,
                        Probe = presentation.gameObject.AddComponent<EarthAnimationPoseProbe>(),
                        Bridge = presentation.GetComponent<EAMMBasePoseBridge>() });
                }
            }
            Assert.That(_actors.Count, Is.EqualTo(2));
            yield return new WaitForSeconds(1.5f);
        }

        [UnityTest]
        public IEnumerator HeadAndFeetAreMeasuredAcrossAuthoredEammContactsAndAdditiveStages()
        {
            FieldInfo profileField = typeof(EAMMBasePoseBridge).GetField("profile", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo weightField = typeof(EAMMRuntimeProfile).GetField("basePoseWeight", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(profileField, Is.Not.Null); Assert.That(weightField, Is.Not.Null);
            foreach (Actor actor in _actors)
            {
                var copy = UnityEngine.Object.Instantiate((EAMMRuntimeProfile)profileField.GetValue(actor.Bridge));
                _diagnosticClones.Add(copy);
                profileField.SetValue(actor.Bridge, copy);
            }
            for (int stage = 0; stage < 4; stage++)
            {
                foreach (Actor actor in _actors)
                {
                    weightField.SetValue(profileField.GetValue(actor.Bridge), stage == 0 ? 0f : 1f);
                    actor.Presentation.FootContactController.enabled = stage >= 2;
                    var idle = actor.Presentation.GetComponent<HumanoidOrganicIdle>();
                    var body = actor.Presentation.GetComponent<HumanoidProceduralBodyResponse>();
                    if (idle != null) idle.enabled = stage >= 3;
                    if (body != null) body.enabled = stage >= 3;
                    actor.Probe.Scenario = new[] { "authored", "eamm", "eamm+contacts", "full-production" }[stage];
                }
                yield return new WaitForSeconds(0.5f);
                float until = Time.time + 0.5f;
                while (Time.time < until)
                {
                    yield return _frame;
                    foreach (Actor actor in _actors)
                    {
                        var sample = actor.Probe.Latest; _samples.Add(sample);
                        Assert.That(sample.headHeight, Is.GreaterThan(0.25f), $"{sample.actor}/{sample.scenario}: folded upper body.");
                        Assert.That(Mathf.Abs(sample.headPitchDegrees), Is.LessThan(65f), $"{sample.actor}/{sample.scenario}: head points upward.");
                        Assert.That(sample.weightedContactPasses, Is.LessThanOrEqualTo(sample.finalGraphEvaluations));
                        if (stage >= 2)
                        {
                            Assert.That(sample.contactFrame, Is.EqualTo(sample.frame));
                            Assert.That(sample.weightedContactPasses, Is.GreaterThan(0), "No final weighted OnAnimatorIK contact pass submitted goals.");
                        }
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator ProductionIdleTurnsAndStopsKeepFinalPoseAndAuthoredTurnOwnership()
        {
            yield return Observe(0.6f, true, false);
            foreach (float direction in new[] { -1f, 1f })
            {
                foreach (Actor actor in _actors) actor.Input.Move = new float2(direction, 0f);
                yield return new WaitForSeconds(0.25f);
                yield return Observe(0.5f, true, true);
                foreach (Actor actor in _actors) actor.Input.Move = new float2(0f, direction);
                yield return new WaitForSeconds(0.6f);
                foreach (Actor actor in _actors) actor.Input.Move = float2.zero;
                yield return new WaitForSeconds(0.5f);
                yield return Observe(0.7f, true, false);
            }
        }

        [UnityTest]
        public IEnumerator AllElevenAcceptedTechniquesAdvanceVisibleMotionAndReturnToEamm()
        {
            Actor player = _actors.Find(a => a.Presentation.GetComponent<EarthCharacterPoseController>() != null);
            Assert.That(player, Is.Not.Null);
            var pose = player.Presentation.GetComponent<EarthCharacterPoseController>();
            MethodInfo begin = typeof(EarthCharacterPoseController).GetMethod("BeginAuthoritative", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(begin, Is.Not.Null);
            Transform hand = player.Presentation.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
            var techniques = new[] { EarthTechniqueId.RaiseWall, EarthTechniqueId.RaisePlatform, EarthTechniqueId.PullStone,
                EarthTechniqueId.ThrowStone, EarthTechniqueId.VectorPush, EarthTechniqueId.Repair,
                EarthTechniqueId.Resonance, EarthTechniqueId.PillarJump, EarthTechniqueId.Armor,
                EarthTechniqueId.ArmorBarrage, EarthTechniqueId.MeteorFinish };
            foreach (EarthTechniqueId technique in techniques)
            {
            Vector3 previous = player.Presentation.transform.InverseTransformPoint(hand.position);
            begin.Invoke(pose, new object[] { EarthTechniqueKind.Wall, technique, pose.PresentationTick,
                player.Presentation.transform.position + player.Presentation.transform.forward * 3f, 80f, 4f });
            float maxMotion = 0f, minTime = 1f, maxTime = 0f;
            bool castSeen = false;
            float until = Time.time + 1.2f;
            while (Time.time < until)
            {
                yield return _frame;
                EarthAnimationPoseSample sample = player.Probe.Latest;
                _samples.Add(sample);
                if (player.Presentation.CurrentAuthoredAction == EarthAuthoredActionId.MagicCast)
                {
                    castSeen = true;
                    Assert.That(sample.eammWeight, Is.Zero, "EAMM cannot replace a magic pose.");
                    minTime = Mathf.Min(minTime, sample.magicSampleTime); maxTime = Mathf.Max(maxTime, sample.magicSampleTime);
                    maxMotion = Mathf.Max(maxMotion, Vector3.Distance(previous,
                        player.Presentation.transform.InverseTransformPoint(hand.position)));
                }
            }
            Assert.That(castSeen, Is.True, "Accepted event did not enter the cast lane.");
            Assert.That(maxTime - minTime, Is.GreaterThan(0.08f), "Cast sampling is frozen.");
            Assert.That(maxMotion, Is.GreaterThan(0.025f), "The visible hand never left its idle pose.");
            yield return new WaitForSeconds(0.5f);
            Assert.That(player.Bridge.RuntimeStatus, Is.EqualTo(EAMMRuntimeStatus.Active), "Authored compression must not reject EAMM permanently.");
            Debug.Log($"[September Animation] {technique}: accepted cast hand travel={maxMotion:F4}m, sample range={minTime:F3}..{maxTime:F3}.");
            }
        }

        private IEnumerator Observe(float seconds, bool upright, bool turn)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return _frame;
                foreach (Actor actor in _actors)
                {
                    var sample = actor.Probe.Latest;
                    _samples.Add(sample);
                    Assert.That(float.IsFinite(sample.headHeight), Is.True);
                    if (upright)
                    {
                        Assert.That(sample.headHeight, Is.GreaterThan(0.25f), $"{sample.actor}: head compressed into pelvis.");
                        Assert.That(Mathf.Abs(sample.headPitchDegrees), Is.LessThan(65f), $"{sample.actor}: head faces vertically.");
                    }
                    Assert.That(sample.contactFrame, Is.EqualTo(sample.frame), "Final contact telemetry is from an older render frame.");
                    Assert.That(sample.weightedContactPasses, Is.LessThanOrEqualTo(sample.finalGraphEvaluations),
                        "Contact state may advance only once per graph evaluation.");
                    if (sample.leftContactWeight > 0.8f) Assert.That(sample.leftFootError, Is.LessThan(0.18f), $"{sample.actor}: final left contact overwritten.");
                    if (sample.rightContactWeight > 0.8f) Assert.That(sample.rightFootError, Is.LessThan(0.18f), $"{sample.actor}: final right contact overwritten.");
                    if (turn)
                    {
                        Assert.That(sample.authoredTurn, Is.True, "Turn input never reached the authored turn state.");
                        Assert.That(sample.eammWeight, Is.Zero, "Authored turn was overwritten by EAMM.");
                    }
                }
            }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Directory.CreateDirectory("BuildReports/SeptemberAnimation");
            File.WriteAllText("BuildReports/SeptemberAnimation/FinalPose.json", JsonUtility.ToJson(
                new Report { utc = DateTime.UtcNow.ToString("O"), samples = _samples.ToArray() }, true));
            File.WriteAllText("BuildReports/SeptemberAnimation/" + TestContext.CurrentContext.Test.Name + ".json", JsonUtility.ToJson(
                new Report { utc = DateTime.UtcNow.ToString("O"), samples = _samples.ToArray() }, true));
            if (_loaded && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            foreach (var clone in _diagnosticClones) if (clone != null) UnityEngine.Object.Destroy(clone);
            _diagnosticClones.Clear();
            _loaded = false; _actors.Clear(); _samples.Clear();
        }
        [Serializable] private sealed class Report { public string utc; public EarthAnimationPoseSample[] samples; }
        private sealed class Actor
        {
            public HumanoidCharacterPresentation Presentation;
            public AnimationRescueInput Input;
            public EarthAnimationPoseProbe Probe;
            public EAMMBasePoseBridge Bridge;
        }
        private sealed class AnimationRescueInput : MonoBehaviour, IPlanetMotorInputSource
        {
            public float2 Move;
            public PlanetMotorCommand SampleCommand(uint tick) => new PlanetMotorCommand(tick, Move, false);
        }
    }
}
