using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class AnimationSemanticAuditTestLauncher
    {
        [MenuItem("Elemental/QA/Animation Semantic Asset Audit")]
        public static void RunEdit() => Run(
            TestMode.EditMode,
            "AnimationSemanticAssetAuditEdit",
            "Elemental.Tests.EditMode.SeptemberAnimationSemanticAssetAuditTests");

        [MenuItem("Elemental/QA/Animation Semantic Runtime Audit")]
        public static void RunPlay() => Run(
            TestMode.PlayMode,
            "AnimationSemanticRuntimeAuditPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.SemanticMagicSlotsBecomeOneHotAndKeepAValidUpperBodyPose",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.WalkStopKeepsKneesFiniteAndAvoidsAOneFrameLegSnap");

        [MenuItem("Elemental/QA/Animation Semantic Magic Edit Audit")]
        public static void RunMagicEdit() => Run(
            TestMode.EditMode,
            "AnimationSemanticMagicEdit",
            "Elemental.Tests.EditMode.SeptemberAnimationSemanticAssetAuditTests",
            "Elemental.Tests.EditMode.EarthChoreographyTests.VisualPoseConsumesEveryDeclaredChoreographyChannel",
            "Elemental.Tests.EditMode.EarthChoreographyTests.ElevenSemanticSlotsHaveFiniteDistinctBoundedUpperBodySignatures",
            "Elemental.Tests.EditMode.SeptemberAnimationRescueTests.LateStartingAndShortFixedTickPhasesCannotSkipAuthoredContact");

        [MenuItem("Elemental/QA/Animation Semantic Magic Runtime Audit")]
        public static void RunMagicPlay() => Run(
            TestMode.PlayMode,
            "AnimationSemanticMagicPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.SemanticMagicSlotsBecomeOneHotAndKeepAValidUpperBodyPose");

        private static void Run(TestMode mode, string report, params string[] fixtures)
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod(
                "Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[] { mode, report, fixtures });
        }
    }
}
