using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class EarthWallPushInputTestLauncher
    {
        [MenuItem("Elemental/QA/Wall Push Input Edit")]
        public static void Edit() => Run(TestMode.EditMode, "WallPushInputEdit", "Elemental.Tests.EditMode.EarthWallPushRoutingTests");
        [MenuItem("Elemental/QA/Wall Push Input Play")]
        public static void Play() => Run(TestMode.PlayMode, "WallPushInputPlay", "Elemental.Tests.PlayMode.EarthWallPushKeyboardProductionTests");
        private static void Run(TestMode mode, string report, params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mode, report, fixtures });
    }
}
