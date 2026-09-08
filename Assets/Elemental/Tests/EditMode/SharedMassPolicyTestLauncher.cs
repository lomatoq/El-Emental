using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class SharedMassPolicyTestLauncher
    {
        [MenuItem("Elemental/QA/Shared Mass Policy Edit Tests")]
        public static void Edit() => Run(TestMode.EditMode, "SharedMassPolicyEdit", new[]
        { "Elemental.Tests.EditMode.EarthMassPolicyBindingTests", "Elemental.Tests.EditMode.EarthMatterMassPolicyTests" });
        [MenuItem("Elemental/QA/Shared Mass Policy Production Tests")]
        public static void Play() => Run(TestMode.PlayMode, "SharedMassPolicyPlay", new[]
        { "Elemental.Tests.PlayMode.SharedMassPolicyProductionTests", "Elemental.Tests.PlayMode.EarthPersistentBreakRuntimeTests" });
        private static void Run(TestMode mode, string report, string[] filters)
        {
            MethodInfo method = typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            method.Invoke(null, new object[] { mode, report, filters });
        }
    }
}
