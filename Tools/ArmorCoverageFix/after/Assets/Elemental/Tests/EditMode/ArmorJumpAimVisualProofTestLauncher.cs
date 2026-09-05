using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class ArmorJumpAimVisualProofTestLauncher
    {
        [MenuItem("Elemental/QA/Armor Jump Aim Visual Proof")]
        public static void Run()
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod(
                "Run",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null)
                throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[]
            {
                TestMode.PlayMode,
                "ArmorJumpAimVisualProofPlay",
                new[]
                {
                    "Elemental.Tests.PlayMode.ArmorJumpAimVisualProofRuntimeTests.PhysicalArmorAndShortJumpProduceFullBodyVisualEvidence"
                }
            });
        }
    }
}
