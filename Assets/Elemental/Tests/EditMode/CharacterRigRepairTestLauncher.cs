using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class CharacterRigRepairTestLauncher
    {
        [MenuItem("Elemental/QA/Character Rig Repair Edit")]
        public static void Edit() => Run(TestMode.EditMode, "CharacterRigRepairEdit",
            "Elemental.Tests.EditMode.SecondaryBoneSpringSolverTests",
            "Elemental.Tests.EditMode.LinebreakerSecondaryMotionAssetTests");

        [MenuItem("Elemental/QA/Character Rig Repair Play")]
        public static void Play() => Run(TestMode.PlayMode, "CharacterRigRepairPlay",
            "Elemental.Tests.PlayMode.HumanoidSecondaryMotionRuntimeTests");

        private static void Run(TestMode mode, string report, params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mode, report, fixtures });
    }
}
