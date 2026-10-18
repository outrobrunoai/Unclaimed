# Point fixes on the raw Meshy garden post (text-to-3d 01a14bb6-1f50-7143-84c9-bb84a37d1f7f, meshy-t2.5 smart topology):
# Meshy put the globe halfway up and a colonial lantern on top. Kept: its base (flange, bell, six bolts), shaft and
# globe. Changed: shaft, lantern and finial above the globe's centre removed, the globe's top opening capped and its
# edges cut once onto a fitted ellipsoid (smooth outline), only the plain shaft section stretched so the globe sits on
# top at the real height, uniform scale to real size, three material slots, smooth normals, planar UVs. Saves the raw
# import and the adjusted model as separate .blend files and exports the FBX for Unity.
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

ROOT = "/mnt/dados/Projetos/Unity/Unclaimed"
SRC = ROOT + "/Blender/Entorno/meshy/poste_jardim_original.glb"
OUT_DIR = ROOT + "/Blender/Entorno/meshy"
UNITY = ROOT + "/Assets/Modelos/Entorno/PosteJardim"
NAME = "Poste_Jardim_Meshy"

# Heights in Meshy's normalised units (model 0.95 tall), read from the radius profile. The globe is its own closed
# island (z 0.018..0.166, open only where the shaft passed through its top); the shaft runs up through it to the lantern.
CUT = 0.10                       # inside the globe: shaft, lantern and finial above it go, the globe stays whole
SHAFT = (-0.33, -0.03)           # plain shaft between the base's bell and the globe's collar
SCALE = 1.8                      # shaft 0.072 m, globe 0.284 m, flange 0.32 m
HEIGHT = 2.86                    # globe top above the prefab origin, as in the post it replaces
# Concrete footing under the flange (prefab origin = old foot, 1 cm under the lawn): the post stands on it and its shaft
# is shortened by the same amount, so the globe stays where it was (centred on the existing light).
GROUND = 0.01
PAD = (0.44, -0.09, 0.09)        # side, bottom, top (8 cm above the lawn)

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.import_scene.gltf(filepath=SRC)
src = [o for o in bpy.data.objects if o.type == "MESH"][0]
bpy.ops.wm.save_as_mainfile(filepath=OUT_DIR + "/poste_jardim_original.blend")

me = src.data
me.transform(src.matrix_world)
src.matrix_world = Matrix.Identity(4)
bm = bmesh.new(); bm.from_mesh(me)

def islands(faces):
    seen, out = set(), []
    for f in faces:
        if f in seen: continue
        island, stack = [], [f]; seen.add(f)
        while stack:
            g = stack.pop(); island.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in seen: seen.add(h); stack.append(h)
        out.append(island)
    return out

# 1. Material slots by island: milky globe, galvanised bolts (small islands on the flange), metal for the rest.
globe = set()
for island in islands(bm.faces):
    zs = [v.co.z for g in island for v in g.verts]
    small_low = len(island) < 200 and max(zs) < -0.42
    is_globe = min(zs) > 0.01 and 0.15 < max(zs) < CUT + 0.1
    for g in island: g.material_index = 2 if small_low else 1 if is_globe else 0
    if is_globe: globe.update(island)

# 2. Remove the lantern: bisect everything but the globe inside the globe, drop the top, close the shaft's end.
geom = [f for f in bm.faces if f not in globe]
geom = list({v for f in geom for v in f.verts}) + list({e for f in geom for e in f.edges}) + geom
res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, CUT), plane_no=(0, 0, 1), clear_outer=True)
bmesh.ops.holes_fill(bm, edges=[e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)], sides=0)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
# close the globe's top opening with a fan whose centre sits on the sphere through the opening's rim
rim = [e for e in bm.edges if e.is_boundary and all(f.material_index == 1 for f in e.link_faces) and
       min(v.co.z for v in e.verts) > 0.12]
