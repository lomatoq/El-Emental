from pathlib import Path
p=Path('Tools/FireIntegration/Staged/ResultsUIPolish/after/Assets/Elemental/Presentation/UI/StoneReferenceHudPresentation.cs')
s=p.read_text()
s=s.replace('private readonly StoneExactHudDecoration _exact;', 'private readonly StoneExactHudDecoration _exact;\n        private readonly StoneResultAtmosphere _resultAtmosphere;\n        private readonly Label _resultMotto;')
s=s.replace('var brand=Art(result,', '''_resultAtmosphere=new StoneResultAtmosphere { name="reference-result-atmosphere",pickingMode=PickingMode.Ignore }; result.Add(_resultAtmosphere);Fill(_resultAtmosphere);
            var logo=Art(result,"reference-result-logo",theme.logo,0,36,64,64);Center(logo,64,36);
            var alpha=Text("LOCAL ALPHA / 01",13,theme.text);result.Add(alpha);Center(alpha,300,105);alpha.style.unityTextAlign=TextAnchor.MiddleCenter;alpha.style.letterSpacing=3;
            var brand=Art(result,''')
s=s.replace('_skin.wordmark,0,50,350,90); Center(brand,350,50);','_skin.wordmark,0,135,350,90); Center(brand,350,135);')
s=s.replace('Center(_halo,550,150); _halo.style.opacity=.3f;', 'Center(_halo,550,150); _halo.style.display=DisplayStyle.None;')
s=s.replace('_skin.earth,0,250,180,180); Center(_emblem,180,250);','_skin.earth,0,275,185,155); Center(_emblem,185,275);')
s=s.replace('Center(title,900,424); title.style.height=130; title.style.fontSize=112;', 'Center(title,1100,414); title.style.height=170; title.style.fontSize=142;')
s=s.replace('Center(restart,580,686); restart.style.height=98;', 'Center(restart,590,715); restart.style.height=96;')
s=s.replace('Center(back,530,810); back.style.height=84;', 'Center(back,550,835); back.style.height=84;')
s=s.replace('_resultVisual=new VisualElement', '''_resultMotto=Text("Every fall builds a higher rise.",16,theme.text);_resultMotto.name="reference-result-motto";result.Add(_resultMotto);
            _resultMotto.style.position=Position.Absolute;_resultMotto.style.right=55;_resultMotto.style.bottom=60;_resultMotto.style.letterSpacing=1;
            _resultVisual=new VisualElement''')
s=s.replace('child!=_scrim&&!(child is Button)', 'child!=_scrim&&child!=_resultAtmosphere&&!(child is Button)')
s=s.replace('_title.style.fontSize=112;', '_title.style.fontSize=142;')
s=s.replace('ApplyReadable(_score);','_score.style.unityFontStyleAndWeight=FontStyle.Normal;')
s=s.replace('foreach(var button in _resultButtons)button.Tick(reduced,dt);','foreach(var button in _resultButtons)button.Tick(reduced,dt,localWon||draw);')
s=s.replace('_emblem.style.unityBackgroundImageTintColor=accent;', '_emblem.style.unityBackgroundImageTintColor=accent;\n            _title.style.textShadow=new TextShadow { offset=Vector2.zero,blurRadius=localWon?12:9,color=new Color(accent.r,accent.g,accent.b,.42f) };\n            _resultMotto.style.display=localWon||draw?DisplayStyle.None:DisplayStyle.Flex;')
s=s.replace('_scrim.style.backgroundColor=localWon||draw?new Color(.02f,.045f,.06f,.63f):new Color(.13f,.025f,.01f,.73f);', '_scrim.style.backgroundColor=localWon||draw?new Color(.02f,.045f,.06f,.14f):new Color(.13f,.025f,.01f,.34f);\n            _resultAtmosphere.Tick(localWon||draw,reduced,_resultAge);')
s=s.replace('"NEW ROUND":"RETRY ROUND"', '"REMATCH":"RETRY"')
s=s.replace('restart.style.backgroundImage=new StyleBackground(localWon||draw?_skin.primary:_skin.danger);','restart.style.backgroundImage=StyleKeyword.None;')
s=s.replace('private readonly VisualElement _visual,_art;', 'private readonly VisualElement _visual,_art,_icon,_arrow;\n            private readonly StoneResultButtonEdge _edge;')
s=s.replace('_caption=Text(button.text,28,theme.text);', '''_edge=new StoneResultButtonEdge { name="reference-result-button-edge",pickingMode=PickingMode.Ignore };_visual.Add(_edge);Fill(_edge);
                _icon=Art(_visual,"reference-result-button-icon",button.name=="restart-round"?skin.earth:skin.backIcon,28,20,60,50);
                _arrow=Art(_visual,"reference-result-button-arrow",skin.playIcon,500,29,22,32);_arrow.style.left=StyleKeyword.Auto;_arrow.style.right=28;
                _caption=Text(button.text,28,theme.text);''')
s=s.replace('public void Tick(bool reduced,float dt)\n            {','public void Tick(bool reduced,float dt,bool victory)\n            {')
s=s.replace('var sprite=selected?_skin.referenceSelected:_skin.referenceNormal;\n                var insets=selected?_skin.referenceSelectedInsets:Vector4.zero;', 'bool primary=_button.name=="restart-round";\n                var sprite=primary?(victory?_skin.primary:_skin.danger):_skin.referenceNormal;\n                var insets=Vector4.zero;\n                Color accent=victory?new Color(.8f,1,.38f):new Color(1,.48f,.24f);\n                _edge.Set(accent,primary,selected);\n                _icon.style.unityBackgroundImageTintColor=primary?accent:_theme.text;')
s=s.replace('_caption.style.color=selected?new Color(.04f,.055f,.055f):_theme.text;', '_caption.style.color=_theme.text;_caption.style.unityFontStyleAndWeight=selected?FontStyle.Bold:FontStyle.Normal;\n                _caption.style.textShadow=new TextShadow { offset=Vector2.zero,blurRadius=selected?5:0,color=new Color(accent.r,accent.g,accent.b,.35f) };')
p.write_text(s)
