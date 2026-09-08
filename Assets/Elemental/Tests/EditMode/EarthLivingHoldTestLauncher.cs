using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class EarthLivingHoldTestLauncher
    {
        [MenuItem("Elemental/QA/Living Hold Edit")]
        public static void RunEdit() => Run(TestMode.EditMode, "LivingHoldEdit",
            "Elemental.Tests.EditMode.EarthLivingHoldTests",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests");

        [MenuItem("Elemental/QA/Living Hold Play")]
        public static void RunPlay() => Run(TestMode.PlayMode, "LivingHoldPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.AuthoredLivingHoldMovesForTenSecondsAndCancels",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingHeldBodyAimActivatesAfterContactAndReleasesWithoutAFlip",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.RepeatedPunchRequestsAlternateBuffersAndExtendAgainWithoutASnap",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordIsContinuousAtThirtyHertz",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordIsContinuousAtSixtyHertz",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ShippingDualMouseChordIsContinuousAtOneTwentyHertz");

        private static void Run(TestMode mode, string report, params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mode, report, fixtures });
    }
}
