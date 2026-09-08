from pathlib import Path
from PIL import Image
import hashlib
scene=Path('Assets/Elemental/Content/Scenes/EarthCoreSlice.unity').read_text(encoding='utf-8').splitlines()
for a,b in [(17377,17420),(133918,133943),(231675,231687)]:
 print('\n'.join(f'{i+1}: {scene[i]}' for i in range(a-1,b)))
p=Path('Assets/Elemental/Content/UI/Stone/Art/FX/dust_soft.png');im=Image.open(p);print('UI dust:',im.mode,im.size,'alpha unique',len(set(im.getchannel('A').getdata())) if im.mode=='RGBA' else 'none')
a=Path('Assets/Elemental/Presentation/VFX/EarthMaterialFeedbackPresenter.cs');b=Path('Tools/FireIntegration/Staged/StoneQa/validated')/a;print('Presenter byte equal validated:',a.read_bytes()==b.read_bytes())
