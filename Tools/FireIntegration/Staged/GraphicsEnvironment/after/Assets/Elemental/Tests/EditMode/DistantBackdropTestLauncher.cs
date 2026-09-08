using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class DistantBackdropTestLauncher
    {
        [MenuItem("Elemental/QA/Distant Backdrop Edit")]
        public static void RunEdit() => typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { TestMode.EditMode, "DistantBackdropEdit", new[] { "Elemental.Tests.EditMode.DistantBackdropIntegrationTests" } });
        [MenuItem("Elemental/QA/Distant Backdrop Production Play")]
        public static void RunPlay() => typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { TestMode.PlayMode, "DistantBackdropProductionPlay", new[] { "Elemental.Tests.PlayMode.DistantBackdropProductionTests" } });
    }
}
