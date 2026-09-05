using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class PausedChoreographyTestLauncher
    {
        [MenuItem("Elemental/QA/Paused Choreography Runtime Audit")]
        public static void Run()
        {
            MethodInfo run = typeof(Mvp01FocusedTestLauncher).GetMethod(
                "Run", BindingFlags.NonPublic | BindingFlags.Static);
            if (run == null)
                throw new System.MissingMethodException("Mvp01FocusedTestLauncher.Run");
            run.Invoke(null, new object[]
            {
                TestMode.PlayMode,
                "PausedChoreographyPlay",
                new[]
                {
                    "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests." +
                    "PausedGameTimeDoesNotReapplyActiveChoreographyToFrozenBones",
                    "Elemental.Tests.PlayMode.SeptemberAnimationRescueRuntimeTests." +
                    "PausedGameTimeKeepsTheCompleteProductionPoseChainStable"
                }
            });
        }
    }
}
