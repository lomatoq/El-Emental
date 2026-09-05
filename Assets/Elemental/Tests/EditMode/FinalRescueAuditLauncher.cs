using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class FinalRescueAuditLauncher
    {
        [MenuItem("Elemental/QA/Final Pose Ownership And Seismic Audit")]
        public static void RunFinalOwnership() => Run(TestMode.PlayMode, "FinalPoseOwnershipSeismicPlay", new[]
        {
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProtectedMantleRejectsLateMagicAndDoesNotReplayItAfterLanding",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.ProtectedRagdollRecoveryRejectsLateMagicUntilCompletion",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.PausedGameTimeDoesNotReapplyActiveChoreographyToFrozenBones",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.PausedGameTimeKeepsTheCompleteProductionPoseChainStable",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.SeismicVisionRevealsNightGeometryAndImmediatelyLosesAirborneSupport"
        });

        [MenuItem("Elemental/Experimental/SONIC/6 Validate G1 Skeleton Math")]
        public static void RunSonicMath() => Run(TestMode.EditMode, "SonicG1SkeletonEdit", new[]
        {
            "Elemental.Experimental.SonicPrototype.Tests.SonicG1SkeletonTests",
            "Elemental.Experimental.SonicPrototype.Tests.EditMode.SonicHumanoidRetargetMathTests",
            "Elemental.Experimental.SonicPrototype.Tests.SonicPlannerTimelineTests"
        });

        [MenuItem("Elemental/QA/Cross Element Magic Command Ingress Audit")]
        public static void RunCrossElement() => Run(TestMode.PlayMode, "CrossElementMagicIngressPlay", new[]
        {
            "Elemental.Tests.PlayMode.CrossElementMagicIngressTests"
        });

        private static void Run(TestMode mode, string report, string[] tests)
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[] { mode, report, tests });
        }
    }
}
