using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class DayNightSkyTestLauncher
    {
        [MenuItem("Elemental/QA/Run Day Night EditMode Tests")]
        public static void RunEdit() => Run(TestMode.EditMode, "DayNightSkyEdit", "Elemental.Tests.EditMode.DayNightSkyTests");
        [MenuItem("Elemental/QA/Run Day Night PlayMode Tests")]
        public static void RunPlay() => Run(TestMode.PlayMode, "DayNightSkyPlay", "Elemental.Tests.PlayMode.DayNightSkyRuntimeTests");

        private static void Run(TestMode mode, string report, string fixture)
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[] { mode, report, new[] { fixture } });
        }
    }
}
