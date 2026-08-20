using Elemental.Input.Gestures;
using Elemental.Runtime.World;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Magic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class EarthCoreHud : MonoBehaviour
    {
        [SerializeField] private MagicInputController input;
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private EarthPillarMobility pillarMobility;
        [SerializeField] private EarthLandingCushion landingCushion;

        private Label _ability;
        private Label _status;
        private Label _casts;
        private Label _mass;
        private Label _result;
        private Label _parameter;
        private VisualElement _amountFill;
        private VisualElement _chargeFill;
        private VisualElement _liftFill;
        private VisualElement _landingFill;
        private VisualElement _gravityFill;
        private VisualElement _reticle;
        private float _displayPower01;
        private bool _previewActive;
        private bool _subscribed;
        private EarthReticleState _lastReticleState = (EarthReticleState)255;

        public void Configure(
            MagicInputController configuredInput,
            MagicExecutor configuredExecutor,
            EarthPillarMobility configuredPillarMobility,
            EarthLandingCushion configuredLandingCushion = null)
        {
            if (_subscribed) Unsubscribe();
            input = configuredInput;
            executor = configuredExecutor;
            pillarMobility = configuredPillarMobility;
            landingCushion = configuredLandingCushion;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _ability = root.Q<Label>("ability-value");
            _status = root.Q<Label>("status-value");
            _casts = root.Q<Label>("casts-value");
            _mass = root.Q<Label>("mass-value");
            _result = root.Q<Label>("result-value");
            _parameter = root.Q<Label>("parameter-value");
            _amountFill = root.Q<VisualElement>("amount-fill");
            _chargeFill = root.Q<VisualElement>("charge-fill");
            _liftFill = root.Q<VisualElement>("lift-fill");
            _landingFill = root.Q<VisualElement>("landing-fill");
            _gravityFill = root.Q<VisualElement>("gravity-fill");
            _reticle = root.Q<VisualElement>("aim-reticle");
            if (!_subscribed) Subscribe();
        }

        private void OnDisable()
        {
            if (_subscribed) Unsubscribe();
        }

        private void Update()
        {
            if (input != null && _ability != null)
            {
                _ability.text = input.ActiveActionOwner == EarthActionOwner.Resonance || input.IsResonanceVolleyActive
                    ? input.IsResonanceVolleyActive
                        ? $"RESONANCE VOLLEY / {input.ResonanceStoneCount} STONES"
                        : $"RESONANCE / {input.ResonanceCharge01 * 100f:0}%"
                    : input.ActiveActionOwner == EarthActionOwner.Wave
                        ? "WEB WAVE / CHARGING"
                    : input.ActiveActionOwner == EarthActionOwner.Surf
                        ? $"EARTH PLOUGH / {input.SurfSpeed:0.0} M/S"
                    : input.IsArmorActive
                    ? input.ArmorPhase01 < 0.3f
                        ? $"EARTH ARMOR / {input.ArmorPhase01 * 100f:0}%"
                        : input.ArmorPhase01 < 0.78f
                            ? $"ARMOR DOME / {input.ArmorPhase01 * 100f:0}%"
                            : $"ARMOR ORBIT / {input.ArmorPhase01 * 100f:0}%"
                    : input.IsQuickStonePrimed
                    ? $"QUICK STONE / {input.QuickStonePrime01 * 100f:0}%"
                    : input.IsGravityWellActive
                    ? input.GravityGestureDirection == EarthCircularGestureDirection.Clockwise
                        ? $"REWEAVE CW / {input.GravityGesturePhase01 * 100f:0}%"
                        : input.GravityGestureDirection == EarthCircularGestureDirection.CounterClockwise
                            ? $"UNWEAVE CCW / {input.GravityGesturePhase01 * 100f:0}%"
                            : $"GRAVITY GRIP / {input.GravityWellStrength * 100f:0}%"
                    : input.IsVectorFieldActive
                    ? $"VECTOR FIELD / {executor.VectorFieldCharge * 100f:0}%"
                    : input.SelectedAbility == EarthAbilityIds.RaisePlatform
                        ? $"PLATFORM / {Mathf.Lerp(0.6f, 3f, input.PlatformPreviewHeight01):0.0} M"
                        : $"{input.CurrentBendPhase.ToString().ToUpperInvariant()} / " +
                          input.BendOriginMode.ToString().ToUpperInvariant();
            }
            if (executor != null && _casts != null) _casts.text = executor.SuccessfulCommandCount.ToString("00");
            if (input != null)
            {
                if (_amountFill != null) _amountFill.style.width = Length.Percent(input.BendAmount01 * 100f);
                _displayPower01 = Mathf.Max(input.BendCharge01, _displayPower01);
                if (_chargeFill != null) _chargeFill.style.width = Length.Percent(_displayPower01 * 100f);
                if (_reticle != null)
                {
                    Vector2 pointer = input.AimScreenPosition;
                    _reticle.style.left = pointer.x - 11f;
                    _reticle.style.top = Screen.height - pointer.y - 11f;
                    UpdateReticleState(input.ReticleState);
                }
                if (_parameter != null)
                    _parameter.text = $"{input.BendParameterLabel}  {input.BendParameter01 * 100f:0}%";
            }
            if (_liftFill != null)
                _liftFill.style.width = Length.Percent((pillarMobility != null ? pillarMobility.Charge01 : 0f) * 100f);
            if (_landingFill != null)
                _landingFill.style.width = Length.Percent(
                    landingCushion != null && landingCushion.IsHolding ? 100f : 0f);
            if (_gravityFill != null)
                _gravityFill.style.width = Length.Percent(
                    input != null
                        ? input.IsArmorActive
                            ? input.ArmorPhase01 * 100f
                            : input.IsQuickStonePrimed
                                ? input.QuickStonePrime01 * 100f
                                : (input.GravityGestureDirection != EarthCircularGestureDirection.None
                                    ? input.GravityGesturePhase01
                                    : input.GravityWellStrength) * 100f
                        : 0f);
            if (executor != null && _mass != null)
            {
                float mass = executor.HeldMass > 0.01f ? executor.HeldMass : executor.VectorFieldMass;
                if (mass > 0.01f) _mass.text = $"{mass:0} KG HELD";
                else if (input != null && input.IsResonanceVolleyActive)
                    _mass.text = $"{input.ResonanceStoneCount} RESONANT STONES";
                else if (_previewActive && input != null && input.SelectedAbility == EarthAbilityIds.PullRock &&
                         executor.TryGetPreviewMetrics(EarthAbilityIds.PullRock, out MagicPreviewMetrics metrics))
                    _mass.text = $"~{metrics.EstimatedMass:0} KG SELECTED";
                else _mass.text = "NO ROCK HELD";
            }
        }

        private void OnRejected(AbilityRejectedEvent value)
        {
            if (_status != null) _status.text = value.Reason;
        }

        private void OnInputStatus(string value)
        {
            if (_status != null) _status.text = value;
        }

        private void OnPreviewChanged(System.Collections.Generic.IReadOnlyList<Vector3> _) => _previewActive = true;
        private void OnPreviewCleared() => _previewActive = false;
        private void OnPowerChanged(float value) => _displayPower01 = Mathf.Clamp01(value);
        private void OnLaunched(FragmentLaunchedEvent value)
        {
            if (_result != null) _result.text = $"{value.VelocityChange:0.0} M/S RELEASE";
        }

        private void OnBodyGrabbed(EarthBodyGrabbedEvent value)
        {
            if (_status != null) _status.text = $"Existing stone under control ({value.Mass:0} kg).";
        }

        private void OnBodyReleased(EarthBodyReleasedEvent value)
        {
            if (_result != null) _result.text = $"{math.length(value.Velocity):0.0} M/S RELEASE";
        }

        private void OnImpact(ImpactEvent value)
        {
            if (_result != null) _result.text = $"{value.Impulse:0} N·S IMPACT";
        }

        private void OnWallCollapsed(WallCollapsedEvent _)
        {
            if (_status != null) _status.text = "The wall lost cohesion and returned to earth.";
        }

        private void OnMagicPushed(MagicPushEvent value)
        {
            if (_result != null)
                _result.text = value.Wall
                    ? $"{value.Charge * 100f:0}% WALL SHOVE"
                    : $"{value.VelocityChange:0.0} M/S PUSH";
            if (_status != null)
                _status.text = value.Wall ? "The wall was shoved off its resting line." : "Earth force released.";
        }

        private void OnPillarRaised(EarthPillarLaunchEvent value)
        {
            if (_result != null) _result.text = $"{value.VelocityChange:0.0} M/S EARTH LIFT";
            if (_status != null) _status.text = $"Earth pillar {value.Height:0.0} m — launch committed.";
        }

        private void UpdateReticleState(EarthReticleState state)
        {
            if (_reticle == null || state == _lastReticleState) return;
            _reticle.RemoveFromClassList("reticle--invalid");
            _reticle.RemoveFromClassList("reticle--source");
            _reticle.RemoveFromClassList("reticle--ambiguous");
            _reticle.RemoveFromClassList("reticle--valid");
            string className = state switch
            {
                EarthReticleState.Valid => "reticle--valid",
                EarthReticleState.Ambiguous => "reticle--ambiguous",
                EarthReticleState.Terrain or EarthReticleState.Rock or
                    EarthReticleState.Intact or EarthReticleState.Broken => "reticle--source",
                _ => "reticle--invalid"
            };
            _reticle.AddToClassList(className);
            _lastReticleState = state;
        }

        private void Subscribe()
        {
            if (executor != null)
            {
                executor.Events.AbilityRejected += OnRejected;
                executor.Events.FragmentLaunched += OnLaunched;
                executor.Events.EarthBodyGrabbed += OnBodyGrabbed;
                executor.Events.EarthBodyReleased += OnBodyReleased;
                executor.Events.ImpactOccurred += OnImpact;
                executor.Events.WallCollapsed += OnWallCollapsed;
                executor.Events.MagicPushed += OnMagicPushed;
            }
            if (input != null)
            {
                input.StatusChanged += OnInputStatus;
                input.PreviewChanged += OnPreviewChanged;
                input.PreviewCleared += OnPreviewCleared;
                input.PushChargeChanged += OnPowerChanged;
            }
            if (pillarMobility != null) pillarMobility.PillarRaised += OnPillarRaised;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (executor != null)
            {
                executor.Events.AbilityRejected -= OnRejected;
                executor.Events.FragmentLaunched -= OnLaunched;
                executor.Events.EarthBodyGrabbed -= OnBodyGrabbed;
                executor.Events.EarthBodyReleased -= OnBodyReleased;
                executor.Events.ImpactOccurred -= OnImpact;
                executor.Events.WallCollapsed -= OnWallCollapsed;
                executor.Events.MagicPushed -= OnMagicPushed;
            }
            if (input != null)
            {
                input.StatusChanged -= OnInputStatus;
                input.PreviewChanged -= OnPreviewChanged;
                input.PreviewCleared -= OnPreviewCleared;
                input.PushChargeChanged -= OnPowerChanged;
            }
            if (pillarMobility != null) pillarMobility.PillarRaised -= OnPillarRaised;
            _subscribed = false;
        }

    }
}
