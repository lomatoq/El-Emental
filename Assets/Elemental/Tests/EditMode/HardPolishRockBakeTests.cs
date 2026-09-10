using System.Collections.Generic;
using Elemental.Authoring.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Elemental.Tests.EditMode
{
    public sealed class HardPolishRockBakeTests
    {
        [Test] public void AllTwentySourceAndTwelvePhysicsCandidatesPassBeforePublication()
        {
            HardPolishRockBake.Report report=HardPolishRockBake.ValidateCandidateLibrary();
            Assert.That(report.entries.Count,Is.EqualTo(32));
            var guids=new HashSet<string>();int physics=0,maximumPhysicsTriangles=0;
            foreach(var entry in report.entries)
            {
                Assert.That(AssetDatabase.AssetPathToGUID(entry.path),Is.EqualTo(entry.guidBefore));
                Assert.That(guids.Add(entry.guidBefore),Is.True);
                if(!entry.path.Contains("/Physics/"))continue;
                physics++;maximumPhysicsTriangles=System.Math.Max(maximumPhysicsTriangles,entry.triangles);
                Assert.That(entry.triangles,Is.LessThanOrEqualTo(255));
            }
            Assert.That(physics,Is.EqualTo(12));
            TestContext.WriteLine($"Preflight sources=20 physics=12 maxPhysicsTriangles={maximumPhysicsTriangles}; no asset writes.");
        }
    }
}
