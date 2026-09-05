using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class ArmorJumpAimAnimationTestLauncher
    {
        [MenuItem("Elemental/QA/Armor Jump Aim Animation Edit Tests")]
        public static void Run()
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod(
                "Run",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[]
            {
                TestMode.EditMode,
                "ArmorJumpAimAnimationEdit",
                new[] { "Elemental.Tests.EditMode.ArmorJumpAimAnimationPolicyTests" }
            });
        }

        [MenuItem("Elemental/QA/Armor Jump Aim Animation Play Test")]
        public static void RunPlay()
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod(
                "Run",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[]
            {
                TestMode.PlayMode,
                "ArmorJumpAimAnimationPlay",
                new[]
                {
                    "Elemental.Tests.PlayMode.ArmorJumpAimAnimationRuntimeTests.PhysicalArmorHoldReturnsToWeightedOrdinaryPoseAndShortSpaceUsesJumpLane"
                }
            });
        }
    }
}
