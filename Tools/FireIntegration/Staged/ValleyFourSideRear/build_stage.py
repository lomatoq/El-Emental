from pathlib import Path
import shutil
stage=Path(__file__).resolve().parent
root=stage.parents[3]
paths=['Presentation/Environment/DistantBackdrop.cs','Presentation/Environment/DistantBackdropProfile.cs','Presentation/Environment/RockShapeBuilder.cs','Authoring/Editor/Environment/ProceduralValleyAuthoring.cs','Tests/EditMode/DistantBackdropIntegrationTests.cs','Tests/EditMode/ProceduralValleyGeometryTests.cs']
for path in paths:
    rel=Path('Assets/Elemental')/path
    for side in ['before','after']:
        target=stage/side/rel;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(root/rel,target)
def edit(path,old,new):
    p=stage/'after/Assets/Elemental'/path;s=p.read_text(encoding='utf-8');assert old in s,old;p.write_text(s.replace(old,new),encoding='utf-8')
edit(paths[1],'public bool proceduralValley;','public bool proceduralValley;\n        [Tooltip("Add bounded negative-Z sidebands for the front-facing menu; existing combat placements stay unchanged.")]\n        public bool rearContinuation;')
edit(paths[0],'for(int i=0;i<ground+floating;i++)','int originalCount=ground+floating, acceptedGround=0, acceptedFloating=0;\n            for(int i=0;i<originalCount+(profile.rearContinuation?4:0);i++)')
edit(paths[0],'bool airborne=i>=ground;int index=airborne?i-ground:i;','bool rear=i>=originalCount;int rearIndex=i-originalCount;\n                bool airborne=rear?rearIndex==3:i>=ground;\n                int index=rear?(airborne?floating:ground+rearIndex):(airborne?i-ground:i);\n                if(rear && (airborne?acceptedFloating>=6:acceptedGround>=12))continue;')
edit(paths[0],'float side=index%2==0?-1:1;','float side=rear?(rearIndex==1?1:-1):(index%2==0?-1:1);')
edit(paths[0],'float y=airborne?placement.Next(105,255):-profile.valleyFloorBelowCenter;','float y=airborne?placement.Next(105,255):-profile.valleyFloorBelowCenter;\n                    if(rear)\n                    {\n                        // A longitudinal continuation behind Main, not a surrounding ring.\n                        // Independent streams retain every existing combat transform.\n                        z=(airborne?-450f:rearIndex==0?-380f:rearIndex==1?-600f:-850f)+placement.Next(-25,25);\n                        x=(airborne?-100f:side*(rearIndex==0?330f:rearIndex==1?360f:400f))+placement.Next(-20,20);\n                        if(airborne)y=140f+placement.Next(-15,15);\n                    }')
edit(paths[0],'var pivot=new GameObject((airborne?"Island_":"ValleyGroup_")+index.ToString("00")).transform;','if(airborne)acceptedFloating++;else acceptedGround++;\n                var pivot=new GameObject((rear?"Rear_":"")+(airborne?"Island_":"ValleyGroup_")+index.ToString("00")).transform;')
edit(paths[3],'        [MenuItem("Elemental/Environment/Procedural Valley/New Placement Seed")]','        [MenuItem("Elemental/Environment/Procedural Valley/4 Enable Rear Continuation")]\n        public static void EnableRearContinuation()\n        {var owner=Owner();Undo.RecordObject(owner.profile,"Add menu-side valley continuation");owner.profile.rearContinuation=true;EditorUtility.SetDirty(owner.profile);Regenerate();}\n        [MenuItem("Elemental/Environment/Procedural Valley/New Placement Seed")]')
print('Stage baseline and rear continuation prepared')
