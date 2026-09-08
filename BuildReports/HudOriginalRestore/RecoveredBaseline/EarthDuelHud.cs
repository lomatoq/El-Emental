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
        private VisualElement _root, _top, _left, _right, _navigation, _result, _legacy;
        private EarthDuelGauge _healthGauge, _manaGauge;
        private EarthHologramGlobe _globe;
        private Label _redScore, _blueScore, _timer, _healthText, _manaText, _resultText, _respawn;
        private Button _restart;
        private float _entrance;
        private bool _ready, _subscribed, _roundOver, _matchStarted;
        private int _lastSeconds = -1, _lastHealth = -1, _lastMana = -1, _lastRed = -1, _lastBlue = -1, _lastRespawn = -1;
        private Vector3 _arenaRadial;
        private EarthCoreHud _legacyHud;
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
            if (_legacyHud != null) _legacyHud.ShowDiagnostics = showDiagnostics;
            _mana.Reset(); _ready = false; _matchStarted = false; _entrance = 0;
            Build(); Subscribe();
            if (duel != null) duel.SetRoundReady(false);
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
        private void Spend(float value) { if (_ready && duel != null && !duel.IsRoundOver) _mana.Spend(value); }
        private void OnRestarted() { _mana.Reset(); _lastSeconds = -1; _healthGauge.Trail = 1; }
        private void Update()
        {
            using (TickMarker.Auto())
            {
                if (_root == null || duel == null || player == null || planet == null) return;
                if (!_ready)
                {
                    if (readiness != null ? !readiness.IsReady : executor == null || executor.VoxelPlanet == null || !executor.VoxelPlanet.GeometryReady) return;
                    _ready = true;
                    _arenaRadial = planet.InverseTransformDirection((arena != null ? arena.position : player.position) - planet.position).normalized;
                }
                _entrance = Mathf.Min(.6f, _entrance + Time.unscaledDeltaTime);
                float enter = 1f - Mathf.Pow(1f - Mathf.Clamp01(_entrance / .45f), 3);
                _root.style.opacity = enter;
                _top.style.translate = new Translate(Length.Percent(-50), (1 - enter) * -120);
                _left.style.translate = new Translate((1 - enter) * -90, 0);
                _right.style.translate = new Translate((1 - enter) * 90, 0);
                _navigation.style.translate = new Translate((1 - enter) * 230, 0);
                if (!_matchStarted && _entrance >= .5f) { _matchStarted = true; duel.SetRoundReady(true); }
                _legacy.style.display = showDiagnostics ? DisplayStyle.Flex : DisplayStyle.None;
                if (_legacyHud != null) _legacyHud.ShowDiagnostics = showDiagnostics;
                bool continuous = magic != null && (magic.IsGravityWellActive || magic.IsVectorFieldActive || magic.IsArmorActive || magic.SurfSpeed > .1f);
                if (!duel.IsRoundOver)
                {
                    if (continuous) _mana.Spend(8f * Time.deltaTime);
                    _mana.Step(Time.deltaTime);
                }
                float health = Mathf.Clamp01(duel.PlayerHealth / Mathf.Max(1, duel.MaximumHealth));
                _healthGauge.Fill = health;
                _healthGauge.Trail = health > _healthGauge.Trail ? health : Mathf.MoveTowards(_healthGauge.Trail, health, Time.deltaTime * .28f);
                _manaGauge.Fill = Mathf.Lerp(_manaGauge.Fill, _mana.Value / 100, 1 - Mathf.Exp(-14 * Time.unscaledDeltaTime));
                _manaGauge.Trail = _manaGauge.Fill;
                _healthGauge.MarkDirtyRepaint(); _manaGauge.MarkDirtyRepaint();
                UpdateNumber(_healthText, Mathf.CeilToInt(duel.PlayerHealth), ref _lastHealth);
                UpdateNumber(_manaText, Mathf.CeilToInt(_mana.Value), ref _lastMana);
                UpdateNumber(_redScore, playerIsBlue ? duel.BotScore : duel.PlayerScore, ref _lastRed);
                UpdateNumber(_blueScore, playerIsBlue ? duel.PlayerScore : duel.BotScore, ref _lastBlue);
                int seconds = Mathf.CeilToInt(duel.RoundRemainingSeconds);
                if (seconds != _lastSeconds) { _timer.text = $"{seconds / 60:00}:{seconds % 60:00}"; _lastSeconds = seconds; }
                int respawn = Mathf.CeilToInt(duel.PlayerRespawnRemaining);
                if (respawn != _lastRespawn)
                {
                    _respawn.text = respawn > 0 ? $"RETURNING IN  {respawn}" : ""; _lastRespawn = respawn;
                }
                if (_roundOver != duel.IsRoundOver)
                {
                    _roundOver = duel.IsRoundOver; _result.style.display = _roundOver ? DisplayStyle.Flex : DisplayStyle.None;
                    bool blueWon = playerIsBlue ? duel.PlayerScore > duel.BotScore : duel.BotScore > duel.PlayerScore;
                    _resultText.text = duel.PlayerScore == duel.BotScore ? "DRAW" : blueWon ? "BLUE WINS" : "RED WINS";
                    if (_roundOver) _restart.Focus();
                }
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
            VisualElement document = GetComponent<UIDocument>().rootVisualElement;
            _root?.RemoveFromHierarchy();
            _legacy = document.Q(className: "screen") ?? new VisualElement();
            // Keep the existing aim reticle visible while hiding the diagnostic layout.
            VisualElement reticle = document.Q("aim-reticle");
            if (reticle != null) document.Add(reticle);
            _root = Box("duel-hud", "duel-hud"); document.Add(_root); _root.style.opacity = 0;
            _top = Box("duel-scoreboard", "duel-scoreboard"); _root.Add(_top);
            var red = Box("red-team", "duel-team"); red.AddToClassList("duel-red"); _top.Add(red);
            _redScore = Text("0", "duel-score"); red.Add(_redScore); red.Add(Text("RED", "duel-caption"));
            var clock = Box("round-clock", "duel-clock"); _top.Add(clock);
            clock.Add(Text("EARTH  /  DUEL", "duel-caption")); _timer = Text("05:00", "duel-time"); clock.Add(_timer);
            var blue = Box("blue-team", "duel-team"); blue.AddToClassList("duel-blue"); _top.Add(blue);
            _blueScore = Text("0", "duel-score"); blue.Add(_blueScore); blue.Add(Text("BLUE", "duel-caption"));
            _left = Box("health-panel", "duel-side"); _left.AddToClassList("duel-left"); _root.Add(_left);
            _healthGauge = new EarthDuelGauge { name = "health-gauge", Accent = new Color(.96f, .3f, .31f, 1) }; _healthGauge.AddToClassList("duel-gauge"); _left.Add(_healthGauge);
            _left.Add(Text("+", "duel-symbol")); _healthText = Text("100", "duel-vital-value"); _left.Add(_healthText);
            _right = Box("mana-panel", "duel-side"); _right.AddToClassList("duel-right"); _root.Add(_right);
            _manaGauge = new EarthDuelGauge { name = "mana-gauge", Mirror = true, Accent = new Color(.25f, .82f, 1f, 1) }; _manaGauge.AddToClassList("duel-gauge"); _right.Add(_manaGauge);
            _right.Add(Text("✦", "duel-symbol")); _manaText = Text("100", "duel-vital-value"); _right.Add(_manaText);
            _navigation = Box("planet-panel", "duel-navigation"); _root.Add(_navigation);
            _navigation.Add(Text("LOCAL ORBIT", "duel-caption"));
            _globe = new EarthHologramGlobe { name = "planet-globe" }; _globe.AddToClassList("duel-globe"); _navigation.Add(_globe);
            _navigation.Add(Text("▲ YOU     ▱ ARENA", "duel-legend"));
            _respawn = Text("", "duel-respawn"); _root.Add(_respawn);
            _result = Box("round-result", "duel-result"); _result.style.display = DisplayStyle.None; _root.Add(_result);
            _result.Add(Text("ROUND COMPLETE", "duel-caption")); _resultText = Text("", "duel-result-title"); _result.Add(_resultText);
            _restart = new Button(() => duel.RestartRound()) { text = "NEW ROUND", name = "restart-round" }; _restart.AddToClassList("duel-restart"); _result.Add(_restart);
        }
    }
}
