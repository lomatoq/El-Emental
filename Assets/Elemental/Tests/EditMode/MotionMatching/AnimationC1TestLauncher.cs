using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode.MotionMatching
{
    public static class AnimationC1TestLauncher
    {
        [MenuItem("Elemental/QA/Animation C1 Inertialization Edit Audit")]
        public static void Run()
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod(
                "Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[]
            {
                TestMode.EditMode,
                "AnimationC1InertializationEdit",
                new[] { "Elemental.Tests.EditMode.MotionMatching.EarthRotationInertializationTests" }
            });
        }
    }
}
