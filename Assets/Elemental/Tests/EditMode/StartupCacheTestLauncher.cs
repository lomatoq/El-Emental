using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class StartupCacheTestLauncher
    {
        [MenuItem("Elemental/QA/Run Startup Cache EditMode Tests")]
        public static void RunEdit() => Run(TestMode.EditMode, "StartupCacheEdit", "Elemental.Tests.EditMode.StartupCacheTests");
        [MenuItem("Elemental/QA/Run Startup Cache PlayMode Tests")]
        public static void RunPlay() => Run(TestMode.PlayMode, "StartupCachePlay", "Elemental.Tests.PlayMode.StartupCacheRuntimeTests");
        private static void Run(TestMode mode, string report, string fixture)
        {
            var run = typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[] { mode, report, new[] { fixture } });
        }
    }
}
