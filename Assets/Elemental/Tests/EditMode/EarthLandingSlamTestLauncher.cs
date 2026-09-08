using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class EarthLandingSlamTestLauncher
    {
        [MenuItem("Elemental/QA/Landing Slam Edit")]
        public static void Edit() => Run(TestMode.EditMode, "LandingSlamEdit", "Elemental.Tests.EditMode.EarthLandingSlamTests");
        [MenuItem("Elemental/QA/Landing Slam Play")]
        public static void Play() => Run(TestMode.PlayMode, "LandingSlamPlay", "Elemental.Tests.PlayMode.EarthLandingSlamRuntimeTests");
        private static void Run(TestMode mode, string report, params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mode, report, fixtures });
    }
}
