"""Read-only mesh island audit, run in a background Blender copy via MCP."""
import bpy, json
from pathlib import Path
from collections import Counter

root=Path(__file__).resolve().parents[2]
out=root/'BuildReports'/'CharacterRigRepair'
out.mkdir(parents=True,exist_ok=True)
obj=bpy.data.objects['LinebreakerBody']
verts=obj.data.vertices
adj=[[] for _ in verts]
for e in obj.data.edges:
    a,b=e.vertices; adj[a].append(b); adj[b].append(a)
seen=set(); islands=[]
for v in verts:
    if v.index in seen: continue
    seen.add(v.index); todo=[v.index]; ids=[]
    while todo:
        i=todo.pop(); ids.append(i)
        for j in adj[i]:
            if j not in seen: seen.add(j); todo.append(j)
    weights=Counter()
    for i in ids:
        for g in verts[i].groups: weights[obj.vertex_groups[g.group].name]+=g.weight
    islands.append({'id':len(islands),'verts':len(ids),'indices':ids,'min':[min(verts[i].co[a] for i in ids) for a in range(3)],'max':[max(verts[i].co[a] for i in ids) for a in range(3)],'weights':dict(weights.most_common(8))})
(out/'islands.json').write_text(json.dumps(islands,indent=2))
print('ELM_RESULT'+json.dumps([{k:v for k,v in x.items() if k!='indices'} for x in islands]))

from mathutils import Vector
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO'
scene.display.shading.color_type='TEXTURE'
scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True
scene.display.shading.background_type='WORLD'
scene.world.color=(0.055,0.055,0.055)
camera=bpy.data.objects.get('Camera')
scene.camera=camera
camera.data.type='ORTHO'; camera.data.ortho_scale=1.25
camera.location=(1.15,-2.5,0.9)
camera.rotation_euler=(Vector((0,0,0.5))-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.filepath=str(out/'neutral-before.png')
bpy.ops.render.render(write_still=True)
