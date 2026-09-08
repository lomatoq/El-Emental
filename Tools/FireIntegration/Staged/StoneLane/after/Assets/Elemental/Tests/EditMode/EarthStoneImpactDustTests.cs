using System.Reflection;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthStoneImpactDustTests
    {
        [Test]
        public void HeavyShortDropExceedsLightTapButRemainsBounded()
        {
            Assert.That(EarthStoneImpactDust.Strength(1000f, .6f), Is.GreaterThan(EarthStoneImpactDust.Strength(2f, .6f) * 3f));
            Assert.That(EarthStoneImpactDust.Strength(float.MaxValue, float.MaxValue), Is.LessThanOrEqualTo(2.4f));
            Assert.That(EarthStoneImpactDust.Strength(1000f, 2f), Is.GreaterThan(EarthStoneImpactDust.Strength(1000f, .6f)));
        }
        [TestCase(1000000f, .24f)]
        [TestCase(1000f, -2f)]
        [TestCase(0f, 2f)]
        [TestCase(float.NaN, 2f)]
        [TestCase(1000f, float.PositiveInfinity)]
        public void RestingSeparatingAndInvalidInputsCannotEmit(float mass, float speed)
        { Assert.That(EarthStoneImpactDust.Strength(mass, speed), Is.Zero); }
        [Test]
        public void SizeDistributionHasManyFinesAndFewLargeChips()
        {
            int fines=0, large=0;
            for(int i=0;i<1000;i++)
            {
                float sample=EarthStoneImpactDust.FineBiasedSize((i+.5f)/1000f);
                if(sample<.25f) fines++;
                if(sample>.75f) large++;
            }
            Assert.That(fines, Is.InRange(620,640)); Assert.That(large, Is.InRange(85,100));
        }
        [Test]
        public void CosmeticReplayDependsOnCueNotPreviousEvents()
        {
            uint first=EarthStoneImpactDust.CueSeed(17,2,EarthMaterialFeedbackKind.Impact,new float3(1,2,3));
            EarthStoneImpactDust.CueSeed(992,77,EarthMaterialFeedbackKind.Fracture,new float3(90));
            Assert.That(EarthStoneImpactDust.CueSeed(17,2,EarthMaterialFeedbackKind.Impact,new float3(1,2,3)),Is.EqualTo(first));
            Assert.That(EarthStoneImpactDust.CueSeed(17,3,EarthMaterialFeedbackKind.Impact,new float3(1,2,3)),Is.Not.EqualTo(first));
        }
        [Test]
        public void CooldownSeparatesSourcesAndGenerationsAndResetsOnDisable()
        {
            var go=new GameObject("Impact dust cooldown");
            try
            {
                var hub=go.AddComponent<EarthMaterialFeedbackHub>(); int events=0;
                hub.Presented+=_=>events++;
                hub.EmitStoneImpact(Vector3.zero,Vector3.up,1000,.6f,1,17,1); hub.FlushPending();
                hub.EmitStoneImpact(Vector3.zero,Vector3.up,1000,.6f,1,17,1); hub.FlushPending();
                Assert.That(events,Is.EqualTo(1));
                hub.EmitStoneImpact(Vector3.right*5,Vector3.up,1000,.6f,1,18,1); hub.FlushPending();
                hub.EmitStoneImpact(Vector3.zero,Vector3.up,1000,.6f,1,17,2); hub.FlushPending();
                Assert.That(events,Is.EqualTo(3));
                go.SetActive(false); go.SetActive(true);
                hub.EmitStoneImpact(Vector3.zero,Vector3.up,1000,.6f,1,17,2); hub.FlushPending();
                Assert.That(events,Is.EqualTo(4));
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void CosmeticLibraryHasDistinctCenteredNormalizedValidSilhouettes()
        {
            var type=typeof(EarthMaterialFeedbackPresenter).Assembly.GetType("Elemental.Presentation.VFX.EarthCosmeticChipLibrary",true);
            var meshes=(Mesh[])type.GetMethod("Build",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[] { null });
            try
            {
                Assert.That(meshes.Length,Is.EqualTo(4));
                for(int i=0;i<meshes.Length;i++)
                {
                    Assert.That(RumbleRockMeshFactory.Validate(meshes[i],out string reason),Is.True,reason);
                    Assert.That(meshes[i].bounds.center.magnitude,Is.LessThan(.0001f));
                    Vector3 s=meshes[i].bounds.size;
                    Assert.That(Mathf.Max(s.x,Mathf.Max(s.y,s.z)),Is.EqualTo(1f).Within(.0001f));
                    if(i>0) Assert.That((meshes[i].bounds.size-meshes[i-1].bounds.size).sqrMagnitude,Is.GreaterThan(.000001f));
                }
            }
            finally { foreach(var mesh in meshes) Object.DestroyImmediate(mesh); }
        }
    }
}
