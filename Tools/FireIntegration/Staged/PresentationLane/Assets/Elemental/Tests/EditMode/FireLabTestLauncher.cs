using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class FireLabTestLauncher
    {
        [MenuItem("Elemental/QA/Fire Lab Presentation Play")]
        public static void RunPlay() => typeof(Mvp01FocusedTestLauncher).GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static)
            .Invoke(null,new object[]{TestMode.PlayMode,"FireLabPresentationPlay",new[]{"Elemental.Tests.PlayMode.FireLabPresentationTests"}});
    }
}
