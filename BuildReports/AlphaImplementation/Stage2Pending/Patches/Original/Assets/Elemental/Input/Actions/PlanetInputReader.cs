using Elemental.Simulation.Characters;
using Elemental.Simulation.Bending;
using Elemental.Runtime.Characters;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Input.Actions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.InputSystem.PlayerInput))]
    public sealed class PlanetInputReader : MonoBehaviour, IPlanetMotorInputSource
    {
        [SerializeField] private EarthInputAdapter inputAdapter;
        [SerializeField] private EarthPillarMobility earthPillarMobility;
        [SerializeField] private EarthPillarWaveAbility earthPillarWave;
        [SerializeField] private EarthLandingCushion earthLandingCushion;
        [SerializeField] private EarthActionRouterBehaviour actionRouter;
        [SerializeField, Min(0.05f)] private float tapJumpThresholdSeconds = 0.18f;

        private bool _jumpQueued;
        private JumpCastMode _jumpCastMode;
        private float _jumpStartedAt;

        public bool UsesEarthPillarMobility => earthPillarMobility != null;

        public void ConfigureActionRouter(EarthActionRouterBehaviour configuredActionRouter) =>
            actionRouter = configuredActionRouter;

        public void Configure(
            EarthInputAdapter configuredInputAdapter,
            EarthPillarMobility configuredEarthPillarMobility = null,
            EarthPillarWaveAbility configuredEarthPillarWave = null,
            EarthLandingCushion configuredLandingCushion = null,
            EarthActionRouterBehaviour configuredActionRouter = null)
        {
            inputAdapter = configuredInputAdapter;
            earthPillarMobility = configuredEarthPillarMobility;
            earthPillarWave = configuredEarthPillarWave;
            earthLandingCushion = configuredLandingCushion;
            actionRouter = configuredActionRouter;
        }

        private void Awake()
        {
            if (inputAdapter == null) inputAdapter = GetComponent<EarthInputAdapter>();
            if (inputAdapter == null) inputAdapter = gameObject.AddComponent<EarthInputAdapter>();
            inputAdapter.Configure(GetComponent<UnityEngine.InputSystem.PlayerInput>());
            if (actionRouter == null) actionRouter = GetComponent<EarthActionRouterBehaviour>();
        }

        private void OnEnable()
        {
            // Enter Play Mode and round restarts may keep managed fields alive.
            // Never replay a queued jump or an interrupted hold into tick zero.
            RouteCancel();
            if (inputAdapter == null)
            {
                Debug.LogError("[Elemental] Earth input adapter is not configured.", this);
                enabled = false;
                return;
            }
        }

        private void OnDisable()
        {
            RouteCancel();
        }

        private void Update()
        {
            if (_jumpCastMode != JumpCastMode.PendingPillar || earthPillarMobility == null)
                return;
            if (Time.unscaledTime - _jumpStartedAt < tapJumpThresholdSeconds)
                return;
            if (earthPillarMobility.BeginCharge()) _jumpCastMode = JumpCastMode.Pillar;
        }

        public PlanetMotorCommand SampleCommand(uint tick)
        {
            Vector2 move = inputAdapter != null ? inputAdapter.Move : Vector2.zero;
            if (actionRouter != null && actionRouter.Consumes(EarthInputConsumption.Move))
                move = Vector2.zero;
            bool jump = _jumpQueued;
            _jumpQueued = false;
            return new PlanetMotorCommand(tick, new float2(move.x, move.y), jump);
        }

        public void RouteJumpPerformed()
        {
            if (earthPillarMobility != null || earthPillarWave != null) return;
            _jumpQueued = true;
        }

        public void RouteJumpStarted()
        {
            if (earthLandingCushion != null && earthLandingCushion.BeginHold())
            {
                _jumpCastMode = JumpCastMode.Cushion;
                return;
            }
            if (earthPillarMobility != null)
            {
                _jumpStartedAt = Time.unscaledTime;
                _jumpCastMode = JumpCastMode.PendingPillar;
                return;
            }
            _jumpQueued = true;
            _jumpCastMode = JumpCastMode.None;
        }

        public void RouteJumpCanceled()
        {
            if (_jumpCastMode == JumpCastMode.Pillar) earthPillarMobility?.ReleaseCharge();
            else if (_jumpCastMode == JumpCastMode.PendingPillar)
            {
                earthPillarMobility?.CancelCharge();
                _jumpQueued = true;
            }
            else if (_jumpCastMode == JumpCastMode.Cushion) earthLandingCushion?.EndHold();
            _jumpCastMode = JumpCastMode.None;
            _jumpStartedAt = 0f;
        }

        public void RouteCancel()
        {
            earthPillarMobility?.CancelCharge();
            earthPillarWave?.CancelCharge();
            earthLandingCushion?.EndHold();
            _jumpCastMode = JumpCastMode.None;
            _jumpStartedAt = 0f;
            _jumpQueued = false;
        }

        private enum JumpCastMode : byte
        {
            None,
            PendingPillar,
            Pillar,
            Cushion
        }
    }
}
