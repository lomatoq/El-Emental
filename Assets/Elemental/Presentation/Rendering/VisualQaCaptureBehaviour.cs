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
using Elemental.Simulation.Magic;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    [DisallowMultipleComponent]
    public sealed class VisualQaCaptureBehaviour : MonoBehaviour
    {
        [SerializeField, Min(1)] private int settleFrames = 90;

        private readonly FrameTiming[] _latestTiming = new FrameTiming[1];
        private string _requestedOutputPath;
        private int _successfulSupplementalCaptures;

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
                bool fired = executor.ReleaseHeldEarthAtSpeed(direction, 35f, 0u, out Vector3 velocity);
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

                    AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
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
                AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
                for (int index = 0; index < clips.Length; index++)
                    if (clips[index].weight > 0.01f) activeClipsLoop &= clips[index].clip.isLooping;
                AnimatorStateInfo finalState = animator.GetCurrentAnimatorStateInfo(0);
                float locomotionCycles = firstLocomotionTime >= 0f && lastLocomotionTime >= firstLocomotionTime
                    ? lastLocomotionTime - firstLocomotionTime
                    : 0f;
                float travel = Vector3.Distance(start, body.position);
                _scenarioSucceeded = travel > 3f && maximumFootTravel > 0.05f &&
                                     lateFootTravel > 0.015f && locomotionCycles > 1.1f &&
                                     finalState.IsName("Locomotion") && clips.Length > 0 &&
                                     activeClipsLoop && gaitCaptureIndex == 3 &&
                                     _successfulSupplementalCaptures == 3;
                Debug.Log($"[Elemental] Continuous gait QA: travel={travel:0.000} m, " +
                          $"foot={maximumFootTravel:0.000}, lateFoot={lateFootTravel:0.000}, " +
                          $"cycles={locomotionCycles:0.00}, state={finalState.fullPathHash}, looping={activeClipsLoop}.");
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
                PlanetMotor motor = FindAnyObjectByType<PlanetMotor>();
                Rigidbody rider = motor != null ? motor.GetComponent<Rigidbody>() : null;
                CapsuleCollider capsule = motor != null ? motor.GetComponent<CapsuleCollider>() : null;
                if (pool == null || motor == null || rider == null || capsule == null) yield break;
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
                                     motor.MovingSurfaceId == platform.SurfaceId;
                Debug.Log($"[Elemental] Platform rider QA: immediate={immediateRider}, " +
                          $"lift={lift:0.000} m, walk={walk:0.000} m, " +
                          $"riseClearance={minimumClearance:0.000} m, " +
                          $"descentClearance={descentClearance:0.000} m, landed={landed}, " +
                          $"pillarRetreated={pillarRetreated}, chips={activeLaunchChips}, " +
                          $"surface={motor.MovingSurfaceId}, fractured={platform.IsFractured}.");
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
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
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
            int width = Mathf.Max(320, Screen.width);
            int height = Mathf.Max(180, Screen.height);
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

        private IEnumerator CapturePerformanceSample(int frameCount)
        {
            double cpuTotal = 0d;
            double gpuTotal = 0d;
            double cpuMaximum = 0d;
            double gpuMaximum = 0d;
            int count = 0;
            for (int frame = 0; frame < frameCount; frame++)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                if (FrameTimingManager.GetLatestTimings(1, _latestTiming) == 0) continue;
                FrameTiming timing = _latestTiming[0];
                cpuTotal += timing.cpuFrameTime;
                gpuTotal += timing.gpuFrameTime;
                cpuMaximum = Math.Max(cpuMaximum, timing.cpuFrameTime);
                gpuMaximum = Math.Max(gpuMaximum, timing.gpuFrameTime);
                count++;
            }

            if (count == 0)
            {
                Debug.LogWarning("[Elemental] Earth material frame timing capture returned no samples.");
                yield break;
            }
            Debug.Log($"[Elemental] Earth material frame timing samples={count}, " +
                      $"CPU avg={cpuTotal / count:0.00} ms max={cpuMaximum:0.00} ms, " +
                      $"GPU avg={gpuTotal / count:0.00} ms max={gpuMaximum:0.00} ms.");
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
