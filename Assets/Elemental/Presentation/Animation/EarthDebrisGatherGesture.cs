using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    /// <summary>
    /// Gives gravity-grip and repair a recognisable gathering gesture instead of
    /// leaving both hands frozen at a single aim point. The motion is presentation
    /// only: debris authority stays in MagicExecutor.
    /// </summary>
    [DefaultExecutionOrder(2050)]
    [DisallowMultipleComponent]
    public sealed class EarthDebrisGatherGesture : MonoBehaviour
    {
        private static readonly FieldInfo LeftTargetField = typeof(HumanoidCharacterPresentation).GetField(
            "leftHandTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo RightTargetField = typeof(HumanoidCharacterPresentation).GetField(
            "rightHandTarget", BindingFlags.Instance | BindingFlags.NonPublic);

        private HumanoidCharacterPresentation _presentation;
        private MagicExecutor _executor;
        private PlanetMotor _motor;
        private Transform _leftTarget;
        private Transform _rightTarget;
        private Vector3 _leftVelocity;
        private Vector3 _rightVelocity;
        private float _weight;

        private void Awake()
        {
            _presentation = GetComponent<HumanoidCharacterPresentation>();
            _executor = GetComponentInParent<MagicExecutor>();
            _motor = GetComponentInParent<PlanetMotor>();
            ResolveTargets();
        }

        private void LateUpdate()
        {
            if (_presentation == null || _executor == null) return;
            if (_leftTarget == null || _rightTarget == null) ResolveTargets();
            if (_leftTarget == null || _rightTarget == null) return;

            bool active = _executor.IsGravityWellActive || _executor.IsRepairActive;
            _weight = Mathf.MoveTowards(
                _weight,
                active ? 1f : 0f,
                Time.deltaTime / (active ? 0.10f : 0.18f));
            if (_weight <= 0.001f) return;

            Vector3 up = _motor != null && _motor.LocalUp.sqrMagnitude > 0.5f
                ? _motor.LocalUp.normalized
                : transform.up;
            Vector3 focus = _executor.GravityWellFocus;
            Vector3 shoulderCenter = transform.position + up * 0.72f;
            Vector3 aim = Vector3.ProjectOnPlane(focus - shoulderCenter, up).normalized;
            if (aim.sqrMagnitude < 0.1f) aim = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            Vector3 right = Vector3.Cross(up, aim).normalized;

            float time = Time.unscaledTime * 2.25f;
            float gather = 0.5f + 0.5f * Mathf.Sin(time * Mathf.PI);
            float spiralRadius = Mathf.Lerp(0.31f, 0.13f, gather);
            float reach = Mathf.Lerp(0.48f, 0.68f, gather);
            Vector3 basePoint = shoulderCenter + aim * reach - up * Mathf.Lerp(0.02f, 0.12f, gather);
            Vector3 leftDesired = basePoint - right * spiralRadius +
                                  up * Mathf.Sin(time) * 0.08f -
                                  aim * Mathf.Cos(time) * 0.055f;
            Vector3 rightDesired = basePoint + right * spiralRadius -
                                   up * Mathf.Sin(time + 0.65f) * 0.08f -
                                   aim * Mathf.Cos(time + 0.65f) * 0.055f;

            float response = Mathf.Lerp(0.18f, 0.075f, _weight);
            _leftTarget.position = Vector3.SmoothDamp(
                _leftTarget.position, leftDesired, ref _leftVelocity, response, 4.2f, Time.deltaTime);
            _rightTarget.position = Vector3.SmoothDamp(
                _rightTarget.position, rightDesired, ref _rightVelocity, response, 4.2f, Time.deltaTime);
            Quaternion inwardLeft = Quaternion.LookRotation((focus - leftDesired).normalized, up) *
                                     Quaternion.Euler(0f, 0f, -24f * _weight);
            Quaternion inwardRight = Quaternion.LookRotation((focus - rightDesired).normalized, up) *
                                      Quaternion.Euler(0f, 0f, 24f * _weight);
            _leftTarget.rotation = Quaternion.Slerp(_leftTarget.rotation, inwardLeft, 1f - Mathf.Exp(-14f * Time.deltaTime));
            _rightTarget.rotation = Quaternion.Slerp(_rightTarget.rotation, inwardRight, 1f - Mathf.Exp(-14f * Time.deltaTime));
        }

        private void ResolveTargets()
        {
            if (_presentation == null) return;
            _leftTarget = LeftTargetField?.GetValue(_presentation) as Transform;
            _rightTarget = RightTargetField?.GetValue(_presentation) as Transform;
        }
    }

    internal static class EarthDebrisGatherGestureBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindAnyObjectByType<EarthDebrisGatherGestureInstaller>(FindObjectsInactive.Include) != null) return;
            var host = new GameObject("Earth Debris Gesture Installer")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<EarthDebrisGatherGestureInstaller>();
        }
    }

    internal sealed class EarthDebrisGatherGestureInstaller : MonoBehaviour
    {
        private void Start()
        {
            HumanoidCharacterPresentation[] presentations = Object.FindObjectsByType<HumanoidCharacterPresentation>(
                FindObjectsInactive.Include);
            for (int index = 0; index < presentations.Length; index++)
            {
                HumanoidCharacterPresentation presentation = presentations[index];
                if (presentation != null && presentation.GetComponent<EarthDebrisGatherGesture>() == null)
                    presentation.gameObject.AddComponent<EarthDebrisGatherGesture>();
            }
        }
    }
}
