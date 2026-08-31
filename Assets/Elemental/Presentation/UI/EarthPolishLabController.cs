using System.Collections;
using System.Collections.Generic;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Runtime.World
{
    [DisallowMultipleComponent]
    public sealed class EarthPolishLabController : MonoBehaviour
    {
        private readonly List<float3> _wallPath = new List<float3>(2);
        private MagicExecutor _executor;
        private MagicInputController _input;
        private PlanetMotor _motor;
        private EarthWall _qaWall;
        private EarthPlatform _qaPlatform;
        private EarthPlatformPool _platformPool;
        private EarthPillarWaveAbility _wave;
        private EarthArmorController _armor;
        private EarthResonanceController _resonance;
        private EarthSurfController _surf;
        private EarthActionRouterBehaviour _actionRouter;
        private EarthMatterKernelBehaviour _matterKernel;
        private EarthMatterReturnController _matterReturn;
        private EarthTechniqueComboRuntime _comboRuntime;
        private CelestialSystemBehaviour _celestial;
        private HumanoidCharacterPresentation _animationPresentation;
        private bool _showGeometryIntegrity;
        private int _geometryValidCount;
        private int _geometryBlockedCount;
        private string _firstGeometryFault = string.Empty;
        private Coroutine _locomotionProof;
        private string _locomotionProofStatus = "ready";

        private void Awake()
        {
            _executor = FindAnyObjectByType<MagicExecutor>();
            _input = FindAnyObjectByType<MagicInputController>();
            // The lab contains presentation/impact mannequins. Drive the actual input
            // owner so QA buttons, gameplay input and the camera always observe one hero.
            _motor = _input != null ? _input.GetComponentInParent<PlanetMotor>() : null;
            if (_motor == null) _motor = FindAnyObjectByType<PlanetMotor>();
            _platformPool = FindAnyObjectByType<EarthPlatformPool>();
            _wave = FindAnyObjectByType<EarthPillarWaveAbility>();
            _armor = _motor != null ? _motor.GetComponent<EarthArmorController>() : null;
            if (_armor == null) _armor = FindAnyObjectByType<EarthArmorController>();
            _resonance = _motor != null ? _motor.GetComponent<EarthResonanceController>() : null;
            if (_resonance == null) _resonance = FindAnyObjectByType<EarthResonanceController>();
            _surf = _motor != null ? _motor.GetComponent<EarthSurfController>() : null;
            if (_surf == null) _surf = FindAnyObjectByType<EarthSurfController>();
            _actionRouter = _input != null ? _input.GetComponent<EarthActionRouterBehaviour>() : null;
            if (_actionRouter == null) _actionRouter = FindAnyObjectByType<EarthActionRouterBehaviour>();
            _matterKernel = _executor != null ? _executor.MatterKernel : null;
            if (_matterKernel == null) _matterKernel = FindAnyObjectByType<EarthMatterKernelBehaviour>();
            _matterReturn = _executor != null ? _executor.MatterReturnController : null;
            if (_matterReturn == null) _matterReturn = FindAnyObjectByType<EarthMatterReturnController>();
            _comboRuntime = _executor != null ? _executor.ComboRuntime : null;
            if (_comboRuntime == null) _comboRuntime = FindAnyObjectByType<EarthTechniqueComboRuntime>();
            _celestial = FindAnyObjectByType<CelestialSystemBehaviour>();
            _animationPresentation = _motor != null
                ? _motor.GetComponentInChildren<HumanoidCharacterPresentation>(true)
                : null;
        }

        private void OnGUI()
        {
            Event current = Event.current;
            if (current != null && current.type == EventType.KeyDown)
            {
                // Direct visual-court shortcuts remain usable even when the shipping
                // HUD is intentionally drawn over the non-shipping lab panel.
                if (current.keyCode == KeyCode.F4)
                {
                    _armor?.Begin();
                    current.Use();
                }
                else if (current.keyCode == KeyCode.F5)
                {
                    PrimeResonance();
                    current.Use();
                }
                else if (current.keyCode == KeyCode.F6)
                {
                    _celestial?.SetTimeOfDayForQa(0.23f);
                    current.Use();
                }
                else if (current.keyCode == KeyCode.F7)
                {
                    _celestial?.SetTimeOfDayForQa(0.72f);
                    current.Use();
                }
            }
            if (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.F8)
            {
                _showGeometryIntegrity = !_showGeometryIntegrity;
                if (_showGeometryIntegrity) ScanRuntimeGeometry();
                current.Use();
            }
            GUILayout.BeginArea(new Rect(18f, 18f, 350f, Mathf.Min(742f, Screen.height - 36f)), GUI.skin.box);
            GUILayout.Label("EARTH POLISH LAB / V4.1");
            if (_input != null)
            {
                GUILayout.Label($"Input owner: {_input.ActiveActionOwner}");
                EarthInputChordState chord = _actionRouter != null ? _actionRouter.ChordState : default;
                if (chord.IsPending)
                    GUILayout.Label($"Speculative chord: WAVE → resonance  {chord.Window01(Time.unscaledTime) * 100f:0}%");
                EarthGestureToken token = _input.LastGestureToken;
                GUILayout.Label($"Gesture: {token.Kind}  conf {token.Confidence:0.00}  v {token.PeakSpeed:0.00}  a {token.PeakAcceleration:0.0}");
                EarthScrollState scroll = _input.ScrollState;
                GUILayout.Label($"Wheel/{_input.ScrollDeviceProfile}: Δ {scroll.NormalizedDelta:+0.00;-0.00;0.00}  phase {scroll.Continuous:0.00}  v {scroll.Velocity:0.0}");
                if (_input.RankedIntentCount > 0)
                {
                    EarthIntentCandidate best = _input.GetRankedIntentCandidate(0);
                    GUILayout.Label($"Intent: {best.Intent}  score {best.Score:0.00}  reject {best.RejectReason}");
                }
                GUILayout.Label($"Resonance: {_input.ResonanceCharge01 * 100f:0}% / {_input.ResonanceStoneCount} stones");
                GUILayout.Label($"MMB targets: {_executor?.GravityWellCapturedCount ?? 0}");
                GUILayout.Label($"Plough: {_input.SurfSpeed:0.0} m/s");
            }
            if (_motor != null)
                GUILayout.Label($"Motor: {_motor.Telemetry.Speed:0.00} m/s / support {_motor.HasStableSupport}");
            if (_animationPresentation != null)
            {
                EarthLandingCandidateSnapshot landing = _animationPresentation.LandingCandidate;
                GUILayout.Label(
                    $"Anim: {_animationPresentation.MotionPhase} / {_animationPresentation.LandingStyle}  " +
                    $"speed {_animationPresentation.FilteredSpeed:0.00}  turn {_animationPresentation.FilteredTurn:+0.00;-0.00;0.00}");
                GUILayout.Label(landing.IsValid
                    ? $"Landing: {landing.TimeToContact * 1000f:0} ms  impact {landing.ImpactSpeed:0.0} m/s  support {landing.SurfaceId}:{landing.Generation}"
                    : "Landing: no candidate");
                EarthCharacterPoseController pose = _animationPresentation.PoseController;
                if (pose != null)
                    GUILayout.Label(
                        $"Feet Δ: L {pose.LeftAnchorErrorMeters * 100f:0.0} cm / R {pose.RightAnchorErrorMeters * 100f:0.0} cm  " +
                        $"pelvis {pose.PelvisCorrectionMeters * 100f:+0.0;-0.0;0.0} cm");
            }
            GUILayout.Label($"Matter records: {_matterKernel?.ActiveRecordCount ?? 0}  returning: {_matterReturn?.ActiveReturnCount ?? 0}");
            if (_comboRuntime != null && _comboRuntime.OpportunityCount > 0)
            {
                EarthComboOpportunity first = _comboRuntime.GetOpportunity(0);
                GUILayout.Label($"Follow-up: {first.Technique}  {first.Score * 100f:0}%  needs {first.RequiredResult}");
                if (_comboRuntime.OpportunityCount > 1)
                {
                    EarthComboOpportunity second = _comboRuntime.GetOpportunity(1);
                    GUILayout.Label($"Alternate: {second.Technique}  {second.Score * 100f:0}%");
                }
            }
            GUILayout.Space(8f);
            if (GUILayout.Button("Reset golden path"))
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            if (_celestial != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("QA daylight")) _celestial.SetTimeOfDayForQa(0.23f);
                if (GUILayout.Button("QA readable night")) _celestial.SetTimeOfDayForQa(0.72f);
                GUILayout.EndHorizontal();
            }
            if (_motor != null)
            {
                GUI.enabled = _locomotionProof == null;
                if (GUILayout.Button("Run 4s locomotion visual proof"))
                    _locomotionProof = StartCoroutine(RunLocomotionProof());
                GUI.enabled = true;
                GUILayout.Label($"Locomotion proof: {_locomotionProofStatus}");
            }
            if (GUILayout.Button("Spawn V3 control wall")) SpawnControlWall();
            GUI.enabled = _qaWall != null && _qaWall.gameObject.activeInHierarchy;
            if (GUILayout.Button("Light local impact")) ImpactWall(1.2f);
            if (GUILayout.Button("Heavy island impact")) ImpactWall(4.5f);
            GUI.enabled = true;
            if (GUILayout.Button("Repair control wall to 100%")) RepairWall();
            GUILayout.Space(5f);
            if (GUILayout.Button("Spawn rider platform")) SpawnControlPlatform();
            GUI.enabled = _qaPlatform != null && _qaPlatform.gameObject.activeInHierarchy;
            if (GUILayout.Button("Light platform fracture")) ImpactPlatform(1.15f);
            if (GUILayout.Button("Heavy platform island fracture")) ImpactPlatform(4.5f);
            if (GUILayout.Button("Repair platform to 100%")) _qaPlatform.TryBeginRepair(
                unchecked((uint)Time.frameCount), 1f);
            GUI.enabled = true;
            GUILayout.Space(5f);
            if (GUILayout.Button("Cast seeded web wave")) CastWave();
            if (GUILayout.Button(_input != null && _input.IsQuickStonePrimed
                    ? "Fire primed quick stone"
                    : "Prime quick stone from ground"))
                QuickStoneTap();
            if (GUILayout.Button("Prime resonance volley")) PrimeResonance();
            if (GUILayout.Button("Assemble visible armor shell")) _armor?.Begin();
            if (GUILayout.Button("Release armor debris")) _armor?.ReleaseAsDebris();
            if (_surf != null && !_surf.IsActive)
            {
                if (GUILayout.Button("Start Shift+W plough"))
                    _surf.Begin(Time.unscaledTime, _motor != null ? _motor.FacingForward : transform.forward);
            }
            else if (GUILayout.Button("Release Shift+W plough")) _surf?.Release(Time.unscaledTime);
            GUILayout.Space(8f);
            GUILayout.Label("Shift+Space: web wave");
            GUILayout.Label("+ LMB within .15s: resonance");
            GUILayout.Label("Shift+MMB + wheel: armor shell");
            GUILayout.Label("Shift+W: earth plough");
            GUILayout.Label("Hold LMB on structure: pluck cell");
            GUILayout.Label("F4/F5: armor/resonance  F6/F7: day/night");
            GUILayout.Label("F8: geometry integrity court");
            GUILayout.EndArea();

            if (_showGeometryIntegrity)
            {
                GUILayout.BeginArea(new Rect(Screen.width - 392f, 18f, 374f, 152f), GUI.skin.box);
                GUILayout.Label("GEOMETRY INTEGRITY / RUNTIME");
                GUILayout.Label($"Valid: {_geometryValidCount}   Blocked: {_geometryBlockedCount}");
                if (!string.IsNullOrEmpty(_firstGeometryFault))
                    GUILayout.Label(_firstGeometryFault, GUILayout.MaxHeight(62f));
                if (GUILayout.Button("Rescan now")) ScanRuntimeGeometry();
                GUILayout.EndArea();
            }
        }

        private void ScanRuntimeGeometry()
        {
            _geometryValidCount = 0;
            _geometryBlockedCount = 0;
            _firstGeometryFault = string.Empty;
            MeshFilter[] filters = FindObjectsByType<MeshFilter>(FindObjectsInactive.Include);
            for (int index = 0; index < filters.Length; index++)
            {
                MeshFilter filter = filters[index];
                if (filter == null || filter.sharedMesh == null) continue;
                EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(
                    filter.sharedMesh,
                    EarthMeshIntegrityPolicy.OpenVisualSurface,
                    filter.transform.localToWorldMatrix);
                if (report.IsValid) _geometryValidCount++;
                else
                {
                    _geometryBlockedCount++;
                    if (string.IsNullOrEmpty(_firstGeometryFault)) _firstGeometryFault = report.ToString();
                }
            }
        }

        private void SpawnControlPlatform()
        {
            if (_platformPool == null || _motor == null) return;
            Vector3 center = _motor.transform.position;
            Vector3 up = _motor.LocalUp.sqrMagnitude > 0.5f ? _motor.LocalUp.normalized : _motor.transform.up;
            Vector3 forward = Vector3.ProjectOnPlane(_motor.FacingForward, up).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = Vector3.Cross(up, Vector3.right).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 surfaceCenter = center + forward * 4.5f - up * 0.9f;
            var path = new List<float3>(5)
            {
                ToFloat3(surfaceCenter - right * 1.8f - forward * 1.4f),
                ToFloat3(surfaceCenter + right * 1.8f - forward * 1.4f),
                ToFloat3(surfaceCenter + right * 2.1f + forward * 1.1f),
                ToFloat3(surfaceCenter - right * 0.2f + forward * 1.8f),
                ToFloat3(surfaceCenter - right * 2.0f + forward * 0.8f)
            };
            EarthPlatformGeometry geometry = EarthPlatformGeometrySolver.Build(
                path,
                ToFloat3(_executor != null && _executor.PlanetCenterTransform != null
                    ? _executor.PlanetCenterTransform.position
                    : Vector3.zero));
            _qaPlatform = _platformPool.Acquire(in geometry, 1.45f, 0.24f);
        }

        private void ImpactPlatform(float multiplier)
        {
            if (_qaPlatform == null) return;
            _qaPlatform.ApplyEarthImpact(new EarthStructureImpact(
                _qaPlatform.SurfaceTopPoint + _qaPlatform.transform.right * 0.35f,
                _qaPlatform.transform.forward + _qaPlatform.SurfaceUp * 0.08f,
                1500f * multiplier,
                EarthStructureImpactKind.Projectile,
                0x51410002u));
        }

        private void RepairWall()
        {
            if (_qaWall == null || _qaWall.Reassembly == null) return;
            _qaWall.Reassembly.TryBeginRepair(unchecked((uint)Time.frameCount), 1f);
        }

        private void CastWave()
        {
            if (_wave == null || _motor == null) return;
            _wave.TryCast(_motor.FacingForward, 0.62f, 0.82f, out _);
        }

        private void PrimeResonance()
        {
            if (_resonance == null || _motor == null) return;
            float now = Time.unscaledTime;
            if (!_resonance.BeginCharge(now)) return;
            _resonance.ContinueCharge(now + 1.35f, _motor.FacingForward);
            _resonance.ReleaseCharge(now + 1.35f, _motor.FacingForward);
        }

        private void QuickStoneTap()
        {
            if (_input == null) return;
            _input.TryQuickStoneTapAtScreenPoint(new float2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);

        private void SpawnControlWall()
        {
            if (_executor == null || _motor == null) return;
            Vector3 origin = _motor.transform.position;
            Vector3 up = _motor.LocalUp.sqrMagnitude > 0.5f ? _motor.LocalUp.normalized : _motor.transform.up;
            Vector3 forward = Vector3.ProjectOnPlane(_motor.FacingForward, up).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 center = origin + forward * 6f - up * 0.85f;
            Vector3 a = center - right * 3.1f;
            Vector3 b = center + right * 3.1f;
            _wallPath.Clear();
            _wallPath.Add(new float3(a.x, a.y, a.z));
            _wallPath.Add(new float3(b.x, b.y, b.z));
            _executor.TryRaiseWallOnSurface(
                _wallPath, up, 0.58f, 0.52f, unchecked((uint)Time.frameCount), out _qaWall);
        }

        private void ImpactWall(float multiplier)
        {
            if (_qaWall == null) return;
            Vector3 point = _qaWall.transform.position + _qaWall.transform.right * 0.55f;
            var impact = new EarthStructureImpact(
                point,
                _qaWall.transform.forward + _qaWall.SurfaceUp * 0.08f,
                1500f * multiplier,
                EarthStructureImpactKind.Projectile,
                0x51410001u);
            _qaWall.ApplyEarthImpact(in impact);
        }

        private IEnumerator RunLocomotionProof()
        {
            if (_motor == null)
            {
                _locomotionProofStatus = "motor missing";
                _locomotionProof = null;
                yield break;
            }

            MonoBehaviour gameplayInput = _motor.GetComponent<PlanetInputReader>();
            LocomotionProofInput proofInput = _motor.GetComponent<LocomotionProofInput>();
            if (proofInput == null) proofInput = _motor.gameObject.AddComponent<LocomotionProofInput>();
            proofInput.Move = new float2(0f, 1f);
            _motor.ConfigureInputSource(proofInput);

            Rigidbody body = _motor.GetComponent<Rigidbody>();
            Animator animator = _motor.GetComponentInChildren<Animator>(true);
            HumanoidCharacterPresentation presentation = animator != null
                ? animator.GetComponent<HumanoidCharacterPresentation>()
                : null;
            Transform leftFoot = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.LeftFoot)
                : null;
            Vector3 start = body != null ? body.position : _motor.transform.position;
            Vector3 firstFoot = leftFoot != null
                ? animator.transform.InverseTransformPoint(leftFoot.position)
                : Vector3.zero;
            float maximumFootTravel = 0f;
            float maximumUncommandedTurn = 0f;
            float maximumLocomotionFootIk = 0f;
            float firstLocomotionTime = -1f;
            float lastLocomotionTime = -1f;
            int sampledFrames = 0;
            _locomotionProofStatus = "running — watch the legs";

            float end = Time.unscaledTime + 4f;
            while (Time.unscaledTime < end)
            {
                yield return new WaitForFixedUpdate();
                if (leftFoot != null)
                {
                    Vector3 localFoot = animator.transform.InverseTransformPoint(leftFoot.position);
                    maximumFootTravel = Mathf.Max(maximumFootTravel, Vector3.Distance(firstFoot, localFoot));
                }
                if (animator != null)
                {
                    AnimatorStateInfo sampledState = animator.GetCurrentAnimatorStateInfo(0);
                    if (sampledState.IsName("Locomotion"))
                    {
                        if (firstLocomotionTime < 0f) firstLocomotionTime = sampledState.normalizedTime;
                        lastLocomotionTime = sampledState.normalizedTime;
                    }
                }
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
                sampledFrames++;
            }

            proofInput.Move = float2.zero;
            // Leave the zeroed proof input in ownership for a short, deterministic
            // braking window. This validates the real motor deceleration instead of
            // hiding residual glide behind the editor-only settle helper. Ten fixed
            // steps are still comfortably below a perceptible long slide at 50 Hz.
            const int BrakingSteps = 10;
            for (int step = 0; step < BrakingSteps; step++)
                yield return new WaitForFixedUpdate();
            float travel = body != null
                ? Vector3.Distance(start, body.position)
                : Vector3.Distance(start, _motor.transform.position);
            AnimatorStateInfo state = animator != null ? animator.GetCurrentAnimatorStateInfo(0) : default;
            float cycles = firstLocomotionTime >= 0f && lastLocomotionTime >= firstLocomotionTime
                ? lastLocomotionTime - firstLocomotionTime
                : 0f;
            bool locomotionState = animator != null && state.IsName("Locomotion");
            float settledSpeed = body != null
                ? Vector3.ProjectOnPlane(body.linearVelocity, _motor.Telemetry.LocalUp).magnitude
                : 0f;
            if (gameplayInput != null) _motor.ConfigureInputSource(gameplayInput);
            _motor.SettleTangentialMotion();
            bool passed = travel >= 3f && maximumFootTravel >= 0.05f && cycles >= 1f &&
                          locomotionState && _motor.HasStableSupport && settledSpeed <= 0.35f &&
                          maximumUncommandedTurn <= 0.025f &&
                          maximumLocomotionFootIk >= 0.35f && maximumLocomotionFootIk <= 0.90f;
            _locomotionProofStatus =
                $"{(passed ? "PASS" : "FAIL")} / travel {travel:0.00}m / local foot {maximumFootTravel:0.00}m / " +
                $"cycles {cycles:0.00} / state {(locomotionState ? "locomotion" : "invalid")} / " +
                $"support {_motor.HasStableSupport} / settle {settledSpeed:0.00}m/s / " +
                $"ghost turn {maximumUncommandedTurn:0.000} / foot IK {maximumLocomotionFootIk:0.000} / " +
                $"samples {sampledFrames}";
            _locomotionProof = null;
        }

        private sealed class LocomotionProofInput : MonoBehaviour, IPlanetMotorInputSource
        {
            public float2 Move;
            public PlanetMotorCommand SampleCommand(uint tick) => new PlanetMotorCommand(tick, Move, false);
        }
    }
}
