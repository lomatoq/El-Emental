using Elemental.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class SidebarRevealContractTests
    {
        [Test]
        public void RevealWaitsForPanelThenStaggersWithoutOvershoot()
        {
            Assert.That(SidebarRevealNode.Progress(.1f,.14f,.26f),Is.Zero);
            float first=SidebarRevealNode.Progress(.2f,.14f,.26f);
            float second=SidebarRevealNode.Progress(.2f,.185f,.26f);
            Assert.That(first,Is.GreaterThan(second));
            Assert.That(second,Is.GreaterThan(0));
            Assert.That(SidebarRevealNode.Progress(5,.14f,.26f),Is.EqualTo(1));
        }

        [Test]
        public void RepeatedRevealAndLiveLayoutEditsNeverBecomeAuthoredCoordinates()
        {
            var go=new GameObject("Authored staircase button",typeof(RectTransform));
            var profile=ScriptableObject.CreateInstance<MenuScreenLayout>();
            try
            {
                var rect=(RectTransform)go.transform;
                rect.anchoredPosition=new Vector2(60,-180);
                rect.localScale=new Vector3(1.2f,.9f,1);
                var entry=new MenuElementLayout{path="Root",offset=new Vector2(13,7)};
                profile.elements.Add(entry);
                var binding=new MenuLayoutBinding(rect,"Root");
                var node=new SidebarRevealNode(rect,2);
                var settings=new SidebarRevealSettings();
                for(int i=0;i<100;i++)
                {
                    node.RemoveDelta();
                    if(i==40){entry.offset=new Vector2(-27,23);profile.Revision++;}
                    binding.Apply(profile);
                    node.Apply((i%20)*.02f,.14f,settings,false);
                }
                node.RemoveDelta();binding.Apply(profile);
                Assert.That(rect.anchoredPosition,Is.EqualTo(new Vector2(33,-157)));
                Assert.That(rect.localScale,Is.EqualTo(new Vector3(1.2f,.9f,1)));
                node.Apply(1,0,settings,false);
                Assert.That(node.Alpha,Is.EqualTo(1));
                Assert.That(rect.anchoredPosition,Is.EqualTo(new Vector2(33,-157)));
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(profile);}
        }

        [Test]
        public void ReducedMotionKeepsCoordinatesAndSkipsStagger()
        {
            var go=new GameObject("Reduced motion",typeof(RectTransform));
            try
            {
                var rect=(RectTransform)go.transform;rect.anchoredPosition=new Vector2(120,-360);
                var node=new SidebarRevealNode(rect,4);
                node.Apply(.08f,.2f,new SidebarRevealSettings(),true);
                Assert.That(node.Alpha,Is.EqualTo(1));
                Assert.That(rect.anchoredPosition,Is.EqualTo(new Vector2(120,-360)));
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
