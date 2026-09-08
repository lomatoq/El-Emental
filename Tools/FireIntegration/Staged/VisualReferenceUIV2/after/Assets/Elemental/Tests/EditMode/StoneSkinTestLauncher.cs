using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class StoneSkinTestLauncher
    {
        [MenuItem("Elemental/QA/Stone Skin Edit")]
        public static void Edit() => RunEdit("StoneSkinEdit", "Elemental.Tests.EditMode.StoneSkinContractTests");
        [MenuItem("Elemental/QA/Stone Skin Installer Idempotence")]
        public static void Idempotence() => RunEdit("StoneSkinInstallerIdempotence", "Elemental.Tests.EditMode.StoneSkinInstallerIdempotenceTests");
        private static void RunEdit(string report, string fixture) => typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { TestMode.EditMode, report, new[] { fixture } });
        [MenuItem("Elemental/QA/Stone Reference Profile Edit")]
        public static void ReferenceEdit() => RunEdit("StoneReferenceProfileEdit", "Elemental.Tests.EditMode.StoneReferenceProfileTests");
        [MenuItem("Elemental/QA/Stone Skin Play")]
        public static void Play() => typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { TestMode.PlayMode, "StoneSkinPlay", new[] {
                "Elemental.Tests.PlayMode.StoneSkinPlayTests", "Elemental.Tests.PlayMode.HudLayoutPlayTests",
                "Elemental.Tests.PlayMode.FeelFollowupUiPlayTests" } });
    }
}
