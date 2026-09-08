from pathlib import Path
ui=Path('Tools/FireIntegration/Staged/ReferenceSpriteFidelity/after/Assets/Elemental/Presentation/UI')
p=ui/'StoneReferenceGlyph.cs';s=p.read_text(encoding='utf-8').replace('Diamond, DiamondGlow, People','Diamond, DiamondGlow, ButtonGlow, People');s=s.replace('case Shape.People:', '''case Shape.ButtonGlow:
                    var outline=new[]{new Vector2(0,.5f),new Vector2(.07f,.98f),new Vector2(.965f,.98f),new Vector2(1,.5f),new Vector2(.965f,.02f),new Vector2(.07f,.02f)};
                    foreach(float thickness in new[]{6f,3f,1f}) for(int i=0;i<outline.Length;i++)
                    {
                        var a=P(outline[i].x,outline[i].y);var b=P(outline[(i+1)%outline.Length].x,outline[(i+1)%outline.Length].y);
                        var n=new Vector2(-(b-a).y,(b-a).x).normalized*thickness*.5f;int first=vh.currentVertCount;
                        var tint=new Color(color.r,color.g,color.b,color.a*(thickness>4?.06f:thickness>1?.16f:.8f));
                        vh.AddVert(a+n,tint,Vector2.zero);vh.AddVert(b+n,tint,Vector2.zero);vh.AddVert(b-n,tint,Vector2.zero);vh.AddVert(a-n,tint,Vector2.zero);vh.AddTriangle(first,first+1,first+2);vh.AddTriangle(first,first+2,first+3);
                    }break;
                case Shape.People:''');p.write_text(s,encoding='utf-8')
p=ui/'FrontendButton.cs';s=p.read_text(encoding='utf-8').replace('private StoneReferenceGlyph _roleGlyph;','private StoneReferenceGlyph _roleGlyph, _rimGlow;');s=s.replace('_halo=CreateFx("Stone hover halo",_theme.stoneSkin.halo);_halo.transform.SetAsFirstSibling();','''_halo=CreateFx("Stone hover halo",_theme.stoneSkin.halo);_halo.transform.SetAsFirstSibling();
                _rimGlow=StoneReferenceGlyph.Create(_visual,"Reference button edge light",StoneReferenceGlyph.Shape.ButtonGlow,new Color(.73f,.95f,.46f,1));
                var rim=_rimGlow.rectTransform;rim.anchorMin=Vector2.zero;rim.anchorMax=Vector2.one;rim.offsetMin=Vector2.zero;rim.offsetMax=Vector2.zero;_rimGlow.canvasRenderer.SetAlpha(0);''');s=s.replace('_halo.color=Color.clear; // The selected artwork owns its angular rim light; no radial halo behind the plate.','_halo.color=Color.clear; // No radial halo behind the plate.\n                _rimGlow.canvasRenderer.SetAlpha(Reference.Sample("button_hover","glow",_blend));');s=s.replace('_focusMark.color=new Color(1,.94f,.74f,Reference.Sample("focus_enter","focus",_focusProgress));','_focusMark.color=_theme.stoneSkin.referenceSelectedInsets==Vector4.zero?new Color(1,.94f,.74f,Reference.Sample("focus_enter","focus",_focusProgress)):Color.clear;');p.write_text(s,encoding='utf-8')
