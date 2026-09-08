using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class EarthMaterialMicroShakeTestLauncher
    {
        [MenuItem("Elemental/QA/Material Micro Shake Edit")]
        public static void Edit() => Run(TestMode.EditMode, "MaterialMicroShakeEdit", "Elemental.Tests.EditMode.EarthMaterialMicroShakeTests");
        private static void Run(TestMode mode, string report, params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mode, report, fixtures });
    }
}
