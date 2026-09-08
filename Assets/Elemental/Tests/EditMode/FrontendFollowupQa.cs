using System.Reflection;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class FrontendFollowupQa
    {
        public static void Clock()=>Run(TestMode.PlayMode,"FrontendLiveClockPlay",new[]{"Elemental.Tests.PlayMode.MenuCameraProductionTests"});
        public static void Edit()=>Run(TestMode.EditMode,"FrontendFollowupEdit",new[]{
            "Elemental.Tests.EditMode.MenuCameraClockContractTests",
            "Elemental.Tests.EditMode.FrontendAudioEnvelopeTests","Elemental.Tests.EditMode.PlatformPreviewContractTests",
            "Elemental.Tests.EditMode.EarthArenaBaselinePolicyTests","Elemental.Tests.EditMode.EarthDuelMatchStateTests"});
        public static void CameraAudio()=>Run(TestMode.PlayMode,"FrontendCameraAudioPlay",new[]{"Elemental.Tests.PlayMode.MenuCameraProductionTests","Elemental.Tests.PlayMode.FrontendAudioRuntimeTests"});
        public static void Play()=>Run(TestMode.PlayMode,"FrontendFollowupPlay",new[]{
            "Elemental.Tests.PlayMode.MenuCameraProductionTests",
            "Elemental.Tests.PlayMode.PlatformInputProductionTests","Elemental.Tests.PlayMode.FrontendAudioRuntimeTests",
            "Elemental.Tests.PlayMode.ProductionArenaRestoreTests","Elemental.Tests.PlayMode.MenuLayoutProductionTests",
            "Elemental.Tests.PlayMode.FrontendMotionContinuityTests",
            "Elemental.Tests.PlayMode.EarthDuelHealthPlayTests.RestartReassertsClosedControlGateWithoutLosingOriginalFlags"});
        private static void Run(TestMode mode,string report,string[] fixtures)=>typeof(Mvp01FocusedTestLauncher)
            .GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{mode,report,fixtures});
    }
}
