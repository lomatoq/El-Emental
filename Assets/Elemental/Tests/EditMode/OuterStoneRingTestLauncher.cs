using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class OuterStoneRingTestLauncher
    {
        [MenuItem("Elemental/QA/Run Outer Stone Ring EditMode Tests")]
        public static void RunEdit() => Run(TestMode.EditMode, "OuterStoneRingEdit", "Elemental.Tests.EditMode.OuterStoneRingTests");

        [MenuItem("Elemental/QA/Run Arena Mesh Picking EditMode Tests")]
        public static void RunPickingEdit() => Run(TestMode.EditMode, "ArenaMeshPickingEdit", "Elemental.Tests.EditMode.EarthArenaMeshPickingTests");

        [MenuItem("Elemental/QA/Run Outer Stone Ring PlayMode Tests")]
        public static void RunPlay() => Run(TestMode.PlayMode, "OuterStoneRingPlay", "Elemental.Tests.PlayMode.OuterStoneRingRuntimeTests");

        private static void Run(TestMode mode, string report, string fixture)
        {
            // Reuse its domain-reload-safe reporting and restoration of the user's saved scene.
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[] { mode, report, new[] { fixture } });
        }
    }
}
