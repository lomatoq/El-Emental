using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Voxel;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DefaultExecutionOrder(800), DisallowMultipleComponent]
    public sealed class EarthLandingSlam : MonoBehaviour
    {
        private static readonly ProfilerMarker Marker = new("Elemental.Bending.LandingSlam");
        [SerializeField] private Rigidbody body;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private EarthPillarWaveAbility wave;
        [SerializeField] private EarthLandingSlamProfile profile;
        [SerializeField] private EarthLandingSlamSettings settings = EarthLandingSlamSettings.Default;
        private EarthLandingSlamState _state;
        private bool _held, _hasPosition;
        private Vector3 _previousPosition, _contactPoint, _contactNormal;
        private Collider _contactCollider, _casterCollider;
        private float _contactTime = -100f, _safeUntil = -100f;
        private EarthCharacterImpactTarget _impactTarget;
        private VoxelPlanetBehaviour _planet;
        private VoxelEditReceipt _receipt;
        private readonly EarthFragment[] _reserved = new EarthFragment[4];
        private Vector3 _burstPoint, _burstUp;
        private float _chunkRadius;
        public EarthLandingSlamSettings Settings => settings;
        public void ConfigureProfile(EarthLandingSlamProfile value)
        { profile = value; if (profile != null) settings = profile.Settings; }
        public bool IsArmed => _state.IsArmed;
        public bool HasObservedSupport => _state.HasObservedSupport;
        public bool HasRequiredBindings => body != null && motor != null && executor != null && wave != null && _planet != null;
        public MagicExecutor Executor => executor;
        public bool IsHeld => _held;
        public bool SuppressesHardLanding => CommitCount > 0 && Time.fixedTime <= _safeUntil;
        public int CommitCount { get; private set; }
        public int LastReleasedFloorPieces { get; private set; }
        public int LastEjectedRockCount { get; private set; }
        public int LastWaveColumnCount { get; private set; }
        public Vector3 LastImpactPoint { get; private set; }
        public float LastLandingSpeed { get; private set; }
        public string LastRejection { get; private set; }
        public bool HasPendingTerrain => _receipt.IsValid;
        public void Configure(Rigidbody configuredBody, PlanetMotor configuredMotor,
            MagicExecutor configuredExecutor, EarthPillarWaveAbility configuredWave)
        {
            if (_planet != null) _planet.EditCommitted -= OnTerrainCommitted;
            body = configuredBody; motor = configuredMotor; executor = configuredExecutor; wave = configuredWave;
            _planet = executor != null ? executor.VoxelPlanet : null;
            if (_planet != null) _planet.EditCommitted += OnTerrainCommitted;
            _casterCollider = body != null ? body.GetComponent<Collider>() : null;
            _impactTarget = body != null ? body.GetComponent<EarthCharacterImpactTarget>() : null;
            if (profile != null) settings = profile.Settings;
            if (!settings.IsValid) settings = EarthLandingSlamSettings.Default;
            Cancel();
        }
        public void SetHeld(bool held) { if (_held && !held) _state.Cancel(); _held = held; }
        public void Cancel() { _held = false; _state.Cancel(); _hasPosition = false; _contactTime = -100f; }
        private void Start() => Configure(body != null ? body : GetComponent<Rigidbody>(),
            motor != null ? motor : GetComponent<PlanetMotor>(), executor != null ? executor : GetComponent<MagicExecutor>(),
            wave != null ? wave : GetComponent<EarthPillarWaveAbility>());
        private void OnEnable() { if (_planet != null) { _planet.EditCommitted -= OnTerrainCommitted; _planet.EditCommitted += OnTerrainCommitted; } }
        private void OnDisable()
        {
            Cancel(); ReleaseReservations();
            if (_planet != null) _planet.EditCommitted -= OnTerrainCommitted;
        }
        private void OnCollisionEnter(Collision collision) => ObserveContact(collision);
        private void OnCollisionStay(Collision collision) => ObserveContact(collision);
        private void ObserveContact(Collision collision)
        {
            if (motor == null || collision.collider == null || collision.contactCount == 0) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                if (Vector3.Dot(contact.normal, motor.LocalUp) < .55f) continue;
                _contactPoint = contact.point; _contactNormal = contact.normal;
                _contactCollider = collision.collider; _contactTime = Time.fixedTime; return;
            }
        }
        private void FixedUpdate()
        {
            if (!HasRequiredBindings)
            { LastRejection = "Landing slam needs explicit body, motor, MagicInput EarthExecutor/planet and wave bindings"; Cancel(); return; }
            if (!_planet.HasOnlineSimulationAuthority || _impactTarget != null && !_impactTarget.HasSimulationAuthority)
            { LastRejection = "Landing slam requires simulation authority"; Cancel(); return; }
            if (!motor.enabled || body.isKinematic)
            { LastRejection = "Landing slam needs an active physical motor"; Cancel(); return; }
            using var marker = Marker.Auto();
            Vector3 up = motor.LocalUp.normalized;
            if (_hasPosition && Vector3.Distance(body.position, _previousPosition) >
                Mathf.Max(3f, body.linearVelocity.magnitude * Time.fixedDeltaTime * 3f + 1f))
                _state.Cancel(); // Teleport/spawn is never a fall-energy source.
            _previousPosition = body.position; _hasPosition = true;
            float supportSpeed = motor.CurrentSupportFrame.IsValid
                ? math.dot(motor.CurrentSupportFrame.ContactPointVelocity, ToFloat3(up)) : 0f;
            bool contact = _contactCollider != null && Time.fixedTime - _contactTime <= Time.fixedDeltaTime * 2.5f;
            float altitude = Vector3.Distance(body.position, _planet.transform.position);
            if (!_state.Step(_held, motor.HasStableSupport, contact, altitude,
                Vector3.Dot(body.linearVelocity, up) - supportSpeed, in settings)) return;
            LastLandingSpeed = _state.LastSpeed;
            if (TryCommit(_contactPoint, _contactNormal, _contactCollider)) _safeUntil = Time.fixedTime + .18f;
        }
        private bool TryCommit(Vector3 point, Vector3 up, Collider source)
        {
            LastRejection = null;
            if (_receipt.IsValid || !_planet.GeometryReady) { LastRejection = "Terrain transaction busy"; return false; }
            var structure = source != null ? source.GetComponentInParent<EarthArenaStructure>() : null;
            if (structure == null && (source == null || source.GetComponentInParent<VoxelPlanetBehaviour>() != _planet))
            { LastRejection = "Landing requires earth terrain or authored arena stone"; return false; }
            if (structure != null && !structure.CanLandingSlamAt(point, up, settings.CraterRadius))
            { LastRejection = "No intact local arena cells remain"; return false; }
            Vector3 center = point - up * (settings.CraterRadius * .25f);
            float volume = ConservativeSolidVolume(center, settings.CraterRadius);
            var pool = executor.FragmentPool;
            if (volume > .01f && (pool == null || pool.AvailableCount < _reserved.Length))
            { LastRejection = "Physical stone pool full"; return false; }
            if (volume <= .01f && structure == null) { LastRejection = "No solid terrain at contact"; return false; }
            if (volume > .01f)
            {
                float partVolume = volume / _reserved.Length;
                _chunkRadius = Mathf.Pow(partVolume * 3f / (4f * Mathf.PI), 1f / 3f);
                float mass = pool.ResolveNewStoneMass(volume) / _reserved.Length;
                for (int i = 0; i < _reserved.Length; i++)
                {
                    _reserved[i] = pool.ReserveExtraction(executor, center, _chunkRadius, mass);
                    if (_reserved[i] == null) { ReleaseReservations(); LastRejection = "Stone reservation failed"; return false; }
                }
            }
            if (!wave.TryCastLandingImpact(point, up, 1f, out _))
            { ReleaseReservations(); LastRejection = "Radial wave pool busy"; return false; }
            LastWaveColumnCount = wave.LastColumnCount;
            LastReleasedFloorPieces = structure != null
                ? structure.TriggerLandingSlam(point, up, body.mass * LastLandingSpeed, settings.CraterRadius, _casterCollider) : 0;
            _burstPoint = point; _burstUp = up; LastEjectedRockCount = 0;
            _receipt = _planet.ApplySphereEditTransactional(_planet.transform.InverseTransformPoint(center), settings.CraterRadius, false);
            if (!_receipt.IsValid) { ReleaseReservations(); LastRejection = "Terrain edit refused"; return false; }
            LastImpactPoint = point; CommitCount++;
            uint id = 0x534C0000u ^ ((_impactTarget != null ? _impactTarget.StableFighterId : 1u) * 397u) ^ (uint)CommitCount;
            executor.Events.Emit(new TerrainEditedEvent((uint)Time.frameCount, EarthLandingSlamSettings.Ability, ToFloat3(center), settings.CraterRadius));
            executor.Events.Emit(new EarthImpactEvent((uint)Time.frameCount, id, body.mass * LastLandingSpeed,
                .5f * body.mass * LastLandingSpeed * LastLandingSpeed, body.mass, LastLandingSpeed,
                ToFloat3(point), ToFloat3(up), EarthImpactMaterialKind.HeavyBlock));
            pool?.MaterialFeedback?.Emit(EarthMaterialFeedbackKind.Impact, point, up, 1.5f,
                settings.CraterRadius, id, 0, 140, 28);
            return true;
        }
        // Count only cells whose eight corners are inside both original solid and
        // the subtraction sphere. The remaining removed material is not duplicated
        // as physical matter. This bounded, cast-only volume estimate is conservative.
        private float ConservativeSolidVolume(Vector3 center, float radius)
        {
            const int n = 6;
            float width = 2f * radius / n, volume = 0;
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) for (int z = 0; z < n; z++)
            {
                bool solid = true;
                for (int corner = 0; corner < 8 && solid; corner++)
                {
                    Vector3 offset = new Vector3(x + (corner & 1), y + ((corner >> 1) & 1), z + ((corner >> 2) & 1)) * width - Vector3.one * radius;
                    solid = offset.sqrMagnitude <= radius * radius &&
                        _planet.State.SampleDensityMaterial(ToFloat3(_planet.transform.InverseTransformPoint(center + offset))).IsSolid;
                }
                if (solid) volume += width * width * width;
            }
            return volume;
        }
        private void OnTerrainCommitted(VoxelEditReceipt receipt)
        {
            if (!_receipt.IsValid || !_receipt.Equals(receipt)) return;
            _receipt = default;
            Vector3 right = Vector3.Cross(_burstUp, Mathf.Abs(_burstUp.y) < .9f ? Vector3.up : Vector3.right).normalized;
            for (int i = 0; i < _reserved.Length; i++)
            {
                var fragment = _reserved[i]; _reserved[i] = null;
                if (fragment == null || !fragment.gameObject.activeSelf) continue;
                Vector3 outward = Quaternion.AngleAxis(i * 90f + 45f, _burstUp) * right;
                Vector3 spawn = _burstPoint + outward * (settings.CraterRadius * .65f) + _burstUp * (_chunkRadius + .15f);
                fragment.CommitExtraction(null, null, spawn + _burstUp * _chunkRadius * .92f, _burstUp, _chunkRadius);
                fragment.LaunchProjectile((outward + _burstUp * 1.2f).normalized, settings.EjectionSpeed, _casterCollider, .65f);
                LastEjectedRockCount++;
                executor.Events.Emit(new FragmentSpawnedEvent((uint)Time.frameCount, fragment.FragmentId, fragment.Mass,
                    ToFloat3(spawn), ToFloat3(_burstPoint), ToFloat3(_burstPoint), _chunkRadius));
            }
        }
        private void ReleaseReservations()
        {
            _receipt = default;
            for (int i = 0; i < _reserved.Length; i++)
            {
                var fragment = _reserved[i]; _reserved[i] = null;
                if (fragment == null) continue;
                fragment.MarkConsumedForPool(); fragment.CompleteReintegration();
            }
        }
        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }
}
