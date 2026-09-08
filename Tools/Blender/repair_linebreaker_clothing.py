"""Reproducible clothing/head weight pass on the weighted project COPY.

Preserves topology, UV, materials and skeleton/rest matrices. Exports to reports
for coordinated promotion; never overwrites the original or runtime asset.
"""
import bpy, json, math
import numpy as np
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parents[2]
out=root/'BuildReports'/'CharacterRigRepair'
out.mkdir(parents=True,exist_ok=True)
obj=bpy.data.objects['LinebreakerBody']; rig=bpy.data.objects['LinebreakerRig']
assert len(obj.data.vertices)==3365, 'Unexpected topology: re-audit before repair.'
islands=json.loads((out/'islands.json').read_text())
region={i:item['id'] for item in islands for i in item['indices']}
deform={b.name for b in rig.data.bones if b.use_deform}
before={v.index:{obj.vertex_groups[g.group].name:g.weight for g in v.groups} for v in obj.data.vertices}
# The helmet and black plume share one topology island. Use the authored albedo
# to exclude warm metal faces from the plume; position alone deforms the crown.
texture=bpy.data.images['LinebreakerTexture.png']
pixels=np.empty(len(texture.pixels),dtype=np.float32);texture.pixels.foreach_get(pixels)
pixels=pixels.reshape((texture.size[1],texture.size[0],4))
uv=obj.data.uv_layers.active.data
hair_votes={};metal_votes={}
for face in obj.data.polygons:
    if region[face.vertices[0]]!=6:continue
    center=sum((uv[i].uv for i in face.loop_indices),Vector((0,0)))/len(face.loop_indices)
    r,g,b,_=pixels[min(texture.size[1]-1,int(center.y*texture.size[1])),min(texture.size[0]-1,int(center.x*texture.size[0]))]
    votes=hair_votes if r<.3 and r<=b*1.16 else metal_votes
    for i in face.vertices:votes[i]=votes.get(i,0)+1
plume_vertices={i for i,n in hair_votes.items() if n>metal_votes.get(i,0)}

def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a))); return t*t*(3-2*t)

def put(i,w):
    w={k:v for k,v in w.items() if v>1e-6}
    s=sum(w.values()); assert s>0
    for g in obj.vertex_groups:
        if g.name in deform:g.remove([i])
    for k,v in w.items():
        g=obj.vertex_groups.get(k) or obj.vertex_groups.new(name=k)
        g.add([i],v/s,'REPLACE')

def torso_weights(z):
    # Matched vertical bands keep skin/clothing boundaries attached together.
    names=['mixamorig:Hips','mixamorig:Spine1','mixamorig:Spine2']
    if z<.53:
        t=smooth(.48,.55,z); return {names[0]:1-t,names[1]:t}
    t=smooth(.53,.625,z); return {names[1]:1-t,names[2]:t}

changed={'head':0,'hair':0,'torso':0,'belt':0,'skirt':0}
for v in obj.data.vertices:
    x,y,z=v.co; island=region[v.index]
    if island in (7,8,12):
        put(v.index,{'Secondary_HairLock':1});changed['hair']+=1
    elif island in (6,9,10,11,14,15) or (island==0 and z>=.715):
        # Plume exits the crown at the BACK. Everything under the crown is rigid.
        plume=(island==6 and v.index in plume_vertices and y>.025 and z>.80)
        if plume:
            # Conservative retained chain envelope, with rigid root at the crest.
            old=before[v.index]
            w={k:t for k,t in old.items() if k.startswith('Secondary_Tail_')}
            if not w:w={'Secondary_Tail_01':1}
            root_lock=smooth(.955,.987,z)
            total=sum(w.values())
            w={k:t/total*(1-root_lock) for k,t in w.items()}
            w['Secondary_HairLock']=root_lock
            put(v.index,w)
        else:put(v.index,{'Secondary_HairLock':1})
        changed['head']+=1
    elif island in (2,3):
        # Use the same torso envelope on both overlapping surfaces; unrelated
        # thigh influences previously pulled the belt through the waist.
        put(v.index,torso_weights(z));changed['belt']+=1
    elif island==0 and .48<=z<=.615 and abs(x)<.12:
        put(v.index,torso_weights(z));changed['torso']+=1
    elif island==0 and .36<z<.48 and abs(x)<.14:
        # Preserve the split skirt silhouette while removing opposite-leg leaks.
        w=before[v.index]
        side='Left' if x>0 else 'Right'
        thigh=smooth(.48,.37,z)*smooth(.012,.08,abs(x))*.82
        put(v.index,{'mixamorig:Hips':1-thigh,'mixamorig:'+side+'UpLeg':thigh})
        changed['skirt']+=1

