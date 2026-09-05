using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class SeptemberFollowupTestLauncher
    {
        [MenuItem("Elemental/QA/September Followup Edit Audit")]
        public static void Run()
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[] { TestMode.EditMode, "SeptemberFollowupEdit", new[]
            {
                "Elemental.Tests.EditMode.EarthStableArmIkGeometryTests",
                "Elemental.Tests.EditMode.NativeBuildSceneOrderTests",
                "Elemental.Tests.EditMode.EarthSeismicPerceptionTests"
            } });
        }
    }
}
