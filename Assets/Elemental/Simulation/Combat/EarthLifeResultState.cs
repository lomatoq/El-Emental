using System;

namespace Elemental.Simulation.Combat
{
    public enum EarthLifeResult { None, Won, Lost, Draw }

    /// <summary>Score-derived per-life feedback; recoverable knockdowns never enter this state.</summary>
    public struct EarthLifeResultState
    {
        private int _localDeaths, _opponentDeaths;
        private bool _lost, _won, _pending;
        private float _settleRemaining;
        public EarthLifeResult Result { get; private set; }
        public float Remaining { get; private set; }
        public float Age { get; private set; }
        public int Resolutions { get; private set; }

        public void Reset(int localDeaths = 0, int opponentDeaths = 0)
        { this = default; _localDeaths = localDeaths; _opponentDeaths = opponentDeaths; }

        public void Step(int localDeaths, int opponentDeaths, float deltaSeconds, float simultaneousWindow = .12f, float displaySeconds = 2.4f)
        {
            if (localDeaths < _localDeaths || opponentDeaths < _opponentDeaths) Reset(localDeaths, opponentDeaths);
            float dt = float.IsFinite(deltaSeconds) ? Math.Max(0, deltaSeconds) : 0;
            bool lost = localDeaths > _localDeaths, won = opponentDeaths > _opponentDeaths;
            _localDeaths = localDeaths; _opponentDeaths = opponentDeaths;
            if (lost || won)
            {
                if (!_pending)
                {
                    _settleRemaining = Math.Max(0, simultaneousWindow);
                    _lost = _won = false; _pending = true;
                    Result = EarthLifeResult.None; Remaining = Age = 0;
                }
                _lost |= lost; _won |= won;
                // Start the observation window after this sample, not before it.
                return;
            }
            if (_pending)
            {
                _settleRemaining -= dt;
                if (_settleRemaining > 0) return;
                Result = _lost && _won ? EarthLifeResult.Draw : _won ? EarthLifeResult.Won : EarthLifeResult.Lost;
                Remaining = Math.Max(.3f, displaySeconds); Age = 0; Resolutions++; _pending = false;
                return;
            }
            Remaining = Math.Max(0, Remaining - dt);
            if (Remaining > 0) Age += dt;
            else Result = EarthLifeResult.None;
        }
    }
}