obj.data.update()
all_sums=[sum(g.weight for g in v.groups if obj.vertex_groups[g.group].name in deform) for v in obj.data.vertices]
report={'source':bpy.data.filepath,'changed_regions':changed,'vertices':len(obj.data.vertices),
        'plume_vertices':len(plume_vertices),
        'max_normalization_error':max(abs(s-1) for s in all_sums),
        'topology_preserved':True,'uv_preserved':True,'skeleton_preserved':True}
assert report['max_normalization_error']<1e-5
head_ids=[v.index for v in obj.data.vertices if region[v.index] in (6,7,8,9,10,11,12,14,15)]
report['head_arm_weight_before']=sum(w for i in head_ids for name,w in before[i].items() if 'arm' in name.lower() or 'shoulder' in name.lower())
report['head_arm_weight_after']=sum(g.weight for i in head_ids for g in obj.data.vertices[i].groups if 'arm' in obj.vertex_groups[g.group].name.lower() or 'shoulder' in obj.vertex_groups[g.group].name.lower())
assert report['head_arm_weight_after']==0

# Save/export BEFORE adding diagnostic cameras or poses.
destination=root/'ArtSource/Characters/Linebreaker/LinebreakerRigged_clothing.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(destination))
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(out/'Linebreaker.fbx'),use_selection=True,
    object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
    use_armature_deform_only=False)
(out/'repair-report.json').write_text(json.dumps(report,indent=2))

scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO';scene.display.shading.color_type='TEXTURE'
scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
scene.display.shading.background_type='WORLD';scene.world.color=(.055,.055,.055)
camera=bpy.data.objects['Camera'];scene.camera=camera
camera.data.type='ORTHO';camera.data.ortho_scale=1.25
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
poses={'neutral':{},'deep-bend':{'mixamorig:Spine1':(-35,0,0),'mixamorig:Spine2':(-20,0,0),'mixamorig:LeftArm':(0,0,-65),'mixamorig:RightArm':(0,0,65)},
       'back-bend':{'mixamorig:Spine1':(35,0,0),'mixamorig:Spine2':(20,0,0),'mixamorig:LeftArm':(0,0,-65),'mixamorig:RightArm':(0,0,65)},
       'kick-turn':{'mixamorig:Spine2':(10,25,0),'mixamorig:LeftUpLeg':(-65,0,0),'mixamorig:LeftLeg':(25,0,0),'mixamorig:LeftArm':(0,0,-80),'mixamorig:RightArm':(0,0,65)},
       'head-turn':{'mixamorig:Head':(20,35,0),'mixamorig:LeftArm':(0,0,-90),'mixamorig:RightArm':(0,0,90)},
       'secondary-extreme':{'Secondary_Tail_01':(6,0,4),'Secondary_Tail_02':(6,0,4),'Secondary_Tail_03':(6,0,4),'Secondary_Belt_L_01':(-10,0,5),'Secondary_Belt_L_02':(-10,0,5),'Secondary_Belt_R_01':(-10,0,-5),'Secondary_Belt_R_02':(-10,0,-5)}}
for name,pose in poses.items():
    for b in rig.pose.bones:b.rotation_mode='XYZ';b.rotation_euler=(0,0,0)
    for name_b,angles in pose.items():rig.pose.bones[name_b].rotation_euler=[math.radians(a) for a in angles]
    bpy.context.view_layer.update()
    for side,loc in [('front',(1.15,-2.5,.9)),('back',(-1.15,2.5,.9))]:
        camera.location=loc;camera.rotation_euler=(Vector((0,0,.5))-camera.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(out/(name+'-'+side+'-after.png'));bpy.ops.render.render(write_still=True)
print('ELM_RESULT'+json.dumps(report))
