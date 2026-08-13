using System;
using System.Collections.Generic;

namespace Elemental.Presentation.Rendering
{
    public enum VisualQaScenario : byte
    {
        None = 0,
        Wall = 1,
        PullPreview = 2,
        PullHeld = 3,
        Throw = 4,
        WallCollapse = 5,
        PillarWave = 6,
        Platform = 7,
        LandingCushion = 8,
        GravityWell = 9,
        WallDebris = 10,
        Dawn = 11,
        Night = 12,
        Meteor = 13,
        MageCast = 14,
        Reassembly = 15,
        EarthMaterialFracture = 16
    }

    public readonly struct VisualQaCaptureRequest
    {
        public const string Argument = "-elementalVisualQa";
        public const string MagicArgument = "-elementalVisualQaMagic";
        public const string ScenarioArgument = "-elementalVisualQaScenario";

        public VisualQaCaptureRequest(string outputPath, VisualQaScenario scenario)
        {
            OutputPath = outputPath;
            Scenario = scenario;
        }

        public string OutputPath { get; }
        public VisualQaScenario Scenario { get; }
        public bool DemonstrateMagic => Scenario != VisualQaScenario.None;

        public static bool TryParse(IReadOnlyList<string> arguments, out VisualQaCaptureRequest request)
        {
            request = default;
            if (arguments == null) return false;
            for (int index = 0; index < arguments.Count - 1; index++)
            {
                if (!string.Equals(arguments[index], Argument, StringComparison.OrdinalIgnoreCase)) continue;
                string output = arguments[index + 1];
                if (string.IsNullOrWhiteSpace(output)) return false;
                VisualQaScenario scenario = VisualQaScenario.None;
                for (int candidate = 0; candidate < arguments.Count; candidate++)
                {
                    if (string.Equals(arguments[candidate], MagicArgument, StringComparison.OrdinalIgnoreCase))
                    {
                        scenario = VisualQaScenario.Wall;
                    }
                    if (candidate < arguments.Count - 1 &&
                        string.Equals(arguments[candidate], ScenarioArgument, StringComparison.OrdinalIgnoreCase))
                    {
                        scenario = ParseScenario(arguments[candidate + 1]);
                    }
                }

                request = new VisualQaCaptureRequest(output, scenario);
                return true;
            }
            return false;
        }

        private static VisualQaScenario ParseScenario(string value)
        {
            if (string.Equals(value, "wall", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.Wall;
            if (string.Equals(value, "wall-collapse", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.WallCollapse;
            if (string.Equals(value, "pull-preview", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.PullPreview;
            if (string.Equals(value, "pull-held", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.PullHeld;
            if (string.Equals(value, "throw", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.Throw;
            if (string.Equals(value, "wave", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.PillarWave;
            if (string.Equals(value, "platform", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.Platform;
            if (string.Equals(value, "cushion", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.LandingCushion;
            if (string.Equals(value, "gravity", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.GravityWell;
            if (string.Equals(value, "wall-debris", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.WallDebris;
            if (string.Equals(value, "dawn", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.Dawn;
            if (string.Equals(value, "night", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.Night;
            if (string.Equals(value, "meteor", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.Meteor;
            if (string.Equals(value, "mage-cast", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.MageCast;
            if (string.Equals(value, "reassembly", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.Reassembly;
            if (string.Equals(value, "earth-material", StringComparison.OrdinalIgnoreCase)) return VisualQaScenario.EarthMaterialFracture;
            return VisualQaScenario.None;
        }
    }
}
