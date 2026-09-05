using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class SeismicVisionTestLauncher
    {
        [MenuItem("Elemental/QA/Seismic Vision Edit Audit")]
        public static void RunEdit() => Run(TestMode.EditMode, "SeismicVisionEdit", "Elemental.Tests.EditMode.EarthSeismicPerceptionTests");
        [MenuItem("Elemental/QA/Seismic Vision Runtime Audit")]
        public static void RunPlay() => Run(TestMode.PlayMode, "SeismicVisionPlay",
            "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests.SeismicVisionRevealsNightGeometryAndImmediatelyLosesAirborneSupport");
        private static void Run(TestMode mode, string report, string fixture)
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[] { mode, report, new[] { fixture } });
        }
    }
}
