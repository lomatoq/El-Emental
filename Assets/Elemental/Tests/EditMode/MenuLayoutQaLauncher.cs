using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class MenuLayoutQaLauncher
    {
        [MenuItem("Elemental/QA/Menu Layout Edit")]
        public static void Edit()=>Run(TestMode.EditMode,"MenuLayoutEdit",new[]{"Elemental.Tests.EditMode.MenuLayoutContractTests"});
        [MenuItem("Elemental/QA/Menu Layout Play")]
        public static void Play()=>Run(TestMode.PlayMode,"MenuLayoutPlay",new[]{"Elemental.Tests.PlayMode.MenuLayoutProductionTests","Elemental.Tests.PlayMode.FrontendMotionContinuityTests","Elemental.Tests.PlayMode.FeelFollowupUiPlayTests.ButtonHoldsCenteredPressWithoutChangingAuthoredAnchorOrScale"});
        private static void Run(TestMode mode,string report,string[] fixtures)=>typeof(Mvp01FocusedTestLauncher).GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{mode,report,fixtures});
    }
}
