using System.Reflection;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class EarthLandingRowQa
    {
        public static void Row()=>Run(TestMode.PlayMode,"PillarRowInputPlay",new[]{"Elemental.Tests.PlayMode.PillarRowInputProductionTests"});
        public static void Edit()=>Run(TestMode.EditMode,"LandingRowEdit",new[]{"Elemental.Tests.EditMode.EarthActionRouterTests","Elemental.Tests.EditMode.EarthLandingSlamRoutingTests","Elemental.Tests.EditMode.EarthLandingSlamTests","Elemental.Tests.EditMode.DualMouseEarthGestureSolverTests","Elemental.Tests.EditMode.WallDimensionTopologyTests","Elemental.Tests.EditMode.EarthWallPushMotionTests","Elemental.Tests.EditMode.EarthWallPushRoutingTests","Elemental.Tests.EditMode.EarthCircularRimGestureTests","Elemental.Tests.EditMode.EarthRepairSeamCompletionTests","Elemental.Tests.EditMode.EarthMagicExpansionTests","Elemental.Tests.EditMode.EarthWebWaveAndArmorTests"});
        public static void Play()=>Run(TestMode.PlayMode,"LandingRowAudioPlay",new[]{"Elemental.Tests.PlayMode.PillarRowInputProductionTests","Elemental.Tests.PlayMode.EarthLandingSlamRuntimeTests","Elemental.Tests.PlayMode.FrontendAudioRuntimeTests","Elemental.Tests.PlayMode.LandingSlamKeyboardProductionTests"});
        public static void Slam()=>Run(TestMode.PlayMode,"LandingFinalPlay",new[]{"Elemental.Tests.PlayMode.EarthLandingSlamRuntimeTests","Elemental.Tests.PlayMode.LandingSlamKeyboardProductionTests"});
        public static void Walls()=>Run(TestMode.PlayMode,"WallGeometryPushPlay",new[]{"Elemental.Tests.PlayMode.WallIntactPresentationProductionTests","Elemental.Tests.PlayMode.EarthWallHeldPushPlayTests","Elemental.Tests.PlayMode.EarthWallPushKeyboardProductionTests"});
        public static void RepairPush()=>Run(TestMode.PlayMode,"RepairPushPlay",new[]{"Elemental.Tests.PlayMode.RepairMouseProductionTests","Elemental.Tests.PlayMode.EarthWallPushKeyboardProductionTests"});
        public static void AllPlay()=>Run(TestMode.PlayMode,"EarthInteractionFinalPlay",new[]{"Elemental.Tests.PlayMode.PillarRowInputProductionTests","Elemental.Tests.PlayMode.EarthLandingSlamRuntimeTests","Elemental.Tests.PlayMode.FrontendAudioRuntimeTests","Elemental.Tests.PlayMode.LandingSlamKeyboardProductionTests","Elemental.Tests.PlayMode.WallIntactPresentationProductionTests","Elemental.Tests.PlayMode.EarthWallHeldPushPlayTests","Elemental.Tests.PlayMode.EarthWallPushKeyboardProductionTests","Elemental.Tests.PlayMode.RepairMouseProductionTests","Elemental.Tests.PlayMode.EarthLandingSlamPlanetRuntimeTests"});
        public static void Power()=>Run(TestMode.PlayMode,"WallPushPowerPlay",new[]{"Elemental.Tests.PlayMode.EarthWallHeldPushPlayTests","Elemental.Tests.PlayMode.EarthWallPushKeyboardProductionTests"});
        private static void Run(TestMode mode,string report,string[] fixtures)=>typeof(Mvp01FocusedTestLauncher).GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{mode,report,fixtures});
    }
}
