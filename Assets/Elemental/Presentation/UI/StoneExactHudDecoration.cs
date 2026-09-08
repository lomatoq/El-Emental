using Elemental.Simulation.Magic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Elemental.Presentation.UI
{
    // Optional reference composition. Owns decoration only; all numeric values, selection,
    // round state and globe navigation continue to come from EarthDuelHud.
    public sealed class StoneExactHudDecoration
    {
        private const float S=StoneHudExactProfile.UiScale;
        private readonly VisualElement _root;
        private readonly ElementalStoneReferenceProfile _reference;
        private readonly StoneHudExactProfile _profile;
        private readonly Diamond[] _diamonds=new Diamond[4];
        private readonly VisualElement[] _glyphs=new VisualElement[4];
        private readonly Label[] _labels=new Label[4];
        private readonly RowRule _rule;
        private ElementId _selected;
        private bool _hasSelection;
        private static readonly ElementId[] Order={ElementId.Fire,ElementId.Earth,ElementId.Water,ElementId.Air};
        private static readonly Color[] Accents={new Color(.95f,.55f,.30f),new Color(.95f,.88f,.61f),new Color(.39f,.72f,.92f),new Color(.79f,.80f,.76f)};
        public StoneExactHudDecoration(VisualElement root,ElementalStoneSkin skin,ElementalStoneReferenceProfile reference)
        {
            _root=root; _reference=reference; _profile=reference.exactHud;
            root.Q("reference-element-wheel").style.display=DisplayStyle.None;
            var scrim=new Scrim(_profile.bottomScrimOpacity){name="reference-exact-bottom-scrim",pickingMode=PickingMode.Ignore};
            root.Add(scrim); Place(scrim,0,0,600,170); scrim.style.left=Length.Percent(50);scrim.style.translate=new Translate(-300*S,0);scrim.style.top=StyleKeyword.Auto;scrim.style.bottom=0;
            var row=new VisualElement{name="reference-exact-element-row",pickingMode=PickingMode.Ignore};root.Add(row);
            Place(row,0,0,430,116);row.style.left=Length.Percent(50);row.style.marginLeft=7.5f*S;row.style.translate=new Translate(-215*S,0);row.style.top=StyleKeyword.Auto;row.style.bottom=30*S;
            _rule=new RowRule(_profile.gold){name="reference-active-rule",pickingMode=PickingMode.Ignore};row.Add(_rule);Place(_rule,0,0,430,116);
            var sprites=new[]{skin.fire,skin.earth,skin.water,skin.air};
            for(int i=0;i<4;i++)
            {
                var diamond=new Diamond(Accents[i],_profile.selectedGold){name="reference-token-"+Order[i],pickingMode=PickingMode.Ignore};
                row.Add(diamond);_diamonds[i]=diamond;
                _glyphs[i]=new VisualElement{name="reference-glyph-"+Order[i],pickingMode=PickingMode.Ignore};diamond.Add(_glyphs[i]);
                _glyphs[i].style.backgroundImage=new StyleBackground(sprites[i]);_glyphs[i].style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);
                _glyphs[i].style.backgroundRepeat=new BackgroundRepeat(Repeat.NoRepeat,Repeat.NoRepeat);
                _labels[i]=new Label(Order[i].ToString().ToUpperInvariant()){name="reference-label-"+Order[i],pickingMode=PickingMode.Ignore};row.Add(_labels[i]);
                Place(_labels[i],StoneHudExactProfile.ElementCenters[i]-45,84,90,18);
            }
            var clock=root.Q("round-clock");clock.style.backgroundImage=StyleKeyword.None;
            var plate=new Frame(Frame.Shape.Timer,_profile.gold){name="reference-exact-clock-frame",pickingMode=PickingMode.Ignore};clock.Add(plate);Fill(plate);plate.SendToBack();
            foreach(string side in new[]{"red-team","blue-team"})
            {
                var team=root.Q(side); HideOrnaments(team);
                var line=new Frame(Frame.Shape.Score,side=="red-team"?new Color(.65f,.28f,.24f):new Color(.25f,.52f,.66f)){name="reference-exact-score-line",pickingMode=PickingMode.Ignore};team.Add(line);Place(line,0,59,95,12);
            }
            var pause=root.Q("pause-match");pause.style.backgroundColor=Color.clear;ClearBorder(pause);
            var octagon=new Frame(Frame.Shape.Pause,_profile.gold){name="reference-exact-pause-frame",pickingMode=PickingMode.Ignore};pause.Add(octagon);Fill(octagon);octagon.SendToBack();
            var globe=root.Q("planet-globe");var oldFrame=globe.Q("reference-map-frame");if(oldFrame!=null)oldFrame.style.display=DisplayStyle.None;
            if(globe is EarthHologramGlobe liveGlobe){liveGlobe.BackingOpacity=.84f;liveGlobe.MarkDirtyRepaint();}
            globe.Query<Label>().ForEach(label=>label.style.display=DisplayStyle.None);
            var orbit=new Frame(Frame.Shape.Globe,_profile.gold){name="reference-exact-orbit-frame",pickingMode=PickingMode.Ignore};globe.Add(orbit);Fill(orbit);
            AddCardinal(globe,"N",109,34);AddCardinal(globe,"S",109,184);AddCardinal(globe,"W",34,109);AddCardinal(globe,"E",184,109);
            Refresh(); Tick(ElementId.Earth,true);
        }
        public void Refresh()
        {
            Place(_root.Q("reference-hud-wordmark"),34,16,176,45);
            var top=_root.Q("duel-scoreboard");top.style.flexDirection=FlexDirection.Row;top.style.paddingTop=top.style.paddingBottom=top.style.paddingLeft=top.style.paddingRight=0;
            var clock=_root.Q("round-clock");Place(clock,95,0,206,76);clock.style.marginLeft=clock.style.marginRight=0;clock.style.backgroundImage=StyleKeyword.None;
            LabelStyle(clock.Q<Label>(className:"duel-caption"),10.5f,_profile.gold,1.2f,false);Place(clock.Q<Label>(className:"duel-caption"),0,11,206,16);
            LabelStyle(clock.Q<Label>(className:"duel-time"),36,new Color(.96f,.94f,.84f),1.4f,true);Place(clock.Q<Label>(className:"duel-time"),0,27,206,39);
            SerifNumber(clock.Q<Label>(className:"duel-time"));
            foreach(string side in new[]{"red-team","blue-team"})
            {
                var team=_root.Q(side);Place(team,side=="red-team"?0:301,0,94,76);team.style.marginTop=0;team.style.paddingBottom=0;ClearBorder(team);
                var score=team.Q<Label>(className:"duel-score");Place(score,0,2,94,36);LabelStyle(score,33,new Color(.96f,.94f,.86f),1,true);SerifNumber(score);
                var caption=team.Q<Label>(className:"duel-caption");Place(caption,0,37,94,20);LabelStyle(caption,10,new Color(.91f,.87f,.73f),2,false);
            }
            foreach(string kind in new[]{"health","energy"})
            {
                LabelStyle(_root.Q<Label>(kind+"-value"),22,new Color(.98f,.96f,.88f),0,true);
                LabelStyle(_root.Q<Label>(kind+"-icon"),19,new Color(.98f,.96f,.88f),0,true);
                var under=_root.Q(kind+"-under-bar");var caption=under.Q<Label>("reference-"+(kind=="health"?"HP":"MP"));Place(caption,0,54,50,17);LabelStyle(caption,9,_profile.gold,1.5f,false);
            }
            var orbitCaption=_root.Q<Label>("orbit-caption");LabelStyle(orbitCaption,11,_profile.selectedGold,2,false);
            foreach(string cardinal in new[]{"N","S","W","E"})LabelStyle(_root.Q<Label>("reference-exact-cardinal-"+cardinal),8,_profile.gold,0,false);
            var legend=_root.Q("orbit-legend");if(legend!=null)legend.style.display=DisplayStyle.None;
            for(int i=0;i<4;i++)LabelStyle(_labels[i],12,Accents[i],1.8f,false);
        }
        public void Tick(ElementId selected,bool reduced)
        {
            // Stable active edge: no per-frame layout writes, allocations, circular halo or
            // motion is needed for the target. Reduced motion therefore has identical output.
            if(_hasSelection&&_selected==selected)return;_selected=selected;_hasSelection=true;
            _rule.SelectedCenter=-1;
            for(int i=0;i<4;i++)
            {
                bool active=Order[i]==selected;Rect rect=StoneHudExactProfile.ElementVisualRect(i,active);
                Place(_diamonds[i],rect.x,rect.y,rect.width,rect.height);_diamonds[i].Active=active;_diamonds[i].MarkDirtyRepaint();
                float glyph=active?31:25;Place(_glyphs[i],(rect.width-glyph)*.5f,(rect.height-glyph)*.5f,glyph,glyph);
                if(active)_rule.SelectedCenter=StoneHudExactProfile.ElementCenters[i];
            }
            _rule.MarkDirtyRepaint();
        }
        private void AddCardinal(VisualElement parent,string text,float x,float y)
        {var label=new Label(text){name="reference-exact-cardinal-"+text,pickingMode=PickingMode.Ignore};parent.Add(label);Place(label,x-8,y-8,16,16);LabelStyle(label,8,_profile.gold,0,false);}
        private void LabelStyle(Label label,float size,Color color,float spacing,bool bold)
        {
            if(label==null)return;label.style.fontSize=size*S;label.style.color=color;label.style.letterSpacing=spacing*S;label.style.unityTextAlign=TextAnchor.MiddleCenter;
            label.style.paddingLeft=label.style.paddingRight=label.style.paddingTop=label.style.paddingBottom=0;
            Font font=bold?_reference.hudReadableFont:_reference.hudFont;
            if(font!=null){label.style.unityFont=font;label.style.unityFontDefinition=FontDefinition.FromFont(font);}
            label.style.unityFontStyleAndWeight=bold?FontStyle.Bold:FontStyle.Normal;
            // Installed Cinzel is weight400. A narrow matching outline supplies the
            // reference's heavier strokes without substituting a different typeface.
            label.style.unityTextOutlineColor=color;
            label.style.unityTextOutlineWidth=(bold?.28f:.22f)*S;
        }
        private void SerifNumber(Label label)
        {if(label!=null&&_reference.hudFont!=null){label.style.unityFont=_reference.hudFont;label.style.unityFontDefinition=FontDefinition.FromFont(_reference.hudFont);label.style.unityTextOutlineWidth=.55f*S;}}
        private static void Place(VisualElement e,float x,float y,float w,float h)
        {if(e==null)return;e.style.position=Position.Absolute;e.style.left=x*S;e.style.top=y*S;e.style.width=w*S;e.style.height=h*S;e.style.right=e.style.bottom=StyleKeyword.Auto;e.style.marginLeft=e.style.marginRight=e.style.marginTop=e.style.marginBottom=0;}
        private static void Fill(VisualElement e){e.style.position=Position.Absolute;e.style.left=e.style.top=e.style.right=e.style.bottom=0;}
        private static void ClearBorder(VisualElement e){e.style.borderTopWidth=e.style.borderBottomWidth=e.style.borderLeftWidth=e.style.borderRightWidth=0;}
        private static void HideOrnaments(VisualElement parent)
        {foreach(var child in parent.Children())if(child.GetType().Name=="ReferenceOrnament")child.style.display=DisplayStyle.None;}
        private static void Polygon(Painter2D p,Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {p.BeginPath();p.MoveTo(a);p.LineTo(b);p.LineTo(c);p.LineTo(d);p.ClosePath();}
        private sealed class Diamond:VisualElement
        {
            public bool Active;private readonly Color _accent,_gold;
            public Diamond(Color accent,Color gold){_accent=accent;_gold=gold;generateVisualContent+=Draw;}
            private void Draw(MeshGenerationContext ctx)
            {
                var p=ctx.painter2D;Vector2 c=contentRect.center;float r=Mathf.Min(contentRect.width,contentRect.height)*.5f-S;
                p.fillColor=new Color(.025f,.035f,.04f,Active?.16f:.26f);p.strokeColor=Active?_gold:new Color(_accent.r,_accent.g,_accent.b,.90f);p.lineWidth=(Active?2.2f:1.2f)*S;
                Polygon(p,c+Vector2.up*r,c+Vector2.right*r,c+Vector2.down*r,c+Vector2.left*r);p.Fill();p.Stroke();
                if(!Active)return;
                for(int i=6;i>=1;i--){float outer=r+i*S;p.strokeColor=new Color(_gold.r,_gold.g,_gold.b,.24f*(1-i/7f));p.lineWidth=2*S;Polygon(p,c+Vector2.up*outer,c+Vector2.right*outer,c+Vector2.down*outer,c+Vector2.left*outer);p.Stroke();}
            }
        }
        private sealed class RowRule:VisualElement
        {
            public float SelectedCenter=-1;private readonly Color _gold;
            public RowRule(Color gold){_gold=gold;generateVisualContent+=Draw;}
            private void Draw(MeshGenerationContext ctx)
            {
                var p=ctx.painter2D;p.lineWidth=S;p.strokeColor=new Color(_gold.r,_gold.g,_gold.b,.28f);p.BeginPath();p.MoveTo(new Vector2(0,107*S));p.LineTo(new Vector2(430*S,107*S));p.Stroke();
                if(SelectedCenter<0)return;float x=SelectedCenter*S;p.fillColor=_gold;p.strokeColor=_gold;p.lineWidth=2*S;
                p.BeginPath();p.MoveTo(new Vector2(x-35*S,107*S));p.LineTo(new Vector2(x+35*S,107*S));p.Stroke();
                Polygon(p,new Vector2(x,103*S),new Vector2(x+4*S,107*S),new Vector2(x,111*S),new Vector2(x-4*S,107*S));p.Fill();
                p.BeginPath();p.MoveTo(new Vector2(x-4*S,1*S));p.LineTo(new Vector2(x+4*S,1*S));p.LineTo(new Vector2(x,6*S));p.ClosePath();p.Fill();
            }
        }
        private sealed class Scrim:VisualElement
        {
            private readonly float _alpha;public Scrim(float alpha){_alpha=alpha;generateVisualContent+=Draw;}
            private void Draw(MeshGenerationContext ctx)
            {
                float w=contentRect.width,h=contentRect.height;
                if(w<=0||h<=0)return;
                // Vertex alpha interpolation gives a smooth local fade without striped
                // constant-alpha rectangles or another imported background image.
                const int columns=24,rows=6,stride=columns+1;
                var mesh=ctx.Allocate(stride*(rows+1),columns*rows*6);
                for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
                {float fx=(float)x/columns,fy=(float)y/rows;float alpha=_alpha*fy*fy*Mathf.Pow(Mathf.Sin(fx*Mathf.PI),2);mesh.SetNextVertex(new Vertex{position=new Vector3(fx*w,fy*h,Vertex.nearZ),tint=new Color(.015f,.025f,.03f,alpha),uv=Vector2.zero});}
                for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
                {ushort a=(ushort)(y*stride+x),b=(ushort)(a+1),c=(ushort)(a+stride),d=(ushort)(c+1);mesh.SetNextIndex(a);mesh.SetNextIndex(c);mesh.SetNextIndex(b);mesh.SetNextIndex(b);mesh.SetNextIndex(c);mesh.SetNextIndex(d);}
            }
        }
        private sealed class Frame:VisualElement
        {
            public enum Shape{Timer,Score,Pause,Globe}private readonly Shape _shape;private readonly Color _gold;
            public Frame(Shape shape,Color gold){_shape=shape;_gold=gold;generateVisualContent+=Draw;}
            private void Draw(MeshGenerationContext ctx)
            {
                var p=ctx.painter2D;float w=contentRect.width,h=contentRect.height;p.strokeColor=_gold;p.fillColor=new Color(.025f,.045f,.05f,.76f);p.lineWidth=S;
                if(_shape==Shape.Timer){p.BeginPath();p.MoveTo(new Vector2(1,1));p.LineTo(new Vector2(w-1,1));p.LineTo(new Vector2(w-1,h-19*S));p.LineTo(new Vector2(w-19*S,h-1));p.LineTo(new Vector2(19*S,h-1));p.LineTo(new Vector2(1,h-19*S));p.ClosePath();p.Fill();p.Stroke();p.fillColor=_gold;Vector2 c=new Vector2(w*.5f,h-1);Polygon(p,c+Vector2.up*4*S,c+Vector2.right*4*S,c+Vector2.down*4*S,c+Vector2.left*4*S);p.Fill();}
                else if(_shape==Shape.Pause){float c=9*S;p.BeginPath();p.MoveTo(new Vector2(c,1));p.LineTo(new Vector2(w-c,1));p.LineTo(new Vector2(w-1,c));p.LineTo(new Vector2(w-1,h-c));p.LineTo(new Vector2(w-c,h-1));p.LineTo(new Vector2(c,h-1));p.LineTo(new Vector2(1,h-c));p.LineTo(new Vector2(1,c));p.ClosePath();p.Fill();p.Stroke();}
                else if(_shape==Shape.Score){p.BeginPath();p.MoveTo(new Vector2(0,h*.5f));p.LineTo(new Vector2(w,h*.5f));p.Stroke();}
                else
                {
                    Vector2 c=new Vector2(w*.5f,h*.5f);float r=97*S;
                    p.BeginPath();p.Arc(c,r,0,360);p.Stroke();p.strokeColor=new Color(_gold.r,_gold.g,_gold.b,.42f);p.BeginPath();p.Arc(c,r-5*S,0,360);p.Stroke();p.fillColor=_gold;
                    for(int i=0;i<4;i++){float angle=i*Mathf.PI*.5f;Vector2 v=c+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r;Polygon(p,v+Vector2.up*4*S,v+Vector2.right*4*S,v+Vector2.down*4*S,v+Vector2.left*4*S);p.Fill();}
                }
            }
        }
    }
}
