using System;
using Elemental.Core.Time;
using Elemental.Simulation.Time;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class WorldBootstrap : MonoBehaviour
    {
        private static readonly ProfilerMarker FixedTickMarker = new ProfilerMarker("Elemental.World.FixedTick");

        [SerializeField, Min(1)] private int physicsTickRate = 60;

        private SimulationClock _clock;
        private bool _smokeAutoQuit;

        public SimulationTick CurrentTick => _clock?.CurrentTick ?? default;

        private void Awake()
        {
            _clock = new SimulationClock(physicsTickRate);
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], "-smokeAutoQuit", StringComparison.OrdinalIgnoreCase))
                {
                    _smokeAutoQuit = true;
                    break;
                }
            }
        }

        private void FixedUpdate()
        {
            using (FixedTickMarker.Auto())
            {
                SimulationTick tick = _clock.Advance();
                if (_smokeAutoQuit && tick.Value >= 120u)
                {
                    Application.Quit(0);
                }
            }
        }
    }
}
