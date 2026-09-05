using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class EarthArmorCoverageTestLauncher
    {
        [MenuItem("Elemental/QA/Armor Coverage/Edit Contract")]
        public static void RunEdit() => Run(
            TestMode.EditMode,
            "ArmorCoverageEdit",
            "Elemental.Tests.EditMode.EarthArmorCoverageShellTests");

        [MenuItem("Elemental/QA/Armor Coverage/Animated Production Shell")]
        public static void RunPlay() => Run(
            TestMode.PlayMode,
            "ArmorCoveragePlay",
            "Elemental.Tests.PlayMode.EarthArmorCoverageRuntimeTests.CompactArmorKeepsNeckShouldersAndTorsoClosedWhileWalkingAndTurning");

        private static void Run(TestMode mode, string report, string filter)
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod(
                "Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null) throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[] { mode, report, new[] { filter } });
        }
    }
}
