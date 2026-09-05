"""Export the CURRENT edited columns, without rebaking or modifying their meshes.

Temporary export objects carry closed convex collision, seam bonds, and one intact
proxy for each authored damaged silhouette. Authored fallen stones remain loose.
"""
import bpy
import bmesh
import json
import sys
import types
import math
from pathlib import Path
from mathutils import Matrix, Vector

PROJECT=Path(__file__).resolve().parents[2]
OUT=PROJECT/'Assets/Elemental/Content/Arena/OuterStoneRing'
helper=types.ModuleType('outer_export_helpers')
sys.modules[helper.__name__]=helper
source=(PROJECT/'Tools/Blender/bake_broken_crown_arena.py').read_text(encoding='utf-8')
exec(compile(source.rsplit('result = main()',1)[0],'arena_helpers','exec'),helper.__dict__)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    roots=sorted([o for o in bpy.context.scene.objects if o.name.startswith('OuterArch_') and o.type=='EMPTY'],key=lambda o:o.name)
    assert len(roots)==7, 'Expected seven edited column roots.'
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,check_existing=False)
    previous_active=bpy.context.view_layer.objects.active
    previous_selected=list(bpy.context.selected_objects)
    export=bpy.data.collections.new('TEMP_OuterRing_Export')
    bpy.context.scene.collection.children.link(export)
    created=[]; meshes=[]; structures=[]; loose=[]; ignored=[]
    def empty(name,parent=None,matrix=None):
        o=bpy.data.objects.new(name,None);export.objects.link(o);created.append(o)
        o.parent=parent
        if matrix is not None:o.matrix_world=matrix
        return o
    def meshobj(name,mesh,parent,matrix):
        o=bpy.data.objects.new(name,mesh);export.objects.link(o);created.append(o);meshes.append(mesh)
        o.parent=parent;o.matrix_world=matrix
        return o
    def authored_copy(original,name,parent):
        # Copy the full authored stack, including Smooth by Angle node assets.
        # Recreating only bevels silently discarded the artist's normal fixes.
        o=original.copy();o.data=original.data.copy();o.name=name
        export.objects.link(o);created.append(o);meshes.append(o.data)
        o.parent=parent;o.matrix_world=original.matrix_world.copy()
        o.hide_render=False;o.hide_viewport=False;o.hide_set(False)
        return o
    try:
        for ri,root in enumerate(roots):
            sid=f'outer_arch_{ri+1:02d}'
            frame=empty(f'FRAME_{sid}',matrix=root.matrix_world.copy())
            fr=empty(f'FR_{sid}_ROOT',parent=frame,matrix=frame.matrix_world.copy())
            rows=[]; raws=[]; copies=[]
            for original in sorted(root.children,key=lambda o:o.name):
                if original.type!='MESH':continue
                if len(original.data.polygons)==0:
                    ignored.append(original.name);continue
                bm=bmesh.new();bm.from_mesh(original.data)
                if any(not e.is_manifold for e in bm.edges):
                    raise RuntimeError(f'{original.name} has open geometry after editing.')
                if bm.calc_volume(signed=True)<0:
                    bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
                volume=helper.volume(bm)
                local=frame.matrix_world.inverted()@original.matrix_world
                for v in bm.verts:v.co=local@v.co
                bm.normal_update()
                minimum,maximum,_=helper.bounds(bm)
                seams=helper.seam_metadata(bm)
                rest_values=original.get('assembled_local_matrix')
                moved=False
                if rest_values is not None and len(rest_values)==16:
                    rest=Matrix([list(rest_values)[k:k+4] for k in range(0,16,4)])
                    moved=(local.translation-rest.translation).length>0.025 or local.to_quaternion().rotation_difference(rest.to_quaternion()).angle>0.01
                if original.get('authored_detached') or moved:
                    # Artist-moved fragments are authored loose stones, not remote
                    # foundations or invisible bonds back to the standing shaft.
                    name=f'OuterArch_{ri+1:02d}_Loose_{original.name.replace(".","_")}'
                    obj=authored_copy(original,name,frame)
                    loose.append(name);bm.free();continue
                i=len(rows)+1
                obj=authored_copy(original,f'FR_{sid}_P{i:03d}',fr)
                col=helper.create_convex_collider(obj,collection=export)
                created.append(col);meshes.append(col.data)
                col.parent=fr;col.matrix_world=obj.matrix_world.copy();col.hide_set(False)
                copies.append(obj);raws.append(bm)
                rows.append({'id':i,'name':obj.name,'collider':col.name,'repairable':True,
                             'volume_cubic_metres':volume,'seams':seams,'minZ':minimum.z,'maxZ':maximum.z})
            # Union the UNBEVELLED solids before applying the outer edge bevel.
            # Joining evaluated shards retains every fracture groove and internal cap.
            # Manifold union removes those caps while retaining authored missing volumes.
            bpy.ops.object.select_all(action='DESELECT')
            intact_parts=[]
            bpy.context.view_layer.update()
            for obj in copies:
                mesh=obj.data.copy()
                # Boolean topology invalidates the source split-normal layer.
                # This is a temporary union input; artist meshes stay untouched.
                stale_normals=mesh.attributes.get('custom_normal')
                if stale_normals is not None:mesh.attributes.remove(stale_normals)
                # Do the boolean in shared column coordinates, avoiding differences
                # introduced by repeated world/local floating point transforms.
                mesh.transform(frame.matrix_world.inverted()@obj.matrix_world)
                solid=bmesh.new();solid.from_mesh(mesh)
                bmesh.ops.recalc_face_normals(solid,faces=list(solid.faces))
                solid.normal_update()
                # A 0.1 mm overlap removes coincident-plane ambiguity in sequential
                # boolean unions of split solids (far below the 8 mm edge bevel).
                for v in solid.verts:v.co+=v.normal*0.0001
                bmesh.ops.triangulate(solid,faces=list(solid.faces))
                solid.to_mesh(mesh);solid.free()
                part=meshobj('TEMP_Intact',mesh,frame,frame.matrix_world.copy())
                intact_parts.append(part)
            intact=intact_parts[0];intact.name=f'OuterArch_{ri+1:02d}_INTACT'
            bpy.context.view_layer.objects.active=intact
            intact.select_set(True)
            for part in intact_parts[1:]:
                mod=intact.modifiers.new('Bake joined stone volume','BOOLEAN')
                mod.operation='UNION';mod.solver='MANIFOLD';mod.object=part
                bpy.ops.object.modifier_apply(modifier=mod.name)
                bpy.data.objects.remove(part,do_unlink=True)
            joined=bmesh.new();joined.from_mesh(intact.data)
            bmesh.ops.recalc_face_normals(joined,faces=list(joined.faces))
            islands=helper.connected_components(joined)
            if len(islands)!=1 or any(not e.is_manifold for e in joined.edges):
                stats=[]
                for island in islands:
                    part=helper.copy_vertex_component(joined,set(island))
                    stats.append({'verts':len(part.verts),'volume':helper.volume(part),'bounds':[list(v) for v in helper.bounds(part)[:2]]})
                    part.free()
                raise RuntimeError(f'{sid}: invalid union: {stats}, nonmanifold={sum(not e.is_manifold for e in joined.edges)}')
            joined.to_mesh(intact.data);joined.free()
            bevel=intact.modifiers.new('Intact outer stone edges','BEVEL')
            bevel.width=.008;bevel.segments=1;bevel.limit_method='ANGLE';bevel.angle_limit=.6
            bpy.ops.object.modifier_apply(modifier=bevel.name)
            final_check=bmesh.new();final_check.from_mesh(intact.data)
            try:
                # The manifold Boolean can leave warped n-gons. Blender and the
                # FBX exporter are allowed to tessellate those n-gons differently,
                # which makes a valid polygon normal become a backwards corner
                # normal on one exported triangle. Freeze one triangulation before
                # smoothing so Blender and Unity see the same geometric faces.
                bmesh.ops.recalc_face_normals(final_check,faces=list(final_check.faces))
                bmesh.ops.triangulate(final_check,faces=list(final_check.faces))
                bmesh.ops.recalc_face_normals(final_check,faces=list(final_check.faces))
                if (len(helper.connected_components(final_check))!=1 or
                    any(not e.is_manifold for e in final_check.edges) or
                    final_check.calc_volume(signed=True)<=0):
                    raise RuntimeError(f'{sid}: final bevel produced an invalid intact solid')
                final_check.to_mesh(intact.data)
            finally:
                final_check.free()
            # Join keeps the first piece transform; bake it to the column frame.
            intact.data.transform(frame.matrix_world.inverted()@intact.matrix_world)
            intact.matrix_world=frame.matrix_world.copy()
            # Match the artist's 30-degree smoothing on curved stone surfaces.
            # Tiny Boolean slivers must not participate in neighbouring smooth
            # fans; isolate only those faces rather than faceting the whole arch.
            # Do this after the last transform, which can perturb tiny faces.
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
            for face in intact.data.polygons:face.use_smooth=True
            intact.data.set_sharp_from_angle(angle=math.radians(30))
            for face in intact.data.polygons:
                if face.area<1e-6:
                    face.use_smooth=False
                    for loop in face.loop_indices:
                        intact.data.edges[intact.data.loops[loop].edge_index].use_edge_sharp=True
            intact.data.update()
            invalid_fans=[face for face in intact.data.polygons
                          if face.use_smooth and any(intact.data.corner_normals[i].vector.length_squared<.25
                                                     for i in face.loop_indices)]
            for face in invalid_fans:
                face.use_smooth=False
                for loop in face.loop_indices:
                    intact.data.edges[intact.data.loops[loop].edge_index].use_edge_sharp=True
            intact.data.update()
            # A final area-aware guard preserves smooth curves and isolates only
            # triangles whose interpolated corner normals still oppose their
            # frozen winding. These are rare Boolean slivers around inner bends.
            intact.data.calc_loop_triangles()
            backward_faces=set()
            for tri in intact.data.loop_triangles:
                if tri.area<1e-6:continue
                average=Vector()
                for loop_index in tri.loops:
                    average+=intact.data.corner_normals[loop_index].vector
                if average.length_squared<.25 or tri.normal.dot(average.normalized())<.70:
                    backward_faces.add(tri.polygon_index)
            for polygon_index in backward_faces:
                face=intact.data.polygons[polygon_index]
                face.use_smooth=False
                for loop in face.loop_indices:
                    intact.data.edges[intact.data.loops[loop].edge_index].use_edge_sharp=True
            intact.data.update()
            intact.data.calc_loop_triangles()
            for tri in intact.data.loop_triangles:
                if tri.area<1e-6:continue
                average=Vector()
                for loop_index in tri.loops:
                    average+=intact.data.corner_normals[loop_index].vector
                if average.length_squared<.25 or tri.normal.dot(average.normalized())<.70:
                    raise RuntimeError(f'{sid}: backward significant corner normal after final guard')
            minz=min(r['minZ'] for r in rows)
            for r in rows:r['foundation']=r['minZ']<=minz+0.35
            bonds=[];owners={}
            for i,r in enumerate(rows):
                for seam,record in r['seams'].items():owners.setdefault(int(seam),[]).append((i,record))
            pairs=set()
            def bond(a,b,point,area,foundation=False):
                marker=empty(f'BOND_{sid}_{len(bonds)+1:03d}',frame,frame.matrix_world@Matrix.Translation(Vector(point)))
                bonds.append({'id':len(bonds)+1,'pieceA':a,'pieceB':b,'marker':marker.name,
                              'contactArea':max(0.0001,area),'foundation':foundation})
            for seam,items in sorted(owners.items()):
                for a,sa in items:
                    opposite=[(b,sb) for b,sb in items if sb['side']*sa['side']<0]
                    amin,amax,_=helper.bounds(raws[a])
                    for b,sb in opposite:
                        pair=tuple(sorted((a,b)))
                        if pair in pairs:continue
                        bmin,bmax,_=helper.bounds(raws[b])
                        lower=Vector(tuple(max(amin[k],bmin[k]) for k in range(3)))
                        upper=Vector(tuple(min(amax[k],bmax[k]) for k in range(3)))
                        if any(lower[k]>upper[k]+0.002 for k in range(3)):continue
                        small=sa if sa['area']<=sb['area'] else sb
                        p=Vector(small['centroid'])
                        p=Vector(tuple(max(lower[k],min(upper[k],p[k])) for k in range(3)))
                        pairs.add(pair)
                        bond(a,b,p,min(sa['area'],sb['area']))
            for i,r in enumerate(rows):
                if r['foundation']:
                    bm=raws[i];low=[v.co for v in bm.verts if v.co.z<=minz+0.35]
                    point=sum(low,Vector())/len(low)
                    bond(i,-1,point,max(0.015,r['volume_cubic_metres']**(2/3)*0.35),True)
            # Fail visibly if an edited fragment no longer connects to a foundation.
            reached={b['pieceA'] for b in bonds if b['foundation']}
            for _ in rows:
                for b in bonds:
                    if b['pieceB']>=0 and (b['pieceA'] in reached or b['pieceB'] in reached):
                        reached.update((b['pieceA'],b['pieceB']))
            if len(reached)!=len(rows):
                raise RuntimeError(f'{sid}: disconnected unsupported pieces {set(range(len(rows)))-reached}')
            for bm in raws:bm.free()
            for r in rows:r.pop('seams')
            structures.append({'structure_id':sid,'frame_object':frame.name,'intact_object':intact.name,
                               'damage_mode':'normal','trigger':'Impact','fracture_profile':'authored_column_closed_seams_v1',
                               'repairable':True,'piece_count':len(rows),'pieces':rows,'bonds':bonds})
        bpy.ops.object.select_all(action='DESELECT')
        for o in export.objects:o.hide_set(False);o.select_set(True)
        bpy.context.view_layer.objects.active=next(iter(export.objects))
        bpy.ops.export_scene.fbx(filepath=str(OUT/'OuterStoneRing.fbx'),check_existing=False,use_selection=True,
            object_types={'EMPTY','MESH'},use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,
            use_custom_props=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',
            apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',path_mode='AUTO',embed_textures=False)
        sidecar={'schemaVersion':1,'structures':structures,'looseRocks':loose,'cosmeticRubble':[],
                 'sourceFile':bpy.data.filepath,'ignoredEmptyArtistObjects':ignored}
        (OUT/'OuterStoneRing.fracture.json').write_text(json.dumps(sidecar,indent=2),encoding='utf-8')
        # Keep inspectable baked proxies in Blender without changing the artist's
        # fracture objects or parenting. Hidden previews do not enter later exports.
        baked=bpy.data.collections.get('BAKED_OuterRing_Intact')
        if baked is None:
            baked=bpy.data.collections.new('BAKED_OuterRing_Intact')
            bpy.context.scene.collection.children.link(baked)
        for old in list(baked.objects):bpy.data.objects.remove(old,do_unlink=True)
        for s in structures:
            src=bpy.data.objects[s['intact_object']]
            saved=bpy.data.objects.new('BAKED_'+s['structure_id'],src.data.copy())
            baked.objects.link(saved);saved.matrix_world=src.matrix_world.copy()
            saved['source_structure']=s['structure_id'];saved['closed_connected_intact']=True
            saved.hide_viewport=True;saved.hide_render=True
            if saved.name in bpy.context.view_layer.objects:saved.hide_set(True)
        return {'file':str(OUT/'OuterStoneRing.fbx'),'structures':len(structures),
                'pieces':sum(s['piece_count'] for s in structures),'loose':len(loose),'ignoredEmpty':ignored}
    finally:
        # Only task-created temporary export data is removed. Authored objects stay intact.
        for o in list(export.objects):bpy.data.objects.remove(o,do_unlink=True)
        bpy.data.collections.remove(export)
        for mesh in list(bpy.data.meshes):
            if mesh.users==0 and mesh in meshes:bpy.data.meshes.remove(mesh)
        for o in previous_selected:o.select_set(True)
        bpy.context.view_layer.objects.active=previous_active
        bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,check_existing=False)


result=main()
