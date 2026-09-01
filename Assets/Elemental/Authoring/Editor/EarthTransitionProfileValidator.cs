using System;
using System.Collections.Generic;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public static class EarthTransitionProfileValidator
    {
        [MenuItem("Elemental Suite/Validation/Validate Selected Transition Profile")]
        public static void ValidateSelectedMenu()
        {
            EarthTransitionProfile profile = Selection.activeObject as EarthTransitionProfile;
            var errors = new List<string>();
            if (!Validate(profile, errors))
                throw new InvalidOperationException(
                    "Earth transition profile validation failed:\n- " +
                    string.Join("\n- ", errors));
            Debug.Log(
                $"[Elemental] Earth transition profile valid: {profile.PairCount} pairs; " +
                $"profile flag={profile.UseTransitionProfile}, queue flag={profile.UseTransitionQueue}.",
                profile);
        }

        public static bool Validate(
            EarthTransitionProfile profile,
            List<string> errors)
        {
            if (profile == null)
            {
                errors?.Add("Select an EarthTransitionProfile asset.");
                return false;
            }

            bool valid = true;
            var selectors = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < profile.PairCount; index++)
            {
                EarthTransitionPairOverride pair = profile.PairAt(index);
                if (pair == null || !pair.HasValidSelector)
                {
                    errors?.Add($"Pair {index} has no destination state/category selector.");
                    valid = false;
                    continue;
                }
                string selector = SelectorKey(pair);
                if (!selectors.Add(selector))
                {
                    errors?.Add($"Pair {index} duplicates selector '{selector}'.");
                    valid = false;
                }
                EarthTransitionRule rule = pair.ToRule();
                if (rule.Family == EarthTransitionFamily.PoseInertialized &&
                    rule.BodyMask == EarthTransitionBodyMask.None)
                {
                    errors?.Add($"Pair {index} requests pose inertia with an empty body mask.");
                    valid = false;
                }
                if (rule.CancelPolicy == EarthTransitionCancelPolicy.InsideCancelWindow &&
                    !rule.CancelWindow.Enabled)
                {
                    errors?.Add($"Pair {index} uses cancel-window policy without a window.");
                    valid = false;
                }
                if (rule.FootReleasePolicy ==
                        EarthTransitionFootReleasePolicy.ReleaseAfterDelay &&
                    rule.FootReleaseSeconds <= 0f)
                {
                    errors?.Add($"Pair {index} uses delayed foot release with zero delay.");
                    valid = false;
                }
            }
            return valid;
        }

        private static string SelectorKey(EarthTransitionPairOverride pair) =>
            $"SS:{pair.MatchSourceState}:{pair.SourceState}|" +
            $"DS:{pair.MatchDestinationState}:{pair.DestinationState}|" +
            $"SC:{pair.MatchSourceCategory}:{pair.SourceCategory}|" +
            $"DC:{pair.MatchDestinationCategory}:{pair.DestinationCategory}";
    }
}
