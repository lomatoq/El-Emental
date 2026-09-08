using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class EarthWallSurfaceTestLauncher
    {
        [MenuItem("Elemental/QA/Wall Surface Edit")]
        public static void Run() => typeof(Mvp01FocusedTestLauncher)
            .GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { TestMode.EditMode, "WallSurfaceEdit", new[] {
                "Elemental.Tests.EditMode.EarthStoneBevelProfileTests" } });
    }
}
