from pathlib import Path
lane=Path('Tools/FireIntegration/Staged/ReferenceSpriteFidelity/after');ui=lane/'Assets/Elemental/Presentation/UI'
p=lane/'Assets/Elemental/Authoring/Editor/StoneUI/ElementalStoneSkinInstaller.cs';s=p.read_text(encoding='utf-8');s=s.replace('skin.referenceCurtain=PrepareReferenceCurtain();','skin.referenceCurtain=PrepareReferenceCurtain();\n            StoneReferenceSpriteInstaller.Apply(skin);');p.write_text(s,encoding='utf-8')
p=ui/'FrontendMenuView.cs';s=p.read_text(encoding='utf-8').replace('GetComponent<Image>().color=colors[i]','GetComponent<Image>().color=Color.white');s=s.replace('var footerDiamond=Image(footerBlock,"Element footer diamond",_theme.accent); footerDiamond.raycastTarget=false;','var footerDiamond=StoneReferenceGlyph.Create(footerBlock,"Element footer diamond",StoneReferenceGlyph.Shape.DiamondGlow,_theme.accent);').replace('footerDiamond.sprite=_theme.stoneSkin.diamond; footerDiamond.preserveAspect=true;','');s='\n'.join(line for line in s.splitlines() if 'var core=Image(footerDiamond.transform' not in line)+'\n';p.write_text(s,encoding='utf-8')
p=ui/'StoneReferenceHudPresentation.cs';s=p.read_text(encoding='utf-8');s=s.replace('public sealed class StoneReferenceHudPresentation','public sealed class StoneReferenceHudPresentation')
# Insert own runtime bindings without changing true Button.text, callbacks or restart permission.
pos=s.index('        private static void StyleButton(')
s=s[:pos]+'''        private readonly System.Collections.Generic.List<ReferenceResultButton> _resultButtons=new System.Collections.Generic.List<ReferenceResultButton>();
        private void StyleButton(Button b,Sprite sprite,ElementalUITheme theme)
        {
            if(_skin.referenceNormal==null){b.style.backgroundImage=new StyleBackground(sprite);return;}
            _resultButtons.Add(new ReferenceResultButton(b,_skin,theme));
        }
        private sealed class ReferenceResultButton
        {
            private readonly Button _button;
            private readonly ElementalStoneSkin _skin;
            private readonly ElementalUITheme _theme;
            private readonly VisualElement _visual,_art;
            private readonly Label _caption;
            private bool _hover,_focus,_press;
            private float _scale=1;
            public ReferenceResultButton(Button button,ElementalStoneSkin skin,ElementalUITheme theme)
            {
                _button=button;_skin=skin;_theme=theme;
                button.style.backgroundImage=StyleKeyword.None;button.style.backgroundColor=Color.clear;
                button.style.color=Color.clear;button.style.borderTopWidth=button.style.borderBottomWidth=button.style.borderLeftWidth=button.style.borderRightWidth=0;
                _visual=new VisualElement{name="reference-button-visual",pickingMode=PickingMode.Ignore};button.Add(_visual);Fill(_visual);
                _art=new VisualElement{name="reference-button-artwork",pickingMode=PickingMode.Ignore};_visual.Add(_art);Fill(_art);
                _art.style.backgroundSize=new BackgroundSize(Length.Percent(100),Length.Percent(100));
                _caption=Text(button.text,28,theme.text);_caption.name="reference-button-caption";_caption.style.unityTextAlign=TextAnchor.MiddleCenter;_caption.style.letterSpacing=3;_visual.Add(_caption);Fill(_caption);
                button.RegisterCallback<PointerEnterEvent>(_=>_hover=true);button.RegisterCallback<PointerLeaveEvent>(_=>{_hover=false;_press=false;});
                button.RegisterCallback<FocusInEvent>(_=>_focus=true);button.RegisterCallback<FocusOutEvent>(_=>{_focus=false;_press=false;});
                button.RegisterCallback<PointerDownEvent>(_=>_press=true);button.RegisterCallback<PointerUpEvent>(_=>_press=false);
            }
            public void Tick(bool reduced,float dt)
            {
                bool enabled=_button.enabledInHierarchy,selected=enabled&&(_hover||_focus||_press);
                var sprite=selected?_skin.referenceSelected:_skin.referenceNormal;
                var insets=selected?_skin.referenceSelectedInsets:Vector4.zero;
                float height=_button.resolvedStyle.height;if(!float.IsFinite(height)||height<1)height=84;
                float nativeFace=sprite.rect.height-insets.y-insets.w,k=height/nativeFace;
                _art.style.backgroundImage=new StyleBackground(sprite);
                _art.style.left=-insets.x*k;_art.style.bottom=-insets.y*k;_art.style.right=-insets.z*k;_art.style.top=-insets.w*k;
                var border=sprite.border;_art.style.unitySliceLeft=(int)border.x;_art.style.unitySliceBottom=(int)border.y;_art.style.unitySliceRight=(int)border.z;_art.style.unitySliceTop=(int)border.w;_art.style.unitySliceScale=k;
                _art.style.opacity=enabled?1:.55f;
                _caption.text=_button.text;_caption.style.color=selected?new Color(.04f,.055f,.055f):_theme.text;
                _button.style.color=Color.clear;
                float target=reduced?1:_press?.984f:selected?1.04f:1;
                _scale=reduced?1:Mathf.Lerp(_scale,target,1-Mathf.Exp(-dt*18));_visual.style.scale=new Scale(Vector3.one*_scale);
            }
        }
'''+s[s.index('    }',pos):]
s=s.replace('float dt=Time.unscaledDeltaTime;','float dt=Time.unscaledDeltaTime;\n            foreach(var button in _resultButtons)button.Tick(reduced,dt);')
p.write_text(s,encoding='utf-8')
p=lane/'Assets/Elemental/Tests/PlayMode/StoneSkinPlayTests.cs';s=p.read_text(encoding='utf-8').replace('Is.EqualTo(2007)','Is.EqualTo(2138)').replace('Is.EqualTo(783)','Is.EqualTo(736)').replace('Is.EqualTo("Press Visual")','Is.EqualTo("Button artwork")').replace('graphic.transform.Find("Button role icon")','graphic.transform.parent.Find("Button role icon")').replace('graphic.transform.localScale','graphic.transform.parent.localScale');s=s.replace('Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(560, 78)));','Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(560, 78)));\n            Assert.That(graphic.transform.parent.Find("Reference role silhouette").GetComponent<StoneReferenceGlyph>().shape,Is.EqualTo(StoneReferenceGlyph.Shape.Target));');s=s.replace('yield return Capture("Pressed");','Assert.That(graphic.rectTransform.offsetMin.x,Is.LessThan(-40),"The whole green construction extends left of the stable cream-face hit rectangle.");\n            Assert.That(graphic.rectTransform.offsetMin.y,Is.LessThan(-40),"The lower green tail must not be cropped to the cream plate.");\n            yield return Capture("Pressed");');p.write_text(s,encoding='utf-8')
