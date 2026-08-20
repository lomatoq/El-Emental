using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Matter;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthResonanceController : MonoBehaviour
    {
        private const int MaximumStones = 28;

        [SerializeField] private Rigidbody casterBody;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private Collider casterCollider;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private EarthFragmentPool fragmentPool;
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private EarthResonanceProfile profile;

        private readonly EarthFragment[] _stones = new EarthFragment[MaximumStones];
        private EarthResonanceSession _session;
        private EarthResonanceChargeSample _sample;
        private Vector3 _aimDirection;
        private float _nextAutomaticFireAt;

        public bool IsCharging => _session != null && _session.IsCharging;
        public bool IsVolleyActive => _session != null && _session.IsVolleyActive;
        public float Charge01 => _sample.Charge01;
        public int ActiveStoneCount { get; private set; }
        public EarthMatterId PrimaryMatterId
        {
            get
            {
                for (int index = 0; index < _stones.Length; index++)
                {
                    EarthFragment stone = _stones[index];
                    if (stone != null && stone.gameObject.activeSelf &&
                        stone.MatterIdentity != null && stone.MatterIdentity.MatterId.IsValid)
                        return stone.MatterIdentity.MatterId;
                }
                return default;
            }
        }

        public void Configure(
            Rigidbody configuredBody,
            PlanetMotor configuredMotor,
            Transform configuredPlanetCenter,
            EarthFragmentPool configuredPool,
            MagicExecutor configuredExecutor,
            EarthResonanceProfile configuredProfile)
        {
            casterBody = configuredBody;
            motor = configuredMotor;
            planetCenter = configuredPlanetCenter;
            fragmentPool = configuredPool;
            executor = configuredExecutor;
            profile = configuredProfile;
            casterCollider = configuredBody != null ? configuredBody.GetComponent<Collider>() : null;
            RecreateSession();
        }

        public bool BeginCharge(float now)
        {
            EnsureSession();
            if (!_session.Begin(now)) return false;
            _sample = default;
            return true;
        }

        public void ContinueCharge(float now, Vector3 aimDirection)
        {
            if (!IsCharging) return;
            _aimDirection = SafeDirection(aimDirection, transform.forward);
            _sample = _session.Sample(now);
            if (_sample.Activated) EnsureStoneCount(_sample.StoneCount);
        }

        public bool ReleaseCharge(float now, Vector3 aimDirection)
        {
            if (!IsCharging) return false;
            _aimDirection = SafeDirection(aimDirection, transform.forward);
            _sample = _session.Release(now);
            if (!_sample.Activated)
            {
                ReleaseAll(false);
                return false;
            }
            EnsureStoneCount(_sample.StoneCount);
            _nextAutomaticFireAt = now;
            return true;
        }

        public bool FireNearest(Vector3 aimDirection, float now)
        {
            if (!IsVolleyActive || now < _nextAutomaticFireAt) return false;
            _nextAutomaticFireAt = now + (profile != null ? profile.AutomaticFireInterval : 0.11f);
            Vector3 aim = SafeDirection(aimDirection, transform.forward);
            int best = -1;
            float bestScore = float.NegativeInfinity;
            Vector3 origin = casterBody != null ? casterBody.worldCenterOfMass : transform.position;
            for (int index = 0; index < _stones.Length; index++)
            {
                EarthFragment stone = _stones[index];
                if (stone == null || !stone.gameObject.activeSelf) continue;
                Vector3 toStone = stone.Body.worldCenterOfMass - origin;
                float score = Vector3.Dot(toStone.normalized, aim) - toStone.sqrMagnitude * 0.002f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = index;
            }
            if (best < 0) return false;
            Launch(best, aim, 0f);
            return true;
        }

        public int FireAll(Vector3 aimDirection)
        {
            if (!IsVolleyActive) return 0;
            Vector3 aim = SafeDirection(aimDirection, transform.forward);
            Vector3 up = CurrentUp();
            Vector3 right = Vector3.Cross(up, aim).normalized;
            if (right.sqrMagnitude < 0.1f) right = Vector3.Cross(up, transform.forward).normalized;
            int launched = 0;
            for (int index = 0; index < _stones.Length; index++)
            {
                if (_stones[index] == null || !_stones[index].gameObject.activeSelf) continue;
                float signed = ((index * 0.6180339f) % 1f) * 2f - 1f;
                Vector3 fan = (aim + right * signed * 0.16f + up * (0.03f + 0.06f * Mathf.Abs(signed))).normalized;
                Launch(index, fan, launched * 0.35f);
                launched++;
            }
            return launched;
        }

        public void Cancel()
        {
            _session?.Cancel();
            ReleaseAll(false);
            _sample = default;
        }

        private void FixedUpdate()
        {
            if (_session == null) return;
            if (_session.Expire(Time.fixedUnscaledTime))
            {
                ReleaseAll(false);
                return;
            }
            if (!IsCharging && !IsVolleyActive) return;
            UpdateHoverTargets();
        }

        private void EnsureStoneCount(int desired)
        {
            desired = Mathf.Clamp(desired, 0, MaximumStones);
            for (int index = 0; index < desired; index++)
            {
                if (_stones[index] != null && _stones[index].gameObject.activeSelf) continue;
                if (fragmentPool == null || executor == null) return;
                Vector3 up = CurrentUp();
                Vector3 tangent = TangentDirection(index, up);
                float radial01 = Mathf.Sqrt((index + 0.5f) / Mathf.Max(1f, desired));
                float radius = Mathf.Lerp(1.2f, Mathf.Max(1.2f, _sample.Radius), radial01);
                Vector3 spawn = (casterBody != null ? casterBody.worldCenterOfMass : transform.position) +
                                tangent * radius - up * Mathf.Lerp(0.75f, 1.3f, radial01);
                float largestStone = profile != null ? profile.LargestStoneRadius : 0.58f;
                float smallestStone = profile != null ? profile.SmallestStoneRadius : 0.26f;
                float stoneRadius = Mathf.Lerp(largestStone, smallestStone, radial01) *
                                    Mathf.Lerp(0.86f, 1.14f, Hash01(index));
                float mass = Mathf.Max(2f, 500f * (4f / 3f) * Mathf.PI * stoneRadius * stoneRadius * stoneRadius);
                EarthFragment stone = fragmentPool.Acquire(executor, spawn, stoneRadius, mass, transform);
                if (stone == null) return;
                stone.SetTargetKind(EarthPhysicalTargetKind.ResonanceProjectile);
                stone.gameObject.layer = 2;
                stone.BeginBendControl(spawn, Vector3.zero, _sample.Charge01, BendTuning.Default);
                _stones[index] = stone;
                ActiveStoneCount++;
            }
        }

        private void UpdateHoverTargets()
        {
            Vector3 center = casterBody != null ? casterBody.worldCenterOfMass : transform.position;
            Vector3 up = CurrentUp();
            Vector3 forward = motor != null ? motor.FacingForward : Vector3.ProjectOnPlane(transform.forward, up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.Cross(transform.right, up).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            float minimumFormationRadius = profile != null ? profile.MinimumFormationRadius : 2.15f;
            float radius = Mathf.Max(minimumFormationRadius, _sample.Radius);
            int formationSlot = 0;
            for (int index = 0; index < _stones.Length; index++)
            {
                EarthFragment stone = _stones[index];
                if (stone == null || !stone.gameObject.activeSelf || !stone.IsHeld) continue;
                float radial01 = Mathf.Sqrt((formationSlot + 0.5f) /
                                            Mathf.Max(1f, ActiveStoneCount));
                float angle = formationSlot * 2.39996323f;
                Vector3 planar = (forward * Mathf.Cos(angle) + right * Mathf.Sin(angle)).normalized;
                float horizontalRadius = Mathf.Lerp(
                    minimumFormationRadius * 0.42f,
                    radius,
                    radial01);
                float canopyHeight = Mathf.Lerp(
                    Mathf.Max(1.7f, radius * 0.82f),
                    0.38f,
                    Mathf.Pow(radial01, 0.72f));
                float stagger = Mathf.Lerp(-0.10f, 0.12f, Hash01(index + 211));
                Vector3 target = center + planar * horizontalRadius + up * (canopyHeight + stagger);
                stone.UpdateBendTarget(target, casterBody != null ? casterBody.linearVelocity : Vector3.zero, _sample.Charge01);
                formationSlot++;
            }
        }

        private void Launch(int index, Vector3 direction, float spread)
        {
            EarthFragment stone = _stones[index];
            if (stone == null) return;
            float speed = profile != null ? profile.ProjectileSpeed : 34f;
            stone.gameObject.layer = 0;
            stone.LaunchProjectile(direction, Mathf.Max(18f, speed - spread), casterCollider);
            _stones[index] = null;
            ActiveStoneCount = Mathf.Max(0, ActiveStoneCount - 1);
            _session.ConsumeStone();
        }

        private void ReleaseAll(bool launch)
        {
            Vector3 up = CurrentUp();
            for (int index = 0; index < _stones.Length; index++)
            {
                EarthFragment stone = _stones[index];
                if (stone == null) continue;
                if (stone.gameObject.activeSelf)
                {
                    stone.gameObject.layer = 0;
                    if (launch) stone.LaunchProjectile((stone.transform.position - transform.position).normalized, 12f, casterCollider);
                    else stone.StopBendControl();
                }
                _stones[index] = null;
            }
            ActiveStoneCount = 0;
        }

        private Vector3 CurrentUp()
        {
            if (motor != null && motor.LocalUp.sqrMagnitude > 0.5f) return motor.LocalUp.normalized;
            if (planetCenter != null)
            {
                Vector3 radial = transform.position - planetCenter.position;
                if (radial.sqrMagnitude > 0.1f) return radial.normalized;
            }
            return transform.up;
        }

        private Vector3 TangentDirection(int index, Vector3 up)
        {
            Vector3 forward = motor != null ? motor.FacingForward : transform.forward;
            forward = Vector3.ProjectOnPlane(forward, up).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            float angle = index * 2.3999632f;
            return (forward * Mathf.Cos(angle) + right * Mathf.Sin(angle)).normalized;
        }

        private void Awake() => EnsureSession();
        private void OnDisable() => Cancel();
        private void EnsureSession()
        {
            if (_session == null) RecreateSession();
        }
        private void RecreateSession()
        {
            EarthResonanceProfileData data = profile != null ? profile.Data : EarthResonanceProfileData.Default;
            _session = new EarthResonanceSession(in data);
        }
        private static Vector3 SafeDirection(Vector3 value, Vector3 fallback) =>
            value.sqrMagnitude > 0.0001f ? value.normalized : fallback.normalized;
        private static float Hash01(int value)
        {
            uint hash = unchecked((uint)value) * 747796405u + 2891336453u;
            hash = ((hash >> ((int)(hash >> 28) + 4)) ^ hash) * 277803737u;
            hash = (hash >> 22) ^ hash;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }
    }
}
