from pathlib import Path
import shutil,json,hashlib,difflib
lane=Path('Tools/FireIntegration/Staged/MenuSpriteRefinement')
paths=['Assets/Elemental/Presentation/UI/FrontendMenuView.cs','Assets/Elemental/Presentation/UI/FrontendButton.cs','Assets/Elemental/Authoring/Editor/StoneUI/StoneReferenceSpriteInstaller.cs']
for rel in paths:
 for side in ['before','after']:
  p=lane/side/rel;p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(rel,p)
p=lane/'after'/paths[0];s=p.read_text(encoding='utf-8-sig').replace('Place(_panel,54,32,570,1020)','Place(_panel,82,32,570,1020)');s=s.replace('caption.margin = new Vector4(ReferenceActive ? 114 : skinned ? 72 : 24, 0, 24, 0); caption.alignment = TextAlignmentOptions.MidlineLeft;','caption.margin = ReferenceActive ? new Vector4(100,0,100,0) : new Vector4(skinned ? 72 : 24,0,24,0); caption.alignment = ReferenceActive ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;');p.write_text(s,encoding='utf-8')
p=lane/'after'/paths[1];s=p.read_text(encoding='utf-8-sig').replace('foreach(var caption in _captions)caption.color=ink;','foreach(var caption in _captions)caption.color=caption.name=="Button chevron"&&!cream?new Color(.67f,.84f,.38f):ink;');p.write_text(s,encoding='utf-8')
p=lane/'after'/paths[2];s=p.read_text(encoding='utf-8-sig').replace('importer.alphaIsTransparency=true;importer.mipmapEnabled=false;','importer.alphaIsTransparency=true;importer.mipmapEnabled=true;\n            // Native 2K art is minified to tens/hundreds of UI pixels. Mips suppress etched rim/texture aliasing.\n            importer.mipmapFilter=TextureImporterMipFilter.BoxFilter;importer.mipMapsPreserveCoverage=false;').replace('importer.filterMode=FilterMode.Bilinear','importer.filterMode=FilterMode.Trilinear');p.write_text(s,encoding='utf-8')
p=lane/'compile_review.py';p.write_text(Path('Tools/FireIntegration/Staged/ReferenceSpriteFidelity/compile_review.py').read_text(encoding='utf-8').replace('ReferenceSpriteFidelity','MenuSpriteRefinement'),encoding='utf-8')
manifest=[];patch=[]
for rel in paths:
 before=lane/'before'/rel;after=lane/'after'/rel
 manifest.append({'path':rel,'beforeSHA256':hashlib.sha256(before.read_bytes()).hexdigest(),'afterSHA256':hashlib.sha256(after.read_bytes()).hexdigest(),'actualStillMatchesBefore':before.read_bytes()==Path(rel).read_bytes()})
 patch.extend(difflib.unified_diff(before.read_text(encoding='utf-8-sig').splitlines(True),after.read_text(encoding='utf-8').splitlines(True),fromfile='a/'+rel,tofile='b/'+rel))
(lane/'import-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8');(lane/'source.patch').write_text(''.join(patch),encoding='utf-8')
print('staged',len(paths),'fresh',all(r['actualStillMatchesBefore'] for r in manifest))
