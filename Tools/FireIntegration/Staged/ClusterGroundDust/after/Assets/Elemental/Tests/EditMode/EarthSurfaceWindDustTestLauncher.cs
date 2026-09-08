using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class EarthSurfaceWindDustTestLauncher
    {
        [MenuItem("Elemental/QA/Surface Wind Dust Edit")]
        public static void RunEdit() => Run(TestMode.EditMode, "SurfaceWindDustEdit", "Elemental.Tests.EditMode.EarthSurfaceWindPolicyTests", "Elemental.Tests.EditMode.EarthGroundDustDensityTests");
        [MenuItem("Elemental/QA/Surface Wind Dust Visual Play")]
        public static void RunPlay() => Run(TestMode.PlayMode, "SurfaceWindDustPlay", "Elemental.Tests.PlayMode.EarthSurfaceWindDustProductionTests");
        private static void Run(TestMode mode, string report, params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null,new object[] { mode,report,fixtures });
    }
}
