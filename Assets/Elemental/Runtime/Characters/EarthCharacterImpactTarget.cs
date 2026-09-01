using System;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
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
        private EarthRecoverableKnockdownState _localKnockdown;
        private EarthWorldResponseFanoutAdapter _worldResponseFanout;

        public EarthDuelFighterId FighterId => fighterId;
        public uint StableFighterId => stableFighterId;
        public Rigidbody Body => targetBody;
        public ImpactResponseMode ResponseMode => responseProfile != null
            ? responseProfile.ResponseMode
            : ImpactResponseMode.Legacy;
        public EarthCharacterImpactResponse LastResponse { get; private set; }
        public float LastReactionVelocityChange { get; private set; }
        public float LastEffectiveVelocityChange { get; private set; }
        public int AcceptedImpactCount { get; private set; }
        public bool IsRecoverablyKnockedDown =>
            _localKnockdown.IsActive ||
            (duelController != null && duelController.IsRecoverablyKnockedDown(fighterId));

        public event Action<EarthCharacterImpactResponse> ImpactResolved;
        public event Action<EarthWorldResponseEvent> WorldResponseRequested;

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
            _visibleRagdoll?.ConfigureLocalizedReactionProfile(responseProfile);
            EnsureLocalWorldResponseFanout();
            ClearDedupe();
        }

        public void BindDuel(EarthMvpDuelController duel) => duelController = duel;

        public void BindWorldResponseFanout(EarthWorldResponseFanoutAdapter fanout) =>
            _worldResponseFanout = fanout;

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
            else if (outcome == CharacterOutcome.RecoverableKnockdown)
            {
                Vector3 up = transform.position.sqrMagnitude > 0.1f
                    ? transform.position.normalized
                    : transform.up;
                BeginRecoverableKnockdown(
                    new RagdollHandoff(
                        point,
                        Vector3.ClampMagnitude(up * (downwardImpactSpeed * 0.08f), 1.2f),
                        true));
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
                ImpactResponseMode responseMode = responseProfile != null
                    ? responseProfile.ResponseMode
                    : ImpactResponseMode.Legacy;
                EarthCharacterImpactResolution resolution;
                if (responseProfile != null && responseMode == ImpactResponseMode.Calibrated)
                {
                    EarthCharacterImpactCalibration calibration =
                        responseProfile.CalibrationFor(sourceKind);
                    resolution = EarthCharacterImpactSolver.Resolve(
                        in impact,
                        in _tuning,
                        responseMode,
                        in calibration);
                }
                else
                {
                    resolution = EarthCharacterImpactSolver.Resolve(
                        in impact,
                        in _tuning,
                        ImpactResponseMode.Legacy);
                }
                bool stoneImpact = IsStoneImpact(sourceKind);
                bool clusteredStoneRagdoll = false;
                if (stoneImpact)
                    clusteredStoneRagdoll = RegisterStoneCluster(
                        point,
                        sourceStableId,
                        resolution.ReactionVelocityChange);
                EarthCharacterImpactResponse response = resolution.Response;
                if (stoneImpact)
                {
                    var outcomeInput = new CharacterOutcomeInput(
                        sourceKind,
                        0f,
                        0f,
                        clusteredStoneRagdoll
                            ? _stoneCluster.CumulativeVelocityChange
                            : resolution.ReactionVelocityChange,
                        _stoneCluster.HitCount,
                        true);
                    CharacterOutcome outcome = CharacterOutcomeResolver.Resolve(in outcomeInput);
                    response = ToImpactResponse(outcome);
                }
                Remember(sourceStableId, tick, impactTime);
                AcceptedImpactCount++;
                LastResponse = response;
                LastReactionVelocityChange = resolution.ReactionVelocityChange;
                LastEffectiveVelocityChange = resolution.EffectiveVelocityChange;

                Vector3 requestedVelocityChange = safeDirection * resolution.AppliedVelocityChange;
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
                if (responseMode == ImpactResponseMode.Legacy &&
                    stoneImpact && !clusteredStoneRagdoll)
                    velocityChange = Vector3.ClampMagnitude(
                        velocityChange,
                        responseProfile != null ? responseProfile.SingleStoneRootVelocity : 0.8f);
                EarthWorldResponseEvent worldResponse = CreateWorldResponse(
                    in impact,
                    response,
                    safeDirection,
                    resolution.ReactionVelocityChange);
                WorldResponseRequested?.Invoke(worldResponse);
                _worldResponseFanout?.Publish(in worldResponse);

                if (response == EarthCharacterImpactResponse.Knockout)
                {
                    duelController?.RequestKnockout(
                        fighterId,
                        new RagdollHandoff(point, velocityChange, true));
                }
                else if (response == EarthCharacterImpactResponse.RecoverableKnockdown)
                {
                    BeginRecoverableKnockdown(
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
            ContactPoint contact = collision.GetContact(0);
            Collider other = contact.otherCollider;
            if (other != null && other.transform.IsChildOf(transform))
                other = contact.thisCollider;
            Rigidbody otherBody = other != null ? other.attachedRigidbody : collision.rigidbody;
            EarthFragment controlledFragment = otherBody != null
                ? otherBody.GetComponent<EarthFragment>()
                : null;
            // Controlled matter is still in the player's hand, not a projectile.
            // Ignoring contact here prevents the emergence/hover controller from
            // knocking out its own caster (or an opponent brushed during handling).
            if (controlledFragment != null && controlledFragment.IsHeld)
                return EarthCharacterImpactResponse.Ignore;
            float impulse = collision.impulse.magnitude;
            if (impulse <= 0.01f) return EarthCharacterImpactResponse.Ignore;
            ResolveSource(other, out EarthCharacterImpactSourceKind sourceKind, out uint sourceId);
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
            _visibleRagdoll?.ConfigureLocalizedReactionProfile(responseProfile);
            _tuning = responseProfile != null ? responseProfile.Tuning : EarthCharacterImpactTuning.Default;
            EnsureLocalWorldResponseFanout();
        }

        private void Update()
        {
            if (!_localKnockdown.IsActive) return;
            EarthRecoverableKnockdownStep step = EarthRecoverableKnockdownSolver.Step(
                in _localKnockdown,
                Time.deltaTime);
            _localKnockdown = step.State;
            if (step.BeginAuthoredRecovery)
            {
                Vector3 up = transform.position.sqrMagnitude > 0.1f
                    ? transform.position.normalized
                    : transform.up;
                _visibleRagdoll?.RecoverToAnimated(
                    up,
                    Vector3.ProjectOnPlane(transform.forward, up),
                    false);
            }
            if (step.Completed) _visibleRagdoll?.CompleteRecovery();
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
                EarthCharacterImpactSourceKind.QuickStone or
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

        private void BeginRecoverableKnockdown(in RagdollHandoff handoff)
        {
            if (duelController != null)
            {
                duelController.RequestRecoverableKnockdown(fighterId, in handoff);
                return;
            }
            if (_localKnockdown.IsActive) return;
            _localKnockdown = EarthRecoverableKnockdownState.Begin();
            _visibleRagdoll?.BeginRagdoll(in handoff);
        }

        private void EnsureLocalWorldResponseFanout()
        {
            if (_worldResponseFanout != null) return;
            MagicExecutor localExecutor = GetComponent<MagicExecutor>();
            if (localExecutor != null)
                _worldResponseFanout = new EarthWorldResponseFanoutAdapter(localExecutor.Events);
        }

        private EarthWorldResponseEvent CreateWorldResponse(
            in EarthCharacterImpact impact,
            EarthCharacterImpactResponse response,
            Vector3 safeDirection,
            float reactionVelocityChange)
        {
            EarthWorldResponseKind kind = response switch
            {
                EarthCharacterImpactResponse.Knockout => EarthWorldResponseKind.Knockout,
                EarthCharacterImpactResponse.RecoverableKnockdown => EarthWorldResponseKind.Knockdown,
                _ => EarthWorldResponseKind.CharacterImpact
            };
            float energy = 0.5f * Mathf.Max(0.01f, targetBody.mass) *
                           reactionVelocityChange * reactionVelocityChange;
            uint responseId = EarthWorldResponseId.Compose(
                stableFighterId,
                impact.SourceStableId,
                impact.Tick,
                response);
            return new EarthWorldResponseEvent(
                responseId,
                impact.Tick,
                impact.SourceStableId,
                stableFighterId,
                kind,
                impact.SourceKind,
                response,
                impact.Point,
                ToFloat3(-safeDirection),
                impact.Direction,
                impact.Impulse,
                energy,
                Mathf.InverseLerp(0.65f, _tuning.MaximumVelocityChange, reactionVelocityChange));
        }

        private static EarthCharacterImpactResponse ToImpactResponse(CharacterOutcome outcome) =>
            outcome switch
            {
                CharacterOutcome.Stumble => EarthCharacterImpactResponse.Flinch,
                CharacterOutcome.Stagger => EarthCharacterImpactResponse.Stagger,
                CharacterOutcome.RecoverableKnockdown =>
                    EarthCharacterImpactResponse.RecoverableKnockdown,
                CharacterOutcome.Knockout => EarthCharacterImpactResponse.Knockout,
                _ => EarthCharacterImpactResponse.Ignore
            };

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
