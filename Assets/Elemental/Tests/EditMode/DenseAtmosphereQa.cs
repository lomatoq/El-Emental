using System.Reflection;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class DenseAtmosphereQa
    {
        public static void WispEdit()=>Run(TestMode.EditMode,"CurvedDustEdit",new[]{"Elemental.Tests.EditMode.SurfaceDustMeshTests","Elemental.Tests.EditMode.WallcoeurDustIntegrationTests"});
        public static void WispPlay()=>Run(TestMode.PlayMode,"CurvedDustPlay",new[]{"Elemental.Tests.PlayMode.EarthSurfaceWindDustProductionTests","Elemental.Tests.PlayMode.EarthDustCompositingRuntimeTests"});
        public static void Edit()=>Run(TestMode.EditMode,"DenseAtmosphereEdit",new[]{"Elemental.Tests.EditMode.WallcoeurDustIntegrationTests","Elemental.Tests.EditMode.EarthSurfaceWindPolicyTests","Elemental.Tests.EditMode.EarthGroundDustDensityTests","Elemental.Tests.EditMode.ProceduralCloudBanksTests","Elemental.Tests.EditMode.ArenaColumnFireTests","Elemental.Tests.EditMode.HudMotionContractTests","Elemental.Tests.EditMode.SidebarRevealContractTests"});
        public static void Play()=>Run(TestMode.PlayMode,"DenseAtmospherePlay",new[]{"Elemental.Tests.PlayMode.EarthDustCompositingRuntimeTests","Elemental.Tests.PlayMode.EarthSurfaceWindDustProductionTests","Elemental.Tests.PlayMode.FireLabPresentationTests","Elemental.Tests.PlayMode.HudMotionPlayTests","Elemental.Tests.PlayMode.FrontendMotionContinuityTests","Elemental.Tests.PlayMode.MenuLayoutProductionTests"});
        private static void Run(TestMode mode,string report,string[] fixtures)=>typeof(Mvp01FocusedTestLauncher).GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{mode,report,fixtures});
    }
}
