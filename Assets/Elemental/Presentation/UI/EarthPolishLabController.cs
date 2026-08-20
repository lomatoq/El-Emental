using System.Collections.Generic;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
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
        private bool _showGeometryIntegrity;
        private int _geometryValidCount;
        private int _geometryBlockedCount;
        private string _firstGeometryFault = string.Empty;

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
        }

        private void OnGUI()
        {
            Event current = Event.current;
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
    }
}
