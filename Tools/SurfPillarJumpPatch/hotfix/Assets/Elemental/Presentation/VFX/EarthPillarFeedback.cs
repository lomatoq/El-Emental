using Elemental.Presentation.Camera;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Geometry;
using Elemental.Runtime.World;
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
        [SerializeField] private EarthEffectsTuningProfile effectsProfile;
        [SerializeField] private EarthStoneBevelProfile stoneBevelProfile;
        [SerializeField] private EarthMaterialFeedbackHub materialFeedback;
        public void ConfigureMaterialFeedback(EarthMaterialFeedbackHub hub) => materialFeedback = hub;
        private float _nextDustAt;
        private readonly EarthStoneRenderBevelCache _renderBevels = new();
        private readonly EarthCosmeticMaterialCache cosmeticMaterials = new();

        private Vector3[] _chipVelocities = System.Array.Empty<Vector3>();
        private Vector3[] _chipInitialScales = System.Array.Empty<Vector3>();
        private Vector3[] _chipAngularVelocities = System.Array.Empty<Vector3>();
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
            PlanetCameraRig configuredCameraRig,
            EarthEffectsTuningProfile configuredEffectsProfile = null)
        {
            if (mobility != null) mobility.PillarRaised -= OnPillarRaised;
            mobility = configuredMobility;
            pillar = configuredPillar;
            groundChips = configuredGroundChips;
            cameraRig = configuredCameraRig;
            effectsProfile = configuredEffectsProfile;
            EnsureChipBuffers();
            PrepareStoneMeshes();
            if (mobility != null && isActiveAndEnabled) mobility.PillarRaised += OnPillarRaised;
            HideAll();
        }

        private void OnEnable()
        {
            // Configure runs during authoring too. Its nonserialized buffers do not
            // survive a scene/domain reload, while the authored chip references do.
            EnsureChipBuffers();
            PrepareStoneMeshes();
            if (mobility != null)
            {
                mobility.PillarRaised -= OnPillarRaised;
                mobility.PillarRaised += OnPillarRaised;
            }
        }

        private void PrepareStoneMeshes()
        {
            if (!Application.isPlaying) return;
            Bevel(pillar);
            if (groundChips != null) foreach (var chip in groundChips)
            {
                Bevel(chip);
                if (chip == null) continue;
                var renderer = chip.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                Material source = effectsProfile != null ? effectsProfile.Materials.PillarChips : renderer.sharedMaterial;
                // Legacy lab scenes predate the V5 profile binding and still use
                // EarthTriplanar, which has no controllable depth-write pass. Keep
                // that exact opaque material instead of misclassifying it as a
                // depth-free cosmetic copy. Production V5 chips continue through
                // the strict cosmetic material contract.
                Material rendered = EarthEffectRenderOrder.SupportsCosmeticDepthControl(source)
                    ? cosmeticMaterials.Get(source)
                    : source;
                EarthEffectRenderOrder.ApplyCosmeticRenderer(renderer, rendered);
            }
        }

        private void Bevel(Transform target)
        {
            if (target == null) return;
            var filter = target.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                filter.sharedMesh = _renderBevels.Get(filter.sharedMesh, stoneBevelProfile);
        }

        private void OnDestroy()
        {
            _renderBevels.Clear();
            cosmeticMaterials.Dispose();
        }

        private void EnsureChipBuffers()
        {
            int chipCount = groundChips != null ? groundChips.Length : 0;
            if (_chipVelocities.Length != chipCount)
                _chipVelocities = new Vector3[chipCount];
            if (_chipInitialScales.Length != chipCount)
                _chipInitialScales = new Vector3[chipCount];
            if (_chipAngularVelocities.Length != chipCount)
                _chipAngularVelocities = new Vector3[chipCount];
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
            if (_elapsed < _riseSeconds && _elapsed >= _nextDustAt)
            {
                _nextDustAt = _elapsed + .07f;
                materialFeedback?.Emit(EarthMaterialFeedbackKind.Emerge, _surfaceBase, _up,
                    1f, _radius, dustCount: 28, chipCount: 6);
            }
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
            _nextDustAt = 0f;
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
            EarthPillarEffectsTuning tuning = effectsProfile != null ? effectsProfile.Pillar : null;
            Vector2 sizeRange = tuning != null ? tuning.ChipSize : new Vector2(0.09f, 0.24f);
            Vector2 radialSpeed = tuning != null ? tuning.ChipRadialSpeed : new Vector2(1.4f, 4.2f);
            Vector2 upSpeed = tuning != null ? tuning.ChipUpSpeed : new Vector2(0.8f, 2.5f);
            int count = Mathf.Min(groundChips.Length, _chipVelocities.Length);
            for (int index = 0; index < count; index++)
            {
                Transform chip = groundChips[index];
                if (chip == null) continue;
                float angle = ((index * 137.5f) + (seed % 31u)) * Mathf.Deg2Rad;
                Vector3 radial = ((_side * Mathf.Cos(angle)) +
                                  (Vector3.Cross(_up, _side) * Mathf.Sin(angle))).normalized;
                chip.position = _surfaceBase + (radial * (_radius * 0.55f));
                uint spinSeed = seed ^ ((uint)(index + 1) * 193u);
                chip.rotation = Quaternion.Euler(Hash(spinSeed) * 360f,
                    Hash(spinSeed + 17u) * 360f, Hash(spinSeed + 31u) * 360f);
                _chipAngularVelocities[index] = new Vector3(
                    Mathf.Lerp(-360f, 360f, Hash(spinSeed + 47u)),
                    Mathf.Lerp(-420f, 420f, Hash(spinSeed + 61u)),
                    Mathf.Lerp(-300f, 300f, Hash(spinSeed + 79u)));
                chip.localScale = Vector3.one * Mathf.Lerp(sizeRange.x, sizeRange.y, (index % 5) / 4f);
                _chipInitialScales[index] = chip.localScale;
                _chipVelocities[index] = (radial * Mathf.Lerp(radialSpeed.x, radialSpeed.y, (index % 7) / 6f)) +
                                         (_up * Mathf.Lerp(upSpeed.x, upSpeed.y, (index % 4) / 3f));
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
                chip.Rotate(_chipAngularVelocities[index] * Time.deltaTime, Space.Self);
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

        private static float Hash(uint seed)
        {
            seed ^= seed >> 16; seed *= 0x7FEB352Du; seed ^= seed >> 15;
            return (seed & 0xFFFFFFu) / 16777215f;
        }

        private static Vector3 ToVector3(Unity.Mathematics.float3 value) =>
            new Vector3(value.x, value.y, value.z);
    }
}
