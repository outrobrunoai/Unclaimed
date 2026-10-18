# Inspect the raw Meshy garden post: parts, size, triangles, materials, loose islands, normals, and a preview render.
import bpy, bmesh, sys, math
from mathutils import Vector
SRC = "/mnt/dados/Projetos/Unity/Unclaimed/Blender/Entorno/meshy/poste_jardim_original.glb"
OUT = sys.argv[sys.argv.index("--") + 1]
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.import_scene.gltf(filepath=SRC)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
allmin = Vector((1e9,) * 3); allmax = Vector((-1e9,) * 3)
for o in meshes:
    bm = bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    mn = Vector((min(v.co[i] for v in bm.verts) for i in range(3))); mx = Vector((max(v.co[i] for v in bm.verts) for i in range(3)))
    allmin = Vector(map(min, allmin, mn)); allmax = Vector(map(max, allmax, mx))
    # loose islands
    seen = set(); islands = []
    for v in bm.verts:
        if v.index in seen: continue
        stack = [v]; seen.add(v.index); isl = []
        while stack:
            a = stack.pop(); isl.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen: seen.add(b.index); stack.append(b)
        zs = [p.co.z for p in isl]; xs = [p.co.x for p in isl]; ys = [p.co.y for p in isl]
        islands.append((len(isl), min(zs), max(zs), max(xs) - min(xs), max(ys) - min(ys)))
    nonman = sum(1 for e in bm.edges if not e.is_manifold)
    print("OBJ", o.name, "tris", tris, "verts", len(bm.verts), "mats", [m.name if m else None for m in o.data.materials],
          "uv", [l.name for l in o.data.uv_layers], "nonmanifold_edges", nonman)
    for i in sorted(islands, key=lambda t: t[1]): print("  island verts=%d z %.3f..%.3f  w %.3f x %.3f" % i)
    bm.free()
print("BOUNDS", tuple(round(c, 3) for c in allmin), tuple(round(c, 3) for c in allmax), "size", tuple(round(c, 3) for c in allmax - allmin))
# preview render: neutral grey clay, two views
sc = bpy.context.scene
try: sc.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError: sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 900, 1200
sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
sc.world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
h = allmax.z - allmin.z; c = (allmin + allmax) / 2
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = h * 1.1
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun); sun.rotation_euler = (0.8, 0.2, 0.6); sun.data.energy = 3
for name, d in (("frente", Vector((0, -1, 0.15))), ("lado", Vector((1, 0, 0.15)))):
    cam.location = c + d.normalized() * 10
    cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = OUT + "_" + name + ".png"
    bpy.ops.render.render(write_still=True)
