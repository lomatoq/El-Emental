"""Compare proxy normal strategies on disposable mesh copies; never save Blender.

Run through Blender MCP with __file__ set to this path. No authored object or
BAKED mesh is modified. Temporary mesh datablocks are always removed.
"""
import bpy
import datetime
import hashlib
import math
import struct

AREA_EPSILON = 1e-6
ANGLE = math.radians(30.0)


def fingerprint(mesh):
    digest = hashlib.sha256()
    for vertex in mesh.vertices:
        digest.update(struct.pack('<3f', *vertex.co))
    for loop in mesh.loops:
        digest.update(struct.pack('<2I', loop.vertex_index, loop.edge_index))
    for face in mesh.polygons:
        digest.update(struct.pack('<II?', face.loop_start, face.loop_total, face.use_smooth))
    for edge in mesh.edges:
        digest.update(bytes([edge.use_edge_sharp]))
    digest.update(bytes([mesh.has_custom_normals]))
    return digest.hexdigest()


def evaluate(mesh):
    mesh.calc_loop_triangles()
    significant_count = 0
    backward_count = 0
    backward_area = 0.0
    total_area = 0.0
    minimum_dot = 1.0
    zero_normal_corners = 0
    all_backward_count = 0
    all_backward_area = 0.0
    maximum_backward_area = 0.0
    worst = []
    for triangle in mesh.loop_triangles:
        a, b, c = [tuple(mesh.vertices[i].co) for i in triangle.vertices]
        u = [b[i] - a[i] for i in range(3)]
        v = [c[i] - a[i] for i in range(3)]
        cross = (u[1]*v[2] - u[2]*v[1], u[2]*v[0] - u[0]*v[2], u[0]*v[1] - u[1]*v[0])
        length = math.sqrt(sum(component*component for component in cross))
        area = length * 0.5
        total_area += area
        if length < 1e-30:
            continue
        dots = [sum(float(mesh.corner_normals[loop].vector[k])*cross[k]/length
                    for k in range(3)) for loop in triangle.loops]
        if min(dots) < -0.01:
            all_backward_count += 1
            all_backward_area += area
            maximum_backward_area = max(maximum_backward_area, area)
        if area < AREA_EPSILON:
            continue
        significant_count += 1
        zero_normal_corners += sum(mesh.corner_normals[loop].vector.length_squared < 0.25
                                   for loop in triangle.loops)
        minimum_dot = min(minimum_dot, min(dots))
        if min(dots) < -0.01:
            backward_count += 1
            backward_area += area
            worst.append({'face': triangle.polygon_index, 'areaM2': area, 'dot': min(dots)})
    edge_faces = [[] for _ in mesh.edges]
    for face in mesh.polygons:
        for loop in face.loop_indices:
            edge_faces[mesh.loops[loop].edge_index].append(face.index)
    curved_smooth_edges = 0
    curved_smooth_angle_sum = 0.0
    for edge, adjacent in zip(mesh.edges, edge_faces):
        if edge.use_edge_sharp or len(adjacent) != 2:
            continue
        a, b = [mesh.polygons[i] for i in adjacent]
        if not a.use_smooth or not b.use_smooth or min(a.area, b.area) < AREA_EPSILON:
            continue
        dot = mesh.polygon_normals[a.index].vector.dot(mesh.polygon_normals[b.index].vector)
        angle = math.acos(max(-1.0, min(1.0, dot)))
        if math.radians(1.0) <= angle <= ANGLE + 1e-5:
            curved_smooth_edges += 1
            curved_smooth_angle_sum += math.degrees(angle)
    return {
        'significantTriangles': significant_count,
        'backwardSignificantTriangles': backward_count,
        'backwardSignificantAreaM2': backward_area,
        'allBackwardTriangleCount': all_backward_count,
        'allBackwardTriangleAreaM2': all_backward_area,
        'allBackwardAreaFraction': all_backward_area / max(total_area, 1e-30),
        'maximumBackwardTriangleAreaM2': maximum_backward_area,
        'totalAreaM2': total_area,
        'minimumSignificantCornerDot': minimum_dot,
        'zeroNormalSignificantTriangleCorners': zero_normal_corners,
        'smoothFaces': sum(face.use_smooth for face in mesh.polygons),
        'flatFaces': sum(not face.use_smooth for face in mesh.polygons),
        'sharpEdges': sum(edge.use_edge_sharp for edge in mesh.edges),
        'curvedSmoothEdges1To30Degrees': curved_smooth_edges,
        'meanCurvedSmoothEdgeDegrees': curved_smooth_angle_sum / max(1, curved_smooth_edges),
        'worstTriangles': sorted(worst, key=lambda item: item['areaM2'], reverse=True)[:3],
    }


def main():
    rows = []
    for source in sorted((o for o in bpy.context.scene.objects
                          if o.type == 'MESH' and o.name.startswith('BAKED_outer_arch_')),
                         key=lambda o: o.name):
        before = fingerprint(source.data)
        variants = {}
        for strategy in ('current_source', 'angle_30', 'angle_30_isolated_slivers'):
            mesh = source.data.copy()
            mesh.name = 'TEMP_NormalDiagnostic_' + source.name
            try:
                stale = mesh.attributes.get('custom_normal')
                if stale is not None:
                    mesh.attributes.remove(stale)
                if strategy != 'current_source':
                    for face in mesh.polygons:
                        face.use_smooth = True
                    mesh.set_sharp_from_angle(angle=ANGLE)
                if strategy == 'angle_30_isolated_slivers':
                    sliver_faces = [face for face in mesh.polygons if face.area < AREA_EPSILON]
                    for face in sliver_faces:
                        face.use_smooth = False
                        for loop in face.loop_indices:
                            mesh.edges[mesh.loops[loop].edge_index].use_edge_sharp = True
                mesh.update()
                # Coincident vertices inside a larger polygon can also create a
                # zero normal. Isolate only that polygon, preserving all other
                # curved smoothing rather than forcing the whole proxy flat.
                guarded_faces = []
                if strategy == 'angle_30_isolated_slivers':
                    guarded_faces = [face for face in mesh.polygons if face.use_smooth and
                                     any(mesh.corner_normals[loop].vector.length_squared < 0.25
                                         for loop in face.loop_indices)]
                    for face in guarded_faces:
                        face.use_smooth = False
                        for loop in face.loop_indices:
                            mesh.edges[mesh.loops[loop].edge_index].use_edge_sharp = True
                    mesh.update()
                variants[strategy] = evaluate(mesh)
                variants[strategy]['zeroNormalGuardedFaces'] = len(guarded_faces)
            finally:
                bpy.data.meshes.remove(mesh)
        unchanged = before == fingerprint(source.data)
        if not unchanged:
            raise RuntimeError('Diagnostic unexpectedly changed source ' + source.name)
        rows.append({'object': source.name, 'sourceUnchanged': unchanged, 'variants': variants})
    return {
        'recordedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'sourceBlend': bpy.data.filepath,
        'blenderVersion': bpy.app.version_string,
        'method': 'Disposable mesh copies only; significant triangle winding vs corner normals; 30-degree smooth edges with every sub-threshold polygon and incident edge isolated, plus selective isolation of polygons with zero corner normals.',
        'significantTriangleAreaThresholdM2': AREA_EPSILON,
        'negativeDotThreshold': -0.01,
        'sourceObjectsModified': False,
        'temporaryDatablocksRemaining': sum(m.name.startswith('TEMP_NormalDiagnostic_') for m in bpy.data.meshes),
        'objects': rows,
    }


result = main()
