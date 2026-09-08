using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class ProceduralValleyTestLauncher
    {
        [MenuItem("Elemental/QA/Procedural Valley Geometry Edit")]
        public static void Run()=>typeof(Mvp01FocusedTestLauncher).GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static)
            .Invoke(null,new object[]{TestMode.EditMode,"ProceduralValleyV2Edit",new[]{"Elemental.Tests.EditMode.ProceduralValleyGeometryTests","Elemental.Tests.EditMode.DistantBackdropIntegrationTests"}});
    }
}
