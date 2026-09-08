using Elemental.Simulation.Magic;
using UnityEngine;
using UnityEngine.UIElements;
namespace Elemental.Presentation.UI
{
    // Decoration only. The caller supplies real selected element, scores and end-of-round state.
    public sealed class StoneReferenceHudPresentation
    {
        private readonly VisualElement _root, _wheel, _result, _halo, _emblem, _scrim, _resultVisual;
        private readonly VisualElement[] _tokenGlow=new VisualElement[4];
        private readonly VisualElement[] _tokens = new VisualElement[4];
        private readonly Label _score, _title;
        private readonly ElementalStoneSkin _skin;
        private readonly ElementalStoneReferenceProfile _profile;
        private readonly StoneExactHudDecoration _exact;
        private readonly StoneResultAtmosphere _resultAtmosphere;
        private readonly Label _resultMotto;
        private readonly ElementId[] _elements={ElementId.Fire,ElementId.Water,ElementId.Air,ElementId.Earth};
        private bool _wasOver;
        private float _resultAge, _elementAge;
        private ElementId _selected;
        private readonly System.Collections.Generic.Dictionary<VisualElement,StyleEnum<Visibility>> _combatVisibility=new System.Collections.Generic.Dictionary<VisualElement,StyleEnum<Visibility>>();
        public StoneReferenceHudPresentation(VisualElement root, ElementalUITheme theme, VisualElement result, Label title, Button restart, System.Action menu)
        {
            _root=root; _skin=theme.stoneSkin; _profile=_skin.referenceProfile; _result=result; _title=title;
            Art(root,"reference-hud-wordmark",_skin.wordmark,30,22,280,74);
            var clock=root.Q("round-clock"); clock.style.backgroundColor=Color.clear;
            clock.style.borderTopWidth=clock.style.borderBottomWidth=clock.style.borderLeftWidth=clock.style.borderRightWidth=0;
            clock.style.width=280; clock.style.height=117; clock.style.paddingTop=0;
            var clockCaption=clock.Q<Label>(className:"duel-caption");
            if(clockCaption!=null){clockCaption.style.position=Position.Absolute;clockCaption.style.top=20;clockCaption.style.left=0;clockCaption.style.width=Length.Percent(100);clockCaption.style.height=22;}
            var clockTime=clock.Q<Label>(className:"duel-time");
            if(clockTime!=null){clockTime.style.position=Position.Absolute;clockTime.style.top=42;clockTime.style.left=0;clockTime.style.width=Length.Percent(100);clockTime.style.height=58;clockTime.style.unityTextAlign=TextAnchor.MiddleCenter;}
            clock.style.backgroundImage=new StyleBackground(_skin.timerPlate);
            clock.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);
            var top=root.Q("duel-scoreboard"); top.style.backgroundImage=StyleKeyword.None; top.style.backgroundColor=Color.clear;
            foreach(var side in new[]{"red-team","blue-team"})
            {
                var team=root.Q(side); team.style.width=100; team.style.marginTop=12; team.style.paddingBottom=12;
                team.style.borderBottomColor=theme.accent;team.style.borderBottomWidth=1;
                var ornament=new ReferenceOrnament(ReferenceOrnament.Kind.ScoreWing,side=="blue-team"){name="reference-score-wing",pickingMode=PickingMode.Ignore};
                team.Add(ornament);Fill(ornament);
            }
            foreach(var role in new[]{"health-gauge","mana-gauge"})
            {
                var caps=new ReferenceOrnament(ReferenceOrnament.Kind.GaugeCaps,role=="mana-gauge"){name="reference-gauge-caps",pickingMode=PickingMode.Ignore};
                root.Q(role).Add(caps);Fill(caps);
            }
            var globe=root.Q("planet-globe");
            // Existing globe mesh and navigation data remain live behind the transparent ornament.
            var frame=Art(globe,"reference-map-frame",_skin.minimap,0,0,0,0); Fill(frame);
            foreach(var pair in new[]{("N",.5f,.02f),("S",.5f,.91f),("W",.04f,.47f),("E",.92f,.47f)})
            {
                var label=Text(pair.Item1,14,theme.text); globe.Add(label); label.style.position=Position.Absolute;
                label.style.left=Length.Percent(pair.Item2*100); label.style.top=Length.Percent(pair.Item3*100);
            }
            AddVitalCaption(root.Q("health-under-bar"),"HP",theme);
            AddVitalCaption(root.Q("energy-under-bar"),"MP",theme);
            var pause=root.Q("pause-match"); pause.style.backgroundColor=new Color(.035f,.065f,.09f,.7f);
            pause.style.borderTopWidth=pause.style.borderBottomWidth=pause.style.borderLeftWidth=pause.style.borderRightWidth=1;
            pause.style.borderTopColor=pause.style.borderBottomColor=pause.style.borderLeftColor=pause.style.borderRightColor=theme.accent;
            _wheel=new VisualElement { name="reference-element-wheel",pickingMode=PickingMode.Ignore }; root.Add(_wheel);
            _wheel.style.position=Position.Absolute; _wheel.style.left=Length.Percent(50); _wheel.style.bottom=60;
            _wheel.style.width=480; _wheel.style.height=210; _wheel.style.translate=new Translate(-240,0);
            var arc=new ReferenceOrnament(ReferenceOrnament.Kind.WheelArc,false){name="reference-wheel-arc",pickingMode=PickingMode.Ignore};_wheel.Add(arc);Fill(arc);
            Sprite[] glyphs={_skin.fire,_skin.water,_skin.air,_skin.earth};
            Vector2[] places={new Vector2(42,79),new Vector2(210,8),new Vector2(378,79),new Vector2(193,95)};
            for(int i=0;i<4;i++)
            {
                float size=i==3?94:60; _tokens[i]=Art(_wheel,"element-"+_elements[i],_skin.ring,places[i].x,places[i].y,size,size);
                _tokens[i].style.backgroundColor=new Color(.02f,.04f,.045f,.88f);
                _tokens[i].style.borderTopLeftRadius=_tokens[i].style.borderTopRightRadius=_tokens[i].style.borderBottomLeftRadius=_tokens[i].style.borderBottomRightRadius=Length.Percent(50);
                _tokenGlow[i]=Art(_tokens[i],"selection-glow",_skin.halo,-size*.2f,-size*.2f,size*1.4f,size*1.4f);
                _tokenGlow[i].style.unityBackgroundImageTintColor=new Color(.76f,1,.42f);_tokenGlow[i].style.opacity=0;
                Art(_tokens[i],"glyph",glyphs[i],size*.2f,size*.2f,size*.6f,size*.6f);
                var label=Text(_elements[i].ToString().ToUpperInvariant(),i==3?18:13,theme.text); _tokens[i].Add(label);
                label.style.position=Position.Absolute; label.style.top=Length.Percent(100); label.style.left=-20; label.style.width=size+40;
                label.style.unityTextAlign=TextAnchor.MiddleCenter; label.style.letterSpacing=2;
            }
            // Use the existing end-of-round node and its existing restart callback/authority gate.
            Fill(result); result.style.translate=new Translate(0,0); result.style.paddingTop=result.style.paddingBottom=result.style.paddingLeft=result.style.paddingRight=0;
            result.style.backgroundImage=StyleKeyword.None; result.style.backgroundColor=Color.clear;
            result.style.borderTopWidth=result.style.borderBottomWidth=0;
            result.style.borderBottomLeftRadius=result.style.borderBottomRightRadius=0;
            foreach(var child in result.Children()) if(child!=title&&child!=restart) child.style.display=DisplayStyle.None;
            _scrim=new VisualElement { name="reference-result-scrim",pickingMode=PickingMode.Ignore }; result.Insert(0,_scrim); Fill(_scrim);
            _resultAtmosphere=new StoneResultAtmosphere { name="reference-result-atmosphere",pickingMode=PickingMode.Ignore }; result.Add(_resultAtmosphere);Fill(_resultAtmosphere);
            var logo=Art(result,"reference-result-logo",theme.logo,0,36,64,64);Center(logo,64,36);
            var alpha=Text("LOCAL ALPHA / 01",13,theme.text);result.Add(alpha);Center(alpha,300,105);alpha.style.unityTextAlign=TextAnchor.MiddleCenter;alpha.style.letterSpacing=3;
            var brand=Art(result,"reference-result-brand",_skin.wordmark,0,135,350,90); Center(brand,350,135);
            _halo=Art(result,"reference-result-halo",_skin.halo,0,150,550,550); Center(_halo,550,150); _halo.style.display=DisplayStyle.None;
            _emblem=Art(result,"reference-result-emblem",_skin.earth,0,275,185,155); Center(_emblem,185,275);
            Center(title,1100,414); title.style.height=170; title.style.fontSize=142; title.style.unityFontStyleAndWeight=FontStyle.Normal;
            title.style.unityTextAlign=TextAnchor.MiddleCenter; title.style.letterSpacing=6; title.style.marginTop=title.style.marginBottom=0;
            var divider=Art(result,"reference-result-divider",_skin.divider,0,560,650,30); Center(divider,650,560);
            _score=Text("",58,theme.text); _score.name="reference-result-score"; result.Add(_score); Center(_score,500,586);
            _score.style.unityTextAlign=TextAnchor.MiddleCenter; _score.style.letterSpacing=6;
            Center(restart,590,715); restart.style.height=96; StyleButton(restart,_skin.primary,theme);
            var back=new Button(()=>menu?.Invoke()){text="BACK TO MENU",name="reference-result-menu"}; result.Add(back);
            Center(back,550,835); back.style.height=84; StyleButton(back,_skin.normal,theme);
            _resultMotto=Text("Every fall builds a higher rise.",16,theme.text);_resultMotto.name="reference-result-motto";result.Add(_resultMotto);
            _resultMotto.style.position=Position.Absolute;_resultMotto.style.right=55;_resultMotto.style.bottom=60;_resultMotto.style.letterSpacing=1;
            _resultVisual=new VisualElement{name="reference-result-motion",pickingMode=PickingMode.Ignore};
            var decorative=new System.Collections.Generic.List<VisualElement>();
            foreach(var child in result.Children())if(child!=_scrim&&child!=_resultAtmosphere&&!(child is Button))decorative.Add(child);
            result.Add(_resultVisual);Fill(_resultVisual);_resultVisual.style.transformOrigin=new TransformOrigin(Length.Percent(50),Length.Percent(50));
            foreach(var child in decorative)_resultVisual.Add(child);
            // Button hit rectangles stay outside the moving visual group.
            root.Query<TextElement>().ForEach(e=>{ if(_profile.hudFont!=null){e.style.unityFont=_profile.hudFont;e.style.unityFontDefinition=FontDefinition.FromFont(_profile.hudFont);} });
            if(_profile.exactHud!=null && _profile.exactHud.enabled)
                _exact=new StoneExactHudDecoration(root,_skin,_profile);
            foreach(var child in root.Children()) if(child!=result) _combatVisibility.Add(child,child.style.visibility);
        }
        public void Refresh()
        {
            var top=_root.Q("duel-scoreboard");top.style.backgroundImage=StyleKeyword.None;top.style.backgroundColor=Color.clear;
            _root.Query<TextElement>().ForEach(e=>{
                if(_profile.hudFont!=null){e.style.unityFont=_profile.hudFont;e.style.unityFontDefinition=FontDefinition.FromFont(_profile.hudFont);}
                if(e.ClassListContains("duel-score"))e.style.fontSize=46;
                else if(e.ClassListContains("duel-time")){e.style.fontSize=45;e.style.letterSpacing=4;}
                else if(e.ClassListContains("duel-caption")){e.style.fontSize=14;e.style.letterSpacing=2;}
                else if(e.ClassListContains("duel-vital-value"))e.style.fontSize=30;
                else if(e.ClassListContains("duel-legend"))e.style.fontSize=12;
                if(!_result.Contains(e))ApplyReadable(e);
            });
            _title.style.fontSize=142; _score.style.fontSize=58;
            _score.style.unityFontStyleAndWeight=FontStyle.Normal;
            _root.Q<Button>("restart-round").style.fontSize=28;
            _root.Q<Button>("reference-result-menu").style.fontSize=28;
            for(int i=0;i<_tokens.Length;i++)
            { var label=_tokens[i].Q<Label>(); if(label!=null)label.style.fontSize=i==3?18:13; }
            _result.style.backgroundImage=StyleKeyword.None;_result.style.backgroundColor=Color.clear;
            _exact?.Refresh();
        }
        private void ApplyReadable(TextElement element)
        {
            if(_profile.hudReadableFont!=null){element.style.unityFont=_profile.hudReadableFont;element.style.unityFontDefinition=FontDefinition.FromFont(_profile.hudReadableFont);}
            element.style.unityFontStyleAndWeight=FontStyle.Bold;
        }
        private sealed class ReferenceOrnament : VisualElement
        {
            public enum Kind { WheelArc, GaugeCaps, ScoreWing }
            private readonly Kind _kind;private readonly bool _mirror;
            public ReferenceOrnament(Kind kind,bool mirror){_kind=kind;_mirror=mirror;generateVisualContent+=Draw;}
            private void Draw(MeshGenerationContext context)
            {
                var p=context.painter2D;p.strokeColor=new Color(1,.88f,.62f,.9f);p.fillColor=new Color(1,.91f,.71f);p.lineWidth=1.2f;
                float w=contentRect.width,h=contentRect.height;if(w<=0||h<=0)return;
                if(_kind==Kind.WheelArc)
                {
                    p.BeginPath();p.MoveTo(new Vector2(36,186));p.BezierCurveTo(new Vector2(130,38),new Vector2(350,38),new Vector2(444,186));p.Stroke();
                    foreach(float x in new[]{36f,444f}){p.BeginPath();p.Arc(new Vector2(x,186),3.5f,0,360);p.Fill();}
                }
                else if(_kind==Kind.ScoreWing)
                {
                    float edge=_mirror?w:0,inner=_mirror?0:w;
                    p.BeginPath();p.MoveTo(new Vector2(edge,h-1));p.LineTo(new Vector2(inner,h-1));p.LineTo(new Vector2(inner,h-12));p.Stroke();
                    float tip=_mirror?5:w-5;p.BeginPath();p.MoveTo(new Vector2(tip,h-4));p.LineTo(new Vector2(tip+3,h-1));p.LineTo(new Vector2(tip,h+2));p.LineTo(new Vector2(tip-3,h-1));p.ClosePath();p.Fill();
                }
                else
                {
                    for(int end=0;end<2;end++)
                    {
                        float y=end==0?5:h-5,sign=end==0?-1:1;
                        Vector2 Point(float x,float dy)=>new Vector2((_mirror?58-x:x)*w/58,y+dy*sign);
                        p.BeginPath();p.MoveTo(Point(8,0));p.LineTo(Point(11,3));p.LineTo(Point(16,0));p.LineTo(Point(21,3));p.LineTo(Point(27,0));p.LineTo(Point(30,3));p.Stroke();
                        foreach(float x in new[]{8f,30f}){p.BeginPath();p.Arc(Point(x,0),1.6f,0,360);p.Fill();}
                    }
                }
            }
        }
        public void Tick(ElementId selected, bool reduced, bool over, bool localWon, bool draw, int localScore, int opponentScore, bool restartAllowed)
        {
            _exact?.Tick(selected,reduced);
            float dt=Time.unscaledDeltaTime;
            foreach(var button in _resultButtons)button.Tick(reduced,dt,localWon||draw);
            // Real round-end presentation takes precedence over per-life toast and combat ornaments.
            // Keep their data, authored layout and display state; restore original visibility on restart.
            foreach(var pair in _combatVisibility) pair.Key.style.visibility=over?new StyleEnum<Visibility>(Visibility.Hidden):pair.Value;
            if(selected!=_selected){_selected=selected;_elementAge=0;} _elementAge+=dt;
            for(int i=0;i<4;i++)
            {
                bool active=_elements[i]==selected;
                float selectT=_elementAge/_profile.Duration("element_select",.22f,reduced);
                float glow=selectT<1?_profile.Sample("element_select","glow",selectT):reduced?_profile.Sample("selected_breath","opacity",1):_profile.LoopSample("selected_breath","opacity",_elementAge);
                _tokenGlow[i].style.opacity=active?glow:0;
                _tokens[i].style.opacity=active?1:.92f;
                _tokens[i].style.unityBackgroundImageTintColor=active?new Color(.85f,1,.55f,1):Color.white;
                float scale=active&&!reduced?_profile.Sample("element_select","scale",selectT,1):1;
                _tokens[i].style.scale=new Scale(new Vector3(scale,scale,1));
            }
            if(over&&!_wasOver)_resultAge=0; _wasOver=over; if(!over)return; _resultAge+=dt;
            _title.text=draw?"DRAW":localWon?"VICTORY":"DEFEAT";
            _score.text=localScore+"  -  "+opponentScore;
            Color accent=draw?new Color(.95f,.86f,.66f):localWon?new Color(.85f,1,.55f):new Color(1,.55f,.32f);
            _title.style.color=accent; _emblem.style.unityBackgroundImageTintColor=accent;
            _title.style.textShadow=new TextShadow { offset=Vector2.zero,blurRadius=localWon?12:9,color=new Color(accent.r,accent.g,accent.b,.42f) };
            _resultMotto.style.display=localWon||draw?DisplayStyle.None:DisplayStyle.Flex;
            _halo.style.unityBackgroundImageTintColor=accent;
            _scrim.style.backgroundColor=localWon||draw?new Color(.02f,.045f,.06f,.14f):new Color(.13f,.025f,.01f,.34f);
            _resultAtmosphere.Tick(localWon||draw,reduced,_resultAge);
            string resultTrack=localWon?"success":"defeat";
            float t=_resultAge/_profile.Duration(resultTrack,localWon?.48f:.34f,reduced);
            _result.style.opacity=_profile.Sample(resultTrack,"alpha",t,1);
            _resultVisual.style.translate=new Translate(0,reduced?0:_profile.Sample(resultTrack,"y",t));
            float resultScale=reduced?1:_profile.Sample(resultTrack,"scale",t,1);
            _resultVisual.style.scale=new Scale(new Vector3(resultScale,resultScale,1));
            _halo.style.opacity=reduced?_profile.Sample("selected_breath","opacity",1):_profile.LoopSample("selected_breath","opacity",_resultAge);
            var restart=_root.Q<Button>("restart-round"); restart.text=restartAllowed?(localWon||draw?"REMATCH":"RETRY"):"WAITING FOR HOST";
            restart.style.backgroundImage=StyleKeyword.None;
        }
        private static void AddVitalCaption(VisualElement parent,string value,ElementalUITheme theme)
        { var label=Text(value,14,theme.text); label.name="reference-"+value;parent.Add(label);label.style.position=Position.Absolute;label.style.top=66;label.style.width=Length.Percent(100);label.style.unityTextAlign=TextAnchor.MiddleCenter;label.style.letterSpacing=3; }
        private static Label Text(string value,float size,Color color)=>new Label(value){pickingMode=PickingMode.Ignore,style={fontSize=size,color=color}};
        private static void Fill(VisualElement e){e.style.position=Position.Absolute;e.style.left=0;e.style.top=0;e.style.right=0;e.style.bottom=0;e.style.width=StyleKeyword.Auto;e.style.height=StyleKeyword.Auto;}
        private static void Center(VisualElement e,float width,float y){e.style.position=Position.Absolute;e.style.left=Length.Percent(50);e.style.top=y;e.style.width=width;e.style.translate=new Translate(-width*.5f,0);}
        private static VisualElement Art(VisualElement parent,string name,Sprite sprite,float x,float y,float width,float height)
        { var e=new VisualElement{name=name,pickingMode=PickingMode.Ignore};parent.Add(e);e.style.position=Position.Absolute;e.style.left=x;e.style.top=y;e.style.width=width;e.style.height=height;e.style.backgroundImage=new StyleBackground(sprite);e.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);e.style.backgroundRepeat=new BackgroundRepeat(Repeat.NoRepeat,Repeat.NoRepeat);return e; }
        private readonly System.Collections.Generic.List<ReferenceResultButton> _resultButtons=new System.Collections.Generic.List<ReferenceResultButton>();
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
            private readonly VisualElement _visual,_art,_icon,_arrow;
            private readonly StoneResultButtonEdge _edge;
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
                _edge=new StoneResultButtonEdge { name="reference-result-button-edge",pickingMode=PickingMode.Ignore };_visual.Add(_edge);Fill(_edge);
                _icon=Art(_visual,"reference-result-button-icon",button.name=="restart-round"?skin.earth:skin.backIcon,28,20,60,50);
                _arrow=Art(_visual,"reference-result-button-arrow",skin.playIcon,500,29,22,32);_arrow.style.left=StyleKeyword.Auto;_arrow.style.right=28;
                _caption=Text(button.text,28,theme.text);_caption.name="reference-button-caption";_caption.style.unityTextAlign=TextAnchor.MiddleCenter;_caption.style.letterSpacing=3;_visual.Add(_caption);Fill(_caption);
                button.RegisterCallback<PointerEnterEvent>(_=>_hover=true);button.RegisterCallback<PointerLeaveEvent>(_=>{_hover=false;_press=false;});
                button.RegisterCallback<FocusInEvent>(_=>_focus=true);button.RegisterCallback<FocusOutEvent>(_=>{_focus=false;_press=false;});
                button.RegisterCallback<PointerDownEvent>(_=>_press=true);button.RegisterCallback<PointerUpEvent>(_=>_press=false);
            }
            public void Tick(bool reduced,float dt,bool victory)
            {
                bool enabled=_button.enabledInHierarchy,selected=enabled&&(_hover||_focus||_press);
                bool primary=_button.name=="restart-round";
                var sprite=primary?(victory?_skin.primary:_skin.danger):_skin.referenceNormal;
                var insets=Vector4.zero;
                Color accent=victory?new Color(.8f,1,.38f):new Color(1,.48f,.24f);
                _edge.Set(accent,primary,selected);
                _icon.style.unityBackgroundImageTintColor=primary?accent:_theme.text;
                float height=_button.resolvedStyle.height;if(!float.IsFinite(height)||height<1)height=84;
                float nativeFace=sprite.rect.height-insets.y-insets.w,k=height/nativeFace;
                _art.style.backgroundImage=new StyleBackground(sprite);
                _art.style.left=-insets.x*k;_art.style.bottom=-insets.y*k;_art.style.right=-insets.z*k;_art.style.top=-insets.w*k;
                var border=sprite.border;_art.style.unitySliceLeft=(int)border.x;_art.style.unitySliceBottom=(int)border.y;_art.style.unitySliceRight=(int)border.z;_art.style.unitySliceTop=(int)border.w;_art.style.unitySliceScale=k;
                _art.style.opacity=enabled?1:.55f;
                _caption.text=_button.text;_caption.style.color=_theme.text;_caption.style.unityFontStyleAndWeight=selected?FontStyle.Bold:FontStyle.Normal;
                _caption.style.textShadow=new TextShadow { offset=Vector2.zero,blurRadius=selected?5:0,color=new Color(accent.r,accent.g,accent.b,.35f) };
                _button.style.color=Color.clear;
                float target=reduced?1:_press?.984f:selected?1.04f:1;
                _scale=reduced?1:Mathf.Lerp(_scale,target,1-Mathf.Exp(-dt*18));_visual.style.scale=new Scale(Vector3.one*_scale);
            }
        }
    }
}
