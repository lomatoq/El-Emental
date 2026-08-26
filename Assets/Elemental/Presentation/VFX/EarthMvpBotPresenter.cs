using Elemental.Runtime.Characters;
using Elemental.Simulation.Combat;
using Elemental.Presentation.Animation;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    /// <summary>
    /// Presentation-only telegraph for the MVP linebreaker. It reads the runtime
    /// adapter and never feeds timing, targeting, or hit decisions back to gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthMvpBotPresenter : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int TurnHash = Animator.StringToHash("Turn");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int CastHash = Animator.StringToHash("Cast");
        private static readonly int EarthPoseHash = Animator.StringToHash("EarthPose");
        private static readonly int MotionTimeHash = Animator.StringToHash("EarthMotionTime");
        private static readonly int MagicAttackWeightHash = Animator.StringToHash("EarthPose10");
        private const string MagicLayerName = "Earth Magic Upper Body";

        [SerializeField] private EarthMvpBotController controller;
        [SerializeField] private LineRenderer strikeLine;
        [SerializeField] private Renderer[] stoneRenderers;
        [SerializeField] private Animator humanoidAnimator;
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private Rigidbody rootBody;
        [SerializeField] private HumanoidCharacterPresentation sharedPresentation;
        [SerializeField] private Color enemyTint = new Color(0.08f, 0.42f, 0.94f, 1f);
        [SerializeField] private Color windupEdge = new Color(0.12f, 0.78f, 1f, 1f);
        [SerializeField] private Color strikeEdge = new Color(0.72f, 0.94f, 1f, 1f);
        [SerializeField] private Color recoveryEdge = new Color(0.22f, 0.36f, 0.50f, 1f);

        private MaterialPropertyBlock _properties;
        private Color[] _restingEdges;
        private Color[] _restingBaseColors;
        private int _magicLayerIndex = -1;
        private float _locomotionSpeed;
        private float _turn;
        private float _attackLayerWeight;

        public void Configure(
            EarthMvpBotController configuredController,
            LineRenderer configuredStrikeLine,
            Renderer[] configuredStoneRenderers,
            Animator configuredHumanoidAnimator = null,
            PlanetMotor configuredMotor = null,
            Rigidbody configuredRootBody = null,
            HumanoidCharacterPresentation configuredSharedPresentation = null)
        {
            Unsubscribe();
            controller = configuredController;
            strikeLine = configuredStrikeLine;
            stoneRenderers = configuredStoneRenderers;
            humanoidAnimator = configuredHumanoidAnimator;
            motor = configuredMotor;
            rootBody = configuredRootBody;
            sharedPresentation = configuredSharedPresentation;
            CacheRestingEdges();
            PrepareHumanoidAnimator();
            Subscribe();
            ApplyPhase(controller != null ? controller.Phase : EarthMvpBotPhase.Disabled);
        }

        private void Awake()
        {
            _properties ??= new MaterialPropertyBlock();
            if (humanoidAnimator == null) humanoidAnimator = GetComponentInChildren<Animator>(true);
            if (motor == null) motor = GetComponent<PlanetMotor>();
            if (rootBody == null) rootBody = GetComponent<Rigidbody>();
            CacheRestingEdges();
            PrepareHumanoidAnimator();
        }

        private void OnEnable()
        {
            PrepareHumanoidAnimator();
            Subscribe();
            ApplyPhase(controller != null ? controller.Phase : EarthMvpBotPhase.Disabled);
        }

        private void Start()
        {
            // Scene startup systems can initialize renderer property blocks after
            // OnEnable. Reassert the opponent identity once after every component
            // has completed Awake/OnEnable so the X Bot never settles back to red.
            ApplyPhase(controller != null ? controller.Phase : EarthMvpBotPhase.Disabled);
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (strikeLine != null) strikeLine.positionCount = 0;
            if (Application.isPlaying && humanoidAnimator != null && humanoidAnimator.isActiveAndEnabled)
            {
                humanoidAnimator.SetBool(CastHash, false);
                if (_magicLayerIndex >= 0) humanoidAnimator.SetLayerWeight(_magicLayerIndex, 0f);
            }
        }

        private void LateUpdate()
        {
            UpdateHumanoidAnimator();
            if (controller == null || strikeLine == null) return;
            bool visible = controller.Phase == EarthMvpBotPhase.Windup || controller.IsCharging;
            if (!visible)
            {
                strikeLine.positionCount = 0;
                return;
            }

            Vector3 direction = controller.LockedStrikeDirection;
            if (direction.sqrMagnitude < 0.1f)
            {
                strikeLine.positionCount = 0;
                return;
            }

            Vector3 up = controller.LocalUp;
            Vector3 start = transform.position + up * 0.92f;
            float distance = controller.IsCharging
                ? 5.8f
                : Mathf.Lerp(1.2f, 5.8f, controller.Telegraph01);
            strikeLine.positionCount = 2;
            strikeLine.SetPosition(0, start);
            strikeLine.SetPosition(1, start + direction.normalized * distance);
            float pulse = 0.78f + Mathf.Sin(Time.time * 18f) * 0.22f;
            strikeLine.widthMultiplier = controller.IsCharging ? 0.24f : Mathf.Lerp(0.08f, 0.18f, pulse);
        }

        private void PrepareHumanoidAnimator()
        {
            if (humanoidAnimator == null) return;
            humanoidAnimator.applyRootMotion = false;
            if (!Application.isPlaying || !humanoidAnimator.isActiveAndEnabled) return;
            _locomotionSpeed = 0f;
            _turn = 0f;
            _attackLayerWeight = 0f;
            _magicLayerIndex = humanoidAnimator.GetLayerIndex(MagicLayerName);
            if (_magicLayerIndex >= 0) humanoidAnimator.SetLayerWeight(_magicLayerIndex, 0f);
            humanoidAnimator.SetBool(GroundedHash, motor != null && motor.HasStableSupport);
            humanoidAnimator.SetBool(CastHash, false);
            humanoidAnimator.SetFloat(EarthPoseHash, 10f);
            humanoidAnimator.SetFloat(MagicAttackWeightHash, 1f);
            humanoidAnimator.SetFloat(MotionTimeHash, 0f);
        }

        private void UpdateHumanoidAnimator()
        {
            if (humanoidAnimator == null || controller == null || !humanoidAnimator.isActiveAndEnabled) return;

            if (sharedPresentation == null)
            {
                Vector3 up = controller.LocalUp;
                Vector3 tangentVelocity = rootBody != null
                    ? Vector3.ProjectOnPlane(rootBody.linearVelocity, up)
                    : Vector3.zero;
                float signedSpeed = tangentVelocity.magnitude;
                if (signedSpeed > 0.001f && Vector3.Dot(tangentVelocity, transform.forward) < 0f)
                    signedSpeed = -signedSpeed;
                float targetSpeed = Mathf.Clamp(signedSpeed / 3.4f, -1f, 1f);
                float targetTurn = motor != null ? Mathf.Clamp(motor.LastCommand.Move.x, -1f, 1f) : 0f;
                _locomotionSpeed = Mathf.MoveTowards(_locomotionSpeed, targetSpeed, Time.deltaTime * 6.5f);
                _turn = Mathf.MoveTowards(_turn, targetTurn, Time.deltaTime * 8f);

                humanoidAnimator.SetFloat(SpeedHash, _locomotionSpeed);
                humanoidAnimator.SetFloat(TurnHash, _turn);
                humanoidAnimator.SetBool(GroundedHash, motor != null && motor.HasStableSupport);
                humanoidAnimator.SetFloat(
                    VerticalSpeedHash,
                    rootBody != null ? Vector3.Dot(rootBody.linearVelocity, up) : 0f);
            }

            bool attacking = controller.Phase is EarthMvpBotPhase.Windup or EarthMvpBotPhase.Strike ||
                             controller.IsCharging;
            float targetAttackWeight = attacking ? 1f : 0f;
            _attackLayerWeight = Mathf.MoveTowards(
                _attackLayerWeight,
                targetAttackWeight,
                Time.deltaTime * (attacking ? 9f : 6f));
            if (_magicLayerIndex >= 0)
                humanoidAnimator.SetLayerWeight(_magicLayerIndex, _attackLayerWeight);
            humanoidAnimator.SetBool(CastHash, attacking);
            humanoidAnimator.SetFloat(EarthPoseHash, 10f);
            humanoidAnimator.SetFloat(MagicAttackWeightHash, 1f);
            float motionTime = controller.Phase switch
            {
                EarthMvpBotPhase.Windup => Mathf.Lerp(0.08f, 0.42f, controller.Telegraph01),
                EarthMvpBotPhase.Strike => 0.52f,
                EarthMvpBotPhase.Recover => 0.88f,
                _ => 0f
            };
            if (controller.IsCharging) motionTime = 0.56f;
            humanoidAnimator.SetFloat(MotionTimeHash, motionTime);
        }

        private void Subscribe()
        {
            if (controller == null) return;
            controller.PhaseChanged -= ApplyPhase;
            controller.PhaseChanged += ApplyPhase;
        }

        private void Unsubscribe()
        {
            if (controller != null) controller.PhaseChanged -= ApplyPhase;
        }

        private void CacheRestingEdges()
        {
            _properties ??= new MaterialPropertyBlock();
            if (stoneRenderers == null)
            {
                _restingEdges = null;
                _restingBaseColors = null;
                return;
            }

            _restingEdges = new Color[stoneRenderers.Length];
            _restingBaseColors = new Color[stoneRenderers.Length];
            for (int index = 0; index < stoneRenderers.Length; index++)
            {
                Material material = stoneRenderers[index] != null
                    ? stoneRenderers[index].sharedMaterial
                    : null;
                _restingEdges[index] = material != null && material.HasProperty("_EdgeColor")
                    ? material.GetColor("_EdgeColor")
                    : Color.white;
                _restingBaseColors[index] = material != null && material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material != null && material.HasProperty("_Color")
                        ? material.GetColor("_Color")
                        : Color.white;
            }
        }

        private void ApplyPhase(EarthMvpBotPhase phase)
        {
            Color lineColor = phase switch
            {
                EarthMvpBotPhase.Windup => windupEdge,
                EarthMvpBotPhase.Strike => strikeEdge,
                EarthMvpBotPhase.Recover => recoveryEdge,
                _ => windupEdge
            };
            if (strikeLine != null)
            {
                strikeLine.startColor = lineColor;
                strikeLine.endColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.08f);
                if (phase is not (EarthMvpBotPhase.Windup or EarthMvpBotPhase.Strike))
                    strikeLine.positionCount = 0;
            }

            if (stoneRenderers == null) return;
            _properties ??= new MaterialPropertyBlock();
            for (int index = 0; index < stoneRenderers.Length; index++)
            {
                Renderer renderer = stoneRenderers[index];
                if (renderer == null) continue;
                Color resting = _restingEdges != null && index < _restingEdges.Length
                    ? _restingEdges[index]
                    : Color.white;
                Color restingBase = _restingBaseColors != null && index < _restingBaseColors.Length
                    ? _restingBaseColors[index]
                    : Color.white;
                Color edge = phase switch
                {
                    EarthMvpBotPhase.Windup => Color.Lerp(resting, windupEdge, 0.82f),
                    EarthMvpBotPhase.Strike => strikeEdge,
                    EarthMvpBotPhase.Recover => Color.Lerp(resting, recoveryEdge, 0.55f),
                    _ => resting
                };
                renderer.GetPropertyBlock(_properties);
                _properties.SetColor("_EdgeColor", edge);
                Color baseTint = Color.Lerp(restingBase, enemyTint, 0.88f);
                Material material = renderer.sharedMaterial;
                if (material != null && material.HasProperty("_BaseColor"))
                    _properties.SetColor("_BaseColor", baseTint);
                if (material != null && material.HasProperty("_Color"))
                    _properties.SetColor("_Color", baseTint);
                renderer.SetPropertyBlock(_properties);
            }
        }
    }
}
