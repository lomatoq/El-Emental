using Elemental.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
namespace Elemental.Tests.EditMode
{
    public sealed class MenuLayoutContractTests
    {
        [Test] public void LayoutComposesWithAnimationWithoutDriftAndResetsSize()
        {
            var go=new GameObject("Animated menu",typeof(RectTransform));var p=ScriptableObject.CreateInstance<MenuScreenLayout>();
            try
            {
                var r=(RectTransform)go.transform;r.anchoredPosition=new Vector2(30,40);r.sizeDelta=new Vector2(300,70);r.localRotation=Quaternion.Euler(0,0,8);
                var s=new MenuElementLayout{path="Root",offset=new Vector2(10,-5),size=new Vector2(400,90),rotation=4};p.elements.Add(s);
                var b=new MenuLayoutBinding(r,"Root");for(int i=0;i<100;i++)b.Apply(p);
                Assert.That(r.anchoredPosition,Is.EqualTo(new Vector2(40,35)));Assert.That(r.localEulerAngles.z,Is.EqualTo(12).Within(.001));
                r.anchoredPosition=new Vector2(50,60);b.Apply(p);Assert.That(r.anchoredPosition,Is.EqualTo(new Vector2(60,55)));
                s.offset=Vector2.zero;s.size=Vector2.zero;s.rotation=0;b.Apply(p);
                Assert.That(r.anchoredPosition,Is.EqualTo(new Vector2(50,60)));Assert.That(r.sizeDelta,Is.EqualTo(new Vector2(300,70)));
                Assert.That(r.localEulerAngles.z,Is.EqualTo(8).Within(.001));
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(p);}
        }
        [Test] public void ToolkitButtonLayoutMovesTheHitTargetAndKeepsItEnabled()
        {
            var p=ScriptableObject.CreateInstance<MenuScreenLayout>();var button=new Button();
            try
            {
                button.style.width=300;button.style.height=70;button.style.translate=new Translate(-150,0);button.style.paddingTop=7;
                p.elements.Add(new MenuElementLayout{path="button",offset=new Vector2(12,20),size=new Vector2(420,96),padding=new Vector4(32,20,32,20)});
                var b=new MenuLayoutBinding(button,"result/button");b.Apply(p);b.Apply(p);
                Assert.That(button.style.width.value.value,Is.EqualTo(420));Assert.That(button.style.translate.value.x.value,Is.EqualTo(-138));
                Assert.That(button.style.paddingTop.value.value,Is.EqualTo(20));Assert.That(button.enabledSelf,Is.True);
                p.elements[0].padding=Vector4.one*-1;b.Apply(p);
                Assert.That(button.style.paddingTop.value.value,Is.EqualTo(7));
            }
            finally{Object.DestroyImmediate(p);}
        }
        [Test] public void AllProductionMenusHaveDistinctEditableProfiles()
        {
            var theme=AssetDatabase.LoadAssetAtPath<ElementalUITheme>("Assets/Elemental/Content/UI/Frontend/ElementalUITheme.asset");
            Assert.That(theme.menuPresentation,Is.Not.Null);
            foreach(MenuScreenId id in System.Enum.GetValues(typeof(MenuScreenId)))Assert.That(theme.menuPresentation.Get(id),Is.Not.Null,id.ToString());
            Assert.That(theme.menuPresentation.Get(MenuScreenId.Victory),Is.Not.SameAs(theme.menuPresentation.Get(MenuScreenId.Defeat)));
        }
    }
}
