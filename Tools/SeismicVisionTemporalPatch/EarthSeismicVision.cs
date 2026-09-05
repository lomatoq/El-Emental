using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Magic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Elemental.Presentation.VFX
{
    /// <summary>Local earth perception, composed over visible opaque depth by the world pass.</summary>
    [DefaultExecutionOrder(1700), DisallowMultipleComponent, RequireComponent(typeof(PlanetMotor))]
    public sealed class EarthSeismicVision : MonoBehaviour
    {
        private const int WaveCount = 5;
        private static readonly ProfilerMarker PublishMarker = new("Elemental.SeismicVision.Publish");
        private static readonly int ActiveId = Shader.PropertyToID("_EarthSeismicVision");
        private static readonly int WavesId = Shader.PropertyToID("_EarthSeismicWaves");
        private static readonly int StrengthsId = Shader.PropertyToID("_EarthSeismicStrengths");
        private static readonly int RadiusTravelsId = Shader.PropertyToID("_EarthSeismicRadiusTravels");
        private readonly Vector4[] _waves = new Vector4[WaveCount];
        private readonly float[] _strengths = new float[WaveCount];
        private readonly float[] _radiusTravels = new float[WaveCount];
        private readonly Pulse[] _pulses = new Pulse[WaveCount];
        private PlanetMotor _motor;
        private MagicInputController _input;
        private int _nextPulse;
        private float _nextAutomaticPulse;
        private Vector3 _lastStepPosition;
        private bool _published;

        private struct Pulse
        {
            public Vector3 Origin;
            public float StartedAt, Radius, Duration, LastPublishedRadius;
        }

        public bool Requested { get; private set; }
        public bool IsActive { get; private set; }
        public int VisiblePulseCount { get; private set; }

        private void Awake()
        {
            _motor = GetComponent<PlanetMotor>();
            _input = GetComponent<MagicInputController>();
            _lastStepPosition = transform.position;
        }

        private void Update()
        {
            if (_input != null && _input.isActiveAndEnabled && Keyboard.current != null &&
                Keyboard.current.vKey.wasPressedThisFrame)
                SetActive(!Requested);
            RefreshPerception();
        }

        public void SetActive(bool active)
        {
            Requested = active;
            RefreshPerception();
        }

        private void RefreshPerception()
        {
            using (PublishMarker.Auto())
            {
                bool active = EarthSeismicPerception.CanPerceive(Requested,
                    _input != null && _input.isActiveAndEnabled && _input.SelectedElement == ElementId.Earth,
                    _motor != null && _motor.HasStableSupport,
                    _motor != null && _motor.AcceptsMovingSupport,
                    _motor != null && _motor.IsMantling);
                if (!active)
                {
                    if (_published) ClearPerception();
                    IsActive = false;
                    return;
                }

                bool entering = !IsActive;
                IsActive = true;
                if (entering || Time.unscaledTime >= _nextAutomaticPulse ||
                    Vector3.ProjectOnPlane(transform.position - _lastStepPosition, _motor.LocalUp).sqrMagnitude >= 0.72f * 0.72f)
                {
                    EmitPulse(_motor.SupportFeetPoint(_motor.LocalUp), _motor.LocalUp, 22f, 2.2f);
                    _lastStepPosition = transform.position;
                    _nextAutomaticPulse = Time.unscaledTime + 0.68f;
                }
                VisiblePulseCount = 0;
                for (int i = 0; i < WaveCount; i++)
                {
                    Pulse pulse = _pulses[i];
                    float age = Time.unscaledTime - pulse.StartedAt;
                    float strength = pulse.Duration > 0f ? EarthSeismicPerception.Strength(age, pulse.Duration) : 0f;
                    float currentRadius = EarthSeismicPerception.Radius(age, pulse.Radius, pulse.Duration);
                    float previousRadius = Mathf.Clamp(pulse.LastPublishedRadius, 0f, currentRadius);
                    _radiusTravels[i] = currentRadius - previousRadius;
                    pulse.LastPublishedRadius = currentRadius;
                    _pulses[i] = pulse;
                    _waves[i] = new Vector4(pulse.Origin.x, pulse.Origin.y, pulse.Origin.z, currentRadius);
                    _strengths[i] = strength;
                    if (strength > 0f) VisiblePulseCount++;
                }
                Shader.SetGlobalVectorArray(WavesId, _waves);
                Shader.SetGlobalFloatArray(StrengthsId, _strengths);
                Shader.SetGlobalFloatArray(RadiusTravelsId, _radiusTravels);
                Shader.SetGlobalFloat(ActiveId, 1f);
                _published = true;
            }
        }

        public void EmitPulse(Vector3 origin, Vector3 up, float radius, float duration)
        {
            if (!IsActive) return;
            int slot = _nextPulse++ % WaveCount;
            _pulses[slot] = new Pulse
            {
                Origin = origin,
                StartedAt = Time.unscaledTime,
                Radius = Mathf.Max(0.1f, radius),
                Duration = Mathf.Max(0.15f, duration),
                LastPublishedRadius = 0f
            };
            _waves[slot] = new Vector4(origin.x, origin.y, origin.z, 0f);
            _radiusTravels[slot] = 0f;
        }

        private void ClearPerception()
        {
            Shader.SetGlobalFloat(ActiveId, 0f);
            for (int i = 0; i < WaveCount; i++)
            {
                _pulses[i] = default;
                _strengths[i] = 0f;
                _radiusTravels[i] = 0f;
            }
            Shader.SetGlobalFloatArray(StrengthsId, _strengths);
            Shader.SetGlobalFloatArray(RadiusTravelsId, _radiusTravels);
            VisiblePulseCount = 0;
            _published = false;
        }

        private void OnDisable()
        {
            if (_published) ClearPerception();
            IsActive = false;
        }
    }
}
