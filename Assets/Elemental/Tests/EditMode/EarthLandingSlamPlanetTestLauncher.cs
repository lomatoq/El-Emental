using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
namespace Elemental.Tests.EditMode
{
    public static class EarthLandingSlamPlanetTestLauncher
    {
        [MenuItem("Elemental/QA/Landing Slam Planet Play")]
        public static void Play() => typeof(Mvp01FocusedTestLauncher).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { TestMode.PlayMode, "LandingSlamPlanetPlay", new[] { "Elemental.Tests.PlayMode.EarthLandingSlamPlanetRuntimeTests" } });
    }
}
