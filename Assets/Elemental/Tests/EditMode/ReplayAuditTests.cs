using System.Collections.Generic;
using Elemental.Simulation.Magic;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class ReplayAuditTests
    {
        [Test]
        public void IdenticalCheckpointsReportNoDivergence()
        {
            List<ReplayCheckpoint> expected = Timeline(11ul);
            ReplayDivergenceReport report = ReplayAuditor.FindFirstDivergence(expected, Timeline(11ul));

            Assert.That(report.Diverged, Is.False);
            Assert.That(report.Reason, Does.Contain("match"));
        }

        [Test]
        public void FirstDivergenceReportsTickSubsystemAndBothHashes()
        {
            List<ReplayCheckpoint> expected = Timeline(11ul);
            List<ReplayCheckpoint> actual = Timeline(11ul);
            var changed = new ReplaySubsystemHashes(11ul, 22ul, 99ul, 44ul, 55ul);
            actual[1] = new ReplayCheckpoint(120u, in changed);

            ReplayDivergenceReport report = ReplayAuditor.FindFirstDivergence(expected, actual);

            Assert.That(report.Diverged, Is.True);
            Assert.That(report.Tick, Is.EqualTo(120u));
            Assert.That(report.Subsystem, Is.EqualTo(ReplaySubsystem.Fields));
            Assert.That(report.Expected, Is.EqualTo(33ul));
            Assert.That(report.Actual, Is.EqualTo(99ul));
        }

        private static List<ReplayCheckpoint> Timeline(ulong terrain)
        {
            var hashes = new ReplaySubsystemHashes(terrain, 22ul, 33ul, 44ul, 55ul);
            return new List<ReplayCheckpoint>
            {
                new ReplayCheckpoint(60u, in hashes),
                new ReplayCheckpoint(120u, in hashes),
                new ReplayCheckpoint(180u, in hashes)
            };
        }
    }
}
