using System;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Matter;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public readonly struct EarthStoneCounterImpact
    {
        public EarthStoneCounterImpact(EarthPhysicalTargetHandle source, Vector3 point, Vector3 forward,
            float mass, float radius, in EarthStoneCounterDecision decision, uint sequence)
        { Source = source; Point = point; Forward = forward; SourceMass = mass; Radius = radius;
          ClosingSpeed = decision.ClosingSpeed; RecoilSpeed = decision.RecoilSpeed;
          Tier = decision.Tier; Sequence = sequence; }
        public EarthPhysicalTargetHandle Source { get; }
        public Vector3 Point { get; }
        public Vector3 Forward { get; }
        public float SourceMass { get; }
        public float Radius { get; }
        public float ClosingSpeed { get; }
        public float RecoilSpeed { get; }
        public EarthStoneCounterTier Tier { get; }
        public uint Sequence { get; }
    }

    /// <summary>Explicitly wired Earth-only guard. A bounded sweep precedes PhysX contact delivery.</summary>
    [DefaultExecutionOrder(-700), DisallowMultipleComponent]
    public sealed class EarthStoneCounterGuard : MonoBehaviour
    {
        private static readonly ProfilerMarker Marker = new("Elemental.Earth.StoneCounter");
        private readonly Collider[] _nearby = new Collider[64];
        private readonly RaycastHit[] _occlusion = new RaycastHit[32];
        [SerializeField] private Rigidbody _defender;
        [SerializeField] private PlanetMotor _motor;
        [SerializeField] private ActiveRagdollPuppet _puppet;
        [SerializeField] private EarthRockDebrisPool _pool;
        private bool _held;
        private Vector3 _forward;
        private float _nextCounterAt;
        public bool IsGuarding => _held && isActiveAndEnabled && _defender != null &&
            !_defender.isKinematic && Time.timeScale > 0f && (_motor == null || !_motor.IsImpactStunned);
        public uint Sequence { get; private set; }
        public int QuerySaturationCount { get; private set; }
        public int RejectedPartitionCount { get; private set; }
        public string LastRejection { get; private set; } = "None";
        public event Action<EarthStoneCounterImpact> Countered;

        public void Configure(Rigidbody defender, PlanetMotor motor, EarthRockDebrisPool pool,
            ActiveRagdollPuppet puppet = null)
        { _defender = defender; _motor = motor; _pool = pool; _puppet = puppet; }

        public void SetHeld(bool held, Vector3 facing)
        {
            _held = held;
            Vector3 up = _motor != null ? _motor.LocalUp : transform.up;
            Vector3 tangent = Vector3.ProjectOnPlane(facing, up);
            _forward = tangent.sqrMagnitude > .01f ? tangent.normalized : transform.forward;
            if (!held) _nextCounterAt = 0f;
        }
        private void OnDisable() { _held = false; _nextCounterAt = 0f; }
        private void FixedUpdate() => TickGuard(Time.fixedDeltaTime);

        public bool TickGuard(float fixedDelta)
        {
            if (!IsGuarding || Time.fixedTime < _nextCounterAt) return false;
            if (_pool == null) { LastRejection = "MissingDebrisPool: wire guard through the Earth executor"; return false; }
            using (Marker.Auto())
            {
                Vector3 center = _defender.worldCenterOfMass;
                // Fixed local query budget. High-speed candidates are tested across the next physical tick.
                int count = UnityEngine.Physics.OverlapSphereNonAlloc(center + _forward * 1.5f,
                    4.5f, _nearby, ~0, QueryTriggerInteraction.Ignore);
                if (count == _nearby.Length) { QuerySaturationCount++; LastRejection = "GuardQueryFull"; return false; }
                Collider selected = null;
                IEarthPhysicalTarget selectedTarget = null;
                EarthMatterIdentity selectedIdentity = null;
                EarthStoneCounterDecision selectedDecision = default;
                float selectedRadius = 0f, nearest = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    Collider shape = _nearby[i];
                    Rigidbody body = shape != null ? shape.attachedRigidbody : null;
                    if (body == null || body == _defender || body.isKinematic ||
                        body.transform.IsChildOf(transform) || body.linearVelocity.sqrMagnitude < 4f ||
                        Vector3.Dot(body.linearVelocity, _forward) > -EarthStoneCounterPolicy.MinimumClosingSpeed) continue;
                    IEarthPhysicalTarget target = body.GetComponent<IEarthPhysicalTarget>();
                    if (target == null || !target.IsEarthTargetValid || !target.TargetHandle.IsValid ||
                        (target.TargetKind != EarthPhysicalTargetKind.Rock &&
                         target.TargetKind != EarthPhysicalTargetKind.ResonanceProjectile)) continue;
                    EarthFragment fragment = target as EarthFragment;
                    EarthRockDebris debris = target as EarthRockDebris;
                    if (fragment != null && fragment.IsHeld || debris != null && !debris.CounterGuardEligible) continue;
                    EarthMatterIdentity identity = body.GetComponent<EarthMatterIdentity>();
                    if (identity == null) continue;
                    if (identity.TryRead(out EarthMatterRecord record) &&
                        record.Phase != EarthMatterPhase.FreeDynamic && record.Phase != EarthMatterPhase.Sleeping) continue;
                    float radius = fragment != null ? fragment.Radius : debris != null ? debris.BreakRadius :
                        record.Volume > 0f ? Mathf.Pow(record.Volume * .2387324f, 1f / 3f) : shape.bounds.extents.magnitude;
                    EarthStoneCounterDecision decision = EarthStoneCounterPolicy.Evaluate(
                        (float3)(body.worldCenterOfMass - center),
                        (float3)(body.linearVelocity - _defender.linearVelocity), (float3)_forward,
                        radius, body.mass, _defender.mass, fixedDelta, _pool.SmallStoneRadius, _pool.LargeStoneRadius);
                    float distance = (body.worldCenterOfMass - center).sqrMagnitude;
                    if (!decision.Accepted || distance >= nearest || !Unoccluded(center, shape, body)) continue;
                    nearest = distance; selected = shape; selectedTarget = target;
                    selectedIdentity = identity; selectedDecision = decision; selectedRadius = radius;
                }
                if (selected == null) return false;
                Rigidbody source = selectedTarget.Body;
                Vector3 point = selected.ClosestPoint(center + _forward * EarthStoneCounterPolicy.Reach);
                var impact = new EarthStoneCounterImpact(selectedTarget.TargetHandle, point, _forward,
                    source.mass, selectedRadius, in selectedDecision, Sequence + 1u);
                // No parent retirement, recoil or punch event until the canonical partition succeeds.
                if (!_pool.TryEmitBreak(point, _forward, Vector3.zero, selectedRadius, source.mass,
                    selectedTarget.StableEarthId, selectedDecision.Fracture, 0, selectedIdentity, counterSplit: true))
                { RejectedPartitionCount++; LastRejection = _pool.LastBreakRejection; return false; }
                if (selectedTarget is EarthFragment retiredFragment) retiredFragment.RetireAfterCounterSplit();
                else if (selectedTarget is EarthRockDebris retiredDebris) retiredDebris.ResetPiece();
                else source.gameObject.SetActive(false);
                if (selectedDecision.RecoilSpeed > 0f)
                {
                    float currentBackwards = Vector3.Dot(_defender.linearVelocity, -_forward);
                    float deltaSpeed = Mathf.Max(0f, selectedDecision.RecoilSpeed - currentBackwards);
                    Vector3 recoil = -_forward * deltaSpeed;
                    if (_puppet != null) _puppet.ApplyUniformVelocityChange(recoil);
                    else _defender.AddForce(recoil, ForceMode.VelocityChange);
                    _motor?.BeginDirectedExternalLaunch(1, 8);
                }
                Sequence = impact.Sequence;
                _nextCounterAt = Time.fixedTime + .12f;
                LastRejection = "None";
                Countered?.Invoke(impact);
                return true;
            }
        }

        private bool Unoccluded(Vector3 origin, Collider target, Rigidbody targetBody)
        {
            Vector3 point = target.ClosestPoint(origin);
            Vector3 delta = point - origin;
            float distance = delta.magnitude;
            if (distance < .01f) return true;
            int count = UnityEngine.Physics.RaycastNonAlloc(origin, delta / distance,
                _occlusion, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == _occlusion.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = _occlusion[i];
                if (hit.collider == null || hit.rigidbody == _defender || hit.rigidbody == targetBody ||
                    hit.collider.transform.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }
    }
}
