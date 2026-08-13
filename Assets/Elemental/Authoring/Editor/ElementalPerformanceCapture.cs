using System;
using System.Diagnostics;
using System.IO;
using Elemental.Simulation.Capabilities;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public static class ElementalPerformanceCapture
    {
        private const int SimulatedTicks = 60 * 60 * 60;

        [MenuItem("Elemental/Diagnostics/Capture Capability Matrix")]
        public static void Capture()
        {
            var report = new PerformanceMatrixReport
            {
                unityVersion = Application.unityVersion,
                utc = DateTime.UtcNow.ToString("O"),
                operatingSystem = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType,
                processorCount = SystemInfo.processorCount,
                systemMemoryMegabytes = SystemInfo.systemMemorySize,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                simulatedTicks = SimulatedTicks,
                profiles = new[]
                {
                    Run(CapabilityProfileData.NativeHigh),
                    Run(CapabilityProfileData.NativeLow),
                    Run(CapabilityProfileData.WebLab)
                }
            };
            string root = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Could not resolve project root.");
            string folder = Path.Combine(root, "BuildReports");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "PerformanceMatrix.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            UnityEngine.Debug.Log("[Elemental] Capability matrix captured: " + path);
        }

        private static ProfilePerformanceEvidence Run(CapabilityProfileData profile)
        {
            var scheduler = new AdaptiveBudgetScheduler(in profile);
            var normal = new BudgetPressure(0.75f, 0.8f, 0.9f, 0.7f);
            var visualPressure = new BudgetPressure(1.5f, 0.8f, 0.9f, 0.7f);
            scheduler.Evaluate(in normal);
            var stopwatch = new Stopwatch();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Start();
            bool changedCanonicalRules = false;
            int presentationDegradations = 0;
            for (int tick = 0; tick < SimulatedTicks; tick++)
            {
                BudgetPressure pressure = tick % 600 == 0 ? visualPressure : normal;
                DegradationDecision decision = scheduler.Evaluate(in pressure);
                changedCanonicalRules |= decision.CanonicalActiveRulesChanged;
                if (decision.Kind == DegradationKind.ReducePresentation) presentationDegradations++;
            }
            stopwatch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            return new ProfilePerformanceEvidence
            {
                profile = profile.Kind.ToString(),
                durationMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                managedBytesAllocated = allocated,
                presentationDegradations = presentationDegradations,
                canonicalRulesChanged = changedCanonicalRules,
                passed = allocated == 0L && !changedCanonicalRules && presentationDegradations > 0
            };
        }

        [Serializable]
        private sealed class PerformanceMatrixReport
        {
            public string unityVersion;
            public string utc;
            public string operatingSystem;
            public string processor;
            public int processorCount;
            public int systemMemoryMegabytes;
            public string graphicsDevice;
            public int simulatedTicks;
            public ProfilePerformanceEvidence[] profiles;
        }

        [Serializable]
        private sealed class ProfilePerformanceEvidence
        {
            public string profile;
            public double durationMilliseconds;
            public long managedBytesAllocated;
            public int presentationDegradations;
            public bool canonicalRulesChanged;
            public bool passed;
        }
    }
}
