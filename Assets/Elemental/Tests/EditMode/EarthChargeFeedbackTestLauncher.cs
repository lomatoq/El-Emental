using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class EarthChargeFeedbackTestLauncher
    {
        [MenuItem("Elemental/QA/Charge Feedback Edit")]
        public static void RunEdit() => Run(TestMode.EditMode, "ChargeFeedbackEdit",
            "Elemental.Tests.EditMode.EarthChargeFeedbackTests");
        [MenuItem("Elemental/QA/Charge Feedback Play")]
        public static void RunPlay() => Run(TestMode.PlayMode, "ChargeFeedbackPlay",
            "Elemental.Tests.PlayMode.EarthChargeFeedbackRuntimeTests");
        private static void Run(TestMode mode, string report, params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mode, report, fixtures });
    }
}
