"""Exports Daniel (outfit C, dark jacket and jeans) to Blender/Daniel/export/Daniel.fbx for Unity (Humanoid).
Only outfit C's clothes stay; its masks hide the body under them. Prints each material's base-colour/normal images
so the Unity side can build its materials.
Run: blender -b --python Blender/Daniel/scripts/daniel_exportar.py"""
import os, shutil, json
import bpy

SRC = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(SRC, "export")
ROUPA = "C_jaqueta_escura"
OUTRAS = ("A_uniforme_limiar", "B_camiseta_jeans")
MANTER = {"Delete.male_casualsuit05", "Delete.shoes03"}

bpy.ops.wm.open_mainfile(filepath=os.path.join(SRC, "Daniel.blend"))
os.makedirs(os.path.join(OUT, "Texturas"), exist_ok=True)
for nome in OUTRAS:
    c = bpy.data.collections.get(nome)
    if c:
        for o in list(c.objects): bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(c)
corpo = bpy.data.objects["Daniel"]
for m in list(corpo.modifiers):
    if m.type == "MASK" and m.name.startswith("Delete."):
        if m.name in MANTER: m.show_viewport = m.show_render = True
        else: corpo.modifiers.remove(m)

# Images used by each material (copied next to the FBX; Unity builds the materials from this list).
mats = {}
for o in bpy.data.objects:
    if o.type != "MESH": continue
    for s in o.material_slots:
        m = s.material
        if m is None or m.name in mats or not m.use_nodes: continue
        imgs = {}
        for n in m.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image and n.image.filepath:
                p = bpy.path.abspath(n.image.filepath)
                if os.path.exists(p):
                    shutil.copy(p, os.path.join(OUT, "Texturas", os.path.basename(p)))
                    imgs[n.label or n.name] = os.path.basename(p)
        mats[m.name] = {"objeto": o.name, "imagens": imgs}
json.dump(mats, open(os.path.join(OUT, "materiais.json"), "w"), indent=1, ensure_ascii=False)

# Bind pose = T-pose (what Unity's Humanoid avatar expects): pose the rig with MPFB's t-pose.json, bake every mesh's
# modifiers (masks, armature) in that pose, apply the pose as the rest pose and skin the meshes to it again.
TPOSE = os.path.expanduser("~/.config/blender/5.2/extensions/blender_org/mpfb/data/poses/game_engine_fk/t-pose.json")
from mathutils import Euler
rig = bpy.data.objects["Daniel.rig"]
for pb in rig.pose.bones: pb.rotation_mode = "QUATERNION"
for name, e in json.load(open(TPOSE))["bone_rotations"].items():
    pb = rig.pose.bones.get(name)
    if pb: pb.rotation_quaternion = Euler(e, "XYZ").to_quaternion()
bpy.context.view_layer.update()
for o in [o for o in bpy.data.objects if o.type == "MESH" and not o.hide_render]:
    bpy.context.view_layer.objects.active = o
    if o.data.shape_keys: bpy.ops.object.shape_key_remove(all=True, apply_mix=True)   # keeps the MakeHuman proportions
    for m in list(o.modifiers):
        if m.show_render: bpy.ops.object.modifier_apply(modifier=m.name)
        else: o.modifiers.remove(m)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="POSE"); bpy.ops.pose.select_all(action="SELECT"); bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode="OBJECT")
for o in [o for o in bpy.data.objects if o.type == "MESH" and not o.hide_render]:
    o.parent = rig; a = o.modifiers.new("Armature", "ARMATURE"); a.object = rig
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

bpy.ops.object.select_all(action="DESELECT")
for o in bpy.data.objects:
    if o.type in ("MESH", "ARMATURE") and not o.hide_render: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Daniel.fbx"), use_selection=True, object_types={"MESH", "ARMATURE"},
                         use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", path_mode="STRIP",
                         mesh_smooth_type="FACE", armature_nodetype="NULL",
                         bake_space_transform=True)
print("EXPORT", [o.name for o in bpy.context.selected_objects])
print("MATS", json.dumps(mats, ensure_ascii=False))
