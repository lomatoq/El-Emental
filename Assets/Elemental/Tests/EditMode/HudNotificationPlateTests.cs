using System.Reflection;
using Elemental.Presentation.UI;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Tests.EditMode
{
    public sealed class HudNotificationPlateTests
    {
        [TestCase(EarthLifeResult.Won)]
        [TestCase(EarthLifeResult.Lost)]
        public void NotificationsUseWholeCreamPlateAndReturningIsCenteredTextOnly(EarthLifeResult outcome)
        {
            var go=new GameObject("Notification plate contract");go.SetActive(false);
            var theme=ScriptableObject.CreateInstance<ElementalUITheme>();
            try
            {
                theme.stoneSkin=AssetDatabase.LoadAssetAtPath<ElementalStoneSkin>("Assets/Elemental/Content/UI/Stone/ElementalStoneSkin.asset");
                Assert.That(theme.stoneSkin,Is.Not.Null);
                var hud=go.AddComponent<EarthDuelHud>();
                var life=new Label();var returning=new Label();
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(EarthDuelHud).GetField("_theme",flags).SetValue(hud,theme);
                typeof(EarthDuelHud).GetField("_lifeResultLabel",flags).SetValue(hud,life);
                typeof(EarthDuelHud).GetField("_respawn",flags).SetValue(hud,returning);
                // A previously sliced background must be fully reset by the adapter.
                life.style.unitySliceLeft=125;returning.style.unitySliceTop=12;
                typeof(EarthDuelHud).GetMethod("ApplyLifeResultArt",flags).Invoke(hud,new object[]{outcome});
                var original=theme.stoneSkin.roundWinPlate;
                var clone=life.style.backgroundImage.value.sprite;
                Assert.That(clone.texture,Is.EqualTo(original.texture));Assert.That(clone.rect,Is.EqualTo(original.rect));
                Assert.That(clone.border,Is.EqualTo(Vector4.zero));
                typeof(EarthDuelHud).GetMethod("ApplyLifeResultArt",flags).Invoke(hud,new object[]{outcome});
                Assert.That(life.style.backgroundImage.value.sprite,Is.SameAs(clone),"Reuse the owned sprite; never allocate it on every art refresh.");
                Assert.That(returning.style.backgroundImage.value.sprite,Is.Null);
                Assert.That(returning.style.left.value,Is.EqualTo(Length.Percent(50)));
                Assert.That(returning.style.top.value,Is.EqualTo(Length.Percent(50)));
                foreach(var label in new[]{life})
                {
                    Assert.That(label.style.backgroundSize.value,Is.EqualTo(new BackgroundSize(BackgroundSizeType.Contain)));
                    Assert.That(label.style.backgroundRepeat.value,Is.EqualTo(new BackgroundRepeat(Repeat.NoRepeat,Repeat.NoRepeat)));
                    Assert.That(label.style.unitySliceLeft.value+label.style.unitySliceRight.value+label.style.unitySliceTop.value+label.style.unitySliceBottom.value,Is.Zero);
                    Assert.That(label.style.unitySliceScale.value,Is.EqualTo(1));
                }
                typeof(EarthDuelHud).GetMethod("OnDestroy",flags).Invoke(hud,null);
                Assert.That(clone==null,Is.True,"HUD destruction must release its owned sprite.");
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(theme);}
        }
    }
}
