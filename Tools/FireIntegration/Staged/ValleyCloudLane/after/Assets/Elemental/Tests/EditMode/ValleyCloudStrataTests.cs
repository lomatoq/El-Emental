using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public static class ValleyCloudTestLauncher
    {
        [UnityEditor.MenuItem("Elemental/QA/Run Valley Cloud Tests")]
        public static void Run()
        {
            var run=typeof(Mvp01FocusedTestLauncher).GetMethod("Run",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            run.Invoke(null,new object[]{UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,"ValleyCloudEdit",new[]{"Elemental.Tests.EditMode.ValleyCloudStrataTests"}});
        }
    }
    public sealed class ValleyCloudStrataTests
    {
        [TestCase(12f)][TestCase(55.1f)][TestCase(80f)]
        public void VolumeCeilingAlwaysClearsPlanetBottom(float radius)
        {
            var scale=ValleyCloudStrata.VolumeScale(radius);
            var centre=ValleyCloudStrata.VolumeCentre(radius);
            Assert.That(centre.y+scale.y*0.5f,Is.EqualTo(-radius-15).Within(0.001f));
            Assert.That(scale.x,Is.GreaterThanOrEqualTo(7000));
            Assert.That(scale.z,Is.GreaterThanOrEqualTo(7000));
        }
    }
}
