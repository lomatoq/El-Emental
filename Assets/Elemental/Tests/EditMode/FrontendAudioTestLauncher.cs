using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class FrontendAudioTestLauncher
    {
        [MenuItem("Elemental/QA/Frontend Audio Edit")]
        public static void Edit() => Run(TestMode.EditMode, "FrontendAudioEdit", "Elemental.Tests.EditMode.FrontendAudioEnvelopeTests");
        [MenuItem("Elemental/QA/Frontend Audio Play")]
        public static void Play() => Run(TestMode.PlayMode, "FrontendAudioPlay", "Elemental.Tests.PlayMode.FrontendAudioRuntimeTests");
        private static void Run(TestMode mode, string report, params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mode, report, fixtures });
    }
}
