using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class ProductionArenaRestoreTestLauncher
    {
        [MenuItem("Elemental/QA/Production Arena Restore Edit")]
        public static void Edit() => Run(TestMode.EditMode, "ProductionArenaRestoreEdit", "Elemental.Tests.EditMode.EarthArenaBaselinePolicyTests", "Elemental.Tests.EditMode.EarthDuelMatchStateTests");
        [MenuItem("Elemental/QA/Production Arena Restore Play")]
        public static void Play() => Run(TestMode.PlayMode, "ProductionArenaRestorePlay", "Elemental.Tests.PlayMode.ProductionArenaRestoreTests");
        private static void Run(TestMode mode, string report, params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mode, report, fixtures });
    }
}
