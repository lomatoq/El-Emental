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
        [MenuItem("Elemental/QA/Stone Dust/Fire Convex Followup Edit")]
        public static void RunFireEdit() => Run(TestMode.EditMode,"FireConvexFollowupEdit",
            "Elemental.Tests.EditMode.FireConvexSurfaceTests",
            "Elemental.Tests.EditMode.FireSurfaceResolverTests",
            "Elemental.Tests.EditMode.FireWorldTests");
        [MenuItem("Elemental/QA/Stone Dust/Fire Convex Followup Play")]
        public static void RunFirePlay() => Run(TestMode.PlayMode,"FireConvexFollowupPlay",
            "Elemental.Tests.PlayMode.FireEarthFractureContactTests");
        [MenuItem("Elemental/QA/Stone Dust/Play Contact Dust Compositing")]
        public static void RunContactCompositing() => Run(TestMode.PlayMode,"StoneContactCompositingPlay",
            "Elemental.Tests.PlayMode.EarthStoneImpactDustRuntimeTests",
            "Elemental.Tests.PlayMode.EarthDustCompositingRuntimeTests");
        [MenuItem("Elemental/QA/Stone Dust/Play Current Wall Contracts")]
        public static void RunWallContracts() => Run(TestMode.PlayMode,"StoneCurrentWallContractsPlay",
            "Elemental.Tests.PlayMode.EarthPhysicsRegressionTests.EqualMagicImpulseSlidesSmallWallFartherThanHeavyWall",
            "Elemental.Tests.PlayMode.EarthPhysicsRegressionTests.WallPoolRaisesRectangularCollidersWithoutGrowingTerrainEditCost");
        private static void Run(TestMode mode,string report,params string[] fixtures) =>
            typeof(Mvp01FocusedTestLauncher).GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static)
                .Invoke(null,new object[]{mode,report,fixtures});
    }
}
