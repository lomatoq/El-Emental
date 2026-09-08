using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    // Both UI systems consume the same serialized contract. A binding composes
    // the artist's offset with animation output rather than owning its clock.
    public sealed class MenuLayoutBinding
    {
        private readonly RectTransform rect;
        private readonly VisualElement element;
        private readonly TMP_Text text;
        private readonly TextElement toolkitText;
        private readonly float originalFont;
        private readonly Vector4 originalMargin;
        private StyleLength originalToolkitFont;
        private StyleLength originalPaddingLeft,originalPaddingTop,originalPaddingRight,originalPaddingBottom;
        private bool hadFont;
        public readonly string Path;
        private Vector2 positionBase,positionOut,scaleBase,scaleOut;
        private bool applied;
        private float rotationBase,rotationOut;
        private Vector2 originalSize;
        private bool hadWidth,hadHeight;
        private StyleLength originalWidth,originalHeight;
        private MenuElementLayout setting;
        private MenuScreenLayout loaded;
        private int revision=-1;
        public MenuLayoutBinding(RectTransform target,string path)
        {rect=target;Path=path;text=target.GetComponent<TMP_Text>()??target.GetComponentInChildren<TMP_Text>(true);originalSize=rect.sizeDelta;if(text!=null){originalFont=text.fontSize;originalMargin=text.margin;}}
        public MenuLayoutBinding(VisualElement target,string path)
        {element=target;Path=path;originalWidth=target.style.width;originalHeight=target.style.height;toolkitText=target.Q<Label>("reference-button-caption")??target as TextElement;originalToolkitFont=toolkitText!=null?toolkitText.style.fontSize:target.style.fontSize;originalPaddingLeft=target.style.paddingLeft;originalPaddingTop=target.style.paddingTop;originalPaddingRight=target.style.paddingRight;originalPaddingBottom=target.style.paddingBottom;}
        public void Apply(MenuScreenLayout profile)
        {
            if(profile!=loaded||profile!=null&&revision!=profile.Revision)
            {loaded=profile;revision=profile!=null?profile.Revision:0;setting=profile!=null?profile.Find(Path):null;}
            if(setting==null)return;
            Vector2 p=rect!=null?rect.anchoredPosition:new Vector2(element.style.translate.value.x.value,element.style.translate.value.y.value);
            Vector2 s=rect!=null?(Vector2)rect.localScale:(Vector2)element.style.scale.value.value;
            if(!applied||p!=positionOut)positionBase=p;
            if(!applied||s!=scaleOut)scaleBase=s==Vector2.zero?Vector2.one:s;
            float rotation=rect!=null?rect.localEulerAngles.z:element.style.rotate.value.angle.value;
            if(!applied||Mathf.Abs(Mathf.DeltaAngle(rotation,rotationOut))>.001f)rotationBase=rotation;
            rotationOut=rotationBase+setting.rotation;
            positionOut=positionBase+setting.offset;scaleOut=Vector2.Scale(scaleBase,setting.scale);applied=true;
            if(rect!=null)
            {
                rect.anchoredPosition=positionOut;rect.localScale=new Vector3(scaleOut.x,scaleOut.y,1);
                rect.localRotation=Quaternion.Euler(0,0,rotationOut);
                if(setting.size.x<=0&&hadWidth)rect.sizeDelta=new Vector2(originalSize.x,rect.sizeDelta.y);
                if(setting.size.y<=0&&hadHeight)rect.sizeDelta=new Vector2(rect.sizeDelta.x,originalSize.y);
                if(setting.size.x>0)rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,setting.size.x);
                if(setting.size.y>0)rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,setting.size.y);
                if(text!=null)
                {
                    if(setting.fontSize>0)text.fontSize=setting.fontSize;
                    else if(hadFont)text.fontSize=originalFont;
                    var m=originalMargin;for(int i=0;i<4;i++)if(setting.padding[i]>=0)m[i]=setting.padding[i];text.margin=m;
                }
            }
            else
            {
                element.style.translate=new Translate(positionOut.x,positionOut.y);
                element.style.scale=new Scale(new Vector3(scaleOut.x,scaleOut.y,1));element.style.rotate=new Rotate(rotationOut);
                if(setting.size.x<=0&&hadWidth)element.style.width=originalWidth;
                if(setting.size.y<=0&&hadHeight)element.style.height=originalHeight;
                if(setting.size.x>0)element.style.width=setting.size.x;
                if(setting.size.y>0)element.style.height=setting.size.y;
                if(setting.fontSize>0)(toolkitText??element).style.fontSize=setting.fontSize;
                else if(hadFont)(toolkitText??element).style.fontSize=originalToolkitFont;
                element.style.paddingLeft=setting.padding.x>=0?new StyleLength(setting.padding.x):originalPaddingLeft;
                element.style.paddingTop=setting.padding.y>=0?new StyleLength(setting.padding.y):originalPaddingTop;
                element.style.paddingRight=setting.padding.z>=0?new StyleLength(setting.padding.z):originalPaddingRight;
                element.style.paddingBottom=setting.padding.w>=0?new StyleLength(setting.padding.w):originalPaddingBottom;
            }
            hadWidth=setting.size.x>0;hadHeight=setting.size.y>0;
            hadFont=setting.fontSize>0;
        }
        public static void Collect(RectTransform root,List<MenuLayoutBinding> output,string path="",bool skipPages=false)
        {
            output.Add(new MenuLayoutBinding(root,path.Length==0?"Root":path));
            for(int i=0;i<root.childCount;i++)
            {
                var child=root.GetChild(i) as RectTransform;if(child==null)continue;
                if(child.name=="Press Visual"||child.name.StartsWith("Contour bloom:")||child.name.Contains("glow")||child.name.Contains("glint"))continue;
                if(skipPages&&System.Enum.TryParse<FrontendPage>(child.name,out _))continue;
                Collect(child,output,path.Length==0?child.name:path+"/"+child.name,skipPages);
            }
        }
        public static void Collect(VisualElement root,List<MenuLayoutBinding> output,string path="")
        {
            if(root.name=="result-negative-fringe"||root.name=="result-positive-fringe")return;
            string key=string.IsNullOrEmpty(root.name)?path:path.Length==0?root.name:path+"/"+root.name;
            if(!string.IsNullOrEmpty(root.name))output.Add(new MenuLayoutBinding(root,key));
            foreach(var child in root.Children())Collect(child,output,key);
        }
    }
}
