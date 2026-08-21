using System.Reflection;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    internal static class EarthSurfIkOwnershipBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindFirstObjectByType<EarthSurfIkOwnershipInstaller>(
                    FindObjectsInactive.Include) != null)
                return;
            var host = new GameObject("Earth Surf IK Ownership Installer")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<EarthSurfIkOwnershipInstaller>();
        }
    }

    internal sealed class EarthSurfIkOwnershipInstaller : MonoBehaviour
    {
        private float _nextScanAt;

        private void Update()
        {
            if (Time.unscaledTime < _nextScanAt) return;
            _nextScanAt = Time.unscaledTime + 1f;
            EarthSurfFootContactRescue[] rescuers =
                Object.FindObjectsByType<EarthSurfFootContactRescue>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < rescuers.Length; index++)
            {
                EarthSurfFootContactRescue rescue = rescuers[index];
                if (rescue == null) continue;
                if (rescue.GetComponent<EarthAnimatorIkBaselineCapture>() == null)
                    rescue.gameObject.AddComponent<EarthAnimatorIkBaselineCapture>();
                if (rescue.GetComponent<EarthSurfPelvisOwnershipOverride>() == null)
                    rescue.gameObject.AddComponent<EarthSurfPelvisOwnershipOverride>();
            }
        }
    }

    /// <summary>
    /// Captures the authored body position before any regular IK component applies a
    /// pelvis correction. The surf renderer can then replace, rather than stack on,
    /// the collider-based pelvis solve.
    /// </summary>
    [DefaultExecutionOrder(-2200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class EarthAnimatorIkBaselineCapture : MonoBehaviour
    {
        private Animator _animator;

        public Vector3 BodyPosition { get; private set; }
        public Quaternion BodyRotation { get; private set; }
        public int CapturedFrame { get; private set; } = -1;

        private void Awake() => _animator = GetComponent<Animator>();

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null) return;
            BodyPosition = _animator.bodyPosition;
            BodyRotation = _animator.bodyRotation;
            CapturedFrame = Time.frameCount;
        }
    }

    /// <summary>
    /// Final owner of the pelvis only while surfing. The regular pose controller may
    /// still update its foot-lock state, but its collider-derived pelvis offset is
    /// discarded before rendering so it cannot compound with the rendered-board IK.
    /// </summary>
    [DefaultExecutionOrder(2200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator), typeof(EarthSurfFootContactRescue))]
    public sealed class EarthSurfPelvisOwnershipOverride : MonoBehaviour
    {
        private static readonly FieldInfo PelvisOffsetField =
            typeof(EarthSurfFootContactRescue).GetField(
                "_pelvisOffset",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private Animator _animator;
        private EarthSurfFootContactRescue _rescue;
        private EarthAnimatorIkBaselineCapture _baseline;
        private EarthSurfController _surf;
        private PlanetMotor _motor;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _rescue = GetComponent<EarthSurfFootContactRescue>();
            _baseline = GetComponent<EarthAnimatorIkBaselineCapture>();
            if (_baseline == null)
                _baseline = gameObject.AddComponent<EarthAnimatorIkBaselineCapture>();
            _surf = GetComponentInParent<EarthSurfController>();
            _motor = GetComponentInParent<PlanetMotor>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null || _rescue == null ||
                _baseline == null || _baseline.CapturedFrame != Time.frameCount ||
                _surf == null || !_surf.IsActive)
                return;

            float pelvisOffset = PelvisOffsetField?.GetValue(_rescue) is float value
                ? value
                : 0f;
            Vector3 up = _motor != null && _motor.LocalUp.sqrMagnitude > 0.5f
                ? _motor.LocalUp.normalized
                : transform.up;
            _animator.bodyPosition = _baseline.BodyPosition + up * pelvisOffset;
        }
    }
}
