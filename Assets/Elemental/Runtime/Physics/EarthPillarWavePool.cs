using System.Collections.Generic;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public readonly struct EarthPillarWavePulse
    {
        public EarthPillarWavePulse(
            Vector3 position, Vector3 up, Vector3 outward,
            float width, float height, float crest01, uint stableId)
        {
            Position = position;
            Up = up;
            Outward = outward;
            Width = width;
            Height = height;
            Crest01 = crest01;
            StableId = stableId;
        }

        public Vector3 Position { get; }
        public Vector3 Up { get; }
        public Vector3 Outward { get; }
        public float Width { get; }
        public float Height { get; }
        public float Crest01 { get; }
        public uint StableId { get; }
    }

    [DisallowMultipleComponent]
    public sealed class EarthPillarWavePool : MonoBehaviour
    {
        private static readonly Collider[] ImpactHits = new Collider[24];

        [SerializeField, Range(64, 96)] private int capacity = 96;
        [SerializeField] private Mesh columnMesh;
        [SerializeField] private Mesh[] columnMeshVariants;
        [SerializeField] private Material columnMaterial;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private EarthPillarWaveProfile profile;

        private readonly List<EarthPillarWaveColumn> _columns = new List<EarthPillarWaveColumn>(96);
        private int _reuseCursor;
        private uint _nextPulseId = 1u;

        public event System.Action<EarthPillarWavePulse> ColumnBurst;

        public void Configure(
            int configuredCapacity,
            Mesh mesh,
            Material material,
            Transform configuredPlanetCenter,
            EarthPillarWaveProfile configuredProfile)
        {
            capacity = Mathf.Clamp(configuredCapacity, 64, 96);
            columnMesh = mesh;
            columnMaterial = material;
            planetCenter = configuredPlanetCenter;
            profile = configuredProfile;
        }

        public void ConfigureMeshVariants(params Mesh[] meshes)
        {
            columnMeshVariants = meshes;
            for (int index = 0; index < _columns.Count; index++)
            {
                MeshFilter filter = _columns[index].GetComponent<MeshFilter>();
                if (filter != null && meshes != null && meshes.Length > 0)
                    filter.sharedMesh = meshes[index % meshes.Length];
            }
        }

        private void Awake()
        {
            for (int index = 0; index < capacity; index++) CreateColumn();
        }

        public int Launch(
            Vector3 surfaceOrigin,
            Vector3 localUp,
            Vector3 forward,
            float sectorCharge01,
            float powerCharge01,
            Rigidbody caster)
        {
            EarthPillarWaveSample[] samples;
            if (profile != null)
            {
                EarthPillarWaveTuning tuning = profile.Tuning;
                samples = EarthPillarWaveSolver.Build(sectorCharge01, powerCharge01, in tuning);
            }
            else samples = EarthPillarWaveSolver.Build(sectorCharge01, powerCharge01);
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            float planetRadius = Mathf.Max(1f, Vector3.Distance(surfaceOrigin, center));
            Vector3 up = localUp.sqrMagnitude > 0.5f ? localUp.normalized : (surfaceOrigin - center).normalized;
            Vector3 tangentForward = Vector3.ProjectOnPlane(forward, up).normalized;
            if (tangentForward.sqrMagnitude < 0.5f) tangentForward = Vector3.Cross(up, Vector3.right).normalized;
            float impulse = Mathf.Lerp(
                profile != null ? profile.MinimumImpulse : 85f,
                profile != null ? profile.MaximumImpulse : 420f,
                Mathf.Clamp01(powerCharge01));
            for (int index = 0; index < samples.Length; index++)
            {
                EarthPillarWaveSample sample = samples[index];
                Vector3 tangentDirection = Quaternion.AngleAxis(sample.AngleDegrees, up) * tangentForward;
                float arcRadians = sample.ArcDistance / planetRadius;
                Vector3 radial = up * Mathf.Cos(arcRadians) + tangentDirection * Mathf.Sin(arcRadians);
                Vector3 columnUp = radial.normalized;
                Vector3 surface = center + (columnUp * planetRadius);
                Vector3 columnForward = Vector3.ProjectOnPlane(tangentDirection, columnUp).normalized;
                EarthPillarWaveColumn column = Acquire();
                column.Schedule(
                    this,
                    surface,
                    columnUp,
                    columnForward,
                    sample.Height,
                    sample.Width,
                    sample.StartDelay,
                    sample.HoldDuration,
                    sample.Crest01,
                    _nextPulseId++,
                    impulse,
                    caster,
                    profile,
                    ImpactHits);
            }
            return samples.Length;
        }

        private EarthPillarWaveColumn Acquire()
        {
            for (int index = 0; index < _columns.Count; index++)
                if (!_columns[index].gameObject.activeSelf) return _columns[index];
            EarthPillarWaveColumn column = _columns[_reuseCursor];
            _reuseCursor = (_reuseCursor + 1) % _columns.Count;
            column.ResetColumn();
            return column;
        }

        private EarthPillarWaveColumn CreateColumn()
        {
            GameObject go = new GameObject($"Earth Wave Column {_columns.Count + 1:00}");
            go.transform.SetParent(transform, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = columnMeshVariants != null && columnMeshVariants.Length > 0
                ? columnMeshVariants[_columns.Count % columnMeshVariants.Length]
                : columnMesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = columnMaterial;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            EarthPillarWaveColumn column = go.AddComponent<EarthPillarWaveColumn>();
            go.SetActive(false);
            _columns.Add(column);
            return column;
        }

        internal void ReportBurst(in EarthPillarWavePulse pulse) => ColumnBurst?.Invoke(pulse);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider), typeof(MeshRenderer))]
    public sealed class EarthPillarWaveColumn : MonoBehaviour
    {
        private Rigidbody _body;
        private BoxCollider _collider;
        private MeshRenderer _renderer;
        private EarthPillarWaveProfile _profile;
        private Collider[] _impactHits;
        private Rigidbody _caster;
        private ActiveRagdollPuppet _casterPuppet;
        private Vector3 _surface;
        private Vector3 _up;
        private Vector3 _outward;
        private Vector3 _fullScale;
        private float _delay;
        private float _holdDuration;
        private float _impulse;
        private float _elapsed;
        private bool _impacted;
        private EarthPillarWavePool _owner;
        private Quaternion _baseRotation;
        private float _crest01;
        private uint _stableId;
        private readonly List<Collider> _ignoredCasterColliders = new List<Collider>(16);

        public void Schedule(
            EarthPillarWavePool owner,
            Vector3 surface,
            Vector3 up,
            Vector3 forward,
            float height,
            float width,
            float delay,
            float holdDuration,
            float crest01,
            uint stableId,
            float impulse,
            Rigidbody caster,
            EarthPillarWaveProfile profile,
            Collider[] impactHits)
        {
            Resolve();
            RestoreCasterCollisions();
            _owner = owner;
            _profile = profile;
            _impactHits = impactHits;
            _caster = caster;
            _casterPuppet = caster != null ? caster.GetComponent<ActiveRagdollPuppet>() : null;
            _up = up.normalized;
            _outward = Vector3.ProjectOnPlane(surface - (caster != null ? caster.worldCenterOfMass : surface - forward), _up).normalized;
            if (_outward.sqrMagnitude < 0.5f) _outward = forward;
            // These are squat geological teeth, not thin fence boards. Extra depth in
            // the travel direction makes each sample read as an uprooted ground block.
            _fullScale = new Vector3(
                width,
                height,
                width * Mathf.Lerp(0.82f, 1.18f, Mathf.Clamp01(crest01)));
            _surface = surface;
            _delay = Mathf.Max(0f, delay);
            _holdDuration = Mathf.Max(0.05f, holdDuration);
            _impulse = impulse;
            _crest01 = Mathf.Clamp01(crest01);
            _stableId = stableId;
            _elapsed = 0f;
            _impacted = false;
            _baseRotation = Quaternion.AngleAxis((Mathf.Repeat(delay * 173f, 14f) - 7f), _up) *
                            Quaternion.LookRotation(forward, _up);
            transform.SetPositionAndRotation(
                _surface + (_up * height * 0.0125f),
                _baseRotation);
            transform.localScale = new Vector3(width * 0.70f, height * 0.025f, width * 0.36f);
            _renderer.enabled = false;
            _collider.enabled = false;
            _body.isKinematic = true;
            gameObject.SetActive(true);
            IgnoreCasterCollisions();
        }

        public void ResetColumn()
        {
            Resolve();
            _renderer.enabled = false;
            _collider.enabled = false;
            RestoreCasterCollisions();
            gameObject.SetActive(false);
        }

        private void IgnoreCasterCollisions()
        {
            _ignoredCasterColliders.Clear();
            if (_caster == null || _collider == null) return;
            _caster.GetComponentsInChildren(false, _ignoredCasterColliders);
            for (int index = _ignoredCasterColliders.Count - 1; index >= 0; index--)
            {
                Collider casterCollider = _ignoredCasterColliders[index];
                if (casterCollider == null || casterCollider == _collider)
                {
                    _ignoredCasterColliders.RemoveAt(index);
                    continue;
                }
                UnityEngine.Physics.IgnoreCollision(_collider, casterCollider, true);
            }
        }

        private void RestoreCasterCollisions()
        {
            if (_collider != null)
            {
                for (int index = 0; index < _ignoredCasterColliders.Count; index++)
                {
                    Collider casterCollider = _ignoredCasterColliders[index];
                    if (casterCollider != null)
                        UnityEngine.Physics.IgnoreCollision(_collider, casterCollider, false);
                }
            }
            _ignoredCasterColliders.Clear();
        }

        private void FixedUpdate()
        {
            _elapsed += Time.fixedDeltaTime;
            if (_elapsed < _delay) return;
            float localTime = _elapsed - _delay;
            float rise = _profile != null ? _profile.ColumnRiseSeconds : 0.36f;
            float hold = _holdDuration;
            float retreat = _profile != null ? _profile.ColumnRetreatSeconds : 0.46f;
            EarthPillarWaveMotionSample motion = EarthPillarWaveSolver.EvaluateMotion(
                localTime, rise, hold, retreat);
            if (motion.Complete)
            {
                ResetColumn();
                return;
            }
            if (!_renderer.enabled)
            {
                _renderer.enabled = true;
            }
            float visibleHeight = Mathf.Max(0.012f, _fullScale.y * motion.Height01);
            float sink = _fullScale.y * 0.18f * motion.Sink01;
            _body.MovePosition(_surface - (_up * sink) + (_up * visibleHeight * 0.5f));
            float kick = Mathf.Sin(Mathf.Clamp01(localTime / Mathf.Max(0.05f, rise)) * Mathf.PI) *
                         Mathf.Lerp(1.5f, 4.2f, _crest01);
            _body.MoveRotation(Quaternion.AngleAxis(kick, transform.right) * _baseRotation);
            transform.localScale = new Vector3(
                _fullScale.x * motion.Width01,
                visibleHeight,
                _fullScale.z * motion.Width01);
            _collider.enabled = motion.Height01 >= 0.16f && motion.Sink01 < 0.72f;
            if (!_impacted && localTime <= rise && motion.Height01 >= 0.56f)
            {
                _impacted = true;
                EarthPillarWavePulse pulse = new EarthPillarWavePulse(
                    _surface, _up, _outward, _fullScale.x, _fullScale.y, _crest01, _stableId);
                _owner?.ReportBurst(in pulse);
                ApplyImpact();
            }
        }

        private void ApplyImpact()
        {
            float radius = _profile != null ? _profile.ImpactRadius : 1.05f;
            int count = UnityEngine.Physics.OverlapSphereNonAlloc(
                transform.position + (_up * _fullScale.y * 0.28f),
                radius,
                _impactHits,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Collider hit = _impactHits[index];
                if (hit == null || hit.attachedRigidbody == _body || hit.attachedRigidbody == _caster ||
                    (_casterPuppet != null && _casterPuppet.OwnsCollider(hit))) continue;
                EarthWall wall = hit.GetComponentInParent<EarthWall>();
                if (wall == null) wall = hit.GetComponent<EarthWallPiece>()?.Owner;
                wall?.ApplyStructureImpact(transform.position, _outward + _up, _impulse);
                Rigidbody target = hit.attachedRigidbody;
                if (target == null || target.isKinematic) continue;
                target.AddForce((_outward * 0.55f + _up).normalized * _impulse, ForceMode.Impulse);
            }
        }

        private void Resolve()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (_collider == null) _collider = GetComponent<BoxCollider>();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
        }
    }
}
