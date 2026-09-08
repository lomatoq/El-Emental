using Elemental.Input.Gestures;
using NUnit.Framework;
using UnityEngine;
namespace Elemental.Tests.EditMode
{
    public sealed class PlatformPreviewContractTests
    {
        [Test] public void ContourUsesReadableWidthAndRendererTintWithoutChangingSharedArt()
        {
            var root=new GameObject("Contour appearance",typeof(LineRenderer),typeof(EarthPreviewPresenter));
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                material.SetColor("_BaseColor",Color.red);
                var line=root.GetComponent<LineRenderer>();line.sharedMaterial=material;line.widthMultiplier=.018f;
                root.GetComponent<EarthPreviewPresenter>().Configure(line);
                var properties=new MaterialPropertyBlock();line.GetPropertyBlock(properties);
                Assert.That(line.widthMultiplier,Is.EqualTo(.065f).Within(.0001f));
                Assert.That(line.sharedMaterial,Is.SameAs(material));
                Assert.That(material.GetColor("_BaseColor"),Is.EqualTo(Color.red));
                Assert.That(properties.GetColor("_BaseColor"),Is.EqualTo(new Color(1.6f,1.25f,.55f,1)));
                line.widthMultiplier=.12f;root.GetComponent<EarthPreviewPresenter>().Configure(line);
                Assert.That(line.widthMultiplier,Is.EqualTo(.12f),"Keep an artist's already wider stroke.");
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(material);}
        }
        [Test] public void RejectedPlatformAreaGivesActionableSizeFeedback()
        {
            Assert.That(EarthPlatformDrawFeedback.ForArea(.2065f,.8f,75),Does.Contain("larger outline"));
            Assert.That(EarthPlatformDrawFeedback.ForArea(90,.8f,75),Does.Contain("smaller outline"));
            Assert.That(EarthPlatformDrawFeedback.ForArea(2,.8f,75),Is.Null);
        }
        [Test] public void DisabledAuthoredContourBecomesVisibleOnlyWhileDrawing()
        {
            var root=new GameObject("Idle production contour",typeof(LineRenderer),typeof(EarthPreviewPresenter));
            try
            {
                var line=root.GetComponent<LineRenderer>();line.enabled=false;
                var presenter=root.GetComponent<EarthPreviewPresenter>();presenter.Configure(line);
                presenter.Present(new[]{Vector3.zero,Vector3.right,Vector3.forward});
                Assert.That(line.enabled,Is.True);Assert.That(presenter.PositionCount,Is.EqualTo(3));
                presenter.Clear();Assert.That(line.enabled,Is.False);Assert.That(presenter.PositionCount,Is.Zero);
                presenter.Present(new[]{Vector3.zero});Assert.That(line.enabled,Is.False);
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
