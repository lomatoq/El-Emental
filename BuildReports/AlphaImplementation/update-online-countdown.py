from pathlib import Path
r=Path('BuildReports/AlphaImplementation/Stage2Pending')
def edit(rel,a,b):
 p=r/rel;s=p.read_text(encoding='utf-8-sig');assert a in s,p;p.write_text(s.replace(a,b),encoding='utf-8')
p='Assets/Elemental/NetworkingStage2/NgoGameplayTransport.cs'
edit(p,'        public bool Running => _started;','        private bool _countingDown;\n        private double _beginAt;\n        private uint _beginTick;\n        public float CountdownSeconds { get; set; } = 4f;\n        public float SecondsUntilStart => _countingDown && _network != null ? Mathf.Max(0f, (float)(_beginAt - _network.ServerTime.Time)) : 0f;\n        public bool Running => _started;')
edit(p,'        public event Action RoundStarted;','        public event Action CountdownStarted;')
edit(p,'            if (Time.unscaledTime < _nextHandshake || _started) return;','            if (_countingDown)\n            {\n                if (SecondsUntilStart <= 0f) StartRound(_beginTick);\n                return;\n            }\n            if (Time.unscaledTime < _nextHandshake || _started) return;')
edit(p,'                    Send(begin, _remoteClient); StartRound(begin.Tick);','                    begin.Value = (float)(_network.ServerTime.Time + Mathf.Clamp(CountdownSeconds, 1f, 30f));\n                    Send(begin, _remoteClient); ScheduleRound(begin.Tick, begin.Value);')
edit(p,'                    if (!_started) StartRound(packet.Tick); return;','                    if (!_started && !_countingDown) ScheduleRound(packet.Tick, packet.Value); return;')
edit(p,'        private void StartRound(uint tick)\n        { binding.BeginRound(tick); _started = true; SetStatus("Online round running."); RoundStarted?.Invoke(); }','''        private void ScheduleRound(uint tick, double deadline)
        {
            double remaining = deadline - _network.ServerTime.Time;
            if (remaining < -2d || remaining > 31d) { Fail("Invalid or expired round countdown."); return; }
            _beginTick = tick; _beginAt = deadline; _countingDown = true;
            SetStatus("Both players ready. Match starts after the countdown."); CountdownStarted?.Invoke();
        }
        private void StartRound(uint tick)
        { binding.BeginRound(tick); _started = true; _countingDown = false; SetStatus("Online round running."); }''')
edit(p,'            _registered = false; _started = false;','            _registered = false; _started = false; _countingDown = false; _beginAt = 0d; _beginTick = 0;')
p='Assets/Elemental/NetworkingStage2/EarthOnlineFrontend.cs'
edit(p,'transport.RoundStarted += RoundStarted','transport.CountdownStarted += CountdownStarted')
edit(p,'transport.RoundStarted -= RoundStarted','transport.CountdownStarted -= CountdownStarted')
edit(p,'            transport.SetReady(true);','            transport.CountdownSeconds = flow.CountdownDurationSeconds;\n            transport.SetReady(true);')
edit(p,'        private void RoundStarted()','        private void CountdownStarted()')
edit(p,'flow.BeginOnlineMatch();','flow.BeginOnlineMatch(() => transport.SecondsUntilStart);')
p='FrontendPatch/Modified/FrontendFlowController.cs'
edit(p,'        public float PresentationTransitionSeconds => theme.transitionSeconds;','        public float PresentationTransitionSeconds => theme.transitionSeconds;\n        public float CountdownDurationSeconds => Mathf.Max(1f, theme.countdownSeconds);\n        private Func<float> _networkCountdownRemaining;')
edit(p,'                float remaining = Mathf.Max(0f, duration - _transition);','                float remaining = _networkRound && _networkCountdownRemaining != null\n                    ? _networkCountdownRemaining() : Mathf.Max(0f, duration - _transition);')
edit(p,'        public bool BeginOnlineMatch()','        public bool BeginOnlineMatch(Func<float> countdownRemaining)')
edit(p,'            _networkRound = true; State = FrontendState.Starting; _transition = 0;','            _networkCountdownRemaining = countdownRemaining ?? throw new ArgumentNullException(nameof(countdownRemaining));\n            _networkRound = true; State = FrontendState.Starting; _transition = 0;')
edit(p,'            RestorePauseState();\n            _networkRound = false;','            RestorePauseState();\n            _networkCountdownRemaining = null;\n            _networkRound = false;')
p=r/'INTEGRATION.md';s=p.read_text(encoding='utf-8-sig');s+='\nCountdown steering (2026-09-06): host sends Begin with a shared NGO ServerTime deadline after both-ready/world barriers. Transport raises CountdownStarted without enabling binding input or advancing round time; BeginRound occurs only at deadline. Frontend reads SecondsUntilStart each frame for 4–3–2–1 and final 1.5-second camera blend. Late/invalid expired deadlines fail explicitly. Prepared source only: SDK compile and delayed-network two-player proof still required. Reference: https://docs-multiplayer.unity3d.com/netcode/1.4.0/advanced-topics/networktime-ticks/\n';p.write_text(s,encoding='utf-8')
