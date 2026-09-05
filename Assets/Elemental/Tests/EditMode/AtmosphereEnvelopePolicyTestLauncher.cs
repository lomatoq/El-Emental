using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class AtmosphereEnvelopePolicyTestLauncher
    {
        [MenuItem("Elemental/QA/Run Atmosphere Envelope EditMode Tests")]
        public static void Run()
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod(
                "Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null)
                throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[]
            {
                TestMode.EditMode,
                "AtmosphereEnvelopeEdit",
                new[] { "Elemental.Tests.EditMode.AtmosphereEnvelopePolicyTests" }
            });
        }
    }
}
