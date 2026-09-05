using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    /// <summary>
    /// Staged under Tools. Copy into Assets/Elemental/Tests/PlayMode before running.
    /// Partial fixture intentionally reuses the production-scene setup and cleanup.
    /// </summary>
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        private readonly List<WalkStopFrameDiagnostic> _walkStopFrames = new();
        private readonly List<MagicPhaseTrace> _magicPhaseTraces = new();

        [UnityTest]
        public IEnumerator RapidBurstKeepsCurrentContactAndLatestRequestWithoutStaleReplay()
        {
            Actor actor = _actors.Find(value =>
                value.Presentation.GetComponent<EarthCharacterPoseController>() != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose = actor.Presentation.PoseController;
            EarthAnimationDriver driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            EarthChoreographyDirector choreography =
                actor.Presentation.GetComponent<EarthChoreographyDirector>();
            EarthFootContactController feet = actor.Presentation.FootContactController;
            Animator animator = actor.Presentation.Animator;
            EarthDualMouseAbilityController dualMouse =
                actor.Presentation.GetComponentInParent<EarthDualMouseAbilityController>();
            Assert.That(dualMouse, Is.Not.Null);
            Assert.That(dualMouse.HasSharedPresentationOwner, Is.True,
                "Shipping LMB/RMB abilities did not yield Animator ownership to the shared presenter.");
            int magicLayer = animator.GetLayerIndex("Earth Magic Upper Body");
            Assert.That(magicLayer, Is.GreaterThanOrEqualTo(0));
            EarthTechniqueId[] techniques =
            {
                EarthTechniqueId.RaiseWall, EarthTechniqueId.RaisePlatform,
                EarthTechniqueId.PullStone, EarthTechniqueId.ThrowStone,
                EarthTechniqueId.VectorPush, EarthTechniqueId.Repair,
                EarthTechniqueId.Resonance, EarthTechniqueId.PillarJump,
                EarthTechniqueId.Armor, EarthTechniqueId.ArmorBarrage,
                EarthTechniqueId.MeteorFinish
            };
            var phaseMasks = new Dictionary<uint, int>(techniques.Length);
            var peakWeights = new Dictionary<uint, float>(techniques.Length);
            void OnPhase(uint sequence, EarthTechniqueId technique, EarthCastPhase phase)
            {
                int bit = 1 << (int)phase;
                phaseMasks.TryGetValue(sequence, out int mask);
                phaseMasks[sequence] = mask | bit;
                _magicPhaseTraces.Add(new MagicPhaseTrace
                {
                    realtime = Time.realtimeSinceStartup,
                    frame = Time.frameCount,
                    sequence = sequence,
                    technique = technique.ToString(),
                    phase = phase.ToString(),
                    queued = pose.QueuedPresentationCount,
                    layerWeight = driver.GetLayerWeight(magicLayer),
                    handWeight = actor.Presentation.HandConstraintWeight
                });
            }

            pose.PresentationPhaseChanged += OnPhase;
            try
            {
                actor.Input.Move = new float2(0f, 1f);
                MethodInfo dualRequest = typeof(EarthDualMouseAbilityController).GetMethod(
                    "RequestPresentation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(dualRequest, Is.Not.Null);
                Vector3 dualTarget = actor.Presentation.transform.position +
                                     actor.Presentation.transform.forward * 3f;
                // Exercise the shipping dual-mouse event ingress twice in one
                // rendered frame without mutating the arena or projectile pools.
                dualRequest.Invoke(dualMouse, new object[]
                {
                    EarthTechniqueId.PillarJump, dualTarget, 80f, 5f
                });
                dualRequest.Invoke(dualMouse, new object[]
                {
                    EarthTechniqueId.GravityGrip, dualTarget, 80f, 8f
                });
                Assert.That(pose.CurrentRequest.Technique, Is.EqualTo(EarthTechniqueId.PillarJump));
                Assert.That(pose.QueuedPresentationCount, Is.EqualTo(1),
                    "Two simultaneous mouse actions did not share the presentation arbiter.");

                for (int index = 0; index < techniques.Length; index++)
                {
                    uint sequence = 0xe0000000u + (uint)index;
                    EarthTechniqueId technique = techniques[index];
                    pose.RequestSemanticPresentation(
                        MagicPresentationSemanticResolver.ResolveKind(technique),
                        technique,
                        sequence,
                        actor.Presentation.transform.position +
                        actor.Presentation.transform.forward * (2.5f + index * .05f),
                        40f + index * 8f,
                        3f + index);
                    // Each adjacent pair shares a rendered frame (the LMB/RMB
                    // overlap case); pairs themselves arrive as a rapid burst.
                    if ((index & 1) != 0) yield return null;
                }
                double lastInputAt = Time.realtimeSinceStartupAsDouble;
                Assert.That(pose.QueuedPresentationCount, Is.EqualTo(1),
                    "Responsive magic may retain only the latest pending request.");
                Assert.That(pose.SupersededPresentationRequests, Is.GreaterThanOrEqualTo(10),
                    "Obsolete rapid requests were left queued for delayed playback.");

                float priorLayer = driver.GetLayerWeight(magicLayer);
                float maximumLayerDropWhileQueued = 0f;
                bool sawMovingMagic = false;
                double deadline = Time.realtimeSinceStartupAsDouble + 18d;
                while ((pose.QueuedPresentationCount > 0 || pose.CurrentRequest.IsActive) &&
                       Time.realtimeSinceStartupAsDouble < deadline)
                {
                    yield return _frame;
                    int slot = (int)EarthHumanoidMotionResolver.Resolve(
                        pose.CurrentRequest.Technique);
                    uint sequence = pose.LastAuthoritativeTick;
                    if (slot > 0)
                    {
                        float weight = driver.GetFloat(Animator.StringToHash($"EarthPose{slot:00}"));
                        peakWeights.TryGetValue(sequence, out float peak);
                        peakWeights[sequence] = Mathf.Max(peak, weight);
                    }
                    float layer = driver.GetLayerWeight(magicLayer);
                    if (pose.QueuedPresentationCount > 0 && priorLayer > .25f)
                        maximumLayerDropWhileQueued = Mathf.Max(
                            maximumLayerDropWhileQueued,
                            priorLayer - layer);
                    priorLayer = layer;
                    sawMovingMagic |= actor.Presentation.FilteredSpeed > .4f &&
                                      actor.Presentation.CurrentAuthoredAction ==
                                      EarthAuthoredActionId.MagicCast;
                    _samples.Add(actor.Probe.Latest);
                }
                actor.Input.Move = float2.zero;
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline),
                    "Queued presentation did not drain.");
                Assert.That(Time.realtimeSinceStartupAsDouble - lastInputAt, Is.LessThan(1.5d),
                    "Magic continued replaying stale gestures long after input release.");
                Assert.That(pose.DroppedPresentationRequests, Is.Zero,
                    "The fixed presentation queue dropped an accepted command.");
                Assert.That(sawMovingMagic, Is.True,
                    "Base locomotion did not continue under the masked magic recovery.");
                Assert.That(maximumLayerDropWhileQueued, Is.LessThan(.22f),
                    "The upper-body layer was cut between queued actions.");

                const int strike = 1 << (int)EarthCastPhase.Strike;
                const int sustain = 1 << (int)EarthCastPhase.Sustain;
                const int recover = 1 << (int)EarthCastPhase.Recover;
                uint latestSequence = 0xe0000000u + (uint)(techniques.Length - 1);
                phaseMasks.TryGetValue(latestSequence, out int latestPhases);
                Assert.That(latestPhases & (strike | sustain | recover),
                    Is.EqualTo(strike | sustain | recover),
                    "The latest request did not reach rendered contact and recovery.");
                peakWeights.TryGetValue(latestSequence, out float latestPeak);
                Assert.That(latestPeak, Is.GreaterThan(.75f),
                    "The latest rapid request never became the visible semantic pose.");
                for (int index = 0; index < techniques.Length - 1; index++)
                {
                    uint staleSequence = 0xe0000000u + (uint)index;
                    Assert.That(phaseMasks.ContainsKey(staleSequence), Is.False,
                        $"Superseded {techniques[index]} replayed after input had moved on.");
                }

                yield return new WaitForSeconds(.75f);
                float residualPoseWeight = 0f;
                for (int slot = 1; slot <= 11; slot++)
                    residualPoseWeight += driver.GetFloat(
                        Animator.StringToHash($"EarthPose{slot:00}"));
                EarthAnimationPoseSample finalPose = actor.Probe.Latest;
                _samples.Add(finalPose);
                Assert.That(driver.GetLayerWeight(magicLayer), Is.LessThan(.05f));
                Assert.That(residualPoseWeight, Is.LessThan(.08f));
                Assert.That(actor.Presentation.HandConstraintWeight, Is.LessThan(.03f));
                Assert.That(choreography.AppliedVisualPose.MaximumAbsDegrees, Is.LessThan(1.5f));
                Assert.That(Mathf.Abs(feet.PelvisOffsetMeters), Is.LessThanOrEqualTo(.225f));
                Assert.That(float.IsFinite(feet.LeftKneeAngleDegrees) &&
                            float.IsFinite(feet.RightKneeAngleDegrees), Is.True);
                Assert.That(finalPose.headHeight, Is.GreaterThan(.25f));
                Assert.That(Mathf.Abs(finalPose.headPitchDegrees), Is.LessThan(65f));
                Assert.That(actor.Presentation.CurrentAuthoredAction,
                    Is.Not.EqualTo(EarthAuthoredActionId.MagicCast));
            }
            finally
            {
                actor.Input.Move = float2.zero;
                pose.PresentationPhaseChanged -= OnPhase;
            }
        }

        [UnityTest]
        public IEnumerator ShippingDualMouseChordStartsPromptlyAndDoesNotReplayAfterRelease()
        {
            yield return ObserveShippingDualMouseChord(false);
        }

        [UnityTest]
        public IEnumerator ShippingDualMouseChordWithoutHandConstraintsIsolatesSourceMotion()
        {
            yield return ObserveShippingDualMouseChord(true);
        }

        [UnityTest]
        public IEnumerator ShippingDualMouseChordIsContinuousAtThirtyHertz()
        {
            yield return ObserveShippingDualMouseChord(false, 30);
        }

        [UnityTest]
        public IEnumerator ShippingDualMouseChordIsContinuousAtSixtyHertz()
        {
            yield return ObserveShippingDualMouseChord(false, 60);
        }

        [UnityTest]
        public IEnumerator ShippingDualMouseChordIsContinuousAtOneTwentyHertz()
        {
            yield return ObserveShippingDualMouseChord(false, 120);
        }

        private IEnumerator ObserveShippingDualMouseChord(
            bool suppressHandConstraints,
            int requestedFrameRate = 0)
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose = actor.Presentation.PoseController;
            EarthActionRouterBehaviour router =
                actor.Presentation.GetComponentInParent<EarthActionRouterBehaviour>();
            EarthDualMouseAbilityController dualMouse =
                actor.Presentation.GetComponentInParent<EarthDualMouseAbilityController>();
            PlayerInput playerInput = actor.Presentation.GetComponentInParent<PlayerInput>();
            EarthAnimationDriver driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            Animator animator = actor.Presentation.Animator;
            Assert.That(router, Is.Not.Null);
            Assert.That(dualMouse, Is.Not.Null);
            Assert.That(playerInput, Is.Not.Null);
            int magicLayer = actor.Presentation.Animator.GetLayerIndex("Earth Magic Upper Body");
            Mouse mouse = CreateMagicBurstMouse(playerInput);
            int maximumPending = 0;
            double releasedAt = 0d;
            double lastReleasedAt = 0d;
            double firstVisibleAt = 0d;
            MagicBoneContinuityRecorder continuity =
                actor.Presentation.gameObject.AddComponent<MagicBoneContinuityRecorder>();
            continuity.Configure(animator, actor.Presentation, pose, driver, magicLayer);
            bool sawPunchSemantic = false;
            double punchAcceptedAt = 0d;
            double punchContactAt = 0d;
            float punchPosePeak = 0f;
            FieldInfo profileField = typeof(HumanoidCharacterPresentation).GetField(
                "magicMotionProfile", BindingFlags.Instance | BindingFlags.NonPublic);
            var motionProfile = profileField?.GetValue(actor.Presentation) as EarthMagicMotionProfile;
            EarthMagicMotionProfile diagnosticProfile = null;
            int previousTargetFrameRate = Application.targetFrameRate;
            int previousVSyncCount = QualitySettings.vSyncCount;
            float previousCaptureDeltaTime = Time.captureDeltaTime;
            if (requestedFrameRate > 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = requestedFrameRate;
                Time.captureDeltaTime = 1f / requestedFrameRate;
            }
            if (suppressHandConstraints)
            {
                Assert.That(motionProfile, Is.Not.Null);
                diagnosticProfile = UnityEngine.Object.Instantiate(motionProfile);
                for (int index = 0; index < diagnosticProfile.motions.Length; index++)
                {
                    EarthMagicMotionEntry entry = diagnosticProfile.motions[index];
                    if (entry == null) continue;
                    entry.actionHandInfluence = 0f;
                    entry.sustainedHandInfluence = 0f;
                }
                profileField.SetValue(actor.Presentation, diagnosticProfile);
            }
            try
            {
                Camera camera = FindAnimationSceneComponent<Camera>(_scene);
                GameObject proxyObject = FindAnimationSceneObject(_scene, "Planet Collision Proxy");
                Collider proxy = proxyObject != null ? proxyObject.GetComponent<Collider>() : null;
                Physics.SyncTransforms();
                Assert.That(TryFindAnimationSurfacePoint(camera, proxy, out Vector2 pointer), Is.True,
                    "Quick Stone physical-input audit could not find a valid terrain pixel.");
                QueueDualMouseState(mouse, pointer, false, false);
                yield return null;
                QueueDualMouseState(mouse, pointer, true, true);
                yield return null;
                QueueDualMouseState(mouse, pointer, false, false);
                releasedAt = Time.realtimeSinceStartupAsDouble;
                lastReleasedAt = releasedAt;
                yield return null;
                Assert.That(
                    dualMouse.IsStompStoneActive ||
                    router.Current.Intent == EarthActionIntentKind.StompStone,
                    Is.True,
                    "Synthetic LMB+RMB did not commit the shipping stomp-stone ability. " +
                    "The router's Current value is intentionally a one-Update edge, so the " +
                    "durable gameplay session is the frame-rate-independent acceptance signal.");

                double visibleDeadline = releasedAt + .20d;
                while (Time.realtimeSinceStartupAsDouble < visibleDeadline)
                {
                    maximumPending = Mathf.Max(maximumPending, pose.QueuedPresentationCount);
                    if (pose.CurrentRequest.IsActive && driver.GetLayerWeight(magicLayer) > .18f)
                    {
                        firstVisibleAt = Time.realtimeSinceStartupAsDouble;
                        break;
                    }
                    yield return _frame;
                }
                Assert.That(firstVisibleAt, Is.GreaterThan(0d),
                    "Accepted dual-mouse magic never became visible.");
                Assert.That(firstVisibleAt - releasedAt, Is.LessThan(.20d),
                    "Accepted dual-mouse magic missed its responsive onset budget.");

                // Repeat the physical chord rapidly while the first bespoke
                // action is active. Gameplay may reject an unavailable stomp, but
                // presentation must never accumulate those attempts for later.
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    QueueDualMouseState(mouse, pointer, true, true);
                    yield return null;
                    QueueDualMouseState(mouse, pointer, false, false);
                    lastReleasedAt = Time.realtimeSinceStartupAsDouble;
                    yield return null;
                    maximumPending = Mathf.Max(maximumPending, pose.QueuedPresentationCount);
                }
                Assert.That(maximumPending, Is.LessThanOrEqualTo(1));

                double tailSimulationDeadline = Time.timeAsDouble + 1.5d;
                double tailWallDeadline = lastReleasedAt +
                                          (requestedFrameRate > 0 ? 4d : 1.5d);
                while ((pose.CurrentRequest.IsActive || pose.QueuedPresentationCount > 0) &&
                       Time.timeAsDouble < tailSimulationDeadline &&
                       Time.realtimeSinceStartupAsDouble < tailWallDeadline)
                {
                    maximumPending = Mathf.Max(maximumPending, pose.QueuedPresentationCount);
                    sawPunchSemantic |= pose.CurrentRequest.Technique ==
                                        EarthTechniqueId.QuickStonePunch;
                    if (pose.CurrentRequest.Technique == EarthTechniqueId.QuickStonePunch)
                    {
                        if (punchAcceptedAt <= 0d)
                            punchAcceptedAt = Time.realtimeSinceStartupAsDouble;
                        if (pose.RenderedContactReached && punchContactAt <= 0d)
                            punchContactAt = Time.realtimeSinceStartupAsDouble;
                    }
                    punchPosePeak = Mathf.Max(punchPosePeak,
                        driver.GetFloat(Animator.StringToHash("EarthPose11")));
                    _samples.Add(actor.Probe.Latest);
                    yield return _frame;
                }
                Assert.That(pose.CurrentRequest.IsActive || pose.QueuedPresentationCount > 0,
                    Is.False,
                    "Released LMB/RMB left delayed presentation actions replaying after 1.5 simulated seconds.");
                Assert.That(maximumPending, Is.LessThanOrEqualTo(1));
                Assert.That(pose.DroppedPresentationRequests, Is.Zero);
                Assert.That(sawPunchSemantic, Is.True,
                    "Stomp-stone launch never requested its authored punch semantic.");
                Assert.That(punchAcceptedAt, Is.GreaterThan(0d));
                Assert.That(punchContactAt, Is.GreaterThan(0d),
                    "The physical projectile launched but its authored punch contact never rendered.");
                Assert.That(punchContactAt - punchAcceptedAt, Is.LessThan(.14d),
                    "Physical release replayed punch wind-up after the projectile was already gone.");
                Assert.That(punchPosePeak, Is.GreaterThan(.70f),
                    "The launch punch was replaced before its slot became visibly weighted.");
                if (requestedFrameRate > 0)
                {
                    Debug.Log(
                        $"MAGIC_FRAME_RATE target={requestedFrameRate} " +
                        $"samples={continuity.SampleCount} actualAverageDelta={continuity.AverageDeltaSeconds:F5} " +
                        $"actualMaximumDelta={continuity.MaximumDeltaSeconds:F5} " +
                        $"animationAverageDelta={continuity.AverageAnimationDeltaSeconds:F5} " +
                        $"maximumBoneStep={continuity.MaximumStepDegrees:F3}");
                    Assert.That(continuity.SampleCount, Is.GreaterThan(8));
                    Assert.That(continuity.MaximumDeltaSeconds, Is.LessThan(.075f),
                        "The controlled frame-rate audit stalled and no longer represents interactive rendering.");
                    Assert.That(continuity.AverageAnimationDeltaSeconds,
                        Is.EqualTo(1f / requestedFrameRate).Within(.0025f),
                        "The requested capture delta did not reach the animation update path.");
                }
                Assert.That(continuity.MaximumStepDegrees, Is.LessThan(48f),
                    $"Rapid semantic handoff snapped a rendered upper-body bone by {continuity.MaximumStepDegrees:F2} degrees in one post-IK frame.");
            }
            finally
            {
                Time.captureDeltaTime = previousCaptureDeltaTime;
                Application.targetFrameRate = previousTargetFrameRate;
                QualitySettings.vSyncCount = previousVSyncCount;
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (continuity != null) UnityEngine.Object.Destroy(continuity);
                if (diagnosticProfile != null)
                {
                    profileField.SetValue(actor.Presentation, motionProfile);
                    UnityEngine.Object.Destroy(diagnosticProfile);
                }
            }
        }

        [UnityTest]
        public IEnumerator ShippingShortLmbQuickStoneUsesPunchInsteadOfHeavyThrow()
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose = actor.Presentation.PoseController;
            MagicInputController magic = actor.Presentation.GetComponentInParent<MagicInputController>();
            PlayerInput playerInput = actor.Presentation.GetComponentInParent<PlayerInput>();
            EarthAnimationDriver driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            Animator animator = actor.Presentation.Animator;
            Assert.That(magic, Is.Not.Null);
            Assert.That(playerInput, Is.Not.Null);
            int magicLayer = animator.GetLayerIndex("Earth Magic Upper Body");
            Mouse mouse = CreateMagicBurstMouse(playerInput);
            try
            {
                Camera camera = FindAnimationSceneComponent<Camera>(_scene);
                GameObject proxyObject = FindAnimationSceneObject(_scene, "Planet Collision Proxy");
                Collider proxy = proxyObject != null ? proxyObject.GetComponent<Collider>() : null;
                Physics.SyncTransforms();
                Assert.That(TryFindAnimationSurfacePoint(camera, proxy, out Vector2 pointer), Is.True,
                    "Quick Stone physical-input audit could not find a valid terrain pixel.");
                QueueDualMouseState(mouse, pointer, false, false);
                yield return null;

                // First short LMB tap traverses the dual-button disambiguator and
                // primes the canonical Quick Stone session.
                QueueDualMouseState(mouse, pointer, true, false);
                yield return null;
                QueueDualMouseState(mouse, pointer, false, false);
                yield return null;
                double primeDeadline = Time.realtimeSinceStartupAsDouble + 1.2d;
                while (!magic.IsQuickStonePrimed &&
                       Time.realtimeSinceStartupAsDouble < primeDeadline)
                    yield return _frame;
                Assert.That(magic.IsQuickStonePrimed, Is.True,
                    "A physical short-LMB tap did not prime Quick Stone through shipping input.");

                // A rapid second tap may buffer while extraction finishes. The
                // launch event itself must choose the short punch, not the heavy
                // two-hand throw used by charged rocks.
                QueueDualMouseState(mouse, pointer, true, false);
                yield return null;
                QueueDualMouseState(mouse, pointer, false, false);
                double releasedAt = Time.realtimeSinceStartupAsDouble;
                yield return null;

                double onsetAt = 0d;
                float punchPeak = 0f;
                float heavyThrowPeak = 0f;
                double onsetDeadline = releasedAt + 1.25d;
                while (Time.realtimeSinceStartupAsDouble < onsetDeadline)
                {
                    punchPeak = Mathf.Max(punchPeak,
                        driver.GetFloat(Animator.StringToHash("EarthPose11")));
                    heavyThrowPeak = Mathf.Max(heavyThrowPeak,
                        driver.GetFloat(Animator.StringToHash("EarthPose04")));
                    if (pose.CurrentRequest.Technique == EarthTechniqueId.QuickStonePunch &&
                        driver.GetLayerWeight(magicLayer) > .18f)
                    {
                        onsetAt = Time.realtimeSinceStartupAsDouble;
                        break;
                    }
                    yield return _frame;
                }
                Assert.That(onsetAt, Is.GreaterThan(0d),
                    "Quick Stone launch never reached the punch presentation semantic.");
                Assert.That(onsetAt - releasedAt, Is.LessThan(1.25d),
                    "Quick Stone punch appeared too late after the buffered second tap.");

                double peakDeadline = Time.realtimeSinceStartupAsDouble + .30d;
                while (Time.realtimeSinceStartupAsDouble < peakDeadline)
                {
                    punchPeak = Mathf.Max(punchPeak,
                        driver.GetFloat(Animator.StringToHash("EarthPose11")));
                    heavyThrowPeak = Mathf.Max(heavyThrowPeak,
                        driver.GetFloat(Animator.StringToHash("EarthPose04")));
                    yield return _frame;
                }
                Assert.That(punchPeak, Is.GreaterThan(.70f),
                    "Quick Stone punch never became visually readable.");
                Assert.That(heavyThrowPeak, Is.LessThan(.35f),
                    "Quick Stone incorrectly selected the slow heavy-throw silhouette.");
            }
            finally
            {
                if (mouse != null) InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator ShippingHeldBodyAimActivatesAfterContactAndReleasesWithoutAFlip()
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose = actor.Presentation.PoseController;
            MagicInputController magic = actor.Presentation.GetComponentInParent<MagicInputController>();
            FieldInfo executorField = typeof(HumanoidCharacterPresentation).GetField(
                "executor", BindingFlags.Instance | BindingFlags.NonPublic);
            MagicExecutor executor = executorField?.GetValue(actor.Presentation) as MagicExecutor;
            EarthAnimationDriver driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            Animator animator = actor.Presentation.Animator;
            Assert.That(magic, Is.Not.Null);
            Assert.That(executor, Is.Not.Null);
            FieldInfo profileField = typeof(HumanoidCharacterPresentation).GetField(
                "magicMotionProfile", BindingFlags.Instance | BindingFlags.NonPublic);
            var motionProfile = profileField?.GetValue(actor.Presentation) as EarthMagicMotionProfile;
            Assert.That(motionProfile, Is.Not.Null);
            EarthMagicMotionEntry gravityMotion = motionProfile.Find(
                (int)EarthHumanoidPoseSlot.GravityRepair);
            EarthMagicMotionEntry vectorMotion = motionProfile.Find(
                (int)EarthHumanoidPoseSlot.VectorPush);
            Assert.That(gravityMotion, Is.Not.Null);
            Assert.That(vectorMotion, Is.Not.Null);
            int magicLayer = animator.GetLayerIndex("Earth Magic Upper Body");
            int castA = Animator.StringToHash("Earth Magic Upper Body.Earth Cast");
            int castB = Animator.StringToHash("Earth Magic Upper Body.Earth Cast B");
            MagicBoneContinuityRecorder continuity =
                actor.Presentation.gameObject.AddComponent<MagicBoneContinuityRecorder>();
            continuity.Configure(animator, actor.Presentation, pose, driver, magicLayer);
            GameObject heldTarget = null;
            const float ownerHandoffResidualWeight = .005f;
            try
            {
                Camera camera = magic.CastCamera;
                Assert.That(camera, Is.Not.Null);
                Ray ray = camera.ViewportPointToRay(new Vector3(.5f, .52f, 0f));
                heldTarget = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                heldTarget.name = "Animation Held Aim Physical Target";
                SceneManager.MoveGameObjectToScene(heldTarget, _scene);
                heldTarget.transform.position = ray.GetPoint(4f);
                heldTarget.transform.localScale = Vector3.one * .42f;
                Rigidbody heldBody = heldTarget.AddComponent<Rigidbody>();
                heldBody.useGravity = false;
                heldBody.mass = 18f;
                heldTarget.AddComponent<PhysicalImpactTarget>().Configure(heldBody);
                Physics.SyncTransforms();
                PlanetMotor motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
                Assert.That(motor, Is.Not.Null);

                // Gravity Well can become a live sustained owner without an
                // authoritative gameplay event. Verify that the first field in
                // a fresh fixture uses its own rendered buffer contact instead
                // of waiting forever for a transient contact flag.
                Assert.That(executor.TryBeginGravityWell(
                    heldTarget.GetComponent<Collider>(),
                    heldBody.worldCenterOfMass,
                    motor.LocalUp,
                    true), Is.True);
                bool firstGravityWeightedBeforeContact = false;
                double firstGravityDeadline = Time.realtimeSinceStartupAsDouble + 1.8d;
                while ((actor.Presentation.HandConstraintWeight < .12f ||
                        actor.Presentation.MagicClipTime + .0005f < gravityMotion.timing.Contact) &&
                       Time.realtimeSinceStartupAsDouble < firstGravityDeadline)
                {
                    yield return _frame;
                    firstGravityWeightedBeforeContact |=
                        actor.Presentation.MagicClipTime + .0005f < gravityMotion.timing.Contact &&
                        actor.Presentation.HandConstraintWeight > .02f;
                }
                Assert.That(firstGravityWeightedBeforeContact, Is.False,
                    "The first live field constrained hands before its own rendered contact.");
                Assert.That(actor.Presentation.HandConstraintWeight, Is.GreaterThan(.12f),
                    "The first live Gravity Well never acquired post-contact hand IK.");
                int firstGravityState = driver.GetCurrentAnimatorStateInfo(magicLayer).fullPathHash;
                Assert.That(firstGravityState == castA || firstGravityState == castB, Is.True,
                    "The first persistent field was not rendered by an independent magic buffer.");
                executor.CancelGravityWell();
                double firstGravityReleaseDeadline = Time.realtimeSinceStartupAsDouble + 1.2d;
                while (actor.Presentation.HandConstraintWeight > ownerHandoffResidualWeight &&
                       Time.realtimeSinceStartupAsDouble < firstGravityReleaseDeadline)
                    yield return _frame;
                Assert.That(actor.Presentation.HandConstraintWeight,
                    Is.LessThanOrEqualTo(ownerHandoffResidualWeight));

                // A pure persistent field carries no authoritative gameplay tick.
                // Cancel/reacquire of the same kind must still start the inactive
                // A/B state. Rewinding the still-visible prior state in place is a
                // visible snap even though the semantic and tick are unchanged.
                heldBody.linearVelocity = Vector3.zero;
                heldBody.angularVelocity = Vector3.zero;
                Assert.That(executor.TryBeginGravityWell(
                    heldTarget.GetComponent<Collider>(),
                    heldBody.worldCenterOfMass,
                    motor.LocalUp,
                    true), Is.True,
                    "A same-kind persistent field could not be reacquired after cancellation.");
                yield return _frame;
                Assert.That(actor.Presentation.MagicClipTime,
                    Is.LessThan(gravityMotion.timing.Contact - .02f),
                    "Reacquired Gravity Well resumed at stale contact instead of restarting anticipation.");
                bool reacquireWeightedBeforeContact =
                    actor.Presentation.HandConstraintWeight > .02f;
                double reacquireDeadline = Time.realtimeSinceStartupAsDouble + 1.8d;
                while ((actor.Presentation.HandConstraintWeight < .12f ||
                        actor.Presentation.MagicClipTime + .0005f < gravityMotion.timing.Contact) &&
                       Time.realtimeSinceStartupAsDouble < reacquireDeadline)
                {
                    yield return _frame;
                    reacquireWeightedBeforeContact |=
                        actor.Presentation.MagicClipTime + .0005f < gravityMotion.timing.Contact &&
                        actor.Presentation.HandConstraintWeight > .02f;
                }
                Assert.That(reacquireWeightedBeforeContact, Is.False,
                    "Reacquired persistent field constrained hands before its new contact.");
                int secondGravityState = driver.GetCurrentAnimatorStateInfo(magicLayer).fullPathHash;
                Assert.That(secondGravityState == castA || secondGravityState == castB, Is.True);
                Assert.That(secondGravityState, Is.Not.EqualTo(firstGravityState),
                    "Same-kind persistent field rewound the visible buffer instead of alternating A/B states.");
                Assert.That(actor.Presentation.HandConstraintWeight, Is.GreaterThan(.12f),
                    "Reacquired Gravity Well never reached post-contact hand IK.");
                executor.CancelGravityWell();
                firstGravityReleaseDeadline = Time.realtimeSinceStartupAsDouble + 1.2d;
                while (actor.Presentation.HandConstraintWeight > ownerHandoffResidualWeight &&
                       Time.realtimeSinceStartupAsDouble < firstGravityReleaseDeadline)
                    yield return _frame;
                Assert.That(actor.Presentation.HandConstraintWeight,
                    Is.LessThanOrEqualTo(ownerHandoffResidualWeight));

                heldBody.linearVelocity = Vector3.zero;
                heldBody.angularVelocity = Vector3.zero;
                ray = camera.ViewportPointToRay(new Vector3(.5f, .52f, 0f));
                heldBody.position = ray.GetPoint(4f);
                Physics.SyncTransforms();
                Vector3 projected = camera.WorldToScreenPoint(heldBody.worldCenterOfMass);
                Assert.That(projected.z, Is.GreaterThan(0f));
                float2 pointer = new float2(projected.x, projected.y);
                Assert.That(magic.TryBeginEarthBendAtScreenPoint(
                    pointer, BendOriginMode.Aim, .45f), Is.True,
                    "Shipping earth-bend ingress did not acquire the physical target.");
                Assert.That(executor.HeldBody, Is.SameAs(heldBody));

                float previousWeight = actor.Presentation.HandConstraintWeight;
                float maximumWeightStep = 0f;
                bool weightedBeforeContact = false;
                double contactDeadline = Time.realtimeSinceStartupAsDouble + 1.4d;
                while ((!pose.RenderedContactReached ||
                        actor.Presentation.HandConstraintWeight < .12f) &&
                       Time.realtimeSinceStartupAsDouble < contactDeadline)
                {
                    yield return _frame;
                    float weight = actor.Presentation.HandConstraintWeight;
                    weightedBeforeContact |= !pose.RenderedContactReached && weight > .02f;
                    maximumWeightStep = Mathf.Max(maximumWeightStep,
                        Mathf.Abs(weight - previousWeight));
                    previousWeight = weight;
                }
                Assert.That(weightedBeforeContact, Is.False,
                    "Held aim IK overrode the authored reach before its rendered contact.");
                Assert.That(pose.RenderedContactReached, Is.True);
                Assert.That(actor.Presentation.HandConstraintWeight, Is.GreaterThan(.12f),
                    "Real held-body aim never acquired its post-contact constraint.");

                Assert.That(magic.TrySetEarthBendTargetAtScreenPoint(
                    pointer + new float2(28f, 12f), 1f / 60f), Is.True);
                Assert.That(magic.TryReleaseEarthBendAtScreenPoint(
                    pointer + new float2(28f, 12f),
                    camera.transform.forward * 5f,
                    BendGestureIntent.Flick,
                    out Vector3 releaseVelocity), Is.True,
                    "Physical held-body release did not traverse shipping earth-bend output.");
                Assert.That(releaseVelocity.sqrMagnitude, Is.GreaterThan(.01f));
                double releaseDeadline = Time.realtimeSinceStartupAsDouble + 1.4d;
                while ((executor.HeldBody != null ||
                        actor.Presentation.HandConstraintWeight > ownerHandoffResidualWeight) &&
                       Time.realtimeSinceStartupAsDouble < releaseDeadline)
                {
                    yield return _frame;
                    float weight = actor.Presentation.HandConstraintWeight;
                    maximumWeightStep = Mathf.Max(maximumWeightStep,
                        Mathf.Abs(weight - previousWeight));
                    previousWeight = weight;
                }
                Assert.That(executor.HeldBody, Is.Null,
                    "Shipping earth-bend release left the physical body owned.");
                Assert.That(actor.Presentation.HandConstraintWeight,
                    Is.LessThanOrEqualTo(ownerHandoffResidualWeight),
                    "Held aim constraint remained after physical release.");

                heldBody.linearVelocity = Vector3.zero;
                heldBody.angularVelocity = Vector3.zero;
                heldBody.position = actor.Presentation.transform.position +
                                    actor.Presentation.transform.forward * 3f +
                                    actor.Presentation.transform.up * .8f;
                Physics.SyncTransforms();
                Collider targetCollider = heldTarget.GetComponent<Collider>();
                Assert.That(executor.TryBeginGravityWell(
                    targetCollider,
                    heldBody.worldCenterOfMass,
                    motor.LocalUp,
                    true), Is.True,
                    "The real gravity-well owner could not acquire the physical target.");
                bool gravityWeightedBeforeContact = false;
                contactDeadline = Time.realtimeSinceStartupAsDouble + 1.8d;
                while ((actor.Presentation.HandConstraintWeight < .12f ||
                        actor.Presentation.MagicClipTime + .0005f < gravityMotion.timing.Contact) &&
                       Time.realtimeSinceStartupAsDouble < contactDeadline)
                {
                    yield return _frame;
                    float weight = actor.Presentation.HandConstraintWeight;
                    gravityWeightedBeforeContact |=
                        actor.Presentation.MagicClipTime + .0005f < gravityMotion.timing.Contact &&
                        weight > .02f;
                    maximumWeightStep = Mathf.Max(maximumWeightStep,
                        Mathf.Abs(weight - previousWeight));
                    previousWeight = weight;
                }
                Assert.That(gravityWeightedBeforeContact, Is.False,
                    "Gravity-well IK reused stale contact from the prior held-body buffer.");
                Assert.That(actor.Presentation.HandConstraintWeight, Is.GreaterThan(.12f),
                    "Gravity-well ownership never acquired a post-contact hand constraint.");
                executor.CancelGravityWell();
                releaseDeadline = Time.realtimeSinceStartupAsDouble + 1.2d;
                while (actor.Presentation.HandConstraintWeight > ownerHandoffResidualWeight &&
                       Time.realtimeSinceStartupAsDouble < releaseDeadline)
                {
                    yield return _frame;
                    float weight = actor.Presentation.HandConstraintWeight;
                    maximumWeightStep = Mathf.Max(maximumWeightStep,
                        Mathf.Abs(weight - previousWeight));
                    previousWeight = weight;
                }
                Assert.That(actor.Presentation.HandConstraintWeight,
                    Is.LessThanOrEqualTo(ownerHandoffResidualWeight));

                heldBody.linearVelocity = Vector3.zero;
                heldBody.angularVelocity = Vector3.zero;
                Assert.That(executor.TryBeginVectorField(
                    targetCollider,
                    heldBody,
                    heldBody.worldCenterOfMass,
                    actor.Presentation.transform.forward), Is.True,
                    "The real vector-field owner could not acquire the physical target.");
                bool vectorWeightedBeforeContact = false;
                contactDeadline = Time.realtimeSinceStartupAsDouble + 1.8d;
                while ((actor.Presentation.HandConstraintWeight < .12f ||
                        actor.Presentation.MagicClipTime + .0005f < vectorMotion.timing.Contact) &&
                       Time.realtimeSinceStartupAsDouble < contactDeadline)
                {
                    yield return _frame;
                    float weight = actor.Presentation.HandConstraintWeight;
                    vectorWeightedBeforeContact |=
                        actor.Presentation.MagicClipTime + .0005f < vectorMotion.timing.Contact &&
                        weight > .02f;
                    maximumWeightStep = Mathf.Max(maximumWeightStep,
                        Mathf.Abs(weight - previousWeight));
                    previousWeight = weight;
                }
                Assert.That(vectorWeightedBeforeContact, Is.False,
                    "Vector-field IK reused stale contact from the prior gravity buffer.");
                Assert.That(actor.Presentation.HandConstraintWeight, Is.GreaterThan(.12f),
                    "Vector-field ownership never acquired a post-contact hand constraint.");
                Assert.That(executor.ReleaseVectorField(), Is.True,
                    "Physical vector-field release was rejected.");
                releaseDeadline = Time.realtimeSinceStartupAsDouble + 1.2d;
                while (actor.Presentation.HandConstraintWeight > .03f &&
                       Time.realtimeSinceStartupAsDouble < releaseDeadline)
                {
                    yield return _frame;
                    float weight = actor.Presentation.HandConstraintWeight;
                    maximumWeightStep = Mathf.Max(maximumWeightStep,
                        Mathf.Abs(weight - previousWeight));
                    previousWeight = weight;
                }
                Assert.That(actor.Presentation.HandConstraintWeight, Is.LessThan(.03f));
                Assert.That(maximumWeightStep, Is.LessThan(.16f),
                    "A sustained magic owner changed hand-constraint weight discontinuously.");
                Assert.That(continuity.MaximumStepDegrees, Is.LessThan(48f),
                    $"Held/gravity/vector aim ownership flipped an upper-body bone by {continuity.MaximumStepDegrees:F2} degrees.");
            }
            finally
            {
                if (continuity != null) UnityEngine.Object.Destroy(continuity);
                if (heldTarget != null) UnityEngine.Object.Destroy(heldTarget);
            }
        }

        [UnityTest]
        public IEnumerator GravityGripPreContactWithoutGenericC1MatchesBoundedSourceAtThirtyHertz() =>
            ObserveGravityGripWithoutGenericC1(30);

        [UnityTest]
        public IEnumerator GravityGripPreContactWithoutGenericC1MatchesBoundedSourceAtSixtyHertz() =>
            ObserveGravityGripWithoutGenericC1(60);

        private IEnumerator ObserveGravityGripWithoutGenericC1(int requestedFrameRate)
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            EarthAnimationDriver driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            Animator animator = actor.Presentation.Animator;
            FieldInfo executorField = typeof(HumanoidCharacterPresentation).GetField(
                "executor", BindingFlags.Instance | BindingFlags.NonPublic);
            MagicExecutor executor = executorField?.GetValue(actor.Presentation) as MagicExecutor;
            Assert.That(driver, Is.Not.Null);
            Assert.That(driver.UsesPlayableGraph, Is.True,
                "C1 isolation requires the production PlayableGraph path.");
            Assert.That(executor, Is.Not.Null);

            PlanetMotor motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            Assert.That(motor, Is.Not.Null);
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = "Gravity C1 isolation target";
            SceneManager.MoveGameObjectToScene(target, _scene);
            target.transform.position = actor.Presentation.transform.position +
                                        actor.Presentation.transform.forward * 3f +
                                        actor.Presentation.transform.up * .8f;
            Rigidbody body = target.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 18f;
            target.AddComponent<PhysicalImpactTarget>().Configure(body);
            Physics.SyncTransforms();

            GravityPreContactContinuityRecorder continuity = null;
            int previousTargetFrameRate = Application.targetFrameRate;
            int previousVSyncCount = QualitySettings.vSyncCount;
            float previousCaptureDeltaTime = Time.captureDeltaTime;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = requestedFrameRate;
            Time.captureDeltaTime = 1f / requestedFrameRate;
            driver.SetInertializationEnabledForQa(false);
            try
            {
                continuity = actor.Presentation.gameObject.AddComponent<GravityPreContactContinuityRecorder>();
                continuity.Configure(animator, actor.Presentation, .145f, .35f);
                Assert.That(executor.TryBeginGravityWell(
                    target.GetComponent<Collider>(),
                    body.worldCenterOfMass,
                    motor.LocalUp,
                    true), Is.True);
                continuity.BeginCapture();
                double sampleDeadline = Time.realtimeSinceStartupAsDouble + 1.2d;
                // MagicClipTime still contains the completed idle/recovery clock until the
                // presentation Update consumes this new request. Always allow that first
                // render evaluation, then wait for the newly reset clock to cross the window.
                // Testing the stale value synchronously used to skip every frame (.98 >= .37)
                // and report a false zero-sample isolation.
                do
                {
                    yield return _frame;
                }
                while ((!continuity.SawFreshClock || continuity.FreshMaximumObservedClock < .37f) &&
                       Time.realtimeSinceStartupAsDouble < sampleDeadline);

                Debug.Log(
                    $"[GravityC1Isolation] requestedHz={requestedFrameRate} " +
                    $"observedFrames={continuity.ObservedFrameCount} " +
                    $"clockRange={continuity.MinimumObservedClock:F4}..{continuity.MaximumObservedClock:F4} " +
                    $"freshMax={continuity.FreshMaximumObservedClock:F4} " +
                    $"windowSamples={continuity.SampleCount} trace={continuity.ClockTrace}");
                Assert.That(continuity.ObservedFrameCount, Is.GreaterThanOrEqualTo(3),
                    "The final-pose recorder did not run for enough rendered frames.");
                Assert.That(continuity.SawFreshClock, Is.True,
                    "The new Gravity Grip request never produced a fresh/reset magic buffer clock.");
                Assert.That(continuity.FreshMaximumObservedClock, Is.GreaterThanOrEqualTo(.37f),
                    "The fresh Gravity Grip buffer did not advance through the measured source interval.");
                int expectedSamples = Mathf.Max(3, Mathf.FloorToInt(
                    (.35f - .145f) /
                    (EarthMagicClipClock.MaximumNormalizedSpeedPerSecond / requestedFrameRate)) - 1);
                Assert.That(continuity.SampleCount, Is.GreaterThanOrEqualTo(expectedSamples),
                    $"The {requestedFrameRate} Hz isolation never captured a meaningful .145-.35 source interval.");
                Assert.That(continuity.MaximumHandConstraintWeight, Is.LessThan(.001f),
                    "The C1 source isolation accidentally admitted hand IK.");
                Debug.Log(
                    $"[GravityC1Isolation] requestedHz={requestedFrameRate} " +
                    $"maximumFinalBoneStep={continuity.MaximumStepDegrees:F3} " +
                    $"samples={continuity.SampleCount} avgDelta={continuity.AverageDeltaSeconds:F5}");
                Assert.That(continuity.MaximumStepDegrees, Is.LessThan(90f),
                    "Controller/retarget output still exceeds the directly sampled source bound with C1 bypassed.");
            }
            finally
            {
                executor.CancelGravityWell();
                driver.SetInertializationEnabledForQa(true);
                if (continuity != null) UnityEngine.Object.Destroy(continuity);
                UnityEngine.Object.Destroy(target);
                Time.captureDeltaTime = previousCaptureDeltaTime;
                Application.targetFrameRate = previousTargetFrameRate;
                QualitySettings.vSyncCount = previousVSyncCount;
            }
        }

        [UnityTest]
        public IEnumerator ProtectedMantleRejectsLateMagicAndDoesNotReplayItAfterLanding()
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose = actor.Presentation.PoseController;
            PlanetMotor motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            Assert.That(motor, Is.Not.Null);
            Vector3 up = motor.LocalUp.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            GameObject ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name = "Magic ownership mantle audit ledge";
            SceneManager.MoveGameObjectToScene(ledge, _scene);
            ledge.transform.SetPositionAndRotation(
                motor.SupportFeetPoint(up) + forward * 2.3f + up * .2f,
                Quaternion.LookRotation(forward, up));
            ledge.transform.localScale = new Vector3(4f, 1.4f, 3f);
            for (int layer = 0; layer < 32; layer++)
                if ((motor.GroundMask.value & (1 << layer)) != 0)
                {
                    ledge.layer = layer;
                    break;
                }
            Physics.SyncTransforms();
            try
            {
                uint mantleSequence = motor.MantleSequence;
                actor.Input.Move = new float2(0f, 1f);
                double deadline = Time.realtimeSinceStartupAsDouble + 6d;
                while (motor.MantleSequence == mantleSequence &&
                       Time.realtimeSinceStartupAsDouble < deadline)
                    yield return _frame;
                Assert.That(motor.IsMantling, Is.True);
                yield return _frame;
                Assert.That(pose.PresentationSuppressed, Is.True);
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Grip,
                    EarthTechniqueId.QuickStonePunch,
                    0xfa000001u,
                    actor.Presentation.transform.position + forward * 2f,
                    30f,
                    5f);
                yield return new WaitForFixedUpdate();
                Assert.That(pose.HasAuthoritativePresentation, Is.False);
                Assert.That(pose.QueuedPresentationCount, Is.Zero);
                Assert.That(actor.Presentation.CurrentAuthoredAction,
                    Is.EqualTo(EarthAuthoredActionId.Mantle));

                actor.Input.Move = float2.zero;
                deadline = Time.realtimeSinceStartupAsDouble + 4d;
                while ((motor.IsMantling || !motor.HasStableSupport ||
                        pose.PresentationSuppressed) &&
                       Time.realtimeSinceStartupAsDouble < deadline)
                    yield return _frame;
                Assert.That(pose.PresentationSuppressed, Is.False);
                Assert.That(pose.HasAuthoritativePresentation, Is.False,
                    "A cast received during mantle replayed after landing.");
                Assert.That(pose.QueuedPresentationCount, Is.Zero);
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Grip,
                    EarthTechniqueId.QuickStonePunch,
                    0xfa000002u,
                    actor.Presentation.transform.position + forward * 2f,
                    30f,
                    5f);
                Assert.That(pose.HasAuthoritativePresentation, Is.True,
                    "New magic did not resume after mantle released the skeleton.");
            }
            finally
            {
                actor.Input.Move = float2.zero;
                if (ledge != null) UnityEngine.Object.Destroy(ledge);
            }
        }

        [UnityTest]
        public IEnumerator ProtectedRagdollRecoveryRejectsLateMagicUntilCompletion()
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose = actor.Presentation.PoseController;
            PlanetMotor motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            HumanoidRagdollRig visibleRagdoll =
                actor.Presentation.GetComponent<HumanoidRagdollRig>();
            Assert.That(motor, Is.Not.Null);
            Assert.That(visibleRagdoll, Is.Not.Null);

            visibleRagdoll.BeginRagdoll(Vector3.zero);
            yield return _frame;
            Assert.That(visibleRagdoll.IsRagdollActive, Is.True);
            Assert.That(pose.PresentationSuppressed, Is.True);
            pose.RequestSemanticPresentation(
                EarthTechniqueKind.Grip,
                EarthTechniqueId.QuickStonePunch,
                0xfb000001u,
                actor.Presentation.transform.position + motor.FacingForward * 2f,
                30f,
                5f);
            yield return new WaitForFixedUpdate();
            Assert.That(pose.HasAuthoritativePresentation, Is.False);
            Assert.That(pose.QueuedPresentationCount, Is.Zero);

            visibleRagdoll.RecoverToAnimated(motor.LocalUp, motor.FacingForward, false);
            yield return _frame;
            Assert.That(visibleRagdoll.IsRecoveringToAnimation, Is.True);
            Assert.That(pose.PresentationSuppressed, Is.True);
            pose.RequestSemanticPresentation(
                EarthTechniqueKind.Wall,
                EarthTechniqueId.RaiseWall,
                0xfb000002u,
                actor.Presentation.transform.position + motor.FacingForward * 3f,
                200f,
                7f);
            yield return new WaitForFixedUpdate();
            Assert.That(pose.HasAuthoritativePresentation, Is.False);
            Assert.That(pose.QueuedPresentationCount, Is.Zero);

            visibleRagdoll.CompleteRecovery();
            yield return _frame;
            yield return new WaitForFixedUpdate();
            Assert.That(pose.PresentationSuppressed, Is.False);
            Assert.That(pose.HasAuthoritativePresentation, Is.False,
                "A cast received during ragdoll/recovery replayed after completion.");
            pose.RequestSemanticPresentation(
                EarthTechniqueKind.Grip,
                EarthTechniqueId.QuickStonePunch,
                0xfb000003u,
                actor.Presentation.transform.position + motor.FacingForward * 2f,
                30f,
                5f);
            Assert.That(pose.HasAuthoritativePresentation, Is.True,
                "New magic did not resume after recovery released the skeleton.");
        }

        [UnityTest]
        public IEnumerator RepeatedPunchRequestsAlternateBuffersAndExtendAgainWithoutASnap()
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose = actor.Presentation.PoseController;
            EarthAnimationDriver driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            Animator animator = actor.Presentation.Animator;
            int magicLayer = animator.GetLayerIndex("Earth Magic Upper Body");
            int castA = Animator.StringToHash("Earth Magic Upper Body.Earth Cast");
            int castB = Animator.StringToHash("Earth Magic Upper Body.Earth Cast B");
            Transform forearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Assert.That(forearm, Is.Not.Null);
            Vector3 target = actor.Presentation.transform.position +
                             actor.Presentation.transform.forward * 3f;

            pose.RequestSemanticPresentation(EarthTechniqueKind.Grip,
                EarthTechniqueId.QuickStonePunch, 0xf1000001u, target, 35f, 30f);
            double firstDeadline = Time.realtimeSinceStartupAsDouble + 1.4d;
            while (!pose.RenderedContactReached &&
                   Time.realtimeSinceStartupAsDouble < firstDeadline)
                yield return _frame;
            Assert.That(pose.RenderedContactReached, Is.True,
                "First punch never produced a visible contact pose.");
            int firstState = driver.GetCurrentAnimatorStateInfo(magicLayer).fullPathHash;
            Assert.That(firstState == castA || firstState == castB, Is.True);

            Quaternion previous = forearm.localRotation;
            Quaternion start = previous;
            float maximumStep = 0f;
            float maximumExcursion = 0f;
            float minimumClock = 1f;
            float maximumClock = 0f;
            FieldInfo motionProfileField = typeof(HumanoidCharacterPresentation).GetField(
                "magicMotionProfile", BindingFlags.Instance | BindingFlags.NonPublic);
            var motionProfile = motionProfileField?.GetValue(actor.Presentation) as EarthMagicMotionProfile;
            EarthMagicMotionEntry punchMotion = motionProfile != null ? motionProfile.Find(11) : null;
            Assert.That(punchMotion, Is.Not.Null);
            pose.RequestSemanticPresentation(EarthTechniqueKind.Grip,
                EarthTechniqueId.QuickStonePunch, 0xf1000002u, target, 35f, 30f);
            double secondDeadline = Time.realtimeSinceStartupAsDouble + 1.4d;
            while ((!pose.RenderedContactReached || pose.LastAuthoritativeTick != 0xf1000002u) &&
                   Time.realtimeSinceStartupAsDouble < secondDeadline)
            {
                yield return _frame;
                Quaternion current = forearm.localRotation;
                maximumStep = Mathf.Max(maximumStep, Quaternion.Angle(previous, current));
                maximumExcursion = Mathf.Max(maximumExcursion, Quaternion.Angle(start, current));
                previous = current;
                float clock = driver.GetFloat(Animator.StringToHash("EarthMotionTime"));
                minimumClock = Mathf.Min(minimumClock, clock);
                maximumClock = Mathf.Max(maximumClock, clock);
            }

            Assert.That(pose.LastAuthoritativeTick, Is.EqualTo(0xf1000002u));
            Assert.That(pose.RenderedContactReached, Is.True,
                "Repeated same-slot punch never reached its own visible contact.");
            int secondState = driver.GetCurrentAnimatorStateInfo(magicLayer).fullPathHash;
            Assert.That(secondState == castA || secondState == castB, Is.True);
            Assert.That(secondState, Is.Not.EqualTo(firstState),
                "Repeated punch reused the outgoing Animator state instead of the independent buffer.");
            Assert.That(minimumClock, Is.LessThan(.15f),
                "Repeated punch did not restart its own clip clock.");
            Assert.That(maximumClock,
                Is.GreaterThanOrEqualTo(punchMotion.timing.Contact - .005f),
                "Repeated punch did not extend back to its contact beat.");
            Assert.That(maximumExcursion, Is.GreaterThan(8f),
                "Repeated punch remained frozen instead of retracting and extending again.");
            Assert.That(maximumStep, Is.LessThan(48f),
                $"A/B handoff snapped the punch forearm by {maximumStep:F2} degrees in one frame.");
        }

        [UnityTest]
        public IEnumerator RapidCommittedPunchesRenderContactThenLatestRetractsAndExtends()
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose = actor.Presentation.PoseController;
            EarthAnimationDriver driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            Animator animator = actor.Presentation.Animator;
            Transform forearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Assert.That(forearm, Is.Not.Null);
            Vector3 target = actor.Presentation.transform.position +
                             actor.Presentation.transform.forward * 3f;
            const uint firstSequence = 0xf2000001u;
            const uint latestSequence = 0xf2000006u;

            pose.RequestSemanticPresentation(
                EarthTechniqueKind.Grip, EarthTechniqueId.QuickStonePunch,
                firstSequence, target, 30f, 26f,
                immediateActionBoundary: true);
            for (uint index = 2; index <= 6; index++)
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Grip, EarthTechniqueId.QuickStonePunch,
                    0xf2000000u + index, target, 30f, 26f,
                    immediateActionBoundary: true);

            Assert.That(pose.LastAuthoritativeTick, Is.EqualTo(firstSequence),
                "Pre-render repeats rewound the release-aligned contact buffer.");
            Assert.That(pose.QueuedPresentationCount, Is.EqualTo(1));

            Quaternion previous = forearm.localRotation;
            Quaternion start = previous;
            float maximumStep = 0f;
            float maximumExcursion = 0f;
            float latestMinimumClock = 1f;
            float latestMaximumClock = 0f;
            double deadline = Time.realtimeSinceStartupAsDouble + 1.5d;
            while ((!pose.RenderedContactReached ||
                    pose.LastAuthoritativeTick != latestSequence) &&
                   Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return _frame;
                Quaternion current = forearm.localRotation;
                maximumStep = Mathf.Max(maximumStep, Quaternion.Angle(previous, current));
                maximumExcursion = Mathf.Max(maximumExcursion, Quaternion.Angle(start, current));
                previous = current;
                if (pose.LastAuthoritativeTick == latestSequence)
                {
                    float clock = driver.GetFloat(Animator.StringToHash("EarthMotionTime"));
                    latestMinimumClock = Mathf.Min(latestMinimumClock, clock);
                    latestMaximumClock = Mathf.Max(latestMaximumClock, clock);
                }
            }

            Assert.That(pose.LastAuthoritativeTick, Is.EqualTo(latestSequence));
            Assert.That(pose.RenderedContactReached, Is.True,
                "The latest coalesced punch never rendered its own contact.");
            Assert.That(latestMinimumClock, Is.LessThan(.15f),
                "The follow-up punch stayed parked at contact instead of retracting.");
            Assert.That(latestMaximumClock, Is.GreaterThanOrEqualTo(.465f),
                "The follow-up punch did not extend back to authored contact.");
            Assert.That(maximumExcursion, Is.GreaterThan(8f));
            Assert.That(maximumStep, Is.LessThan(48f));
            Assert.That(pose.QueuedPresentationCount, Is.Zero);
        }

        private static bool TryFindAnimationSurfacePoint(
            Camera camera,
            Collider proxy,
            out Vector2 screenPoint)
        {
            screenPoint = default;
            if (camera == null || proxy == null) return false;
            int width = Mathf.Max(320, Screen.width);
            int height = Mathf.Max(200, Screen.height);
            for (int y = 16; y < height - 16; y += 8)
            for (int x = 16; x < width - 16; x += 8)
            {
                if (!proxy.Raycast(camera.ScreenPointToRay(new Vector2(x, y)),
                        out _, 200f)) continue;
                screenPoint = new Vector2(x, y);
                return true;
            }
            return false;
        }

        private static T FindAnimationSceneComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T value = root.GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }

        private static GameObject FindAnimationSceneObject(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child.gameObject;
            return null;
        }

        private static Mouse CreateMagicBurstMouse(PlayerInput playerInput)
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>("Magic burst routed mouse");
            playerInput.ActivateInput();
            if (playerInput.user.valid)
            {
                if (Keyboard.current != null)
                    playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", Keyboard.current, mouse);
                else InputUser.PerformPairingWithDevice(mouse, playerInput.user);
            }
            return mouse;
        }

        private static void QueueDualMouseState(
            Mouse mouse,
            Vector2 pointer,
            bool left,
            bool right)
        {
            var state = new MouseState { position = pointer };
            state.WithButton(MouseButton.Left, left);
            state.WithButton(MouseButton.Right, right);
            InputSystem.QueueStateEvent(mouse, state);
        }

        [UnityTest]
        public IEnumerator SemanticMagicSlotsBecomeOneHotAndKeepAValidUpperBodyPose()
        {
            Actor actor = _actors.Find(value =>
                value.Presentation.GetComponent<EarthCharacterPoseController>() != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose =
                actor.Presentation.GetComponent<EarthCharacterPoseController>();
            EarthAnimationDriver driver = actor.Presentation.GetComponent<EarthAnimationDriver>();
            EarthChoreographyDirector choreography =
                actor.Presentation.GetComponent<EarthChoreographyDirector>();
            Animator animator = actor.Presentation.GetComponent<Animator>();
            Assert.That(choreography, Is.Not.Null);
            FieldInfo profileField = typeof(HumanoidCharacterPresentation).GetField(
                "magicMotionProfile", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(profileField, Is.Not.Null);
            EarthMagicMotionProfile motionProfile =
                profileField.GetValue(actor.Presentation) as EarthMagicMotionProfile;
            Assert.That(motionProfile, Is.Not.Null,
                "Production presentation is not bound to its semantic timing profile.");
            int magicLayer = animator.GetLayerIndex("Earth Magic Upper Body");
            Assert.That(magicLayer, Is.GreaterThanOrEqualTo(0));
            int castHash = Animator.StringToHash("Earth Magic Upper Body.Earth Cast");
            int castBHash = Animator.StringToHash("Earth Magic Upper Body.Earth Cast B");
            EarthTechniqueId[] techniques =
            {
                EarthTechniqueId.RaiseWall, EarthTechniqueId.RaisePlatform,
                EarthTechniqueId.PullStone, EarthTechniqueId.ThrowStone,
                EarthTechniqueId.VectorPush, EarthTechniqueId.Repair,
                EarthTechniqueId.Resonance, EarthTechniqueId.PillarJump,
                EarthTechniqueId.Armor, EarthTechniqueId.ArmorBarrage,
                EarthTechniqueId.MeteorFinish
            };
            var visualPoses = new List<EarthChoreographyPoseOffset>(techniques.Length);

            foreach (EarthTechniqueId technique in techniques)
            {
                int slot = (int)EarthHumanoidMotionResolver.Resolve(technique);
                EarthMagicMotionEntry motion = motionProfile.Find(slot);
                Assert.That(motion, Is.Not.Null, technique.ToString());
                pose.CancelPresentationForAnimationOwnership();
                pose.RequestSemanticPresentation(
                    EarthTechniqueKind.Wall, technique, pose.PresentationTick,
                    actor.Presentation.transform.position + actor.Presentation.transform.forward * 3f,
                    80f, 4f);
                float desiredPeak = 0f;
                float smallestOtherSumAtPeak = float.PositiveInfinity;
                float minimumHeadHeight = float.PositiveInfinity;
                float maximumHeadPitch = 0f;
                EarthChoreographyPoseOffset strongestVisualPose = default;
                float strongestVisualDegrees = 0f;
                bool sawCastState = false;
                bool sawSynchronizedContact = false;
                double deadline = Time.realtimeSinceStartupAsDouble + 1.25d;
                while (Time.realtimeSinceStartupAsDouble < deadline)
                {
                    yield return _frame;
                    if (actor.Presentation.CurrentAuthoredAction != EarthAuthoredActionId.MagicCast)
                        continue;
                    EarthAnimationPoseSample sample = actor.Probe.Latest;
                    _samples.Add(sample);
                    minimumHeadHeight = Mathf.Min(minimumHeadHeight, sample.headHeight);
                    maximumHeadPitch = Mathf.Max(maximumHeadPitch, Mathf.Abs(sample.headPitchDegrees));
                    AnimatorStateInfo state = driver.GetCurrentAnimatorStateInfo(magicLayer);
                    AnimatorStateInfo next = driver.GetNextAnimatorStateInfo(magicLayer);
                    sawCastState |= state.fullPathHash == castHash || next.fullPathHash == castHash ||
                                    state.fullPathHash == castBHash || next.fullPathHash == castBHash;
                    float desired = driver.GetFloat(Animator.StringToHash($"EarthPose{slot:00}"));
                    float other = 0f;
                    for (int candidate = 1; candidate <= 11; candidate++)
                        if (candidate != slot)
                            other += driver.GetFloat(Animator.StringToHash($"EarthPose{candidate:00}"));
                    if (desired > desiredPeak)
                    {
                        desiredPeak = desired;
                        smallestOtherSumAtPeak = other;
                    }
                    EarthChoreographyPoseOffset visualPose = choreography.AppliedVisualPose;
                    if (visualPose.MaximumAbsDegrees > strongestVisualDegrees)
                    {
                        strongestVisualDegrees = visualPose.MaximumAbsDegrees;
                        strongestVisualPose = visualPose;
                    }
                    if (choreography.CurrentRequest.Technique == technique &&
                        choreography.CurrentRequest.Phase is EarthCastPhase.Sustain or EarthCastPhase.Recover &&
                        actor.Presentation.MagicClipTime >= motion.timing.Contact - .001f)
                        sawSynchronizedContact = true;
                }

                Assert.That(sawCastState, Is.True, $"{technique} never entered the saved cast state.");
                Assert.That(desiredPeak, Is.GreaterThan(0.82f),
                    $"{technique} never selected semantic slot {slot} strongly enough.");
                Assert.That(smallestOtherSumAtPeak, Is.LessThan(0.12f),
                    $"{technique} kept another semantic clip mixed into slot {slot}.");
                Assert.That(minimumHeadHeight, Is.GreaterThan(0.22f),
                    $"{technique} compressed the head into the torso.");
                Assert.That(maximumHeadPitch, Is.LessThan(65f),
                    $"{technique} turned the head toward the vertical axis.");
                Assert.That(strongestVisualDegrees, Is.GreaterThan(.08f),
                    $"{technique} did not consume the choreography channels visually.");
                Assert.That(strongestVisualPose.IsFinite, Is.True, technique.ToString());
                Assert.That(sawSynchronizedContact, Is.True,
                    $"{technique} advanced its semantic phase without reaching contact marker " +
                    $"{motion.timing.Contact:F2}.");
                visualPoses.Add(strongestVisualPose);
                yield return new WaitForSeconds(0.45f);
            }

            int distinctPoses = 0;
            for (int candidate = 0; candidate < visualPoses.Count; candidate++)
            {
                bool duplicate = false;
                for (int prior = 0; prior < candidate; prior++)
                    duplicate |= ChoreographyPoseDistance(visualPoses[candidate], visualPoses[prior]) < .08f;
                if (!duplicate) distinctPoses++;
            }
            Assert.That(distinctPoses, Is.GreaterThanOrEqualTo(9),
                "The placeholder/shared clips still collapse the semantic techniques to one silhouette.");
        }

        private static float ChoreographyPoseDistance(
            in EarthChoreographyPoseOffset left,
            in EarthChoreographyPoseOffset right) =>
            math.length(left.ChestEuler - right.ChestEuler) +
            math.length(left.HeadEuler - right.HeadEuler) +
            math.length(left.LeftShoulderEuler - right.LeftShoulderEuler) +
            math.length(left.RightShoulderEuler - right.RightShoulderEuler);

        [UnityTest]
        public IEnumerator WalkStopKeepsKneesFiniteAndAvoidsAOneFrameLegSnap()
        {
            foreach (Actor actor in _actors) actor.Input.Move = new float2(0f, 1f);
            yield return new WaitForSeconds(0.75f);

            float[] previousLeft = new float[_actors.Count];
            float[] previousRight = new float[_actors.Count];
            for (int index = 0; index < _actors.Count; index++)
            {
                EarthFootContactController feet = _actors[index].Presentation.FootContactController;
                previousLeft[index] = feet.LeftKneeAngleDegrees;
                previousRight[index] = feet.RightKneeAngleDegrees;
                _actors[index].Input.Move = float2.zero;
            }

            float maximumKneeStep = 0f;
            double deadline = Time.realtimeSinceStartupAsDouble + 1.1d;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return _frame;
                for (int index = 0; index < _actors.Count; index++)
                {
                    Actor actor = _actors[index];
                    EarthFootContactController feet = actor.Presentation.FootContactController;
                    float left = feet.LeftKneeAngleDegrees;
                    float right = feet.RightKneeAngleDegrees;
                    float leftStep = Mathf.Abs(left - previousLeft[index]);
                    float rightStep = Mathf.Abs(right - previousRight[index]);
                    EAMMBasePoseBridge bridge = actor.Bridge;
                    _walkStopFrames.Add(new WalkStopFrameDiagnostic
                    {
                        actor = actor.Presentation.name,
                        frame = Time.frameCount,
                        leftKnee = left,
                        rightKnee = right,
                        leftStep = leftStep,
                        rightStep = rightStep,
                        filteredSpeed = actor.Presentation.FilteredSpeed,
                        leftIkWeight = feet.LeftFootIkWeight,
                        rightIkWeight = feet.RightFootIkWeight,
                        leftReason = feet.LeftReason.ToString(),
                        rightReason = feet.RightReason.ToString(),
                        pelvisOffset = feet.PelvisOffsetMeters,
                        pelvisTarget = feet.PelvisTargetMeters,
                        leftPelvisRequest = feet.LeftPelvisRequestMeters,
                        rightPelvisRequest = feet.RightPelvisRequestMeters,
                        usesAuthoredIdleKnees = bridge != null && bridge.UsesAuthoredIdleKnees,
                        idleKneeEammWeight = bridge != null ? bridge.AppliedIdleKneeEammWeight : -1f,
                        sourcePoseFrame = bridge != null ? bridge.SourcePoseFrame : -1,
                        hasLocomotionQuery = bridge != null && bridge.HasLocomotionQuery,
                        locomotionQuery = bridge != null && bridge.IsLocomotionQuery
                    });
                    Assert.That(float.IsFinite(left) && float.IsFinite(right), Is.True);
                    // Vector3.Angle is unsigned and capped at 180 degrees, so a
                    // 178-degree per-frame ceiling cannot distinguish a natural
                    // heel-strike extension from backwards bending. Continuity is
                    // enforced here; the settled pose is checked below.
                    Assert.That(left, Is.InRange(.5f, 180f), $"{actor.Presentation.name}: invalid left-knee chain.");
                    Assert.That(right, Is.InRange(.5f, 180f), $"{actor.Presentation.name}: invalid right-knee chain.");
                    maximumKneeStep = Mathf.Max(maximumKneeStep, leftStep, rightStep);
                    previousLeft[index] = left;
                    previousRight[index] = right;
                    EarthAnimationPoseSample sample = actor.Probe.Latest;
                    _samples.Add(sample);
                    if (sample.leftContactWeight > 0.8f)
                        Assert.That(sample.leftFootError, Is.LessThan(0.18f));
                    if (sample.rightContactWeight > 0.8f)
                        Assert.That(sample.rightFootError, Is.LessThan(0.18f));
                }
            }
            Assert.That(maximumKneeStep, Is.LessThan(35f),
                "A walk-stop transition snapped a knee in one rendered frame.");
            foreach (Actor actor in _actors)
            {
                Assert.That(actor.Presentation.FilteredSpeed, Is.LessThan(0.35f),
                    $"{actor.Presentation.name}: locomotion did not settle after input release.");
                Assert.That(actor.Bridge.AppliedEammMasterWeight, Is.GreaterThan(0.95f),
                    $"{actor.Presentation.name}: the knee fix disabled the production EAMM base pose.");
                Assert.That(actor.Bridge.UsesAuthoredIdleKnees, Is.True,
                    $"{actor.Presentation.name}: the settled idle query did not return knee ownership to the Humanoid controller.");
                Assert.That(actor.Bridge.AppliedIdleKneeEammWeight, Is.LessThan(0.01f),
                    $"{actor.Presentation.name}: lower-leg ownership did not finish its bounded idle handoff.");
                EarthFootContactController feet = actor.Presentation.FootContactController;
                Assert.That(feet.LeftKneeAngleDegrees, Is.InRange(3f, 178f),
                    $"{actor.Presentation.name}: left knee remained straight/folded after stopping.");
                Assert.That(feet.RightKneeAngleDegrees, Is.InRange(3f, 178f),
                    $"{actor.Presentation.name}: right knee remained straight/folded after stopping.");
            }
        }

        [Serializable]
        private sealed class WalkStopDiagnosticReport
        {
            public string utc;
            public WalkStopFrameDiagnostic[] frames;
        }

        [Serializable]
        private sealed class WalkStopFrameDiagnostic
        {
            public string actor;
            public int frame;
            public float leftKnee;
            public float rightKnee;
            public float leftStep;
            public float rightStep;
            public float filteredSpeed;
            public float leftIkWeight;
            public float rightIkWeight;
            public string leftReason;
            public string rightReason;
            public float pelvisOffset;
            public float pelvisTarget;
            public float leftPelvisRequest;
            public float rightPelvisRequest;
            public bool usesAuthoredIdleKnees;
            public float idleKneeEammWeight;
            public int sourcePoseFrame;
            public bool hasLocomotionQuery;
            public bool locomotionQuery;
        }

        [Serializable]
        private sealed class MagicPhaseTrace
        {
            public float realtime;
            public int frame;
            public uint sequence;
            public string technique;
            public string phase;
            public int queued;
            public float layerWeight;
            public float handWeight;
        }
    }

    [DefaultExecutionOrder(32000)]
    public sealed class MagicBoneContinuityRecorder : MonoBehaviour
    {
        private static readonly int MotionTimeHash = Animator.StringToHash("EarthMotionTime");
        private static readonly int MotionTimeAHash = Animator.StringToHash("EarthMotionTimeA");
        private static readonly int MotionTimeBHash = Animator.StringToHash("EarthMotionTimeB");
        private Transform[] _bones;
        private Quaternion[] _previous;
        private EarthCharacterPoseController _pose;
        private HumanoidCharacterPresentation _presentation;
        private EarthAnimationDriver _driver;
        private int _magicLayer;
        private bool _hasPrevious;

        public float MaximumStepDegrees { get; private set; }
        public int SampleCount { get; private set; }
        public float AverageDeltaSeconds => SampleCount > 0 ? _totalDeltaSeconds / SampleCount : 0f;
        public float AverageAnimationDeltaSeconds =>
            SampleCount > 0 ? _totalAnimationDeltaSeconds / SampleCount : 0f;
        public float MaximumDeltaSeconds { get; private set; }
        private float _totalDeltaSeconds;
        private float _totalAnimationDeltaSeconds;

        public void Configure(
            Animator animator,
            HumanoidCharacterPresentation presentation,
            EarthCharacterPoseController pose,
            EarthAnimationDriver driver,
            int magicLayer)
        {
            HumanBodyBones[] ids =
            {
                HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Head,
                HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand
            };
            var resolved = new List<Transform>(ids.Length);
            foreach (HumanBodyBones id in ids)
            {
                Transform value = animator.GetBoneTransform(id);
                if (value != null) resolved.Add(value);
            }
            if (resolved.Count < 7)
                throw new InvalidOperationException(
                    "Humanoid magic continuity audit could not resolve the upper-body chain.");
            _bones = resolved.ToArray();
            _previous = new Quaternion[_bones.Length];
            _pose = pose;
            _presentation = presentation;
            _driver = driver;
            _magicLayer = magicLayer;
        }

        private void LateUpdate()
        {
            if (_bones == null || _driver == null || _pose == null) return;
            float delta = Mathf.Max(0f, Time.unscaledDeltaTime);
            _totalDeltaSeconds += delta;
            _totalAnimationDeltaSeconds += Mathf.Max(0f, Time.deltaTime);
            MaximumDeltaSeconds = Mathf.Max(MaximumDeltaSeconds, delta);
            SampleCount++;
            for (int index = 0; index < _bones.Length; index++)
            {
                Quaternion current = _bones[index].localRotation;
                if (_hasPrevious)
                {
                    float step = Quaternion.Angle(_previous[index], current);
                    MaximumStepDegrees = Mathf.Max(MaximumStepDegrees, step);
                    if (step > 30f)
                    {
                        AnimatorStateInfo state = _driver.GetCurrentAnimatorStateInfo(_magicLayer);
                        AnimatorStateInfo next = _driver.GetNextAnimatorStateInfo(_magicLayer);
                        Debug.Log(
                            $"MAGIC_BONE_STEP stage=LateUpdate frame={Time.frameCount} " +
                            $"bone={_bones[index].name} step={step:F3} " +
                            $"delta={Time.deltaTime:F5} unscaledDelta={Time.unscaledDeltaTime:F5} " +
                            $"sequence={_pose.LastAuthoritativeTick} " +
                            $"technique={_pose.CurrentRequest.Technique} " +
                            $"phase={_pose.CurrentRequest.Phase} state={state.fullPathHash} " +
                            $"next={next.fullPathHash} transition={_driver.IsInTransition(_magicLayer)} " +
                            $"clock={_driver.GetFloat(MotionTimeHash):F4} " +
                            $"clockA={_driver.GetFloat(MotionTimeAHash):F4} " +
                            $"clockB={_driver.GetFloat(MotionTimeBHash):F4} " +
                            $"layer={_driver.GetLayerWeight(_magicLayer):F3} " +
                            $"handIk={(_presentation != null ? _presentation.HandConstraintWeight : -1f):F3} " +
                            $"previous={_previous[index].eulerAngles} current={current.eulerAngles}");
                    }
                }
                _previous[index] = current;
            }
            _hasPrevious = true;
        }
    }

    [DefaultExecutionOrder(32000)]
    public sealed class GravityPreContactContinuityRecorder : MonoBehaviour
    {
        private Transform[] _bones;
        private Quaternion[] _previous;
        private HumanoidCharacterPresentation _presentation;
        private float _minimumClock;
        private float _maximumClock;
        private bool _hasPrevious;
        private bool _armed;
        private bool _sawFreshClock;
        private float _totalDelta;
        private readonly List<float> _clockTrace = new List<float>(32);

        public int SampleCount { get; private set; }
        public int ObservedFrameCount { get; private set; }
        public float MaximumStepDegrees { get; private set; }
        public float MaximumHandConstraintWeight { get; private set; }
        public float MinimumObservedClock { get; private set; } = float.PositiveInfinity;
        public float MaximumObservedClock { get; private set; } = float.NegativeInfinity;
        public float FreshMaximumObservedClock { get; private set; } = float.NegativeInfinity;
        public bool SawFreshClock => _sawFreshClock;
        public string ClockTrace => string.Join(",", _clockTrace.ConvertAll(value => value.ToString("F3")));
        public float AverageDeltaSeconds => SampleCount > 1
            ? _totalDelta / (SampleCount - 1)
            : 0f;

        public void BeginCapture()
        {
            _armed = true;
        }

        public void Configure(
            Animator animator,
            HumanoidCharacterPresentation presentation,
            float minimumClock,
            float maximumClock)
        {
            HumanBodyBones[] ids =
            {
                HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Head,
                HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand
            };
            var bones = new List<Transform>(ids.Length);
            foreach (HumanBodyBones id in ids)
            {
                Transform bone = animator.GetBoneTransform(id);
                if (bone != null) bones.Add(bone);
            }
            _bones = bones.ToArray();
            _previous = new Quaternion[_bones.Length];
            _presentation = presentation;
            _minimumClock = minimumClock;
            _maximumClock = maximumClock;
        }

        private void LateUpdate()
        {
            if (!_armed || _bones == null || _presentation == null) return;
            float clock = _presentation.MagicClipTime;
            ObservedFrameCount++;
            MinimumObservedClock = Mathf.Min(MinimumObservedClock, clock);
            MaximumObservedClock = Mathf.Max(MaximumObservedClock, clock);
            if (clock <= .1f) _sawFreshClock = true;
            if (_sawFreshClock)
                FreshMaximumObservedClock = Mathf.Max(FreshMaximumObservedClock, clock);
            if (_clockTrace.Count < 32) _clockTrace.Add(clock);
            if (clock < _minimumClock || clock > _maximumClock) return;
            MaximumHandConstraintWeight = Mathf.Max(
                MaximumHandConstraintWeight,
                _presentation.HandConstraintWeight);
            if (_hasPrevious) _totalDelta += Mathf.Max(0f, Time.deltaTime);
            for (int index = 0; index < _bones.Length; index++)
            {
                Quaternion current = _bones[index].localRotation;
                if (_hasPrevious)
                    MaximumStepDegrees = Mathf.Max(
                        MaximumStepDegrees,
                        Quaternion.Angle(_previous[index], current));
                _previous[index] = current;
            }
            _hasPrevious = true;
            SampleCount++;
        }
    }
}
