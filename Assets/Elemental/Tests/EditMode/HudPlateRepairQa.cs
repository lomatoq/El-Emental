using System.Reflection;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class HudPlateRepairQa
    {
        public static void Edit()=>Run(TestMode.EditMode,"HudPlateRepairEdit",new[]{"Elemental.Tests.EditMode.HudMotionContractTests","Elemental.Tests.EditMode.HudNotificationPlateTests"});
        public static void Play()=>Run(TestMode.PlayMode,"HudPlateRepairPlay",new[]{"Elemental.Tests.PlayMode.FeelFollowupUiPlayTests.ActualLifeLossesDisplayWinLoseAndMutualDrawBeforeRespawn","Elemental.Tests.PlayMode.HudMotionPlayTests"});
        public static void DecorEdit()=>Run(TestMode.EditMode,"DecorFractureFollowupEdit",new[]{"Elemental.Tests.EditMode.EarthDebrisCollisionImpulseTests"});
        public static void DecorPlay()=>Run(TestMode.PlayMode,"DecorFractureFollowupPlay",new[]{"Elemental.Tests.PlayMode.EarthPersistentBreakRuntimeTests"});
        private static void Run(TestMode mode,string report,string[] fixtures)=>typeof(Mvp01FocusedTestLauncher).GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{mode,report,fixtures});
    }
}
