using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class GameplayCameraFramingTestLauncher
    {
        [MenuItem("Elemental/QA/Gameplay Camera Framing Edit Tests")]
        public static void RunEdit() => Run(
            TestMode.EditMode,
            "GameplayCameraFramingEdit",
            "Elemental.Tests.EditMode.EarthGameplayCameraFramingTests");

        [MenuItem("Elemental/QA/Gameplay Camera Full Body Visual Proof")]
        public static void RunPlay() => Run(
            TestMode.PlayMode,
            "GameplayCameraFramingPlay",
            "Elemental.Tests.PlayMode.GameplayCameraFramingRuntimeTests.ProductionCameraFramesHeadAndFeetThroughMagicAndReturn");

        private static void Run(TestMode mode, string report, params string[] fixtures)
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod(
                "Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[] { mode, report, fixtures });
        }
    }
}
