using Elemental.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public class StoneHudExactLayoutTests
    {
        [Test] public void EverySelectedElementHasSeparateVisualEnvelope()
        {
            for(int active=0;active<4;active++)
                for(int i=0;i<4;i++)for(int j=i+1;j<4;j++)
                    Assert.That(StoneHudExactProfile.ElementVisualRect(i,i==active).Overlaps(StoneHudExactProfile.ElementVisualRect(j,j==active)),Is.False);
        }
        [Test] public void ExactLayoutKeepsGameplayResultAndTransformsIndependent()
        {
            var layout=ScriptableObject.CreateInstance<ElementalHudLayout>();
            try
            {
                var originalResult=layout.lifeResult;StoneHudExactProfile.ApplyLayout(layout);
                Assert.That(layout.lifeResult,Is.SameAs(originalResult));
                Assert.That(layout.health.group.anchor.x,Is.EqualTo(0));Assert.That(layout.energy.group.anchor.x,Is.EqualTo(1));
                Assert.That(layout.navigation.group.anchor,Is.EqualTo(Vector2.one));
                Assert.That(layout.pause.button.size.x/StoneHudExactProfile.UiScale,Is.GreaterThanOrEqualTo(44));
                Assert.That(layout.pause.button.size.y/StoneHudExactProfile.UiScale,Is.GreaterThanOrEqualTo(44));
                var firstSize=layout.navigation.globe.size;StoneHudExactProfile.ApplyLayout(layout);Assert.That(layout.navigation.globe.size,Is.EqualTo(firstSize));
            }
            finally{Object.DestroyImmediate(layout);}
        }
    }
}
