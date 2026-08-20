using Elemental.Presentation.Camera;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class EarthPillarFeedback : MonoBehaviour
    {
        [SerializeField] private EarthPillarMobility mobility;
        [SerializeField] private Transform pillar;
        [SerializeField] private Transform[] groundChips;
        [SerializeField] private PlanetCameraRig cameraRig;

        private readonly Vector3[] _chipVelocities = new Vector3[20];
        private readonly Vector3[] _chipInitialScales = new Vector3[20];
        private Vector3 _surfaceBase;
        private Vector3 _up;
        private Vector3 _side;
        private float _height;
        private float _radius;
        private float _riseSeconds;
        private float _holdSeconds;
        private float _retreatSeconds;
        private float _elapsed;
        private bool _active;

        public void Configure(
            EarthPillarMobility configuredMobility,
            Transform configuredPillar,
            Transform[] configuredGroundChips,
            PlanetCameraRig configuredCameraRig)
        {
            if (mobility != null) mobility.PillarRaised -= OnPillarRaised;
            mobility = configuredMobility;
            pillar = configuredPillar;
            groundChips = configuredGroundChips;
            cameraRig = configuredCameraRig;
            if (mobility != null && isActiveAndEnabled) mobility.PillarRaised += OnPillarRaised;
            HideAll();
        }

        private void OnEnable()
        {
            if (mobility != null) mobility.PillarRaised += OnPillarRaised;
        }

        private void OnDisable()
        {
            if (mobility != null) mobility.PillarRaised -= OnPillarRaised;
            HideAll();
        }

        private void Update()
        {
            if (!_active || pillar == null) return;
            _elapsed += Time.deltaTime;
            float rise01 = Mathf.Clamp01(_elapsed / Mathf.Max(0.05f, _riseSeconds));
            float eased = rise01 * rise01 * (3f - (2f * rise01));
            float shakeEnvelope = 1f - rise01;
            float shake = (Mathf.Sin((_elapsed * 54f) + _height) * 0.08f +
                           Mathf.Sin((_elapsed * 91f) + _radius) * 0.035f) * shakeEnvelope;
            float visibleHeight = Mathf.Max(0.04f, _height * eased);
            Vector3 fullyRaisedCenter = _surfaceBase + (_up * (_height * 0.5f));
            pillar.position = _surfaceBase + (_up * (visibleHeight * 0.5f)) + (_side * shake);
            pillar.rotation = Quaternion.FromToRotation(Vector3.up, _up) *
                              Quaternion.Euler(0f, shake * 45f, shake * 10f);
            pillar.localScale = new Vector3(_radius, visibleHeight * 0.5f, _radius);

            UpdateChips();
            float retreatStart = _riseSeconds + _holdSeconds;
            if (_elapsed <= retreatStart) return;
            float sink = Mathf.Clamp01((_elapsed - retreatStart) / Mathf.Max(0.05f, _retreatSeconds));
            float smoothSink = sink * sink * (3f - (2f * sink));
            // The launch tooth is presentation, not a permanent invisible floor.
            // Put it fully back below its source surface before the ballistic arc
            // returns, so the character never appears to fall through visible rock.
            pillar.position = fullyRaisedCenter - (_up * (_height * smoothSink));
            if (sink >= 1f) HideAll();
        }

        private void OnPillarRaised(EarthPillarLaunchEvent value)
        {
            HideAll();
            _surfaceBase = ToVector3(value.SurfaceBase);
            _up = ToVector3(value.LocalUp).normalized;
            _side = Vector3.Cross(_up, UnityEngine.Camera.main != null
                ? UnityEngine.Camera.main.transform.forward
                : Vector3.forward).normalized;
            if (_side.sqrMagnitude < 0.5f) _side = Vector3.Cross(_up, Vector3.right).normalized;
            _height = value.Height;
            _radius = value.Radius;
            _riseSeconds = value.RiseSeconds;
            _holdSeconds = Mathf.Lerp(0.08f, 0.22f, value.Charge01);
            _retreatSeconds = Mathf.Lerp(0.34f, 0.48f, value.Charge01);
            _elapsed = 0f;
            _active = true;
            if (pillar != null) pillar.gameObject.SetActive(true);
            PrepareChips(value.Tick);
            cameraRig?.AddPresentationImpulse(
                Mathf.Lerp(0.06f, 0.16f, value.Charge01),
                Mathf.Lerp(0.18f, 0.34f, value.Charge01),
                value.Tick ^ 0xE417u);
        }

        private void PrepareChips(uint seed)
        {
            if (groundChips == null) return;
            int count = Mathf.Min(groundChips.Length, _chipVelocities.Length);
            for (int index = 0; index < count; index++)
            {
                Transform chip = groundChips[index];
                if (chip == null) continue;
                float angle = ((index * 137.5f) + (seed % 31u)) * Mathf.Deg2Rad;
                Vector3 radial = ((_side * Mathf.Cos(angle)) +
                                  (Vector3.Cross(_up, _side) * Mathf.Sin(angle))).normalized;
                chip.position = _surfaceBase + (radial * (_radius * 0.55f));
                chip.rotation = Quaternion.FromToRotation(Vector3.up, _up) *
                                Quaternion.Euler(index * 17f, index * 29f, index * 11f);
                chip.localScale = Vector3.one * Mathf.Lerp(0.09f, 0.24f, (index % 5) / 4f);
                _chipInitialScales[index] = chip.localScale;
                _chipVelocities[index] = (radial * Mathf.Lerp(1.4f, 4.2f, (index % 7) / 6f)) +
                                         (_up * Mathf.Lerp(0.8f, 2.5f, (index % 4) / 3f));
                chip.gameObject.SetActive(true);
            }
        }

        private void UpdateChips()
        {
            if (groundChips == null) return;
            int count = Mathf.Min(groundChips.Length, _chipVelocities.Length);
            for (int index = 0; index < count; index++)
            {
                Transform chip = groundChips[index];
                if (chip == null || !chip.gameObject.activeSelf) continue;
                Vector3 velocity = _chipVelocities[index] - (_up * (5.5f * Time.deltaTime));
                _chipVelocities[index] = velocity;
                chip.position += velocity * Time.deltaTime;
                chip.Rotate(new Vector3(31f, 47f, 23f) * Time.deltaTime, Space.Self);
                float shrink01 = Mathf.Clamp01((_elapsed - 0.30f) / 0.50f);
                float scale01 = 1f - (shrink01 * shrink01 * (3f - (2f * shrink01)));
                chip.localScale = _chipInitialScales[index] * scale01;
                if (scale01 <= 0.001f) chip.gameObject.SetActive(false);
            }
        }

        private void HideAll()
        {
            _active = false;
            if (pillar != null) pillar.gameObject.SetActive(false);
            if (groundChips == null) return;
            for (int index = 0; index < groundChips.Length; index++)
                if (groundChips[index] != null) groundChips[index].gameObject.SetActive(false);
        }

        private static Vector3 ToVector3(Unity.Mathematics.float3 value) =>
            new Vector3(value.x, value.y, value.z);
    }
}
