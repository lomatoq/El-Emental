using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class StartupSceneMeshTestLauncher
    {
        [MenuItem("Elemental/QA/Run Startup Scene Mesh EditMode Test")]
        public static void Run()
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod(
                "Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[]
            {
                TestMode.EditMode,
                "StartupSceneMeshEdit",
                new[] { "Elemental.Tests.EditMode.OuterRingPersistentPieceMeshTests" }
            });
        }
    }
}
