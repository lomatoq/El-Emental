using Elemental.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Tests.EditMode
{
    public sealed class HudMotionContractTests
    {
        [Test] public void VisibilityReversesWithoutJumpAndCompletesBothDirections()
        {
            var motion=new UiVisibilityMotion();
            float partial=motion.Step(true,.1f,false);
            Assert.That(motion.Step(false,0,false),Is.EqualTo(partial));
            Assert.That(motion.Step(false,.02f,false),Is.LessThan(partial));
            Assert.That(motion.Step(true,1,false),Is.EqualTo(1));
            Assert.That(motion.Step(false,1,false),Is.EqualTo(0));
        }
        [Test] public void ScaleCompositionPreservesAuthoredTransformAndLiveEdits()
        {
            var element=new VisualElement();
            element.style.translate=new Translate(Length.Percent(-50),18);
            element.style.scale=new Scale(new Vector3(1.3f,.8f,1));
            var motion=new ToolkitOpacityScaleMotion(element);
            for(int i=0;i<100;i++)motion.Apply(.6f,.9f);
            Assert.That(element.style.scale.value.value.x,Is.EqualTo(1.17f).Within(.0001f));
            Assert.That(element.style.translate.value.x.unit,Is.EqualTo(LengthUnit.Percent));
            Assert.That(element.style.translate.value.x.value,Is.EqualTo(-50));
            motion.Restore();
            Assert.That(element.style.scale.value.value.y,Is.EqualTo(.8f));
            element.style.scale=new Scale(new Vector3(2,2,1));
            motion.Apply(1,.5f);
            Assert.That(element.style.scale.value.value.x,Is.EqualTo(1));
            motion.Restore();
            Assert.That(element.style.scale.value.value.x,Is.EqualTo(2));
        }
        [Test] public void ReducedVisibilityStillFadesAndSettlesQuickly()
        {
            var element=new VisualElement();
            var adapter=new ToolkitOpacityScaleMotion(element);adapter.Apply(1,.95f);
            Assert.That(element.style.scale.value.value.x,Is.EqualTo(.95f).Within(.0001f));
            var motion=new UiVisibilityMotion();
            Assert.That(motion.Step(true,.02f,true),Is.InRange(.01f,.99f));
            Assert.That(motion.Step(true,.05f,true),Is.EqualTo(1).Within(.0001f));
        }
    }
}