gz = [v.co.z for f in bm.faces if f.material_index == 1 for v in f.verts]
radius = (max(gz) - min(gz)) / 2 + 0.003
centre = max(gz) - radius
cap = bmesh.ops.holes_fill(bm, edges=rim, sides=0)["faces"]
for f in cap: f.material_index = 1
for v in bmesh.ops.poke(bm, faces=cap)["verts"]: v.co.z = centre + radius
# Meshy's globe has 18 segments, so its outline shows corners up close. Fit an axis-aligned ellipsoid to it, cut every
# globe edge once (globe only) and put all but the rim hidden in the collar onto that surface.
import numpy as np
def globe_verts(): return {v for f in bm.faces if f.material_index == 1 for v in f.verts}
pts = np.array([v.co[:] for v in globe_verts() if not v.is_boundary])
x, y, z = pts.T
A_, C_, D_, E_, F_ = np.linalg.lstsq(np.c_[x * x + y * y, z * z, x, y, z], np.ones(len(pts)), rcond=None)[0]
x0, y0, z0 = -D_ / (2 * A_), -E_ / (2 * A_), -F_ / (2 * C_)
G_ = 1 + A_ * (x0 * x0 + y0 * y0) + C_ * z0 * z0
ra, rc = math.sqrt(G_ / A_), math.sqrt(G_ / C_)
def on_ellipsoid(co):
    d = co - Vector((x0, y0, z0))
    return Vector((x0, y0, z0)) + d / math.sqrt((d.x * d.x + d.y * d.y) / (ra * ra) + d.z * d.z / (rc * rc))
dev = max((v.co - on_ellipsoid(v.co)).length for v in globe_verts() if not v.is_boundary)
gedges = [e for e in bm.edges if e.link_faces and all(f.material_index == 1 for f in e.link_faces)]
bmesh.ops.subdivide_edges(bm, edges=gedges, cuts=1, use_grid_fill=True)
for v in globe_verts():
    if not v.is_boundary: v.co = on_ellipsoid(v.co)
print("GLOBE ellipsoid centre (%.4f, %.4f, %.4f) semi-axes %.4f / %.4f, max move of Meshy verts %.4f (normalised units)" %
      (x0, y0, z0, ra, rc, dev))

# 3. Stretch only the shaft section so the globe ends at the original post's height.
a, b = SHAFT
bottom = min(v.co.z for v in bm.verts)
top = max(v.co.z for v in bm.verts)
k = ((HEIGHT - PAD[2]) / SCALE - ((a - bottom) + (top - b))) / (b - a)
for v in bm.verts:
    z = v.co.z
    if a < z < b: v.co.z = a + (z - a) * k
    elif z >= b: v.co.z = z + (b - a) * (k - 1)

# 4. Real size, flange standing on the footing.
bmesh.ops.scale(bm, vec=(SCALE, SCALE, SCALE), verts=bm.verts)
bottom = min(v.co.z for v in bm.verts)
bmesh.ops.translate(bm, vec=(0, 0, PAD[2] - bottom), verts=bm.verts)

# 5. Footing: chamfered concrete block, a thin mortar bed under the flange, anchor studs through the nuts and a ring of
# bare soil where the lawn meets the block. Slots 3 (concrete) and 4 (soil).
flange_r = max(math.hypot(v.co.x, v.co.y) for v in bm.verts if v.co.z < PAD[2] + 0.03)
bolts = []
for island in islands([f for f in bm.faces if f.material_index == 2]):
    if any(g.material_index != 2 for g in island): continue
    vs = {v for g in island for v in g.verts}
    bolts.append((sum(v.co.x for v in vs) / len(vs), sum(v.co.y for v in vs) / len(vs), max(v.co.z for v in vs)))
def tagged(geom, slot):
    faces = [f for f in geom if isinstance(f, bmesh.types.BMFace)]
    for f in faces: f.material_index = slot
    return faces
