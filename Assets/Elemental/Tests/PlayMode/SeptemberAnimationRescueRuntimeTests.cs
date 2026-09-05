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
    public sealed partial class SeptemberAnimationRescueRuntimeTests
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
        public IEnumerator ProductionMantleOwnsPoseReleasesFeetAndReturnsToContacts()
        {
            yield return ObserveProductionMantle(false);
        }

        [UnityTest]
        public IEnumerator ProductionMantleNativeHandFallbackUsesBaseLayer()
        {
            yield return ObserveProductionMantle(true);
        }

        private IEnumerator ObserveProductionMantle(bool nativeHandFallback)
        {
            Actor actor=_actors.Find(a=>a.Presentation.GetComponent<EarthCharacterPoseController>()!=null);
            Assert.That(actor,Is.Not.Null);
            PlanetMotor motor=actor.Presentation.GetComponentInParent<PlanetMotor>();
            Animator animator=actor.Presentation.GetComponent<Animator>();
            EarthAnimationDriver driver=actor.Presentation.GetComponent<EarthAnimationDriver>();
            if(nativeHandFallback)
            {
                var rig=actor.Presentation.GetComponent<EarthAnimationRigBridge>();
                if(rig!=null) rig.ResetMagicIk();
                var builder=actor.Presentation.GetComponent("RigBuilder") as Behaviour;
                if(builder!=null) builder.enabled=false;
                FieldInfo bridge=typeof(HumanoidCharacterPresentation).GetField("animationRigBridge",BindingFlags.NonPublic|BindingFlags.Instance);
                Assert.That(bridge,Is.Not.Null);
                bridge.SetValue(actor.Presentation,null);
            }
            Vector3 up=motor.LocalUp.normalized;
            Vector3 forward=Vector3.ProjectOnPlane(motor.FacingForward,up).normalized;
            Vector3 feet=motor.SupportFeetPoint(up);
            Vector3 start=motor.transform.position;
            GameObject ledge=GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name="Production mantle animation acceptance ledge";
            SceneManager.MoveGameObjectToScene(ledge,_scene);
            // Broad .9m step in front of the real scene actor; no input bypass,
            // teleport, motor override or forced animation-state entry.
            ledge.transform.SetPositionAndRotation(feet+forward*2.3f+up*.2f,Quaternion.LookRotation(forward,up));
            ledge.transform.localScale=new Vector3(4f,1.4f,3f);
            for(int layer=0;layer<32;layer++)
                if((motor.GroundMask.value&(1<<layer))!=0) { ledge.layer=layer; break; }
            UnityEngine.Physics.SyncTransforms();
            try
            {
                uint sequence=motor.MantleSequence;
                actor.Input.Move=new float2(0f,1f);
                double deadline=Time.realtimeSinceStartupAsDouble+6d;
                while(motor.MantleSequence==sequence&&Time.realtimeSinceStartupAsDouble<deadline) yield return _frame;
                Assert.That(motor.IsMantling,Is.True,"Real forward movement did not start mantle: "+motor.MantleLastRejection);
                actor.Input.Move=float2.zero;
                bool sawRaise=false,sawTransfer=false,sawSettle=false,sawHands=false;
                float closestHand=float.PositiveInfinity;
                deadline=Time.realtimeSinceStartupAsDouble+4d;
                while(motor.IsMantling&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    yield return _frame;
                    if(!motor.IsMantling) break;
                    var sample=actor.Probe.Latest; _samples.Add(sample);
                    Assert.That(actor.Presentation.CurrentAuthoredAction,Is.EqualTo(EarthAuthoredActionId.Mantle));
                    Assert.That(sample.eammWeight,Is.Zero,"EAMM replaced the protected mantle pose.");
                    Assert.That(sample.headHeight,Is.GreaterThan(.25f));
                    if(motor.MantleProgress>.15f)
                        Assert.That(driver.GetCurrentAnimatorStateInfo(0).fullPathHash,Is.EqualTo(Animator.StringToHash("Base Layer.Mantle")));
                    Assert.That(driver.GetFloat(Animator.StringToHash("MantleTime")),Is.EqualTo(motor.MantleProgress).Within(.06f));
                    if(!motor.HasStableSupport)
                    {
                        Assert.That(sample.leftContactWeight,Is.LessThan(.001f));
                        Assert.That(sample.rightContactWeight,Is.LessThan(.001f));
                        Assert.That(sample.grounded,Is.False,"Settle timing cannot fabricate ground contact.");
                    }
                    sawRaise|=motor.MantlePhase==EarthMantlePhase.Raise;
                    sawTransfer|=motor.MantlePhase==EarthMantlePhase.Transfer;
                    sawSettle|=motor.MantlePhase==EarthMantlePhase.Settle;
                    if(motor.MantleProgress>.20f&&motor.MantleProgress<.50f)
                    {
                        Vector3 right=Vector3.Cross(motor.LocalUp,motor.FacingForward).normalized;
                        Vector3 target=motor.MantleLedgePoint+right*.18f+motor.LocalUp*.025f;
                        closestHand=Mathf.Min(closestHand,Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightHand).position,target));
                        sawHands|=actor.Presentation.HandConstraintWeight>.7f;
                    }
                }
                Assert.That(motor.IsMantling,Is.False,"Traversal timed out.");
                Assert.That(motor.MantleLastRejection,Is.Null);
                Assert.That(sawRaise&&sawTransfer&&sawSettle,Is.True,"The complete motor-owned mantle phases must be visible.");
                Assert.That(sawHands,Is.True);
                Assert.That(closestHand,Is.LessThan(.35f),"The authored/fallback wrist never reached the ledge contact region.");
                yield return new WaitForSeconds(1.2f);
                Assert.That(Vector3.Dot(motor.transform.position-start,up),Is.GreaterThan(.55f));
                Assert.That(motor.HasStableSupport,Is.True);
                Assert.That(actor.Presentation.CurrentAuthoredAction,Is.Not.EqualTo(EarthAuthoredActionId.Mantle));
                Assert.That(actor.Bridge.AppliedEammMasterWeight,Is.GreaterThan(.5f));
                EarthFootContactController recoveredFeet = actor.Presentation.FootContactController;
                AnimatorStateInfo recoveredState = driver.GetCurrentAnimatorStateInfo(0);
                AnimatorStateInfo recoveredNext = driver.IsInTransition(0)
                    ? driver.GetNextAnimatorStateInfo(0)
                    : default;
                Assert.That(recoveredFeet.LeftFootIkWeight + recoveredFeet.RightFootIkWeight,
                    Is.GreaterThan(1f),
                    $"Ground contact did not resume after mantle: " +
                    $"L={recoveredFeet.LeftFootIkWeight:F3}/{recoveredFeet.LeftReason}, " +
                    $"R={recoveredFeet.RightFootIkWeight:F3}/{recoveredFeet.RightReason}, " +
                    $"clearance={recoveredFeet.LeftSoleClearance:F3}/{recoveredFeet.RightSoleClearance:F3}, " +
                    $"sourceFeet={actor.Bridge.SourceLeftFootHeight:F3}/{actor.Bridge.SourceRightFootHeight:F3}, " +
                    $"candidateFeet={actor.Bridge.CandidateLeftFootHeight:F3}/{actor.Bridge.CandidateRightFootHeight:F3}, " +
                    $"hasContact={recoveredFeet.LeftHasContact}/{recoveredFeet.RightHasContact}, " +
                    $"actualLocal={motor.transform.InverseTransformPoint(recoveredFeet.LeftActualFootWorld)}/" +
                    $"{motor.transform.InverseTransformPoint(recoveredFeet.RightActualFootWorld)}, " +
                    $"targetLocal={motor.transform.InverseTransformPoint(recoveredFeet.LeftTargetWorld)}/" +
                    $"{motor.transform.InverseTransformPoint(recoveredFeet.RightTargetWorld)}, " +
                    $"stable={motor.HasStableSupport}, policy={actor.Presentation.CurrentFootPolicy}, " +
                    $"EAMM={actor.Bridge.AppliedEammMasterWeight:F3}, query={actor.Bridge.IsLocomotionQuery}, " +
                    $"state={recoveredState.fullPathHash}@{recoveredState.normalizedTime:F2}, " +
                    $"transition={driver.IsInTransition(0)}, next={recoveredNext.fullPathHash}.");
            }
            finally { actor.Input.Move=float2.zero; UnityEngine.Object.Destroy(ledge); }
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
            if (_walkStopFrames.Count > 0)
                File.WriteAllText(
                    "BuildReports/SeptemberAnimation/WalkStopOwnershipDiagnostics.json",
                    JsonUtility.ToJson(new WalkStopDiagnosticReport
                    {
                        utc = DateTime.UtcNow.ToString("O"),
                        frames = _walkStopFrames.ToArray()
                    }, true));
            if (_magicPhaseTraces.Count > 0)
                File.WriteAllText(
                    "BuildReports/SeptemberAnimation/MagicBurstPhaseDiagnostics.json",
                    JsonUtility.ToJson(new MagicPhaseDiagnosticReport
                    {
                        utc = DateTime.UtcNow.ToString("O"),
                        frames = _magicPhaseTraces.ToArray()
                    }, true));
            File.WriteAllText("BuildReports/SeptemberAnimation/FinalPose.json", JsonUtility.ToJson(
                new Report { utc = DateTime.UtcNow.ToString("O"), samples = _samples.ToArray() }, true));
            File.WriteAllText("BuildReports/SeptemberAnimation/" + TestContext.CurrentContext.Test.Name + ".json", JsonUtility.ToJson(
                new Report { utc = DateTime.UtcNow.ToString("O"), samples = _samples.ToArray() }, true));
            if (_loaded && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            foreach (var clone in _diagnosticClones) if (clone != null) UnityEngine.Object.Destroy(clone);
            _diagnosticClones.Clear();
            _loaded = false; _actors.Clear(); _samples.Clear(); _walkStopFrames.Clear();
            _magicPhaseTraces.Clear();
        }
        [Serializable] private sealed class Report { public string utc; public EarthAnimationPoseSample[] samples; }
        [Serializable] private sealed class MagicPhaseDiagnosticReport
        {
            public string utc;
            public MagicPhaseTrace[] frames;
        }
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
