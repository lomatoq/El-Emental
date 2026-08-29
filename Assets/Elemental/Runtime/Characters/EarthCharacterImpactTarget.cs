using System;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Combat;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class EarthCharacterImpactTarget : MonoBehaviour
    {
        private const int DedupeCapacity = 8;
        private static readonly ProfilerMarker ResolveMarker =
            new ProfilerMarker("Elemental.Character.ImpactResolve");

        [SerializeField] private EarthDuelFighterId fighterId;
        [SerializeField] private uint stableFighterId = 1u;
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private EarthMvpDuelController duelController;
        [SerializeField] private CharacterImpactResponseProfile responseProfile;

        private readonly uint[] _recentSourceIds = new uint[DedupeCapacity];
        private readonly uint[] _recentTicks = new uint[DedupeCapacity];
        private readonly float[] _recentTimes = new float[DedupeCapacity];
        private int _dedupeCursor;
        private float _suppressUntil;
        private EarthLocalizedHitClusterState _stoneCluster;
        private HumanoidRagdollRig _visibleRagdoll;
        private EarthCharacterImpactTuning _tuning = EarthCharacterImpactTuning.Default;

        public EarthDuelFighterId FighterId => fighterId;
        public uint StableFighterId => stableFighterId;
        public Rigidbody Body => targetBody;
        public EarthCharacterImpactResponse LastResponse { get; private set; }
        public float LastEffectiveVelocityChange { get; private set; }
        public int AcceptedImpactCount { get; private set; }

        public event Action<EarthCharacterImpactResponse> ImpactResolved;

        public void Configure(
            EarthDuelFighterId configuredFighterId,
            uint configuredStableFighterId,
            Rigidbody configuredBody,
            EarthMvpDuelController configuredDuel = null,
            CharacterImpactResponseProfile configuredProfile = null)
        {
            fighterId = configuredFighterId;
            stableFighterId = configuredStableFighterId != 0u ? configuredStableFighterId : 1u;
            targetBody = configuredBody;
            duelController = configuredDuel;
            responseProfile = configuredProfile != null ? configuredProfile : responseProfile;
            _tuning = responseProfile != null ? responseProfile.Tuning : EarthCharacterImpactTuning.Default;
            _visibleRagdoll = GetComponentInChildren<HumanoidRagdollRig>(true);
            ClearDedupe();
        }

        public void BindDuel(EarthMvpDuelController duel) => duelController = duel;

        public void SuppressImpacts(float seconds)
        {
            _suppressUntil = Mathf.Max(_suppressUntil, Time.time + Mathf.Max(0f, seconds));
            ClearDedupe();
        }

        public CharacterOutcome ResolveFallLanding(
            Vector3 point,
            float fallDistance,
            float downwardImpactSpeed)
        {
            var input = new CharacterOutcomeInput(
                EarthCharacterImpactSourceKind.FallLanding,
                fallDistance,
                downwardImpactSpeed,
                downwardImpactSpeed * 0.42f);
            CharacterOutcome outcome = CharacterOutcomeResolver.Resolve(in input);
            if (outcome == CharacterOutcome.Knockout)
            {
                Vector3 up = transform.position.sqrMagnitude > 0.1f
                    ? transform.position.normalized
                    : transform.up;
                duelController?.RequestKnockout(
                    fighterId,
                    new RagdollHandoff(point, up * Mathf.Min(4.5f, downwardImpactSpeed * 0.25f), true));
            }
            return outcome;
        }

        public EarthCharacterImpactResponse ApplyImpact(
            Vector3 point,
            Vector3 direction,
            float impulse,
            EarthCharacterImpactSourceKind sourceKind,
            uint sourceStableId,
            float closingSpeed = 0f,
            float strength01 = 0f,
            uint tick = 0u)
        {
            using (ResolveMarker.Auto())
            {
                if (Time.time < _suppressUntil || targetBody == null || impulse <= 0f)
                    return EarthCharacterImpactResponse.Ignore;
                if (tick == 0u) tick = CurrentPhysicsTick;
                float impactTime = Time.time;
                if (IsDuplicate(sourceKind, sourceStableId, tick, impactTime))
                    return EarthCharacterImpactResponse.Ignore;

                Vector3 safeDirection = direction.sqrMagnitude > 0.0001f
                    ? direction.normalized
                    : transform.up;
                var impact = new EarthCharacterImpact(
                    sourceStableId,
                    tick,
                    sourceKind,
                    ToFloat3(point),
                    ToFloat3(safeDirection),
                    impulse,
                    Mathf.Max(0.01f, targetBody.mass),
                    closingSpeed,
                    strength01);
                EarthCharacterImpactResolution resolution = EarthCharacterImpactSolver.Resolve(
                    in impact,
                    in _tuning,
                    responseProfile != null
                        ? responseProfile.ResponseMode
                        : ImpactResponseMode.Legacy);
                bool stoneImpact = IsStoneImpact(sourceKind);
                bool clusteredStoneRagdoll = stoneImpact && RegisterStoneCluster(
                    point,
                    sourceStableId,
                    resolution.EffectiveVelocityChange);
                EarthCharacterImpactResponse response = resolution.Response;
                if (stoneImpact)
                {
                    _visibleRagdoll?.ApplyLocalizedRagdollImpulse(
                        point,
                        safeDirection,
                        resolution.EffectiveVelocityChange);
                    if (clusteredStoneRagdoll)
                        response = EarthCharacterImpactResponse.Knockout;
                    else if (response == EarthCharacterImpactResponse.Knockout)
                        response = EarthCharacterImpactResponse.Stagger;
                }
                Remember(sourceStableId, tick, impactTime);
                AcceptedImpactCount++;
                LastResponse = response;
                LastEffectiveVelocityChange = resolution.EffectiveVelocityChange;

                Vector3 requestedVelocityChange = safeDirection * resolution.EffectiveVelocityChange;
                Vector3 up = transform.position.sqrMagnitude > 0.1f
                    ? transform.position.normalized
                    : transform.up;
                EarthCharacterLaunchBudget launchBudget = EarthCharacterLaunchBudgetSolver.Resolve(
                    sourceKind,
                    responseProfile != null
                        ? responseProfile.MaximumRagdollRise
                        : EarthRagdollLaunchLimiter.DefaultMaximumRiseMeters,
                    responseProfile != null
                        ? responseProfile.MaximumRagdollTangentSpeed
                        : EarthRagdollLaunchLimiter.DefaultMaximumTangentSpeed);
                float3 limited = EarthRagdollLaunchLimiter.LimitVelocityChange(
                    ToFloat3(targetBody.linearVelocity),
                    ToFloat3(requestedVelocityChange),
                    ToFloat3(up),
                    EarthRagdollLaunchLimiter.DefaultGravityMagnitude,
                    launchBudget.MaximumRiseMeters,
                    launchBudget.MaximumTangentSpeed);
                Vector3 velocityChange = new Vector3(limited.x, limited.y, limited.z);
                if (stoneImpact && !clusteredStoneRagdoll)
                    velocityChange = Vector3.ClampMagnitude(
                        velocityChange,
                        responseProfile != null ? responseProfile.SingleStoneRootVelocity : 0.8f);
                if (response == EarthCharacterImpactResponse.Knockout)
                {
                    duelController?.RequestKnockout(
                        fighterId,
                        new RagdollHandoff(point, velocityChange, true));
                }
                else if (response != EarthCharacterImpactResponse.Ignore &&
                         !targetBody.isKinematic)
                {
                    targetBody.AddForceAtPosition(velocityChange, point, ForceMode.VelocityChange);
                }

                ImpactResolved?.Invoke(response);
                return response;
            }
        }

        public EarthCharacterImpactResponse ReceiveCollision(Collision collision)
        {
            if (collision == null || collision.contactCount == 0)
                return EarthCharacterImpactResponse.Ignore;
            float impulse = collision.impulse.magnitude;
            if (impulse <= 0.01f) return EarthCharacterImpactResponse.Ignore;
            ContactPoint contact = collision.GetContact(0);
            Collider other = contact.otherCollider;
            if (other != null && other.transform.IsChildOf(transform))
                other = contact.thisCollider;
            ResolveSource(other, out EarthCharacterImpactSourceKind sourceKind, out uint sourceId);
            Rigidbody otherBody = other != null ? other.attachedRigidbody : collision.rigidbody;
            float closingSpeed = otherBody != null
                ? (otherBody.linearVelocity - targetBody.linearVelocity).magnitude
                : collision.relativeVelocity.magnitude;
            return ApplyImpact(
                contact.point,
                -contact.normal,
                impulse,
                sourceKind,
                sourceId,
                closingSpeed);
        }

        private void Awake()
        {
            if (targetBody == null) targetBody = GetComponent<Rigidbody>();
            if (_visibleRagdoll == null)
                _visibleRagdoll = GetComponentInChildren<HumanoidRagdollRig>(true);
            _tuning = responseProfile != null ? responseProfile.Tuning : EarthCharacterImpactTuning.Default;
        }

        private bool RegisterStoneCluster(
            Vector3 point,
            uint sourceStableId,
            float effectiveVelocityChange)
        {
            EarthLocalizedHitClusterResult result = EarthLocalizedHitClusterSolver.Step(
                in _stoneCluster,
                ToFloat3(point),
                Time.time,
                sourceStableId,
                effectiveVelocityChange);
            _stoneCluster = result.State;
            return result.FullRagdoll;
        }

        private static bool IsStoneImpact(EarthCharacterImpactSourceKind source) =>
            source is EarthCharacterImpactSourceKind.LooseStone or
                EarthCharacterImpactSourceKind.ArmorProjectile or
                EarthCharacterImpactSourceKind.BotProjectile or
                EarthCharacterImpactSourceKind.StonePunch;

        private void OnCollisionEnter(Collision collision) => ReceiveCollision(collision);

        private bool IsDuplicate(
            EarthCharacterImpactSourceKind sourceKind,
            uint sourceStableId,
            uint tick,
            float impactTime)
        {
            for (int index = 0; index < DedupeCapacity; index++)
                if (EarthCharacterLaunchBudgetSolver.IsCastScopedDuplicate(
                        sourceKind,
                        sourceStableId,
                        impactTime,
                        _recentSourceIds[index],
                        _recentTimes[index]) ||
                    EarthCharacterImpactSolver.IsDuplicate(
                        sourceStableId,
                        tick,
                        _recentSourceIds[index],
                        _recentTicks[index]))
                    return true;
            return false;
        }

        private void Remember(uint sourceStableId, uint tick, float impactTime)
        {
            if (sourceStableId == 0u) return;
            _recentSourceIds[_dedupeCursor] = sourceStableId;
            _recentTicks[_dedupeCursor] = tick;
            _recentTimes[_dedupeCursor] = impactTime;
            _dedupeCursor = (_dedupeCursor + 1) % DedupeCapacity;
        }

        private void ClearDedupe()
        {
            Array.Clear(_recentSourceIds, 0, _recentSourceIds.Length);
            Array.Clear(_recentTicks, 0, _recentTicks.Length);
            Array.Clear(_recentTimes, 0, _recentTimes.Length);
            _dedupeCursor = 0;
            _stoneCluster = default;
        }

        private static void ResolveSource(
            Collider collider,
            out EarthCharacterImpactSourceKind sourceKind,
            out uint sourceStableId)
        {
            sourceKind = EarthCharacterImpactSourceKind.Physics;
            sourceStableId = 0u;
            if (collider == null) return;

            EarthArmorPiece armor = collider.GetComponentInParent<EarthArmorPiece>();
            if (armor != null)
            {
                sourceKind = EarthCharacterImpactSourceKind.ArmorProjectile;
                sourceStableId = armor.ImpactSourceId;
                return;
            }
            EarthPillarWaveColumn wave = collider.GetComponentInParent<EarthPillarWaveColumn>();
            if (wave != null)
            {
                sourceKind = wave.ImpactKind;
                sourceStableId = wave.ImpactSourceId;
                return;
            }
            EarthFragment fragment = collider.GetComponentInParent<EarthFragment>();
            if (fragment != null)
            {
                sourceKind = EarthCharacterImpactSourceKind.LooseStone;
                sourceStableId = fragment.FragmentId;
                return;
            }
            EarthMatterIdentity matter = collider.GetComponentInParent<EarthMatterIdentity>();
            if (matter != null && matter.MatterId.IsValid)
                sourceStableId = matter.MatterId.StableId;
        }

        private static uint CurrentPhysicsTick
        {
            get
            {
                float step = Mathf.Max(0.0001f, Time.fixedDeltaTime);
                return unchecked((uint)Mathf.Max(1, Mathf.RoundToInt(Time.fixedTime / step)));
            }
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }
}
