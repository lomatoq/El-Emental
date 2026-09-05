using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class AirborneMantleTestLauncher
    {
        [MenuItem("Elemental/QA/Airborne Mantle Admission Edit Tests")]
        public static void RunEdit() => Run(
            TestMode.EditMode,
            "AirborneMantleEdit",
            "Elemental.Tests.EditMode.EarthAirborneMantleAdmissionTests");

        [MenuItem("Elemental/QA/Airborne Moving Platform Mantle Play Test")]
        public static void RunPlay() => Run(
            TestMode.PlayMode,
            "AirborneMantlePlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.AirborneForwardJumpCatchesAndMantlesRisingPlatformWithRealHands");

        private static void Run(TestMode mode, string report, params string[] fixtures)
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod(
                "Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[] { mode, report, fixtures });
        }
    }
}
