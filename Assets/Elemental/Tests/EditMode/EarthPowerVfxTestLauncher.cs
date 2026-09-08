using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class EarthPowerVfxTestLauncher
    {
        [MenuItem("Elemental/QA/Power VFX Edit")]
        public static void RunEdit() => Run(TestMode.EditMode, "PowerVfxEdit", "Elemental.Tests.EditMode.EarthChargeVignetteTests");
        [MenuItem("Elemental/QA/Power VFX Play")]
        public static void RunPlay() => Run(TestMode.PlayMode, "PowerVfxPlay", "Elemental.Tests.PlayMode.EarthPowerVfxRuntimeTests");
        [MenuItem("Elemental/QA/Power VFX Production Visual Play")]
        public static void RunProduction() => Run(TestMode.PlayMode, "PowerVfxProductionPlay", "Elemental.Tests.PlayMode.EarthPowerVfxProductionTests");
        [MenuItem("Elemental/QA/Wall Rise Continuity Play")]
        public static void RunWallRise() => Run(TestMode.PlayMode, "WallRiseContinuityPlay", "Elemental.Tests.PlayMode.EarthWallRiseContinuityTests");
        [MenuItem("Elemental/QA/Wall RMB Remote Input Play")]
        public static void RunWallRmb() => Run(TestMode.PlayMode, "WallRmbRemoteInputPlay", "Elemental.Tests.PlayMode.EarthWallRmbInputTests");
        [MenuItem("Elemental/QA/Paired Mouse Gesture Edit")]
        public static void RunPairedMouseEdit() => Run(TestMode.EditMode, "PairedMouseGestureEdit", "Elemental.Tests.EditMode.DualMouseEarthGestureSolverTests");
        private static void Run(TestMode mode, string report, params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mode, report, fixtures });
    }
}
