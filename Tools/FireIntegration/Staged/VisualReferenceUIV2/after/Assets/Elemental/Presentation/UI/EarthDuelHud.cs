using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Magic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class EarthDuelHud : MonoBehaviour
    {
        [SerializeField] private EarthMvpDuelController duel;
        [SerializeField] private EarthSceneReadinessGate readiness;
        [SerializeField] private MagicInputController magic;
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private EarthDualMouseAbilityController dualMouse;
        [SerializeField] private EarthPillarMobility pillar;
        [SerializeField] private EarthPillarWaveAbility wave;
        [SerializeField] private Transform player;
        [SerializeField] private Transform planet;
        [SerializeField] private Transform arena;
        [SerializeField] private bool showDiagnostics;
        [SerializeField] private bool playerIsBlue;
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("Elemental.DuelHud.Tick");
        private EarthDisplayMana _mana;
        private VisualElement _root, _top, _left, _right, _navigation, _result, _legacy, _clock;
        private EarthDuelGauge _healthGauge, _manaGauge;
        private EarthHologramGlobe _globe;
        private Label _redScore, _blueScore, _timer, _healthText, _manaText, _resultText, _respawn;
        private Button _restart, _pause;
        private System.Action _pauseRequested, _endMatchRequested;
        private bool _reducedMotion;
        private float _trailFrom,_trailTarget=-1,_trailAge,_manaFrom,_manaTarget=-1,_manaAge;
        private float _fillFrom,_fillTarget=-1,_fillAge;
        private StoneReferenceHudPresentation _referenceHud;
        private ElementalStoneReferenceProfile Reference => _theme?.stoneSkin?.referenceProfile;
        private bool ReferenceActive => Reference != null && Reference.enabled;
        public ElementId SelectedElement => magic != null ? magic.SelectedElement : ElementId.Earth;
        public void SetReducedMotion(bool reduced) => _reducedMotion=reduced;
        public void ConfigurePause(System.Action pause, System.Action endMatch = null) { _pauseRequested=pause; _endMatchRequested=endMatch; }
        private float _entrance;
        private bool _ready, _subscribed, _roundOver;
        private int _lastSeconds = -1, _lastHealth = -1, _lastMana = -1, _lastRed = -1, _lastBlue = -1, _lastRespawn = -1;
        private Vector3 _arenaRadial;
        private EarthCoreHud _legacyHud;
        private bool _combatVisible = true;
        private EarthDuelFighterId _localFighter = EarthDuelFighterId.Player;
        private bool _restartAllowed = true;
        public void SetLocalPerspective(EarthDuelFighterId fighter, bool canRestart)
        {
            _localFighter = fighter; _restartAllowed = canRestart;
            _lastHealth = _lastRespawn = -1;
            ResetLifeResult();
            if (_restart != null) { _restart.SetEnabled(canRestart); _restart.text = canRestart ? "NEW ROUND" : "WAITING FOR HOST"; }
        }
        private int LocalDeaths => duel == null ? 0 : _localFighter == EarthDuelFighterId.Player ? duel.BotScore : duel.PlayerScore;
        private int OpponentDeaths => duel == null ? 0 : _localFighter == EarthDuelFighterId.Player ? duel.PlayerScore : duel.BotScore;
        private float LocalHealth => _localFighter == EarthDuelFighterId.Player ? duel.PlayerHealth : duel.BotHealth;
        private float LocalRespawn => _localFighter == EarthDuelFighterId.Player ? duel.PlayerRespawnRemaining : duel.BotRespawnRemaining;
        private ElementalUITheme _theme;
        private ElementalStoneSkin.HudBinding _stoneSkinBinding;
        private EarthLifeResultState _lifeResultState;
        private Label _lifeResultLabel;
        private EarthLifeResult _lastLifeResult;
        private VisualElement _healthUnderBar, _energyUnderBar, _healthIcon, _energyIcon, _pauseIcon, _orbitCaption, _orbitLegend;
        private ElementalHudLayout _appliedLayout;
        private int _layoutRevision = -1;
        private static readonly ProfilerMarker LayoutMarker = new ProfilerMarker("Elemental.DuelHud.ApplyLayout");
        public void SetFrontendPresentation(ElementalUITheme theme, bool combatVisible)
        {
            if (_root != null && _referenceHud != null && !(theme?.stoneSkin?.referenceProfile != null && theme.stoneSkin.referenceProfile.enabled))
            { _theme=theme; _combatVisible=combatVisible; Build(); return; }
            _theme = theme;
            _combatVisible = combatVisible; _entrance = 0;
            if (_root == null) return;
            _root.style.display = combatVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_legacy != null) _legacy.style.display = combatVisible && Debug.isDebugBuild && showDiagnostics ? DisplayStyle.Flex : DisplayStyle.None;
            var reticle = GetComponent<UIDocument>().rootVisualElement.Q("aim-reticle");
            if (reticle != null) reticle.style.display = combatVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (theme != null)
            {
                _root.style.color = theme.text;
                if (_healthGauge != null) _healthGauge.Accent = theme.damage;
                if (_manaGauge != null) _manaGauge.Accent = theme.energy;
                if (theme.hudFont != null) _root.style.unityFont = theme.hudFont;
                ApplyThemeTextSizes();
                ApplyLayoutIfChanged(true);
                if (_stoneSkinBinding == null || !_stoneSkinBinding.Matches(_root))
                    _stoneSkinBinding = new ElementalStoneSkin.HudBinding(_root);
                _stoneSkinBinding.Apply(theme.stoneSkin);
                if (ReferenceActive)
                {
                    if (_referenceHud == null) _referenceHud = new StoneReferenceHudPresentation(_root,theme,_result,_resultText,_restart,()=>_endMatchRequested?.Invoke());
                    _referenceHud.Refresh();
                }
            }
        }
        private void ApplyLayoutIfChanged(bool force = false)
        {
            var layout = ReferenceActive && Reference.hudLayout != null ? Reference.hudLayout : _theme != null ? _theme.hudLayout : null;
            if (layout == null || _root == null || (!force && layout == _appliedLayout && layout.Revision == _layoutRevision)) return;
            using (LayoutMarker.Auto())
            {
                ApplyVital(layout.health, _left, _healthGauge, _healthUnderBar, _healthIcon, _healthText);
                ApplyVital(layout.energy, _right, _manaGauge, _energyUnderBar, _energyIcon, _manaText);
                HudLayoutAdapter.Apply(_navigation, layout.navigation.group);
                HudLayoutAdapter.Apply(_orbitCaption, layout.navigation.caption);
                HudLayoutAdapter.Apply(_globe, layout.navigation.globe);
                HudLayoutAdapter.Apply(_orbitLegend, layout.navigation.legend);
                HudLayoutAdapter.Apply(_pause, layout.pause.button);
                HudLayoutAdapter.Apply(_pauseIcon, layout.pause.icon);
                HudLayoutAdapter.Apply(_top, layout.scoreboard);
                HudLayoutAdapter.Apply(_lifeResultLabel, layout.lifeResult);
                _appliedLayout = layout; _layoutRevision = layout.Revision;
            }
        }
        private static void ApplyVital(HudVitalLayout data, VisualElement group, VisualElement bar,
            VisualElement underBar, VisualElement icon, VisualElement value)
        {
            HudLayoutAdapter.Apply(group, data.group); HudLayoutAdapter.Apply(bar, data.bar);
            HudLayoutAdapter.Apply(underBar, data.underBar); HudLayoutAdapter.Apply(icon, data.icon);
            HudLayoutAdapter.Apply(value, data.value);
        }
        private static void AnimateLayoutEntrance(VisualElement element, HudElementLayout data, float x, float y)
        { element.style.marginLeft = data.position.x + x; element.style.marginTop = data.position.y + y; }
        private void ApplyThemeTextSizes()
        {
            if (_root == null || _theme == null) return;
            _root.Query<TextElement>().ForEach(element =>
            {
                // Hidden Toolkit trees have unresolved/default computed sizes.
                // Read the saved theme roles, never freeze those defaults at startup.
                float size = element.ClassListContains("duel-life-result") ? _theme.lifeResultFontSize :
                    element.ClassListContains("duel-score") ? _theme.hudScoreSize :
                    element.ClassListContains("duel-time") ? _theme.hudTimerSize :
                    element.ClassListContains("duel-caption") ? _theme.hudCaptionSize :
                    element.ClassListContains("duel-symbol") ? _theme.hudSymbolSize :
                    element.ClassListContains("duel-vital-value") ? _theme.hudValueSize :
                    element.ClassListContains("duel-legend") ? _theme.hudLegendSize :
                    element.ClassListContains("duel-respawn") ? _theme.hudRespawnSize :
                    element.ClassListContains("duel-result-title") ? _theme.hudResultSize : _theme.hudButtonSize;
                element.style.fontSize = size * _theme.hudFontScale;
                if (_theme.hudFont != null)
                {
                    element.style.unityFont = _theme.hudFont;
                    element.style.unityFontDefinition = FontDefinition.FromFont(_theme.hudFont);
                }
            });
        }
        public float DisplayMana => _mana.Value;
        public EarthHologramGlobe Globe => _globe;
        public void Configure(EarthMvpDuelController match, EarthSceneReadinessGate gate,
            MagicInputController input, MagicExecutor world, EarthDualMouseAbilityController shots,
            Transform actor, Transform center, Transform arenaMarker, EarthPillarMobility launch, EarthPillarWaveAbility pillars)
        {
            Unsubscribe(); duel = match; readiness = gate; magic = input; executor = world; dualMouse = shots;
            player = actor; planet = center; arena = arenaMarker; pillar = launch; wave = pillars;
            if (isActiveAndEnabled) Subscribe();
        }
        private void OnEnable()
        {
            _legacyHud = GetComponent<EarthCoreHud>();
            if (_legacyHud != null) _legacyHud.ShowDiagnostics = Debug.isDebugBuild && showDiagnostics;
            _mana.Reset(); _ready = false; _entrance = 0;
            Build(); Subscribe();

        }
        private void OnDisable() { Unsubscribe(); _root?.RemoveFromHierarchy(); }
        private void Subscribe()
        {
            if (_subscribed) return;
            if (magic != null) magic.MagicCommandExecuted += OnCommand;
            if (dualMouse != null) dualMouse.StoneShotCommitted += Spend;
            if (pillar != null) pillar.PillarRaised += OnPillar;
            if (wave != null) wave.CastCommitted += OnWave;
            if (duel != null) duel.RoundRestarted += OnRestarted;
            _subscribed = true;
        }
        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (magic != null) magic.MagicCommandExecuted -= OnCommand;
            if (dualMouse != null) dualMouse.StoneShotCommitted -= Spend;
            if (pillar != null) pillar.PillarRaised -= OnPillar;
            if (wave != null) wave.CastCommitted -= OnWave;
            if (duel != null) duel.RoundRestarted -= OnRestarted;
            _subscribed = false;
        }
        private void OnCommand(MagicCommand command)
        {
            // Extraction and release are separate accepted actions, never count previews/rejections.
            Spend(command.Ability == EarthAbilityIds.LineWall || command.Ability == EarthAbilityIds.RaisePlatform ? 20f
                : command.Ability == EarthAbilityIds.FlickThrow ? 4f : 10f);
        }
        private void OnPillar(EarthPillarLaunchEvent value) => Spend(20f);
        private void OnWave(Vector3 point, float radius, int count) => Spend(20f);
        private void Spend(float value) { if (_ready && duel != null && !duel.IsRoundOver) { _mana.Spend(value); if (_manaGauge != null) _manaGauge.Flash = .7f; } }
        private void OnRestarted() { _mana.Reset(); _lastSeconds = -1; _healthGauge.Trail = 1; ResetLifeResult(); }
        private void ResetLifeResult()
        {
            _lifeResultState.Reset(LocalDeaths, OpponentDeaths);
            _lastLifeResult = EarthLifeResult.None;
            if (_lifeResultLabel != null) _lifeResultLabel.style.display = DisplayStyle.None;
        }
        private void UpdateLifeResult()
        {
            _lifeResultState.Step(LocalDeaths, OpponentDeaths, Time.deltaTime,
                _theme != null ? _theme.simultaneousDeathWindow : .12f, _theme != null ? _theme.lifeResultSeconds : 2.4f);
            var outcome = _lifeResultState.Result;
            if (_lastLifeResult != outcome)
            {
                _lastLifeResult = outcome;
                _lifeResultLabel.text = outcome == EarthLifeResult.Won ? "YOU WON ROUND" : outcome == EarthLifeResult.Lost ? "YOU LOST ROUND" : "DRAW";
                _lifeResultLabel.style.display = outcome == EarthLifeResult.None ? DisplayStyle.None : DisplayStyle.Flex;
                if (_theme != null) _lifeResultLabel.style.color = outcome == EarthLifeResult.Lost ? _theme.damage : outcome == EarthLifeResult.Won ? _theme.accent : _theme.text;
            }
            if (outcome != EarthLifeResult.None)
                _lifeResultLabel.style.opacity = Mathf.Min(Mathf.Clamp01(_lifeResultState.Age / .12f), Mathf.Clamp01(_lifeResultState.Remaining / .3f));
        }
        private void Update()
        {
            using (TickMarker.Auto())
            {
                // Inspector edits apply even while the world is paused or the menu hides the HUD.
                ApplyLayoutIfChanged();
                if (duel != null && _lifeResultLabel != null) UpdateLifeResult();
                if (_root == null || duel == null || player == null || planet == null || !_combatVisible) return;
                if (!_ready)
                {
                    if (readiness != null ? !readiness.IsReady : executor == null || executor.VoxelPlanet == null || !executor.VoxelPlanet.GeometryReady) return;
                    _ready = true;
                    _arenaRadial = planet.InverseTransformDirection((arena != null ? arena.position : player.position) - planet.position).normalized;
                }
                _entrance = Mathf.Min(.6f, _entrance + Time.unscaledDeltaTime);
                float enter = ReferenceActive && _reducedMotion ? Mathf.Clamp01(_entrance/.08f) : 1f - Mathf.Pow(1f - Mathf.Clamp01(_entrance / .45f), 3);
                float motionEnter = _reducedMotion ? 1 : enter;
                _root.style.opacity = enter;
                if (_appliedLayout != null)
                {
                    AnimateLayoutEntrance(_top, _appliedLayout.scoreboard, 0, (1 - motionEnter) * -120);
                    AnimateLayoutEntrance(_left, _appliedLayout.health.group, (1 - motionEnter) * -90, 0);
                    AnimateLayoutEntrance(_right, _appliedLayout.energy.group, (1 - motionEnter) * 90, 0);
                    AnimateLayoutEntrance(_navigation, _appliedLayout.navigation.group, (1 - motionEnter) * 230, 0);
                }
                else
                {
                    _top.style.translate = new Translate(Length.Percent(-50), (1 - motionEnter) * -120);
                    _left.style.translate = new Translate((1 - motionEnter) * -90, 0);
                    _right.style.translate = new Translate((1 - motionEnter) * 90, 0);
                    _navigation.style.translate = new Translate((1 - motionEnter) * 230, 0);
                }

                _legacy.style.display = (Debug.isDebugBuild && showDiagnostics) ? DisplayStyle.Flex : DisplayStyle.None;
                if (_legacyHud != null) _legacyHud.ShowDiagnostics = Debug.isDebugBuild && showDiagnostics;
                bool continuous = magic != null && (magic.IsGravityWellActive || magic.IsVectorFieldActive || magic.IsArmorActive || magic.SurfSpeed > .1f);
                if (!duel.IsRoundOver)
                {
                    if (continuous) _mana.Spend(8f * Time.deltaTime);
                    _mana.Step(Time.deltaTime);
                }
                float health = Mathf.Clamp01(LocalHealth / Mathf.Max(1, duel.MaximumHealth));
                if(ReferenceActive)
                {
                    if(_fillTarget!=health){_fillFrom=_healthGauge.Fill;_fillTarget=health;_fillAge=0;}
                    if(_trailTarget!=health){_trailFrom=health>_healthGauge.Trail?health:_healthGauge.Trail;_trailTarget=health;_trailAge=0;}
                    float manaTarget=_mana.Value/100;
                    if(_manaTarget!=manaTarget){_manaFrom=_manaGauge.Fill;_manaTarget=manaTarget;_manaAge=0;}
                    _fillAge+=Time.unscaledDeltaTime;_trailAge+=Time.unscaledDeltaTime;_manaAge+=Time.unscaledDeltaTime;
                    _healthGauge.Fill=Reference.SampleValue("meter_value",_fillFrom,_fillTarget,_fillAge,_reducedMotion);
                    _healthGauge.Trail=Reference.SampleValue("meter_damage_tail",_trailFrom,_trailTarget,_trailAge,_reducedMotion);
                    _manaGauge.Fill=Reference.SampleValue("meter_value",_manaFrom,_manaTarget,_manaAge,_reducedMotion);
                }
                else
                {
                    _healthGauge.Fill=health;
                    _healthGauge.Trail=health>_healthGauge.Trail?health:Mathf.MoveTowards(_healthGauge.Trail,health,Time.deltaTime*.28f);
                    _manaGauge.Fill=Mathf.Lerp(_manaGauge.Fill,_mana.Value/100,1-Mathf.Exp(-14*Time.unscaledDeltaTime));
                }
                _manaGauge.Trail = _manaGauge.Fill;
                _healthGauge.MarkDirtyRepaint(); _manaGauge.MarkDirtyRepaint();
                _healthGauge.Flash = Mathf.MoveTowards(_healthGauge.Flash, 0, Time.unscaledDeltaTime / .2f);
                _manaGauge.Flash = Mathf.MoveTowards(_manaGauge.Flash, 0, Time.unscaledDeltaTime / .18f);
                int healthValue = Mathf.CeilToInt(LocalHealth);
                if (_lastHealth >= 0 && healthValue < _lastHealth) _healthGauge.Flash = 1f;
                if (healthValue != _lastHealth) _left.EnableInClassList("duel-critical", health > 0f && health <= .25f);
                UpdateNumber(_healthText, healthValue, ref _lastHealth);
                UpdateNumber(_manaText, Mathf.CeilToInt(_mana.Value), ref _lastMana);
                UpdateNumber(_redScore, playerIsBlue ? duel.BotScore : duel.PlayerScore, ref _lastRed);
                UpdateNumber(_blueScore, playerIsBlue ? duel.PlayerScore : duel.BotScore, ref _lastBlue);
                int seconds = Mathf.CeilToInt(duel.RoundRemainingSeconds);
                if (seconds != _lastSeconds)
                {
                    _timer.text = $"{seconds / 60:00}:{seconds % 60:00}";
                    _clock.EnableInClassList("duel-clock-urgent", seconds <= 10 && seconds > 0);
                    if (_theme != null)
                    {
                        _timer.style.color = seconds <= 10 && seconds > 0 ? _theme.damage : _theme.text;
                        _clock.style.borderBottomColor = seconds <= 10 && seconds > 0 ? _theme.damage : _theme.accent;
                    }
                    _lastSeconds = seconds;
                }
                int respawn = Mathf.CeilToInt(LocalRespawn);
                if (respawn != _lastRespawn)
                {
                    _respawn.text = respawn > 0 ? $"RETURNING IN  {respawn}" : "";
                    _respawn.style.display = respawn > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                    _lastRespawn = respawn;
                }
                if (_roundOver != duel.IsRoundOver)
                {
                    _roundOver = duel.IsRoundOver; _result.style.display = _roundOver ? DisplayStyle.Flex : DisplayStyle.None;
                    bool blueWon = playerIsBlue ? duel.PlayerScore > duel.BotScore : duel.BotScore > duel.PlayerScore;
                    _resultText.text = duel.PlayerScore == duel.BotScore ? "DRAW" : blueWon ? "BLUE WINS" : "RED WINS";
                    if (_roundOver) _restart.Focus();
                }
                if (_referenceHud != null)
                    _referenceHud.Tick(SelectedElement,_reducedMotion,_roundOver,OpponentDeaths>LocalDeaths,OpponentDeaths==LocalDeaths,OpponentDeaths,LocalDeaths,_restartAllowed);
                Vector3 radial = planet.InverseTransformDirection(player.position - planet.position).normalized;
                // Parallel transport the view instead of pinning world north near either pole.
                Quaternion target = Quaternion.FromToRotation(_globe.View * radial, Vector3.forward) * _globe.View;
                _globe.View = Quaternion.Slerp(_globe.View, target, 1 - Mathf.Exp(-7 * Time.unscaledDeltaTime));
                _globe.PlayerRadial = radial;
                _globe.PlayerForward = planet.InverseTransformDirection(player.forward);
                _globe.ArenaRadial = _arenaRadial; _globe.HasNavigation = true; _globe.MarkDirtyRepaint();
            }
        }
        private static void UpdateNumber(Label label, int number, ref int old)
        { if (old == number) return; label.text = number.ToString(); old = number; }
        private static VisualElement Box(string name, string css)
        { var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore }; e.AddToClassList(css); return e; }
        private static Label Text(string text, string css)
        { var l = new Label(text) { pickingMode = PickingMode.Ignore }; l.AddToClassList(css); return l; }
        private void Build()
        {
            _appliedLayout = null; _layoutRevision = -1; _referenceHud=null;
            // A re-enabled document creates new labels; cached values belong to the old tree.
            _lastSeconds = _lastHealth = _lastMana = _lastRed = _lastBlue = _lastRespawn = -1;
            _roundOver = false; _fillTarget=_trailTarget=_manaTarget=-1;
            VisualElement document = GetComponent<UIDocument>().rootVisualElement;

            _root?.RemoveFromHierarchy();

            _legacy = document.Q(className: "screen") ?? new VisualElement();

            // Keep the existing aim reticle visible while hiding the diagnostic layout.

            VisualElement reticle = document.Q("aim-reticle");

            if (reticle != null) document.Add(reticle);

            _root = Box("duel-hud", "duel-hud"); document.Add(_root); _root.style.opacity = 0;
            _lifeResultLabel = Text("", "duel-life-result"); _lifeResultLabel.name = "life-result";
            _lifeResultLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _lifeResultLabel.style.backgroundColor = new Color(.03f, .055f, .07f, .45f);
            _lifeResultLabel.style.borderTopLeftRadius = _lifeResultLabel.style.borderTopRightRadius =
                _lifeResultLabel.style.borderBottomLeftRadius = _lifeResultLabel.style.borderBottomRightRadius = 10;
            _root.Add(_lifeResultLabel); ResetLifeResult();
            _pause = new Button(() => _pauseRequested?.Invoke()) { name = "pause-match", tooltip = "Pause (Esc)" };
            _pause.style.position = Position.Absolute; _pause.style.right = 24; _pause.style.top = 24;
            _pause.style.width = 44; _pause.style.height = 44;
            _pause.style.backgroundColor = new Color(.03f, .08f, .13f, .32f);
            _pause.style.borderTopWidth = _pause.style.borderBottomWidth = _pause.style.borderLeftWidth = _pause.style.borderRightWidth = 0;
            _pause.style.borderTopLeftRadius = _pause.style.borderTopRightRadius = _pause.style.borderBottomLeftRadius = _pause.style.borderBottomRightRadius = 8;
            _pause.style.flexDirection = FlexDirection.Row; _pause.style.alignItems = Align.Center; _pause.style.justifyContent = Justify.Center;
            _pauseIcon = Box("pause-icon", "duel-pause-icon");
            _pauseIcon.style.flexDirection = FlexDirection.Row; _pauseIcon.style.alignItems = Align.Center;
            _pauseIcon.style.justifyContent = Justify.Center;
            _pause.Add(_pauseIcon);
            for (int bar = 0; bar < 2; bar++)
            {
                var stroke = new VisualElement { pickingMode = PickingMode.Ignore };
                stroke.style.width = Length.Percent(20); stroke.style.height = Length.Percent(100); stroke.style.marginLeft = stroke.style.marginRight = Length.Percent(15);
                stroke.style.backgroundColor = new Color(1f, .957f, .867f, .72f); _pauseIcon.Add(stroke);
            }
            _pauseIcon.style.width = 20; _pauseIcon.style.height = 18;
            _root.Add(_pause);

            _top = Box("duel-scoreboard", "duel-scoreboard"); _root.Add(_top);

            var red = Box("red-team", "duel-team"); red.AddToClassList("duel-red"); _top.Add(red);

            _redScore = Text("0", "duel-score"); red.Add(_redScore); red.Add(Text("RED", "duel-caption"));

            _clock = Box("round-clock", "duel-clock"); _top.Add(_clock);

            _clock.Add(Text("EARTH  /  DUEL", "duel-caption")); _timer = Text("05:00", "duel-time"); _clock.Add(_timer);

            var blue = Box("blue-team", "duel-team"); blue.AddToClassList("duel-blue"); _top.Add(blue);

            _blueScore = Text("0", "duel-score"); blue.Add(_blueScore); blue.Add(Text("BLUE", "duel-caption"));

            _left = Box("health-panel", "duel-side"); _left.AddToClassList("duel-left"); _root.Add(_left);

            _healthGauge = new EarthDuelGauge { name = "health-gauge", Accent = new Color(.96f, .3f, .31f, 1) }; _healthGauge.AddToClassList("duel-gauge"); _left.Add(_healthGauge);

            _healthUnderBar = Box("health-under-bar", "duel-under-bar"); _left.Add(_healthUnderBar);
            _healthIcon = Text("+", "duel-symbol"); _healthIcon.name = "health-icon"; _healthUnderBar.Add(_healthIcon);
            _healthText = Text("100", "duel-vital-value"); _healthText.name = "health-value"; _healthUnderBar.Add(_healthText);

            _right = Box("mana-panel", "duel-side"); _right.AddToClassList("duel-right"); _root.Add(_right);

            _manaGauge = new EarthDuelGauge { name = "mana-gauge", Mirror = true, Accent = new Color(.25f, .82f, 1f, 1) }; _manaGauge.AddToClassList("duel-gauge"); _right.Add(_manaGauge);

            _energyUnderBar = Box("energy-under-bar", "duel-under-bar"); _right.Add(_energyUnderBar);
            _energyIcon = Text("✦", "duel-symbol"); _energyIcon.name = "energy-icon"; _energyUnderBar.Add(_energyIcon);
            _manaText = Text("100", "duel-vital-value"); _manaText.name = "energy-value"; _energyUnderBar.Add(_manaText);

            _navigation = Box("planet-panel", "duel-navigation"); _root.Add(_navigation);

            _orbitCaption = Text("LOCAL ORBIT", "duel-caption"); _orbitCaption.name = "orbit-caption"; _navigation.Add(_orbitCaption);

            _globe = new EarthHologramGlobe { name = "planet-globe" }; _globe.AddToClassList("duel-globe"); _navigation.Add(_globe);

            _orbitLegend = Text("▲ YOU     ▱ ARENA", "duel-legend"); _orbitLegend.name = "orbit-legend"; _navigation.Add(_orbitLegend);

            _respawn = Text("", "duel-respawn"); _respawn.style.display = DisplayStyle.None; _root.Add(_respawn);
            _result = Box("round-result", "duel-result"); _result.style.display = DisplayStyle.None; _root.Add(_result);

            _result.Add(Text("ROUND COMPLETE", "duel-caption")); _resultText = Text("", "duel-result-title"); _result.Add(_resultText);

            _restart = new Button(() => { if (_restartAllowed) duel.RestartRound(); }) { text = "NEW ROUND", name = "restart-round" }; _restart.AddToClassList("duel-restart"); _result.Add(_restart);
            SetFrontendPresentation(_theme, _combatVisible);
        }

    }

}