side, z0, z1 = PAD
cube = bmesh.ops.create_cube(bm, size=1.0)["verts"]
for v in cube: v.co = Vector((v.co.x * side, v.co.y * side, z0 + (v.co.z + 0.5) * (z1 - z0)))
pad_faces = list({f for v in cube for f in v.link_faces})
for f in pad_faces: f.material_index = 3
top_edges = [e for e in {e for f in pad_faces for e in f.edges} if all(v.co.z > z1 - 1e-4 for v in e.verts)]
bevel = bmesh.ops.bevel(bm, geom=top_edges, offset=0.012, segments=1,
                        affect="EDGES", profile=0.5)
for f in bevel["faces"]: f.material_index = 3
bed = bmesh.ops.create_cone(bm, cap_ends=True, segments=32, radius1=flange_r + 0.012, radius2=flange_r + 0.006, depth=0.008)
for v in bed["verts"]: v.co.z += z1 + 0.004 - 0.0005
tagged({f for v in bed["verts"] for f in v.link_faces}, 3)
for x, y, zt in bolts:
    stud = bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.0055, radius2=0.0045, depth=0.02)
    for v in stud["verts"]: v.co += Vector((x, y, zt + 0.006))
    tagged({f for v in stud["verts"] for f in v.link_faces}, 2)
ring = bmesh.ops.create_circle(bm, cap_ends=True, segments=40, radius=1.0)["verts"]
centre_v = None
for i, v in enumerate(ring):
    a = math.atan2(v.co.y, v.co.x)
    r = 0.315 + 0.02 * math.sin(3 * a + 0.7) + 0.012 * math.sin(7 * a + 2.1) + 0.008 * math.sin(13 * a)
    v.co = Vector((math.cos(a) * r, math.sin(a) * r, GROUND - 0.002))
soil = list({f for v in ring for f in v.link_faces})
inner = bmesh.ops.poke(bm, faces=soil)
for v in inner["verts"]: v.co.z = GROUND + 0.006
for f in inner["faces"]: f.material_index = 4
print("FOOTING flange r %.3f, %d anchor studs, pad %.2f m, %.2f m above the lawn" % (flange_r, len(bolts), side, z1 - GROUND))

gv = {v for f in bm.faces if f.material_index == 1 for v in f.verts}
gz = [v.co.z for v in gv]
cz = (min(gz) + max(gz)) / 2
rad = [(v.co - Vector((0, 0, cz))).length for v in gv]
rxy = [math.hypot(v.co.x, v.co.y) for v in gv if abs(v.co.z - cz) < 0.02]
print("GLOBE height %.3f centre z %.3f radius(equator band) %.3f..%.3f radius(3D, all) %.3f..%.3f" %
      (max(gz) - min(gz), cz, min(rxy), max(rxy), min(rad), max(rad)))
# The slots exist before the indices reach the mesh: clearing a mesh's slots afterwards resets every index to 0.
me.materials.clear()
for name, col in (("Metal_Preto", (0.02, 0.022, 0.022, 1)), ("Globo_Leitoso", (0.9, 0.88, 0.82, 1)), ("Parafusos", (0.5, 0.5, 0.5, 1)),
                  ("Concreto", (0.62, 0.62, 0.6, 1)), ("Terra", (0.3, 0.23, 0.16, 1))):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = col
    me.materials.append(m)
bm.to_mesh(me); bm.free()

# 6. Smooth normals with sharp creases kept (flange edges, collars), planar UVs (the materials are untextured).
obj = src; obj.name = NAME; me.name = NAME
bpy.context.view_layer.objects.active = obj; obj.select_set(True)
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
# the globe is one continuous surface: no hard edges on it (its low segment count would otherwise show as creases)
sharp = me.attributes.get("sharp_edge")
if sharp:
    edge_faces = {}
    for p in me.polygons:
        for ek in p.edge_keys: edge_faces.setdefault(ek, []).append(p.material_index == 1)
    for e in me.edges:
        flags = edge_faces.get(e.key, [])
        if flags and all(flags): sharp.data[e.index].value = False
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.01)
bpy.ops.object.mode_set(mode="OBJECT")
# concrete and soil use the generator's tiled materials: box-projected UVs in metres, like its world-UV meshes
uv = me.uv_layers.active.data
for p in me.polygons:
    if p.material_index < 3: continue
    n = p.normal
    ax = max(range(3), key=lambda i: abs(n[i]))
    for li in p.loop_indices:
        co = me.vertices[me.loops[li].vertex_index].co
        uv[li].uv = (co.y, co.z) if ax == 0 else (co.x, co.z) if ax == 1 else (co.x, co.y)

