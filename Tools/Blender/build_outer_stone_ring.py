"""Build seven capped, individually fractured arches in the open Blender scene.

Reuses the Broken Crown fracture implementation. Never edits the source mesh or
the original arena file. Run via Blender MCP after appending BrokenCrownArena.
"""
import json
import math
from pathlib import Path
import sys
import types

import bpy
import bmesh
from mathutils import Euler, Matrix, Vector

PROJECT = Path(__file__).resolve().parents[2]
OUTPUT = PROJECT / 'ArtSource/Environment/OuterStoneRing'
module = types.ModuleType('outer_ring_arena_bake')
sys.modules[module.__name__] = module
code = (PROJECT / 'Tools/Blender/bake_broken_crown_arena.py').read_text(encoding='utf-8')
exec(compile(code.rsplit('result = main()', 1)[0], 'arena_bake_helpers', 'exec'), module.__dict__)


def collection(name, parent):
    item = bpy.data.collections.new(name)
    parent.children.link(item)
    return item


def hull2(points):
    pts = sorted(set((float(p.x), float(p.y)) for p in points))
    def cross(a, b, c):
        return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    lower, upper = [], []
    for p in pts:
        while len(lower)>1 and cross(lower[-2], lower[-1], p)<=0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper)>1 and cross(upper[-2], upper[-1], p)<=0:
            upper.pop()
        upper.append(p)
    return [Vector(p) for p in lower[:-1]+upper[:-1]]


def point_distance(p, a, b):
    ab = b-a
    t = max(0.0, min(1.0, (p-a).dot(ab)/ab.length_squared))
    return (p-(a+t*ab)).length


def clearance(poly, floor):
    return min([point_distance(p,a,b) for p in poly for a,b in zip(floor,floor[1:]+floor[:1])] +
               [point_distance(p,a,b) for p in floor for a,b in zip(poly,poly[1:]+poly[:1])])


