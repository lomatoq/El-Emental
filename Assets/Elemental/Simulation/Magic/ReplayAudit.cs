using System;
using System.Collections.Generic;

namespace Elemental.Simulation.Magic
{
    public enum ReplaySubsystem : byte
    {
        Terrain = 1,
        Characters = 2,
        Fields = 3,
        ThermalWater = 4,
        Missions = 5
    }

    public readonly struct ReplaySubsystemHashes
    {
        public ReplaySubsystemHashes(ulong terrain, ulong characters, ulong fields, ulong thermalWater, ulong missions)
        {
            Terrain = terrain;
            Characters = characters;
            Fields = fields;
            ThermalWater = thermalWater;
            Missions = missions;
        }

        public ulong Terrain { get; }
        public ulong Characters { get; }
        public ulong Fields { get; }
        public ulong ThermalWater { get; }
        public ulong Missions { get; }

        public ulong Get(ReplaySubsystem subsystem)
        {
            return subsystem switch
            {
                ReplaySubsystem.Terrain => Terrain,
                ReplaySubsystem.Characters => Characters,
                ReplaySubsystem.Fields => Fields,
                ReplaySubsystem.ThermalWater => ThermalWater,
                ReplaySubsystem.Missions => Missions,
                _ => throw new ArgumentOutOfRangeException(nameof(subsystem))
            };
        }
    }

    public readonly struct ReplayCheckpoint
    {
        public ReplayCheckpoint(uint tick, in ReplaySubsystemHashes hashes)
        {
            Tick = tick;
            Hashes = hashes;
        }

        public uint Tick { get; }
        public ReplaySubsystemHashes Hashes { get; }
    }

    public readonly struct ReplayDivergenceReport
    {
        public ReplayDivergenceReport(bool diverged, uint tick, ReplaySubsystem subsystem, ulong expected, ulong actual, string reason)
        {
            Diverged = diverged;
            Tick = tick;
            Subsystem = subsystem;
            Expected = expected;
            Actual = actual;
            Reason = reason ?? string.Empty;
        }

        public bool Diverged { get; }
        public uint Tick { get; }
        public ReplaySubsystem Subsystem { get; }
        public ulong Expected { get; }
        public ulong Actual { get; }
        public string Reason { get; }
    }

    public static class ReplayAuditor
    {
        private static readonly ReplaySubsystem[] OrderedSubsystems =
        {
            ReplaySubsystem.Terrain,
            ReplaySubsystem.Characters,
            ReplaySubsystem.Fields,
            ReplaySubsystem.ThermalWater,
            ReplaySubsystem.Missions
        };

        public static ReplayDivergenceReport FindFirstDivergence(
            IReadOnlyList<ReplayCheckpoint> expected,
            IReadOnlyList<ReplayCheckpoint> actual)
        {
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            if (actual == null) throw new ArgumentNullException(nameof(actual));

            int shared = Math.Min(expected.Count, actual.Count);
            for (int index = 0; index < shared; index++)
            {
                ReplayCheckpoint left = expected[index];
                ReplayCheckpoint right = actual[index];
                if (left.Tick != right.Tick)
                {
                    return new ReplayDivergenceReport(
                        true, Math.Min(left.Tick, right.Tick), default, left.Tick, right.Tick,
                        "Checkpoint timeline differs.");
                }

                for (int subsystemIndex = 0; subsystemIndex < OrderedSubsystems.Length; subsystemIndex++)
                {
                    ReplaySubsystem subsystem = OrderedSubsystems[subsystemIndex];
                    ulong expectedHash = left.Hashes.Get(subsystem);
                    ulong actualHash = right.Hashes.Get(subsystem);
                    if (expectedHash == actualHash) continue;
                    return new ReplayDivergenceReport(
                        true, left.Tick, subsystem, expectedHash, actualHash,
                        $"First divergence at tick {left.Tick} in {subsystem}.");
                }
            }

            if (expected.Count != actual.Count)
            {
                uint tick = expected.Count > shared ? expected[shared].Tick : actual[shared].Tick;
                return new ReplayDivergenceReport(
                    true, tick, default, (ulong)expected.Count, (ulong)actual.Count,
                    "Checkpoint count differs.");
            }

            return new ReplayDivergenceReport(false, 0u, default, 0ul, 0ul, "Replay checkpoints match.");
        }
    }
}