dims = obj.dimensions
tris = sum(len(p.vertices) - 2 for p in me.polygons)
per = [0] * len(me.materials)
for p in me.polygons: per[p.material_index] += len(p.vertices) - 2
print("ADJUSTED dims %.3f x %.3f x %.3f tris %d per-slot %s" % (dims.x, dims.y, dims.z, tris, per))
bpy.ops.wm.save_as_mainfile(filepath=OUT_DIR + "/poste_jardim_ajustado.blend")

os.makedirs(UNITY, exist_ok=True)
exp = obj.copy(); exp.data = obj.data.copy(); bpy.context.collection.objects.link(exp)
exp.rotation_euler = (0, 0, math.pi)          # Blender +X maps to Unity -X with this export: turn the copy
bpy.ops.object.select_all(action="DESELECT"); exp.select_set(True); bpy.context.view_layer.objects.active = exp
bpy.ops.object.transform_apply(rotation=True)
# Globe as its own object so Unity can give it its own renderer (no shadows, like the post it replaces).
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="DESELECT")
exp.active_material_index = 1; bpy.ops.object.material_slot_select(); bpy.ops.mesh.separate(type="SELECTED")
bpy.ops.mesh.select_all(action="DESELECT")
for i in (3, 4): exp.active_material_index = i; bpy.ops.object.material_slot_select()
bpy.ops.mesh.separate(type="SELECTED")
bpy.ops.object.mode_set(mode="OBJECT")
others = [o for o in bpy.context.selected_objects if o is not exp]
glb = next(o for o in others if any(p.material_index == 1 for p in o.data.polygons))
base = next(o for o in others if o is not glb)
for o, nm in ((exp, "Poste"), (glb, "Globo"), (base, "Apoio")): o.name = nm; o.data.name = nm
for o in (exp, glb, base):
    bpy.context.view_layer.objects.active = o; bpy.ops.object.material_slot_remove_unused()
for o in (exp, glb, base): o.select_set(True)
bpy.ops.export_scene.fbx(filepath=UNITY + "/" + NAME + ".fbx", use_selection=True, object_types={"MESH"},
                         axis_forward="-Z", axis_up="Y", bake_space_transform=True, apply_scale_options="FBX_SCALE_UNITS",
                         mesh_smooth_type="FACE", use_custom_props=False, bake_anim=False, add_leaf_bones=False, path_mode="STRIP")
for o in (exp, glb, base): bpy.data.objects.remove(o, do_unlink=True)

# Preview renders of the adjusted model (clay, slots tinted).
if "--" in sys.argv:
    out = sys.argv[sys.argv.index("--") + 1]
    sc = bpy.context.scene
    try: sc.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError: sc.render.engine = "BLENDER_EEVEE"
    for m in me.materials:
        m.use_nodes = True
        bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs["Base Color"].default_value = m.diffuse_color
    sc.render.resolution_x, sc.render.resolution_y = 600, 1200
    sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
    sc.world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = dims.z * 1.08
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
    sun.rotation_euler = (0.8, 0.2, 0.6); sun.data.energy = 3
    c = Vector((0, 0, dims.z / 2))
    cam.location = c + Vector((0, -10, 1.5)); cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = out + ".png"; bpy.ops.render.render(write_still=True)
    cam.data.ortho_scale = 0.7
    for nm, cc in (("_topo", Vector((0, 0, dims.z - 0.3))), ("_base", Vector((0, 0, 0.2)))):
        cam.location = cc + Vector((0, -10, 3)); cam.rotation_euler = (cc - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.resolution_x = sc.render.resolution_y = 700
        sc.render.filepath = out + nm + ".png"; bpy.ops.render.render(write_still=True)
