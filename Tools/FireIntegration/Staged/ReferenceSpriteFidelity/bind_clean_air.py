from pathlib import Path
import shutil,json,hashlib
from PIL import Image
lane=Path('Tools/FireIntegration/Staged/ReferenceSpriteFidelity');art=lane/'after/Assets/Elemental/Content/UI/Stone/ReferenceSprites';src=Path('C:/Users/nirrt/.codex/generated_images/01a079a5-a033-7170-b296-6176489a38b6/exec-362cdd82-4f67-45bf-897b-d4ff4764f7d6.png');im=Image.open(src);assert im.mode=='RGBA';shutil.copy2(src,art/'air-glyph.png')
p=lane/'after/Assets/Elemental/Authoring/Editor/StoneUI/StoneReferenceSpriteInstaller.cs';s=p.read_text(encoding='utf-8').replace('skin.air=Prepare("element-glyphs",2172,724,new Rect(1695,130,390,440),Vector4.zero,"reference-air");','skin.air=Prepare("air-glyph",1254,1254,new Rect(190,104,970,1070),Vector4.zero,"reference-air");');p.write_text(s,encoding='utf-8')
p=lane/'after/Assets/Elemental/Tests/EditMode/ReferenceSpriteGeometryTests.cs';s=p.read_text(encoding='utf-8').replace('[TestCase("element-glyphs",2172,724)]','[TestCase("element-glyphs",2172,724)]\n        [TestCase("air-glyph",1254,1254)]');p.write_text(s,encoding='utf-8')
report=json.loads((lane/'art-provenance.json').read_text());report+=[{'file':'element-glyphs.png','source':'C:/Users/nirrt/.codex/generated_images/01a079a5-a033-7170-b296-6176489a38b6/exec-cedb2c0e-4b5a-4358-9a14-58c8d503812a.png','note':'Only Fire/Earth/Water slots are bound. Air slot rejected for edge debris; separate Air glyph used.'},{'file':'air-glyph.png','source':str(src)}]
for r in report:
 im=Image.open(art/r['file']);r.update(size=im.size,alpha=im.getextrema()[3],sha256=hashlib.sha256((art/r['file']).read_bytes()).hexdigest())
(lane/'art-provenance.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
