from pathlib import Path
import shutil
lane=Path(__file__).resolve().parent;root=lane.parents[3]
def edit(rel,fn):
 p=root/rel;a=lane/'after'/rel;b=lane/'before'/rel
 a.parent.mkdir(parents=True,exist_ok=True);b.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,b);a.write_text(fn(p.read_text(encoding='utf-8-sig')),encoding='utf-8')
edit('Assets/Elemental/Presentation/Fire/ArenaColumnFires.cs',lambda s:s.replace('public const string OwnedName=', 'private static int owners;\n  public static bool HasActive=>owners>0;\n  private void OnEnable(){owners++;}\n  public const string OwnedName=').replace('Mathf.Lerp(.12f,3.8f,','Mathf.Lerp(.3f,14f,').replace('lamp.range=9','lamp.range=12').replace('new Color(1,.38f,.09f)','new Color(1,.52f,.18f)').replace('private void OnDisable(){','private void OnDisable(){owners=Mathf.Max(0,owners-1);'))
edit('Assets/Elemental/Presentation/Rendering/AtmosphereFullscreenFeature.cs',lambda s:s.replace('bool drawClouds=ValleyCloudParticles.HasActive || ProceduralCloudBanks.HasActive;','bool drawClouds=ValleyCloudParticles.HasActive || ProceduralCloudBanks.HasActive || Elemental.Presentation.Fire.ArenaColumnFires.HasActive;'))
edit('Assets/Elemental/Authoring/Editor/ArenaColumnFireSetup.cs',lambda s:s.replace('profile.SpawnRate=50','profile.SpawnRate=70').replace('profile.FlameMinWidth=.22f;profile.FlameMaxWidth=.42f','profile.FlameMinWidth=.32f;profile.FlameMaxWidth=.60f').replace('profile.FreeLift=1.5f;EditorUtility.SetDirty(profile);', '''profile.FreeLift=1.5f;
   var decorShader=Shader.Find("Elemental/Fire/Column Decor Flame");
   if(decorShader==null || ShaderUtil.ShaderHasError(decorShader))throw new InvalidOperationException("Import Column Decor Flame shader first.");
   const string materialPath="Assets/Elemental/Content/VFX/Fire/Fire_ColumnDecor.mat";
   var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
   if(material==null){material=new Material(source.CpuMaterial);material.name="Fire_ColumnDecor";AssetDatabase.CreateAsset(material,materialPath);}
   material.shader=decorShader;material.SetColor("_EdgeColor",new Color(1,.12f,.01f));material.SetColor("_BodyColor",new Color(2.4f,.62f,.05f));
   material.SetColor("_CoreColor",new Color(4,2.8f,.7f));material.SetFloat("_CoreEmission",2.4f);material.SetFloat("_Opacity",.85f);
   profile.CpuMaterial=material;EditorUtility.SetDirty(material);EditorUtility.SetDirty(profile);'''))
edit('Assets/Elemental/Tests/EditMode/ArenaColumnFireTests.cs',lambda s:s.replace('Is.InRange(.119f,3.801f)','Is.InRange(.299f,14.001f)').replace('4.181f','15.401f'))
shader=root/'Assets/Elemental/Presentation/Fire/Shaders/FireCpuMesh.shader'
dest=lane/'after/Assets/Elemental/Presentation/Fire/Shaders/ColumnDecorFlame.shader';dest.parent.mkdir(parents=True,exist_ok=True)
dest.write_text(shader.read_text().replace('Elemental/Fire/CpuMeshFlame','Elemental/Fire/Column Decor Flame').replace('"LightMode"="SRPDefaultUnlit"','"LightMode"="ElementalValleyCloud"'),encoding='utf-8')
shutil.copy2(root/'Tools/FireIntegration/Staged/ArenaColumnFires/compile.py',lane/'compile.py')
