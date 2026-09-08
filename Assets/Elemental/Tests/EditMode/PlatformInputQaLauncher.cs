using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class PlatformInputQaLauncher
    {
        [MenuItem("Elemental/QA/Platform Input Play")]
        public static void Play()=>Run(TestMode.PlayMode,"PlatformInputPlay",new[]{"Elemental.Tests.PlayMode.PlatformInputProductionTests"});
        [MenuItem("Elemental/QA/Platform Input Edit")]
        public static void Edit()=>Run(TestMode.EditMode,"PlatformInputEdit",new[]{"Elemental.Tests.EditMode.PlatformPreviewContractTests"});
        private static void Run(TestMode mode,string report,string[] fixtures)=>typeof(Mvp01FocusedTestLauncher).GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{mode,report,fixtures});
    }
}
