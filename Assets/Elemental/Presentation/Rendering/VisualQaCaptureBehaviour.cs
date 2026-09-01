using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Input.Gestures;
using Elemental.Runtime.World;
using Elemental.Runtime.Physics;
using Elemental.Runtime.Characters;
using Elemental.Presentation.VFX;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Magic;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Elemental.Presentation.Rendering
{
    [DisallowMultipleComponent]
    public sealed class VisualQaCaptureBehaviour : MonoBehaviour
    {
        private const int EvidenceWidth = 1920;
        private const int EvidenceHeight = 1080;
        [SerializeField, Min(1)] private int settleFrames = 90;

        private readonly FrameTiming[] _latestTiming = new FrameTiming[1];
        private string _requestedOutputPath;
        private int _successfulSupplementalCaptures;
        private Mvp01ProfilerEvidence _lastPerformanceEvidence;

        public bool IsPerformanceCaptureRunning { get; private set; }

        public bool BeginMvpPerformanceCapture(int frameCount = 600)
        {
            if (IsPerformanceCaptureRunning) return false;
            IsPerformanceCaptureRunning = true;
            StartCoroutine(CapturePerformanceSample(Mathf.Clamp(frameCount, 60, 1800)));
            return true;
        }

        public bool BeginMvpRescueEvidence()
        {
            if (!Application.isPlaying) return false;
            // Editor-driven evidence must own the coroutine lifetime; an inherited
            // command-line QA request would otherwise quit Play Mode underneath it.
            StopAllCoroutines();
            IsPerformanceCaptureRunning = false;
            _scenarioSucceeded = false;
            _lastPerformanceEvidence = null;
            _requestedOutputPath = Path.GetFullPath(Path.Combine(
                "BuildReports", "Mvp01RescueCurrent.png"));
            StartCoroutine(RunMvpRescueEvidence());
            return true;
        }

        public bool MvpRescueEvidenceSucceeded => _scenarioSucceeded;

        private IEnumerator RunMvpRescueEvidence()
        {
            yield return Demonstrate(VisualQaScenario.MvpRescue);
            if (_scenarioSucceeded) yield return CaptureFrameToPng(_requestedOutputPath);
            string statusPath = Path.ChangeExtension(_requestedOutputPath, ".json");
            File.WriteAllText(
                statusPath,
                $"{{\n  \"success\": {(_scenarioSucceeded ? "true" : "false")},\n" +
                $"  \"utc\": \"{DateTime.UtcNow:O}\"\n}}");
            Debug.Log($"[Elemental] Editor MVP rescue evidence completed: {_scenarioSucceeded}.");
        }

        private IEnumerator Start()
        {
            if (!VisualQaCaptureRequest.TryParse(Environment.GetCommandLineArgs(), out VisualQaCaptureRequest request))
                yield break;

            string fullPath = Path.GetFullPath(request.OutputPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                Debug.LogError("[Elemental] Visual QA output directory could not be resolved.");
                Application.Quit(3);
                yield break;
            }
            Directory.CreateDirectory(directory);
            _requestedOutputPath = fullPath;

            // These two deterministic proof paths exercise platform and combat
            // independently. Disable autonomous strikes before the settle window so
            // an unrelated valid KO cannot pre-empt their scripted sequence.
            if (request.Scenario == VisualQaScenario.MvpRescue ||
                request.Scenario == VisualQaScenario.Platform)
            {
                EarthMvpBotController botController = FindAnyObjectByType<EarthMvpBotController>();
                if (botController != null) botController.enabled = false;
            }
            if (request.Scenario == VisualQaScenario.Platform)
            {
                EarthCharacterImpactTarget[] impactTargets =
                    FindObjectsByType<EarthCharacterImpactTarget>(FindObjectsInactive.Exclude);
                for (int index = 0; index < impactTargets.Length; index++)
                    if (impactTargets[index].FighterId == EarthDuelFighterId.Player)
                        impactTargets[index].SuppressImpacts(30f);
                MagicInputController playerInput = FindAnyObjectByType<MagicInputController>();
                ActiveRagdollPuppet playerPuppet = playerInput != null
                    ? playerInput.GetComponent<ActiveRagdollPuppet>()
                    : null;
                playerPuppet?.SuppressImpacts(30f);
            }

            for (int frame = 0; frame < settleFrames; frame++) yield return null;

            if (request.DemonstrateMagic)
            {
                yield return Demonstrate(request.Scenario);
                if (!_scenarioSucceeded)
                {
                    Debug.LogError($"[Elemental] Visual QA scenario failed: {request.Scenario}.");
                    Application.Quit(5);
                    yield break;
                }
                if (request.Scenario == VisualQaScenario.EarthMaterialFracture)
                    yield return CapturePerformanceSample(45);
            }

            yield return CaptureFrameToPng(fullPath);
            yield return new WaitForSecondsRealtime(0.5f);

            bool captured = File.Exists(fullPath) && new FileInfo(fullPath).Length > 0;
            VoxelPlanetBehaviour planet = FindAnyObjectByType<VoxelPlanetBehaviour>();
            if (planet != null)
                Debug.Log($"[Elemental] Voxel render queue peak: {planet.PeakRenderQueueMilliseconds:0.00} ms; pending: {planet.PendingRenderCount}.");
            if (captured) Debug.Log($"[Elemental] Visual QA captured: {fullPath}");
            else Debug.LogError($"[Elemental] Visual QA capture failed: {fullPath}");
            Application.Quit(captured ? 0 : 4);
        }

        private bool _scenarioSucceeded;
        private PlanetMotor _animationLandingMotor;
        private Rigidbody _animationLandingBody;

        private IEnumerator Demonstrate(VisualQaScenario scenario)
        {
            MagicInputController input = FindAnyObjectByType<MagicInputController>();
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            GameObject proxyObject = GameObject.Find("Planet Collision Proxy");
            Collider proxy = proxyObject != null ? proxyObject.GetComponent<Collider>() : null;
            if (scenario != VisualQaScenario.AnimationLanding &&
                (input == null || camera == null || proxy == null)) yield break;

            Physics.SyncTransforms();
            List<float2> surfaceLine = scenario == VisualQaScenario.AnimationLanding
                ? new List<float2>()
                : FindSurfaceLine(camera, proxy);
            if (surfaceLine == null) yield break;

            if (scenario == VisualQaScenario.MvpRescue)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 120;
                PlanetMotor motor = input != null
                    ? input.GetComponent<PlanetMotor>()
                    : FindAnyObjectByType<PlanetMotor>();
                Rigidbody body = motor != null ? motor.GetComponent<Rigidbody>() : null;
                EarthPlatformPool platformPool = FindAnyObjectByType<EarthPlatformPool>();
                EarthArmorController armor = input != null
                    ? input.GetComponent<EarthArmorController>()
                    : null;
                EarthMvpDuelController duel = FindAnyObjectByType<EarthMvpDuelController>();
                EarthMvpBotController botController = FindAnyObjectByType<EarthMvpBotController>();
                if (motor == null || body == null || platformPool == null || armor == null ||
                    duel == null || proxy == null)
                    yield break;

                EarthCharacterImpactTarget playerImpact = input != null
                    ? input.GetComponent<EarthCharacterImpactTarget>()
                    : null;
                EarthCharacterImpactTarget botImpact = null;
                EarthCharacterImpactTarget[] impactTargets =
                    FindObjectsByType<EarthCharacterImpactTarget>(FindObjectsInactive.Exclude);
                for (int index = 0; index < impactTargets.Length; index++)
                {
                    EarthCharacterImpactTarget target = impactTargets[index];
                    if (target.FighterId == EarthDuelFighterId.Bot && target.Body != null)
                        botImpact = target;
                }
                if (playerImpact == null || botImpact == null) yield break;
                if (botController != null) botController.enabled = false;

                var scripted = motor.gameObject.AddComponent<VisualQaMotorInput>();
                motor.ConfigureInputSource(scripted);
                for (int tick = 0; tick < 45; tick++) yield return new WaitForFixedUpdate();
                scripted.Move = new float2(0.18f, 0.72f);
                for (int tick = 0; tick < 50; tick++) yield return new WaitForFixedUpdate();
                scripted.Move = float2.zero;

                VoxelPlanetBehaviour runtimePlanet = FindAnyObjectByType<VoxelPlanetBehaviour>();
                float terrainWarmupDeadline = Time.realtimeSinceStartup + 6f;
                while (runtimePlanet != null &&
                       (runtimePlanet.PendingRenderCount > 0 || runtimePlanet.PendingColliderCount > 0) &&
                       Time.realtimeSinceStartup < terrainWarmupDeadline)
                    yield return null;
                for (int warmFrame = 0; warmFrame < 45; warmFrame++) yield return null;

                Vector3 planetCenter = proxy.bounds.center;
                Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f
                    ? motor.LocalUp.normalized
                    : (body.worldCenterOfMass - planetCenter).normalized;
                Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
                if (forward.sqrMagnitude < 0.5f)
                    forward = Vector3.ProjectOnPlane(body.transform.forward, up).normalized;
                Vector3 right = Vector3.Cross(up, forward).normalized;
                Vector3 surface = proxy.ClosestPoint(body.worldCenterOfMass);
                EarthPlatformGeometry firstGeometry = BuildQaPlatformGeometry(
                    surface,
                    planetCenter,
                    forward,
                    right,
                    1.55f);
                EarthPlatform firstPlatform = platformPool.Acquire(in firstGeometry, 1.25f, 0.24f);
                for (int tick = 0; tick < 55; tick++) yield return new WaitForFixedUpdate();

                Vector3 secondCenter = proxy.ClosestPoint(surface + forward * 3.4f + right * 0.9f);
                EarthPlatformGeometry secondGeometry = BuildQaPlatformGeometry(
                    secondCenter,
                    planetCenter,
                    forward,
                    right,
                    1.35f);
                EarthPlatform secondPlatform = platformPool.Acquire(in secondGeometry, 1.05f, 0.22f);
                for (int tick = 0; tick < 55; tick++) yield return new WaitForFixedUpdate();
                yield return CaptureSupplementalFrame("two-platforms");

                bool armorStarted = armor.Begin();
                for (int tick = 0; tick < 45; tick++) yield return new WaitForFixedUpdate();
                yield return CaptureSupplementalFrame("armor-staged");
                bool armorStaged = armorStarted && armor.ActivePieceCount >= 12;
                // The proof needs to validate both a complete staged shell and the
                // canonical local-hit-to-knockout pipeline. Keeping the defensive
                // shell active consumes the deliberately tiny staged impact samples,
                // so end the already-captured armor phase atomically before starting
                // the independent hit-reaction sequence.
                armor.EndArmor(EarthArmorEndReason.Disabled);

                // Platform carry or the staged shell can legitimately trigger an
                // earlier safety respawn in a stressed editor run. Wait for the
                // canonical active state and its short post-respawn immunity to end
                // before proving the three-distinct-stone escalation contract.
                float playerReadyDeadline = Time.realtimeSinceStartup + 8f;
                while (duel.PlayerPhase != EarthDuelFighterPhase.Active &&
                       Time.realtimeSinceStartup < playerReadyDeadline)
                    yield return null;
                for (int tick = 0; tick < 50; tick++) yield return new WaitForFixedUpdate();

                EarthCharacterImpactResponse botResponse = botImpact.ApplyImpact(
                    botImpact.transform.position,
                    botImpact.transform.forward + botImpact.transform.up * 0.08f,
                    botImpact.Body.mass * 8.2f,
                    EarthCharacterImpactSourceKind.SurfNose,
                    0x5F00E101u,
                    8.2f,
                    1f,
                    0xE101u);
                for (int tick = 0; tick < 25; tick++) yield return new WaitForFixedUpdate();
                EarthCharacterImpactResponse firstStoneResponse = playerImpact.ApplyImpact(
                    playerImpact.transform.position,
                    playerImpact.transform.up + playerImpact.transform.right,
                    playerImpact.Body.mass * 2f,
                    EarthCharacterImpactSourceKind.BotProjectile,
                    0xB070E102u,
                    0f,
                    1f,
                    0xE102u);
                EarthCharacterImpactResponse secondStoneResponse = playerImpact.ApplyImpact(
                    playerImpact.transform.position + playerImpact.transform.right * 0.12f,
                    playerImpact.transform.up + playerImpact.transform.right,
                    playerImpact.Body.mass * 2f,
                    EarthCharacterImpactSourceKind.BotProjectile,
                    0xB070E103u,
                    0f,
                    1f,
                    0xE103u);
                EarthCharacterImpactResponse playerResponse = playerImpact.ApplyImpact(
                    playerImpact.transform.position + playerImpact.transform.right * 0.18f,
                    playerImpact.transform.up + playerImpact.transform.right,
                    playerImpact.Body.mass * 2f,
                    EarthCharacterImpactSourceKind.BotProjectile,
                    0xB070E104u,
                    0f,
                    1f,
                    0xE104u);
                for (int tick = 0; tick < 20; tick++) yield return new WaitForFixedUpdate();
                yield return CaptureSupplementalFrame("dual-ko");

                float respawnDeadline = Time.realtimeSinceStartup + 8f;
                while ((duel.BotPhase != EarthDuelFighterPhase.Active ||
                        duel.PlayerPhase != EarthDuelFighterPhase.Active) &&
                       Time.realtimeSinceStartup < respawnDeadline)
                {
                    if (botController != null) botController.enabled = false;
                    yield return null;
                }
                if (botController != null) botController.enabled = false;
                BeginMvpPerformanceCapture(720);
                while (IsPerformanceCaptureRunning) yield return null;
                bool performanceAccepted = _lastPerformanceEvidence != null &&
                    (Application.isEditor
                        ? _lastPerformanceEvidence.editorDiagnosticPassed
                        : _lastPerformanceEvidence.authoritativePassed);
                _scenarioSucceeded = firstPlatform != null && secondPlatform != null &&
                                     armorStaged &&
                                     botResponse == EarthCharacterImpactResponse.Knockout &&
                                     playerResponse == EarthCharacterImpactResponse.Knockout &&
                                     duel.BotKnockoutCount >= 1 && duel.PlayerKnockoutCount >= 1 &&
                                     duel.BotPhase == EarthDuelFighterPhase.Active &&
                                     duel.PlayerPhase == EarthDuelFighterPhase.Active &&
                                     performanceAccepted;
                Debug.Log($"[Elemental] MVP rescue QA: platforms={firstPlatform != null && secondPlatform != null}, " +
                          $"armor={armorStarted}/{armorStaged}/{armor.ActivePieceCount}, " +
                          $"responses={botResponse}/{firstStoneResponse}/{secondStoneResponse}/{playerResponse}, " +
                          $"knockouts={duel.BotKnockoutCount}/{duel.PlayerKnockoutCount}, " +
                          $"respawn={duel.BotPhase}/{duel.PlayerPhase}, " +
                          $"performance={performanceAccepted}.");
                yield break;
            }

            if (scenario == VisualQaScenario.AnimationLanding)
            {
                PlanetMotor sceneMotor = FindAnyObjectByType<PlanetMotor>();
                Rigidbody body = sceneMotor != null ? sceneMotor.GetComponent<Rigidbody>() : null;
                HumanoidCharacterPresentation presentation = sceneMotor != null
                    ? sceneMotor.GetComponentInChildren<HumanoidCharacterPresentation>(true)
                    : null;
                if (sceneMotor == null || body == null || presentation == null || camera == null)
                {
                    Debug.LogError($"[Elemental] Landing QA dependencies missing: " +
                                   $"motor={sceneMotor != null}, body={body != null}, " +
                                   $"presentation={presentation != null}, camera={camera != null}.");
                    yield break;
                }
                PrepareAnimationLandingCourt(camera, sceneMotor, body);
                while (!sceneMotor.HasStableSupport) yield return new WaitForFixedUpdate();
                CapsuleCollider sceneCapsule = sceneMotor.GetComponent<CapsuleCollider>();
                if (sceneCapsule == null) yield break;
                Vector3 courtUp = sceneMotor.LocalUp.normalized;
                Vector3 courtForward = Vector3.ProjectOnPlane(sceneMotor.FacingForward, courtUp).normalized;
                if (courtForward.sqrMagnitude < 0.1f)
                    courtForward = Vector3.ProjectOnPlane(sceneMotor.transform.forward, courtUp).normalized;
                Vector3 capsuleScale = sceneCapsule.transform.lossyScale;
                float capsuleRadius = sceneCapsule.radius * Mathf.Max(
                    Mathf.Abs(capsuleScale.x), Mathf.Abs(capsuleScale.z));
                float capsuleHalfHeight = Mathf.Max(
                    capsuleRadius,
                    sceneCapsule.height * 0.5f * Mathf.Abs(capsuleScale.y));
                Vector3 feet = sceneCapsule.transform.TransformPoint(sceneCapsule.center) -
                               courtUp * capsuleHalfHeight;
                var courtSupport = new GameObject("Animation Landing QA Support");
                int groundMaskValue = sceneMotor.GroundMask.value;
                int courtLayer = 0;
                for (int layer = 0; layer < 32; layer++)
                {
                    if ((groundMaskValue & (1 << layer)) == 0) continue;
                    courtLayer = layer;
                    break;
                }
                courtSupport.layer = courtLayer;
                courtSupport.transform.SetPositionAndRotation(
                    feet - courtUp * 0.02f,
                    Quaternion.LookRotation(courtForward, courtUp));
                BoxCollider courtCollider = courtSupport.AddComponent<BoxCollider>();
                courtCollider.size = new Vector3(8f, 0.20f, 8f);
                int courtHash = courtCollider.GetHashCode();
                uint courtSurfaceId = unchecked((uint)(courtHash == int.MinValue
                    ? int.MaxValue
                    : Mathf.Abs(courtHash)));
                if (courtSurfaceId == 0u) courtSurfaceId = 1u;
                Debug.Log($"[Elemental] Landing QA support: mask=0x{groundMaskValue:X8}, " +
                          $"layer={courtLayer}, surface={courtSurfaceId}.");
                body.position += courtUp * 0.12f;
                Physics.SyncTransforms();
                for (int settle = 0; settle < 8; settle++) yield return new WaitForFixedUpdate();
                _successfulSupplementalCaptures = 0;
                float[] startHeights = { 0.90f, 0.95f, 4.00f };
                float[] downwardSpeeds = { -1.5f, -1.3f, 16f };
                float[] planarSpeeds = { 0.25f, 5.0f, 0.25f };
                EarthLandingStyle[] expectedStyles =
                {
                    EarthLandingStyle.Soft,
                    EarthLandingStyle.Moving,
                    EarthLandingStyle.Hard
                };
                string[] labels = { "soft", "moving", "hard" };
                bool allStylesMatched = true;
                for (int run = 0; run < startHeights.Length; run++)
                {
                    while (!sceneMotor.HasStableSupport) yield return new WaitForFixedUpdate();
                    int runCaptureStart = _successfulSupplementalCaptures;
                    Vector3 up = sceneMotor.LocalUp.normalized;
                    Vector3 tangent = Vector3.ProjectOnPlane(sceneMotor.FacingForward, up).normalized;
                    body.position += up * startHeights[run];
                    body.linearVelocity = -up * downwardSpeeds[run] + tangent * planarSpeeds[run];
                    body.angularVelocity = Vector3.zero;
                    Physics.SyncTransforms();
                    sceneMotor.BeginExternalLaunch(5);
                    bool capturedPre = false;
                    bool capturedContact = false;
                    bool matchedExpectedStyle = false;
                    float minimumCandidateTime = float.PositiveInfinity;
                    float maximumCandidateImpact = 0f;
                    float minimumVerticalSpeed = float.PositiveInfinity;
                    bool sawRising = false;
                    bool sawFalling = false;
                    int guard = 0;
                    while (guard++ < 420 &&
                           (!capturedContact || _successfulSupplementalCaptures < runCaptureStart + 4))
                    {
                        yield return new WaitForFixedUpdate();
                        EarthLandingCandidateSnapshot landing = presentation.LandingCandidate;
                        float observedVertical = Vector3.Dot(body.linearVelocity, sceneMotor.LocalUp);
                        minimumVerticalSpeed = Mathf.Min(minimumVerticalSpeed, observedVertical);
                        if (landing.IsValid)
                        {
                            minimumCandidateTime = Mathf.Min(minimumCandidateTime, landing.TimeToContact);
                            maximumCandidateImpact = Mathf.Max(maximumCandidateImpact, landing.ImpactSpeed);
                        }
                        sawRising |= presentation.MotionPhase == EarthAnimationPhase.Rising;
                        sawFalling |= presentation.MotionPhase == EarthAnimationPhase.Falling;
                        matchedExpectedStyle |= presentation.LandingStyle == expectedStyles[run];
                        int runCaptureCount = _successfulSupplementalCaptures - runCaptureStart;
                        if (!capturedPre && presentation.MotionPhase == EarthAnimationPhase.PreLanding)
                        {
                            capturedPre = true;
                            yield return CaptureSupplementalFrame($"{labels[run]}-pre-a");
                            if (_successfulSupplementalCaptures - runCaptureStart == 1)
                                yield return CaptureSupplementalFrame($"{labels[run]}-pre-b");
                        }
                        else if (capturedPre && runCaptureCount == 1 && landing.IsValid && landing.TimeToContact <= 0.10f)
                        {
                            yield return CaptureSupplementalFrame($"{labels[run]}-pre-b");
                        }
                        else if (runCaptureCount == 2 && sceneMotor.HasStableSupport)
                        {
                            capturedContact = true;
                            yield return CaptureSupplementalFrame($"{labels[run]}-contact");
                        }
                        else if (capturedContact && runCaptureCount == 3 &&
                                 ((presentation.MotionPhase == EarthAnimationPhase.LandingRecovery &&
                                   presentation.MotionPhaseSeconds >= RecoveryCaptureDelay(presentation)) ||
                                  presentation.MotionPhase == EarthAnimationPhase.LocomotionLoop ||
                                  presentation.MotionPhase == EarthAnimationPhase.GroundedIdle))
                        {
                            yield return CaptureSupplementalFrame($"{labels[run]}-recovery");
                        }
                    }
                    EarthLandingCandidateSnapshot finalCandidate = presentation.LandingCandidate;
                    Debug.Log($"[Elemental] Landing QA {labels[run]}: frames={guard - 1}, " +
                              $"captures={_successfulSupplementalCaptures - runCaptureStart}/4, " +
                              $"phase={presentation.MotionPhase}, style={presentation.LandingStyle}, " +
                              $"candidate={finalCandidate.IsValid}, ttc={finalCandidate.TimeToContact:0.000}, " +
                              $"impact={finalCandidate.ImpactSpeed:0.00}, " +
                              $"minTtc={minimumCandidateTime:0.000}, maxImpact={maximumCandidateImpact:0.00}, " +
                              $"minVertical={minimumVerticalSpeed:0.00}, rising={sawRising}, falling={sawFalling}, " +
                              $"vertical={Vector3.Dot(body.linearVelocity, sceneMotor.LocalUp):0.00}, " +
                              $"support={sceneMotor.HasStableSupport}, styleMatched={matchedExpectedStyle}, " +
                              $"surface={finalCandidate.SurfaceId}, expectedSurface={courtSurfaceId}, " +
                              $"movingSupport={finalCandidate.MovingSupport}.");
                    allStylesMatched &= matchedExpectedStyle;
                    body.linearVelocity = Vector3.zero;
                    sceneMotor.SettleTangentialMotion();
                    for (int settle = 0; settle < 8; settle++) yield return new WaitForFixedUpdate();
                }
                _scenarioSucceeded = _successfulSupplementalCaptures == 12 && allStylesMatched;
                Destroy(courtSupport);
                _animationLandingMotor = null;
                _animationLandingBody = null;
                yield break;
            }

            if (scenario == VisualQaScenario.Dawn || scenario == VisualQaScenario.Night)
            {
                CelestialSystemBehaviour celestial = FindAnyObjectByType<CelestialSystemBehaviour>();
                if (celestial == null) yield break;
                celestial.SetTimeOfDayForQa(scenario == VisualQaScenario.Dawn ? 0.015f : 0.72f);
                _scenarioSucceeded = true;
                for (int frame = 0; frame < 8; frame++) yield return null;
                yield break;
            }

            if (scenario == VisualQaScenario.Meteor)
            {
                MeteorShowerBehaviour shower = FindAnyObjectByType<MeteorShowerBehaviour>();
                PlanetMotor sceneMotor = FindAnyObjectByType<PlanetMotor>();
                if (shower == null || sceneMotor == null) yield break;
                Vector3 center = proxy.bounds.center;
                Vector3 target = proxy.ClosestPoint(
                    sceneMotor.transform.position + sceneMotor.FacingForward * 6f +
                    camera.transform.right * 3.5f);
                Vector3 up = (target - center).normalized;
                _scenarioSucceeded = shower.SpawnForQa(
                    target + up * 13f - sceneMotor.FacingForward * 2f,
                    -up * 31f + sceneMotor.FacingForward * 5f,
                    0.72f);
                yield return new WaitForSecondsRealtime(0.48f);
                yield break;
            }

            if (scenario == VisualQaScenario.Armor)
            {
                EarthArmorController armor = input.GetComponent<EarthArmorController>();
                if (armor == null || !armor.Begin()) yield break;
                FrameArmorQaCamera(camera, armor, false);
                _successfulSupplementalCaptures = 0;
                // The last body plate starts 3.5 ms after each preceding plate; wait
                // through the stagger as well as the 0.30 s assembly motion.
                yield return new WaitForSecondsRealtime(0.55f);
                yield return CaptureSupplementalFrame("body-shell");
                // Open the gathered plates into a readable protective dome without
                // consuming the overscroll confirmation used by the radial release.
                for (int step = 0; step < 5; step++)
                    armor.ApplyWheel(120f, Time.unscaledTime + step * 0.04f);
                yield return new WaitForSecondsRealtime(0.42f);
                FrameArmorQaCamera(camera, armor, true);
                int piecesBeforeShot = armor.ControllablePieceCount;
                Rigidbody armorCaster = armor.GetComponent<Rigidbody>();
                Vector3 casterVelocityBeforeShot = armorCaster != null
                    ? armorCaster.linearVelocity
                    : Vector3.zero;
                bool fired = armor.FireNearest(camera.transform.forward);
                yield return new WaitForSecondsRealtime(0.08f);
                yield return CaptureSupplementalFrame("aimed-shot");
                bool casterStayedStable = armorCaster == null ||
                    (armorCaster.linearVelocity - casterVelocityBeforeShot).sqrMagnitude < 0.0001f;
                _scenarioSucceeded = armor.IsActive && armor.ActivePieceCount >= 12 &&
                                     armor.Phase01 > 0.3f && armor.Phase01 < 1f &&
                                     fired && armor.ControllablePieceCount == piecesBeforeShot - 1 &&
                                     casterStayedStable && _successfulSupplementalCaptures == 2;
                yield break;
            }

            if (scenario == VisualQaScenario.QuickStone)
            {
                MagicExecutor executor = FindAnyObjectByType<MagicExecutor>();
                if (executor == null || surfaceLine.Count == 0 ||
                    !input.SelectEarthAbility(EarthAbilityIds.PullRock)) yield break;
                var primePath = new List<float2>(1) { surfaceLine[0] };
                if (!input.TryCommitScreenPath(primePath, 0.18f)) yield break;
                yield return new WaitForSecondsRealtime(0.16f);
                EarthFragment stone = executor.HeldFragment;
                if (stone == null || stone.Body == null) yield break;
                Vector3 direction = Vector3.ProjectOnPlane(camera.transform.forward, stone.transform.up).normalized;
                if (direction.sqrMagnitude < 0.5f) direction = camera.transform.forward.normalized;
                bool fired = executor.ReleaseHeldEarthAtSpeed(direction, 70f, 0u, out Vector3 velocity);
                yield return new WaitForSecondsRealtime(0.10f);
                _scenarioSucceeded = fired && velocity.magnitude >= 30f && stone.Body != null &&
                                     stone.Body.linearVelocity.magnitude >= 20f;
                yield break;
            }

            if (scenario == VisualQaScenario.MageWalk)
            {
                PlanetMotor sceneMotor = FindAnyObjectByType<PlanetMotor>();
                Rigidbody body = sceneMotor != null ? sceneMotor.GetComponent<Rigidbody>() : null;
                Animator animator = sceneMotor != null ? sceneMotor.GetComponentInChildren<Animator>(true) : null;
                if (sceneMotor == null || body == null || animator == null || animator.runtimeAnimatorController == null)
                    yield break;
                HumanoidCharacterPresentation presentation = animator.GetComponent<HumanoidCharacterPresentation>();
                VisualQaMotorInput scripted = sceneMotor.gameObject.AddComponent<VisualQaMotorInput>();
                scripted.Move = new float2(0f, 1f);
                sceneMotor.ConfigureInputSource(scripted);
                _successfulSupplementalCaptures = 0;
                Vector3 start = body.position;
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (leftFoot == null) yield break;
                Vector3 firstFootLocal = animator.transform.InverseTransformPoint(leftFoot.position);
                Vector3 lateFootLocal = firstFootLocal;
                float maximumFootTravel = 0f;
                float lateFootTravel = 0f;
                float maximumUncommandedTurn = 0f;
                float maximumLocomotionFootIk = 0f;
                float firstLocomotionTime = -1f;
                float lastLocomotionTime = -1f;
                int gaitCaptureIndex = 0;
                for (int frame = 0; frame < 150; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    yield return null;
                    Vector3 currentFootLocal = animator.transform.InverseTransformPoint(leftFoot.position);
                    maximumFootTravel = Mathf.Max(maximumFootTravel,
                        Vector3.Distance(firstFootLocal, currentFootLocal));
                    if (frame == 80) lateFootLocal = currentFootLocal;
                    if (frame > 80)
                        lateFootTravel = Mathf.Max(lateFootTravel,
                            Vector3.Distance(lateFootLocal, currentFootLocal));
                    if (presentation != null)
                    {
                        maximumUncommandedTurn = Mathf.Max(
                            maximumUncommandedTurn,
                            Mathf.Abs(presentation.FilteredTurn));
                        if (presentation.PoseController != null)
                            maximumLocomotionFootIk = Mathf.Max(
                                maximumLocomotionFootIk,
                                presentation.PoseController.FootIkWeight);
                    }

                    EarthAnimationGraph animationGraph = presentation != null
                        ? presentation.AnimationGraph
                        : null;
                    AnimatorStateInfo state = animationGraph != null && animationGraph.IsActive
                        ? animationGraph.GetCurrentAnimatorStateInfo(0)
                        : animator.GetCurrentAnimatorStateInfo(0);
                    if (state.IsName("Locomotion"))
                    {
                        if (firstLocomotionTime < 0f) firstLocomotionTime = state.normalizedTime;
                        lastLocomotionTime = state.normalizedTime;
                    }

                    if (frame == 82 || frame == 100 || frame == 118)
                    {
                        yield return CaptureSupplementalFrame($"gait-{(char)('a' + gaitCaptureIndex)}");
                        gaitCaptureIndex++;
                    }
                }
                bool activeClipsLoop = true;
                var clips = new List<AnimatorClipInfo>(4);
                EarthAnimationGraph finalAnimationGraph = presentation != null
                    ? presentation.AnimationGraph
                    : null;
                if (finalAnimationGraph != null && finalAnimationGraph.IsActive)
                    finalAnimationGraph.GetCurrentAnimatorClipInfo(0, clips);
                else
                    animator.GetCurrentAnimatorClipInfo(0, clips);
                for (int index = 0; index < clips.Count; index++)
                    if (clips[index].weight > 0.01f) activeClipsLoop &= clips[index].clip.isLooping;
                AnimatorStateInfo finalState = finalAnimationGraph != null && finalAnimationGraph.IsActive
                    ? finalAnimationGraph.GetCurrentAnimatorStateInfo(0)
                    : animator.GetCurrentAnimatorStateInfo(0);
                float locomotionCycles = firstLocomotionTime >= 0f && lastLocomotionTime >= firstLocomotionTime
                    ? lastLocomotionTime - firstLocomotionTime
                    : 0f;
                float travel = Vector3.Distance(start, body.position);
                _scenarioSucceeded = travel > 3f && maximumFootTravel > 0.05f &&
                                     lateFootTravel > 0.015f && locomotionCycles > 1.1f &&
                                     finalState.IsName("Locomotion") && clips.Count > 0 &&
                                     activeClipsLoop && !animator.stabilizeFeet &&
                                     maximumUncommandedTurn <= 0.025f &&
                                     maximumLocomotionFootIk >= 0.35f &&
                                     maximumLocomotionFootIk <= 0.90f && gaitCaptureIndex == 3 &&
                                     _successfulSupplementalCaptures == 3;
                Debug.Log($"[Elemental] Continuous gait QA: travel={travel:0.000} m, " +
                          $"foot={maximumFootTravel:0.000}, lateFoot={lateFootTravel:0.000}, " +
                          $"cycles={locomotionCycles:0.00}, state={finalState.fullPathHash}, looping={activeClipsLoop}, " +
                          $"ghostTurn={maximumUncommandedTurn:0.000}, footIk={maximumLocomotionFootIk:0.000}, " +
                          $"mecanimFeet={animator.stabilizeFeet}.");
                yield break;
            }

            if (scenario == VisualQaScenario.Wall || scenario == VisualQaScenario.WallCollapse ||
                scenario == VisualQaScenario.WallDebris ||
                scenario == VisualQaScenario.EarthMaterialFracture ||
                scenario == VisualQaScenario.WallPush)
            {
                _scenarioSucceeded = input.SelectEarthAbility(Elemental.Simulation.Magic.EarthAbilityIds.LineWall) &&
                                     input.TryCommitScreenPath(surfaceLine, 0.8f);
                if (scenario == VisualQaScenario.WallDebris)
                {
                    yield return new WaitForSecondsRealtime(1.05f);
                    EarthWall wall = FindAnyObjectByType<EarthWall>();
                    if (wall == null || !wall.ApplyRockImpact(
                            wall.transform.position + (wall.transform.up * wall.Height * 0.15f),
                            camera.transform.forward,
                            6200f))
                    {
                        _scenarioSucceeded = false;
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(2.25f);
                    yield break;
                }
                if (scenario == VisualQaScenario.WallPush)
                {
                    yield return new WaitForSecondsRealtime(1.05f);
                    EarthWall wall = FindAnyObjectByType<EarthWall>();
                    PlanetMotor sceneMotor = FindAnyObjectByType<PlanetMotor>();
                    Rigidbody caster = sceneMotor != null ? sceneMotor.GetComponent<Rigidbody>() : null;
                    MagicExecutor executor = input.EarthExecutor;
                    if (wall == null || caster == null || executor == null || wall.SurfaceCollider == null)
                    {
                        _scenarioSucceeded = false;
                        yield break;
                    }
                    Vector3 wallStart = wall.transform.position;
                    Vector3 casterStart = caster.position;
                    Vector3 push = Vector3.ProjectOnPlane(
                        wall.transform.position - caster.position,
                        wall.SurfaceUp).normalized;
                    if (push.sqrMagnitude < 0.5f) push = wall.transform.forward;
                    if (!executor.TryBeginVectorField(
                            wall.SurfaceCollider, wall.Body, wall.transform.position, push))
                    {
                        _scenarioSucceeded = false;
                        yield break;
                    }
                    executor.UpdateVectorField(push, 1f);
                    for (int frame = 0; frame < 70; frame++) yield return new WaitForFixedUpdate();
                    executor.ReleaseVectorField();
                    for (int frame = 0; frame < 8; frame++) yield return new WaitForFixedUpdate();
                    float wallTravel = Vector3.Distance(wallStart, wall.transform.position);
                    float casterTravel = Vector3.Distance(casterStart, caster.position);
                    _scenarioSucceeded = wallTravel >= 2f && casterTravel < 0.75f;
                    Debug.Log($"[Elemental] Wall push QA: wall={wallTravel:0.000} m, caster={casterTravel:0.000} m.");
                    yield break;
                }
                if (scenario == VisualQaScenario.WallCollapse)
                {
                    yield return new WaitForSecondsRealtime(1.05f);
                    EarthWall wall = FindAnyObjectByType<EarthWall>();
                    _scenarioSucceeded = wall != null && wall.ApplyRockImpact(
                        wall.transform.position + (wall.transform.up * wall.Height * 0.12f),
                        camera.transform.forward,
                        6200f);
                    yield return new WaitForSecondsRealtime(1.15f);
                    yield break;
                }
                if (scenario == VisualQaScenario.EarthMaterialFracture)
                {
                    yield return new WaitForSecondsRealtime(1.05f);
                    EarthWall wall = FindAnyObjectByType<EarthWall>();
                    CelestialSystemBehaviour celestial = FindAnyObjectByType<CelestialSystemBehaviour>();
                    celestial?.SetTimeOfDayForQa(0.23f);
                    if (wall == null) yield break;
                    Vector3 shotTarget = wall.transform.position + wall.transform.up * wall.Height * 0.36f;
                    Vector3 viewNormal = wall.transform.forward;
                    if (Vector3.Dot(camera.transform.position - shotTarget, viewNormal) < 0f) viewNormal = -viewNormal;
                    Vector3 viewSide = wall.transform.right;
                    if (Vector3.Dot(camera.transform.position - shotTarget, viewSide) < 0f) viewSide = -viewSide;
                    Elemental.Presentation.Camera.PlanetCameraRig qaCameraRig =
                        FindAnyObjectByType<Elemental.Presentation.Camera.PlanetCameraRig>();
                    if (qaCameraRig != null) qaCameraRig.enabled = false;
                    float wallLength = Vector3.Distance(wall.Start, wall.End);
                    camera.transform.position = shotTarget + viewNormal * (wallLength * 1.55f + 2.2f) +
                                                viewSide * (wallLength * 0.22f) + wall.transform.up * 1.1f;
                    camera.transform.rotation = Quaternion.LookRotation(
                        shotTarget - camera.transform.position,
                        wall.transform.up);
                    camera.fieldOfView = 54f;
                    PlanetMotor visibleMotor = FindAnyObjectByType<PlanetMotor>();
                    if (visibleMotor != null)
                    {
                        Renderer[] actorRenderers = visibleMotor.GetComponentsInChildren<Renderer>(true);
                        for (int index = 0; index < actorRenderers.Length; index++)
                            actorRenderers[index].enabled = false;
                    }
                    yield return null;
                    Vector3 impactPoint = wall != null
                        ? wall.transform.position + wall.transform.up * wall.Height * 0.18f
                        : Vector3.zero;
                    _scenarioSucceeded = wall != null && wall.ApplyRockImpact(
                        impactPoint, -viewNormal, 58f);
                    // Freeze only the QA tableau after canonical fracture has run. This keeps the
                    // exterior/interior face contract readable instead of photographing a random
                    // later point in the ballistic collapse.
                    var qaPieces = new IEarthPhysicalTarget[48];
                    int qaPieceCount = wall.CopyActiveTargetsNonAlloc(qaPieces);
                    EarthPieceRuntime showcasePiece = null;
                    float showcaseDistance = float.MaxValue;
                    for (int index = 0; index < qaPieceCount; index++)
                    {
                        if (!(qaPieces[index] is EarthPieceRuntime piece) || piece.Body == null) continue;
                        piece.Body.linearVelocity = Vector3.zero;
                        piece.Body.angularVelocity = Vector3.zero;
                        piece.Body.isKinematic = true;
                        float distance = (piece.Body.worldCenterOfMass - impactPoint).sqrMagnitude;
                        if (distance < showcaseDistance)
                        {
                            showcaseDistance = distance;
                            showcasePiece = piece;
                        }
                    }
                    if (showcasePiece != null)
                        showcasePiece.Body.position += viewNormal * 0.82f + viewSide * 0.28f + wall.transform.up * 0.12f;
                    MagicExecutor executor = input.EarthExecutor;
                    if (executor != null && wall != null)
                    {
                        executor.Events.Emit(new Elemental.Simulation.Magic.EarthImpactEvent(
                            900u,
                            wall.WallId,
                            1200f,
                            90000f,
                            520f,
                            18f,
                            (float3)impactPoint,
                            (float3)viewNormal,
                            Elemental.Simulation.Magic.EarthImpactMaterialKind.Structure));
                    }
                    yield return new WaitForSecondsRealtime(0.55f);
                    MeshRenderer pieceRenderer = wall != null && wall.FirstFracturePiece != null
                        ? wall.FirstFracturePiece.GetComponent<MeshRenderer>()
                        : null;
                    EarthSurfaceScarPool scarPool = FindAnyObjectByType<EarthSurfaceScarPool>();
                    bool distinctSurfaces = pieceRenderer != null &&
                                            pieceRenderer.sharedMaterials.Length == 2 &&
                                            pieceRenderer.sharedMaterials[0] != pieceRenderer.sharedMaterials[1];
                    _scenarioSucceeded &= distinctSurfaces && scarPool != null && scarPool.ActiveCount > 0;
                    Debug.Log($"[Elemental] Earth material QA wall={wall?.WallId ?? 0}, " +
                              $"pieces={wall?.ActiveFracturePieceCount ?? 0}, " +
                              $"submeshes={pieceRenderer?.sharedMaterials.Length ?? 0}, " +
                              $"distinct={distinctSurfaces}, scars={scarPool?.ActiveCount ?? 0}.");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(1.05f);
                yield break;
            }

            if (scenario == VisualQaScenario.GravityWell)
            {
                EarthWallPool wallPool = FindAnyObjectByType<EarthWallPool>();
                MagicExecutor executor = input.EarthExecutor;
                PlanetMotor gripMotor = FindAnyObjectByType<PlanetMotor>();
                VoxelPlanetBehaviour voxel = FindAnyObjectByType<VoxelPlanetBehaviour>();
                if (wallPool == null || executor == null || gripMotor == null || voxel == null) yield break;
                Vector3 planetCenter = voxel.transform.position;
                Vector3 radial = (gripMotor.transform.position - planetCenter).normalized;
                Vector3 forward = Vector3.ProjectOnPlane(gripMotor.FacingForward, radial).normalized;
                Vector3 baseDirection = (radial + forward * (7f / voxel.Radius)).normalized;
                Vector3 side = Vector3.Cross(baseDirection, forward).normalized;
                Vector3 start = planetCenter +
                                (baseDirection - side * (1.7f / voxel.Radius)).normalized * voxel.Radius;
                Vector3 end = planetCenter +
                              (baseDirection + side * (1.7f / voxel.Radius)).normalized * voxel.Radius;
                EarthWall wall = wallPool.Acquire(start, end, planetCenter, 2.1f, 0.55f);
                _scenarioSucceeded = wall != null;
                yield return new WaitForSecondsRealtime(1.05f);
                Collider wallCollider = wall != null ? wall.GetComponent<Collider>() : null;
                if (wall == null || wallCollider == null || executor == null) yield break;
                Vector3 focus = wall.transform.position + wall.transform.up * 3.2f + side * 2f;
                _scenarioSucceeded = executor.TryBeginGravityWell(wallCollider, focus, wall.transform.up) &&
                                     wall.ApplyRockImpact(focus, camera.transform.forward, 6200f);
                // Fracture happens after the grip has remembered the whole structure.
                // Its source collider is disabled, so only the latched-source path can register these pieces.
                yield return new WaitForSecondsRealtime(1.2f);
                _scenarioSucceeded &= executor.IsGravityWellActive && executor.GravityWellCapturedCount >= 8;
                var sourceTargets = new IEarthPhysicalTarget[48];
                int sourceCount = wall.CopyActiveTargetsNonAlloc(sourceTargets);
                Debug.Log($"[Elemental] Gravity QA wall={wall.WallId}, fractured={wall.IsCollapsing}, " +
                          $"active={wall.ActiveFracturePieceCount}, source={sourceCount}, " +
                          $"captured={executor.GravityWellCapturedCount}/{executor.GravityWellMaximumCapturedTargets}, " +
                          $"executorActive={executor.isActiveAndEnabled}.");
                yield break;
            }

            if (scenario == VisualQaScenario.Reassembly)
            {
                EarthWallPool wallPool = FindAnyObjectByType<EarthWallPool>();
                MagicExecutor executor = input.EarthExecutor;
                PlanetMotor repairMotor = FindAnyObjectByType<PlanetMotor>();
                VoxelPlanetBehaviour voxel = FindAnyObjectByType<VoxelPlanetBehaviour>();
                if (wallPool == null || executor == null || repairMotor == null || voxel == null) yield break;
                Vector3 planetCenter = voxel.transform.position;
                Vector3 radial = (repairMotor.transform.position - planetCenter).normalized;
                Vector3 forward = Vector3.ProjectOnPlane(repairMotor.FacingForward, radial).normalized;
                Vector3 baseDirection = (radial + forward * (7f / voxel.Radius)).normalized;
                Vector3 side = Vector3.Cross(baseDirection, forward).normalized;
                Vector3 start = planetCenter +
                                (baseDirection - side * (1.9f / voxel.Radius)).normalized * voxel.Radius;
                Vector3 end = planetCenter +
                              (baseDirection + side * (1.9f / voxel.Radius)).normalized * voxel.Radius;
                EarthWall wall = wallPool.Acquire(start, end, planetCenter, 2.6f, 0.58f);
                yield return new WaitForSecondsRealtime(1.05f);
                if (wall == null || !wall.ApplyRockImpact(
                        wall.transform.position + wall.transform.up * 0.2f,
                        camera.transform.forward,
                        6200f)) yield break;
                yield return new WaitForSecondsRealtime(0.18f);
                Collider pieceCollider = wall.FirstFracturePiece != null
                    ? wall.FirstFracturePiece.GetComponent<Collider>()
                    : null;
                Vector3 focus = wall.transform.position + wall.transform.up * 2.2f;
                _scenarioSucceeded = pieceCollider != null &&
                                     executor.TryBeginGravityWell(pieceCollider, focus, wall.transform.up);
                EarthReassemblyController repair = wall.Reassembly;
                float repairDeadline = Time.realtimeSinceStartup + 28f;
                while (repair != null && repair.IsRepairing && repair.WeldedPieceCount < 1 &&
                       Time.realtimeSinceStartup < repairDeadline)
                    yield return null;
                _scenarioSucceeded &= repair != null && wall != null &&
                                      repair.SelectedPieceCount == wall.StructureRuntime.PieceCount &&
                                      (repair.WeldedPieceCount >= 1 || !wall.IsCollapsing);
                Debug.Log($"[Elemental] Reassembly QA wall={wall.WallId}, " +
                           $"selected={repair?.SelectedPieceCount ?? 0}, " +
                           $"welded={repair?.WeldedPieceCount ?? 0}, " +
                           $"progress={(repair?.Progress01 ?? 0f):0.00}, active={repair?.IsRepairing ?? false}, " +
                           $"piece={repair?.CurrentPieceIndex ?? -1}, phase={repair?.CurrentPiecePhase}, " +
                           $"error={repair?.CurrentPiecePositionError ?? 0f:0.000}, " +
                           $"speed={repair?.CurrentPieceSpeed ?? 0f:0.000}, retry={repair?.CurrentPieceRetryCount ?? 0}.");
                yield break;
            }

            if (scenario == VisualQaScenario.Platform)
            {
                EarthPlatformPool pool = FindAnyObjectByType<EarthPlatformPool>();
                PlanetMotor motor = input != null
                    ? input.GetComponent<PlanetMotor>()
                    : FindAnyObjectByType<PlanetMotor>();
                Rigidbody rider = motor != null ? motor.GetComponent<Rigidbody>() : null;
                CapsuleCollider capsule = motor != null ? motor.GetComponent<CapsuleCollider>() : null;
                if (pool == null || motor == null || rider == null || capsule == null) yield break;

                // Keep this visual proof focused on platform carry/locomotion. Combat
                // KO is exercised separately by the MVP rescue scenario.
                EarthMvpBotController botController = FindAnyObjectByType<EarthMvpBotController>();
                if (botController != null) botController.enabled = false;
                motor.GetComponent<EarthCharacterImpactTarget>()?.SuppressImpacts(20f);
                motor.GetComponent<ActiveRagdollPuppet>()?.SuppressImpacts(20f);

                Vector3 center = proxy.bounds.center;
                Vector3 up = (rider.worldCenterOfMass - center).normalized;
                Vector3 surface = proxy.ClosestPoint(rider.worldCenterOfMass);
                Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
                if (forward.sqrMagnitude < 0.5f) forward = Vector3.Cross(up, Vector3.right).normalized;
                Vector3 right = Vector3.Cross(up, forward).normalized;
                var path = new List<float3>(4)
                {
                    ToFloat3(surface - right * 1.65f - forward * 1.65f),
                    ToFloat3(surface + right * 1.65f - forward * 1.65f),
                    ToFloat3(surface + right * 1.65f + forward * 1.65f),
                    ToFloat3(surface - right * 1.65f + forward * 1.65f)
                };
                EarthPlatformGeometry geometry = EarthPlatformGeometrySolver.Build(path, ToFloat3(center));
                float initialRadius = Vector3.Distance(rider.worldCenterOfMass, center);
                EarthPlatform platform = pool.Acquire(in geometry, 1.4f, 0.24f);
                if (platform == null) yield break;
                yield return new WaitForFixedUpdate();
                bool immediateRider = motor.MovingSurfaceId == platform.SurfaceId;
                float minimumClearance = float.PositiveInfinity;
                for (int tick = 0; tick < 65; tick++)
                {
                    yield return new WaitForFixedUpdate();
                    float radius = capsule.radius * Mathf.Max(
                        Mathf.Abs(motor.transform.lossyScale.x),
                        Mathf.Abs(motor.transform.lossyScale.z));
                    float halfHeight = Mathf.Max(
                        radius,
                        capsule.height * 0.5f * Mathf.Abs(motor.transform.lossyScale.y));
                    Vector3 feet = motor.transform.TransformPoint(capsule.center) - up * halfHeight;
                    minimumClearance = Mathf.Min(minimumClearance,
                        Vector3.Dot(feet - platform.SurfaceTopPoint, up));
                    if (tick == 18) yield return CaptureSupplementalFrame("rising");
                }
                float lift = Vector3.Distance(rider.worldCenterOfMass, center) - initialRadius;
                VisualQaMotorInput scripted = motor.gameObject.AddComponent<VisualQaMotorInput>();
                motor.ConfigureInputSource(scripted);
                Vector3 walkStart = rider.position;
                scripted.Move = new float2(0f, 0.55f);
                for (int tick = 0; tick < 12; tick++) yield return new WaitForFixedUpdate();
                scripted.Move = float2.zero;
                float walk = Vector3.ProjectOnPlane(rider.position - walkStart, up).magnitude;
                yield return CaptureSupplementalFrame("walking");

                EarthPillarMobility pillarMobility = motor.GetComponent<EarthPillarMobility>();
                bool launched = pillarMobility != null && pillarMobility.BeginCharge();
                if (launched)
                {
                    yield return new WaitForSecondsRealtime(0.18f);
                    launched = pillarMobility.ReleaseCharge();
                }
                bool descending = false;
                bool landed = false;
                float descentClearance = float.PositiveInfinity;
                bool capturedAirborne = false;
                for (int tick = 0; launched && tick < 240; tick++)
                {
                    yield return new WaitForFixedUpdate();
                    float verticalSpeed = Vector3.Dot(rider.linearVelocity, up);
                    float clearance = Vector3.Dot(
                        motor.SupportFeetPoint(up) - platform.SurfaceTopPoint,
                        up);
                    if (verticalSpeed < -0.25f) descending = true;
                    if (descending)
                    {
                        descentClearance = Mathf.Min(descentClearance, clearance);
                        if (!capturedAirborne)
                        {
                            capturedAirborne = true;
                            yield return CaptureSupplementalFrame("pillar-airborne");
                        }
                    }
                    if (descending && motor.HasStableSupport && clearance < 0.25f)
                    {
                        landed = true;
                        break;
                    }
                }
                GameObject launchPillar = null;
                int activeLaunchChips = 0;
                GameObject feedbackRoot = GameObject.Find("Earth Pillar Feedback");
                if (feedbackRoot != null)
                {
                    Transform[] feedbackChildren = feedbackRoot.GetComponentsInChildren<Transform>(true);
                    for (int index = 0; index < feedbackChildren.Length; index++)
                    {
                        if (feedbackChildren[index].name == "Rising Earth Pillar")
                            launchPillar = feedbackChildren[index].gameObject;
                        if (feedbackChildren[index].name.StartsWith("Lift Ground Chip") &&
                            feedbackChildren[index].gameObject.activeSelf)
                            activeLaunchChips++;
                    }
                }
                bool pillarRetreated = launchPillar != null && !launchPillar.activeSelf &&
                                       activeLaunchChips == 0;
                _scenarioSucceeded = immediateRider && !platform.IsFractured && lift > 0.35f &&
                                     minimumClearance > -0.08f && walk > 0.12f && launched &&
                                     landed && descentClearance > -0.14f && pillarRetreated &&
                                     motor.HasStableSupport;
                Debug.Log($"[Elemental] Platform rider QA: immediate={immediateRider}, " +
                          $"lift={lift:0.000} m, walk={walk:0.000} m, " +
                          $"riseClearance={minimumClearance:0.000} m, " +
                          $"descentClearance={descentClearance:0.000} m, landed={landed}, " +
                          $"pillarRetreated={pillarRetreated}, chips={activeLaunchChips}, " +
                          $"surface={motor.MovingSurfaceId}, stable={motor.HasStableSupport}, " +
                          $"fractured={platform.IsFractured}.");
                yield break;
            }

            if (scenario == VisualQaScenario.PillarWave)
            {
                EarthPillarWavePool pool = FindAnyObjectByType<EarthPillarWavePool>();
                PlanetMotor motor = FindAnyObjectByType<PlanetMotor>();
                Rigidbody body = motor != null ? motor.GetComponent<Rigidbody>() : null;
                if (pool != null && motor != null && body != null)
                {
                    Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : motor.transform.up;
                    int count = pool.Launch(
                        body.worldCenterOfMass - (up * 1.25f),
                        up,
                        motor.FacingForward,
                        0.45f,
                        1f,
                        body);
                    _scenarioSucceeded = count >= 40;
                }
                // Capture the travelling state: the near rows have already sunk while
                // the distinct outer crest is reaching full height.
                yield return new WaitForSecondsRealtime(1.35f);
                yield break;
            }

            if (scenario == VisualQaScenario.LandingCushion)
            {
                Material earthMaterial = FindEarthMaterial();
                PlanetMotor sceneMotor = FindAnyObjectByType<PlanetMotor>();
                Rigidbody sceneBody = sceneMotor != null ? sceneMotor.GetComponent<Rigidbody>() : null;
                if (sceneMotor == null || sceneBody == null) yield break;
                GameObject actor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                actor.name = "Visual QA Falling Body";
                actor.GetComponent<MeshRenderer>().enabled = false;
                Vector3 center = proxy.bounds.center;
                Vector3 target = sceneBody.worldCenterOfMass + (sceneMotor.FacingForward * 4f);
                Vector3 surface = proxy.ClosestPoint(target);
                Vector3 up = (surface - center).normalized;
                actor.transform.position = surface + (up * 6f);
                Rigidbody body = actor.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.linearVelocity = -up * 12f;
                PlanetMotor motor = actor.AddComponent<PlanetMotor>();
                motor.BeginExternalLaunch(90);
                motor.enabled = false;
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual QA Cushion";
                visual.GetComponent<MeshRenderer>().sharedMaterial = earthMaterial;
                Destroy(visual.GetComponent<Collider>());
                EarthLandingCushion cushion = actor.AddComponent<EarthLandingCushion>();
                cushion.Configure(body, motor, null, proxy, null, visual.transform);
                _scenarioSucceeded = cushion.BeginHold();
                yield return new WaitForSecondsRealtime(0.36f);
                _scenarioSucceeded &= cushion.LastPrediction.Valid;
                yield break;
            }

            float2 pullStart = surfaceLine[0];
            var pullStroke = new List<float2>(2)
            {
                pullStart,
                pullStart + new float2(4f, 74f)
            };
            if (!input.SelectEarthAbility(Elemental.Simulation.Magic.EarthAbilityIds.PullRock)) yield break;
            if (scenario == VisualQaScenario.PullPreview)
            {
                _scenarioSucceeded = input.TryPreviewScreenPath(pullStroke, 0.8f);
                for (int frame = 0; frame < 3; frame++) yield return null;
                yield break;
            }

            if (!input.TryCommitScreenPath(pullStroke, 0.8f)) yield break;
            for (int frame = 0; frame < 30; frame++) yield return null;
            if (scenario == VisualQaScenario.PullHeld || scenario == VisualQaScenario.MageCast)
            {
                _scenarioSucceeded = true;
                yield break;
            }

            if (scenario != VisualQaScenario.Throw) yield break;
            if (!input.SelectEarthAbility(Elemental.Simulation.Magic.EarthAbilityIds.FlickThrow)) yield break;
            var flickStroke = new List<float2>(2)
            {
                new float2(Screen.width * 0.44f, Screen.height * 0.48f),
                new float2(Screen.width * 0.62f, Screen.height * 0.55f)
            };
            input.TryPreviewScreenPath(flickStroke, 0.18f);
            _scenarioSucceeded = input.TryCommitScreenPath(flickStroke, 0.18f);
            for (int frame = 0; frame < 4; frame++) yield return null;
        }

        private IEnumerator CaptureSupplementalFrame(string suffix)
        {
            if (string.IsNullOrWhiteSpace(_requestedOutputPath)) yield break;
            if (_animationLandingMotor != null && _animationLandingBody != null)
            {
                FrameAnimationLandingCamera(
                    UnityEngine.Camera.main,
                    _animationLandingMotor,
                    _animationLandingBody);
                HumanoidCharacterPresentation presentation =
                    _animationLandingMotor.GetComponentInChildren<HumanoidCharacterPresentation>(true);
                Animator qaAnimator = presentation != null ? presentation.Animator : null;
                Transform hips = qaAnimator != null && qaAnimator.isHuman
                    ? qaAnimator.GetBoneTransform(HumanBodyBones.Hips)
                    : null;
                AnimatorStateInfo state = qaAnimator != null
                    ? qaAnimator.GetCurrentAnimatorStateInfo(0)
                    : default;
                Debug.Log($"[Elemental] Landing frame {suffix}: " +
                          $"body={_animationLandingBody.worldCenterOfMass}, " +
                          $"visual={presentation?.transform.position}, hips={hips?.position}, " +
                          $"state={state.fullPathHash}, time={state.normalizedTime:0.000}, " +
                          $"transition={qaAnimator != null && qaAnimator.IsInTransition(0)}.");
            }
            string directory = Path.GetDirectoryName(_requestedOutputPath);
            string name = Path.GetFileNameWithoutExtension(_requestedOutputPath);
            string extension = Path.GetExtension(_requestedOutputPath);
            string path = Path.Combine(directory ?? string.Empty, $"{name}-{suffix}{extension}");
            DateTime previousWrite = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            yield return CaptureFrameToPng(path);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline &&
                   (!File.Exists(path) || File.GetLastWriteTimeUtc(path) <= previousWrite))
                yield return null;
            if (!File.Exists(path) || new FileInfo(path).Length == 0 ||
                File.GetLastWriteTimeUtc(path) <= previousWrite)
            {
                Debug.LogError($"[Elemental] Supplemental gait capture failed: {path}");
                yield break;
            }
            _successfulSupplementalCaptures++;
            Debug.Log($"[Elemental] Supplemental gait captured: {path}");
        }

        private void PrepareAnimationLandingCourt(
            UnityEngine.Camera camera,
            PlanetMotor motor,
            Rigidbody body)
        {
            _animationLandingMotor = motor;
            _animationLandingBody = body;

            // Landing evidence must show the animated player, not the nearby
            // combat silhouettes used by the impact court. Keep this render-only
            // isolation local to the dedicated QA process.
            EarthCombatDummy[] dummies = FindObjectsByType<EarthCombatDummy>(
                FindObjectsInactive.Include);
            for (int index = 0; index < dummies.Length; index++)
                if (dummies[index] != null) dummies[index].gameObject.SetActive(false);
            GameObject landmarks = GameObject.Find("Earth Diorama Landmarks");
            if (landmarks != null) landmarks.SetActive(false);

            Elemental.Presentation.Camera.PlanetCameraRig legacy =
                FindAnyObjectByType<Elemental.Presentation.Camera.PlanetCameraRig>();
            if (legacy != null) legacy.enabled = false;
            Elemental.Presentation.Camera.EarthCinemachineCameraController localRig =
                FindAnyObjectByType<Elemental.Presentation.Camera.EarthCinemachineCameraController>();
            if (localRig != null) localRig.enabled = false;
            Unity.Cinemachine.CinemachineBrain brain = camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            FrameAnimationLandingCamera(camera, motor, body);
        }

        private static void FrameAnimationLandingCamera(
            UnityEngine.Camera camera,
            PlanetMotor motor,
            Rigidbody body)
        {
            if (camera == null || motor == null || body == null) return;
            Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : body.transform.up;
            Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            if (forward.sqrMagnitude < 0.5f)
                forward = Vector3.ProjectOnPlane(body.transform.forward, up).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 focus = body.worldCenterOfMass + up * 0.28f;
            Vector3 position = focus - forward * 4.4f + right * 1.55f + up * 0.82f;
            camera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(focus - position, up));
            camera.fieldOfView = 39f;
        }

        private static float RecoveryCaptureDelay(HumanoidCharacterPresentation presentation)
        {
            CharacterPresentationProfile profile = presentation != null ? presentation.Profile : null;
            if (profile == null) return 0.10f;
            float recovery = presentation.LandingStyle switch
            {
                EarthLandingStyle.Moving => profile.MovingLandingRecovery,
                EarthLandingStyle.Hard => profile.HardLandingRecovery,
                _ => profile.SoftLandingRecovery
            };
            return recovery * 0.72f;
        }

        private static void FrameArmorQaCamera(
            UnityEngine.Camera camera,
            EarthArmorController armor,
            bool expanded)
        {
            if (camera == null || armor == null) return;
            PlanetMotor motor = FindAnyObjectByType<PlanetMotor>();
            Rigidbody body = motor != null ? motor.GetComponent<Rigidbody>() : null;
            if (motor == null || body == null) return;

            // This is evidence framing, not gameplay camera behaviour. Freeze both
            // camera drivers so the explicitly rendered QA frame can show whether
            // stones really trace the limbs instead of hiding the result in a wide
            // action composition.
            Elemental.Presentation.Camera.PlanetCameraRig legacy =
                FindAnyObjectByType<Elemental.Presentation.Camera.PlanetCameraRig>();
            if (legacy != null) legacy.enabled = false;
            Elemental.Presentation.Camera.EarthCinemachineCameraController localRig =
                FindAnyObjectByType<Elemental.Presentation.Camera.EarthCinemachineCameraController>();
            if (localRig != null) localRig.enabled = false;
            Unity.Cinemachine.CinemachineBrain brain = camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            GameObject landmarks = GameObject.Find("Earth Diorama Landmarks");
            if (landmarks != null) landmarks.SetActive(false);

            Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : body.transform.up;
            Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            if (forward.sqrMagnitude < 0.5f)
                forward = Vector3.ProjectOnPlane(body.transform.forward, up).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 focus = body.worldCenterOfMass + up * (expanded ? 0.62f : 0.18f);
            float back = expanded ? 6.1f : 3.15f;
            float side = expanded ? 1.3f : 1.05f;
            float lift = expanded ? 1.25f : 0.48f;
            camera.transform.SetPositionAndRotation(
                focus - forward * back + right * side + up * lift,
                Quaternion.LookRotation(
                    focus - (focus - forward * back + right * side + up * lift),
                    up));
            camera.fieldOfView = expanded ? 49f : 42f;
        }

        private static IEnumerator CaptureFrameToPng(string path)
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null) yield break;
            int width = EvidenceWidth;
            int height = EvidenceHeight;
            RenderTexture target = RenderTexture.GetTemporary(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            camera.targetTexture = target;
            target.Create();
            // A hidden batchmode player does not guarantee a normal backbuffer camera
            // pass. Render the URP camera explicitly into the owned target so a black
            // PNG can never masquerade as visual evidence.
            camera.Render();
            yield return null;
            RenderTexture.active = target;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            pixels.Apply(false, false);
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            Destroy(pixels);
        }

        private sealed class VisualQaMotorInput : MonoBehaviour, IPlanetMotorInputSource
        {
            public float2 Move;
            public PlanetMotorCommand SampleCommand(uint tick) => new PlanetMotorCommand(tick, Move, false);
        }

        private static EarthPlatformGeometry BuildQaPlatformGeometry(
            Vector3 surfaceCenter,
            Vector3 planetCenter,
            Vector3 forward,
            Vector3 right,
            float halfExtent)
        {
            var path = new List<float3>(4)
            {
                ToFloat3(surfaceCenter - right * halfExtent - forward * halfExtent),
                ToFloat3(surfaceCenter + right * halfExtent - forward * halfExtent),
                ToFloat3(surfaceCenter + right * halfExtent + forward * halfExtent),
                ToFloat3(surfaceCenter - right * halfExtent + forward * halfExtent)
            };
            return EarthPlatformGeometrySolver.Build(path, ToFloat3(planetCenter));
        }

        private IEnumerator CapturePerformanceSample(int frameCount)
        {
            var totalSamples = new double[frameCount];
            var cpuSamples = new double[frameCount];
            var gpuSamples = new double[frameCount];
            var footContactSamples = new double[frameCount];
            double cpuTotal = 0d;
            double gpuTotal = 0d;
            double cpuMaximum = 0d;
            double gpuMaximum = 0d;
            int count = 0;
            int totalCount = 0;
            int gcSampleFrames = 0;
            int steadyStateGcFramesOverZero = 0;
            long steadyStateMaximumGcBytesInFrame = 0L;
            int footContactFrameSamples = 0;
            int footContactMissingFrames = 0;
            int footContactTotalInvocations = 0;
            int footContactMinimumInvocations = int.MaxValue;
            const int warmupFrames = 60;
            string captureId = Guid.NewGuid().ToString("N");
            bool isEditor = Application.isEditor;
            bool isBatchMode = Application.isBatchMode;
            string captureMode = isEditor
                ? "editor-diagnostic-camera-rt"
                : "standalone-game-backbuffer";
            string renderSurface = isEditor ? "persistent-camera-render-texture" : "game-backbuffer";
            int renderWidth = 0;
            int renderHeight = 0;
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            RenderTexture diagnosticTarget = null;
            RenderTexture previousTarget = camera != null ? camera.targetTexture : null;
            int previousScreenWidth = Screen.width;
            int previousScreenHeight = Screen.height;
            FullScreenMode previousFullScreenMode = Screen.fullScreenMode;
            ProfilerRecorder footContactRecorder = default;
            ProfilerRecorder cpuFrameRecorder = default;
            ProfilerRecorder gpuFrameRecorder = default;
            Mvp01RuntimeRenderAudit runtimeRenderAudit = null;
            EarthFootContactController[] footControllers =
                FindObjectsByType<EarthFootContactController>(FindObjectsInactive.Exclude);
            int activeFootControllerCount = 0;
            for (int index = 0; index < footControllers.Length; index++)
                if (footControllers[index] != null && footControllers[index].isActiveAndEnabled)
                    activeFootControllerCount++;

            try
            {
                if (isEditor)
                {
                    if (camera != null)
                    {
                        diagnosticTarget = new RenderTexture(
                            EvidenceWidth,
                            EvidenceHeight,
                            24,
                            RenderTextureFormat.ARGB32,
                            RenderTextureReadWrite.Default)
                        {
                            name = "MVP Performance 1920x1080 Diagnostic"
                        };
                        diagnosticTarget.Create();
                        camera.targetTexture = diagnosticTarget;
                        renderWidth = diagnosticTarget.width;
                        renderHeight = diagnosticTarget.height;
                    }
                }
                else
                {
                    Screen.SetResolution(EvidenceWidth, EvidenceHeight, FullScreenMode.Windowed);
                    for (int frame = 0; frame < 120 &&
                         (Screen.width != EvidenceWidth || Screen.height != EvidenceHeight); frame++)
                        yield return null;
                    renderWidth = Screen.width;
                    renderHeight = Screen.height;
                }

                for (int frame = 0; frame < warmupFrames; frame++) yield return null;

                ProfilerRecorderOptions recorderOptions =
                    ProfilerRecorderOptions.WrapAroundWhenCapacityReached |
                    ProfilerRecorderOptions.SumAllSamplesInFrame;
                footContactRecorder = new ProfilerRecorder(
                    "Elemental.Character.FootContact",
                    1,
                    recorderOptions);
                cpuFrameRecorder = new ProfilerRecorder(
                    ProfilerCategory.Internal,
                    "Main Thread",
                    1,
                    recorderOptions);
                gpuFrameRecorder = new ProfilerRecorder(
                    ProfilerCategory.Render,
                    "GPU Frame Time",
                    1,
                    recorderOptions);
                if (footContactRecorder.Valid) footContactRecorder.Start();
                if (cpuFrameRecorder.Valid) cpuFrameRecorder.Start();
                if (gpuFrameRecorder.Valid) gpuFrameRecorder.Start();

                // Discard the arming frame so recorder construction and first-use
                // bookkeeping can never contaminate the 720-frame evidence window.
                yield return null;
                if (footContactRecorder.Valid)
                    TryCopyProfilerSample(ref footContactRecorder, out _);
                if (cpuFrameRecorder.Valid)
                    TryCopyProfilerSample(ref cpuFrameRecorder, out _);
                if (gpuFrameRecorder.Valid)
                    TryCopyProfilerSample(ref gpuFrameRecorder, out _);
                long gcWindowStart = GC.GetAllocatedBytesForCurrentThread();

                for (int frame = 0; frame < frameCount; frame++)
                {
                    yield return null;
                    long gcWindowEnd = GC.GetAllocatedBytesForCurrentThread();
                    long allocatedBytes = Math.Max(0L, gcWindowEnd - gcWindowStart);
                    gcSampleFrames++;
                    if (allocatedBytes > 0L) steadyStateGcFramesOverZero++;
                    steadyStateMaximumGcBytesInFrame = Math.Max(
                        steadyStateMaximumGcBytesInFrame,
                        allocatedBytes);
                    totalSamples[totalCount++] = Time.unscaledDeltaTime * 1000.0;

                    ProfilerRecorderSample footSample = default;
                    if (footContactRecorder.Valid)
                        TryCopyProfilerSample(ref footContactRecorder, out footSample);
                    int footInvocationsThisFrame = (int)Math.Min(
                        int.MaxValue,
                        footSample.Count);
                    if (footInvocationsThisFrame > 0)
                    {
                        footContactSamples[footContactFrameSamples++] =
                            footSample.Value / 1000000.0;
                        footContactTotalInvocations += footInvocationsThisFrame;
                        footContactMinimumInvocations = Math.Min(
                            footContactMinimumInvocations,
                            footInvocationsThisFrame);
                    }
                    else
                    {
                        footContactMissingFrames++;
                    }

                    double fallback = totalSamples[totalCount - 1];
                    ProfilerRecorderSample cpuSample = default;
                    if (cpuFrameRecorder.Valid)
                        TryCopyProfilerSample(ref cpuFrameRecorder, out cpuSample);
                    double cpuMilliseconds = cpuSample.Value / 1000000.0;
                    double cpu = double.IsFinite(cpuMilliseconds) &&
                                 cpuMilliseconds > 0.0 && cpuMilliseconds < 1000.0
                        ? cpuMilliseconds
                        : fallback;
                    ProfilerRecorderSample gpuSample = default;
                    if (gpuFrameRecorder.Valid)
                        TryCopyProfilerSample(ref gpuFrameRecorder, out gpuSample);
                    double gpuMilliseconds = gpuSample.Value / 1000000.0;
                    double gpu = double.IsFinite(gpuMilliseconds) &&
                                 gpuMilliseconds > 0.0 && gpuMilliseconds < 1000.0
                        ? gpuMilliseconds
                        : 0.0;
                    cpuSamples[count] = cpu;
                    gpuSamples[count] = gpu;
                    cpuTotal += cpu;
                    gpuTotal += gpu;
                    cpuMaximum = Math.Max(cpuMaximum, cpu);
                    gpuMaximum = Math.Max(gpuMaximum, gpu);
                    count++;
                    // Begin the next allocation window only after every evidence-
                    // harness read above. This prevents the meter from measuring
                    // its own profiler interop and reports the yielded game frame.
                    gcWindowStart = GC.GetAllocatedBytesForCurrentThread();
                }

                runtimeRenderAudit = CaptureRuntimeRenderAudit(camera);
            }
            finally
            {
                if (footContactRecorder.Valid) footContactRecorder.Dispose();
                if (cpuFrameRecorder.Valid) cpuFrameRecorder.Dispose();
                if (gpuFrameRecorder.Valid) gpuFrameRecorder.Dispose();
                if (camera != null) camera.targetTexture = previousTarget;
                if (diagnosticTarget != null)
                {
                    diagnosticTarget.Release();
                    Destroy(diagnosticTarget);
                }
                if (!isEditor &&
                    (previousScreenWidth != Screen.width ||
                     previousScreenHeight != Screen.height ||
                     previousFullScreenMode != Screen.fullScreenMode))
                {
                    Screen.SetResolution(
                        previousScreenWidth,
                        previousScreenHeight,
                        previousFullScreenMode);
                }
                IsPerformanceCaptureRunning = false;
            }

            double totalP95 = Percentile(totalSamples, totalCount, 0.95);
            double cpuP95 = Percentile(cpuSamples, count, 0.95);
            double gpuP95 = Percentile(gpuSamples, count, 0.95);
            double footP50 = Percentile(footContactSamples, footContactFrameSamples, 0.50);
            double footP95 = Percentile(footContactSamples, footContactFrameSamples, 0.95);
            double footP99 = Percentile(footContactSamples, footContactFrameSamples, 0.99);
            double footMaximum = Percentile(footContactSamples, footContactFrameSamples, 1.0);
            EarthPlatformPool platformPool = FindAnyObjectByType<EarthPlatformPool>();
            EarthPlatform[] platforms = FindObjectsByType<EarthPlatform>(FindObjectsInactive.Include);
            double preparationPeak = 0.0;
            for (int index = 0; index < platforms.Length; index++)
                preparationPeak = Math.Max(
                    preparationPeak,
                    platforms[index].PeakPreparationSliceMilliseconds);

            var report = new Mvp01ProfilerEvidence
            {
                schema = "mvp-performance-evidence-v2",
                unityVersion = Application.unityVersion,
                utc = DateTime.UtcNow.ToString("O"),
                captureId = captureId,
                mode = captureMode,
                renderSurface = renderSurface,
                isEditor = isEditor,
                isBatchMode = isBatchMode,
                requestedFrameSamples = frameCount,
                warmupFrames = warmupFrames,
                totalFrameSamples = totalCount,
                frameTimingSamples = count,
                renderWidth = renderWidth,
                renderHeight = renderHeight,
                totalFrameP95Milliseconds = totalP95,
                cpuFrameAverageMilliseconds = count > 0 ? cpuTotal / count : 0.0,
                cpuFrameP95Milliseconds = cpuP95,
                cpuFrameMaximumMilliseconds = cpuMaximum,
                gpuFrameAverageMilliseconds = count > 0 ? gpuTotal / count : 0.0,
                gpuFrameP95Milliseconds = gpuP95,
                gpuFrameMaximumMilliseconds = gpuMaximum,
                gpuTimingAvailable = count == frameCount && gpuMaximum > 0.0,
                gcMeasurementMode = "main-thread-allocated-bytes-yield-window",
                gcSampleFrames = gcSampleFrames,
                steadyStateGcFramesOverZero = steadyStateGcFramesOverZero,
                steadyStateMaximumGcBytesInFrame = steadyStateMaximumGcBytesInFrame,
                activeFootControllerCount = activeFootControllerCount,
                footContactFrameSamples = footContactFrameSamples,
                footContactMissingFrames = footContactMissingFrames,
                footContactTotalInvocations = footContactTotalInvocations,
                footContactMinimumInvocations = footContactMinimumInvocations == int.MaxValue
                    ? 0
                    : footContactMinimumInvocations,
                footContactP50Milliseconds = footP50,
                footContactP95Milliseconds = footP95,
                footContactP99Milliseconds = footP99,
                footContactMaximumMilliseconds = footMaximum,
                acquireSolidPeakMilliseconds = platformPool != null
                    ? platformPool.PeakAcquireSolidMilliseconds
                    : 0.0,
                fracturePreparationPeakMilliseconds = preparationPeak,
                runtimeRenderAudit = runtimeRenderAudit
            };
            report.EvaluateGates();
            _lastPerformanceEvidence = report;
            string reportPath = ResolvePerformanceReportPath();
            if (!string.IsNullOrWhiteSpace(reportPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                Debug.Log($"[Elemental] MVP profiler report: {reportPath}");
            }
            Debug.Log($"[Elemental] MVP frame timing samples={count}, " +
                      $"total p95={totalP95:0.00} ms, " +
                      $"CPU avg={report.cpuFrameAverageMilliseconds:0.00} ms p95={cpuP95:0.00} ms max={cpuMaximum:0.00} ms, " +
                      $"GPU avg={report.gpuFrameAverageMilliseconds:0.00} ms p95={gpuP95:0.00} ms max={gpuMaximum:0.00} ms, " +
                      $"GC frames={steadyStateGcFramesOverZero}/{gcSampleFrames}, " +
                      $"foot p95={footP95:0.000} ms coverage={footContactFrameSamples}/{frameCount} " +
                      $"invocations>={report.footContactMinimumInvocations}, " +
                      $"AcquireSolid peak={report.acquireSolidPeakMilliseconds:0.00} ms, " +
                      $"fracture slice peak={preparationPeak:0.00} ms, " +
                      $"renderAudit={report.runtimeRenderAuditPassed}, " +
                      $"editorDiagnostic={report.editorDiagnosticPassed}, " +
                      $"authoritative={report.authoritativePassed}.");
        }

        private static unsafe bool TryCopyProfilerSample(
            ref ProfilerRecorder recorder,
            out ProfilerRecorderSample sample)
        {
            ProfilerRecorderSample local = default;
            int copied = recorder.CopyTo(&local, 1, true);
            sample = local;
            return copied > 0;
        }

        private static Mvp01RuntimeRenderAudit CaptureRuntimeRenderAudit(
            UnityEngine.Camera camera)
        {
            var audit = new Mvp01RuntimeRenderAudit
            {
                utc = DateTime.UtcNow.ToString("O"),
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                activeColorSpace = QualitySettings.activeColorSpace.ToString(),
                qualityLevelIndex = QualitySettings.GetQualityLevel(),
                qualityLevelName = QualitySettings.names.Length > QualitySettings.GetQualityLevel()
                    ? QualitySettings.names[QualitySettings.GetQualityLevel()]
                    : string.Empty,
                qualityPipelineAsset = QualitySettings.renderPipeline != null
                    ? QualitySettings.renderPipeline.name
                    : "<graphics-default>",
                activePipelineAsset = GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline.name
                    : "<built-in>",
                qualityAntiAliasingSamples = QualitySettings.antiAliasing,
                qualityShadows = QualitySettings.shadows.ToString(),
                qualityShadowResolution = QualitySettings.shadowResolution.ToString(),
                qualityShadowCascades = QualitySettings.shadowCascades,
                qualityShadowDistance = QualitySettings.shadowDistance,
                cameraName = camera != null ? camera.name : string.Empty,
                cameraAllowHdr = camera != null && camera.allowHDR,
                cameraAllowMsaa = camera != null && camera.allowMSAA
            };

            UniversalRenderPipelineAsset pipeline =
                GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) pipeline = UniversalRenderPipeline.asset;
            if (pipeline != null)
            {
                audit.universalPipelineAsset = pipeline.name;
                audit.pipelineSupportsHdr = pipeline.supportsHDR;
                audit.pipelineMsaaSamples = pipeline.msaaSampleCount;
                audit.pipelineRenderScale = pipeline.renderScale;
                audit.pipelineDepthTexture = pipeline.supportsCameraDepthTexture;
                audit.pipelineOpaqueTexture = pipeline.supportsCameraOpaqueTexture;
                audit.pipelineShadowDistance = pipeline.shadowDistance;
                audit.pipelineShadowCascades = pipeline.shadowCascadeCount;
                audit.pipelineMainLightShadowAtlas = pipeline.mainLightShadowmapResolution;
            }

            UniversalAdditionalCameraData cameraData = camera != null
                ? camera.GetUniversalAdditionalCameraData()
                : null;
            if (cameraData != null)
            {
                audit.activeRendererType = cameraData.scriptableRenderer != null
                    ? cameraData.scriptableRenderer.GetType().FullName
                    : string.Empty;
                audit.activeRendererIndex = pipeline != null &&
                                            ReferenceEquals(
                                                cameraData.scriptableRenderer,
                                                pipeline.GetRenderer(0))
                    ? 0
                    : -1;
                audit.cameraRenderType = cameraData.renderType.ToString();
                audit.cameraStackCount = cameraData.renderType == CameraRenderType.Base
                    ? cameraData.cameraStack.Count
                    : 0;
                audit.cameraPostProcessing = cameraData.renderPostProcessing;
                audit.cameraRequiresDepthTexture = cameraData.requiresDepthTexture;
                audit.cameraRequiresOpaqueTexture = cameraData.requiresColorTexture;
                audit.cameraRendersShadows = cameraData.renderShadows;
                audit.cameraStopNaN = cameraData.stopNaN;
                audit.cameraDithering = cameraData.dithering;
                audit.cameraAntialiasing = cameraData.antialiasing.ToString();
                audit.cameraAntialiasingQuality = cameraData.antialiasingQuality.ToString();
            }

            ScriptableRendererData[] rendererDataCandidates =
                Resources.FindObjectsOfTypeAll<ScriptableRendererData>();
            var rendererDataNames = new List<string>(rendererDataCandidates.Length);
            for (int index = 0; index < rendererDataCandidates.Length; index++)
            {
                ScriptableRendererData rendererData = rendererDataCandidates[index];
                if (rendererData != null && !rendererDataNames.Contains(rendererData.name))
                    rendererDataNames.Add(rendererData.name);
            }
            rendererDataNames.Sort(StringComparer.Ordinal);
            audit.loadedRendererDataAssets = string.Join(",", rendererDataNames);

            ScriptableRendererFeature[] features =
                Resources.FindObjectsOfTypeAll<ScriptableRendererFeature>();
            var featureStates = new List<string>(features.Length);
            for (int index = 0; index < features.Length; index++)
            {
                ScriptableRendererFeature feature = features[index];
                if (feature == null) continue;
                featureStates.Add($"{feature.name}:{feature.GetType().Name}:active={feature.isActive}");
                if (feature is ScreenSpaceAmbientOcclusion)
                {
                    audit.ssaoFeatureFound = true;
                    audit.ssaoFeatureActive |= feature.isActive;
                    audit.ssaoFeatureName = feature.name;
                }
            }
            featureStates.Sort(StringComparer.Ordinal);
            audit.loadedRendererFeatures = string.Join("|", featureStates);
            audit.ssaoGlobalKeywordEnabled =
                Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            audit.ssaoTextureBound =
                Shader.GetGlobalTexture("_ScreenSpaceOcclusionTexture") != null;

            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null ||
                        material.name.IndexOf(
                            "ArenaSandstone",
                            StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    audit.arenaMaterialName = material.name;
                    audit.arenaMaterialShader = material.shader != null
                        ? material.shader.name
                        : string.Empty;
                    audit.arenaMaterialSsaoKeyword =
                        material.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
                    rendererIndex = renderers.Length;
                    break;
                }
            }

            Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Exclude);
            var activeVolumes = new List<string>(volumes.Length);
            for (int index = 0; index < volumes.Length; index++)
            {
                Volume volume = volumes[index];
                if (volume == null || !volume.isActiveAndEnabled || volume.weight <= 0f)
                    continue;
                string profileName = volume.sharedProfile != null
                    ? volume.sharedProfile.name
                    : "<runtime-profile>";
                activeVolumes.Add(
                    $"{volume.name}:priority={volume.priority:0.###}:weight={volume.weight:0.###}:" +
                    $"global={volume.isGlobal}:profile={profileName}");
            }
            activeVolumes.Sort(StringComparer.Ordinal);
            audit.activeVolumes = string.Join("|", activeVolumes);

            VolumeStack stack = VolumeManager.instance != null
                ? VolumeManager.instance.stack
                : null;
            if (stack != null)
            {
                ColorAdjustments color = stack.GetComponent<ColorAdjustments>();
                WhiteBalance balance = stack.GetComponent<WhiteBalance>();
                DepthOfField depthOfField = stack.GetComponent<DepthOfField>();
                if (color != null)
                {
                    audit.resolvedColorAdjustments = true;
                    audit.resolvedPostExposure = color.postExposure.value;
                    audit.resolvedContrast = color.contrast.value;
                    audit.resolvedSaturation = color.saturation.value;
                }
                if (balance != null)
                {
                    audit.resolvedWhiteBalance = true;
                    audit.resolvedTemperature = balance.temperature.value;
                    audit.resolvedTint = balance.tint.value;
                }
                if (depthOfField != null)
                {
                    audit.resolvedDepthOfField = true;
                    audit.resolvedDepthOfFieldMode = depthOfField.mode.value.ToString();
                    audit.resolvedFocusDistance = depthOfField.focusDistance.value;
                    audit.resolvedAperture = depthOfField.aperture.value;
                    audit.resolvedFocalLength = depthOfField.focalLength.value;
                }
            }

            audit.Evaluate();
            return audit;
        }

        private string ResolvePerformanceReportPath()
        {
            string reportName = Application.isEditor
                ? "Mvp01ProfilerEditorDiagnosticLatest.json"
                : "Mvp01Profiler.json";
            if (!string.IsNullOrWhiteSpace(_requestedOutputPath))
            {
                string requestedDirectory = Path.GetDirectoryName(_requestedOutputPath);
                if (!string.IsNullOrWhiteSpace(requestedDirectory))
                    return Path.Combine(requestedDirectory, reportName);
            }
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrWhiteSpace(projectRoot)
                ? string.Empty
                : Path.Combine(projectRoot, "BuildReports", reportName);
        }

        private static double Percentile(double[] samples, int count, double quantile)
        {
            if (samples == null || count <= 0) return 0.0;
            Array.Sort(samples, 0, count);
            if (count == 1) return samples[0];
            double position = (count - 1) * Math.Clamp(quantile, 0.0, 1.0);
            int lower = Math.Clamp((int)Math.Floor(position), 0, count - 1);
            int upper = Math.Min(count - 1, lower + 1);
            double interpolation = position - lower;
            return samples[lower] + (samples[upper] - samples[lower]) * interpolation;
        }

        [Serializable]
        public sealed class Mvp01ProfilerEvidence
        {
            public string schema;
            public string unityVersion;
            public string utc;
            public string captureId;
            public string mode;
            public string renderSurface;
            public bool isEditor;
            public bool isBatchMode;
            public int requestedFrameSamples;
            public int warmupFrames;
            public int totalFrameSamples;
            public int frameTimingSamples;
            public int renderWidth;
            public int renderHeight;
            public double totalFrameP95Milliseconds;
            public double cpuFrameAverageMilliseconds;
            public double cpuFrameP95Milliseconds;
            public double cpuFrameMaximumMilliseconds;
            public double gpuFrameAverageMilliseconds;
            public double gpuFrameP95Milliseconds;
            public double gpuFrameMaximumMilliseconds;
            public bool gpuTimingAvailable;
            public string gcMeasurementMode;
            public int gcSampleFrames;
            public int steadyStateGcFramesOverZero;
            public long steadyStateMaximumGcBytesInFrame;
            public int activeFootControllerCount;
            public int footContactFrameSamples;
            public int footContactMissingFrames;
            public int footContactTotalInvocations;
            public int footContactMinimumInvocations;
            public double footContactP50Milliseconds;
            public double footContactP95Milliseconds;
            public double footContactP99Milliseconds;
            public double footContactMaximumMilliseconds;
            public double acquireSolidPeakMilliseconds;
            public double fracturePreparationPeakMilliseconds;
            public Mvp01RuntimeRenderAudit runtimeRenderAudit;
            public bool resolutionGatePassed;
            public bool sampleCoverageGatePassed;
            public bool cpuBudgetGatePassed;
            public bool gpuBudgetGatePassed;
            public bool gpuTimingWaived;
            public bool cpuGpuBudgetGatePassed;
            public bool zeroGcGatePassed;
            public bool footContactGatePassed;
            public bool runtimeRenderAuditPassed;
            public bool editorDiagnosticPassed;
            public bool authoritativePassed;
            public bool passed;

            public void EvaluateGates()
            {
                resolutionGatePassed = renderWidth == EvidenceWidth &&
                                       renderHeight == EvidenceHeight;
                sampleCoverageGatePassed = requestedFrameSamples == 720 &&
                                           totalFrameSamples == requestedFrameSamples &&
                                           frameTimingSamples == requestedFrameSamples;
                cpuBudgetGatePassed = cpuFrameP95Milliseconds > 0.0 &&
                                      cpuFrameP95Milliseconds <= 16.67;
                gpuBudgetGatePassed = gpuTimingAvailable &&
                                      gpuFrameP95Milliseconds > 0.0 &&
                                      gpuFrameP95Milliseconds <= 16.67;
                gpuTimingWaived = !gpuTimingAvailable &&
                                  runtimeRenderAudit != null &&
                                  runtimeRenderAudit.graphicsDeviceType == "Direct3D11";
                cpuGpuBudgetGatePassed = cpuBudgetGatePassed &&
                                         (gpuBudgetGatePassed || gpuTimingWaived);
                zeroGcGatePassed = gcSampleFrames == requestedFrameSamples &&
                                   steadyStateGcFramesOverZero == 0 &&
                                   steadyStateMaximumGcBytesInFrame == 0L;
                footContactGatePassed = activeFootControllerCount >= 2 &&
                                        footContactFrameSamples == requestedFrameSamples &&
                                        footContactMissingFrames == 0 &&
                                        footContactMinimumInvocations >= 2 &&
                                        footContactP95Milliseconds <= 0.30;
                runtimeRenderAuditPassed = runtimeRenderAudit != null &&
                                           runtimeRenderAudit.passed;
                editorDiagnosticPassed = isEditor &&
                                         resolutionGatePassed &&
                                         sampleCoverageGatePassed &&
                                         cpuGpuBudgetGatePassed &&
                                         footContactGatePassed &&
                                         runtimeRenderAuditPassed;
                authoritativePassed = !isEditor && !isBatchMode &&
                                      resolutionGatePassed &&
                                      sampleCoverageGatePassed &&
                                      cpuGpuBudgetGatePassed &&
                                      zeroGcGatePassed &&
                                      footContactGatePassed &&
                                      runtimeRenderAuditPassed;
                passed = authoritativePassed;
            }
        }

        [Serializable]
        public sealed class Mvp01RuntimeRenderAudit
        {
            public string utc;
            public string graphicsDeviceType;
            public string graphicsDeviceName;
            public string activeColorSpace;
            public int qualityLevelIndex;
            public string qualityLevelName;
            public string qualityPipelineAsset;
            public string activePipelineAsset;
            public string universalPipelineAsset;
            public int qualityAntiAliasingSamples;
            public string qualityShadows;
            public string qualityShadowResolution;
            public int qualityShadowCascades;
            public float qualityShadowDistance;
            public bool pipelineSupportsHdr;
            public int pipelineMsaaSamples;
            public float pipelineRenderScale;
            public bool pipelineDepthTexture;
            public bool pipelineOpaqueTexture;
            public float pipelineShadowDistance;
            public int pipelineShadowCascades;
            public int pipelineMainLightShadowAtlas;
            public string cameraName;
            public bool cameraAllowHdr;
            public bool cameraAllowMsaa;
            public string cameraRenderType;
            public int cameraStackCount;
            public bool cameraPostProcessing;
            public bool cameraRequiresDepthTexture;
            public bool cameraRequiresOpaqueTexture;
            public bool cameraRendersShadows;
            public bool cameraStopNaN;
            public bool cameraDithering;
            public string cameraAntialiasing;
            public string cameraAntialiasingQuality;
            public int activeRendererIndex;
            public string activeRendererType;
            public string loadedRendererDataAssets;
            public string loadedRendererFeatures;
            public bool ssaoFeatureFound;
            public bool ssaoFeatureActive;
            public string ssaoFeatureName;
            public bool ssaoGlobalKeywordEnabled;
            public bool ssaoTextureBound;
            public string arenaMaterialName;
            public string arenaMaterialShader;
            public bool arenaMaterialSsaoKeyword;
            public string activeVolumes;
            public bool resolvedColorAdjustments;
            public float resolvedPostExposure;
            public float resolvedContrast;
            public float resolvedSaturation;
            public bool resolvedWhiteBalance;
            public float resolvedTemperature;
            public float resolvedTint;
            public bool resolvedDepthOfField;
            public string resolvedDepthOfFieldMode;
            public float resolvedFocusDistance;
            public float resolvedAperture;
            public float resolvedFocalLength;
            public bool pipelineContractPassed;
            public bool cameraContractPassed;
            public bool ssaoContractPassed;
            public bool authoredLookContractPassed;
            public string mismatchSummary;
            public bool passed;

            public void Evaluate()
            {
                pipelineContractPassed =
                    universalPipelineAsset == "ElEmentalURP" &&
                    activePipelineAsset == universalPipelineAsset &&
                    activeRendererIndex == 0 &&
                    loadedRendererDataAssets.IndexOf(
                        "ElEmentalRenderer",
                        StringComparison.Ordinal) >= 0 &&
                    pipelineSupportsHdr &&
                    pipelineMsaaSamples == 1 &&
                    Mathf.Abs(pipelineRenderScale - 1f) <= 0.001f &&
                    pipelineDepthTexture &&
                    Mathf.Abs(pipelineShadowDistance - 90f) <= 0.01f &&
                    pipelineShadowCascades == 4 &&
                    pipelineMainLightShadowAtlas == 4096 &&
                    qualityShadows == UnityEngine.ShadowQuality.All.ToString() &&
                    qualityShadowCascades == 4 &&
                    Mathf.Abs(qualityShadowDistance - 90f) <= 0.01f;

                cameraContractPassed =
                    cameraAllowHdr &&
                    cameraPostProcessing &&
                    cameraRequiresDepthTexture &&
                    cameraRendersShadows &&
                    cameraStopNaN &&
                    cameraDithering &&
                    cameraAntialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing.ToString() &&
                    cameraAntialiasingQuality == AntialiasingQuality.High.ToString();

                ssaoContractPassed = ssaoFeatureFound && ssaoFeatureActive;

                // These are the authored EarthCoreSlice values. A Play-mode-only
                // lookdev component used to overwrite them with a hotter, flatter
                // clone; accepting that runtime state would reproduce the user's
                // "Scene View okay, Game/player awful" mismatch.
                authoredLookContractPassed =
                    resolvedColorAdjustments &&
                    resolvedWhiteBalance &&
                    Mathf.Abs(resolvedPostExposure - 0f) <= 0.001f &&
                    Mathf.Abs(resolvedContrast - 7f) <= 0.001f &&
                    Mathf.Abs(resolvedSaturation - -8f) <= 0.001f &&
                    Mathf.Abs(resolvedTemperature - 2f) <= 0.001f &&
                    Mathf.Abs(resolvedTint - -1f) <= 0.001f;

                var mismatches = new List<string>(4);
                if (!pipelineContractPassed) mismatches.Add("pipeline/quality/renderer/shadows");
                if (!cameraContractPassed) mismatches.Add("game-camera/SMAA/post/depth");
                if (!ssaoContractPassed) mismatches.Add("SSAO-feature");
                if (!authoredLookContractPassed)
                {
                    mismatches.Add(
                        $"runtime-volume(actual={resolvedPostExposure:0.###}/" +
                        $"{resolvedContrast:0.###}/{resolvedSaturation:0.###}/" +
                        $"{resolvedTemperature:0.###}/{resolvedTint:0.###};" +
                        "expected=0/7/-8/2/-1)");
                }
                mismatchSummary = mismatches.Count == 0
                    ? "none"
                    : string.Join("|", mismatches);
                passed = pipelineContractPassed &&
                         cameraContractPassed &&
                         ssaoContractPassed &&
                         authoredLookContractPassed;
            }
        }

        private static Material FindEarthMaterial()
        {
            MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer == null || renderer.sharedMaterial == null) continue;
                if (renderer.name == "Earth Landing Cushion Preview")
                    return renderer.sharedMaterial;
            }

            foreach (MeshRenderer renderer in renderers)
            {
                Material material = renderer != null ? renderer.sharedMaterial : null;
                if (material != null && material.shader != null && material.shader.isSupported)
                    return material;
            }

            return null;
        }

        private static List<float2> FindSurfaceLine(UnityEngine.Camera camera, Collider proxy)
        {
            int width = Mathf.Max(320, Screen.width);
            int height = Mathf.Max(200, Screen.height);
            var candidates = new List<List<float2>>(24);
            for (int y = 16; y < height - 16; y += 8)
            {
                int first = -1;
                int last = -1;
                for (int x = 16; x < width - 16; x += 8)
                {
                    if (!proxy.Raycast(camera.ScreenPointToRay(new Vector2(x, y)), out _, 200f)) continue;
                    if (first < 0) first = x;
                    last = x;
                }

                if (last - first < 80) continue;
                int inset = Mathf.Max(16, (last - first) / 5);
                candidates.Add(new List<float2>(2)
                {
                    new float2(first + inset, y),
                    new float2(last - inset, y)
                });
            }

            if (candidates.Count == 0) return null;
            int chosen = Mathf.Clamp(Mathf.RoundToInt((candidates.Count - 1) * 0.62f), 0, candidates.Count - 1);
            return candidates[chosen];
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }
}
