using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class DustFlowFollowupQa
    {
        public static void Edit()=>Run(TestMode.EditMode,"DustFlowFollowupEdit",new[]{"Elemental.Tests.EditMode.WallcoeurDustIntegrationTests","Elemental.Tests.EditMode.EarthSurfaceWindPolicyTests","Elemental.Tests.EditMode.EarthGroundDustDensityTests","Elemental.Tests.EditMode.MenuLayoutContractTests"});
        public static void Play()=>Run(TestMode.PlayMode,"DustFlowFollowupPlay",new[]{"Elemental.Tests.PlayMode.EarthDustCompositingRuntimeTests","Elemental.Tests.PlayMode.EarthSurfaceWindDustProductionTests","Elemental.Tests.PlayMode.MenuLayoutProductionTests","Elemental.Tests.PlayMode.FrontendMotionContinuityTests"});
        private static void Run(TestMode mode,string report,string[] fixtures)=>typeof(Mvp01FocusedTestLauncher).GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{mode,report,fixtures});
    }
}
