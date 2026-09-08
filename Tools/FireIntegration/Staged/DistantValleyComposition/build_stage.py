from pathlib import Path
import shutil,json
stage=Path(__file__).resolve().parent;root=stage.parents[3]
paths=['Presentation/Environment/DistantBackdrop.cs','Presentation/Environment/DistantBackdropProfile.cs','Authoring/Editor/Environment/ProceduralValleyAuthoring.cs']
for path in paths:
    rel=Path('Assets/Elemental')/path
    for side in ['before','after']:
        p=stage/side/rel;p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(root/rel,p)
def edit(path,old,new):
    p=stage/'after/Assets/Elemental'/path;s=p.read_text(encoding='utf-8');assert old in s,old;p.write_text(s.replace(old,new),encoding='utf-8')
data=json.loads((stage/'analytical-projections.json').read_text());data.sort(key=lambda x:x['airborne'])
lines=[]
for item in data:
    x,y,z=item['position'];lines.append('                new ViewLandmark{name="'+item['name']+'",airborne='+str(item['airborne']).lower()+',variant='+str(item['variant'])+',position=new Vector3('+f'{x:.4f}f,{y:.4f}f,{z:.4f}f'+'),scale='+f'{item["scale"]:.4f}f'+'},')
fields='''        [System.Serializable] public struct ViewLandmark
        {public string name;public bool airborne;public int variant;public Vector3 position;public float scale;}
        [Header("Explicit Main and Combat composition")]
        public bool viewComposition;
        public Mesh[] viewGroundPillars=new Mesh[0];
        public ViewLandmark[] viewLandmarks=DefaultViewLandmarks();
        public static ViewLandmark[] DefaultViewLandmarks()=>new[]{
'''+ '\n'.join(lines)+ '\n        };\n'
edit(paths[1],'        public bool proceduralValley;','        public bool proceduralValley;\n'+fields)
edit(paths[0],'            // Only destroy the exact root previously created by this component.','''            if(profile.proceduralValley&&profile.viewComposition&&(profile.viewGroundPillars==null||profile.viewGroundPillars.Length!=6||profile.viewLandmarks==null||profile.viewLandmarks.Length!=12))
            {Debug.LogError("Use Compose Main And Combat Views to bind the six accepted pillar assets and twelve authored slots.",this);return;}
            // Only destroy the exact root previously created by this component.''')
edit(paths[0],'            for(int i=0;i<originalCount+(profile.rearContinuation?4:0);i++)','''            int legacyCount=originalCount+(profile.rearContinuation?4:0);
            int authoredCount=profile.viewComposition?Mathf.Min(12,profile.viewLandmarks.Length):0;
            for(int i=0;i<legacyCount+authoredCount;i++)''')
edit(paths[0],'                bool rear=i>=originalCount;int rearIndex=i-originalCount;','''                bool authored=i>=legacyCount;
                var landmark=authored?profile.viewLandmarks[i-legacyCount]:default;
                bool rear=!authored&&i>=originalCount;int rearIndex=i-originalCount;''')
edit(paths[0],'                bool airborne=rear?rearIndex==3:i>=ground;','''                bool airborne=authored?landmark.airborne:rear?rearIndex==3:i>=ground;
                if(profile.viewComposition&&!authored&&airborne)continue;''')
edit(paths[0],'                int index=rear?(airborne?floating:ground+rearIndex):(airborne?i-ground:i);','''                int index=authored?100+i-legacyCount:rear?(airborne?floating:ground+rearIndex):(airborne?i-ground:i);
                if(authored&&(airborne?acceptedFloating>=6:acceptedGround>=18))continue;''')
edit(paths[0],'                Mesh[] bank=airborne?profile.islandSilhouettes:profile.silhouettes;','                Mesh[] bank=airborne?profile.islandSilhouettes:authored?profile.viewGroundPillars:profile.silhouettes;')
edit(paths[0],'                Mesh[] lowBank=airborne?profile.islandLodSilhouettes:profile.lodSilhouettes;','                Mesh[] lowBank=airborne?profile.islandLodSilhouettes:authored?null:profile.lodSilhouettes;')
edit(paths[0],'                int variant=index%bank.Length;Mesh mesh=bank[variant];','                int variant=authored?Mathf.Clamp(landmark.variant,0,bank.Length-1):index%bank.Length;Mesh mesh=bank[variant];')
edit(paths[0],'                Vector3 position=default;Bounds envelope=default;bool accepted=false;','''                if(authored){height=Mathf.Clamp(landmark.scale,1,600);rotation=Quaternion.LookRotation(forward,_up);}
                Vector3 position=default;Bounds envelope=default;bool accepted=false;''')
edit(paths[0],'                    position=center+right*x+forward*z+_up*y;','''                    if(authored)
                    {
                        x=landmark.position.x;y=landmark.position.y;z=landmark.position.z;
                        if(attempt>0){x+=placement.Next(-20,20);z+=placement.Next(-20,20);}
                    }
                    position=center+right*x+forward*z+_up*y;''')
edit(paths[0],'                var pivot=new GameObject((rear?"Rear_":"")+(airborne?"Island_":"ValleyGroup_")+index.ToString("00")).transform;','                var pivot=new GameObject(authored?"View_"+landmark.name:(rear?"Rear_":"")+(airborne?"Island_":"ValleyGroup_")+index.ToString("00")).transform;')
editor='''        [MenuItem("Elemental/Environment/Procedural Valley/5 Compose Main And Combat Views")]
        public static void ComposeViews()
        {
            var owner=Owner();var profile=owner.profile;
            int[] variants={0,4,8,2,6,10};var meshes=new Mesh[variants.Length];
            for(int i=0;i<variants.Length;i++)
            {
                string path=Root+"/Preview/Pillar_"+variants[i].ToString("00")+".asset";
                meshes[i]=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(meshes[i]==null)throw new InvalidOperationException("Accepted pillar preview mesh missing: "+path+". Do not substitute a new shape.");
            }
            Undo.RecordObject(profile,"Compose decorative landmarks for Main and Combat");
            profile.viewGroundPillars=meshes;
            if(profile.viewLandmarks==null||profile.viewLandmarks.Length==0)profile.viewLandmarks=DistantBackdropProfile.DefaultViewLandmarks();
            profile.viewComposition=true;profile.rearContinuation=true;EditorUtility.SetDirty(profile);Regenerate();
        }
'''
edit(paths[2],'        [MenuItem("Elemental/Environment/Procedural Valley/New Placement Seed")]',editor+'        [MenuItem("Elemental/Environment/Procedural Valley/New Placement Seed")]')
print('Prepared three-file composition stage')
