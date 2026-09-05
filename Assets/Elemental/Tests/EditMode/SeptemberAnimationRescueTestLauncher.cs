using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class SeptemberAnimationRescueTestLauncher
    {
        [MenuItem("Elemental/QA/September Animation Edit Tests")]
        public static void RunEdit() => Run(TestMode.EditMode, "SeptemberAnimationEdit", "Elemental.Tests.EditMode.SeptemberAnimationRescueTests");
        [MenuItem("Elemental/QA/September Animation Play Tests")]
        public static void RunPlay() => Run(TestMode.PlayMode, "SeptemberAnimationPlay", "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests");
        [MenuItem("Elemental/QA/September Actual Surface Foot Tests")]
        public static void RunSurface() => Run(TestMode.PlayMode, "SeptemberSurfacePlay", "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.FinalHumanoidFeetTraverseRealPitHumpAndSlopeAtControlledThirtySixtyOneTwentySteps");
        [MenuItem("Elemental/QA/Animation Foot Support Edit Tests")]
        public static void RunFootSupport() => Run(TestMode.EditMode, "EarthFootSupportAuthorityEdit", "Elemental.Tests.EditMode.EarthFootSupportAuthorityIntegrationTests");
        [MenuItem("Elemental/QA/September Mantle Animation Play Tests")]
        public static void RunMantle() => Run(TestMode.PlayMode, "SeptemberMantleAnimationPlay", new[]
        {
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionMantleOwnsPoseReleasesFeetAndReturnsToContacts",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionMantleNativeHandFallbackUsesBaseLayer"
        });
        [MenuItem("Elemental/QA/September Mantle Native Play Test")]
        public static void RunMantleNative() => Run(TestMode.PlayMode, "SeptemberMantleNativePlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionMantleNativeHandFallbackUsesBaseLayer");
        [MenuItem("Elemental/QA/September Mantle Rig Play Test")]
        public static void RunMantleRig() => Run(TestMode.PlayMode, "SeptemberMantleRigPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProductionMantleOwnsPoseReleasesFeetAndReturnsToContacts");
        private static void Run(TestMode mode, string report, string fixture)
            => Run(mode, report, new[] { fixture });
        private static void Run(TestMode mode, string report, string[] fixtures)
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[] { mode, report, fixtures });
        }
    }
}
