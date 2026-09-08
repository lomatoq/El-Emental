using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class VisualPolishFollowupLauncher
    {
        public static void ResearchEdit() => Run(TestMode.EditMode,"VisualResearchEdit",new[]{
            "Elemental.Tests.EditMode.WallcoeurDustIntegrationTests",
            "Elemental.Tests.EditMode.EarthStoneImpactDustTests",
            "Elemental.Tests.EditMode.EarthMaterialPassTests",
            "Elemental.Tests.EditMode.FireWorldTests",
            "Elemental.Tests.EditMode.ArenaColumnFireTests"});
        public static void ResearchPlay() => Run(TestMode.PlayMode,"VisualResearchPlay",new[]{
            "Elemental.Tests.PlayMode.FeelFollowupUiPlayTests.ActualLifeLossesDisplayWinLoseAndMutualDrawBeforeRespawn",
            "Elemental.Tests.PlayMode.EarthStoneImpactDustRuntimeTests",
            "Elemental.Tests.PlayMode.EarthDustCompositingRuntimeTests",
            "Elemental.Tests.PlayMode.EarthStonePhysicalDropProductionTests",
            "Elemental.Tests.PlayMode.FireLabPresentationTests",
            "Elemental.Tests.PlayMode.VisualPolishAtmosphereTests"});
        private static void Run(TestMode mode,string report,string[] fixtures) => typeof(Mvp01FocusedTestLauncher).GetMethod("Run",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)
            .Invoke(null,new object[]{mode,report,fixtures});
        [MenuItem("Elemental/QA/Visual Polish Followup Play")]
        public static void Play() => typeof(Mvp01FocusedTestLauncher).GetMethod("Run",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)
            .Invoke(null,new object[]{TestMode.PlayMode,"VisualPolishFollowupPlay",new[]{
                "Elemental.Tests.PlayMode.FeelFollowupUiPlayTests.ActualLifeLossesDisplayWinLoseAndMutualDrawBeforeRespawn",
                "Elemental.Tests.PlayMode.VisualPolishAtmosphereTests"}});
    }
}
