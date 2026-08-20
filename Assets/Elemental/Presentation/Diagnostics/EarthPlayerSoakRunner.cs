using System;
using System.IO;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Presentation.Diagnostics
{
    /// <summary>
    /// Opt-in player-build soak harness. It is inert in ordinary play and exists only
    /// when -earthSoakSeconds is supplied. It drives real locomotion and bounded hero
    /// systems, records percentiles, validates finite bodies, then exits explicitly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthPlayerSoakRunner : MonoBehaviour, IPlanetMotorInputSource
    {
        private float _duration;
        private string _output;
        private float _startedAt;
        private float _nextIntegrityCheck;
        private int _cyclePhase = -1;
        private bool _failed;
        private PlanetMotor _motor;
        private Rigidbody _motorBody;
        private EarthArmorController _armor;
        private EarthResonanceController _resonance;
        private EarthSurfController _surf;
        private EarthPillarWaveAbility _wave;
        private EarthPerformanceTelemetry _telemetry;
        private Rigidbody[] _bodies;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!TryReadArguments(out float seconds, out string output)) return;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            var host = new GameObject("Earth Player Soak Runner");
            DontDestroyOnLoad(host);
            EarthPlayerSoakRunner runner = host.AddComponent<EarthPlayerSoakRunner>();
            runner._duration = Mathf.Clamp(seconds, 5f, 7200f);
            runner._output = output;
            runner._startedAt = Time.realtimeSinceStartup;
        }

        public PlanetMotorCommand SampleCommand(uint tick)
        {
            float elapsed = Time.realtimeSinceStartup - _startedAt;
            float angle = elapsed * 0.41f;
            var move = new float2(math.sin(angle) * 0.62f, 0.78f);
            bool jump = Mathf.FloorToInt(elapsed * 0.5f) != Mathf.FloorToInt((elapsed - Time.fixedDeltaTime) * 0.5f) &&
                        Mathf.FloorToInt(elapsed * 0.5f) % 4 == 0;
            return new PlanetMotorCommand(tick, move, jump);
        }

        private void Update()
        {
            ResolveSceneSystems();
            float elapsed = Time.realtimeSinceStartup - _startedAt;
            DriveHeroCycle(elapsed);
            if (elapsed >= _nextIntegrityCheck)
            {
                _nextIntegrityCheck = elapsed + 1f;
                ValidateBodies();
            }
            if (elapsed < _duration) return;

            if (_telemetry != null)
            {
                string path = string.IsNullOrWhiteSpace(_output)
                    ? Path.Combine(Application.persistentDataPath, "EarthPlayerSoak.json")
                    : _output;
                _telemetry.WriteSnapshot(path);
                Debug.Log($"[EarthSoak] Wrote telemetry: {path}");
            }
            Debug.Log($"[EarthSoak] Completed {_duration:F1}s; failed={_failed}.");
            Application.Quit(_failed ? 2 : 0);
            enabled = false;
        }

        private void ResolveSceneSystems()
        {
            if (_motor == null)
            {
                _motor = FindAnyObjectByType<PlanetMotor>();
                if (_motor != null)
                {
                    _motor.ConfigureInputSource(this);
                    _motorBody = _motor.GetComponent<Rigidbody>();
                }
            }
            if (_armor == null) _armor = FindAnyObjectByType<EarthArmorController>();
            if (_resonance == null) _resonance = FindAnyObjectByType<EarthResonanceController>();
            if (_surf == null) _surf = FindAnyObjectByType<EarthSurfController>();
            if (_wave == null) _wave = FindAnyObjectByType<EarthPillarWaveAbility>();
            if (_telemetry == null) _telemetry = FindAnyObjectByType<EarthPerformanceTelemetry>();
            if (_bodies == null && _motor != null)
                _bodies = FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude);
        }

        private void DriveHeroCycle(float elapsed)
        {
            if (_motor == null) return;
            int phase = Mathf.FloorToInt(elapsed % 20f);
            if (phase == _cyclePhase)
            {
                if (phase >= 10 && phase <= 12)
                    _resonance?.ContinueCharge(Time.time, _motor.FacingForward);
                if (phase >= 16 && phase <= 18)
                    _surf?.Continue(new Vector2(0.15f, 1f), _motor.FacingForward);
                return;
            }
            _cyclePhase = phase;
            switch (phase)
            {
                case 1:
                    _wave?.BeginCharge(0.25f);
                    _wave?.SetShiftHeldSeconds(0.55f);
                    _wave?.ReleaseCharge();
                    break;
                case 3:
                    _armor?.Begin();
                    break;
                case 4:
                case 5:
                    _armor?.ApplyWheel(120f, Time.unscaledTime);
                    break;
                case 6:
                    _armor?.FireNearest(_motor.FacingForward);
                    break;
                case 7:
                    _armor?.ReleaseAsDebris();
                    break;
                case 10:
                    _resonance?.BeginCharge(Time.time);
                    break;
                case 13:
                    _resonance?.ReleaseCharge(Time.time, _motor.FacingForward);
                    break;
                case 14:
                    _resonance?.FireNearest(_motor.FacingForward, Time.time);
                    break;
                case 15:
                    _resonance?.Cancel();
                    break;
                case 16:
                    _surf?.Begin(Time.time, _motor.FacingForward);
                    break;
                case 19:
                    _surf?.Release(Time.time);
                    break;
            }
        }

        private void ValidateBodies()
        {
            if (_bodies == null) return;
            for (int index = 0; index < _bodies.Length; index++)
            {
                Rigidbody body = _bodies[index];
                if (body == null || !body.gameObject.activeInHierarchy) continue;
                Vector3 position = body.position;
                Vector3 velocity = body.linearVelocity;
                if (float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z) &&
                    float.IsFinite(velocity.x) && float.IsFinite(velocity.y) && float.IsFinite(velocity.z) &&
                    velocity.sqrMagnitude < 22500f) continue;
                _failed = true;
                Debug.LogError($"[EarthSoak] Non-finite or runaway body: {body.name}, p={position}, v={velocity}.");
            }
            if (_motorBody != null && _motorBody.position.magnitude > 200f)
            {
                _failed = true;
                Debug.LogError($"[EarthSoak] Player escaped the bounded world: {_motorBody.position}.");
            }
        }

        private static bool TryReadArguments(out float seconds, out string output)
        {
            seconds = 0f;
            output = null;
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                if (argument.StartsWith("-earthSoakSeconds=", StringComparison.OrdinalIgnoreCase))
                    float.TryParse(argument.Substring(argument.IndexOf('=') + 1),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out seconds);
                else if (argument.StartsWith("-earthSoakOutput=", StringComparison.OrdinalIgnoreCase))
                    output = argument.Substring(argument.IndexOf('=') + 1).Trim('"');
            }
            return seconds >= 5f;
        }
    }
}
