using System;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Bending;
using Elemental.Runtime.Characters;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public enum EarthCombatDummyState : byte
    {
        Grounded = 0,
        Braced = 1,
        Staggered = 2,
        FullRagdoll = 3,
        Recovering = 4
    }

    public enum EarthCombatArchetype : byte
    {
        Shaper = 0,
        Scout = 1,
        Sentinel = 2
    }

    public readonly struct EarthSeismicCounterEvent
    {
        public EarthSeismicCounterEvent(Vector3 point, Vector3 localUp, float radius, float impulse)
        {
            Point = point;
            LocalUp = localUp;
            Radius = Mathf.Max(0f, radius);
            Impulse = Mathf.Max(0f, impulse);
        }
        public Vector3 Point { get; }
        public Vector3 LocalUp { get; }
        public float Radius { get; }
        public float Impulse { get; }
    }

    public readonly struct EarthCombatResponse
    {
        public EarthCombatResponse(EarthCombatDummyState state, float holdSeconds, float retainedImpulse01)
        {
            State = state;
            HoldSeconds = math.max(0f, holdSeconds);
            RetainedImpulse01 = math.saturate(retainedImpulse01);
        }

        public EarthCombatDummyState State { get; }
        public float HoldSeconds { get; }
        public float RetainedImpulse01 { get; }
    }

    public static class EarthCombatResponseSolver
    {
        public static EarthCombatResponse Evaluate(
            float impulse,
            float kineticEnergy,
            bool braced,
            float staggerImpulse = 180f,
            float ragdollImpulse = 720f)
        {
            float braceMultiplier = braced ? 1.65f : 1f;
            float adjusted = math.max(0f, impulse) / braceMultiplier;
            float energyBoost = 1f - math.exp(-math.max(0f, kineticEnergy) / 8000f);
            float effective = adjusted * math.lerp(1f, 1.35f, energyBoost);
            if (effective >= math.max(staggerImpulse + 1f, ragdollImpulse))
                return new EarthCombatResponse(
                    EarthCombatDummyState.FullRagdoll,
                    math.lerp(1.2f, 2.8f, math.saturate(effective / (ragdollImpulse * 2f))),
                    math.saturate(adjusted / math.max(1f, ragdollImpulse)));
            if (effective >= math.max(1f, staggerImpulse))
                return new EarthCombatResponse(
                    EarthCombatDummyState.Staggered,
                    math.lerp(0.22f, 0.72f,
                        math.saturate((effective - staggerImpulse) /
                                      math.max(1f, ragdollImpulse - staggerImpulse))),
                    math.saturate(adjusted / math.max(1f, ragdollImpulse)));
            return new EarthCombatResponse(
                braced ? EarthCombatDummyState.Braced : EarthCombatDummyState.Grounded,
                0f,
                math.saturate(adjusted / math.max(1f, staggerImpulse)));
        }
    }

    /// <summary>
    /// Deterministic final guard for physical sandbox targets. Repeated hero impacts
    /// may add velocity, but they must not accumulate into an escape-speed runaway.
    /// The solver preserves the authored direction inside the arena and applies a
    /// smooth inward correction outside it; it never teleports the body.
    /// </summary>
    public static class EarthCombatMotionSafetySolver
    {
        public static float3 Stabilize(
            float3 position,
            float3 velocity,
            float maximumSpeed,
            float arenaRadius,
            float returnAcceleration,
            float deltaTime)
        {
            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(velocity)))
                return float3.zero;
            float speedLimit = math.max(1f, maximumSpeed);
            float speedSq = math.lengthsq(velocity);
            if (speedSq > speedLimit * speedLimit)
                velocity *= speedLimit / math.sqrt(speedSq);

            float radius = math.length(position);
            if (radius <= math.max(1f, arenaRadius)) return velocity;
            float3 outward = math.normalizesafe(position, new float3(0f, 1f, 0f));
            float outwardSpeed = math.max(0f, math.dot(velocity, outward));
            velocity -= outward * outwardSpeed * math.saturate(deltaTime * 8f);
            velocity -= outward * math.max(0f, returnAcceleration) * math.max(0f, deltaTime);
            speedSq = math.lengthsq(velocity);
            if (speedSq > speedLimit * speedLimit)
                velocity *= speedLimit / math.sqrt(speedSq);
            return velocity;
        }
    }

    /// <summary>
    /// Physics-readable combat target for the EarthPolishLab sandbox. It exposes
    /// brace/stagger/ragdoll/recovery without owning damage or bending authority.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class EarthCombatDummy : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float staggerImpulse = 180f;
        [SerializeField, Min(1f)] private float ragdollImpulse = 720f;
        [SerializeField, Min(0.05f)] private float recoveryBlendSeconds = 0.42f;
        [SerializeField, Min(1f)] private float maximumLinearSpeed = 26f;
        [SerializeField, Min(8f)] private float arenaRadius = 72f;
        [SerializeField, Min(0f)] private float arenaReturnAcceleration = 32f;
        [SerializeField, Min(1f)] private float maximumAngularSpeed = 14f;
        [SerializeField] private EarthCombatArchetype archetype;
        [SerializeField] private bool seismicCounterEnabled = true;
        [SerializeField] private EarthCharacterImpactTarget characterImpactAuthority;

        private Rigidbody _body;
        private float _stateUntil;
        private Quaternion _recoveryStart;
        private bool _braceRequested;
        private float _suppressImpactsUntil;
        private readonly Collider[] _counterHits = new Collider[32];

        public EarthCombatDummyState State { get; private set; }
        public EarthImpactEvent LastImpact { get; private set; }
        public event Action<EarthCombatDummyState> StateChanged;
        public event Action<EarthSeismicCounterEvent> SeismicCounterReleased;
        public EarthCombatArchetype Archetype => archetype;

        public void Configure(float configuredStaggerImpulse, float configuredRagdollImpulse)
        {
            staggerImpulse = Mathf.Max(1f, configuredStaggerImpulse);
            ragdollImpulse = Mathf.Max(staggerImpulse + 1f, configuredRagdollImpulse);
        }

        public void SetCharacterImpactAuthority(EarthCharacterImpactTarget authority)
        {
            characterImpactAuthority = authority;
        }

        public void Configure(
            EarthCombatArchetype configuredArchetype,
            float configuredStaggerImpulse,
            float configuredRagdollImpulse)
        {
            archetype = configuredArchetype;
            maximumLinearSpeed = configuredArchetype switch
            {
                EarthCombatArchetype.Scout => 30f,
                EarthCombatArchetype.Sentinel => 22f,
                _ => 26f
            };
            Configure(configuredStaggerImpulse, configuredRagdollImpulse);
        }

        public void SetBraced(bool braced)
        {
            _braceRequested = braced;
            if (State is EarthCombatDummyState.Staggered or EarthCombatDummyState.FullRagdoll or
                EarthCombatDummyState.Recovering) return;
            SetState(braced ? EarthCombatDummyState.Braced : EarthCombatDummyState.Grounded);
        }

        public void ForceFullRagdoll(float holdSeconds)
        {
            _stateUntil = Time.time + Mathf.Max(0.1f, holdSeconds);
            SetState(EarthCombatDummyState.FullRagdoll);
        }

        public void ResetCombatState(float impactGraceSeconds = 0.65f)
        {
            _braceRequested = false;
            _stateUntil = 0f;
            _suppressImpactsUntil = Time.time + Mathf.Max(0f, impactGraceSeconds);
            LastImpact = default;
            if (_body != null)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
            SetState(EarthCombatDummyState.Grounded);
        }

        public void ApplyEarthImpact(in EarthImpactEvent impact)
        {
            if (Time.time < _suppressImpactsUntil) return;
            LastImpact = impact;
            EarthSeismicCounterResult counter = EarthSeismicCounterSolver.Evaluate(
                _braceRequested && seismicCounterEnabled,
                impact.Impulse,
                impact.KineticEnergy,
                staggerImpulse * 0.72f,
                ragdollImpulse);
            EarthCombatResponse response = EarthCombatResponseSolver.Evaluate(
                impact.Impulse,
                impact.KineticEnergy,
                _braceRequested,
                staggerImpulse,
                ragdollImpulse);
            if (response.State is EarthCombatDummyState.Staggered or EarthCombatDummyState.FullRagdoll)
            {
                _stateUntil = Time.time + response.HoldSeconds;
                SetState(response.State);
            }
            if (counter.Triggered) ReleaseSeismicCounter(in impact, in counter);
        }

        private void ReleaseSeismicCounter(
            in EarthImpactEvent impact,
            in EarthSeismicCounterResult counter)
        {
            Vector3 point = new Vector3(impact.Point.x, impact.Point.y, impact.Point.z);
            Vector3 localUp = transform.position.sqrMagnitude > 0.01f
                ? transform.position.normalized
                : Vector3.up;
            int count = UnityEngine.Physics.OverlapSphereNonAlloc(
                point, counter.Radius, _counterHits, ~0, QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Rigidbody target = _counterHits[index] != null
                    ? _counterHits[index].attachedRigidbody
                    : null;
                if (target == null || target == _body || target.isKinematic) continue;
                Vector3 away = Vector3.ProjectOnPlane(target.worldCenterOfMass - point, localUp);
                if (away.sqrMagnitude < 0.01f) away = Vector3.Cross(localUp, transform.right);
                float falloff = 1f - Mathf.Clamp01(away.magnitude / Mathf.Max(0.1f, counter.Radius));
                target.AddForce((away.normalized + localUp * 0.18f).normalized *
                                counter.Impulse * falloff, ForceMode.Impulse);
            }
            SeismicCounterReleased?.Invoke(new EarthSeismicCounterEvent(
                point, localUp, counter.Radius, counter.Impulse));
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            State = EarthCombatDummyState.Grounded;
        }

        private void FixedUpdate()
        {
            if (_body == null) return;
            if (_body.isKinematic) return;
            float3 stabilized = EarthCombatMotionSafetySolver.Stabilize(
                ToFloat3(_body.position),
                ToFloat3(_body.linearVelocity),
                maximumLinearSpeed,
                arenaRadius,
                arenaReturnAcceleration,
                Time.fixedDeltaTime);
            _body.linearVelocity = new Vector3(stabilized.x, stabilized.y, stabilized.z);
            if (_body.angularVelocity.sqrMagnitude > maximumAngularSpeed * maximumAngularSpeed)
                _body.angularVelocity = _body.angularVelocity.normalized * maximumAngularSpeed;
            if ((State == EarthCombatDummyState.Staggered || State == EarthCombatDummyState.FullRagdoll) &&
                Time.time >= _stateUntil)
            {
                _recoveryStart = _body.rotation;
                _stateUntil = Time.time + recoveryBlendSeconds;
                SetState(EarthCombatDummyState.Recovering);
            }
            if (State != EarthCombatDummyState.Recovering) return;

            Vector3 up = transform.position.sqrMagnitude > 0.01f
                ? transform.position.normalized
                : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            if (forward.sqrMagnitude < 0.2f) forward = Vector3.Cross(up, Vector3.right).normalized;
            Quaternion target = Quaternion.LookRotation(forward, up);
            float remaining = Mathf.Max(0f, _stateUntil - Time.time);
            float t = 1f - remaining / Mathf.Max(0.05f, recoveryBlendSeconds);
            _body.MoveRotation(Quaternion.Slerp(_recoveryStart, target, t * t * (3f - 2f * t)));
            _body.angularVelocity *= 0.82f;
            if (Time.time < _stateUntil) return;
            SetState(_braceRequested ? EarthCombatDummyState.Braced : EarthCombatDummyState.Grounded);
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);

        private void OnCollisionEnter(Collision collision)
        {
            if (characterImpactAuthority != null) return;
            if (_body == null || collision == null || collision.contactCount == 0 ||
                Time.time < _suppressImpactsUntil) return;
            float impulse = collision.impulse.magnitude;
            float relativeSpeed = collision.relativeVelocity.magnitude;
            float mass = collision.rigidbody != null ? collision.rigidbody.mass : _body.mass;
            float energy = 0.5f * Mathf.Max(0.01f, mass) * relativeSpeed * relativeSpeed;
            ContactPoint contact = collision.GetContact(0);
            var impact = new EarthImpactEvent(
                unchecked((uint)Time.frameCount),
                0u,
                impulse,
                energy,
                mass,
                relativeSpeed,
                new float3(contact.point.x, contact.point.y, contact.point.z),
                new float3(contact.normal.x, contact.normal.y, contact.normal.z),
                EarthImpactMaterialKind.Structure);
            ApplyEarthImpact(in impact);
        }

        private void SetState(EarthCombatDummyState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