def build():
    if bpy.data.collections.get('OuterStoneRing'):
        raise RuntimeError('OuterStoneRing already exists; inspect it before rebuilding.')
    src = bpy.data.objects['stone arch 3d model']
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    ring = collection('OuterStoneRing', bpy.context.scene.collection)
    archive = collection('99_Original_Arch', ring)
    archive.objects.link(src)
    for c in list(src.users_collection):
        if c != archive:
            c.objects.unlink(src)
    archive.hide_viewport = archive.hide_render = True
    for name in ('90_FRACTURE_BAKED', '99_AUTHORING'):
        c = bpy.data.collections.get(name)
        if c:
            c.hide_viewport = c.hide_render = True
    source_bm, repair = module.repair_source_mesh(src)
    for v in source_bm.verts:
        v.co = src.matrix_world @ v.co
    # +X is the inward pointing hook. Pivot at the centre of the foot.
    bottom = [v.co for v in source_bm.verts if v.co.z < 0.9]
    pivot = Vector(((min(v.x for v in bottom)+max(v.x for v in bottom))/2,
                    (min(v.y for v in bottom)+max(v.y for v in bottom))/2,
                    min(v.co.z for v in source_bm.verts)))
    for v in source_bm.verts:
        v.co -= pivot
    source_bm.normal_update()
    floor_obj = bpy.data.objects['Arena_FloorBase_INTACT']
    floor_points = [floor_obj.matrix_world@v.co for v in floor_obj.data.vertices]
    floor_hull = hull2(floor_points)
    center = Vector(((min(p.x for p in floor_points)+max(p.x for p in floor_points))/2,
                     (min(p.y for p in floor_points)+max(p.y for p in floor_points))/2,0))
    surface = bpy.data.materials['M_Arena_FractureSurface_Preview']
    interior = bpy.data.materials['M_Arena_FractureInterior_Preview']
    # Damage planes remove only terminal portions; the remaining shaft stays continuous.
    variants = [
        ('Full', None, None, (0.0, 0.0), 11),
        ('TipBroken', (2.45,0,6.7), (1,0.13,0.24), (1.0,-2.2), 13),
        ('CrownBroken', (0,0,6.1), (0.14,-0.17,1), (-1.6,1.8), 12),
        ('FullLeaning', None, None, (2.4,-1.2), 14),
        ('EdgeBroken', (2.05,0,6.9), (0.83,-0.35,0.46), (-0.7,-2.8), 12),
        ('UpperBroken', (0,0,5.65), (-0.16,0.20,1), (1.9,1.0), 10),
        ('SmallTipBroken', (2.8,0,6.55), (1,0.3,0.1), (-2.1,0.6), 13),
    ]
    rows = []
    for index, (label, cut, normal, tilt, count) in enumerate(variants):
        seed = 904170 + index*137
        group = collection(f'{index+1:02d}_Arch_{label}', ring)
        standing = collection(f'Arch_{index+1:02d}_Standing_Pieces', group)
        rubble = collection(f'Arch_{index+1:02d}_Detached_Pieces', group)
        root = bpy.data.objects.new(f'OuterArch_{index+1:02d}_ROOT', None)
        group.objects.link(root)
        root.empty_display_size = 0.3
        root['fracture_seed'] = seed
        root['tilt_degrees'] = list(tilt)
        root['damage_variant'] = label
        root['clearance_metres'] = 2.5
        root['fracture_method'] = 'BrokenCrown capped architectural plane split'
        theta = math.radians(90 + 180/7 + index*360/7)
        rotation = Matrix.Rotation(theta+math.pi,4,'Z') @ Euler((math.radians(tilt[0]), math.radians(tilt[1]),0)).to_matrix().to_4x4()
        # Use the full silhouette for spacing, even where a crown has fallen.
        full = [rotation@v.co for v in source_bm.verts]
        lo, hi = 10.0, 18.0
        for _ in range(30):
            radius = (lo+hi)/2
            translation = center + Vector((radius*math.cos(theta),radius*math.sin(theta),0))
            gap = clearance(hull2([p+translation for p in full]),floor_hull)
            if gap < 2.5:
                lo = radius
            else:
                hi = radius
        translation.z = -min(p.z for p in full) + 0.10
        root.matrix_world = Matrix.Translation(translation)@rotation
        main = source_bm.copy()
        detached = None
        if cut:
            main.free()
            no = Vector(normal).normalized()
            main = module.bisected_half(source_bm,Vector(cut),no,clear_outer=True,seam_id=900+index,seam_side=-1)
            detached = module.bisected_half(source_bm,Vector(cut),no,clear_outer=False,seam_id=900+index,seam_side=1)
            if len(detached.faces)<4 or module.volume(detached)<0.015:
                raise RuntimeError(f'{label}: damage plane failed to remove a solid chip.')
        temp_mesh = bpy.data.meshes.new('OuterArch_FractureInput')
        main.to_mesh(temp_mesh)
        main.free()
        temp = bpy.data.objects.new('OuterArch_FractureInput',temp_mesh)
        group.objects.link(temp)
        pieces, stats = module.fracture_bmesh(temp,target_count=count,seed=seed,floor_mode=False,
                                             profile='column_break_plane_split_v1',structure_id=f'outer_arch_{index+1:02d}')
        # A concave hook can intersect one cutting cell in multiple solid islands.
        # Each island must be its own independently selectable fracture object.
        connected_pieces = []
        for piece in pieces:
            components = module.connected_components(piece)
            if len(components) == 1:
                connected_pieces.append(piece)
            else:
                connected_pieces.extend(module.copy_vertex_component(piece,set(c)) for c in components)
                piece.free()
        pieces = connected_pieces
        count = len(pieces)
        made = []
        def create_piece(bm, serial, loose=False):
            if any(not e.is_manifold for e in bm.edges):
                raise RuntimeError('Open fracture detected')
            if bm.calc_volume(signed=True) < 0:
                bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
            # Terminal damage and future fractures both use the arena interior material.
            if cut and not loose:
                cut_no=Vector(normal).normalized()
                for f in bm.faces:
                    if all(abs((v.co-Vector(cut)).dot(cut_no))<0.0001 for v in f.verts):
                        f.material_index=1
            name=f'FR_outer_arch_{index+1:02d}_{serial:03d}' + ('_FALLEN' if loose else '')
            obj=module.create_mesh_object(name,bm,source=temp,collection=rubble if loose else standing,
                                           surface_material=surface,interior_material=interior)
            local=obj.matrix_world.copy()
            obj.parent=root
            obj.matrix_parent_inverse=Matrix.Identity(4)
            obj.matrix_basis=local
            obj.hide_set(False); obj.hide_render=False
            obj['outer_ring_generated']=True
            obj['earth_structure_id']=f'outer_arch_{index+1:02d}'
            obj['earth_piece_id']=serial
            obj['earth_fracture_seed']=seed
            obj['authored_detached']=loose
            obj['assembled_local_matrix']=list(sum((list(row) for row in local),[]))
            obj['closed_volume_m3']=module.volume(bm)
            if loose:
                obj.rotation_euler = Euler((1.15+index*0.08,0.3,0.5+index*0.41))
                obj.location=Vector((-0.6,2.4 if index%2 else -2.4,0))
                bpy.context.view_layer.update()
                lowest=min((obj.matrix_world@v.co).z for v in obj.data.vertices)
                mw=obj.matrix_world.copy(); mw.translation.z += 0.10-lowest; obj.matrix_world=mw
            else:
                # A small editable chamfer catches light at the already closed seams.
                bevel=obj.modifiers.new('Stone fracture edge 8mm','BEVEL')
                bevel.width=0.008; bevel.segments=1; bevel.limit_method='ANGLE'; bevel.angle_limit=0.65
            made.append(obj)
            bm.free()
            return obj
        for serial,p in enumerate(pieces):
            create_piece(p,serial)
        if detached:
            create_piece(detached,count,True)
        bpy.data.objects.remove(temp,do_unlink=True)
        bpy.data.meshes.remove(temp_mesh)
        rows.append({'arch':root.name,'variant':label,'seed':seed,'standingPieces':count,
                     'fallenPieces':int(cut is not None),'tiltDegrees':list(tilt),'radius':radius,
                     'fullSilhouetteClearance':gap,'stats':stats,'objects':[o.name for o in made]})
    source_bm.free()
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.context.scene.unit_settings.scale_length=1.0
    bpy.context.view_layer.update()
    report={'status':'PASS','sourcePreserved':src.name,'arenaReference':'BrokenCrownArena_Working.blend',
            'arches':rows,'count':7,'standingPieces':sum(r['standingPieces'] for r in rows),
            'fallenPieces':sum(r['fallenPieces'] for r in rows)}
    OUTPUT.mkdir(parents=True,exist_ok=True)
    (OUTPUT/'OuterStoneRing.validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    bpy.ops.object.select_all(action='DESELECT')
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active
            space.overlay.show_extras=False
            space.overlay.show_relationship_lines=False
            space.region_3d.view_location=Vector((0,0,2.8))
            space.region_3d.view_distance=49
            space.region_3d.view_rotation=Euler((math.radians(48),0,math.radians(25))).to_quaternion()
            space.clip_end=1000
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT/'OuterStoneRing_LayoutPreview.blend'),check_existing=False,copy=True)
    reference = bpy.data.collections.get('BrokenCrownArena')
    if reference and reference.name in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.unlink(reference)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT/'OuterStoneRing_Working.blend'),check_existing=False)
    return report


result=build()
