from pathlib import Path
import shutil
lane=Path('Tools/FireIntegration/Staged/ReferenceSpriteFidelity')
for rel in ['Assets/Elemental/Presentation/UI/StoneReferenceHudPresentation.cs','Assets/Elemental/Tests/PlayMode/StoneSkinPlayTests.cs']:
 for side in ['before','after']:
  p=lane/side/rel;p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(rel,p)
def edit(name,fn):
 p=lane/'after'/name;p.write_text(fn(p.read_text(encoding='utf-8-sig')),encoding='utf-8')
ui='Assets/Elemental/Presentation/UI/'
edit(ui+'ElementalStoneSkin.cs',lambda s:s.replace('public Sprite referenceSelected, referenceCurtain;','public Sprite referenceSelected, referenceCurtain, referenceNormal, referenceCard;\n        // Native pixel insets from the complete construction bounds to its cream button face.\n        public Vector4 referenceSelectedInsets;'))
edit(ui+'FrontendButton.cs',lambda s:s.replace('private Image _roleIcon;','private Image _roleIcon;\n        private StoneReferenceGlyph _roleGlyph;').replace('_restingSprite = (_graphic as Image)?.sprite;','_restingSprite = (_graphic as Image)?.sprite;\n            _roleGlyph=_visual.GetComponentInChildren<StoneReferenceGlyph>(true);').replace('var go = new GameObject("Press Visual", typeof(RectTransform), typeof(Image));','var go = new GameObject("Press Visual", typeof(RectTransform));').replace('var image = go.GetComponent<Image>();','var art = new GameObject("Button artwork",typeof(RectTransform),typeof(Image));\n            var artRect=(RectTransform)art.transform;artRect.SetParent(_visual,false);artRect.anchorMin=Vector2.zero;artRect.anchorMax=Vector2.one;artRect.offsetMin=artRect.offsetMax=Vector2.zero;\n            var image = art.GetComponent<Image>();').replace('source.color = Color.clear;','art.transform.SetAsFirstSibling();\n            source.color = Color.clear;').replace('skinned.color = Color.white;','skinned.color = active?Color.white:new Color(.6f,.6f,.6f,.65f);\n                var insets=ReferenceActive&&sprite==_theme.stoneSkin.referenceSelected?_theme.stoneSkin.referenceSelectedInsets:Vector4.zero;\n                float faceHeight=sprite.rect.height-insets.y-insets.w;\n                float density=faceHeight/Mathf.Max(1,_visual.rect.height);\n                skinned.pixelsPerUnitMultiplier=density;\n                skinned.rectTransform.offsetMin=new Vector2(-insets.x,-insets.y)/density;\n                skinned.rectTransform.offsetMax=new Vector2(insets.z,insets.w)/density;').replace('if(_roleIcon!=null)_roleIcon.color=ink;','if(_roleIcon!=null)_roleIcon.color=ink;\n            if(_roleGlyph!=null)_roleGlyph.color=ink;').replace('_halo.color=new Color(.73f,.95f,.46f,Reference.Sample("button_hover","glow",_blend));','_halo.color=Color.clear; // The selected artwork owns its angular rim light; no radial halo behind the plate.'))
# Every page calls this one menu button factory. Native procedural silhouettes reuse its visual/hit hierarchy.
def menu(s):
 s=s.replace('private readonly Image[] _elementGlows = new Image[4];','private readonly Graphic[] _elementGlows = new Graphic[4];').replace('private readonly Image[] _elementFrames = new Image[4];','private readonly Graphic[] _elementFrames = new Graphic[4];')
 s=s.replace('graphic.sprite = tier == 0 && skin.primary != null ? skin.primary : skin.normal;','graphic.sprite = ReferenceActive && skin.referenceNormal != null ? skin.referenceNormal : tier == 0 && skin.primary != null ? skin.primary : skin.normal;')
 marker='            if (ReferenceActive) { var arrow=Label(r,'
 insert='''            if (ReferenceActive && roleIcon != null)
            {
                StoneReferenceGlyph.Shape? shape=roleIcon==skin.hostIcon?StoneReferenceGlyph.Shape.People:roleIcon==skin.joinIcon?StoneReferenceGlyph.Shape.PersonPlus:roleIcon==skin.settingsIcon?StoneReferenceGlyph.Shape.Gear:roleIcon==skin.exitIcon?StoneReferenceGlyph.Shape.Door:roleIcon==skin.botIcon?StoneReferenceGlyph.Shape.Target:(StoneReferenceGlyph.Shape?)null;
                if(shape.HasValue){r.Find("Button role icon").GetComponent<Image>().enabled=false;var glyph=StoneReferenceGlyph.Create(r,"Reference role silhouette",shape.Value,_theme.text);Place(glyph.rectTransform,38,18,42,42);}
            }
'''
 s=s.replace(marker,insert+marker)
 s=s.replace('var glow=Image(parent,"Element glow "+ElementOrder[i],Color.clear); glow.sprite=skin.halo;','var glow=StoneReferenceGlyph.Create(parent,"Element glow "+ElementOrder[i],StoneReferenceGlyph.Shape.DiamondGlow,Color.clear);')
 s=s.replace('Place(glow.rectTransform,26+i*128,-27,124,124)','Place(glow.rectTransform,41+i*128,-12,94,94)')
 s=s.replace('var frame=Image(parent,"Element status "+ElementOrder[i],colors[i]); frame.sprite=skin.diamond;','var frame=StoneReferenceGlyph.Create(parent,"Element status "+ElementOrder[i],StoneReferenceGlyph.Shape.Diamond,colors[i]);').replace('frame.preserveAspect=true; frame.raycastTarget=false;','frame.raycastTarget=false;')
 s=s.replace('ribbon.sprite=skin.ribbon; ribbon.type=UnityEngine.UI.Image.Type.Sliced;','ribbon.sprite=skin.referenceCard!=null?skin.referenceCard:skin.ribbon; ribbon.type=UnityEngine.UI.Image.Type.Simple;')
 s=s.replace('Place(ribbon.rectTransform,-12,112,594,82);','Place(ribbon.rectTransform,-12,108,594,112);')
 s=s.replace('Place(_activeElementGlyph.rectTransform,29,9,65,65)','Place(_activeElementGlyph.rectTransform,58,8,68,68)').replace('Place(_activeElementLabel.rectTransform,110,8,470,34)','Place(_activeElementLabel.rectTransform,168,8,366,34)').replace('Place(_activeElementTraits.rectTransform,110,44,470,26)','Place(_activeElementTraits.rectTransform,156,48,366,26)')
 old='var halo=Image(footerBlock,"Element footer glow",new Color(1,.91f,.58f,.5f));halo.sprite=_theme.stoneSkin.halo;halo.raycastTarget=false;Place(halo.rectTransform,9,23,46,46);'
 new='var halo=StoneReferenceGlyph.Create(footerBlock,"Element footer glow",StoneReferenceGlyph.Shape.DiamondGlow,new Color(1,.91f,.58f,.8f));Place(halo.rectTransform,17,31,30,30);'
 s=s.replace(old,new)
 return s
edit(ui+'FrontendMenuView.cs',menu)
p=lane/'compile_review.py';p.write_text(Path('Tools/FireIntegration/Staged/UIFinalOrnaments/compile_review.py').read_text().replace('UIFinalOrnaments','ReferenceSpriteFidelity'))
