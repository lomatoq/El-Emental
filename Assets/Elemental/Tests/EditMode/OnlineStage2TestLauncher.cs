using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class OnlineStage2TestLauncher
    {
        [MenuItem("Elemental/QA/Online Stage2 Edit")]
        public static void Edit() => Run(TestMode.EditMode, "OnlineStage2Edit",
            "Elemental.Tests.EditMode.OnlineProtocolTests",
            "Elemental.Tests.EditMode.OnlineMatchReplicaTests",
            "Elemental.Tests.EditMode.OnlineGeometryTests",
            "Elemental.Tests.EditMode.OnlineSemanticInputTests",
            "Elemental.Tests.EditMode.OnlineActorGraphAuthoringTests",
            "Elemental.Tests.EditMode.OnlinePauseInputTests",
            "Elemental.Online.Tests.OnlineLaunchProfileTests",
            "Elemental.Online.Tests.OnlineArenaIdentityTests");

        [MenuItem("Elemental/QA/Online Stage2 Play")]
        public static void Play() => Run(TestMode.PlayMode, "OnlineStage2Play",
            "Elemental.Tests.PlayMode.OnlineAuthorityGateTests",
            "Elemental.Tests.PlayMode.OnlineSemanticRoutingTests");

        [MenuItem("Elemental/QA/Arena Round Reset Play")]
        public static void ArenaReset() => Run(TestMode.PlayMode, "ArenaRoundResetPlay", "Elemental.Tests.PlayMode.ArenaRoundResetTests");

        private static void Run(TestMode mode, string report, params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { mode, report, fixtures });
    }
}
