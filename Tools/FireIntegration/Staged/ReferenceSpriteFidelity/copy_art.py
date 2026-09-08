from pathlib import Path
import shutil,json,hashlib
from PIL import Image
lane=Path('Tools/FireIntegration/Staged/ReferenceSpriteFidelity');out=lane/'after/Assets/Elemental/Content/UI/Stone/ReferenceSprites';out.mkdir(parents=True,exist_ok=True)
src=Path('C:/Users/nirrt/.codex/generated_images/01a079a5-a033-7170-b296-6176489a38b6')
items=[('selected-construction.png','exec-4b420cd2-4465-4191-af98-3b4e88e63bf9.png'),('passive-button.png','exec-0c9de1e1-6c70-43bb-9dc7-7bd4357bdec9.png'),('active-element-card.png','exec-db893603-a2ee-4bc0-8215-1c8ce36c5292.png')]
report=[]
for dest,source in items:
 im=Image.open(src/source);assert im.mode=='RGBA' and im.getextrema()[3][0]==0
 shutil.copy2(src/source,out/dest);report.append({'file':dest,'source':str(src/source),'size':im.size,'alpha':im.getextrema()[3],'sha256':hashlib.sha256((out/dest).read_bytes()).hexdigest()})
(lane/'art-provenance.json').write_text(json.dumps(report,indent=2))
p=lane/'after/Assets/Elemental/Presentation/UI/FrontendMenuView.cs';s=p.read_text(encoding='utf-8');s=s.replace('ReferenceActive ? 484 : 345','ReferenceActive ? 552 : 345').replace('new Vector2(0,-484)','new Vector2(0,-552)').replace('Place(ribbon.rectTransform,-12,108,594,112)','Place(ribbon.rectTransform,-12,122,594,133)');p.write_text(s,encoding='utf-8')
