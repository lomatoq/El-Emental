using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Elemental.Tests.EditMode
{
    public static class EarthStoneDustQaLauncher
    {
        [MenuItem("Elemental/QA/Stone Dust/Edit Policy and Meshes")]
        public static void RunEdit() => Run(TestMode.EditMode,"StoneDustEdit",
            "Elemental.Tests.EditMode.EarthStoneImpactDustTests",
            "Elemental.Tests.EditMode.EarthMaterialPassTests",
            "Elemental.Tests.EditMode.RumbleRockMeshFactoryTests");
        [MenuItem("Elemental/QA/Stone Dust/Play Physical Drops and Captures")]
        public static void RunPhysical() => Run(TestMode.PlayMode,"StonePhysicalDropPlay",
            "Elemental.Tests.PlayMode.EarthStonePhysicalDropProductionTests");
        [MenuItem("Elemental/QA/Stone Dust/Play Adapter Compositing and Mass")]
        public static void RunRegression() => Run(TestMode.PlayMode,"StoneDustRegressionPlay",
            "Elemental.Tests.PlayMode.EarthStoneImpactDustRuntimeTests",
            "Elemental.Tests.PlayMode.EarthDustCompositingRuntimeTests",
            "Elemental.Tests.PlayMode.SharedMassPolicyProductionTests",
            "Elemental.Tests.PlayMode.EarthPhysicsRegressionTests");
        private static void Run(TestMode mode,string report,params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static)
                .Invoke(null,new object[]{mode,report,fixtures});
    }
}
