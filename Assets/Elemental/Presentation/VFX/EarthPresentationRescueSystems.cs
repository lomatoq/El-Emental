using System;
using System.Collections.Generic;
using System.Reflection;
using Elemental.Input.Gestures;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityCamera = global::UnityEngine.Camera;

namespace Elemental.Presentation.VFX
{
    internal static class EarthPresentationRescueBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EarthPresentationRescueInstaller.Ensure();
    }

    [DefaultExecutionOrder(1600)]
    internal sealed class EarthPresentationRescueInstaller : MonoBehaviour
    {
        private static EarthPresentationRescueInstaller _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var host = new GameObject("Earth Presentation Rescue Installer")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<EarthPresentationRescueInstaller>();
            if (host.GetComponent<EarthMaterialLookdevTuner>() == null)
                host.AddComponent<EarthMaterialLookdevTuner>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Scan();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (_instance == this) _instance = null;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Scan();

        private static void Scan()
        {
            EarthCharacterPoseController[] poses = FindObjectsByType<EarthCharacterPoseController>(
                FindObjectsInactive.Include);
            for (int index = 0; index < poses.Length; index++)
            {
                EarthCharacterPoseController pose = poses[index];
                if (pose == null) continue;
                // EarthFootContactController is the only runtime writer of feet,
                // knees and pelvis on every support, including surf. The old
                // rendered-board rescue stacked a second Animator IK pass and was
                // the source of the visible foot snaps this rescue is replacing.
                PlanetMotor motor = pose.GetComponentInParent<PlanetMotor>();
                if (motor != null && motor.GetComponent<EarthSeismicVision>() == null)
                    motor.gameObject.AddComponent<EarthSeismicVision>();
            }

            UnityCamera[] cameras = FindObjectsByType<UnityCamera>(FindObjectsInactive.Include);
            for (int index = 0; index < cameras.Length; index++)
            {
                UnityCamera camera = cameras[index];
                if (camera == null || camera.cameraType != CameraType.Game) continue;
                if (camera.GetComponent<EarthChargeCameraLookdev>() == null)
                    camera.gameObject.AddComponent<EarthChargeCameraLookdev>();
            }
        }
    }

    /// <summary>
    /// The surf collider is intentionally simple and stable, while the visible hero
    /// shell is banked, ramped and procedurally beveled. This late IK pass intersects
    /// the actual rendered mesh, widens the stance, and overrides the simplified
    /// collider contact so boots cannot visibly pass through the stone board.
    /// </summary>
    [DefaultExecutionOrder(2100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class EarthSurfFootContactRescue : MonoBehaviour
    {
        private static readonly FieldInfo VisualRootField = typeof(EarthSurfController).GetField(
            "_boardVisualRoot", BindingFlags.Instance | BindingFlags.NonPublic);

        private Animator _animator;
        private EarthSurfController _surf;
        private PlanetMotor _motor;
        private Transform _leftFoot;
        private Transform _rightFoot;
        private Transform _leftUpperLeg;
        private Transform _rightUpperLeg;
        private Transform _visualRoot;
        private Mesh _cachedMesh;
        private Vector3[] _vertices = Array.Empty<Vector3>();
        private int[] _triangles = Array.Empty<int>();
        private float _pelvisOffset;
        private float _pelvisVelocity;

        public float LeftFootError { get; private set; }
        public float RightFootError { get; private set; }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _motor = GetComponentInParent<PlanetMotor>();
            _surf = GetComponentInParent<EarthSurfController>();
            ResolveBones();
        }

        private void ResolveBones()
        {
            if (_animator == null || !_animator.isHuman) return;
            _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _leftUpperLeg = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _rightUpperLeg = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        }

        // Deliberately not a Unity OnAnimatorIK callback. Kept only as a dormant
        // migration shell for old serialized references; the independent
        // EarthFootContactController owns all visible lower-body IK.
        private void LegacyAnimatorIkDisabled(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null || !_animator.isHuman ||
                _surf == null || !_surf.IsActive || _leftFoot == null || _rightFoot == null)
            {
                _pelvisOffset = Mathf.SmoothDamp(
                    _pelvisOffset, 0f, ref _pelvisVelocity, 0.08f, 2f, Mathf.Max(0.0001f, Time.deltaTime));
                return;
            }

            _visualRoot = VisualRootField?.GetValue(_surf) as Transform;
            if (_visualRoot == null || !_visualRoot.gameObject.activeInHierarchy) return;
            MeshFilter filter = _visualRoot.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return;
            CacheMesh(mesh);

            Vector3 visualRight = _visualRoot.right.normalized;
            Vector3 visualForward = _visualRoot.forward.normalized;
            Vector3 midpoint = (_leftFoot.position + _rightFoot.position) * 0.5f;
            float signedSeparation = Vector3.Dot(_rightFoot.position - _leftFoot.position, visualRight);
            float desiredSeparation = Mathf.Lerp(0.46f, 0.58f,
                Mathf.InverseLerp(2f, 12f, _surf.Speed));
            float half = Mathf.Max(Mathf.Abs(signedSeparation) * 0.5f, desiredSeparation * 0.5f);

            Vector3 leftGuess = midpoint - visualRight * half - visualForward * 0.09f;
            Vector3 rightGuess = midpoint + visualRight * half + visualForward * 0.09f;
            if (!TryProjectToRenderedBoard(leftGuess, -1f, out Vector3 leftPoint, out Vector3 leftNormal) ||
                !TryProjectToRenderedBoard(rightGuess, 1f, out Vector3 rightPoint, out Vector3 rightNormal))
                return;

            const float sole = 0.032f;
            leftPoint += leftNormal * sole;
            rightPoint += rightNormal * sole;
            ApplyFoot(AvatarIKGoal.LeftFoot, _leftFoot, leftPoint, leftNormal);
            ApplyFoot(AvatarIKGoal.RightFoot, _rightFoot, rightPoint, rightNormal);
            ApplyKneeHints(visualForward, visualRight);

            Vector3 supportUp = (leftNormal + rightNormal).normalized;
            if (supportUp.sqrMagnitude < 0.5f)
                supportUp = _motor != null ? _motor.LocalUp : _visualRoot.up;
            float leftError = Vector3.Dot(leftPoint - _leftFoot.position, supportUp);
            float rightError = Vector3.Dot(rightPoint - _rightFoot.position, supportUp);
            float targetPelvis = Mathf.Clamp(Mathf.Min(leftError, rightError), -0.16f, 0.035f);
            _pelvisOffset = Mathf.SmoothDamp(
                _pelvisOffset,
                targetPelvis,
                ref _pelvisVelocity,
                0.055f,
                3.2f,
                Mathf.Max(0.0001f, Time.deltaTime));
            _animator.bodyPosition += supportUp * _pelvisOffset;
            LeftFootError = Vector3.Distance(_leftFoot.position, leftPoint);
            RightFootError = Vector3.Distance(_rightFoot.position, rightPoint);
        }

        private void CacheMesh(Mesh mesh)
        {
            if (_cachedMesh == mesh && _vertices.Length > 0 && _triangles.Length > 0) return;
            _cachedMesh = mesh;
            _vertices = mesh.vertices;
            _triangles = mesh.triangles;
        }

        private bool TryProjectToRenderedBoard(
            Vector3 worldGuess,
            float side,
            out Vector3 worldPoint,
            out Vector3 worldNormal)
        {
            Vector3 localGuess = _visualRoot.InverseTransformPoint(worldGuess);
            Bounds bounds = _cachedMesh.bounds;
            localGuess.x = Mathf.Clamp(localGuess.x, bounds.min.x + 0.08f, bounds.max.x - 0.08f);
            localGuess.z = Mathf.Clamp(localGuess.z, bounds.min.z + 0.10f, bounds.max.z - 0.10f);

            if (!TryRaycastLocalMesh(localGuess, out Vector3 localPoint, out Vector3 localNormal))
            {
                // Split-rail silhouettes can have a deliberate central gap. Search
                // outward toward the intended foot side instead of snapping both
                // boots onto the same rail.
                float span = Mathf.Max(0.2f, bounds.size.x * 0.18f);
                bool found = false;
                for (int step = 1; step <= 4 && !found; step++)
                {
                    Vector3 shifted = localGuess;
                    shifted.x = Mathf.Clamp(
                        shifted.x + side * span * step,
                        bounds.min.x + 0.08f,
                        bounds.max.x - 0.08f);
                    found = TryRaycastLocalMesh(shifted, out localPoint, out localNormal);
                }
                if (!found)
                {
                    worldPoint = default;
                    worldNormal = default;
                    return false;
                }
            }

            worldPoint = _visualRoot.TransformPoint(localPoint);
            worldNormal = _visualRoot.TransformDirection(localNormal).normalized;
            return true;
        }

        private bool TryRaycastLocalMesh(
            Vector3 localGuess,
            out Vector3 localPoint,
            out Vector3 localNormal)
        {
            Vector3 origin = new Vector3(localGuess.x, _cachedMesh.bounds.max.y + 0.75f, localGuess.z);
            Vector3 direction = Vector3.down;
            float bestDistance = float.PositiveInfinity;
            Vector3 bestPoint = default;
            Vector3 bestNormal = Vector3.up;
            for (int index = 0; index + 2 < _triangles.Length; index += 3)
            {
                Vector3 a = _vertices[_triangles[index]];
                Vector3 b = _vertices[_triangles[index + 1]];
                Vector3 c = _vertices[_triangles[index + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.y <= 0.0001f) continue;
                if (!RayTriangle(origin, direction, a, b, c, out float distance)) continue;
                if (distance < 0f || distance >= bestDistance) continue;
                bestDistance = distance;
                bestPoint = origin + direction * distance;
                bestNormal = normal.normalized;
            }

            if (!float.IsFinite(bestDistance))
            {
                localPoint = default;
                localNormal = default;
                return false;
            }
            localPoint = bestPoint;
            localNormal = bestNormal;
            return true;
        }

        private static bool RayTriangle(
            Vector3 origin,
            Vector3 direction,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            out float distance)
        {
            const float epsilon = 0.000001f;
            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            Vector3 p = Vector3.Cross(direction, edge2);
            float determinant = Vector3.Dot(edge1, p);
            if (Mathf.Abs(determinant) < epsilon)
            {
                distance = 0f;
                return false;
            }
            float inverse = 1f / determinant;
            Vector3 t = origin - a;
            float u = Vector3.Dot(t, p) * inverse;
            if (u < 0f || u > 1f)
            {
                distance = 0f;
                return false;
            }
            Vector3 q = Vector3.Cross(t, edge1);
            float v = Vector3.Dot(direction, q) * inverse;
            if (v < 0f || u + v > 1f)
            {
                distance = 0f;
                return false;
            }
            distance = Vector3.Dot(edge2, q) * inverse;
            return distance >= 0f;
        }

        private void ApplyFoot(
            AvatarIKGoal goal,
            Transform animatedFoot,
            Vector3 position,
            Vector3 normal)
        {
            _animator.SetIKPositionWeight(goal, 1f);
            _animator.SetIKRotationWeight(goal, 1f);
            _animator.SetIKPosition(goal, position);
            Vector3 forward = Vector3.ProjectOnPlane(animatedFoot.forward, normal).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.ProjectOnPlane(_visualRoot.forward, normal).normalized;
            _animator.SetIKRotation(goal, Quaternion.LookRotation(forward, normal));
        }

        private void ApplyKneeHints(Vector3 forward, Vector3 right)
        {
            if (_leftUpperLeg == null || _rightUpperLeg == null) return;
            _animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, 0.95f);
            _animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, 0.95f);
            _animator.SetIKHintPosition(
                AvatarIKHint.LeftKnee,
                _leftUpperLeg.position + forward * 0.36f - right * 0.12f);
            _animator.SetIKHintPosition(
                AvatarIKHint.RightKnee,
                _rightUpperLeg.position + forward * 0.36f + right * 0.12f);
        }
    }

    /// <summary>
    /// Adds restrained cinematic feedback without taking camera authority away from
    /// Cinemachine: charge widens the lens, adds a restrained bloom/vignette lift,
    /// and a tiny render-only high-frequency shake communicates stored energy.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityCamera))]
    public sealed class EarthChargeCameraLookdev : MonoBehaviour
    {
        private UnityCamera _camera;
        private MagicInputController _input;
        private Volume _volume;
        private Bloom _bloom;
        private Vignette _vignette;
        private float _charge;
        private float _chargeVelocity;
        private float _lastFovOffset;
        private Vector3 _renderPosition;
        private Quaternion _renderRotation;
        private bool _renderPoseSaved;
        private float _nextResolveAt;

        private void Awake()
        {
            _camera = GetComponent<UnityCamera>();
            BuildVolume();
            ResolveInput();
        }

        private void OnDestroy()
        {
            if (_volume != null && _volume.sharedProfile != null)
                Destroy(_volume.sharedProfile);
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            if (_input == null && Time.unscaledTime >= _nextResolveAt)
            {
                _nextResolveAt = Time.unscaledTime + 1f;
                ResolveInput();
            }
            float raw = _input != null
                ? Mathf.Clamp01(Mathf.Max(_input.BendCharge01, _input.BendAmount01 * 0.72f))
                : 0f;
            _charge = Mathf.SmoothDamp(
                _charge, raw, ref _chargeVelocity,
                raw > _charge ? 0.08f : 0.16f,
                8f,
                Mathf.Max(0.0001f, Time.unscaledDeltaTime));

            float baseFov = Mathf.Clamp(_camera.fieldOfView - _lastFovOffset, 25f, 110f);
            _lastFovOffset = Mathf.Lerp(0f, 6.5f, EaseOut(_charge));
            _camera.fieldOfView = Mathf.Clamp(baseFov + _lastFovOffset, 25f, 115f);
            if (_bloom != null)
                _bloom.intensity.value = Mathf.Lerp(0.07f, 0.24f, _charge);
            if (_vignette != null)
                _vignette.intensity.value = Mathf.Lerp(0.075f, 0.145f, _charge);
        }

        private void OnPreCull()
        {
            if (_camera == null || _charge <= 0.001f || _renderPoseSaved) return;
            _renderPoseSaved = true;
            _renderPosition = transform.position;
            _renderRotation = transform.rotation;
            float time = Time.unscaledTime * Mathf.Lerp(18f, 31f, _charge);
            float x = Mathf.PerlinNoise(time, 13.7f) * 2f - 1f;
            float y = Mathf.PerlinNoise(7.3f, time * 1.07f) * 2f - 1f;
            float z = Mathf.PerlinNoise(time * 0.83f, 27.1f) * 2f - 1f;
            float positionAmplitude = Mathf.Lerp(0f, 0.0065f, _charge * _charge);
            float rotationAmplitude = Mathf.Lerp(0f, 0.14f, _charge * _charge);
            transform.position += transform.right * x * positionAmplitude +
                                  transform.up * y * positionAmplitude;
            transform.rotation = Quaternion.Euler(y * rotationAmplitude, x * rotationAmplitude, z * rotationAmplitude * 0.45f) *
                                 transform.rotation;
        }

        private void OnPostRender()
        {
            if (!_renderPoseSaved) return;
            transform.SetPositionAndRotation(_renderPosition, _renderRotation);
            _renderPoseSaved = false;
        }

        private void ResolveInput()
        {
            MagicInputController[] inputs = FindObjectsByType<MagicInputController>(
                FindObjectsInactive.Exclude);
            _input = inputs.Length > 0 ? inputs[0] : null;
        }

        private void BuildVolume()
        {
            GameObject host = new GameObject("Earth Runtime Lookdev Volume");
            host.transform.SetParent(transform, false);
            _volume = host.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 900f;
            _volume.weight = 1f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Earth Runtime Lookdev";
            _volume.sharedProfile = profile;

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0f);
            color.contrast.Override(7f);
            color.saturation.Override(-8f);
            color.colorFilter.Override(Color.white);
            WhiteBalance balance = profile.Add<WhiteBalance>(true);
            balance.temperature.Override(2f);
            balance.tint.Override(-1f);
            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.Override(1.12f);
            _bloom.intensity.Override(0.07f);
            _bloom.scatter.Override(0.54f);
            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.075f);
            _vignette.smoothness.Override(0.48f);
            DepthOfField depthOfField = profile.Add<DepthOfField>(true);
            depthOfField.active = true;
            depthOfField.mode.Override(DepthOfFieldMode.Off);
        }

        private static float EaseOut(float value)
        {
            value = Mathf.Clamp01(value);
            return 1f - (1f - value) * (1f - value);
        }
    }

    /// <summary>
    /// Grounded seismic perception: footsteps and a V-key toggle send luminous
    /// terrain-following arcs across the actual colliders. Missing support breaks an
    /// arc, so pits, cliffs and destroyed platforms remain legible instead of being
    /// painted over by a flat screen-space ring.
    /// </summary>
    [DefaultExecutionOrder(1700)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlanetMotor))]
    public sealed class EarthSeismicVision : MonoBehaviour
    {
        private const int WaveCount = 5;
        private const int ArcCount = 12;
        private const int PointsPerArc = 9;
        private readonly Pulse[] _pulses = new Pulse[WaveCount];
        private PlanetMotor _motor;
        private Material _material;
        private Vector3 _lastStepPosition;
        private int _nextPulse;
        private float _automaticPulseAt;
        private bool _inputUnavailable;

        private sealed class Pulse
        {
            public GameObject Root;
            public LineRenderer[] Arcs;
            public Vector3 Origin;
            public Vector3 Up;
            public float StartedAt;
            public float Duration;
            public bool Active;
        }

        public bool IsActive { get; private set; }

        private void Awake()
        {
            _motor = GetComponent<PlanetMotor>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _material = new Material(shader) { name = "Earth Seismic Runtime" };
                if (_material.HasProperty("_BaseColor"))
                    _material.SetColor("_BaseColor", new Color(0.12f, 0.78f, 1f, 1f));
                if (_material.HasProperty("_Color"))
                    _material.SetColor("_Color", new Color(0.12f, 0.78f, 1f, 1f));
            }
            BuildPool();
            _lastStepPosition = transform.position;
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        private void Update()
        {
            PollToggle();
            UpdatePulses();
            if (!IsActive || _motor == null || !_motor.HasStableSupport) return;

            Vector3 tangentDelta = Vector3.ProjectOnPlane(
                transform.position - _lastStepPosition,
                _motor.LocalUp);
            if (tangentDelta.magnitude >= 0.72f || Time.unscaledTime >= _automaticPulseAt)
            {
                EmitPulse(transform.position - _motor.LocalUp * 0.78f, _motor.LocalUp, 13f, 1.05f);
                _lastStepPosition = transform.position;
                _automaticPulseAt = Time.unscaledTime + 0.68f;
            }
        }

        public void SetActive(bool active)
        {
            if (IsActive == active) return;
            IsActive = active;
            Shader.SetGlobalFloat("_EarthSeismicVision", active ? 1f : 0f);
            if (active && _motor != null && _motor.HasStableSupport)
                EmitPulse(transform.position - _motor.LocalUp * 0.78f, _motor.LocalUp, 19f, 1.3f);
        }

        public void EmitPulse(Vector3 origin, Vector3 up, float radius, float duration)
        {
            Pulse pulse = _pulses[_nextPulse++ % _pulses.Length];
            pulse.Origin = origin;
            pulse.Up = up.sqrMagnitude > 0.5f ? up.normalized : transform.up;
            pulse.StartedAt = Time.unscaledTime;
            pulse.Duration = Mathf.Max(0.15f, duration);
            pulse.Active = true;
            pulse.Root.SetActive(true);
            pulse.Root.transform.localScale = Vector3.one * Mathf.Max(0.1f, radius);
        }

        private void PollToggle()
        {
            if (_inputUnavailable) return;
            try
            {
                if (global::UnityEngine.Input.GetKeyDown(KeyCode.V)) SetActive(!IsActive);
            }
            catch (InvalidOperationException)
            {
                _inputUnavailable = true;
            }
        }

        private void BuildPool()
        {
            for (int pulseIndex = 0; pulseIndex < _pulses.Length; pulseIndex++)
            {
                var root = new GameObject($"Seismic Pulse {pulseIndex + 1:00}");
                root.transform.SetParent(transform, false);
                var pulse = new Pulse
                {
                    Root = root,
                    Arcs = new LineRenderer[ArcCount],
                    Active = false
                };
                for (int arcIndex = 0; arcIndex < ArcCount; arcIndex++)
                {
                    var arcObject = new GameObject($"Arc {arcIndex + 1:00}");
                    arcObject.transform.SetParent(root.transform, false);
                    LineRenderer line = arcObject.AddComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.loop = false;
                    line.positionCount = PointsPerArc;
                    line.widthMultiplier = 0.035f;
                    line.numCapVertices = 2;
                    line.numCornerVertices = 2;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    line.receiveShadows = false;
                    line.sharedMaterial = _material;
                    pulse.Arcs[arcIndex] = line;
                }
                root.SetActive(false);
                _pulses[pulseIndex] = pulse;
            }
        }

        private void UpdatePulses()
        {
            for (int pulseIndex = 0; pulseIndex < _pulses.Length; pulseIndex++)
            {
                Pulse pulse = _pulses[pulseIndex];
                if (pulse == null || !pulse.Active) continue;
                float age01 = (Time.unscaledTime - pulse.StartedAt) / pulse.Duration;
                if (age01 >= 1f)
                {
                    pulse.Active = false;
                    pulse.Root.SetActive(false);
                    continue;
                }
                DrawPulse(pulse, age01);
            }
        }

        private void DrawPulse(Pulse pulse, float age01)
        {
            Vector3 up = pulse.Up;
            Vector3 tangent = Vector3.Cross(up,
                Mathf.Abs(Vector3.Dot(up, Vector3.forward)) < 0.88f ? Vector3.forward : Vector3.right).normalized;
            Vector3 bitangent = Vector3.Cross(up, tangent).normalized;
            float maximumRadius = pulse.Root.transform.localScale.x;
            float radius = Mathf.Lerp(0.18f, maximumRadius, 1f - (1f - age01) * (1f - age01));
            float alpha = Mathf.Sin(Mathf.Clamp01(age01) * Mathf.PI) * (IsActive ? 0.9f : 0.45f);
            Shader.SetGlobalVector("_EarthSeismicOrigin", pulse.Origin);
            Shader.SetGlobalFloat("_EarthSeismicRadius", radius);

            for (int arcIndex = 0; arcIndex < pulse.Arcs.Length; arcIndex++)
            {
                LineRenderer line = pulse.Arcs[arcIndex];
                int valid = 0;
                float startAngle = arcIndex * Mathf.PI * 2f / ArcCount;
                float arcAngle = Mathf.PI * 2f / ArcCount;
                for (int pointIndex = 0; pointIndex < PointsPerArc; pointIndex++)
                {
                    float t = pointIndex / (float)(PointsPerArc - 1);
                    float angle = startAngle + arcAngle * t;
                    Vector3 radial = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                    Vector3 candidate = pulse.Origin + radial * radius;
                    Vector3 rayOrigin = candidate + up * 2.2f;
                    if (Physics.Raycast(
                            rayOrigin,
                            -up,
                            out RaycastHit hit,
                            5.2f,
                            _motor != null ? _motor.GroundMask : ~0,
                            QueryTriggerInteraction.Ignore) &&
                        Vector3.Dot(hit.normal, up) > 0.08f)
                    {
                        line.SetPosition(pointIndex, hit.point + hit.normal * 0.026f);
                        valid++;
                    }
                    else
                    {
                        line.SetPosition(pointIndex, candidate);
                    }
                }
                line.enabled = valid >= PointsPerArc - 2;
                Color color = new Color(0.08f, 0.74f, 1f, alpha);
                line.startColor = color;
                line.endColor = new Color(0.3f, 0.94f, 1f, alpha * 0.65f);
                line.widthMultiplier = Mathf.Lerp(0.065f, 0.018f, age01);
            }
        }
    }

    /// <summary>
    /// Converts generic short puffs into layered, lingering stone dust. Existing
    /// event routing remains authoritative; this component only upgrades presentation.
    /// </summary>
    [DefaultExecutionOrder(1800)]
    internal sealed class EarthDustLookdevTuner : MonoBehaviour
    {
        private readonly HashSet<global::UnityEngine.EntityId> _configured =
            new HashSet<global::UnityEngine.EntityId>();
        private bool _configuredOnce;

        private void Update()
        {
            if (_configuredOnce) return;
            _configuredOnce = true;
            ParticleSystem[] systems = FindObjectsByType<ParticleSystem>(
                FindObjectsInactive.Include);
            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem system = systems[index];
                if (system == null || _configured.Contains(system.GetEntityId())) continue;
                string name = system.name.ToLowerInvariant();
                if (!name.Contains("dust") && !name.Contains("rubble") &&
                    !name.Contains("chip") && !name.Contains("debris"))
                    continue;
                Configure(system, name.Contains("dust"));
                _configured.Add(system.GetEntityId());
            }
        }

        private static void Configure(ParticleSystem system, bool dust)
        {
            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(main.maxParticles, dust ? 320 : 180);
            if (dust)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.72f, 1.75f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.22f, 2.15f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.62f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.38f, 0.27f, 0.18f, 0.58f),
                    new Color(0.62f, 0.49f, 0.34f, 0.25f));
            }

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.50f, 0.37f, 0.25f), 0f),
                    new GradientColorKey(new Color(0.42f, 0.31f, 0.22f), 0.55f),
                    new GradientColorKey(new Color(0.31f, 0.25f, 0.21f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(dust ? 0.52f : 0.82f, 0.12f),
                    new GradientAlphaKey(dust ? 0.28f : 0.56f, 0.58f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, dust ? 0.24f : 0.75f),
                new Keyframe(0.22f, dust ? 1f : 1.08f),
                new Keyframe(1f, dust ? 1.72f : 0.18f)));

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = new ParticleSystem.MinMaxCurve(dust ? 0.22f : 0.08f, dust ? 0.56f : 0.18f);
            noise.frequency = dust ? 0.42f : 0.8f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);
            noise.damping = true;
            noise.octaveCount = 2;

            ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = new ParticleSystem.MinMaxCurve(dust ? 0.28f : 0.12f);
            limit.dampen = dust ? 0.34f : 0.12f;

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.enableGPUInstancing = true;
                renderer.shadowCastingMode = dust
                    ? UnityEngine.Rendering.ShadowCastingMode.Off
                    : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }
    }

    /// <summary>
    /// Establishes a stronger material baseline without replacing authored families:
    /// readable bevel light, macro breakup, cavity depth and restrained mineral detail.
    /// </summary>
    [DefaultExecutionOrder(1810)]
    internal sealed class EarthMaterialLookdevTuner : MonoBehaviour
    {
        private readonly HashSet<global::UnityEngine.EntityId> _configured =
            new HashSet<global::UnityEngine.EntityId>();
        private bool _configuredOnce;

        private void Update()
        {
            if (_configuredOnce) return;
            _configuredOnce = true;
            Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null || material.shader == null ||
                    material.shader.name != "Elemental/SG Earth Master" ||
                    _configured.Contains(material.GetEntityId()))
                    continue;
                Tune(material);
                _configured.Add(material.GetEntityId());
            }
        }

        private static void Tune(Material material)
        {
            Raise(material, "_NormalStrength", 0.86f);
            Raise(material, "_ProceduralNormalStrength", 0.62f);
            Raise(material, "_MacroVariation", 0.16f);
            Raise(material, "_CavityStrength", 0.58f);
            Raise(material, "_OcclusionStrength", 0.62f);
            Raise(material, "_MineralAmount", 0.045f);
            Raise(material, "_DustAmount", 0.19f);
            if (material.HasProperty("_ExteriorColor"))
            {
                Color color = material.GetColor("_ExteriorColor");
                float luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
                if (luminance < 0.17f)
                    material.SetColor("_ExteriorColor", Color.Lerp(color, new Color(0.46f, 0.31f, 0.20f, 1f), 0.28f));
            }
            if (material.HasProperty("_InteriorColor"))
            {
                Color color = material.GetColor("_InteriorColor");
                material.SetColor("_InteriorColor", Color.Lerp(color, new Color(0.56f, 0.43f, 0.30f, 1f), 0.18f));
            }
        }

        private static void Raise(Material material, string property, float minimum)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, Mathf.Max(minimum, material.GetFloat(property)));
        }
    }
}
