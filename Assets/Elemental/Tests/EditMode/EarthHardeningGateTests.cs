using System.IO;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthHardeningGateTests
    {
        private static readonly string[] RequiredProfilerMarkers =
        {
            "Elemental.Earth.Fracture.Damage",
            "Elemental.Earth.Fracture.Islands",
            "Elemental.Earth.Repair.Select",
            "Elemental.Earth.Repair.Order",
            "Elemental.Earth.Repair.Solve",
            "Elemental.Earth.Repair.Weld",
            "Elemental.Earth.Proxy.Switch",
            "Elemental.Earth.Gesture.Sample",
            "Elemental.Earth.Gesture.Recognize",
            "Elemental.Earth.Intent.Resolve",
            "Elemental.Earth.Camera.Direct",
            "Elemental.Earth.Feedback.Route"
        };

        [Test]
        public void RequiredEarthProfilerBoundariesRemainInstrumented()
        {
            string[] runtimeFiles = Directory.GetFiles(
                "Assets/Elemental", "*.cs", SearchOption.AllDirectories);
            for (int markerIndex = 0; markerIndex < RequiredProfilerMarkers.Length; markerIndex++)
            {
                string marker = RequiredProfilerMarkers[markerIndex];
                bool found = false;
                for (int fileIndex = 0; fileIndex < runtimeFiles.Length && !found; fileIndex++)
                {
                    string path = runtimeFiles[fileIndex].Replace('\\', '/');
                    if (path.Contains("/Tests/")) continue;
                    found = File.ReadAllText(runtimeFiles[fileIndex]).Contains(marker);
                }
                Assert.That(found, Is.True, $"Missing required profiler marker: {marker}");
            }
        }

        [Test]
        public void FinalBuildEvidenceAndCapabilityMatrixAreGreen()
        {
            string development = File.ReadAllText("BuildReports/NativeWindows.json");
            string release = File.ReadAllText("BuildReports/NativeWindowsRelease.json");
            string matrix = File.ReadAllText("BuildReports/PerformanceMatrix.json");

            StringAssert.Contains("\"result\": \"Succeeded\"", development);
            StringAssert.Contains("\"warnings\": 0", development);
            StringAssert.Contains("\"errors\": 0", development);
            StringAssert.Contains("\"result\": \"Succeeded\"", release);
            StringAssert.Contains("\"warnings\": 0", release);
            StringAssert.Contains("\"errors\": 0", release);
            Assert.That(Count(matrix, "\"managedBytesAllocated\": 0"), Is.EqualTo(3));
            Assert.That(Count(matrix, "\"canonicalRulesChanged\": false"), Is.EqualTo(3));
            Assert.That(Count(matrix, "\"passed\": true"), Is.EqualTo(3));
        }

        [Test]
        public void FinalVisualBaselinesArePresentAndNonBlank()
        {
            string[] names =
            {
                "dawn.png", "wave.png", "platform.png", "gravity.png",
                "meteor.png", "mage_cast.png", "reassembly.png"
            };
            for (int index = 0; index < names.Length; index++)
            {
                string path = Path.Combine("BuildReports/VisualQa/M10Final", names[index]);
                Assert.That(File.Exists(path), Is.True, path);
                byte[] bytes = File.ReadAllBytes(path);
                Assert.That(bytes.Length, Is.GreaterThan(100_000), path);
                Assert.That(bytes[0], Is.EqualTo(0x89), path);
                Assert.That(bytes[1], Is.EqualTo(0x50), path);
                Assert.That(bytes[2], Is.EqualTo(0x4E), path);
                Assert.That(bytes[3], Is.EqualTo(0x47), path);
            }
        }

        private static int Count(string value, string token)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(token, offset, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += token.Length;
            }
            return count;
        }
    }
}
