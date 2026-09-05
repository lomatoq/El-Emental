using System;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    /// <summary>
    /// Thin runtime adapter for the deterministic MVP linebreaker planner. The
    /// planner owns intent; this component only samples scene state, feeds the
    /// existing planet motor, and translates the single strike pulse to physics.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(PlanetMotor), typeof(EarthCombatDummy))]
    public sealed class EarthMvpBotController : MonoBehaviour, IPlanetMotorInputSource
    {
        private static readonly ProfilerMarker FixedTickMarker =
            new ProfilerMarker("Elemental.MvpBot.FixedTick");

        [Header("Explicit scene references")]
        [SerializeField] private Transform player;
        [SerializeField] private ActiveRagdollPuppet playerPuppet;
        [SerializeField] private Collider playerCollider;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private EarthCombatDummy combatBody;
        [SerializeField] private Vector3 arenaCenter;

        [Header("MVP linebreaker tuning")]
        [SerializeField, Min(1f)] private float arenaRadius = 6.5f;
        [SerializeField, Min(0.5f)] private float attackRange = 5.8f;
        [SerializeField, Range(5f, 180f)] private float attackConeDegrees = 58f;
        [SerializeField, Min(0.1f)] private float windupSeconds = 0.9f;
        [SerializeField, Min(0f)] private float recoverSeconds = 1.05f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 0.72f;
        [SerializeField, Min(0.1f)] private float chargeSpeed = 15f;
        [SerializeField, Min(0.05f)] private float chargeSeconds = 0.24f;

        [Header("Magic projectile")]
        [SerializeField] private EarthFragmentPool projectilePool;
        [SerializeField] private Collider casterCollider;
        [SerializeField] private EarthMvpDuelController duelController;
        [SerializeField, Min(0.1f)] private float projectileRadius = 0.42f;
        [SerializeField, Min(0.1f)] private float projectileMass = 18f;
        [SerializeField, Min(0.1f)] private float projectileLifetimeSeconds = 2.1f;
        [SerializeField, Min(0.1f)] private float knockoutVelocityChange = 11.8f;
        [SerializeField, Range(0f, 4f)] private float initialStrikeProtectionSeconds = 2.5f;

        private EarthMvpBotPlannerState _plannerState = EarthMvpBotPlannerState.Initial;
        private EarthMvpBotGuardReason _lastGuardReason;
        private float _chargeUntil;
        private uint _lastSampleTick;
        private bool _hasSampled;
        private float _strikeReadyAt;

        public EarthMvpBotPhase Phase => _plannerState.Phase;
        public EarthMvpBotGuardReason LastGuardReason => _lastGuardReason;
        public Vector3 ArenaCenter => arenaCenter;
        public float ArenaRadius => arenaRadius;
        public Vector3 LocalUp => ResolveLocalUp();
        public Vector3 LockedStrikeDirection => ToVector3(_plannerState.LockedStrikeDirection);
        public float Telegraph01 => Phase == EarthMvpBotPhase.Windup
            ? Mathf.Clamp01(_plannerState.PhaseSeconds / Mathf.Max(0.01f, windupSeconds))
            : 0f;
        public bool IsCharging => Time.fixedTime < _chargeUntil;
        public bool HasSampled => _hasSampled;
        public uint LastSampleTick => _lastSampleTick;
        public int StrikeCount { get; private set; }
        public int LandedStrikeCount { get; private set; }

        public event Action<EarthMvpBotPhase> PhaseChanged;
        public event Action StrikeStarted;
        public event Action<Vector3> StrikeLanded;

        public void Configure(
            Transform configuredPlayer,
            Rigidbody configuredPlayerBody,
            PhysicalImpactTarget configuredPlayerImpactTarget,
            ActiveRagdollPuppet configuredPlayerPuppet,
            Transform configuredPlanetCenter,
            Rigidbody configuredBody,
            PlanetMotor configuredMotor,
            EarthCombatDummy configuredCombatBody,
            Vector3 configuredArenaCenter,
            float configuredArenaRadius)
        {
            player = configuredPlayer;
            playerPuppet = configuredPlayerPuppet;
            playerCollider = configuredPlayer != null
                ? configuredPlayer.GetComponent<Collider>()
                : null;
            planetCenter = configuredPlanetCenter;
            targetBody = configuredBody;
            motor = configuredMotor;
            combatBody = configuredCombatBody;
            arenaCenter = configuredArenaCenter;
            // Broken Crown preserves valid authored tangent placement when it seats
            // the fighters. That can put the player farther from the floor centre
            // than the old 6.5 m prototype radius. Treat the configured value as a
            // minimum and always include both current spawn points; otherwise the
            // planner reports TargetOutsideArena forever and the rival never walks.
            float selfDistance = Vector3.Distance(transform.position, arenaCenter);
            float playerDistance = configuredPlayer != null
                ? Vector3.Distance(configuredPlayer.position, arenaCenter)
                : 0f;
            float requiredRadius = Mathf.Max(selfDistance, playerDistance) + 1f;
            arenaRadius = Mathf.Max(attackRange + 0.25f, configuredArenaRadius, requiredRadius);
            ConfigureFighterCollisionFiltering();
            ResetPlanner();
        }

        public void ConfigureTuning(
            float configuredAttackRange,
            float configuredWindupSeconds,
            float configuredChargeSpeed,
            float configuredChargeSeconds,
            float configuredRecoverSeconds,
            float configuredCooldownSeconds)
        {
            attackRange = Mathf.Max(0.5f, configuredAttackRange);
            windupSeconds = Mathf.Max(0.1f, configuredWindupSeconds);
            chargeSpeed = Mathf.Max(0.1f, configuredChargeSpeed);
            chargeSeconds = Mathf.Max(0.05f, configuredChargeSeconds);
            recoverSeconds = Mathf.Max(0f, configuredRecoverSeconds);
            cooldownSeconds = Mathf.Max(0f, configuredCooldownSeconds);
            arenaRadius = Mathf.Max(arenaRadius, attackRange + 0.25f);
        }

        public void ConfigureMagic(
            EarthFragmentPool configuredProjectilePool,
            Collider configuredCasterCollider,
            EarthMvpDuelController configuredDuelController,
            float configuredProjectileRadius = 0.42f,
            float configuredProjectileMass = 18f,
            float configuredProjectileLifetimeSeconds = 2.1f,
            float configuredKnockoutVelocityChange = 11.8f)
        {
            projectilePool = configuredProjectilePool;
            casterCollider = configuredCasterCollider;
            duelController = configuredDuelController;
            ConfigureFighterCollisionFiltering();
            projectileRadius = Mathf.Max(0.1f, configuredProjectileRadius);
            projectileMass = Mathf.Max(0.1f, configuredProjectileMass);
            projectileLifetimeSeconds = Mathf.Max(0.25f, configuredProjectileLifetimeSeconds);
            knockoutVelocityChange = Mathf.Max(0.1f, configuredKnockoutVelocityChange);
        }

        public PlanetMotorCommand SampleCommand(uint tick)
        {
            using var marker = FixedTickMarker.Auto();
            ResolveLocalReferences();
            _lastSampleTick = tick;
            _hasSampled = true;

            Vector3 up = ResolveLocalUp();
            bool targetAvailable = player != null && PlayerCanBeAttacked();
            EarthMvpBotTuning tuning = new EarthMvpBotTuning(
                attackRange,
                attackConeDegrees,
                arenaRadius,
                windupSeconds,
                recoverSeconds,
                cooldownSeconds);
            EarthMvpBotFrame frame = new EarthMvpBotFrame(
                Time.fixedDeltaTime,
                ToFloat3(targetBody != null ? targetBody.worldCenterOfMass : transform.position),
                ToFloat3(transform.forward),
                ToFloat3(player != null ? player.position : transform.position),
                ToFloat3(up),
                ToFloat3(arenaCenter),
                isActiveAndEnabled,
                targetAvailable,
                ResolveBodyState());
            EarthMvpBotPlan plan = EarthMvpBotPlanner.Step(in _plannerState, in frame, in tuning);
            EarthMvpBotPhase previous = _plannerState.Phase;
            _plannerState = plan.State;
            _lastGuardReason = plan.GuardReason;
            if (previous != _plannerState.Phase)
                SetPhasePresentation(previous, _plannerState.Phase);

            Vector3 facing = ToVector3(plan.DesiredFacingDirection);
            if (facing.sqrMagnitude > 0.01f) motor?.SetAimDirection(facing);
            // The duel grants the player matching startup impact protection, but a
            // physical projectile can still transfer momentum before its typed
            // impact is accepted. Keep the first projectile out of the scene until
            // that grace window has elapsed; approach locomotion remains active.
            if (plan.StrikeThisTick && Time.time >= _strikeReadyAt) BeginStrike(up);

            Vector3 move = ToVector3(plan.DesiredMoveDirection);
            if (move.sqrMagnitude < 0.0001f || motor == null)
                return new PlanetMotorCommand(tick, float2.zero, false);
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            float2 command = new float2(Vector3.Dot(move, right), Vector3.Dot(move, forward));
            return new PlanetMotorCommand(tick, command, false);
        }

        public void ResetPlanner()
        {
            EarthMvpBotPhase previous = _plannerState.Phase;
            _plannerState = EarthMvpBotPlannerState.Initial;
            _lastGuardReason = EarthMvpBotGuardReason.None;
            _chargeUntil = -1f;
            StrikeCount = 0;
            LandedStrikeCount = 0;
            if (previous != _plannerState.Phase) PhaseChanged?.Invoke(_plannerState.Phase);
            combatBody?.SetBraced(false);
        }

        private void Awake()
        {
            ResolveLocalReferences();
            ConfigureFighterCollisionFiltering();
            _strikeReadyAt = Time.time + Mathf.Clamp(initialStrikeProtectionSeconds, 0f, 4f);
        }

        private void OnEnable()
        {
            ConfigureFighterCollisionFiltering();
            _strikeReadyAt = Mathf.Max(
                _strikeReadyAt,
                Time.time + Mathf.Clamp(initialStrikeProtectionSeconds, 0f, 4f));
        }

        private void FixedUpdate()
        {
            if (targetBody == null || Time.fixedTime < _chargeUntil) return;
            if (Phase is not (EarthMvpBotPhase.Recover or EarthMvpBotPhase.Cooldown)) return;
            Vector3 up = ResolveLocalUp();
            Vector3 radial = Vector3.Project(targetBody.linearVelocity, up);
            Vector3 tangent = Vector3.ProjectOnPlane(targetBody.linearVelocity, up);
            float retention = Mathf.Exp(-7.5f * Time.fixedDeltaTime);
            targetBody.linearVelocity = radial + tangent * retention;
        }

        private void OnDisable()
        {
            _chargeUntil = -1f;
            combatBody?.SetBraced(false);
        }

        private void BeginStrike(Vector3 up)
        {
            Vector3 direction = LockedStrikeDirection;
            direction = Vector3.ProjectOnPlane(direction, up).normalized;
            if (direction.sqrMagnitude < 0.1f) return;

            _chargeUntil = Time.fixedTime + chargeSeconds;
            StrikeCount++;
            StrikeStarted?.Invoke();

            if (projectilePool == null) return;
            Vector3 origin = transform.position + up * 0.92f + direction * 0.72f;
            EarthFragment projectile = projectilePool.Acquire(
                null,
                origin,
                projectileRadius,
                projectileMass);
            if (projectile == null) return;
            projectile.SetTargetKind(EarthPhysicalTargetKind.ResonanceProjectile);
            EarthMvpMagicProjectile magicProjectile =
                projectile.GetComponent<EarthMvpMagicProjectile>();
            if (magicProjectile == null)
                magicProjectile = projectile.gameObject.AddComponent<EarthMvpMagicProjectile>();
            magicProjectile.Configure(
                projectile,
                playerPuppet,
                duelController,
                this,
                direction,
                knockoutVelocityChange,
                projectileLifetimeSeconds);
            projectile.LaunchProjectile(direction, chargeSpeed, casterCollider, 0.32f);
        }

        internal void NotifyProjectileLanded(Vector3 point)
        {
            LandedStrikeCount++;
            StrikeLanded?.Invoke(point);
        }

        private void SetPhasePresentation(EarthMvpBotPhase previous, EarthMvpBotPhase next)
        {
            bool braced = next == EarthMvpBotPhase.Windup;
            if (previous == EarthMvpBotPhase.Windup || braced) combatBody?.SetBraced(braced);
            if (next == EarthMvpBotPhase.Disabled)
            {
                _chargeUntil = -1f;
            }
            PhaseChanged?.Invoke(next);
        }

        private bool PlayerCanBeAttacked()
        {
            if (player == null || !player.gameObject.activeInHierarchy) return false;
            if (playerPuppet == null) return true;
            CharacterPhysicalMode mode = playerPuppet.CurrentState.Mode;
            return mode is CharacterPhysicalMode.AnimatedMotor or CharacterPhysicalMode.PhysicalAssist;
        }

        private EarthMvpBotBodyState ResolveBodyState()
        {
            if (combatBody == null || !combatBody.isActiveAndEnabled)
                return EarthMvpBotBodyState.Disabled;
            return combatBody.State switch
            {
                EarthCombatDummyState.Grounded => EarthMvpBotBodyState.Ready,
                EarthCombatDummyState.Braced => EarthMvpBotBodyState.Ready,
                EarthCombatDummyState.Staggered => EarthMvpBotBodyState.Staggered,
                EarthCombatDummyState.FullRagdoll => EarthMvpBotBodyState.Ragdolled,
                EarthCombatDummyState.Recovering => EarthMvpBotBodyState.Recovering,
                _ => EarthMvpBotBodyState.Disabled
            };
        }

        private Vector3 ResolveLocalUp()
        {
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            Vector3 up = transform.position - center;
            return up.sqrMagnitude > 0.01f ? up.normalized : transform.up;
        }

        private void ResolveLocalReferences()
        {
            if (targetBody == null) targetBody = GetComponent<Rigidbody>();
            if (motor == null) motor = GetComponent<PlanetMotor>();
            if (combatBody == null) combatBody = GetComponent<EarthCombatDummy>();
            if (playerCollider == null && player != null)
                playerCollider = player.GetComponent<Collider>();
        }

        private void ConfigureFighterCollisionFiltering()
        {
            if (casterCollider == null) casterCollider = GetComponent<Collider>();
            if (playerCollider == null && player != null)
                playerCollider = player.GetComponent<Collider>();
            if (casterCollider != null && playerCollider != null)
                UnityEngine.Physics.IgnoreCollision(casterCollider, playerCollider, true);
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }
}
