using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class EarthWallDepthTestLauncher
    {
        [MenuItem("Elemental/QA/Wall Depth Partition Edit")]
        public static void Run() => typeof(Mvp01FocusedTestLauncher).GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static)
            .Invoke(null,new object[] {TestMode.EditMode,"WallDepthPartitionEdit",new[] {"Elemental.Tests.EditMode.EarthWallDepthPartitionTests"}});
    }
}
