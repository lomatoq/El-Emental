using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Diagnostics
{
    /// <summary>
    /// Startup-only aggregate timings. Recording is sealed when the scene readiness
    /// gate opens, so later casts cannot contaminate the loading sample.
    /// </summary>
    public static class EarthStartupTiming
    {
        public enum Category
        {
            ArenaInitialize,
            ArenaBevel,
            ArenaMeshPicking,
            WallAwake,
            WallPieceVisuals,
            PlatformPoolAwake,
            PlatformRuntimeMeshes,
            PillarPoolAwake,
            PillarMeshLibrary,
            PillarColumns,
            DebrisPoolAwake,
            HeroFragmentPoolAwake,
            Count
        }

        private static readonly long[] ElapsedTicks = new long[(int)Category.Count];
        private static readonly int[] Counts = new int[(int)Category.Count];
        private static readonly ProfilerMarker[] Markers = BuildMarkers();
        private static bool _recording = true;

        public readonly struct Scope : IDisposable
        {
            private readonly Category _category;
            private readonly long _started;
            private readonly bool _active;
            private readonly ProfilerMarker.AutoScope _profiler;

            internal Scope(Category category, bool active)
            {
                _category = category;
                _active = active;
                _started = active ? Stopwatch.GetTimestamp() : 0;
                _profiler = active ? Markers[(int)category].Auto() : default;
            }

            public void Dispose()
            {
                if (!_active) return;
                _profiler.Dispose();
                int index = (int)_category;
                ElapsedTicks[index] += Stopwatch.GetTimestamp() - _started;
                Counts[index]++;
            }
        }

        public static Scope Measure(Category category) => new(category, _recording);

        public static string SealAndFormat()
        {
            _recording = false;
            return Format();
        }

        public static string Format()
        {
            var output = new StringBuilder(384);
            for (int index = 0; index < (int)Category.Count; index++)
            {
                if (index > 0) output.Append(';');
                output.Append((Category)index).Append('=')
                    .Append((ElapsedTicks[index] * 1000.0 / Stopwatch.Frequency)
                        .ToString("F2", CultureInfo.InvariantCulture))
                    .Append("ms/").Append(Counts[index]);
            }
            return output.ToString();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Array.Clear(ElapsedTicks, 0, ElapsedTicks.Length);
            Array.Clear(Counts, 0, Counts.Length);
            _recording = true;
        }

        private static ProfilerMarker[] BuildMarkers()
        {
            var markers = new ProfilerMarker[(int)Category.Count];
            for (int index = 0; index < markers.Length; index++)
                markers[index] = new ProfilerMarker("Elemental.Startup." + (Category)index);
            return markers;
        }
    }
}
