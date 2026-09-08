using System.Reflection;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class BotStartTransitionQa
    {
        public static void Run()=>typeof(Mvp01FocusedTestLauncher).GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{TestMode.PlayMode,"BotStartTransitionPlay",new[]{"Elemental.Tests.PlayMode.MenuLayoutProductionTests","Elemental.Tests.PlayMode.FrontendMotionContinuityTests"}});
    }
}
